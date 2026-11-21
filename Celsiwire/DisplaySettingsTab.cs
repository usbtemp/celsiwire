using System;
using System.Drawing;
using System.Windows.Forms;
using Display.Controls;

namespace Display
{
    class SettingsTab : TabPage
    {
        private const string INTERVAL_HELP = "Time between measurements.\nPick a value from the list, or type a number of seconds, minutes or hours (e.g. 90, 5 min, 2 hours) and press Enter.";

        private const string RECORDS_PER_FILE_HELP = "Number of measurements in one file; when it is reached, a new file is started.\nPick a value from the list, or type a number (1 or more) and press Enter.\nNot used when a new file is started every day.";

        private const string FILE_NAME_HELP = "Name of the log files.\nPick a pattern from the list or type your own; the example below shows the result.";

        private const string WAITING_HELP = "When the log folder cannot be written (e.g. a removed USB drive or a lost network drive), measurements wait in memory.\nWith this on, they are saved to a file of the same name in %LocalAppData%\\" + Consts.App.NAME + " once more than this many lines wait, and when logging stops.\nWith it off, they stay in memory until the folder can be written again, and are lost if logging stops before that.";

        private const string PATH_HELP = "Folder for the log files.\nType a path or choose one with Browse; the folder is created when logging starts.\nDefault: the program folder if it is writable, otherwise Documents\\" + Consts.App.NAME + ".";

        private Label intervalLabel;
        private ComboBox intervalComboBox;

        private CheckBox logToFileCheckBox;
        private Label pathLabel;
        private BorderedTextBox pathBox;
        private TextBox pathTextBox;
        private Button pathBtn;

        private Label fileNameLabel;
        private ComboBox fileNameComboBox;
        private Label fileNamePreviewLabel;
        private ToolTip toolTip;
        private Button openLogFolderBtn;
        private CheckBox autostartCheckBox;
        private CheckBox chartLimitsCheckBox;
        private bool updatingAutostart;

        private Label recordsPerFileLabel;
        private ComboBox recordsPerFileComboBox;
        private CheckBox newFilePerDayCheckBox;
        private CheckBox waitingToAppDataCheckBox;
        private NumericUpDown maxWaitingLinesBox;
        private Label waitingLinesLabel;

