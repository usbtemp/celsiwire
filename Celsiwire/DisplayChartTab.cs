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
        private readonly ToggleButton autoButton;
        private readonly ToolTip toolTip = new ToolTip();
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
                if (control is ToggleButton button && button != autoButton)
                {
                    button.Selected = button == sender;
                }
            }
            chart.Series = (Controller.ChartSeries)((Control)sender).Tag;
        }

        private void OnAutoButton(object sender, EventArgs e)
        {
            chart.ResetRange();
        }

        private void OnAutoRangeChanged(object sender, EventArgs e)
        {
            autoButton.Selected = chart.AutoRange;
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
            chart.AutoRangeChanged += new EventHandler(OnAutoRangeChanged);

            /* Highlighted while the temperature axis fits the data; after the mouse wheel has zoomed it, a click fits it again. */
            autoButton = new ToggleButton()
            {
                Text = "Fit",
                Font = buttonFont,
                Margin = Padding.Empty,
                Selected = chart.AutoRange,
            };
            autoButton.FitText();
            autoButton.Click += new EventHandler(OnAutoButton);
            buttons.Controls.Add(autoButton);
            toolTip.SetToolTip(autoButton, "Fit the temperature axis to the data. The mouse wheel over the chart zooms it.");

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
                toolTip.Dispose();
                buttonFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
