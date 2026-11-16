// Queries HyperX Cloud III Wireless dongle (03F0:05B7 / 0C9D) for headset wireless-connected state.
// Protocol from LennardKittner/HyperHeadset (src/devices/cloud_iii_wireless.rs):
//   send report [0x66, 130, 0...]  -> reply [0x66, 130|11, connected(1/0), ...]
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

public static class HyperXProbe
{
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid g; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort Version; }
    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort a, b, c, d, e, f, g, h, i, j;
    }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, ref HIDP_CAPS c);
    [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr w, int f);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int req, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(string n, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr t);

    static readonly ushort[] Pids = { 0x05B7, 0x0C9D };

    static List<string> Paths()
    {
        var list = new List<string>();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
        try {
            var d = new SP_DEVICE_INTERFACE_DATA(); d.cbSize = Marshal.SizeOf(d);
            for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d); i++) {
                int req; SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal(req);
                try {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero)) {
                        string p = Marshal.PtrToStringAuto(new IntPtr(buf.ToInt64() + 4));
                        if (p.ToLowerInvariant().Contains("vid_03f0")) list.Add(p);
                    }
                } finally { Marshal.FreeHGlobal(buf); }
            }
        } finally { SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    // Returns: 1 = headset connected, 0 = headset off / not linked, -1 = dongle not found / no answer.
    public static int GetConnected(int timeoutMs, bool verbose)
    {
        foreach (string path in Paths()) {
            using (SafeFileHandle h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero)) {
                if (h.IsInvalid) continue;
                var a = new HIDD_ATTRIBUTES(); a.Size = Marshal.SizeOf(a);
                if (!HidD_GetAttributes(h, ref a) || Array.IndexOf(Pids, a.ProductID) < 0) continue;
                IntPtr pp; if (!HidD_GetPreparsedData(h, out pp)) continue;
                var caps = new HIDP_CAPS(); HidP_GetCaps(pp, ref caps); HidD_FreePreparsedData(pp);
                if (verbose) Console.WriteLine("PID {0:X4} page {1:X4} usage {2:X4} in {3} out {4}  {5}",
                    a.ProductID, caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, path);
                if (caps.UsagePage < 0xFF00 || caps.OutputReportByteLength == 0) continue;

                try {
                    using (var fs = new FileStream(h, FileAccess.ReadWrite, 1, true)) {
                        var outBuf = new byte[caps.OutputReportByteLength];
                        outBuf[0] = 0x66; outBuf[1] = 130;
                        fs.Write(outBuf, 0, outBuf.Length);
                        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                        while (DateTime.UtcNow < deadline) {
                            var inBuf = new byte[caps.InputReportByteLength];
                            var ar = fs.BeginRead(inBuf, 0, inBuf.Length, null, null);
                            int left = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                            if (left <= 0 || !ar.AsyncWaitHandle.WaitOne(left)) break;
                            int n = fs.EndRead(ar);
                            if (verbose) Console.WriteLine("  <- " + BitConverter.ToString(inBuf, 0, Math.Min(n, 8)));
                            if (n >= 3 && inBuf[0] == 0x66 && (inBuf[1] == 130 || inBuf[1] == 11))
                                return inBuf[2] == 1 ? 1 : 0;
                        }
                    }
                } catch (Exception ex) { if (verbose) Console.WriteLine("  err: " + ex.Message); }
            }
        }
        return -1;
    }
}
