using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    class StatusTabPanel : Panel
    {
        private readonly Label dateTimeLabel;
        private readonly Label valueLabel;
        private readonly Label statsLabel;
        private readonly Label romLabel;
        private readonly ToolTip toolTip;
        private string romId;

        private bool canResize;
        public DateTime? LastTime { get; private set; }
        private bool alarm;
        protected int margin = Consts.Display.TAB_MARGIN;
        protected int spacing = Consts.Display.TAB_MARGIN / 3;

        public void UpdateName(string name, string tooltip)
        {
            romLabel.Text = name;
            if (tooltip == romId)
            {
                return;
            }
            romId = tooltip;
            foreach (Control control in new Control[] { this, romLabel, valueLabel, dateTimeLabel })
            {
                toolTip.SetToolTip(control, tooltip);
            }
        }

        public void SetAlarm(bool value)
        {
            if (alarm == value)
            {
                return;
            }
            alarm = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int width = alarm ? 3 : 1;
            using (Pen pen = new Pen(alarm ? Consts.Colors.CRITICAL : SystemColors.ControlDark, width))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
        }

        public void UpdateValue(float v)
        {
            if (valueLabel == null)
            {
                return;
            }
            valueLabel.Text = String.Format("{0:F2} °C", v);
            valueLabel.ForeColor = SystemColors.ControlText;
            statsLabel.ForeColor = SystemColors.ControlText;
            dateTimeLabel.ForeColor = SystemColors.ControlText;
        }

        private static string FormatTime(DateTime time)
        {
            return time.Date == DateTime.Today ? time.ToLongTimeString() : Utils.Formats.DateAndTime(time);
        }

        public void UpdateStats(Data.SensorStats stats)
        {
            if (stats.Count == 0)
            {
                statsLabel.Text = string.Empty;
                toolTip.SetToolTip(statsLabel, null);
                return;
            }
            statsLabel.Text = string.Format("{0:F2} °C · {1:F2} °C · {2:F2} °C", stats.Min, stats.Average, stats.Max);
            toolTip.SetToolTip(statsLabel, string.Format("Min · Average · Max\n\nMin {0:F2} °C at {1}\nMax {2:F2} °C at {3}\nAverage of {4} since {5}",
                stats.Min, FormatTime(stats.MinTime), stats.Max, FormatTime(stats.MaxTime), Utils.Formats.Plural(stats.Count, "reading"), FormatTime(stats.Since)));
        }

        public void MarkStale()
        {
            valueLabel.ForeColor = SystemColors.GrayText;
            statsLabel.ForeColor = SystemColors.GrayText;
            dateTimeLabel.ForeColor = Consts.Colors.CRITICAL;
        }

        public void UpdateDateTime(DateTime dt)
        {
            if (dateTimeLabel == null)
            {
                return;
            }
            LastTime = dt;
            dateTimeLabel.Text = Utils.Formats.DateAndTime(dt);
        }

        private void ResizeComponents()
        {
            int controlY = margin;
            int w = ClientSize.Width;

            if (w < 0)
            {
                return;
            }

            /* The labels one below the other, full width, with spacing between them and the margin around them. */
            Label[] labels = { romLabel, valueLabel, statsLabel, dateTimeLabel };
            for (int i = 0; i < labels.Length; i++)
            {
                int h = labels[i].Size.Height;
                labels[i].Size = new Size(w - 2 * margin, h);
                labels[i].Location = new Point(margin, controlY);
                controlY += h + (i < labels.Length - 1 ? spacing : margin);
            }
            if (ClientSize.Height < controlY)
            {
                canResize = false;
                Size = new Size(w, controlY);
                canResize = true;
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (!canResize)
            {
                return;
            }
            ResizeComponents();
        }

        public StatusTabPanel()
        {
            BorderStyle = BorderStyle.None;
            ResizeRedraw = true;
            DoubleBuffered = true;
            toolTip = new ToolTip();

            SuspendLayout();

            romLabel = new Label() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Helper.UiFont.FontFamily, 10) };
            Controls.Add(romLabel);

            valueLabel = new Label() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Helper.UiFont.FontFamily, 18, FontStyle.Bold) };
            valueLabel.Height = valueLabel.PreferredHeight;
            Controls.Add(valueLabel);

            statsLabel = new Label() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Helper.UiFont.FontFamily, 10) };
            Controls.Add(statsLabel);

            dateTimeLabel = new Label() { TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Helper.UiFont.FontFamily, 10) };
            Controls.Add(dateTimeLabel);

            ResumeLayout();

            canResize = true;
            Resize += new EventHandler(OnResize);
        }
    }
}
