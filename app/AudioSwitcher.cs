// Audio Switcher — tray app that picks default output/input from a priority list.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Audio Switcher")]
[assembly: System.Reflection.AssemblyProduct("Audio Switcher")]
[assembly: System.Reflection.AssemblyDescription("Switches default audio devices by priority")]
[assembly: System.Reflection.AssemblyCopyright("elminm")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace AudioSwitcher
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--make-icon") { IconMaker.WriteIco(args[1]); return 0; }

            bool exitOnly = args.Contains("--exit"); // used by the installer/uninstaller
            bool created;
            using (var mutex = new Mutex(!exitOnly, "Local\\AudioSwitcher.Single", out created))
            {
                var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\AudioSwitcher.Show");
                var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\AudioSwitcher.Exit");
                if (exitOnly) { if (!created) exitEvent.Set(); return 0; }
                if (!created) { showEvent.Set(); return 0; }   // already running: bring its window up
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var form = new MainForm(showEvent, exitEvent);
                if (!args.Contains("--tray")) form.Show();
                Application.Run();
                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }

    class Config
    {
        public int PollSeconds = 3;
        public bool AutoSwitch = true;
        public bool KeepManual = true;
        public string Language = "en";
        public List<string> Output = new List<string>();
        public List<string> Input = new List<string>();

        public static Config Load(string path, out string error)
        {
            error = null;
            var c = new Config();
            if (!File.Exists(path)) return c;
            try
            {
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (d == null) { error = "config.json is not a JSON object"; return null; }
                object v;
                if (d.TryGetValue("pollSeconds", out v)) c.PollSeconds = Math.Max(1, Convert.ToInt32(v));
                if (d.TryGetValue("autoSwitch", out v)) c.AutoSwitch = Convert.ToBoolean(v);
                if (d.TryGetValue("keepManual", out v)) c.KeepManual = Convert.ToBoolean(v);
                if (d.TryGetValue("language", out v) && v != null) c.Language = v.ToString();
                if (d.TryGetValue("output", out v)) c.Output = ToList(v);
                if (d.TryGetValue("input", out v)) c.Input = ToList(v);
                return c;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        static List<string> ToList(object v)
        {
            var l = new List<string>();
            var arr = v as object[];
            if (arr != null) foreach (var o in arr) if (o != null && o.ToString().Trim() != "") l.Add(o.ToString());
            return l;
        }

        public string ToJson()
        {
            var js = new JavaScriptSerializer();
            var sb = new StringBuilder();
            sb.Append("{\r\n  \"language\": ").Append(js.Serialize(Language)).Append(",\r\n");
            sb.Append("  \"pollSeconds\": ").Append(PollSeconds).Append(",\r\n");
            sb.Append("  \"autoSwitch\": ").Append(AutoSwitch ? "true" : "false").Append(",\r\n");
            sb.Append("  \"keepManual\": ").Append(KeepManual ? "true" : "false").Append(",\r\n");
            AppendList(sb, js, "output", Output); sb.Append(",\r\n");
            AppendList(sb, js, "input", Input); sb.Append("\r\n}\r\n");
            return sb.ToString();
        }

        static void AppendList(StringBuilder sb, JavaScriptSerializer js, string key, List<string> l)
        {
            sb.Append("  \"").Append(key).Append("\": [");
            for (int i = 0; i < l.Count; i++)
                sb.Append(i == 0 ? "\r\n" : ",\r\n").Append("    ").Append(js.Serialize(l[i]));
            sb.Append(l.Count > 0 ? "\r\n  ]" : "]");
        }
    }

    class BtGroup
    {
        public string Name;
        public BtDevice Device;
        public List<AudioEndpoint> Endpoints = new List<AudioEndpoint>();
        public bool Connected { get { return Device.Connected; } }
    }

    class DBListView : ListView { public DBListView() { DoubleBuffered = true; } }

    class MainForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static readonly Color ActiveColor = Color.FromArgb(0, 128, 64);

        readonly string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        readonly string dir; // where config.json and the log live
        readonly string cfgPath, logPath;
        Config cfg;
        string lastSavedJson;
        CoreAudio audio;
        NotificationClient notif;
        NotifyIcon tray;
        Icon appIcon;
        FileSystemWatcher watcher;
        ListView lvOut, lvIn, lvBt;
        CheckBox chkAuto, chkStartup, chkKeep;
        HashSet<string> prevAvail;
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        readonly Dictionary<Control, string> infoTips = new Dictionary<Control, string>();

        // breaks a tooltip into short lines (WinForms tooltips don't wrap on their own)
        static string Wrap(string text, int width = 42)
        {
            var sb = new StringBuilder();
            foreach (var para in text.Split('\n'))
            {
                if (sb.Length > 0) sb.Append('\n');
                int line = 0;
                foreach (var w in para.Split(' '))
                {
                    if (w.Length == 0) continue;
                    if (line > 0 && line + 1 + w.Length > width) { sb.Append('\n'); line = 0; }
                    else if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(w); line += w.Length;
                }
            }
            return sb.ToString();
        }

        // small "i" icon next to a control; hovering either shows the localized tip
        Label Info(Control owner, string tipKey)
        {
            var l = new Label
            {
                Text = "ⓘ", AutoSize = true, Cursor = Cursors.Help,
                ForeColor = SystemColors.HotTrack,
                Font = new Font("Segoe UI Symbol", Font.SizeInPoints),
                Margin = new Padding(S(1), S(4), S(18), S(2))
            };
            infoTips[owner] = tipKey; infoTips[l] = tipKey;
            tips.SetToolTip(owner, Wrap(L.T(tipKey))); tips.SetToolTip(l, Wrap(L.T(tipKey)));
            return l;
        }
        ComboBox cmbLang;
        Label lblHx, lblNow;
        readonly List<KeyValuePair<object, string>> localized = new List<KeyValuePair<object, string>>();
        ListBox lbLog;
        System.Windows.Forms.Timer tick;
        System.Threading.Timer hxTimer;
        volatile bool dirty = true, configChanged, probeDone;
        volatile int hxState = -1;
        int lastHx = -2, probing;
        string lastSig;
        bool exiting, suppressEvents;
        List<AudioEndpoint> current = new List<AudioEndpoint>(), available = new List<AudioEndpoint>();
        List<BtDevice> btDevices = new List<BtDevice>();
        string btSig;
        int tickCount;
        Font boldFont;
        float scale = 1f;

        int S(int v) { return (int)Math.Round(v * scale); }

        public MainForm(EventWaitHandle showEvent, EventWaitHandle exitEvent)
        {
            // Portable mode (config.json next to the exe, e.g. a dev build) keeps data beside the exe;
            // an installed copy keeps it in %APPDATA%\AudioSwitcher so upgrades never touch it.
            if (File.Exists(Path.Combine(exeDir, "config.json"))) dir = exeDir;
            else
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioSwitcher");
                Directory.CreateDirectory(dir);
            }
            cfgPath = Path.Combine(dir, "config.json");
            logPath = Path.Combine(dir, "audio-switcher.log");
            try { if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024) File.Delete(logPath); } catch { }
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

            string example = Path.Combine(exeDir, "config.example.json");
            try { if (!File.Exists(cfgPath) && File.Exists(example)) File.Copy(example, cfgPath); } catch { }
            string err;
            cfg = Config.Load(cfgPath, out err);
            if (cfg == null) cfg = new Config();
            else if (!File.Exists(cfgPath)) SaveConfig();
            L.Lang = cfg.Language;

            appIcon = IconMaker.MakeIcon(SystemInformation.SmallIconSize.Width);
            BuildUi();
            BuildTray();
            if (err != null) Log(L.T("logCfgReadErr", err));
            Log(L.T("logStarted"));

            audio = new CoreAudio();
            notif = new NotificationClient();
            notif.Changed = () => { dirty = true; };
            audio.Register(notif);

            watcher = new FileSystemWatcher(dir, "config.json");
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
            FileSystemEventHandler onCfg = (s, e) => { configChanged = true; };
            watcher.Changed += onCfg; watcher.Created += onCfg;
            watcher.Renamed += (s, e) => { configChanged = true; };
            watcher.EnableRaisingEvents = true;

            hxTimer = new System.Threading.Timer(_ => ProbeHx(), null, 0, Math.Max(1, cfg.PollSeconds) * 1000);

            tick = new System.Windows.Forms.Timer();
            tick.Interval = 500;
            tick.Tick += OnTick;
            tick.Start();

            var h = Handle; // create window handle so BeginInvoke works while hidden
            var t = new Thread(() =>
            {
                while (true)
                {
                    int which = WaitHandle.WaitAny(new WaitHandle[] { showEvent, exitEvent });
                    try { BeginInvoke(which == 0 ? new Action(ShowWindowFront) : new Action(ExitApp)); } catch { }
                    if (which == 1) return;
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------------- UI ----------------

        void BuildUi()
        {
            Text = "Audio Switcher";
            Icon = appIcon;
            Font = new Font("Segoe UI", 9f);
            boldFont = new Font(Font, FontStyle.Bold);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(880), S(640));
            MinimumSize = new Size(S(660), S(500));
            StartPosition = FormStartPosition.CenterScreen;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(S(10)) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
            Controls.Add(root);

            // top bar
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 0, S(6)) };
            var flags = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            chkAuto = Loc(new CheckBox { AutoSize = true, Checked = cfg.AutoSwitch, Margin = new Padding(0, S(2), 0, S(2)) }, "autoSwitch");
            chkAuto.CheckedChanged += (s, e) =>
            {
                if (suppressEvents) return;
                cfg.AutoSwitch = chkAuto.Checked; SaveConfig();
                Log(L.T(cfg.AutoSwitch ? "logAutoOn" : "logAutoOff"));
                if (cfg.AutoSwitch) Evaluate(true);
            };
            chkKeep = Loc(new CheckBox { AutoSize = true, Checked = cfg.KeepManual, Margin = new Padding(0, S(2), 0, S(2)) }, "keepManual");
            chkKeep.CheckedChanged += (s, e) =>
            {
                if (suppressEvents) return;
                cfg.KeepManual = chkKeep.Checked; SaveConfig();
                Log(L.T(cfg.KeepManual ? "logKeepOn" : "logKeepOff"));
            };
            chkStartup = Loc(new CheckBox { AutoSize = true, Checked = GetStartup(), Margin = new Padding(0, S(2), 0, S(2)) }, "startWithWindows");
            chkStartup.CheckedChanged += (s, e) => { SetStartup(chkStartup.Checked); };
            var lblLang = Loc(new Label { AutoSize = true, Margin = new Padding(0, S(5), S(4), S(2)) }, "language");
            cmbLang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(120), Margin = new Padding(0, 0, S(20), 0) };
            cmbLang.Items.AddRange(L.Names);
            cmbLang.SelectedIndex = L.Index;
            cmbLang.SelectedIndexChanged += (s, e) =>
            {
                if (suppressEvents || cmbLang.SelectedIndex < 0) return;
                L.Lang = L.Codes[cmbLang.SelectedIndex];
                cfg.Language = L.Lang; SaveConfig();
                ApplyTexts();
            };
            lblHx = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, S(5), 0, S(2)) };
            flags.Controls.Add(chkAuto); flags.Controls.Add(Info(chkAuto, "autoSwitchTip"));
            flags.Controls.Add(chkKeep); flags.Controls.Add(Info(chkKeep, "keepManualTip"));
            flags.Controls.Add(chkStartup); flags.Controls.Add(Info(chkStartup, "startWithWindowsTip"));
            flags.Controls.Add(lblLang); flags.Controls.Add(cmbLang); flags.Controls.Add(lblHx);
            lblNow = new Label { AutoSize = true, Font = boldFont, Margin = new Padding(S(2), S(6), 0, 0) };
            top.Controls.Add(flags, 0, 0);
            top.Controls.Add(lblNow, 0, 1);
            root.Controls.Add(top, 0, 0);

            // priority lists
            var pri = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            pri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pri.Controls.Add(PriorityGroup("outputGroup", EDataFlow.Render, out lvOut), 0, 0);
            pri.Controls.Add(PriorityGroup("inputGroup", EDataFlow.Capture, out lvIn), 1, 0);
            root.Controls.Add(pri, 0, 1);

            // bluetooth
            var bt = Loc(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(S(8)) }, "btGroup");
            var btT = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            btT.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            btT.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lvBt = NewListView(new[] { "colDevice", "colStatus", "colType" }, new[] { 0, 120, 130 });
            lvBt.DoubleClick += (s, e) => { var g = SelectedBt(); if (g != null) BtAction(!g.Connected); };
            btT.Controls.Add(lvBt, 0, 0);
            var btB = ButtonColumn();
            btB.Controls.Add(Btn("btnConnect", (s, e) => BtAction(true)));
            btB.Controls.Add(Btn("btnDisconnect", (s, e) => BtAction(false)));
            btB.Controls.Add(Btn("btnRefresh", (s, e) => { dirty = true; }));
            btT.Controls.Add(btB, 1, 0);
            bt.Controls.Add(btT);
            root.Controls.Add(bt, 0, 2);

            // log
            var lg = Loc(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(S(8)) }, "logGroup");
            lbLog = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
            lg.Controls.Add(lbLog);
            root.Controls.Add(lg, 0, 3);
        }

        // Registers a control/column so its text follows the selected language.
        T Loc<T>(T target, string key) where T : class
        {
            localized.Add(new KeyValuePair<object, string>(target, key));
            SetText(target, L.T(key));
            return target;
        }

        static void SetText(object o, string s)
        {
            var c = o as Control;
            if (c != null) { c.Text = s; return; }
            var h = o as ColumnHeader;
            if (h != null) h.Text = s;
        }

        void ApplyTexts()
        {
            foreach (var kv in localized) SetText(kv.Key, L.T(kv.Value));
            foreach (var kv in infoTips) tips.SetToolTip(kv.Key, Wrap(L.T(kv.Value)));
            if (audio != null) RefreshUi();
        }

        Control PriorityGroup(string titleKey, EDataFlow flow, out ListView lvOutParam)
        {
            var gb = Loc(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(S(8)) }, titleKey);
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var lv = NewListView(new[] { "#", "colDevice", "colStatus" }, new[] { 26, 0, 110 });
            lv.DoubleClick += (s, e) => UseNow(lv, flow);
            t.Controls.Add(lv, 0, 0);
            var b = ButtonColumn();
            b.Controls.Add(Btn("btnUp", (s, e) => MoveItem(lv, flow, -1)));
            b.Controls.Add(Btn("btnDown", (s, e) => MoveItem(lv, flow, +1)));
            b.Controls.Add(Btn("btnAdd", (s, e) => ShowAddMenu((Control)s, lv, flow)));
            b.Controls.Add(Btn("btnRemove", (s, e) => RemoveItem(lv, flow)));
            b.Controls.Add(Btn("btnUseNow", (s, e) => UseNow(lv, flow)));
            t.Controls.Add(b, 1, 0);
            gb.Controls.Add(t);
            lvOutParam = lv;
            return gb;
        }

        FlowLayoutPanel ButtonColumn()
        {
            return new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(S(6), 0, 0, 0) };
        }

        Button Btn(string key, EventHandler click)
        {
            var b = Loc(new Button(), key);
            b.Width = S(118); b.Height = S(30);
            b.Margin = new Padding(0, 0, 0, S(6)); b.TextAlign = ContentAlignment.MiddleLeft; b.Padding = new Padding(S(6), 0, 0, 0);
            b.Click += click;
            return b;
        }

        ListView NewListView(string[] cols, int[] widths)
        {
            var lv = new DBListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            for (int i = 0; i < cols.Length; i++)
            {
                var h = lv.Columns.Add(cols[i], S(widths[i] == 0 ? 150 : widths[i]));
                if (cols[i] != "#") Loc(h, cols[i]);
            }
            lv.Tag = Array.IndexOf(widths, 0);
            lv.Resize += (s, e) => FitColumns(lv);
            lv.HandleCreated += (s, e) => FitColumns(lv);
            return lv;
        }

        void FitColumns(ListView lv)
        {
            int fill = (int)lv.Tag, other = 0;
            for (int i = 0; i < lv.Columns.Count; i++) if (i != fill) other += lv.Columns[i].Width;
            int w = lv.ClientSize.Width - other - 4;
            lv.Columns[fill].Width = Math.Max(S(80), w);
        }

        void BuildTray()
        {
            tray = new NotifyIcon { Icon = appIcon, Text = "Audio Switcher", Visible = true };
            var menu = new ContextMenuStrip();
            menu.Items.Add("...");
            menu.Opening += (s, e) => { BuildTrayMenu(menu); e.Cancel = false; };
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindowFront(); };
        }

        void BuildTrayMenu(ContextMenuStrip m)
        {
            m.Items.Clear();
            var open = new ToolStripMenuItem(L.T("trayOpen"), null, (s, e) => ShowWindowFront());
            open.Font = new Font(m.Font, FontStyle.Bold);
            m.Items.Add(open);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(DeviceSubmenu(L.T("output"), EDataFlow.Render));
            m.Items.Add(DeviceSubmenu(L.T("input"), EDataFlow.Capture));
            m.Items.Add(new ToolStripSeparator());
            var auto = new ToolStripMenuItem(L.T("autoSwitchShort")) { Checked = cfg.AutoSwitch };
            auto.Click += (s, e) => { chkAuto.Checked = !chkAuto.Checked; };
            m.Items.Add(auto);
            var keep = new ToolStripMenuItem(L.T("keepManual")) { Checked = cfg.KeepManual };
            keep.Click += (s, e) => { chkKeep.Checked = !chkKeep.Checked; };
            m.Items.Add(keep);
            var lang = new ToolStripMenuItem(L.T("language").TrimEnd(':'));
            for (int i = 0; i < L.Names.Length; i++)
            {
                int li = i;
                lang.DropDownItems.Add(new ToolStripMenuItem(L.Names[i], null, (s, e) => { cmbLang.SelectedIndex = li; }) { Checked = li == L.Index });
            }
            m.Items.Add(lang);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem(L.T("trayExit"), null, (s, e) => ExitApp()));
        }

        ToolStripMenuItem DeviceSubmenu(string title, EDataFlow flow)
        {
            var sub = new ToolStripMenuItem(title);
            string def = SafeDefault(flow);
            foreach (var e in current.Where(x => x.Flow == flow && x.Active))
            {
                var ep = e;
                var it = new ToolStripMenuItem(ep.Name) { Checked = ep.Id == def };
                it.Click += (s, a) => { audio.SetDefault(ep.Id); Log(L.T("logManual", ep.Name)); dirty = true; };
                sub.DropDownItems.Add(it);
            }
            if (sub.DropDownItems.Count == 0) sub.DropDownItems.Add(new ToolStripMenuItem(L.T("trayNoDevices")) { Enabled = false });
            return sub;
        }

        void ShowWindowFront()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }

        void ExitApp()
        {
            exiting = true;
            tick.Stop();
            hxTimer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }

        // ---------------- logic ----------------

        void ProbeHx()
        {
            if (Interlocked.Exchange(ref probing, 1) == 1) return;
            try
            {
                int r = -1;
                try { r = HyperXProbe.GetConnected(800, false); } catch { }
                hxState = r;
                probeDone = true;
            }
            finally { Interlocked.Exchange(ref probing, 0); }
        }

        void OnTick(object sender, EventArgs e)
        {
            if (configChanged) { configChanged = false; ReloadConfig(); }
            if (!probeDone) return; // wait for first headset check so we don't switch twice at startup
            int hx = hxState;
            if (hx != lastHx)
            {
                if (lastHx != -2 || hx != -1) Log(L.T("hxLabel", HxText(hx)));
                lastHx = hx;
                dirty = true;
            }
            if (++tickCount % 6 == 0 && BtSignature(LoadBt()) != btSig) dirty = true; // BT link changes every ~3s
            if (dirty) { dirty = false; Evaluate(false); }
        }

        static string HxText(int hx) { return L.T(hx == 1 ? "hxOn" : hx == 0 ? "hxOff" : "hxNone"); }

        static bool Matches(AudioEndpoint e, string pattern)
        {
            return pattern.Length > 0 && e.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool HiddenByHeadset(AudioEndpoint e)
        {
            return lastHx == 0 && e.Name.IndexOf("HyperX", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        List<string> Pri(EDataFlow f) { return f == EDataFlow.Render ? cfg.Output : cfg.Input; }

        string SafeDefault(EDataFlow f) { try { return audio.GetDefaultId(f, ERole.Console); } catch { return null; } }

        void Evaluate(bool forceApply)
        {
            try
            {
                btDevices = LoadBt();
                btSig = BtSignature(btDevices);
                current = audio.ListAll();
                available = current.Where(x => x.Active && !HiddenByHeadset(x)).ToList();
                string sig = string.Join("|", available.Select(x => x.Id).OrderBy(x => x));
                bool changed = sig != lastSig;
                if (changed && lastSig != null) Log(L.T("logDevicesChanged"));
                lastSig = sig;
                var prev = prevAvail;
                prevAvail = new HashSet<string>(available.Select(x => x.Id));
                if (cfg.AutoSwitch && (changed || forceApply))
                {
                    // Smart mode: on a plain device change, keep the user's choice unless it is gone
                    // or a newly appeared device outranks it. Forced re-applies always use full priority.
                    HashSet<string> smartPrev = (cfg.KeepManual && !forceApply) ? prev : null;
                    ApplyFlow(EDataFlow.Render, "output", smartPrev);
                    ApplyFlow(EDataFlow.Capture, "input", smartPrev);
                }
                RefreshUi();
            }
            catch (Exception ex) { Log(L.T("logError", ex.Message)); }
        }

        int Rank(AudioEndpoint e)
        {
            var l = Pri(e.Flow);
            for (int i = 0; i < l.Count; i++) if (Matches(e, l[i])) return i;
            return int.MaxValue;
        }

        void ApplyFlow(EDataFlow flow, string label, HashSet<string> prev)
        {
            AudioEndpoint target = null;
            string curId = SafeDefault(flow);
            var cur = curId == null ? null : available.FirstOrDefault(x => x.Id == curId);
            if (prev != null && cur != null)
            {
                // current device still here: switch only to a new device with higher priority
                int curRank = Rank(cur);
                target = available.Where(x => x.Flow == flow && !prev.Contains(x.Id) && Rank(x) < curRank)
                                  .OrderBy(x => Rank(x)).FirstOrDefault();
                if (target == null) return;
            }
            else
            {
                foreach (var p in Pri(flow))
                {
                    target = available.FirstOrDefault(x => x.Flow == flow && Matches(x, p));
                    if (target != null) break;
                }
            }
            if (target == null) return;
            if (audio.GetDefaultId(flow, ERole.Console) != target.Id || audio.GetDefaultId(flow, ERole.Communications) != target.Id)
            {
                audio.SetDefault(target.Id);
                Log(L.T("logSwitched", L.T(label), target.Name));
            }
        }

        void RefreshUi()
        {
            string defOut = SafeDefault(EDataFlow.Render), defIn = SafeDefault(EDataFlow.Capture);
            FillPriority(lvOut, EDataFlow.Render, defOut);
            FillPriority(lvIn, EDataFlow.Capture, defIn);
            FillBt();

            var o = current.FirstOrDefault(x => x.Id == defOut);
            var i = current.FirstOrDefault(x => x.Id == defIn);
            string on = o != null ? o.Name : "—", inn = i != null ? i.Name : "—";
            lblNow.Text = L.T("output") + ":  " + on + "        " + L.T("input") + ":  " + inn;
            FitColumns(lvOut); FitColumns(lvIn); FitColumns(lvBt);
            lblHx.Text = lastHx < 0 ? "" : L.T("hxLabel", HxText(lastHx));
            string tip = L.T("output") + ": " + on + "\n" + L.T("input") + ": " + inn;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        void FillPriority(ListView lv, EDataFlow flow, string def)
        {
            int sel = SelIndex(lv);
            var pri = Pri(flow);
            lv.BeginUpdate();
            lv.Items.Clear();
            for (int i = 0; i < pri.Count; i++)
            {
                string p = pri[i];
                var matches = current.Where(x => x.Flow == flow && Matches(x, p)).ToList();
                var avail = available.FirstOrDefault(x => x.Flow == flow && Matches(x, p));
                string name = avail != null ? avail.Name : matches.Count > 0 ? matches[0].Name : p;
                string status;
                Color color = SystemColors.GrayText;
                bool isDef = avail != null && avail.Id == def;
                if (avail != null) { status = L.T(isDef ? "stActive" : "stConnected"); color = isDef ? ActiveColor : SystemColors.WindowText; }
                else if (matches.Any(m => m.Active)) status = L.T("stHeadsetOff");
                else if (matches.Count > 0) status = L.T("stDisconnected");
                else status = L.T("stNotFound");
                var it = new ListViewItem(new[] { (i + 1).ToString(), name, status });
                it.ForeColor = color;
                if (isDef) it.Font = boldFont;
                it.ToolTipText = L.T("matchTip", p);
                lv.Items.Add(it);
            }
            lv.ShowItemToolTips = true;
            SelectRow(lv, sel);
            lv.EndUpdate();
        }

        static List<BtDevice> LoadBt()
        {
            try { return Bluetooth.ListPaired(); } catch { return new List<BtDevice>(); }
        }

        static string BtSignature(List<BtDevice> l)
        {
            return string.Join("|", l.Select(d => d.Address + (d.Connected ? "+" : "-")).OrderBy(x => x));
        }

        static bool BelongsTo(AudioEndpoint e, BtDevice d)
        {
            return string.Equals(e.BtName, d.Name, StringComparison.OrdinalIgnoreCase)
                || e.Name.IndexOf("(" + d.Name + ")", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void FillBt()
        {
            string selName = SelectedBt() != null ? SelectedBt().Name : null;
            var groups = new List<BtGroup>();
            foreach (var d in btDevices)
            {
                var g = new BtGroup { Name = d.Name, Device = d };
                g.Endpoints.AddRange(current.Where(x => BelongsTo(x, d)));
                if (d.IsAudio || g.Endpoints.Count > 0) groups.Add(g);
            }
            groups = groups.OrderBy(g => g.Connected ? 0 : 1).ThenBy(g => g.Name).ToList();
            lvBt.BeginUpdate();
            lvBt.Items.Clear();
            foreach (var g in groups)
            {
                bool hasOut = g.Endpoints.Any(x => x.Flow == EDataFlow.Render), hasIn = g.Endpoints.Any(x => x.Flow == EDataFlow.Capture);
                string kind = L.T(hasOut && hasIn ? "kindBoth" : hasOut ? "kindOut" : hasIn ? "kindIn" : "kindAudio");
                var it = new ListViewItem(new[] { g.Name, L.T(g.Connected ? "stBtConnected" : "stDisconnected"), kind });
                it.ForeColor = g.Connected ? ActiveColor : SystemColors.GrayText;
                it.Tag = g;
                lvBt.Items.Add(it);
                if (g.Name == selName) it.Selected = true;
            }
            if (groups.Count == 0) lvBt.Items.Add(new ListViewItem(new[] { L.T("noBt"), "", "" }) { ForeColor = SystemColors.GrayText });
            lvBt.EndUpdate();
        }

        static int SelIndex(ListView lv) { return lv.SelectedIndices.Count > 0 ? lv.SelectedIndices[0] : -1; }

        static void SelectRow(ListView lv, int i)
        {
            if (i < 0 || i >= lv.Items.Count) return;
            lv.Items[i].Selected = true;
            lv.Items[i].Focused = true;
            lv.EnsureVisible(i);
        }

        BtGroup SelectedBt() { return lvBt.SelectedItems.Count > 0 ? lvBt.SelectedItems[0].Tag as BtGroup : null; }

        // ---------------- actions ----------------

        void MoveItem(ListView lv, EDataFlow flow, int delta)
        {
            var l = Pri(flow);
            int i = SelIndex(lv), j = i + delta;
            if (i < 0 || j < 0 || j >= l.Count) return;
            var t = l[i]; l[i] = l[j]; l[j] = t;
            SaveConfig();
            lv.SelectedIndices.Clear();
            Evaluate(true);
            SelectRow(lv, j);
        }

        void RemoveItem(ListView lv, EDataFlow flow)
        {
            var l = Pri(flow);
            int i = SelIndex(lv);
            if (i < 0) return;
            Log(L.T("logRemoved", l[i]));
            l.RemoveAt(i);
            SaveConfig();
            Evaluate(true);
            SelectRow(lv, Math.Min(i, l.Count - 1));
        }

        void ShowAddMenu(Control anchor, ListView lv, EDataFlow flow)
        {
            var l = Pri(flow);
            var m = new ContextMenuStrip();
            var names = current.Where(x => x.Flow == flow && !l.Any(p => Matches(x, p)))
                               .GroupBy(x => x.Name).Select(g => g.OrderBy(x => x.Active ? 0 : 1).First())
                               .OrderBy(x => x.Active ? 0 : 1).ThenBy(x => x.Name);
            foreach (var e in names)
            {
                var ep = e;
                var it = new ToolStripMenuItem(ep.Name + (ep.Active ? "" : L.T("addNotConnected")));
                it.Click += (s, a) =>
                {
                    l.Add(ep.Name); SaveConfig(); Log(L.T("logAdded", ep.Name));
                    Evaluate(true); SelectRow(lv, l.Count - 1);
                };
                m.Items.Add(it);
            }
            if (m.Items.Count == 0) m.Items.Add(new ToolStripMenuItem(L.T("addAllListed")) { Enabled = false });
            m.Show(anchor, new Point(0, anchor.Height));
        }

        void UseNow(ListView lv, EDataFlow flow)
        {
            int i = SelIndex(lv);
            if (i < 0) return;
            string p = Pri(flow)[i];
            var e = available.FirstOrDefault(x => x.Flow == flow && Matches(x, p));
            if (e == null) { Log(L.T("logNotConnected", p)); return; }
            audio.SetDefault(e.Id);
            Log(L.T("logManual", e.Name));
            dirty = true;
        }

        void BtAction(bool connect)
        {
            var g = SelectedBt();
            if (g == null) return;
            if (connect == g.Connected) { Log(L.T(connect ? "logBtAlreadyOn" : "logBtAlreadyOff", g.Name)); return; }
            Log(L.T(connect ? "logBtConnecting" : "logBtDisconnecting", g.Name));
            Cursor = Cursors.WaitCursor;
            bool ok = false;
            try
            {
                if (!connect) ok = Bluetooth.Disconnect(g.Device.Address); // works with Intel BT offload too
                // Re-read endpoints: after a disconnect they move to the standard BTHENUM driver, which handles reconnect.
                var ids = audio.ListAll().Where(x => BelongsTo(x, g.Device))
                               .Select(x => x.KsFilterId).Where(x => x != null).Distinct().ToList();
                if (!ok)
                    foreach (var id in ids)
                        if (audio.BtCommand(id, connect) >= 0) ok = true;
                if (connect && ids.Count == 0) Log(L.T("logBtNoEndpoint", g.Name));
            }
            catch (Exception ex) { Log(L.T("logError", ex.Message)); }
            finally { Cursor = Cursors.Default; }
            if (!ok) Log(L.T("logBtFailed"));
            dirty = true;
        }

        // ---------------- config / startup / log ----------------

        void SaveConfig()
        {
            try
            {
                lastSavedJson = cfg.ToJson();
                File.WriteAllText(cfgPath, lastSavedJson, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log(L.T("logCfgWriteErr", ex.Message)); }
        }

        void ReloadConfig()
        {
            string text;
            try { text = File.ReadAllText(cfgPath, Encoding.UTF8); }
            catch { configChanged = true; return; } // file busy, retry next tick
            if (text == lastSavedJson) return;
            string err;
            var c = Config.Load(cfgPath, out err);
            if (c == null) { Log(L.T("logCfgInvalid", err)); return; }
            cfg = c;
            lastSavedJson = text;
            L.Lang = cfg.Language;
            suppressEvents = true; chkAuto.Checked = cfg.AutoSwitch; chkKeep.Checked = cfg.KeepManual; cmbLang.SelectedIndex = L.Index; suppressEvents = false;
            ApplyTexts();
            Log(L.T("logCfgReloaded"));
            Evaluate(true);
        }

        static bool GetStartup()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("AudioSwitcher") != null;
        }

        void SetStartup(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue("AudioSwitcher", "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue("AudioSwitcher", false);
                }
                Log(L.T(on ? "logStartupOn" : "logStartupOff"));
            }
            catch (Exception ex) { Log(L.T("logStartupErr", ex.Message)); }
        }

        void Log(string m)
        {
            var now = DateTime.Now;
            try { File.AppendAllText(logPath, now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + m + "\r\n", Encoding.UTF8); } catch { }
            if (lbLog == null) return;
            lbLog.Items.Add(now.ToString("HH:mm:ss") + "   " + m);
            while (lbLog.Items.Count > 300) lbLog.Items.RemoveAt(0);
            lbLog.TopIndex = lbLog.Items.Count - 1;
        }
    }

    static class IconMaker
    {
        public static Bitmap Draw(int s)
        {
            var b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var rect = new RectangleF(s * 0.03f, s * 0.03f, s * 0.94f, s * 0.94f);
                using (var path = RoundRect(rect, s * 0.22f))
                using (var br = new LinearGradientBrush(rect, Color.FromArgb(59, 130, 246), Color.FromArgb(29, 78, 216), 90f))
                    g.FillPath(br, path);
                Func<double, double, PointF> P = (x, y) => new PointF((float)(x * s), (float)(y * s));
                g.FillPolygon(Brushes.White, new[] { P(0.18, 0.39), P(0.32, 0.39), P(0.50, 0.23), P(0.50, 0.77), P(0.32, 0.61), P(0.18, 0.61) });
                using (var pen = new Pen(Color.White, Math.Max(1.4f, s * 0.075f)))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, s * 0.40f, s * 0.36f, s * 0.28f, s * 0.28f, -50, 100);
                    g.DrawArc(pen, s * 0.32f, s * 0.22f, s * 0.52f, s * 0.56f, -50, 100);
                }
            }
            return b;
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Icon MakeIcon(int size)
        {
            using (var bmp = Draw(Math.Max(16, size))) return (Icon)Icon.FromHandle(bmp.GetHicon()).Clone();
        }

        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (var bmp = Draw(s)) using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }
}
