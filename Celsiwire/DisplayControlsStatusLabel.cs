using System.Windows.Forms;

namespace Display.Controls
{
    class StatusLabel : Label
    {
        private const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix;

        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : System.Drawing.SystemColors.GrayText, Flags);
        }
    }
}
