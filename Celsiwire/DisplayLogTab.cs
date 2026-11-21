using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Display
{
    class LogTab : DeviceViewTab
    {
        private readonly Dictionary<owDevice.owDevice, ColumnHeader> headers;
        private readonly ListView list;
        private readonly ToolStripMenuItem copyItem;

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            copyItem.Enabled = list.SelectedItems.Count > 0;
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                OnCopy(sender, e);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                OnSelectAll(sender, e);
                e.Handled = true;
            }
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            list.BeginUpdate();
            foreach (ListViewItem item in list.Items)
            {
                item.Selected = true;
            }
            list.EndUpdate();
        }

        private void OnCopy(object sender, EventArgs e)
        {
            List<ListViewItem> rows = new List<ListViewItem>();
            foreach (ListViewItem item in list.SelectedItems)
            {
                rows.Add(item);
            }
            CopyRows(rows);
        }

        private void OnCopyAll(object sender, EventArgs e)
        {
            List<ListViewItem> rows = new List<ListViewItem>();
            foreach (ListViewItem item in list.Items)
            {
                rows.Add(item);
            }
            CopyRows(rows);
        }

        private string CellText(ListViewItem item, int column)
        {
            if (column == 0)
            {
                return item.Text;
            }
            if (!(item.Tag is Data.MeasurePoint mp) || !(list.Columns[column].Tag is owDevice.owDevice device) || !mp.Data.TryGetValue(device, out float value))
            {
                return string.Empty;
            }
            return Utils.Formats.Value(value);
        }

        private void CopyRows(List<ListViewItem> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }
            StringBuilder text = new StringBuilder();
            for (int c = 0; c < list.Columns.Count; c++)
            {
                text.Append(c > 0 ? "\t" : string.Empty).Append(list.Columns[c].Text);
            }
            text.Append("\r\n");
            for (int r = rows.Count - 1; r >= 0; r--)
            {
                for (int c = 0; c < list.Columns.Count; c++)
                {
                    text.Append(c > 0 ? "\t" : string.Empty).Append(CellText(rows[r], c));
                }
                text.Append("\r\n");
            }
            Helper.CopyToClipboard(text.ToString(), Utils.Formats.Plural(rows.Count, "row"));
        }

        protected override void ResizeComponents()
        {
            FillWithMargin(list);
        }

        protected override void TileUpdate()
        {
            int maxItems = 10;
            bool newTile = false;
            Data.MeasurePoint mp = Controller.Logging.lastMeasurePoint;
            Dictionary<int, string> values = new Dictionary<int, string>();

            SuspendLayout();
            foreach (KeyValuePair<owDevice.owDevice, float> entry in mp.Data)
            {
                if (!headers.TryGetValue(entry.Key, out ColumnHeader header))
                {
                    header = new ColumnHeader()
                    {
                        Text = Configuration.Communication.GetDisplayName(entry.Key),
                        Tag = entry.Key,
                    };
                    headers.Add(entry.Key, header);
                    list.Columns.Add(header);
                    newTile = true;
                }
                string name = Configuration.Communication.GetDisplayName(entry.Key);
                if (header.Text != name)
                {
                    header.Text = name;
                }
                int i = list.Columns.IndexOf(header);
                values[i] = String.Format("{0:F2} °C", entry.Value);
            }

            string[] terms = new string[list.Columns.Count];
            terms[0] = Utils.Formats.Timestamp(mp.Time);
            for (int i = 1; i < terms.Length; i++)
            {
                terms[i] = values.TryGetValue(i, out string value) ? value : string.Empty;
            }
            ListViewItem row = new ListViewItem(terms) { Tag = mp };
            list.Items.Insert(0, row);

            if (list.Items.Count > maxItems)
            {
                list.Items.RemoveAt(maxItems);
            }
            FitColumns();

            if (newTile)
            {
                ResizeComponents();
            }
            ResumeLayout();
        }

        private void FitColumns()
        {
            int padding = TextRenderer.MeasureText("  ", list.Font).Width + 8;
            for (int c = 0; c < list.Columns.Count; c++)
            {
                int width = TextRenderer.MeasureText(list.Columns[c].Text, list.Font).Width;
                foreach (ListViewItem item in list.Items)
                {
                    if (c < item.SubItems.Count)
                    {
                        width = Math.Max(width, TextRenderer.MeasureText(item.SubItems[c].Text, list.Font).Width);
                    }
                }
                width += padding;
                if (list.Columns[c].Width != width)
                {
                    list.Columns[c].Width = width;
                }
            }
        }

        public LogTab()
        {
            list = new ListView();
            headers = new Dictionary<owDevice.owDevice, ColumnHeader>();

            SuspendLayout();

            ColumnHeader timeStampHeader = new ColumnHeader()
            {
                Text = "Timestamp",
                Width = -2,
            };
            list.Columns.Add(timeStampHeader);
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.KeyDown += new KeyEventHandler(OnListKeyDown);

            copyItem = new ToolStripMenuItem("Copy", null, new EventHandler(OnCopy)) { ShortcutKeyDisplayString = "Ctrl+C" };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(copyItem);
            menu.Items.Add(new ToolStripMenuItem("Copy all", null, new EventHandler(OnCopyAll)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Select all", null, new EventHandler(OnSelectAll)) { ShortcutKeyDisplayString = "Ctrl+A" });
            menu.Opening += new System.ComponentModel.CancelEventHandler(OnMenuOpening);
            list.ContextMenuStrip = menu;

            Controls.Add(list);

            ResumeLayout();

            AttachViewEvents();
        }
    }
}
