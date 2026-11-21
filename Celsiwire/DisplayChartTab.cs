using Display.Controls;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    class ChartTab : DeviceViewTab
    {
        private readonly FlowLayoutPanel buttons;
        private readonly Chart chart;
        private readonly Timer slideTimer;
        private readonly Font buttonFont;

        private void AddViewButton(string text, Controller.ChartSeries series, bool selected)
        {
            ToggleButton button = new ToggleButton()
            {
                Text = text,
                Tag = series,
                Font = buttonFont,
                Margin = new Padding((int)(6 * Helper.DpiScale), 0, 0, 0),
                Selected = selected,
            };
            button.FitText();
            button.Click += new EventHandler(OnViewButton);
            buttons.Controls.Add(button);
        }

        private void OnViewButton(object sender, EventArgs e)
        {
            foreach (Control control in buttons.Controls)
            {
                if (control is ToggleButton button)
                {
                    button.Selected = button == sender;
                }
            }
            chart.Series = (Controller.ChartSeries)((Control)sender).Tag;
        }

        private void OnSlideTimer(object sender, EventArgs e)
        {
            if (Visible)
            {
                chart.Invalidate();
            }
        }

        protected override void TileUpdate()
        {
            chart.Invalidate();
        }

        protected override void ResizeComponents()
        {
            FillWithMargin(chart);
        }

        public ChartTab()
        {
            SuspendLayout();

            buttonFont = new Font(Font.FontFamily, Font.Size * 0.95f, Font.Style);
            buttons = new FlowLayoutPanel() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
            chart = new Chart() { Series = Controller.ChartHistory.Day };
            AddViewButton("Day", Controller.ChartHistory.Day, true);
            AddViewButton("Week", Controller.ChartHistory.Week, false);
            AddViewButton("Month", Controller.ChartHistory.Month, false);

            Controls.Add(chart);
            chart.SetToolbar(buttons);

            slideTimer = new Timer() { Interval = 30000 };
            slideTimer.Tick += new EventHandler(OnSlideTimer);
            slideTimer.Start();

            ResumeLayout();

            AttachViewEvents();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                slideTimer.Dispose();
                buttonFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
