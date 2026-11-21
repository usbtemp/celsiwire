using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Display.Controls
{
    /* A colored dot followed by a short text, sized to its content. */
    class LoggingIndicator : Control
    {
        private const int BLINK_HALF_PERIOD = 500;
        private Color dotColor = Color.Gray;
        private readonly Timer blinkTimer = new Timer() { Interval = BLINK_HALF_PERIOD };
        private bool dotOn = true;

        public LoggingIndicator()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            blinkTimer.Tick += new EventHandler(OnBlink);
        }

        private void OnBlink(object sender, EventArgs e)
        {
            dotOn = !dotOn;
            Invalidate();
        }

        /* The dot blinks once a second while the indicator is visible. */
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            dotOn = true;
            if (Visible)
            {
                blinkTimer.Start();
            }
            else
            {
                blinkTimer.Stop();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                blinkTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        public Color DotColor
        {
            get { return dotColor; }
            set
            {
                dotColor = value;
                Invalidate();
            }
        }

        private int DotSize
        {
            get { return Math.Max(6, Font.Height * 3 / 5); }
        }

        private int Gap
        {
            get { return Text.Length > 0 ? Math.Max(4, Font.Height / 3) : 0; }
        }

        public Size MeasureFor(string text)
        {
            Size textSize = text.Length > 0 ? TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty;
            int gap = text.Length > 0 ? Math.Max(4, Font.Height / 3) : 0;
            return new Size(DotSize + gap + textSize.Width + 1, Math.Max(DotSize, Font.Height));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Size = MeasureFor(Text);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Size = MeasureFor(Text);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int dot = DotSize;
            if (dotOn)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush brush = new SolidBrush(dotColor))
                {
                    e.Graphics.FillEllipse(brush, 0, (Height - dot) / 2f, dot, dot);
                }
            }
            if (Text.Length > 0)
            {
                Rectangle bounds = new Rectangle(dot + Gap, 0, Width - dot - Gap, Height);
                TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }
}
