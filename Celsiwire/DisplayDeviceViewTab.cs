using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    abstract class DeviceViewTab : TabPage
    {
        protected abstract void TileUpdate();

        protected abstract void ResizeComponents();

        protected void AttachViewEvents()
        {
            Event.DeviceUpdated += new Event.DisplayEvent(OnDeviceUpdated);
            Resize += new EventHandler(OnResize);
        }

        /* A tab that was never shown has no handle yet; update it through the tab control so it is filled when opened. */
        private void OnDeviceUpdated()
        {
            if (!InvokeRequired)
            {
                TileUpdate();
                return;
            }
            Control target = IsHandleCreated ? this : Parent;
            if (target != null && target.IsHandleCreated && !target.IsDisposed)
            {
                target.BeginInvoke(new Event.DisplayEvent(OnDeviceUpdated));
            }
        }

        /* Fills the tab with one control, leaving the tab margin around it. */
        protected void FillWithMargin(Control control)
        {
            int margin = Consts.Display.TAB_MARGIN;
            control.Location = new Point(margin, margin);
            control.Size = new Size(Math.Max(0, ClientSize.Width - 2 * margin), Math.Max(0, ClientSize.Height - 2 * margin));
        }

        private void OnResize(object sender, EventArgs e)
        {
            ResizeComponents();
        }
    }
}
