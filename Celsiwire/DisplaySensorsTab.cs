using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Display
{
    class SensorsTab : TabPage
    {
        private const int COL_SENSOR = 0;
        private const int COL_OFFSET = 1;
        private const int COL_LOW = 2;
        private const int COL_HIGH = 3;
        private const int COL_VALUE = 4;

        private readonly DataGridView grid;
        private TextBox editingTextBox;
        private string editStartText;
        private readonly NumericUpDown hysteresisBox;
        private readonly CheckBox soundCheckBox;
        private readonly CheckBox trayCheckBox;

        private static bool TryParseLimit(string text, out float? value)
        {
            value = null;
            text = (text ?? string.Empty).Trim().Replace(',', '.');
            if (text.Length == 0)
            {
                return true;
            }
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || number < -100 || number > 200)
            {
                return false;
            }
            value = number;
            return true;
        }

        private static bool TryParseOffset(string text, out float value)
        {
            value = 0f;
            text = (text ?? string.Empty).Trim().Replace(',', '.');
            if (text.Length == 0)
            {
                return true;
            }
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= -Configuration.Device.MaxOffset && value <= Configuration.Device.MaxOffset;
        }

        private static string FormatOffset(float value)
        {
            return value != 0f ? value.ToString("+0.00;-0.00", CultureInfo.CurrentCulture) : string.Empty;
        }

        private static string FormatLimit(float? value)
        {
            return value.HasValue ? value.Value.ToString("0.00", CultureInfo.CurrentCulture) : string.Empty;
        }

        private static string FormatValue(Configuration.Device device)
        {
            return device.HasValue ? string.Format("{0:F2} °C", device.Value) : string.Empty;
        }

        private void PopulateRows()
        {
            if (grid.IsCurrentCellInEditMode)
            {
                return;
            }
            grid.Rows.Clear();
            foreach (Configuration.Device device in Configuration.Communication.deviceList)
            {
                if (!device.available && !device.enabled && !device.Low.HasValue && !device.High.HasValue && device.Offset == 0f)
                {
                    continue;
                }
                int index = grid.Rows.Add(device.DisplayName, FormatOffset(device.Offset), FormatLimit(device.Low), FormatLimit(device.High), FormatValue(device));
                grid.Rows[index].Tag = device;
                grid.Rows[index].Cells[COL_SENSOR].ToolTipText = device.RomId;
            }
        }

        private void PopulateOptions()
        {
            hysteresisBox.Value = Math.Max(hysteresisBox.Minimum, Math.Min(hysteresisBox.Maximum, (decimal)Configuration.Alarms.Hysteresis));
            soundCheckBox.Checked = Configuration.Alarms.PlaySound;
            trayCheckBox.Checked = Configuration.Alarms.TrayNotice;
        }

        private void OnConfigurationLoaded()
        {
            PopulateOptions();
            PopulateRows();
        }

        private void UpdateValues()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                Configuration.Device device = (Configuration.Device)row.Tag;
                row.Cells[COL_VALUE].Value = FormatValue(device);
            }
            grid.Invalidate();
        }

        private void OnRowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!(grid.Rows[e.RowIndex].Tag is Configuration.Device device) || device.alarm == Configuration.AlarmState.None)
            {
                return;
            }
            Rectangle bounds = e.RowBounds;
            int width = Math.Min(bounds.Width, grid.RowHeadersWidth + grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - grid.HorizontalScrollingOffset);
            using (Pen pen = new Pen(Consts.Colors.CRITICAL, 2))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                e.Graphics.DrawRectangle(pen, bounds.Left, bounds.Top, width - 1, bounds.Height - 1);
            }
        }

        private void OnDeviceUpdated()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            BeginInvoke(new Event.DisplayEvent(UpdateValues));
        }

        private void OnVisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                PopulateRows();
            }
        }

        private void OnCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            DataGridViewRow row = grid.Rows[e.RowIndex];
            if (e.ColumnIndex == COL_OFFSET)
            {
                if (!TryParseOffset(Convert.ToString(e.FormattedValue), out float _))
                {
                    row.ErrorText = string.Format("Enter an offset between -{0} and +{0} °C, or leave the cell empty for none.", Configuration.Device.MaxOffset);
                    e.Cancel = true;
                    return;
                }
                row.ErrorText = string.Empty;
                return;
            }
            if (e.ColumnIndex != COL_LOW && e.ColumnIndex != COL_HIGH)
            {
                return;
            }
            if (!TryParseLimit(Convert.ToString(e.FormattedValue), out float? value))
            {
                row.ErrorText = "Enter a temperature in °C, or leave the cell empty for no limit.";
                e.Cancel = true;
                return;
            }
            int otherColumn = e.ColumnIndex == COL_LOW ? COL_HIGH : COL_LOW;
            if (!TryParseLimit(Convert.ToString(row.Cells[otherColumn].Value), out float? other))
            {
                other = null;
            }
            float? low = e.ColumnIndex == COL_LOW ? value : other;
            float? high = e.ColumnIndex == COL_HIGH ? value : other;
            if (low.HasValue && high.HasValue && low.Value >= high.Value)
            {
                row.ErrorText = "Low must be below High.";
                e.Cancel = true;
                return;
            }
            row.ErrorText = string.Empty;
        }

        private bool IsValidCellText(int rowIndex, int columnIndex, string text)
        {
            if (columnIndex == COL_OFFSET)
            {
                return TryParseOffset(text, out float _);
            }
            if (!TryParseLimit(text, out float? value))
            {
                return false;
            }
            int otherColumn = columnIndex == COL_LOW ? COL_HIGH : COL_LOW;
            if (!TryParseLimit(Convert.ToString(grid.Rows[rowIndex].Cells[otherColumn].Value), out float? other))
            {
                return true;
            }
            float? low = columnIndex == COL_LOW ? value : other;
            float? high = columnIndex == COL_HIGH ? value : other;
            return !(low.HasValue && high.HasValue && low.Value >= high.Value);
        }

        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (!(e.Control is TextBox textBox))
            {
                return;
            }
            textBox.TextChanged -= OnEditingTextChanged;
            editingTextBox = textBox;
            SetEditingInvalid(false);
            int column = grid.CurrentCell.ColumnIndex;
            if (column == COL_OFFSET || column == COL_LOW || column == COL_HIGH)
            {
                textBox.TextChanged += OnEditingTextChanged;
                SetEditingInvalid(!IsValidCellText(grid.CurrentCell.RowIndex, column, textBox.Text));
            }
        }

        private void OnEditingTextChanged(object sender, EventArgs e)
        {
            if (grid.CurrentCell == null)
            {
                return;
            }
            SetEditingInvalid(!IsValidCellText(grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex, ((TextBox)sender).Text));
        }

        private void SetEditingInvalid(bool value)
        {
            if (editingTextBox == null)
            {
                return;
            }
            Color color = value ? Consts.Colors.CRITICAL_BACKGROUND : SystemColors.Window;
            if (editingTextBox.BackColor != color)
            {
                editingTextBox.BackColor = color;
            }
        }

        private void OnCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            editStartText = Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
        }

        private void OnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            SetEditingInvalid(false);
            DataGridViewRow row = grid.Rows[e.RowIndex];
            Configuration.Device device = (Configuration.Device)row.Tag;
            if (Convert.ToString(row.Cells[e.ColumnIndex].Value) == editStartText)
            {
                row.ErrorText = string.Empty;
                return;
            }
            if (e.ColumnIndex == COL_OFFSET)
            {
                if (TryParseOffset(Convert.ToString(row.Cells[COL_OFFSET].Value), out float offset))
                {
                    device.Offset = offset;
                    row.Cells[COL_OFFSET].Value = FormatOffset(offset);
                    row.ErrorText = string.Empty;
                    Utils.Log.Info("{0}: offset {1} °C", device.DisplayName, offset.ToString("+0.0##;-0.0##;0", CultureInfo.InvariantCulture));
                }
                return;
            }
            if (e.ColumnIndex != COL_LOW && e.ColumnIndex != COL_HIGH)
            {
                return;
            }
            if (!TryParseLimit(Convert.ToString(row.Cells[e.ColumnIndex].Value), out float? value))
            {
                return;
            }
            if (e.ColumnIndex == COL_LOW)
            {
                device.Low = value;
            }
            else
            {
                device.High = value;
            }
            row.Cells[e.ColumnIndex].Value = FormatLimit(value);
            row.ErrorText = string.Empty;
            Utils.Log.Info("{0}: alarm limits low {1}, high {2}", device.DisplayName,
                device.Low.HasValue ? device.Low.Value.ToString("0.0##", CultureInfo.InvariantCulture) + " °C" : "none",
                device.High.HasValue ? device.High.Value.ToString("0.0##", CultureInfo.InvariantCulture) + " °C" : "none");
        }

        private static DataGridViewTextBoxColumn Column(string header, float weight, bool readOnly, bool right)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn()
            {
                HeaderText = header,
                FillWeight = weight,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            if (right)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            return column;
        }

        public SensorsTab()
        {
            int margin = Consts.Display.TAB_MARGIN;
            Padding = new Padding(margin);

            grid = new DataGridView()
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = true,
                RowHeadersWidth = 28,
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            };
            grid.DefaultCellStyle.Padding = new Padding(0, 3, 0, 3);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 3, 0, 3);
            grid.Columns.Add(Column("Sensor", 40, true, false));
            grid.Columns.Add(Column("Offset (°C)", 20, false, true));
            grid.Columns.Add(Column("Low (°C)", 20, false, true));
            grid.Columns.Add(Column("High (°C)", 20, false, true));
            grid.Columns.Add(Column("Last value", 20, true, true));
            grid.CellValidating += new DataGridViewCellValidatingEventHandler(OnCellValidating);
            grid.CellBeginEdit += new DataGridViewCellCancelEventHandler(OnCellBeginEdit);
            grid.CellEndEdit += new DataGridViewCellEventHandler(OnCellEndEdit);
            grid.EditingControlShowing += new DataGridViewEditingControlShowingEventHandler(OnEditingControlShowing);
            grid.RowPostPaint += new DataGridViewRowPostPaintEventHandler(OnRowPostPaint);

            FlowLayoutPanel options = new FlowLayoutPanel()
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, margin, 0, 0),
            };
            Padding labelMargin = new Padding(0, 6, 2, 0);
            options.Controls.Add(new Label() { Text = "Hysteresis:", AutoSize = true, Margin = labelMargin });
            hysteresisBox = new NumericUpDown() { DecimalPlaces = 1, Increment = 0.1M, Minimum = 0, Maximum = 10, Width = 60 };
            hysteresisBox.ValueChanged += delegate { Configuration.Alarms.Hysteresis = (float)hysteresisBox.Value; };
            options.Controls.Add(hysteresisBox);
            options.Controls.Add(new Label() { Text = "°C", AutoSize = true, Margin = new Padding(2, 6, margin * 2, 0) });
            soundCheckBox = new CheckBox() { Text = "Play sound (repeat every 5 min)", AutoSize = true, Margin = new Padding(0, 4, margin, 0) };
            soundCheckBox.CheckedChanged += delegate { Configuration.Alarms.PlaySound = soundCheckBox.Checked; };
            options.Controls.Add(soundCheckBox);
            trayCheckBox = new CheckBox() { Text = "Show tray notice", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            trayCheckBox.CheckedChanged += delegate { Configuration.Alarms.TrayNotice = trayCheckBox.Checked; };
            options.Controls.Add(trayCheckBox);

            Label hint = new Label()
            {
                Text = "Low and High are alarm limits: an alarm is raised when a value goes below Low or above High. Leave empty for no limit.",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, margin / 2, 0, 0),
            };

            Controls.Add(hint);
            Controls.Add(options);
            Controls.Add(grid);
            grid.BringToFront();

            PopulateOptions();
            PopulateRows();

            VisibleChanged += new EventHandler(OnVisibleChanged);
            Event.DeviceUpdated += new Event.DisplayEvent(OnDeviceUpdated);
            Event.ConfigurationLoaded += new Event.DisplayEvent(OnConfigurationLoaded);
        }
    }
}