        private void InitializeComponents()
        {
            intervalLabel = new Label() { Text = "Measure every:", AutoSize = true };
            Controls.Add(intervalLabel);

            intervalComboBox = new ComboBox();
            intervalComboBox.KeyDown += new KeyEventHandler(OnIntervalComboBoxKeyDown);
            intervalComboBox.Leave += new EventHandler(OnIntervalComboBoxLeave);
            intervalComboBox.SelectedIndexChanged += new EventHandler(OnIntervalComboBoxIndexChanged);
            Controls.Add(intervalComboBox);

            logToFileCheckBox = new CheckBox() { Text = "Log to file", AutoSize = true, Checked = Configuration.Logging.LogToFile };
            logToFileCheckBox.CheckedChanged += new EventHandler(OnLogToFileChanged);
            Controls.Add(logToFileCheckBox);

            pathLabel = new Label() { Text = "Path:", AutoSize = true };
            Controls.Add(pathLabel);

            pathBox = new BorderedTextBox();
            pathTextBox = pathBox.TextBox;
            pathTextBox.Text = Configuration.Logging.LogDirectory;
            pathTextBox.TextChanged += new EventHandler(OnPathTextBoxTextChanged);
            Controls.Add(pathBox);

            pathBtn = new Button() { Text = "Browse" };
            pathBtn.Click += new EventHandler(OnPathBtnClick);
            Controls.Add(pathBtn);

            fileNameLabel = new Label() { Text = "Filename pattern:", AutoSize = true };
            Controls.Add(fileNameLabel);

            fileNameComboBox = new ComboBox();
            fileNameComboBox.Items.AddRange(Utils.FileName.Presets);
            fileNameComboBox.Text = Configuration.Logging.FileNamePattern;
            fileNameComboBox.TextChanged += new EventHandler(OnFileNameComboBoxTextChanged);
            Controls.Add(fileNameComboBox);

            fileNamePreviewLabel = new Label() { AutoSize = true, ForeColor = SystemColors.GrayText };
            Controls.Add(fileNamePreviewLabel);

            toolTip = new ToolTip() { AutoPopDelay = 30000 };
            toolTip.SetToolTip(intervalLabel, INTERVAL_HELP);
            toolTip.SetToolTip(intervalComboBox, INTERVAL_HELP);
            toolTip.SetToolTip(pathLabel, PATH_HELP);
            toolTip.SetToolTip(pathBox, PATH_HELP);
            toolTip.SetToolTip(pathTextBox, PATH_HELP);
            string fileNameHelp = FILE_NAME_HELP + "\n\n" + Utils.FileName.Help();
            toolTip.SetToolTip(fileNameLabel, fileNameHelp);
            toolTip.SetToolTip(fileNameComboBox, fileNameHelp);

            recordsPerFileLabel = new Label() { Text = "Records per file:", AutoSize = true };
            Controls.Add(recordsPerFileLabel);

            recordsPerFileComboBox = new ComboBox();
            recordsPerFileComboBox.KeyDown += new KeyEventHandler(OnRecordsPerFileComboBoxKeyDown);
            recordsPerFileComboBox.Leave += new EventHandler(OnRecordsPerFileComboBoxLeave);
            recordsPerFileComboBox.SelectedIndexChanged += new EventHandler(OnRecordsPerFileComboBoxIndexChanged);
            Controls.Add(recordsPerFileComboBox);
            toolTip.SetToolTip(recordsPerFileLabel, RECORDS_PER_FILE_HELP);
            toolTip.SetToolTip(recordsPerFileComboBox, RECORDS_PER_FILE_HELP);

            newFilePerDayCheckBox = new CheckBox() { Text = "New file every day", AutoSize = true, Checked = Configuration.Logging.NewFilePerDay };
            newFilePerDayCheckBox.CheckedChanged += new EventHandler(OnNewFilePerDayChanged);
            Controls.Add(newFilePerDayCheckBox);

            waitingToAppDataCheckBox = new CheckBox() { Text = "Save waiting lines to local AppData after", AutoSize = true, Checked = Configuration.Logging.WaitingToAppData };
            waitingToAppDataCheckBox.CheckedChanged += new EventHandler(OnWaitingToAppDataChanged);
            Controls.Add(waitingToAppDataCheckBox);

            maxWaitingLinesBox = new NumericUpDown()
            {
                Minimum = Configuration.Logging.MinWaitingLinesLimit,
                Maximum = Configuration.Logging.MaxWaitingLinesLimit,
                Increment = 100,
                ThousandsSeparator = true,
                TextAlign = HorizontalAlignment.Right,
                Value = Configuration.Logging.MaxWaitingLines,
            };
            maxWaitingLinesBox.Width = TextRenderer.MeasureText("1,000,000", maxWaitingLinesBox.Font).Width + SystemInformation.VerticalScrollBarWidth + (int)(12 * Helper.DpiScale);
            maxWaitingLinesBox.ValueChanged += new EventHandler(OnMaxWaitingLinesChanged);
            Controls.Add(maxWaitingLinesBox);

            waitingLinesLabel = new Label() { Text = "lines", AutoSize = true };
            Controls.Add(waitingLinesLabel);
            foreach (Control control in new Control[] { waitingToAppDataCheckBox, maxWaitingLinesBox, waitingLinesLabel })
            {
                toolTip.SetToolTip(control, WAITING_HELP);
            }

            openLogFolderBtn = new Button() { Text = "Open log folder", AutoSize = true };
            openLogFolderBtn.Click += new EventHandler(OnOpenLogFolderBtnClick);
            toolTip.SetToolTip(openLogFolderBtn, Utils.Path.AppDataDirectory);
            Controls.Add(openLogFolderBtn);

            autostartCheckBox = new CheckBox() { Text = "Start with Windows", AutoSize = true };
            try
            {
                autostartCheckBox.Checked = Utils.Autostart.IsEnabled;
            }
            catch (Exception ex)
            {
                Utils.Log.Warn("Could not read start with Windows: {0}", ex.Message);
            }
            autostartCheckBox.CheckedChanged += new EventHandler(OnAutostartChanged);
            toolTip.SetToolTip(autostartCheckBox, "Start " + Consts.App.NAME + " when you log on, load the last configuration and start logging (/autostart).");
            Controls.Add(autostartCheckBox);

            chartLimitsCheckBox = new CheckBox() { Text = "Show alarm limits on the chart", AutoSize = true, Checked = Configuration.Chart.ShowAlarmLimits };
            chartLimitsCheckBox.CheckedChanged += new EventHandler(OnChartLimitsChanged);
            toolTip.SetToolTip(chartLimitsCheckBox, "Draw each sensor's low and high alarm limits as dashed lines in its colour on the Chart tab.");
            Controls.Add(chartLimitsCheckBox);
        }

