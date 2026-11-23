using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Display.Controls
{
    /* Temperature chart with "now" in the middle: the newest half span is on the left and slides with time;
       the right half shows the half span before it, fading with age. */
    class Chart : Control
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(0xD3, 0x2F, 0x2F),
            Color.FromArgb(0x19, 0x76, 0xD2),
            Color.FromArgb(0x21, 0x21, 0x21),
            Color.FromArgb(0x38, 0x8E, 0x3C),
            Color.FromArgb(0xF5, 0x7C, 0x00),
            Color.FromArgb(0x7B, 0x1F, 0xA2),
            Color.FromArgb(0x00, 0x83, 0x8F),
            Color.FromArgb(0x6D, 0x4C, 0x41),
            Color.FromArgb(0xC2, 0x18, 0x5B),
            Color.FromArgb(0x82, 0x77, 0x17),
        };
        private static readonly Color GridColor = Color.FromArgb(0xC8, 0xC8, 0xC8);
        private static readonly Color AxisColor = Color.FromArgb(0x80, 0x80, 0x80);
        private static readonly Color DateColor = Color.FromArgb(0x8B, 0x1A, 0x1A);
        private const int FADE_NEWEST = 150;
        private const int FADE_OLDEST = 20;
        private const int LIMIT_ALPHA = 170;
        private static readonly TimeSpan[] TickSteps =
        {
            TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
            TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(5), TimeSpan.FromDays(7),
        };
        private static readonly float[] NiceFactors = { 1f, 2f, 2.5f, 5f, 10f };
        /* Narrowest temperature range the mouse wheel can set, and the limits it stays within, in °C. */
        private const float MIN_RANGE = 0.5f;
        private const float LOWEST = -55f;
        private const float HIGHEST = 130f;
        private const float MAX_RANGE = HIGHEST - LOWEST;
        /* Each wheel notch narrows (up) or widens (down) the temperature range by this factor. */
        private const double WHEEL_ZOOM = 0.8;

        private Controller.ChartSeries series;
        /* Temperature range set with the mouse wheel; without it the range fits the data. */
        private float? manualMin;
        private float? manualMax;
        private readonly ToolTip hoverTip = new ToolTip();
        private string hoverText;
        private ChartLayout hoverLayout;
        private List<Data.ChartSample> hoverSamples;
        private List<owDevice.owDevice> hoverSensors;
        private TimeSpan hoverTolerance;
        /* While rendering an image: TextRenderer follows the Windows font smoothing setting, so the text is drawn smoothed with GDI+ instead. */
        private bool renderingImage;

        public Controller.ChartSeries Series
        {
            get { return series; }
            set
            {
                series = value;
                Invalidate();
            }
        }

        /* Raised when the temperature range switches between fitting the data and a range set with the mouse wheel. */
        public event EventHandler AutoRangeChanged;

        /* True while the temperature range fits the data, false after the mouse wheel has set it. */
        public bool AutoRange
        {
            get { return !manualMin.HasValue; }
        }

        /* Lets the temperature range fit the data again. */
        public void ResetRange()
        {
            if (AutoRange)
            {
                return;
            }
            manualMin = null;
            manualMax = null;
            HideHover();
            Invalidate();
            AutoRangeChanged?.Invoke(this, EventArgs.Empty);
        }

        private class ChartLayout
        {
            public Rectangle Plot;
            public DateTime Now;
            public DateTime Start;
            public TimeSpan Span;
            public float Min;
            public float Max;

            public float X(DateTime time)
            {
                return Plot.Left + (float)((time - Start).Ticks / (double)Span.Ticks * Plot.Width);
            }

            public float Y(float value)
            {
                return Plot.Bottom - (value - Min) / (Max - Min) * Plot.Height;
            }
        }

        private static float NiceStep(float range, int maxTicks)
        {
            float raw = range / Math.Max(1, maxTicks);
            float magnitude = (float)Math.Pow(10, Math.Floor(Math.Log10(raw)));
            foreach (float factor in NiceFactors)
            {
                if (factor * magnitude >= raw)
                {
                    return factor * magnitude;
                }
            }
            return 10f * magnitude;
        }

        private void DrawText(Graphics g, string text, Point point, Color color)
        {
            if (!renderingImage)
            {
                TextRenderer.DrawText(g, text, Font, point, color);
                return;
            }
            /* Set before every call: measuring with TextRenderer can reset it to the Windows setting, which may be unsmoothed. */
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.DrawString(text, Font, brush, point);
            }
        }

        private static Color ColorOf(List<owDevice.owDevice> devices, owDevice.owDevice device)
        {
            int index = devices.IndexOf(device);
            return Palette[(index < 0 ? 0 : index) % Palette.Length];
        }

        /* Sensors in color and legend order: the enabled sensors in configuration order (the CSV column order),
           so a sensor keeps its color after a restart even when another one is not read at first.
           Sensors measured in this run but disabled since follow, in the order they were first measured. */
        private static List<owDevice.owDevice> ColorOrder()
        {
            List<owDevice.owDevice> devices = new List<owDevice.owDevice>();
            foreach (Configuration.Device sensor in Configuration.Communication.GetDeviceList(typeof(owDevice.DS18B20), true))
            {
                devices.Add(sensor.device);
            }
            foreach (owDevice.owDevice device in Controller.ChartHistory.Devices())
            {
                if (!devices.Contains(device))
                {
                    devices.Add(device);
                }
            }
            return devices;
        }

        private static int Alpha(ChartLayout layout, DateTime time)
        {
            double age = (layout.Now - time).Ticks / (double)layout.Span.Ticks;
            double fade = Math.Min(1.0, Math.Max(0.0, (age - 0.5) / 0.5));
            return (int)(FADE_NEWEST - fade * (FADE_NEWEST - FADE_OLDEST));
        }

        private static string TickFormat(TimeSpan span, TimeSpan step)
        {
            if (step < TimeSpan.FromDays(1))
            {
                return "%H";
            }
            return span <= TimeSpan.FromDays(7) ? "ddd d" : "d.M.";
        }

        /* Draws one sensor: the newer half as it was measured, or the older half moved one span to the right and faded. */
        private static void DrawSensor(Graphics g, ChartLayout layout, List<Data.ChartSample> samples, owDevice.owDevice device, Color color, TimeSpan maxGap, bool older)
        {
            DateTime from = older ? layout.Now - layout.Span : layout.Start;
            DateTime to = older ? layout.Start : layout.Now;
            TimeSpan shift = older ? layout.Span : TimeSpan.Zero;
            List<PointF> run = new List<PointF>();
            DateTime previous = DateTime.MinValue;
            using (Pen pen = new Pen(color, 1.5f * Math.Max(1f, Helper.DpiScale)))
            using (SolidBrush brush = new SolidBrush(color))
            {
                foreach (Data.ChartSample sample in samples)
                {
                    if (sample.Time < from || sample.Time > to || (older && sample.Time == to) || !sample.Values.TryGetValue(device, out float value))
                    {
                        continue;
                    }
                    PointF point = new PointF(layout.X(sample.Time + shift), layout.Y(value));
                    if (run.Count > 0 && sample.Time - previous > maxGap)
                    {
                        FlushRun(g, pen, brush, run, !older);
                    }
                    if (older && run.Count > 0)
                    {
                        pen.Color = Color.FromArgb(Alpha(layout, sample.Time), color);
                        g.DrawLine(pen, run[run.Count - 1], point);
                    }
                    if (older)
                    {
                        brush.Color = Color.FromArgb(Alpha(layout, sample.Time), color);
                    }
                    run.Add(point);
                    previous = sample.Time;
                }
                FlushRun(g, pen, brush, run, !older);
            }
        }

        /* A run of connected points: lines for the newer half (the older half already drew its own faded segments), a dot for a single point. */
        private static void FlushRun(Graphics g, Pen pen, SolidBrush brush, List<PointF> run, bool drawLines)
        {
            if (run.Count == 1)
            {
                float r = pen.Width;
                g.FillEllipse(brush, run[0].X - r, run[0].Y - r, 2 * r, 2 * r);
            }
            else if (run.Count > 1 && drawLines)
            {
                g.DrawLines(pen, run.ToArray());
            }
            run.Clear();
        }

        public void Draw(Graphics g, Rectangle bounds, DateTime now)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.FillRectangle(Brushes.White, bounds);
            hoverLayout = null;
            if (series == null)
            {
                return;
            }

            List<Data.ChartSample> samples = series.Snapshot();
            List<owDevice.owDevice> devices = ColorOrder();
            ChartLayout layout = new ChartLayout() { Now = now, Span = series.Span, Start = now - TimeSpan.FromTicks(series.Span.Ticks / 2) };
            DateTime oldest = now - series.Span;

            float min = float.MaxValue;
            float max = float.MinValue;
            List<owDevice.owDevice> shown = new List<owDevice.owDevice>();
            foreach (Data.ChartSample sample in samples)
            {
                if (sample.Time < oldest || sample.Time > now)
                {
                    continue;
                }
                foreach (KeyValuePair<owDevice.owDevice, float> entry in sample.Values)
                {
                    min = Math.Min(min, entry.Value);
                    max = Math.Max(max, entry.Value);
                    if (!shown.Contains(entry.Key))
                    {
                        shown.Add(entry.Key);
                    }
                }
            }
            List<owDevice.owDevice> legend = new List<owDevice.owDevice>();
            foreach (owDevice.owDevice device in devices)
            {
                if (shown.Contains(device))
                {
                    legend.Add(device);
                }
            }
            List<KeyValuePair<owDevice.owDevice, float>> limits = new List<KeyValuePair<owDevice.owDevice, float>>();
            if (Configuration.Chart.ShowAlarmLimits)
            {
                foreach (owDevice.owDevice device in legend)
                {
                    Configuration.Device sensor = Configuration.Communication.FindDevice(device);
                    float?[] deviceLimits = sensor != null ? new float?[] { sensor.Low, sensor.High } : new float?[0];
                    foreach (float? limit in deviceLimits)
                    {
                        if (limit.HasValue)
                        {
                            limits.Add(new KeyValuePair<owDevice.owDevice, float>(device, limit.Value));
                            min = Math.Min(min, limit.Value);
                            max = Math.Max(max, limit.Value);
                        }
                    }
                }
            }
            if (legend.Count == 0)
            {
                min = 15f;
                max = 25f;
            }
            if (max - min < 1f)
            {
                float middle = (min + max) / 2f;
                min = middle - 0.5f;
                max = middle + 0.5f;
            }

            int lineHeight = TextRenderer.MeasureText(g, "0", Font).Height;
            int pad = lineHeight / 2;
            int swatch = 2 * lineHeight;

            /* Legend rows at the bottom, under the time labels; positions are relative to the legend top. */
            List<Rectangle> legendItems = new List<Rectangle>();
            int legendX = bounds.Left + pad;
            int legendY = 0;
            foreach (owDevice.owDevice device in legend)
            {
                int width = swatch + pad / 2 + TextRenderer.MeasureText(g, Configuration.Communication.GetDisplayName(device), Font).Width;
                if (legendX > bounds.Left + pad && legendX + width > bounds.Right - pad)
                {
                    legendX = bounds.Left + pad;
                    legendY += lineHeight;
                }
                legendItems.Add(new Rectangle(legendX, legendY, width, lineHeight));
                legendX += width + lineHeight;
            }
            int legendHeight = legendItems.Count > 0 ? legendY + lineHeight + pad / 2 : 0;

            /* The top row holds the °C label, the date above the now line and the toolbar on the right. */
            int headerTop = bounds.Top + pad / 2;
            int headerHeight = Math.Max(lineHeight, toolbar != null ? toolbar.Height : 0);
            int headerTextY = headerTop + (headerHeight - lineHeight) / 2;
            int top = headerTop + headerHeight + pad / 2;
            int bottom = bounds.Bottom - pad - 2 * lineHeight - legendHeight;
            if (bottom - top < 2 * lineHeight)
            {
                return;
            }
            int maxYTicks = (bottom - top) / (2 * lineHeight);
            float step;
            if (manualMin.HasValue)
            {
                /* The range set with the mouse wheel is kept exactly; the grid lines fall on round values inside it. */
                layout.Min = manualMin.Value;
                layout.Max = manualMax.Value;
                step = NiceStep(layout.Max - layout.Min, maxYTicks);
            }
            else
            {
                step = NiceStep(max - min, maxYTicks);
                layout.Min = (float)Math.Floor(min / step) * step;
                layout.Max = (float)Math.Ceiling(max / step) * step;
                if (layout.Max <= layout.Min)
                {
                    layout.Max = layout.Min + step;
                }
            }

            NumberFormatInfo numberFormat = Utils.Formats.ValueFormat();
            List<float> yValues = new List<float>();
            List<string> yLabels = new List<string>();
            int yLabelWidth = TextRenderer.MeasureText(g, "°C", Font).Width;
            double firstTick = Math.Ceiling(layout.Min / step - 0.001);
            for (int i = 0; ; i++)
            {
                float value = (float)((firstTick + i) * step);
                if (value > layout.Max + step * 0.001f)
                {
                    break;
                }
                string label = value.ToString("0.##", numberFormat);
                yValues.Add(value);
                yLabels.Add(label);
                yLabelWidth = Math.Max(yLabelWidth, TextRenderer.MeasureText(g, label, Font).Width);
            }
            int left = bounds.Left + pad + yLabelWidth + pad / 2;
            int right = bounds.Right - pad - lineHeight;
            if (right - left < 4 * lineHeight)
            {
                return;
            }
            layout.Plot = Rectangle.FromLTRB(left, top, right, bottom);
            Rectangle plot = layout.Plot;

            using (Pen grid = new Pen(GridColor) { DashStyle = DashStyle.Dot })
            using (Pen axis = new Pen(AxisColor))
            {
                /* Temperature grid and labels. */
                for (int i = 0; i < yValues.Count; i++)
                {
                    int y = (int)Math.Round(layout.Y(yValues[i]));
                    if (y > plot.Top && y < plot.Bottom)
                    {
                        g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    }
                    Size size = TextRenderer.MeasureText(g, yLabels[i], Font);
                    DrawText(g, yLabels[i], new Point(plot.Left - pad / 2 - size.Width, y - size.Height / 2), Color.Black);
                }
                DrawText(g, "°C", new Point(plot.Left - pad / 2 - TextRenderer.MeasureText(g, "°C", Font).Width, headerTextY), DateColor);

                /* Time grid: the labels on the right half name the time of the older data drawn there. */
                TimeSpan tickStep = TickSteps[TickSteps.Length - 1];
                string format = TickFormat(series.Span, tickStep);
                foreach (TimeSpan candidate in TickSteps)
                {
                    string candidateFormat = TickFormat(series.Span, candidate);
                    string widest = candidateFormat == "%H" ? "23" : candidateFormat == "d.M." ? "30.12." : "Wed 30";
                    int labelWidth = TextRenderer.MeasureText(g, widest, Font).Width;
                    double count = series.Span.Ticks / (double)candidate.Ticks;
                    if (plot.Width / count >= labelWidth + lineHeight)
                    {
                        tickStep = candidate;
                        format = candidateFormat;
                        break;
                    }
                }
                DateTime end = layout.Start + series.Span;
                DateTime tick = layout.Start.Date;
                if (tickStep < TimeSpan.FromDays(1))
                {
                    tick = tick.AddTicks((long)Math.Ceiling((layout.Start - tick).Ticks / (double)tickStep.Ticks) * tickStep.Ticks);
                }
                while (tick < layout.Start)
                {
                    tick += tickStep;
                }
                for (; tick <= end; tick += tickStep)
                {
                    int x = (int)Math.Round(layout.X(tick));
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                    g.DrawLine(axis, x, plot.Bottom, x, plot.Bottom + pad / 2);
                    DateTime labelTime = tick <= now ? tick : tick - series.Span;
                    string label = labelTime.ToString(format, CultureInfo.InvariantCulture);
                    Size size = TextRenderer.MeasureText(g, label, Font);
                    DrawText(g, label, new Point(x - size.Width / 2, plot.Bottom + pad / 2), Color.Black);
                }
                g.DrawRectangle(axis, plot);
            }

            int nowX = (int)Math.Round(layout.X(now));
            string dateText = now.ToString("ddd, d.M.yyyy", CultureInfo.InvariantCulture);
            Size dateSize = TextRenderer.MeasureText(g, dateText, Font);
            DrawText(g, dateText, new Point(nowX - dateSize.Width / 2, headerTextY), DateColor);
            string timeText = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            Size timeSize = TextRenderer.MeasureText(g, timeText, Font);
            DrawText(g, timeText, new Point(nowX - timeSize.Width / 2, plot.Bottom + pad / 2 + lineHeight), DateColor);

            if (legend.Count == 0)
            {
                string empty = string.Format("No measurements in the last {0}", series.Name);
                Size size = TextRenderer.MeasureText(g, empty, Font);
                DrawText(g, empty, new Point(plot.Left + (plot.Width - size.Width) / 2, plot.Top + (plot.Height - size.Height) / 2), AxisColor);
            }

            /* Sensor lines. */
            TimeSpan interval = TimeSpan.FromSeconds(Configuration.Logging.Interval.Value);
            TimeSpan maxGap = TimeSpan.FromTicks(3 * Math.Max(series.BucketSize.Ticks, interval.Ticks));
            Region clip = g.Clip;
            g.SetClip(Rectangle.Inflate(plot, 0, -1));
            using (Pen limitPen = new Pen(Color.Black, Math.Max(1f, Helper.DpiScale)) { DashStyle = DashStyle.Dash })
            {
                foreach (KeyValuePair<owDevice.owDevice, float> limit in limits)
                {
                    limitPen.Color = Color.FromArgb(LIMIT_ALPHA, ColorOf(devices, limit.Key));
                    int y = (int)Math.Round(layout.Y(limit.Value));
                    g.DrawLine(limitPen, plot.Left, y, plot.Right, y);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (owDevice.owDevice device in legend)
            {
                Color color = ColorOf(devices, device);
                DrawSensor(g, layout, samples, device, color, maxGap, true);
                DrawSensor(g, layout, samples, device, color, maxGap, false);
            }
            g.Clip = clip;
            hoverLayout = layout;
            hoverSamples = samples;
            hoverSensors = legend;
            hoverTolerance = TimeSpan.FromTicks(Math.Max(series.BucketSize.Ticks, interval.Ticks));

            using (Pen nowPen = new Pen(Consts.Colors.RUNNING, 1.5f * Math.Max(1f, Helper.DpiScale)) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(nowPen, nowX, plot.Top, nowX, plot.Bottom);
            }

            for (int i = 0; i < legend.Count; i++)
            {
                Rectangle item = legendItems[i];
                item.Offset(0, plot.Bottom + pad + 2 * lineHeight);
                using (Pen pen = new Pen(ColorOf(devices, legend[i]), 2f * Math.Max(1f, Helper.DpiScale)))
                {
                    int y = item.Top + item.Height / 2;
                    g.DrawLine(pen, item.Left, y, item.Left + swatch, y);
                }
                DrawText(g, Configuration.Communication.GetDisplayName(legend[i]), new Point(item.Left + swatch + pad / 2, item.Top), Color.Black);
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw(e.Graphics, ClientRectangle, DateTime.Now);
        }

        /* The time under the mouse and each sensor's value there; on the right half, the older data drawn there. */
        private string HoverText(Point point)
        {
            ChartLayout layout = hoverLayout;
            if (layout == null || !layout.Plot.Contains(point) || hoverSensors.Count == 0)
            {
                return null;
            }
            DateTime time = layout.Start + TimeSpan.FromTicks((long)((point.X - layout.Plot.Left) / (double)layout.Plot.Width * layout.Span.Ticks));
            if (time > layout.Now)
            {
                time -= layout.Span;
            }
            NumberFormatInfo format = Utils.Formats.ValueFormat();
            StringBuilder text = new StringBuilder(Utils.Formats.DayClock(time));
            bool found = false;
            foreach (owDevice.owDevice device in hoverSensors)
            {
                if (TryValueAt(device, time, out float value))
                {
                    text.AppendFormat("\n{0}: {1} °C", Configuration.Communication.GetDisplayName(device), value.ToString("F2", format));
                    found = true;
                }
            }
            return found ? text.ToString() : null;
        }

        private bool TryValueAt(owDevice.owDevice device, DateTime time, out float value)
        {
            value = 0f;
            TimeSpan best = hoverTolerance;
            bool found = false;
            foreach (Data.ChartSample sample in hoverSamples)
            {
                TimeSpan distance = sample.Time > time ? sample.Time - time : time - sample.Time;
                if (distance <= best && sample.Values.TryGetValue(device, out float candidate))
                {
                    best = distance;
                    value = candidate;
                    found = true;
                }
            }
            return found;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string text = HoverText(e.Location);
            if (text == null)
            {
                HideHover();
                return;
            }
            if (text != hoverText)
            {
                hoverText = text;
                hoverTip.Show(text, this, e.X + 16, e.Y + 16);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HideHover();
        }

        /* Windows sends the mouse wheel to the focused control, so the chart takes the focus when the mouse is over it. */
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Focused && Form.ActiveForm != null && Form.ActiveForm == FindForm())
            {
                Focus();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        /* The wheel narrows (up) or widens (down) the temperature range, keeping the value under the mouse in place. */
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
            ChartLayout layout = hoverLayout;
            if (layout == null || e.Delta == 0)
            {
                return;
            }
            Rectangle plot = layout.Plot;
            float range = layout.Max - layout.Min;
            float newRange = (float)(range * Math.Pow(WHEEL_ZOOM, e.Delta / 120.0));
            newRange = Math.Max(MIN_RANGE, Math.Min(MAX_RANGE, newRange));
            if (newRange == range && !AutoRange)
            {
                return;
            }
            int y = Math.Max(plot.Top, Math.Min(plot.Bottom, e.Y));
            float below = (plot.Bottom - y) / (float)plot.Height;
            float anchor = layout.Min + below * range;
            bool wasAuto = AutoRange;
            /* Slid back inside LOWEST..HIGHEST when it would cross one of them. */
            float newMin = anchor - below * newRange;
            newMin = Math.Max(LOWEST, Math.Min(HIGHEST - newRange, newMin));
            manualMin = newMin;
            manualMax = newMin + newRange;
            HideHover();
            Invalidate();
            if (wasAuto)
            {
                AutoRangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HideHover()
        {
            if (hoverText != null)
            {
                hoverText = null;
                hoverTip.Hide(this);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private Bitmap Render()
        {
            Bitmap bitmap = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                renderingImage = true;
                try
                {
                    Draw(g, new Rectangle(Point.Empty, bitmap.Size), DateTime.Now);
                }
                finally
                {
                    renderingImage = false;
                }
            }
            return bitmap;
        }

        private void OnSavePng(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Save chart";
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.DefaultExt = "png";
                dialog.FileName = string.Format("{0}-{1}-{2}.png", Consts.App.NAME, series.Name, DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture));
                if (System.IO.Directory.Exists(Configuration.Logging.LogDirectory))
                {
                    dialog.InitialDirectory = Configuration.Logging.LogDirectory;
                }
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    using (Bitmap bitmap = Render())
                    {
                        bitmap.Save(dialog.FileName, ImageFormat.Png);
                    }
                    Event.RaiseMessageChanged("Chart saved to " + System.IO.Path.GetFileName(dialog.FileName), "Chart saved to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    Utils.Log.Error("Cannot save chart to {0}: {1}", dialog.FileName, ex.Message);
                    MessageBox.Show(FindForm(), string.Format("Cannot save {0}:\n\n{1}", dialog.FileName, ex.Message), Consts.App.NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnCopyImage(object sender, EventArgs e)
        {
            try
            {
                using (Bitmap bitmap = Render())
                {
                    Clipboard.SetImage(bitmap);
                }
                Event.RaiseMessageChanged("Chart copied to the clipboard");
            }
            catch (Exception ex)
            {
                Event.RaiseMessageChanged("Cannot copy the chart: " + ex.Message);
            }
        }

        private Control toolbar;

        private int LineHeight
        {
            get { return TextRenderer.MeasureText("0", Font).Height; }
        }

        /* Buttons shown in the top right corner, on the line of the date; they are not part of the saved image. */
        public void SetToolbar(Control control)
        {
            toolbar = control;
            Controls.Add(control);
            control.SizeChanged += new EventHandler(OnToolbarSizeChanged);
            PositionToolbar();
        }

        private void OnToolbarSizeChanged(object sender, EventArgs e)
        {
            PositionToolbar();
            Invalidate();
        }

        private void PositionToolbar()
        {
            if (toolbar != null)
            {
                /* The right edge of the last button lines up with the right side of the chart box (see Draw). */
                int lineHeight = LineHeight;
                int pad = lineHeight / 2;
                int plotRight = ClientSize.Width - pad - lineHeight;
                toolbar.Location = new Point(Math.Max(0, plotRight + 1 - toolbar.Width), pad / 2);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionToolbar();
        }

        public Chart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Save as PNG...", null, new EventHandler(OnSavePng)));
            menu.Items.Add(new ToolStripMenuItem("Copy image", null, new EventHandler(OnCopyImage)));
            ContextMenuStrip = menu;
        }
    }
}
