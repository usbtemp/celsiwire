using System;

namespace Controller
{
    static class ConfigFile
    {
        private static string savedSnapshot;

        public static string Path { get; private set; }

        public static void Load(string path)
        {
            Configuration.SettingsXml.Import(path);
            SetPath(path);
            Display.Event.RaiseConfigurationLoaded();
            MarkSaved();
        }

        public static void Save(string path)
        {
            Configuration.SettingsXml.Export(path);
            SetPath(path);
            MarkSaved();
        }

        public static bool TryGetChanged(out bool changed)
        {
            changed = false;
            if (Path == null || savedSnapshot == null)
            {
                return true;
            }
            try
            {
                changed = Configuration.SettingsXml.Snapshot() != savedSnapshot;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void MarkSaved()
        {
            try
            {
                savedSnapshot = Configuration.SettingsXml.Snapshot();
            }
            catch (InvalidOperationException)
            {
                savedSnapshot = null;
            }
        }

        private static void SetPath(string path)
        {
            Path = path;
            try
            {
                if (Utils.Autostart.IsEnabled)
                {
                    Utils.Autostart.Enable(path);
                    Utils.Log.Info("Start with Windows updated: {0}", Utils.Autostart.CommandLine(path));
                }
            }
            catch (Exception ex)
            {
                Utils.Log.Warn("Could not update start with Windows: {0}", ex.Message);
            }
        }
    }
}
