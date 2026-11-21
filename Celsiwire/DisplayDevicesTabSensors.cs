using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Display.Controls;

namespace Display
{
    public class DevicesTabSensors : DevicesTabGroup
    {
        private const int MAX_NAME_LENGTH = 40;

        private CheckListView owDevicesListBox;
        private bool populating;
        private ListViewItem menuItem;
        private bool portsRefreshing;
        private Button owDevicesDetectBtn;
        private Button owDevicesSelectAllBtn;
        private Button owDevicesReadBtn;

        private BackgroundTask detectTask;
        private BackgroundTask readTask;

        private void InitializeBackgroundWorker()
        {
            detectTask = new BackgroundTask(DevicesDetectTask, DevicesDetectCompleted);
            readTask = new BackgroundTask(DevicesReadTask, DevicesReadCompleted);
        }

        private void InitializeComponents()
        {
            SuspendLayout();

            owDevicesListBox = new CheckListView();
            owDevicesListBox.ItemChecked += new ItemCheckedEventHandler(OnDeviceItemChecked);
            owDevicesListBox.KeyDown += new KeyEventHandler(OnDevicesListKeyDown);
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Rename", null, new EventHandler(OnRenameMenu)) { ShortcutKeyDisplayString = "F2" });
            menu.Items.Add(new ToolStripMenuItem("Copy ROM", null, new EventHandler(OnCopyRomMenu)));
            menu.Items.Add(new ToolStripMenuItem("Copy name", null, new EventHandler(OnCopyNameMenu)));
            menu.Opening += new CancelEventHandler(OnDevicesMenuOpening);
            owDevicesListBox.ContextMenuStrip = menu;
            owDevicesListBox.AfterLabelEdit += new LabelEditEventHandler(OnDeviceAfterLabelEdit);
            Controls.Add(owDevicesListBox);

            owDevicesDetectBtn = new Button() { Text = "Detect", Padding = Helper.ButtonVerticalPadding };
            owDevicesDetectBtn.Click += new EventHandler(OnDevicesDetectBtn);
            owDevicesDetectBtn.AutoSize = true;
            Controls.Add(owDevicesDetectBtn);

            owDevicesSelectAllBtn = CreateSelectAllButton(new EventHandler(OnDevicesSelectAllBtn));

            owDevicesReadBtn = new Button() { Text = "Read", Padding = Helper.ButtonVerticalPadding };
            owDevicesReadBtn.Click += new EventHandler(OnDevicesReadBtn);
            owDevicesReadBtn.AutoSize = true;
            Controls.Add(owDevicesReadBtn);

            ResumeLayout();
        }

        /* ZAZNAVANJE NAPRAV */

        private void DevicesDetectCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Utils.Log.Error(e.Error.ToString());
                Event.RaiseMessageChanged(string.Format("Device detection failed: {0}", e.Error.Message));
                PopulateDevices();
                Event.RaiseDeviceAvailable();
                return;
            }

            PopulateDevices();

