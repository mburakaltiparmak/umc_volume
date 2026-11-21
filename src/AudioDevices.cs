using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UmcVolume
{
    // One active output endpoint, with the USB identity of the hardware behind it.
    sealed class DeviceInfo
    {
        public const int BehringerVid = 0x1397;

        public string Id;             // Core Audio endpoint id
        public string Name;           // "OUT 1-2 (BEHRINGER UMC 202HD 192k)"
        public string InterfaceName;  // "BEHRINGER UMC 202HD 192k"
        public int Vid = -1, Pid = -1;

        public bool IsBehringer { get { return Vid == BehringerVid; } }

        // "BEHRINGER UMC 202HD 192k" -> "UMC202HD"; used for the title and the photo file name.
        public string Model
        {
            get
            {
                string s = InterfaceName ?? "";
                s = Regex.Replace(s, @"^\s*BEHRINGER\s+", "", RegexOptions.IgnoreCase);
                s = Regex.Replace(s, @"\s+\d+\s*k\s*$", "", RegexOptions.IgnoreCase);  // sample-rate suffix
                s = Regex.Replace(s, @"^UMC\s+", "UMC", RegexOptions.IgnoreCase);
                s = s.Trim();
                return s.Length > 0 ? s : (Name ?? "");
            }
        }

        public string Title
        {
            get
            {
                string m = Model;
                return m.StartsWith("UMC", StringComparison.OrdinalIgnoreCase) ? "U-PHORIA " + m : m;
            }
        }
    }

    // Minimal Core Audio (MMDevice API) interop: list outputs and find the default one.
    static class AudioDevices
    {
        const int eRender = 0;
        const int eConsole = 0;
        const int DEVICE_STATE_ACTIVE = 1;
        const int STGM_READ = 0;
        const short VT_LPWSTR = 31;

        static PropertyKey PKEY_Device_FriendlyName = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        static PropertyKey PKEY_DeviceInterface_FriendlyName = new PropertyKey(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 6);
        static PropertyKey PKEY_Device_InstanceIdOfParent = new PropertyKey(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 2);

        static IMMDeviceEnumerator enumerator;

        static IMMDeviceEnumerator Enumerator
        {
            get
            {
                if (enumerator == null)
                    enumerator = (IMMDeviceEnumerator)CreateEnumerator();
                return enumerator;
            }
        }

        // Created through the CLSID rather than a [ComImport] class so that several parts of the
        // app can each cast the same COM object to their own interface declarations.
        public static object CreateEnumerator()
        {
            return Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
        }

        public static string DefaultRenderId()
        {
            IMMDevice dev;
            if (Enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out dev) != 0 || dev == null) return null;
            try
            {
                string id;
                return dev.GetId(out id) == 0 ? id : null;
            }
            finally { Marshal.ReleaseComObject(dev); }
        }

        public static List<DeviceInfo> ListRender()
        {
            var list = new List<DeviceInfo>();
            IMMDeviceCollection col;
            if (Enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out col) != 0 || col == null) return list;
            try
            {
                uint count;
                col.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice dev;
                    if (col.Item(i, out dev) != 0 || dev == null) continue;
                    try
                    {
                        DeviceInfo info = Describe(dev);
                        if (info != null) list.Add(info);
                    }
                    finally { Marshal.ReleaseComObject(dev); }
                }
            }
            finally { Marshal.ReleaseComObject(col); }
            return list;
        }

        static DeviceInfo Describe(IMMDevice dev)
        {
            string id;
            if (dev.GetId(out id) != 0) return null;
            var info = new DeviceInfo { Id = id };

            IPropertyStore store;
            if (dev.OpenPropertyStore(STGM_READ, out store) != 0 || store == null) return info;
            try
            {
                info.Name = ReadString(store, PKEY_Device_FriendlyName) ?? "";
                info.InterfaceName = ReadString(store, PKEY_DeviceInterface_FriendlyName) ?? "";

                // e.g. "{1}.TUSBAUDIO_ENUM\VID_1397&PID_0507&KS\12345678" or "{1}.USB\VID_1397&PID_0508..."
                string parent = ReadString(store, PKEY_Device_InstanceIdOfParent) ?? "";
                Match m = Regex.Match(parent, @"VID_([0-9A-F]{4}).*?PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    info.Vid = Convert.ToInt32(m.Groups[1].Value, 16);
                    info.Pid = Convert.ToInt32(m.Groups[2].Value, 16);
                }
            }
            finally { Marshal.ReleaseComObject(store); }
            return info;
        }

        static string ReadString(IPropertyStore store, PropertyKey key)
        {
            var pv = new PropVariant();
            if (store.GetValue(ref key, out pv) != 0) return null;
            try
            {
                return pv.vt == VT_LPWSTR && pv.pointer != IntPtr.Zero ? Marshal.PtrToStringUni(pv.pointer) : null;
            }
            finally { PropVariantClear(ref pv); }
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant pvar);

        [StructLayout(LayoutKind.Sequential)]
        struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
            public PropertyKey(Guid f, int p) { fmtid = f; pid = p; }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr pointer;
            [FieldOffset(8)] long pad1;
            [FieldOffset(16)] long pad2;
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }
    }
}
