using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Display.Controls;

namespace Display
{
    public partial class MainForm : Form
    {
        /* grafični elementi */
        private Button confLoadBtn;
        private Button confSaveBtn;
        private Button logBtn;

        private DevicesTab tabDevices;
        private SettingsTab tabSettings;
        private TabPage tabStatus;
        private TabPage tabSensors;
        private TabPage tabLog;
        private TabPage tabChart;
        private TabPage tabAbout;
        private TabControl tabControl1;

        private StatusLabel messageLabel;
        private string messageFullText;
        private ToolTip statusToolTip;

        /* podatkovni elementi */
        private TabPage tabBeforeStart;

        /* */
        private BackgroundTask loggingTask;

        private bool portsRefreshing;
        private const int MEASUREMENT_WAIT_ON_CLOSE = 2000;
        private bool switchingTab;
        private bool minimizeOnStart;
        private bool autoStarting;
        private bool startWaitsForPorts;

        private void InitializeBackgroundWorker()
        {
            loggingTask = new BackgroundTask(LoggingTask, LoggingCompleted);
        }

        private void InitializeComponents()
        {
            int margin = Consts.Display.MARGIN;

            SuspendLayout();

            tabControl1 = new TabControl();
            if (Helper.DpiScale >= 1.25f)
            {
                tabControl1.Padding = new Point(margin, margin);
            }

            int index = 0;
            tabDevices = new DevicesTab() { TabIndex = index, Text = "Devices" };
            tabControl1.Controls.Add(tabDevices);

            index++;
            tabSensors = new SensorsTab() { TabIndex = index, Text = "Sensors" };
            tabControl1.Controls.Add(tabSensors);

            index++;
            tabSettings = new SettingsTab() { TabIndex = index, Text = "Settings" };
            tabControl1.Controls.Add(tabSettings);

            index++;
            tabStatus = new StatusTab() { TabIndex = index, Text = "Status" };
            tabControl1.Controls.Add(tabStatus);

            index++;
            tabLog = new LogTab() { TabIndex = index, Text = "Log" };
            tabControl1.Controls.Add(tabLog);

            index++;
            tabChart = new ChartTab() { TabIndex = index, Text = "Chart" };
            tabControl1.Controls.Add(tabChart);

            index++;
            tabAbout = new AboutTab() { TabIndex = index, Text = "About" };
            tabControl1.Controls.Add(tabAbout);

            Controls.Add(tabControl1);

            tabControl1.ShowToolTips = true;
            for (int i = 0; i < tabControl1.TabPages.Count && i < 9; i++)
            {
                tabControl1.TabPages[i].ToolTipText = string.Format("Ctrl+{0}", i + 1);
            }

            messageLabel = new StatusLabel() { Text = Consts.App.NAME.ToUpperInvariant(), TextAlign = ContentAlignment.MiddleLeft };
            statusToolTip = new ToolTip() { AutoPopDelay = 30000 };
            ContextMenuStrip messageMenu = new ContextMenuStrip();
            messageMenu.Items.Add(new ToolStripMenuItem("Copy message", null, new EventHandler(OnCopyMessage)));
            messageLabel.ContextMenuStrip = messageMenu;
            Controls.Add(messageLabel);

            confLoadBtn = new Button() { Text = "Load configuration", AutoSize = true, Enabled = Configuration.SettingsXml.canSave };
            confLoadBtn.Click += new EventHandler(OnLoadBtn);
            Controls.Add(confLoadBtn);

            confSaveBtn = new Button() { Text = "Save configuration", AutoSize = true, Enabled = Configuration.SettingsXml.canSave };
            confSaveBtn.Click += new EventHandler(OnSaveBtn);
            statusToolTip.SetToolTip(confLoadBtn, "Ctrl+L");
            statusToolTip.SetToolTip(confSaveBtn, "Ctrl+S");
            Controls.Add(confSaveBtn);

            logBtn = new Button() { Text = "Start", AutoSize = true };
            logBtn.Click += new EventHandler(OnLogBtn);
            statusToolTip.SetToolTip(logBtn, "F5 start, F6 start and minimize, F8 stop");
            Controls.Add(logBtn);

            ResumeLayout();

            Event.MessageChanged += new Event.StatusEvent(OnMessageChanged);
            Event.LoggingStarted += new Event.DisplayEvent(OnLoggingStarted);
            InitializeTray();
            InitializeLoggingIndicator();
            InitializeTaskbar();
        }

