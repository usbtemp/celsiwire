using System;
using System.Runtime.InteropServices;

namespace Utils
{
    static class WindowMessages
    {
        private const int MSGFLT_ADD = 1;

        public static int Register(string name)
        {
            return NativeMethods.RegisterWindowMessage(name);
        }

        /* Lets the message through to this process also when it runs as administrator and the sender does not. */
        public static void AllowFromLowerIntegrity(int message)
        {
            if (message == 0)
            {
                return;
            }
            try
            {
                NativeMethods.ChangeWindowMessageFilter(message, MSGFLT_ADD);
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int RegisterWindowMessage(string message);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ChangeWindowMessageFilter(int message, int flag);
        }
    }
}
