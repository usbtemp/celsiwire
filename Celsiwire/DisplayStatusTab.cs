using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Display
{
    class StatusTab : DeviceViewTab
    {
        protected static int margin = Consts.Display.TAB_MARGIN;

        private int panelColumns;
        private Size panelSize;

        private readonly Dictionary<owDevice.owDevice, StatusTabPanel> panels;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem copyItem;

        private const int PANEL_HEIGHT_PER_SCALE = 40;

        private static int PanelHeight
        {
            get
            {
                float scale = Helper.DpiScale >= 1.25f ? Helper.DpiScale : 1f;
                return (int)Math.Round(Consts.Display.PANEL_HEIGHT + (scale - 1f) * PANEL_HEIGHT_PER_SCALE);
            }
        }

        private void InitPositions()
        {
            int w = ClientSize.Width - margin;

            panelColumns = w / (Consts.Display.PANEL_MIN_WIDTH + margin);
            if (panelColumns == 0)
            {
                panelColumns = 1;
            }

            panelSize = new Size(w / panelColumns - margin, PanelHeight);
        }

        private void PositionPanel(Panel panel, int row, int i)
        {
            int rowHeight = PanelHeight + margin;

            panel.Size = panelSize;
            panel.Location = new Point(margin + i * (panelSize.Width + margin), margin + row * rowHeight);
        }

        protected override void ResizeComponents()
        {
            InitPositions();
            int i = 0;
            int row = 0;
            foreach (KeyValuePair<owDevice.owDevice, StatusTabPanel> entry in panels)
            {
                StatusTabPanel panel = entry.Value;
                PositionPanel(panel, row, i++);
                if (i >= panelColumns)
                {
                    i = 0; row++;
                }
            }
        }

        private static string Tooltip(owDevice.owDevice owdevice, Configuration.Device device)
        {
            string text = owAlgorithms.StringByte.ToHex(owdevice.rom.id);
            if (device == null)
            {
                return text;
            }
            if (device.Low.HasValue)
            {
                text += string.Format(" · Low {0:F1} °C", device.Low.Value);
            }
            if (device.High.HasValue)
            {
                text += string.Format(" · High {0:F1} °C", device.High.Value);
            }
            if (device.Offset != 0f)
            {
                text += string.Format(" · Offset {0:+0.00;-0.00} °C", device.Offset);
            }
            return text;
        }

        private StatusTabPanel MenuPanel()
        {
            Control control = menu.SourceControl;
            while (control != null && !(control is StatusTabPanel))
            {
                control = control.Parent;
            }
            return control as StatusTabPanel;
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            copyItem.Enabled = MenuPanel() != null;
        }

        private const string COPY_HEADER = "Sensor\tValue (°C)\tMin (°C)\tAverage (°C)\tMax (°C)\tTime\r\n";

        private static string CopyLine(StatusTabPanel panel)
        {
            owDevice.owDevice owdevice = (owDevice.owDevice)panel.Tag;
            Configuration.Device device = Configuration.Communication.FindDevice(owdevice);
            string value = string.Empty, min = string.Empty, average = string.Empty, max = string.Empty;
            if (device != null && device.HasValue)
            {
                value = Utils.Formats.Value(device.Value);
            }
            if (device != null && device.stats.Count > 0)
            {
                min = Utils.Formats.Value(device.stats.Min);
                average = Utils.Formats.Value(device.stats.Average);
                max = Utils.Formats.Value(device.stats.Max);
            }
            string time = panel.LastTime.HasValue ? Utils.Formats.Timestamp(panel.LastTime.Value) : string.Empty;
            return string.Join("\t", new string[] { Configuration.Communication.GetDisplayName(owdevice), value, min, average, max, time }) + "\r\n";
        }

        private void OnCopy(object sender, EventArgs e)
        {
            StatusTabPanel panel = MenuPanel();
            if (panel != null)
            {
                Helper.CopyToClipboard(COPY_HEADER + CopyLine(panel), Configuration.Communication.GetDisplayName((owDevice.owDevice)panel.Tag));
            }
        }

        private void OnCopyAll(object sender, EventArgs e)
        {
            if (panels.Count == 0)
            {
                return;
            }
            StringBuilder text = new StringBuilder(COPY_HEADER);
            foreach (StatusTabPanel panel in panels.Values)
            {
                text.Append(CopyLine(panel));
            }
            Helper.CopyToClipboard(text.ToString(), "All sensors");
        }

        protected override void TileUpdate()
        {
            bool newTile = false;

            Data.MeasurePoint mp = Controller.Logging.lastMeasurePoint;

            foreach (KeyValuePair<owDevice.owDevice, float> entry in mp.Data)
            {
                if (!panels.TryGetValue(entry.Key, out StatusTabPanel panel))
                {
                    panel = new StatusTabPanel() { Tag = entry.Key, ContextMenuStrip = menu };
                    panels.Add(entry.Key, panel);
                    SuspendLayout();
                    Controls.Add(panel);
                    ResumeLayout();
                    newTile = true;
                }

                Configuration.Device device = Configuration.Communication.FindDevice(entry.Key);
                panel.UpdateName(Configuration.Communication.GetDisplayName(entry.Key), Tooltip(entry.Key, device));
                panel.SetAlarm(device != null && device.alarm != Configuration.AlarmState.None);
                if (device != null)
                {
                    panel.UpdateStats(device.stats);
                }
                panel.UpdateValue(entry.Value);
                panel.UpdateDateTime(mp.Time);
            }

            foreach (KeyValuePair<owDevice.owDevice, StatusTabPanel> entry in panels)
            {
                if (!mp.Data.ContainsKey(entry.Key))
                {
                    entry.Value.MarkStale();
                    Configuration.Device device = Configuration.Communication.FindDevice(entry.Key);
                    entry.Value.SetAlarm(device != null && device.alarm != Configuration.AlarmState.None);
                }
            }

            if (newTile)
            {
                ResizeComponents();
            }
        }

        public StatusTab()
        {
            panels = new Dictionary<owDevice.owDevice, StatusTabPanel>();

            copyItem = new ToolStripMenuItem("Copy", null, new EventHandler(OnCopy));
            menu = new ContextMenuStrip();
            menu.Items.Add(copyItem);
            menu.Items.Add(new ToolStripMenuItem("Copy all sensors", null, new EventHandler(OnCopyAll)));
            menu.Opening += new System.ComponentModel.CancelEventHandler(OnMenuOpening);
            ContextMenuStrip = menu;

            AttachViewEvents();
        }
    }
}
