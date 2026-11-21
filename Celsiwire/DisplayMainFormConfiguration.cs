using System;
using System.Windows.Forms;

namespace Display
{
    partial class MainForm
    {
        private const string CHANGED_MARK = " *";
        private const int CHANGES_CHECK_INTERVAL = 500;
        private Timer changesTimer;
        private bool startupOptionsDone;

        private static string ConfigFilter
        {
            get { return string.Format("{0} configuration (*.xml)|*.xml|All files (*.*)|*.*", Consts.App.NAME); }
        }

        private static string ConfigDirectory
        {
            get
            {
                string configPath = Controller.ConfigFile.Path;
                if (configPath != null)
                {
                    return System.IO.Path.GetDirectoryName(configPath);
                }
                string directory = Configuration.Logging.LogDirectory;
                return System.IO.Directory.Exists(directory) ? directory : Utils.Path.Root;
            }
        }

        private void ShowConfigError(string action, string path, Exception ex)
        {
            Utils.Log.Error(ex.ToString());
            string message = string.Format("Could not {0} configuration {1}:\n\n{2}", action, path, ex.Message);
            MessageBox.Show(this, message, Consts.App.NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnLoadBtn(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Load configuration";
                dialog.Filter = ConfigFilter;
                dialog.InitialDirectory = ConfigDirectory;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                LoadConfiguration(dialog.FileName, true);
            }
        }

        private bool LoadConfiguration(string path, bool interactive)
        {
            try
            {
                Controller.ConfigFile.Load(path);
            }
            catch (Exception ex)
            {
                if (interactive)
                {
                    ShowConfigError("load", path, ex);
                }
                else
                {
                    Utils.Log.Error("Could not load configuration {0}: {1}", path, ex.Message);
                    Event.RaiseMessageChanged(string.Format("Could not load configuration {0}: {1}", System.IO.Path.GetFileName(path), ex.Message), string.Format("Could not load configuration {0}: {1}", path, ex.Message));
                }
                return false;
            }
            UpdateChangedState();
            Event.RaiseMessageChanged(string.Format("Configuration loaded from {0}", System.IO.Path.GetFileName(path)), string.Format("Configuration loaded from {0}", path));
            return true;
        }

        private void UpdateChangedState()
        {
            if (!Controller.ConfigFile.TryGetChanged(out bool changed))
            {
                return;
            }

            string configPath = Controller.ConfigFile.Path;
            string title = configPath != null
                ? string.Format("{0} - {1}{2}", Consts.App.NAME, System.IO.Path.GetFileName(configPath), changed ? CHANGED_MARK : string.Empty)
                : Consts.App.NAME;
            if (Text != title)
            {
                Text = title;
            }
        }

        private void OnChangesTimer(object sender, EventArgs e)
        {
            UpdateChangedState();
        }

        private void RunStartupOptions()
        {
            if (Program.StartupOptions.ConfigPath != null && !LoadConfiguration(Program.StartupOptions.ConfigPath, false))
            {
                return;
            }
            if (Program.StartupOptions.AutoStart && !loggingTask.Running && logBtn.Enabled)
            {
                autoStarting = true;
                StartAndMinimize();
                autoStarting = false;
            }
        }

        private void OnSaveBtn(object sender, EventArgs e)
        {
            if (!CommitSettings())
            {
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                string configPath = Controller.ConfigFile.Path;
                dialog.Title = "Save configuration";
                dialog.Filter = ConfigFilter;
                dialog.DefaultExt = "xml";
                dialog.InitialDirectory = ConfigDirectory;
                dialog.FileName = configPath != null ? System.IO.Path.GetFileName(configPath) : Consts.App.NAME + ".xml";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    Controller.ConfigFile.Save(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowConfigError("save", dialog.FileName, ex);
                    return;
                }
                UpdateChangedState();
                Event.RaiseMessageChanged(string.Format("Configuration saved to {0}", System.IO.Path.GetFileName(dialog.FileName)), string.Format("Configuration saved to {0}", dialog.FileName));
            }
        }
    }
}
