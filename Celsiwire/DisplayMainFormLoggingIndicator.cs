using System;
using System.Drawing;
using System.Windows.Forms;
using Display.Controls;

namespace Display
{
    partial class MainForm
    {
        private LoggingIndicator loggingIndicator;
        private DateTime loggingSince;

        private void InitializeLoggingIndicator()
        {
            loggingIndicator = new LoggingIndicator() { Visible = false };
            Controls.Add(loggingIndicator);
            loggingIndicator.BringToFront();
        }

        /* Shown at the right end of the tab strip while logging; green, or red during an alarm. */
        private void UpdateLoggingIndicator()
        {
            if (loggingIndicator == null)
            {
                return;
            }
            if (!loggingTask.Running)
            {
                loggingIndicator.Visible = false;
                return;
            }
            loggingIndicator.DotColor = AnyAlarmActive() ? Consts.Colors.CRITICAL : Consts.Colors.RUNNING;
            string file = Controller.Logging.CurrentFile;
            statusToolTip.SetToolTip(loggingIndicator, string.Format("Logging since {0}\nMeasure every {1}\n{2}\n{3}",
                loggingSince.ToString("g", System.Globalization.CultureInfo.CurrentCulture),
                Configuration.Logging.Interval.currentValue,
                Utils.Formats.Plural(Controller.Logging.Measurements, "measurement"),
                file != null ? "File: " + file : "Not logging to a file"));
            PositionLoggingIndicator();
        }

        private void PositionLoggingIndicator()
        {
            if (loggingIndicator == null || !loggingTask.Running || !tabControl1.IsHandleCreated || tabControl1.TabCount == 0)
            {
                return;
            }
            int margin = Consts.Display.MARGIN;
            Rectangle firstTab = tabControl1.GetTabRect(0);
            Rectangle lastTab = tabControl1.GetTabRect(tabControl1.TabCount - 1);
            int left = tabControl1.Left + lastTab.Right + margin;
            int right = tabControl1.Right - margin;
            string since = loggingSince.Date == DateTime.Today
                ? loggingSince.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture)
                : loggingSince.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
            string[] texts = {
                string.Format("Logging since {0} · {1}", since, Utils.Formats.Plural(Controller.Logging.Measurements, "measurement")),
                "Logging",
                string.Empty,
            };
            string text = string.Empty;
            foreach (string candidate in texts)
            {
                if (loggingIndicator.MeasureFor(candidate).Width <= right - left)
                {
                    text = candidate;
                    break;
                }
            }
            loggingIndicator.Text = text;
            Size size = loggingIndicator.Size;
            loggingIndicator.Location = new Point(right - size.Width, tabControl1.Top + firstTab.Top + (firstTab.Height - size.Height) / 2);
            loggingIndicator.Visible = size.Width <= right - left;
        }
    }
}
