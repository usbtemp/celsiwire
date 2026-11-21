using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Display.Controls;

namespace Display
{
    class DevicesTabSerialPorts : DevicesTabGroup
    {
        private CheckListView serialPortsListBox;
        private bool populating;
        private Button serialPortRefreshBtn;
        private Button serialPortSelectAllBtn;

        private BackgroundTask refreshTask;

        private void InitializeBackgroundWorker()
        {
            refreshTask = new BackgroundTask(SerialPortRefreshTask, SerialPortRefreshCompleted);
        }

        private void InitializeComponents()
        {
            SuspendLayout();

            serialPortsListBox = new CheckListView() { ShowItemToolTips = true };
            serialPortsListBox.ItemCheck += new ItemCheckEventHandler(OnSerialPortItemCheck);
            serialPortsListBox.ItemChecked += new ItemCheckedEventHandler(OnSerialPortItemChecked);
            Controls.Add(serialPortsListBox);

            serialPortRefreshBtn = new Button() { Text = "Refresh", Padding = Helper.ButtonVerticalPadding };
            serialPortRefreshBtn.Click += new EventHandler(OnSerialPortRefreshBtn);
            serialPortRefreshBtn.AutoSize = true;
            Controls.Add(serialPortRefreshBtn);

            serialPortSelectAllBtn = CreateSelectAllButton(new EventHandler(OnSerialPortSelectAllBtn));

            ResumeLayout();
        }

        /* OSVEŽEVANJE - začetek kode */

        private void SerialPortRefreshCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            PopulatePorts();
            Event.RaisePortsRefreshing(false);
            serialPortRefreshBtn.Enabled = true;
            serialPortSelectAllBtn.Enabled = true;
            if (e.Error != null)
            {
                Utils.Log.Error(e.Error.ToString());
                Event.RaiseMessageChanged(string.Format("Refreshing serial ports failed: {0}", e.Error.Message));
                return;
            }

            List<Configuration.Port> newlyEnabled = (List<Configuration.Port>)e.Result;
            if (newlyEnabled.Count > 0)
            {
                Event.RaiseMessageChanged(string.Format("Refresh done. Detected and enabled: {0}", string.Join(", ", newlyEnabled.ConvertAll<string>(PortName).ToArray())));
                return;
            }
            Event.RaiseMessageChanged("Refresh done.");
        }

        private static string PortName(Configuration.Port port)
        {
            return port.ToString();
        }

        private void PopulatePorts()
        {
            populating = true;
            serialPortsListBox.BeginUpdate();
            serialPortsListBox.Items.Clear();
            foreach (Configuration.Port port in Configuration.Communication.GetPorts())
            {
                ListViewItem item = new ListViewItem() { Tag = port, Checked = port.enabled };
                UpdatePortItem(item);
                serialPortsListBox.Items.Add(item);
            }
            serialPortsListBox.EndUpdate();
            serialPortsListBox.FitColumn();
            populating = false;
            UpdateSelectAllButton();
        }

        private bool AllPortsSelected()
        {
            bool anyAvailable = false;
            foreach (ListViewItem item in serialPortsListBox.Items)
            {
                if (!((Configuration.Port)item.Tag).Available)
                {
                    continue;
                }
                anyAvailable = true;
                if (!item.Checked)
                {
                    return false;
                }
            }
            return anyAvailable;
        }

        private void UpdateSelectAllButton()
        {
            SetSelectAllText(serialPortSelectAllBtn, AllPortsSelected());
        }