        private void OnWaitingToAppDataChanged(object sender, EventArgs e)
        {
            Configuration.Logging.WaitingToAppData = waitingToAppDataCheckBox.Checked;
            UpdateFileControlsEnabled();
        }

        private void OnMaxWaitingLinesChanged(object sender, EventArgs e)
        {
            Configuration.Logging.MaxWaitingLines = (int)maxWaitingLinesBox.Value;
        }

        private void OnChartLimitsChanged(object sender, EventArgs e)
        {
            Configuration.Chart.ShowAlarmLimits = chartLimitsCheckBox.Checked;
        }

        private void RelocateComponents()
        {
            int margin = (int)(1.5 * Consts.Display.MARGIN);
            int controlX = margin, controlY = margin;

            intervalLabel.Location = new Point(controlX, controlY);
            controlY += margin + intervalLabel.Size.Height;
            logToFileCheckBox.Location = new Point(controlX, controlY);
            controlY += margin + logToFileCheckBox.Size.Height;
            pathLabel.Location = new Point(controlX, controlY);
            controlY += margin + pathLabel.Size.Height;
            fileNameLabel.Location = new Point(controlX, controlY);
            controlY += margin + fileNameLabel.Size.Height;
            controlY += fileNamePreviewLabel.Size.Height + margin / 2;
            recordsPerFileLabel.Location = new Point(controlX, controlY);

            int firstColumnWidth = Helper.Max(intervalLabel.Size.Width, pathLabel.Size.Width, fileNameLabel.Size.Width, recordsPerFileLabel.Size.Width);
            controlX = margin + firstColumnWidth + margin / 2;
            int secondColumnWidth = ClientSize.Width - margin - controlX;

            intervalComboBox.Location = new Point(controlX, controlY);
            SetComboBoxWidth(intervalComboBox, secondColumnWidth);

            int path_textbox_width = secondColumnWidth - pathBtn.Size.Width - margin;

            pathBox.Location = new Point(controlX, controlY);
            pathBox.Size = new Size(path_textbox_width, intervalComboBox.Size.Height);

            pathBtn.Size = new Size(pathBtn.Size.Width, pathBox.Size.Height);

            fileNameComboBox.Location = new Point(controlX, controlY);
            SetComboBoxWidth(fileNameComboBox, secondColumnWidth);

            recordsPerFileComboBox.Location = new Point(controlX, controlY);
            SetComboBoxWidth(recordsPerFileComboBox, secondColumnWidth);

            Helper.CenterForm(intervalLabel, intervalComboBox);
            Helper.CenterForm(pathLabel, pathBox);
            Helper.RightForm(pathBox, pathBtn, margin);
            Helper.CenterForm(fileNameLabel, fileNameComboBox);
            fileNamePreviewLabel.Location = new Point(fileNameComboBox.Location.X, fileNameComboBox.Location.Y + fileNameComboBox.Size.Height + margin / 4);
            Helper.CenterForm(recordsPerFileLabel, recordsPerFileComboBox);
            newFilePerDayCheckBox.Location = new Point(recordsPerFileComboBox.Location.X, recordsPerFileComboBox.Location.Y + recordsPerFileComboBox.Size.Height + margin / 2);
            waitingToAppDataCheckBox.Location = new Point(newFilePerDayCheckBox.Left, newFilePerDayCheckBox.Bottom + margin / 2);
            Helper.RightForm(waitingToAppDataCheckBox, maxWaitingLinesBox, margin / 2);
            Helper.RightForm(maxWaitingLinesBox, waitingLinesLabel, margin / 2);

            openLogFolderBtn.Location = new Point(margin, ClientSize.Height - openLogFolderBtn.Size.Height - margin);
            autostartCheckBox.Location = new Point(openLogFolderBtn.Right + 2 * margin, openLogFolderBtn.Top + (openLogFolderBtn.Height - autostartCheckBox.Height) / 2);
            chartLimitsCheckBox.Location = new Point(autostartCheckBox.Right + 2 * margin, autostartCheckBox.Top);
        }

