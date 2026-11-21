using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display.Controls
{
    /* A small flat button drawn by itself, so the text sits exactly in the middle with no built-in margins. */
    class ToggleButton : Control
    {
        private static readonly Color BorderColor = Color.FromArgb(0xA0, 0xA0, 0xA0);
        private static readonly Color SelectedColor = Color.FromArgb(0xD6, 0xE4, 0xF2);
        private static readonly Color HoverColor = Color.FromArgb(0xEA, 0xF1, 0xF9);
        private const TextFormatFlags TextFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        private bool selected;
        private bool hover;

        public bool Selected
        {
            get { return selected; }
            set
            {
                selected = value;
                Invalidate();
            }
        }

        /* Room around the text: the width of a half-line padded button times 1.5, twice the height of a tight one. */
        public void FitText()
        {
            Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFlags);
            Size = new Size((text.Width + text.Height + 2) * 3 / 2, (text.Height + 4) * 2);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color back = selected ? SelectedColor : hover ? HoverColor : Color.White;
            using (SolidBrush brush = new SolidBrush(back))
            using (Pen border = new Pen(BorderColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, back, TextFlags);
        }

        public ToggleButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }
    }
}