        private void OnSerialPortItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (!populating)
            {
                UpdateSelectAllButton();
            }
        }

        private void OnDeviceAvailable()
        {
            if (Controller.PortRecovery.TakePortsChanged())
            {
                PopulatePorts();
                return;
            }
            UpdatePortItems();
        }

        private void UpdatePortItems()
        {
            foreach (ListViewItem item in serialPortsListBox.Items)
            {
                UpdatePortItem(item);
            }
        }

        private static void UpdatePortItem(ListViewItem item)
        {
            Configuration.Port port = (Configuration.Port)item.Tag;
            Data.UsbPortInfo usb = port.usb;
            string name = usb != null ? port + " (" + usb.Id + ")" : port.ToString();
            item.ToolTipText = usb != null ? usb.Description : (port.Available ? "Not a USB device" : string.Empty);
            if (port.Available)
            {
                item.Text = port.inUse ? name + " (in use by another program)" : name;
                item.ForeColor = SystemColors.WindowText;
                return;
            }
            item.Text = port.pendingEnable ? name + " (not available, enabled when detected)" : name + " (not available)";
            item.ForeColor = SystemColors.GrayText;
        }

        private void SerialPortRefreshTask(object sender, DoWorkEventArgs e)
        {
            Configuration.Communication.ClearPorts();
            e.Result = Controller.Ports.Detect();
        }

        public void SerialPortRefresh()
        {
            if (refreshTask.Running)
            {
                return;
            }
            serialPortRefreshBtn.Enabled = false;
            serialPortSelectAllBtn.Enabled = false;
            serialPortsListBox.Items.Clear();
            UpdateSelectAllButton();
            Event.RaiseMessageChanged("Refreshing serial ports ...");
            Event.RaisePortsRefreshing(true);
            refreshTask.Start();
        }

        private void OnSerialPortRefreshBtn(object sender, EventArgs e)
        {
            SerialPortRefresh();
        }

        private void OnSerialPortSelectAllBtn(object sender, EventArgs e)
        {
            bool select = !AllPortsSelected();
            foreach (ListViewItem item in serialPortsListBox.Items)
            {
                if (((Configuration.Port)item.Tag).Available)
                {
                    item.Checked = select;
                }
            }
        }

        /* OSVEŽEVANJE - konec kode */

        private void OnSerialPortItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (populating)
            {
                return;
            }
            ListViewItem item = serialPortsListBox.Items[e.Index];
            Configuration.Port port = (Configuration.Port)item.Tag;
            if (!port.Available)
            {
                e.NewValue = CheckState.Unchecked;
                return;
            }
            bool portChecked = e.NewValue == CheckState.Checked;
            Configuration.Communication.SetPort(port, portChecked);
        }


        private void ResizeComponents()
        {
            int controlX1 = margin;
            int controlY = ContentTop;

            if (ButtonWidth == 0)
            {
                ButtonWidth = Helper.Max(serialPortRefreshBtn.PreferredSize.Width, serialPortSelectAllBtn.PreferredSize.Width);
            }

            serialPortsListBox.Location = new Point(controlX1, controlY);
            Size serialPortsListBoxSize = GetListBoxSize();
            serialPortsListBox.Size = serialPortsListBoxSize;

            int defekt = serialPortsListBoxSize.Height - serialPortsListBox.Size.Height;
            Size containerSize = Size;
            containerSize.Height -= defekt;
            Size = containerSize;

            int in_controlX2 = serialPortsListBox.Size.Width + (2 * margin);

            serialPortRefreshBtn.Location = new Point(in_controlX2, controlY);
            serialPortRefreshBtn.Width = ButtonColumnWidth;
            serialPortSelectAllBtn.Width = ButtonColumnWidth;
            controlY += serialPortRefreshBtn.Size.Height + margin;
            serialPortSelectAllBtn.Location = new Point(in_controlX2, controlY);
        }

        private void OnResize(object sender, EventArgs e)
        {
            ResizeComponents();
        }

        private void OnLoaded()
        {
            SerialPortRefresh();
        }

        public DevicesTabSerialPorts() : base()
        {
            InitializeComponents();
            InitializeBackgroundWorker();

            Resize += new EventHandler(OnResize);
            Event.Loaded += new Event.DisplayEvent(OnLoaded);
            Event.ConfigurationLoaded += new Event.DisplayEvent(PopulatePorts);
            Event.DeviceAvailable += new Event.DisplayEvent(OnDeviceAvailable);
        }
    }
}
