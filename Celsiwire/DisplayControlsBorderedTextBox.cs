using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Display.Controls
{
    class BorderedTextBox : Panel
    {
        private static readonly Color BorderColor = Color.FromArgb(122, 122, 122);

        private const string THEME_CLASS = "COMBOBOX";
        private const int CP_BORDER = 4;
        private const int CBB_NORMAL = 1;
        private const int CBB_HOT = 2;
        private const int CBB_FOCUSED = 3;
        private const int CBB_DISABLED = 4;

        public readonly TextBox TextBox;
        private bool hot;

        public BorderedTextBox()
        {
            BackColor = SystemColors.Window;
            DoubleBuffered = true;
            ResizeRedraw = true;

            TextBox = new TextBox() { BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };
            TextBox.GotFocus += delegate { Invalidate(); };
            TextBox.LostFocus += delegate { Invalidate(); };
            TextBox.MouseEnter += delegate { UpdateHot(); };
            TextBox.MouseLeave += delegate { UpdateHot(); };
            Controls.Add(TextBox);

            Height = TextBox.PreferredHeight + 8;
        }

        private void UpdateHot()
        {
            bool value = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (value != hot)
            {
                hot = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateHot();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            UpdateHot();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int padding = 4;
            TextBox.Location = new Point(padding, Math.Max(1, (ClientSize.Height - TextBox.Height) / 2));
            TextBox.Width = Math.Max(0, ClientSize.Width - 2 * padding);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            TextBox.Focus();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            BackColor = Enabled ? SystemColors.Window : SystemColors.Control;
            TextBox.BackColor = BackColor;
        }

        private VisualStyleElement ThemeElement()
        {
            int state = !Enabled ? CBB_DISABLED : TextBox.Focused ? CBB_FOCUSED : hot ? CBB_HOT : CBB_NORMAL;
            return VisualStyleElement.CreateElement(THEME_CLASS, CP_BORDER, state);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            if (Application.RenderWithVisualStyles)
            {
                VisualStyleElement element = ThemeElement();
                if (VisualStyleRenderer.IsElementDefined(element))
                {
                    new VisualStyleRenderer(element).DrawBackground(e.Graphics, bounds);
                    return;
                }
            }
            Color color = !Enabled ? SystemColors.ControlDark : TextBox.Focused ? SystemColors.Highlight : BorderColor;
            using (Pen pen = new Pen(color))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }
        }
    }
}
