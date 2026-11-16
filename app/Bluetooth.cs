// Paired Bluetooth devices + real connection state (Win32 Bluetooth API).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioSwitcher
{
    public class BtDevice
    {
        public string Name;
        public ulong Address;
        public uint ClassOfDevice;
        public bool Connected;
        public int MajorClass { get { return (int)((ClassOfDevice >> 8) & 0x1F); } }
        public bool IsAudio { get { return MajorClass == 4; } } // Audio/Video
    }

    public static class Bluetooth
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEMTIME { public short a, b, c, d, e, f, g, h; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct BLUETOOTH_DEVICE_INFO
        {
            public int dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            public bool fConnected, fRemembered, fAuthenticated;
            public SYSTEMTIME stLastSeen, stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public int dwSize;
            public bool fReturnAuthenticated, fReturnRemembered, fReturnUnknown, fReturnConnected, fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BLUETOOTH_FIND_RADIO_PARAMS { public int dwSize; }

        [DllImport("bthprops.cpl", SetLastError = true)]
        static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS p, ref BLUETOOTH_DEVICE_INFO info);
        [DllImport("bthprops.cpl", SetLastError = true)]
        static extern bool BluetoothFindNextDevice(IntPtr h, ref BLUETOOTH_DEVICE_INFO info);
        [DllImport("bthprops.cpl")] static extern bool BluetoothFindDeviceClose(IntPtr h);
        [DllImport("bthprops.cpl", SetLastError = true)]
        static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS p, out IntPtr radio);
        [DllImport("bthprops.cpl")] static extern bool BluetoothFindNextRadio(IntPtr h, out IntPtr radio);
        [DllImport("bthprops.cpl")] static extern bool BluetoothFindRadioClose(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr h, uint code, ref ulong inBuf, int inSize, IntPtr outBuf, int outSize, out int ret, IntPtr ov);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000C;

        public static List<BtDevice> ListPaired()
        {
            var list = new List<BtDevice>();
            var p = new BLUETOOTH_DEVICE_SEARCH_PARAMS();
            p.dwSize = Marshal.SizeOf(p);
            p.fReturnAuthenticated = true; p.fReturnRemembered = true; p.fReturnConnected = true;
            var info = new BLUETOOTH_DEVICE_INFO();
            info.dwSize = Marshal.SizeOf(info);
            IntPtr h = BluetoothFindFirstDevice(ref p, ref info);
            if (h == IntPtr.Zero) return list;
            try
            {
                do
                {
                    if (info.fRemembered || info.fAuthenticated)
                        list.Add(new BtDevice { Name = info.szName, Address = info.Address, ClassOfDevice = info.ulClassofDevice, Connected = info.fConnected });
                    info = new BLUETOOTH_DEVICE_INFO();
                    info.dwSize = Marshal.SizeOf(info);
                } while (BluetoothFindNextDevice(h, ref info));
            }
            finally { BluetoothFindDeviceClose(h); }
            return list;
        }

        // Drops the Bluetooth link to the device on every local radio. Returns true if any radio accepted it.
        public static bool Disconnect(ulong address)
        {
            bool ok = false;
            var rp = new BLUETOOTH_FIND_RADIO_PARAMS();
            rp.dwSize = Marshal.SizeOf(rp);
            IntPtr radio;
            IntPtr f = BluetoothFindFirstRadio(ref rp, out radio);
            if (f == IntPtr.Zero) return false;
            try
            {
                do
                {
                    int ret;
                    ulong a = address;
                    if (DeviceIoControl(radio, IOCTL_BTH_DISCONNECT_DEVICE, ref a, 8, IntPtr.Zero, 0, out ret, IntPtr.Zero)) ok = true;
                    CloseHandle(radio);
                } while (BluetoothFindNextRadio(f, out radio));
            }
            finally { BluetoothFindRadioClose(f); }
            return ok;
        }
    }
}
