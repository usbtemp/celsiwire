using System;
using System.Windows.Forms;

namespace Display.Controls
{
    class CheckListView : ListView
    {
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_CONTEXTMENU = 0x007B;
        private bool fitting;

        /* True when the last context menu was opened with the Menu key or Shift+F10 (WM_CONTEXTMENU with lParam -1). */
        public bool MenuFromKeyboard { get; private set; }

        public CheckListView()
        {
            View = View.Details;
            CheckBoxes = true;
            HeaderStyle = ColumnHeaderStyle.None;
            FullRowSelect = true;
            MultiSelect = false;
            Columns.Add(string.Empty);
        }

        public void FitColumn()
        {
            if (fitting || Columns.Count == 0)
            {
                return;
            }
            int width = Math.Max(0, ClientSize.Width - 1);
            if (Columns[0].Width == width)
            {
                return;
            }
            fitting = true;
            try
            {
                Columns[0].Width = width;
            }
            finally
            {
                fitting = false;
            }
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            FitColumn();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            ListViewHitTestInfo hit = HitTest(e.Location);
            if (e.Button == MouseButtons.Left && hit.Item != null && hit.Location != ListViewHitTestLocations.StateImage)
            {
                hit.Item.Checked = !hit.Item.Checked;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                return;
            }
            if (m.Msg == WM_CONTEXTMENU)
            {
                MenuFromKeyboard = unchecked((int)m.LParam.ToInt64()) == -1;
            }
            base.WndProc(ref m);
        }
    }
}