            int nrDetected = (int)e.Result;
            Event.RaiseMessageChanged("Detected " + Utils.Formats.Plural(nrDetected, "device") + ".");
            Event.RaiseDeviceAvailable();
        }

        private void PopulateDevices()
        {
            populating = true;
            owDevicesListBox.BeginUpdate();
            owDevicesListBox.Items.Clear();
            foreach (Configuration.Device device in Configuration.Communication.deviceList)
            {
                if (device.available || device.enabled)
                {
                    ListViewItem item = new ListViewItem() { Tag = device, Checked = device.enabled };
                    UpdateDeviceItem(item);
                    owDevicesListBox.Items.Add(item);
                }
            }
            owDevicesListBox.EndUpdate();
            owDevicesListBox.FitColumn();
            populating = false;
            UpdateSelectAllButton();
            UpdateReadButton();
        }

        private void OnDeviceUpdated()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            BeginInvoke(new MethodInvoker(UpdateDeviceItems));
        }

        private void UpdateDeviceItems()
        {
            if (owDevicesListBox.LabelEdit)
            {
                return;
            }
            foreach (ListViewItem item in owDevicesListBox.Items)
            {
                UpdateDeviceItem(item);
            }
            owDevicesListBox.FitColumn();
        }

        private static void UpdateDeviceItem(ListViewItem item)
        {
            Configuration.Device device = (Configuration.Device)item.Tag;
            item.Text = device.ToString();
            item.ForeColor = device.available ? SystemColors.WindowText : SystemColors.GrayText;
        }

        private bool AllDevicesSelected()
        {
            if (owDevicesListBox.Items.Count == 0)
            {
                return false;
            }
            foreach (ListViewItem item in owDevicesListBox.Items)
            {
                if (!item.Checked)
                {
                    return false;
                }
            }
            return true;
        }

        private void UpdateSelectAllButton()
        {
            SetSelectAllText(owDevicesSelectAllBtn, AllDevicesSelected());
        }

        private void DevicesDetectTask(object sender, DoWorkEventArgs e)
        {
            Event.RaiseMessageChanged("Detecting devices on selected ports ...");
            e.Result = Controller.Devices.Detect();
        }

        private void OnDevicesDetectBtn(object sender, EventArgs e)
        {
            if (Configuration.Communication.PortListCount == 0)
            {
                Event.RaiseMessageChanged("No serial port selected!");
                return;
            }
            owDevicesListBox.Items.Clear();
            UpdateSelectAllButton();
            UpdateReadButton();
            Event.RaiseDeviceBusy();
            detectTask.Start();
        }

        /* BRANJE NAPRAV */

        private void DevicesReadCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            foreach (ListViewItem item in owDevicesListBox.Items)
            {
                UpdateDeviceItem(item);
            }
            if (e.Error != null)
            {
                Utils.Log.Error(e.Error.ToString());
                Event.RaiseMessageChanged(string.Format("Reading temperatures failed: {0}", e.Error.Message));
                Event.RaiseDeviceAvailable();
                return;
            }
            DateTime aDate = DateTime.Now;
            Event.RaiseMessageChanged(string.Format("Temperatures read at {0}", Utils.Formats.Clock(aDate)));
            Event.RaiseDeviceAvailable();
        }

        private void DevicesReadTask(object sender, DoWorkEventArgs e)
        {
            Controller.Devices.ReadAll();
        }

        private void OnDevicesReadBtn(object sender, EventArgs e)
        {
            Event.RaiseDeviceBusy();
            Event.RaiseMessageChanged("Reading temperatures from sensors ...");
            readTask.Start();
        }

        private void OnDevicesSelectAllBtn(object sender, EventArgs e)
        {
            bool select = !AllDevicesSelected();
            foreach (ListViewItem item in owDevicesListBox.Items)
            {
                item.Checked = select;
            }
        }

        private void BeginRename(ListViewItem item)
        {
            Configuration.Device device = (Configuration.Device)item.Tag;
            item.Selected = true;
            item.Focused = true;
            owDevicesListBox.LabelEdit = true;
            item.Text = device.DisplayName;
            item.BeginEdit();
        }

        private void OnDevicesListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2 && owDevicesListBox.SelectedItems.Count == 1)
            {
                BeginRename(owDevicesListBox.SelectedItems[0]);
                e.Handled = true;
            }
        }

        private void OnDevicesMenuOpening(object sender, CancelEventArgs e)
        {
            menuItem = owDevicesListBox.MenuFromKeyboard ? null : owDevicesListBox.HitTest(owDevicesListBox.PointToClient(Control.MousePosition)).Item;
            if (menuItem == null && owDevicesListBox.SelectedItems.Count == 1)
            {
                menuItem = owDevicesListBox.SelectedItems[0];
            }
            e.Cancel = menuItem == null;
        }

        private void OnRenameMenu(object sender, EventArgs e)
        {
            BeginRename(menuItem);
        }

        private void OnCopyRomMenu(object sender, EventArgs e)
        {
            Configuration.Device device = (Configuration.Device)menuItem.Tag;
            Helper.CopyToClipboard(device.RomId, "ROM " + device.RomId);
        }

        private void OnCopyNameMenu(object sender, EventArgs e)
        {
            Configuration.Device device = (Configuration.Device)menuItem.Tag;
            Helper.CopyToClipboard(device.DisplayName, "Name");
        }

        private void OnDeviceAfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            ListViewItem item = owDevicesListBox.Items[e.Item];
            Configuration.Device device = (Configuration.Device)item.Tag;
            if (e.Label != null)
            {
                string name = e.Label.Trim();
                if (name.Length > MAX_NAME_LENGTH)
                {
                    name = name.Substring(0, MAX_NAME_LENGTH);
                }
                if (Configuration.Device.NameValid(name))
                {
                    device.Name = name.Length == 0 || name == device.RomId ? null : name;
                }
                else
                {
                    Event.RaiseMessageChanged("Sensor names cannot start with \"=\".");
                }
            }
            e.CancelEdit = true;
            BeginInvoke(new MethodInvoker(delegate
            {
                owDevicesListBox.LabelEdit = false;
                UpdateDeviceItem(item);
            }));
        }

        private void OnDeviceItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (populating)
            {
                return;
            }
            Configuration.Device device = (Configuration.Device)e.Item.Tag;
            Configuration.Communication.SetDevice(device, e.Item.Checked);
            UpdateSelectAllButton();
        }

        private void ResizeComponents()
        {
            int controlX1 = margin; /* levi stolpec */
            int controlY = ContentTop;

            owDevicesListBox.Location = new Point(controlX1, controlY);
            owDevicesListBox.Size = GetListBoxSize();

            int controlX2 = owDevicesListBox.Size.Width + (2 * margin); /* desni stolpec */

            foreach (Button button in new Button[] { owDevicesDetectBtn, owDevicesSelectAllBtn, owDevicesReadBtn })
            {
                button.Width = ButtonColumnWidth;
            }
            owDevicesDetectBtn.Location = new Point(controlX2, controlY);
            controlY += owDevicesDetectBtn.Size.Height + margin;
            owDevicesSelectAllBtn.Location = new Point(controlX2, controlY);
            controlY += owDevicesSelectAllBtn.Size.Height + margin;
            owDevicesReadBtn.Location = new Point(controlX2, controlY);
        }

        private void OnResize(object sender, EventArgs e)
        {
            ResizeComponents();
        }

        private void OnPortsRefreshing(bool refreshing)
        {
            portsRefreshing = refreshing;
            owDevicesDetectBtn.Enabled = !refreshing;
            UpdateReadButton();
        }

        private void UpdateReadButton()
        {
            owDevicesReadBtn.Enabled = !portsRefreshing && owDevicesListBox.Items.Count > 0;
        }

        public DevicesTabSensors() : base()
        {
            InitializeBackgroundWorker();
            InitializeComponents();
            UpdateReadButton();

            Resize += new EventHandler(OnResize);
            Event.PortsRefreshing += new Event.StateEvent(OnPortsRefreshing);
            Event.ConfigurationLoaded += new Event.DisplayEvent(PopulateDevices);
            Event.DeviceUpdated += new Event.DisplayEvent(OnDeviceUpdated);
        }
    }
}
