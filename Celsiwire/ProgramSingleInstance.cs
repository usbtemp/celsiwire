using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Program
{
    static class SingleInstance
    {
        private const string MUTEX_NAME = @"Local\Celsiwire";
        private const string SHOW_MESSAGE_NAME = "Celsiwire.Show";
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        private const int ASFW_ANY = -1;

        private static Mutex mutex;

        public static readonly int ShowMessage = Utils.WindowMessages.Register(SHOW_MESSAGE_NAME);

        public static bool Acquire()
        {
            bool created;
            try
            {
                mutex = new Mutex(true, MUTEX_NAME, out created);
            }
            catch (UnauthorizedAccessException)
            {
                /* The running copy was started as administrator; its mutex cannot be opened from here. */
                mutex = null;
                return false;
            }
            if (!created)
            {
                mutex.Close();
                mutex = null;
                return false;
            }
            /* A copy that is not elevated can still bring this one to the front. */
            Utils.WindowMessages.AllowFromLowerIntegrity(ShowMessage);
            return true;
        }

        public static void Release()
        {
            if (mutex == null)
            {
                return;
            }
            mutex.ReleaseMutex();
            mutex.Close();
            mutex = null;
        }

        public static void ShowRunningInstance()
        {
            NativeMethods.AllowSetForegroundWindow(ASFW_ANY);
            NativeMethods.PostMessage(HWND_BROADCAST, ShowMessage, IntPtr.Zero, IntPtr.Zero);
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AllowSetForegroundWindow(int processId);
        }
    }
}