        private void ResizeComponents()
        {
            Size size = ClientSize;
            int margin = Consts.Display.MARGIN;
            Padding btnPadding = Helper.ButtonPadding;
            logBtn.Padding = btnPadding;

            int buttonHeight = logBtn.Size.Height;
            int tabHeight = size.Height - buttonHeight - 3 * margin;
            int buttonX = margin;
            int buttonY = tabHeight + 2 * margin;

            tabControl1.Location = new Point(margin, margin);
            tabControl1.Size = new Size(size.Width - 2 * margin, tabHeight);

            confLoadBtn.Location = new Point(buttonX, buttonY);
            confLoadBtn.Padding = btnPadding;
            buttonX += margin + confLoadBtn.Size.Width;
            confSaveBtn.Location = new Point(buttonX, buttonY);
            confSaveBtn.Padding = btnPadding;
            buttonX += margin + confSaveBtn.Size.Width;
            messageLabel.Location = new Point(buttonX, buttonY);

            buttonX = size.Width - logBtn.Size.Width - margin;
            logBtn.Location = new Point(buttonX, buttonY);

            messageLabel.Size = new Size(buttonX - margin - messageLabel.Location.X, logBtn.Size.Height);
            PositionLoggingIndicator();
        }

        private void OnMessageChanged(string text, string fullText)
        {
            if (messageLabel.InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Event.StatusEvent(OnMessageChanged), new object[] { text, fullText });
                }
            }
            else
            {
                messageLabel.Text = text;
                messageFullText = fullText;
                statusToolTip.SetToolTip(messageLabel, fullText);
            }
        }

        private void OnCopyMessage(object sender, EventArgs e)
        {
            Helper.CopyToClipboard(messageFullText ?? messageLabel.Text, null);
        }

        private void StartAndMinimize()
        {
            minimizeOnStart = true;
            OnLogBtn(logBtn, EventArgs.Empty);
            if (!loggingTask.Running)
            {
                minimizeOnStart = false;
            }
        }

        private void LoggingStop()
        {
            if (Controller.Logging.IsRunning())
            {
                logBtn.Enabled = false;
                Controller.Logging.Stop();
                Event.RaiseMessageChanged("Stopping logging ...");
            }
        }

        private void LoggingStopped()
        {
            logBtn.Text = "Start";
            minimizeOnStart = false;
            alarmSoundTimer.Stop();
            logBtn.Enabled = Enabled;
            trayIcon.Text = Consts.App.NAME;
            UpdateTrayIcon();
            if (tabBeforeStart != null)
            {
                SwitchTab(tabBeforeStart);
                tabBeforeStart = null;
            }
            Event.RaiseDeviceAvailable();
        }

        private void LoggingCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DateTime aDate = DateTime.Now;
            LoggingStopped();
            if (e.Error != null)
            {
                Utils.Log.Error(e.Error.ToString());
                Event.RaiseMessageChanged(string.Format("Logging failed at {0}: {1}", Utils.Formats.Clock(aDate), e.Error.Message));
                return;
            }
            Event.RaiseMessageChanged(string.Format("Logging stopped at {0}, {1} measures taken.", Utils.Formats.Clock(aDate), Controller.Logging.Measurements));
        }

        private void LoggingTask(object sender, DoWorkEventArgs e)
        {
            Controller.Logging.Start(startWaitsForPorts);
        }

        private void DisableForm()
        {
            deviceBusy = true;
            Button[] buttons = { confLoadBtn, confSaveBtn, logBtn };
            foreach (Button button in buttons)
            {
                button.Enabled = false;
            }
        }

        private void EnableForm()
        {
            deviceBusy = false;
            BeginInvoke(new MethodInvoker(RefreshPortsIfIdle));
            logBtn.Enabled = true;

            if (!Configuration.SettingsXml.canSave)
            {
                return;
            }
            confLoadBtn.Enabled = !portsRefreshing;
            confSaveBtn.Enabled = true;
        }

        private void OnPortsRefreshing(bool refreshing)
        {
            portsRefreshing = refreshing;
            if (refreshing)
            {
                confLoadBtn.Enabled = false;
                if (!loggingTask.Running)
                {
                    logBtn.Enabled = false;
                }
            }
            else if (!loggingTask.Running)
            {
                confLoadBtn.Enabled = Configuration.SettingsXml.canSave;
                logBtn.Enabled = !deviceBusy;
            }
            if (!refreshing && !startupOptionsDone)
            {
                startupOptionsDone = true;
                BeginInvoke(new MethodInvoker(RunStartupOptions));
            }
            if (!refreshing)
            {
                BeginInvoke(new MethodInvoker(RefreshPortsIfIdle));
            }
        }

        private void OnLogBtn(object sender, EventArgs e)
        {
            if (loggingTask.Running)
            {
                LoggingStop();
                return;
            }

            if (!CommitSettings())
            {
                return;
            }

            if (Controller.Logging.Check())
            {
                Event.RaiseDeviceBusy();
                DateTime aDate = DateTime.Now;
                Event.RaiseMessageChanged(string.Format("Logging started at {0}.", Utils.Formats.Clock(aDate)));
                startWaitsForPorts = autoStarting;
                loggingTask.Start();
                logBtn.Text = "Stop";
                logBtn.Enabled = true;
                tabBeforeStart = null;
                return;
            }
            Event.RaiseMessageChanged("No devices selected or found!");
        }

        /* Values typed in the Settings tab are committed on Enter or when leaving the field; Start and Save commit them first. */
        private bool CommitSettings()
        {
            if (!tabSettings.CommitInterval(out string error) || !tabSettings.CommitRecordsPerFile(out error) || !tabSettings.CommitFileNamePattern(out error))
            {
                Event.RaiseMessageChanged(error);
                return false;
            }
            return true;
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                MinimizeToTray();
                return;
            }
            restoreState = WindowState;
            if (Height == 0)
            {
                return;
            }
            ResizeComponents();
        }

        private void SwitchTab(TabPage page)
        {
            switchingTab = true;
            try
            {
                tabControl1.SelectedTab = page;
            }
            finally
            {
                switchingTab = false;
            }
        }

        private void OnLoggingStarted()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Event.DisplayEvent(OnLoggingStarted));
                return;
            }
            if (!loggingTask.Running)
            {
                return;
            }
            loggingSince = DateTime.Now;
            UpdateTrayIcon();
            tabBeforeStart = tabControl1.SelectedTab;
            SwitchTab(tabStatus);
            if (minimizeOnStart)
            {
                minimizeOnStart = false;
                WindowState = FormWindowState.Minimized;
            }
        }

        private void OnPageSwitch(object sender, TabControlCancelEventArgs e)
        {
            if (switchingTab)
            {
                return;
            }
            /* Ponastavi shranjen zavihek, da ga ob koncu zajemanja ne zamenja. */
            tabBeforeStart = null;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            ResizeComponents();
            Resize += new EventHandler(OnResize);
            tabControl1.Selecting += new TabControlCancelEventHandler(OnPageSwitch);
            changesTimer = new Timer() { Interval = CHANGES_CHECK_INTERVAL };
            changesTimer.Tick += new EventHandler(OnChangesTimer);
            changesTimer.Start();
            Event.RaiseLoaded();
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            DestroyBadgeIcons();
            if (Controller.ConfigFile.Path != null)
            {
                Utils.Log.Info("Settings not saved to {0}: configuration {1} is in use", Configuration.SettingsXml.SettingsPath, Controller.ConfigFile.Path);
                return;
            }
            try
            {
                Configuration.SettingsXml.SaveSettings();
                Utils.Log.Info("Settings saved to {0}", Configuration.SettingsXml.SettingsPath);
            }
            catch (Exception ex)
            {
                Utils.Log.Warn("Settings not saved to {0}: {1}", Configuration.SettingsXml.SettingsPath, ex.Message);
            }
        }

        private void OnClose(object sender, FormClosingEventArgs e)
        {
            bool ctrlClose = e.CloseReason == CloseReason.UserClosing && (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (loggingTask.Running && (ctrlClose || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing))
            {
                Utils.Log.Info("Closing while logging ({0}): stopping and writing waiting measurements", ctrlClose ? "Ctrl+close" : e.CloseReason.ToString());
                Controller.Logging.Stop();
                if (!Controller.Logging.WaitForMeasurement(MEASUREMENT_WAIT_ON_CLOSE))
                {
                    Utils.Log.Warn("Closing while a measurement is still running");
                }
                Controller.Logging.FlushPending();
                return;
            }

            if (loggingTask.Running)
            {
                string caption = Consts.App.NAME;
                string message = "Logging in progress ... Do you like to stop?";

                e.Cancel = true;
                DialogResult result = MessageBox.Show(this, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Yes)
                {
                    LoggingStop();
                }
                return;
            }

            if (ctrlClose || e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }
            AskSaveChanges(e);
        }

        private void AskSaveChanges(FormClosingEventArgs e)
        {
            if (!Controller.ConfigFile.TryGetChanged(out bool changed) || !changed)
            {
                return;
            }
            string path = Controller.ConfigFile.Path;
            string message = string.Format("Save changes to {0}?", System.IO.Path.GetFileName(path));
            DialogResult result = MessageBox.Show(this, message, Consts.App.NAME, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result != DialogResult.Yes)
            {
                return;
            }
            try
            {
                Controller.ConfigFile.Save(path);
                Utils.Log.Info("Configuration saved to {0} on close", path);
            }
            catch (Exception ex)
            {
                ShowConfigError("save", path, ex);
                e.Cancel = true;
            }
        }

        public MainForm()
        {
            Font = Helper.UiFont;
            Icon = Helper.AppIcon;
            float scale = Helper.DpiScale;
            /* At 100% the window is tall enough for two rows of status tiles. */
            int height = scale < 1.25f ? 412 : (int)(400 * scale);
            Size = MinimumSize = new Size((int)(700 * scale), height);
            Text = Consts.App.NAME;
            CenterToScreen();

            Event.DeviceBusy += new Event.DisplayEvent(DisableForm);
            Event.DeviceAvailable += new Event.DisplayEvent(EnableForm);
            Event.PortsRefreshing += new Event.StateEvent(OnPortsRefreshing);

            try
            {
                if (Configuration.SettingsXml.LoadSettings())
                {
                    Utils.Log.Info("Settings loaded from {0}", Configuration.SettingsXml.SettingsPath);
                }
            }
            catch (Exception e)
            {
                Utils.Log.Warn("Settings not loaded from {0}: {1}", Configuration.SettingsXml.SettingsPath, e.Message);
                try
                {
                    Utils.Log.Warn("Unreadable settings kept as {0}", Configuration.SettingsXml.KeepBadSettings());
                }
                catch (Exception ex)
                {
                    Utils.Log.Warn("Could not keep a copy of the unreadable settings: {0}", ex.Message);
                }
            }

            InitializeComponents();
            InitializeBackgroundWorker();
            InitializeDeviceChange();

            Load += new EventHandler(OnLoad);
            FormClosing += new FormClosingEventHandler(OnClose);
            FormClosed += new FormClosedEventHandler(OnClosed);
        }
    }
}
