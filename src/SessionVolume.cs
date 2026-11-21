using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace UmcVolume
{
    // Controls loudness on one output device without any audio driver or plug-in, by setting the
    // Windows per-application (session) volume of every app playing on it. The audio engine applies
    // these in software, so it works even though the UMC driver ignores the endpoint volume.
    //
    // Session notifications are only delivered to an MTA thread, so all COM work runs on a
    // dedicated worker thread. New sessions are set the moment they are created; a 1 s sweep
    // catches anything missed. On Dispose every session is put back to 100 %.
    sealed class SessionVolume : IDisposable
    {
        static Guid EventContext = new Guid("8a1e3f52-6c1b-4f0e-9a77-2f5b0c6d4e11");

        readonly Thread worker;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile bool stopping;
        volatile string deviceId;
        float target = 1f; // read by the notification callback on other threads
        string[] names = new string[0];
        readonly object namesLock = new object();

        // Raised on the worker thread when the set of apps playing on the device changes.
        public event EventHandler SessionsChanged;

        public SessionVolume()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "SessionVolume" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }

        public float Target { get { return Volatile.Read(ref target); } }

        public string[] SessionNames
        {
            get { lock (namesLock) return (string[])names.Clone(); }
        }

        public void SetDevice(string endpointId)
        {
            if (endpointId == deviceId) return;
            deviceId = endpointId;
            wake.Set();
        }

        public void SetTarget(float scalar)
        {
            Volatile.Write(ref target, Math.Max(0f, Math.Min(1f, scalar)));
            wake.Set();
        }

        // ---- worker -------------------------------------------------------------

        void Run()
        {
            var enumerator = (IMMDeviceEnumerator)AudioDevices.CreateEnumerator();
            string current = null;
            IAudioSessionManager2 manager = null;
            AudioSessionNotifier notifier = null;

            while (true)
            {
                try
                {
                    string wanted = deviceId;
                    if (wanted != current || (manager == null && wanted != null))
                    {
                        // Switching to another device: give the apps on the old one their full volume back.
                        if (manager != null && wanted != current)
                        {
                            try { Sweep(manager, 1f); } catch { }
                        }
                        Release(ref manager, ref notifier);
                        current = wanted;
                        if (wanted != null) manager = Open(enumerator, wanted, out notifier);
                    }
                    if (manager != null) Sweep(manager, stopping ? 1f : Target);
                }
                catch (Exception ex)
                {
                    Program.Log("Session sweep failed: " + ex.Message);
                    Release(ref manager, ref notifier); // reopen on the next pass
                }

                if (stopping) break;
                wake.WaitOne(1000);
            }
            Release(ref manager, ref notifier);
        }

        IAudioSessionManager2 Open(IMMDeviceEnumerator enumerator, string id, out AudioSessionNotifier notifier)
        {
            notifier = null;
            IMMDevice device;
            if (enumerator.GetDevice(id, out device) != 0 || device == null) return null;
            try
            {
                Guid iid = typeof(IAudioSessionManager2).GUID;
                object o;
                if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0) return null;
                var manager = (IAudioSessionManager2)o;

                // The enumerator must be requested once before notifications start flowing.
                IAudioSessionEnumerator first;
                if (manager.GetSessionEnumerator(out first) == 0 && first != null) Marshal.ReleaseComObject(first);

                notifier = new AudioSessionNotifier(OnSessionCreated);
                manager.RegisterSessionNotification(notifier);
                return manager;
            }
            finally { Marshal.ReleaseComObject(device); }
        }

        void Release(ref IAudioSessionManager2 manager, ref AudioSessionNotifier notifier)
        {
            if (manager != null)
            {
                try { if (notifier != null) manager.UnregisterSessionNotification(notifier); } catch { }
                try { Marshal.ReleaseComObject(manager); } catch { }
            }
            manager = null;
            notifier = null;
        }

        void Sweep(IAudioSessionManager2 manager, float level)
        {
            IAudioSessionEnumerator list;
            if (manager.GetSessionEnumerator(out list) != 0 || list == null) return;
            var seen = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            try
            {
                int count;
                list.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 session;
                    if (list.GetSession(i, out session) != 0 || session == null) continue;
                    try
                    {
                        int state;
                        session.GetState(out state);
                        if (state == AudioSessionStateExpired) continue;
                        Apply(session, level);
                        seen.Add(NameOf(session));
                    }
                    finally { Marshal.ReleaseComObject(session); }
                }
            }
            finally { Marshal.ReleaseComObject(list); }

            var now = new string[seen.Count];
            seen.CopyTo(now);
            bool changed;
            lock (namesLock)
            {
                changed = string.Join("|", now) != string.Join("|", names);
                if (changed) names = now;
            }
            if (changed && SessionsChanged != null) SessionsChanged(this, EventArgs.Empty);
        }

        static void Apply(object session, float level)
        {
            var volume = session as ISimpleAudioVolume;
            if (volume == null) return;
            float currentLevel;
            if (volume.GetMasterVolume(out currentLevel) == 0 && Math.Abs(currentLevel - level) < 0.0005f) return;
            volume.SetMasterVolume(level, ref EventContext);
        }

        static string NameOf(IAudioSessionControl2 session)
        {
            if (session.IsSystemSoundsSession() == 0) return L.SystemSounds;
            uint pid;
            session.GetProcessId(out pid);
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    string desc = null;
                    try { desc = p.MainModule.FileVersionInfo.FileDescription; } catch { }
                    return string.IsNullOrWhiteSpace(desc) ? p.ProcessName : desc.Trim();
                }
            }
            catch { return "PID " + pid; }
        }

        public void Dispose()
        {
            // Let the worker put every session back to 100 % before we go.
            stopping = true;
            wake.Set();
            worker.Join(3000);
            wake.Dispose();
        }

        // Called by Windows on an MTA thread as soon as an app opens a stream on the device.
        void OnSessionCreated(object newSession)
        {
            if (!stopping) Apply(newSession, Target);
            wake.Set(); // refresh the app list
        }

        // ---- Core Audio interop ---------------------------------------------------

        const int CLSCTX_ALL = 23;
        const int AudioSessionStateExpired = 2;

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr groupingParam, uint flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr groupingParam, uint flags, out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
            [PreserveSig] int RegisterSessionNotification(IAudioSessionNotification notification);
            [PreserveSig] int UnregisterSessionNotification(IAudioSessionNotification notification);
            [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duck);
            [PreserveSig] int UnregisterDuckNotification(IntPtr duck);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid ctx);
            [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid ctx);
            [PreserveSig] int GetGroupingParam(out Guid param);
            [PreserveSig] int SetGroupingParam(ref Guid param, ref Guid ctx);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
            [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetProcessId(out uint pid);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference(bool optOut);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute(bool mute, ref Guid ctx);
            [PreserveSig] int GetMute(out bool mute);
        }
    }

    // The callback interface and object must be public and COM-visible: Windows reaches them
    // through a COM callable wrapper, which only exposes public, COM-visible types.
    [ComVisible(true), Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionNotification
    {
        [PreserveSig] int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)] object newSession);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class AudioSessionNotifier : IAudioSessionNotification
    {
        readonly Action<object> onCreated;

        internal AudioSessionNotifier(Action<object> onCreated)
        {
            this.onCreated = onCreated;
        }

        public int OnSessionCreated(object newSession)
        {
            try { onCreated(newSession); } catch { }
            return 0;
        }
    }
}