        private void OnAutostartChanged(object sender, EventArgs e)
        {
            if (updatingAutostart)
            {
                return;
            }
            string configPath = Controller.ConfigFile.Path;
            try
            {
                if (autostartCheckBox.Checked)
                {
                    Utils.Autostart.Enable(configPath);
                    Utils.Log.Info("Start with Windows enabled: {0}", Utils.Autostart.CommandLine(configPath));
                    Event.RaiseMessageChanged(configPath != null
                        ? string.Format("Start with Windows enabled with {0}", System.IO.Path.GetFileName(configPath))
                        : "Start with Windows enabled. Save or load a configuration so it knows which sensors to log.");
                }
                else
                {
                    Utils.Autostart.Disable();
                    Utils.Log.Info("Start with Windows disabled");
                    Event.RaiseMessageChanged("Start with Windows disabled");
                }
            }
            catch (Exception ex)
            {
                Utils.Log.Error("Could not change start with Windows: {0}", ex.Message);
                MessageBox.Show(this, string.Format("Could not change start with Windows:\n\n{0}", ex.Message), Consts.App.NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                updatingAutostart = true;
                autostartCheckBox.Checked = !autostartCheckBox.Checked;
                updatingAutostart = false;
            }
        }

        private void OnOpenLogFolderBtnClick(object sender, EventArgs e)
        {
            Helper.OpenFolder(this, Utils.Path.AppDataDirectory);
        }

        private static void OnDefaultAndCustomComboBoxKeyDown<T>(ComboBox sender, KeyEventArgs e, Configuration.DefaultAndCustom<T> values) where T : Configuration.CustomValue, new()
        {
            if (e.KeyCode != Keys.Enter)
            {
                SetInvalid(sender, false);
                return;
            }
            CommitCustomValue(sender, values);
        }

        private static bool CommitCustomValue<T>(ComboBox sender, Configuration.DefaultAndCustom<T> values) where T : Configuration.CustomValue, new()
        {
            T newValue = new T();
            if (!newValue.TryParse(sender.Text, out int value) || value < 1)
            {
                SetInvalid(sender, true);
                return false;
            }
            newValue.value = value;

            /*
             * Če željenega intervala ni na seznamu, je potrebno predhodnjega odstraniti, ga posodobiti in ga izbrati.
             */
            int i = sender.Items.IndexOf(newValue);
            if (i < 0)
            {
                if (values.customValue != null)
                {
                    sender.Items.Remove(values.customValue);
                }
                values.customValue = newValue;
                sender.Items.Insert(0, values.customValue);
                i = 0;
            }
            sender.SelectedIndex = i;
            sender.Text = sender.Items[i].ToString();
            SetInvalid(sender, false);
            return true;
        }

        private static void SetComboBoxWidth(ComboBox box, int width)
        {
            if (box.Width == width)
            {
                return;
            }
            int start = box.SelectionStart;
            int length = box.SelectionLength;
            box.Width = width;
            if (box.Focused)
            {
                box.Select(start, length);
            }
            else
            {
                box.SelectionLength = 0;
            }
        }

        private static void SetInvalid(ComboBox box, bool invalid)
        {
            box.BackColor = invalid ? Consts.Colors.CRITICAL_BACKGROUND : SystemColors.Window;
        }

        public bool CommitInterval(out string error)
        {
            if (!CommitCustomValue(intervalComboBox, Configuration.Logging.Interval))
            {
                error = string.Format("Invalid interval \"{0}\": enter a number of seconds, or add min or hours (e.g. 5 min).", intervalComboBox.Text);
                return false;
            }
            if (Configuration.Logging.Interval.Value > Controller.Logging.MaxIntervalSeconds)
            {
                SetInvalid(intervalComboBox, true);
                error = string.Format("Invalid interval \"{0}\": at most {1} seconds (1 day).", intervalComboBox.Text, Controller.Logging.MaxIntervalSeconds);
                return false;
            }
            error = null;
            return true;
        }

        public bool CommitRecordsPerFile(out string error)
        {
            error = null;
            if (!Configuration.Logging.LogToFile || Configuration.Logging.NewFilePerDay)
            {
                return true;
            }
            if (!CommitCustomValue(recordsPerFileComboBox, Configuration.Logging.RecordsPerFile))
            {
                error = string.Format("Invalid records per file \"{0}\": enter a number, 1 or more.", recordsPerFileComboBox.Text);
                return false;
            }
            return true;
        }

        public bool CommitFileNamePattern(out string error)
        {
            error = null;
            if (!Configuration.Logging.LogToFile || Utils.FileName.IsValid(fileNameComboBox.Text))
            {
                return true;
            }
            error = string.Format("Invalid file name pattern \"{0}\".", fileNameComboBox.Text);
            return false;
        }

        private void OnIntervalComboBoxIndexChanged(object sender, EventArgs e)
        {
            Configuration.Logging.Interval.currentValue = (Configuration.TimeInterval)intervalComboBox.SelectedItem;
        }

        private void OnIntervalComboBoxKeyDown(object sender, KeyEventArgs e)
        {
            OnDefaultAndCustomComboBoxKeyDown(intervalComboBox, e, Configuration.Logging.Interval);
        }

        private void OnIntervalComboBoxLeave(object sender, EventArgs e)
        {
            CommitCustomValue(intervalComboBox, Configuration.Logging.Interval);
        }

        private void OnFileNameComboBoxTextChanged(object sender, EventArgs e)
        {
            UpdateFileNamePreview();
        }

        private void UpdateFileNamePreview()
        {
            string pattern = fileNameComboBox.Text;
            if (!Utils.FileName.IsValid(pattern))
            {
                SetInvalid(fileNameComboBox, true);
                fileNamePreviewLabel.Text = "Invalid file name pattern";
                return;
            }
            SetInvalid(fileNameComboBox, false);
            Configuration.Logging.FileNamePattern = pattern;
            fileNamePreviewLabel.Text = "Example: " + Utils.FileName.Format(pattern, DateTime.Now, 1);
        }

        private void OnRecordsPerFileComboBoxIndexChanged(object sender, EventArgs e)
        {
            Configuration.Logging.RecordsPerFile.currentValue = (Configuration.CustomValue)recordsPerFileComboBox.SelectedItem;
        }

        private void OnRecordsPerFileComboBoxLeave(object sender, EventArgs e)
        {
            CommitCustomValue(recordsPerFileComboBox, Configuration.Logging.RecordsPerFile);
        }

        private void OnPathTextBoxTextChanged(object sender, EventArgs e)
        {
            Configuration.Logging.LogDirectory = pathTextBox.Text;
        }

        private void OnPathBtnClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Folder for log files";
                dialog.SelectedPath = pathTextBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OnRecordsPerFileComboBoxKeyDown(object sender, KeyEventArgs e)
        {
            OnDefaultAndCustomComboBoxKeyDown(recordsPerFileComboBox, e, Configuration.Logging.RecordsPerFile);
        }

        private static void PopulateComboBox<T>(ComboBox sender, Configuration.DefaultAndCustom<T> defaultValues) where T : Configuration.CustomValue, new()
        {
            T selected = defaultValues.currentValue;
            sender.Items.Clear();
            if (defaultValues.customValue != null)
            {
                sender.Items.Add(defaultValues.customValue);
            }
            foreach (Configuration.CustomValue value in defaultValues.CustomValues)
            {
                sender.Items.Add(value);
            }
            sender.SelectedItem = selected;
            SetInvalid(sender, false);
        }

        private void OnNewFilePerDayChanged(object sender, EventArgs e)
        {
            Configuration.Logging.NewFilePerDay = newFilePerDayCheckBox.Checked;
            UpdateRecordsPerFileEnabled();
        }

        private void OnLogToFileChanged(object sender, EventArgs e)
        {
            Configuration.Logging.LogToFile = logToFileCheckBox.Checked;
            UpdateFileControlsEnabled();
        }

        private void UpdateFileControlsEnabled()
        {
            bool enabled = logToFileCheckBox.Enabled && logToFileCheckBox.Checked;
            foreach (Control control in new Control[] { pathLabel, pathBox, pathBtn, fileNameLabel, fileNameComboBox, fileNamePreviewLabel, newFilePerDayCheckBox, waitingToAppDataCheckBox })
            {
                control.Enabled = enabled;
            }
            maxWaitingLinesBox.Enabled = enabled && waitingToAppDataCheckBox.Checked;
            waitingLinesLabel.Enabled = maxWaitingLinesBox.Enabled;
            UpdateRecordsPerFileEnabled();
        }

        private void UpdateRecordsPerFileEnabled()
        {
            bool enabled = newFilePerDayCheckBox.Enabled && !newFilePerDayCheckBox.Checked;
            recordsPerFileLabel.Enabled = enabled;
            recordsPerFileComboBox.Enabled = enabled;
        }

        private void OnConfigurationLoaded()
        {
            newFilePerDayCheckBox.Checked = Configuration.Logging.NewFilePerDay;
            logToFileCheckBox.Checked = Configuration.Logging.LogToFile;
            waitingToAppDataCheckBox.Checked = Configuration.Logging.WaitingToAppData;
            maxWaitingLinesBox.Value = Configuration.Logging.MaxWaitingLines;
            chartLimitsCheckBox.Checked = Configuration.Chart.ShowAlarmLimits;
            PopulateInterval();
            PopulateRecordsPerFile();
            pathTextBox.Text = Configuration.Logging.LogDirectory;
            fileNameComboBox.Text = Configuration.Logging.FileNamePattern;
            UpdateFileNamePreview();
        }

        private void PopulateInterval()
        {
            PopulateComboBox(intervalComboBox, Configuration.Logging.Interval);
        }

        private void PopulateRecordsPerFile()
        {
            PopulateComboBox(recordsPerFileComboBox, Configuration.Logging.RecordsPerFile);
        }

        public void Disable()
        {
            SetSettingsEnabled(false);
        }

        public void Enable()
        {
            SetSettingsEnabled(true);
        }

        private void SetSettingsEnabled(bool enabled)
        {
            foreach (Control control in Controls)
            {
                if (control != openLogFolderBtn && control != autostartCheckBox && control != chartLimitsCheckBox)
                {
                    control.Enabled = enabled;
                }
            }
            UpdateFileControlsEnabled();
        }

        private void OnResize(object sender, EventArgs e)
        {
            RelocateComponents();
        }

        public SettingsTab()
        {
            InitializeComponents();

            PopulateInterval();
            PopulateRecordsPerFile();
            UpdateFileNamePreview();
            UpdateFileControlsEnabled();

            Event.DeviceBusy += new Event.DisplayEvent(Disable);
            Event.DeviceAvailable += new Event.DisplayEvent(Enable);
            Event.ConfigurationLoaded += new Event.DisplayEvent(OnConfigurationLoaded);

            Resize += new EventHandler(OnResize);
        }
    }
}
