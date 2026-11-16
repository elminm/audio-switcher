// Core Audio interop: enumerate endpoints, set default device, Bluetooth audio connect/disconnect.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioSwitcher
{
    public enum EDataFlow { Render = 0, Capture = 1, All = 2 }
    public enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY
    {
        public Guid fmtid; public int pid;
        public PROPERTYKEY(Guid g, int p) { fmtid = g; pid = p; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KSPROPERTY { public Guid Set; public int Id; public int Flags; }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    class PolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int newState);
        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PROPERTYKEY key);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDeviceTopology
    {
        [PreserveSig] int GetConnectorCount(out int count);
        [PreserveSig] int GetConnector(int index, out IConnector connector);
        [PreserveSig] int GetSubunitCount(out int count);
        [PreserveSig] int GetSubunit(int index, out IntPtr subunit);
        [PreserveSig] int GetPartById(int id, out IntPtr part);
        [PreserveSig] int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSignalPath(IntPtr from, IntPtr to, bool rejectMixedPaths, out IntPtr parts);
    }

    [ComImport, Guid("9c2c4058-23f5-41de-877a-df3af236a09e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IConnector
    {
        [PreserveSig] int GetConnectorType(out int type);
        [PreserveSig] int GetDataFlow(out int flow);
        [PreserveSig] int ConnectTo(IntPtr other);
        [PreserveSig] int Disconnect();
        [PreserveSig] int IsConnected(out bool connected);
        [PreserveSig] int GetConnectedTo(out IntPtr other);
        [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IKsControl
    {
        [PreserveSig] int KsProperty(ref KSPROPERTY property, int propertyLength, IntPtr data, int dataLength, out int bytesReturned);
        [PreserveSig] int KsMethod(IntPtr method, int methodLength, IntPtr data, int dataLength, out int bytesReturned);
        [PreserveSig] int KsEvent(IntPtr evt, int eventLength, IntPtr data, int dataLength, out int bytesReturned);
    }

    // Undocumented but long-stable interface used by every default-device switcher (Win7+).
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }

    public class AudioEndpoint
    {
        public string Id, Name, BtName, KsFilterId;
        public EDataFlow Flow;
        public int State;          // 1 = active, 8 = unplugged
        public bool IsBluetooth;
        public bool Active { get { return State == 1; } }
    }

    public class NotificationClient : IMMNotificationClient
    {
        public Action Changed;
        void Fire() { var c = Changed; if (c != null) c(); }
        public void OnDeviceStateChanged(string id, int newState) { Fire(); }
        public void OnDeviceAdded(string id) { Fire(); }
        public void OnDeviceRemoved(string id) { Fire(); }
        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string id) { Fire(); }
        public void OnPropertyValueChanged(string id, PROPERTYKEY key) { }
    }

    public class CoreAudio
    {
        const int DEVICE_STATE_ACTIVE = 1, DEVICE_STATE_UNPLUGGED = 8, CLSCTX_ALL = 23;
        static PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static PROPERTYKEY PKEY_DeviceInterface_FriendlyName = new PROPERTYKEY(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);
        static readonly Guid KSPROPSETID_BtAudio = new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

        readonly IMMDeviceEnumerator en;
        IPolicyConfig policy;

        public CoreAudio() { en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject(); }

        public void Register(IMMNotificationClient client) { en.RegisterEndpointNotificationCallback(client); }

        public List<AudioEndpoint> ListAll()
        {
            var list = new List<AudioEndpoint>();
            AddFlow(list, EDataFlow.Render);
            AddFlow(list, EDataFlow.Capture);
            return list;
        }

        void AddFlow(List<AudioEndpoint> list, EDataFlow flow)
        {
            IMMDeviceCollection col;
            if (en.EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE | DEVICE_STATE_UNPLUGGED, out col) != 0 || col == null) return;
            try
            {
                int n; col.GetCount(out n);
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d;
                    if (col.Item(i, out d) != 0 || d == null) continue;
                    try
                    {
                        var e = new AudioEndpoint();
                        e.Flow = flow;
                        string id; d.GetId(out id); e.Id = id;
                        int st; d.GetState(out st); e.State = st;
                        IPropertyStore ps;
                        if (d.OpenPropertyStore(0, out ps) == 0 && ps != null)
                        {
                            e.Name = GetString(ps, PKEY_Device_FriendlyName);
                            e.BtName = GetString(ps, PKEY_DeviceInterface_FriendlyName);
                            Marshal.ReleaseComObject(ps);
                        }
                        if (string.IsNullOrEmpty(e.Name)) e.Name = id;
                        e.KsFilterId = GetKsFilterId(d);
                        string k = (e.KsFilterId ?? "").ToLowerInvariant();
                        e.IsBluetooth = k.Contains("bthenum") || k.Contains("bthhfenum");
                        if (e.IsBluetooth) e.BtName = NormalizeBtName(e.BtName, e.Name);
                        list.Add(e);
                    }
                    catch { }
                    finally { Marshal.ReleaseComObject(d); }
                }
            }
            finally { Marshal.ReleaseComObject(col); }
        }

        static string GetString(IPropertyStore ps, PROPERTYKEY key)
        {
            PROPVARIANT v;
            if (ps.GetValue(ref key, out v) != 0) return null;
            string s = v.vt == 31 ? Marshal.PtrToStringUni(v.p) : null; // VT_LPWSTR
            PropVariantClear(ref v);
            return s;
        }

        // The KS filter (driver) the endpoint is wired to; for Bluetooth it is a BTHENUM / BTHHFENUM device.
        string GetKsFilterId(IMMDevice d)
        {
            Guid iid = typeof(IDeviceTopology).GUID;
            object o;
            if (d.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0 || o == null) return null;
            var topo = (IDeviceTopology)o;
            try
            {
                IConnector c;
                if (topo.GetConnector(0, out c) != 0 || c == null) return null;
                try { string id; return c.GetDeviceIdConnectedTo(out id) == 0 ? id : null; }
                finally { Marshal.ReleaseComObject(c); }
            }
            finally { Marshal.ReleaseComObject(topo); }
        }

        static string NormalizeBtName(string iface, string endpointName)
        {
            string s = iface;
            if (string.IsNullOrEmpty(s))
            {
                int a = endpointName.IndexOf('('), b = endpointName.LastIndexOf(')');
                s = (a >= 0 && b > a) ? endpointName.Substring(a + 1, b - a - 1) : endpointName;
            }
            foreach (var suffix in new[] { " Hands-Free AG Audio", " Hands-Free AG", " Hands-Free", " Stereo", " Avrcp Transport" })
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - suffix.Length);
            return s.Trim();
        }

        public string GetDefaultId(EDataFlow flow, ERole role)
        {
            IMMDevice d;
            if (en.GetDefaultAudioEndpoint(flow, role, out d) != 0 || d == null) return null;
            try { string id; d.GetId(out id); return id; }
            finally { Marshal.ReleaseComObject(d); }
        }

        public void SetDefault(string id)
        {
            if (policy == null) policy = (IPolicyConfig)new PolicyConfigClient();
            policy.SetDefaultEndpoint(id, ERole.Console);
            policy.SetDefaultEndpoint(id, ERole.Multimedia);
            policy.SetDefaultEndpoint(id, ERole.Communications);
        }

        // Asks the Bluetooth audio driver to (re)connect or disconnect the device. Returns HRESULT.
        public int BtCommand(string ksFilterId, bool connect)
        {
            IMMDevice d;
            int hr = en.GetDevice(ksFilterId, out d);
            if (hr != 0 || d == null) return hr != 0 ? hr : -1;
            try
            {
                Guid iid = typeof(IKsControl).GUID;
                object o;
                hr = d.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o);
                if (hr != 0 || o == null) return hr != 0 ? hr : -1;
                var ks = (IKsControl)o;
                try
                {
                    var p = new KSPROPERTY { Set = KSPROPSETID_BtAudio, Id = connect ? 0 : 1, Flags = 1 }; // ONESHOT_RECONNECT / DISCONNECT, TYPE_GET
                    int ret;
                    return ks.KsProperty(ref p, Marshal.SizeOf(typeof(KSPROPERTY)), IntPtr.Zero, 0, out ret);
                }
                finally { Marshal.ReleaseComObject(ks); }
            }
            finally { Marshal.ReleaseComObject(d); }
        }
    }
}
