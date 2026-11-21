namespace Utils
{
    static class Autostart
    {
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled
        {
            get
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY))
                {
                    return key != null && key.GetValue(Consts.App.NAME) != null;
                }
            }
        }

        public static string CommandLine(string configPath)
        {
            string line = "\"" + System.Windows.Forms.Application.ExecutablePath + "\" /autostart";
            return string.IsNullOrEmpty(configPath) ? line : line + " /config=\"" + configPath + "\"";
        }

        public static void Enable(string configPath)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RUN_KEY))
            {
                key.SetValue(Consts.App.NAME, CommandLine(configPath));
            }
        }

        public static void Disable()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
            {
                key?.DeleteValue(Consts.App.NAME, false);
            }
        }
    }
}
