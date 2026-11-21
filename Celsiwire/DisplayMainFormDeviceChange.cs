using System;
using System.Windows.Forms;

namespace Display
{
    partial class MainForm
    {
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DEVICE_CHANGE_DELAY = 1000;

        private readonly Timer deviceChangeTimer = new Timer() { Interval = DEVICE_CHANGE_DELAY };
        private bool deviceChangePending;
        private bool deviceBusy;

        private void InitializeDeviceChange()
        {
            deviceChangeTimer.Tick += new EventHandler(OnDeviceChangeTimer);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.SingleInstance.ShowMessage && Program.SingleInstance.ShowMessage != 0)
            {
                RestoreFromTray();
                return;
            }
            if (m.Msg == TaskbarButtonCreatedMessage && TaskbarButtonCreatedMessage != 0)
            {
                base.WndProc(ref m);
                OnTaskbarButtonCreated();
                return;
            }
            base.WndProc(ref m);
            if (m.Msg != WM_DEVICECHANGE)
            {
                return;
            }
            int change = m.WParam.ToInt32();
            if (change == DBT_DEVICEARRIVAL || change == DBT_DEVICEREMOVECOMPLETE)
            {
                deviceChangeTimer.Stop();
                deviceChangeTimer.Start();
            }
        }

        private void OnDeviceChangeTimer(object sender, EventArgs e)
        {
            deviceChangeTimer.Stop();
            deviceChangePending = true;
            RefreshPortsIfIdle();
        }

        private void RefreshPortsIfIdle()
        {
            if (!deviceChangePending || tabDevices == null || loggingTask.Running || deviceBusy || portsRefreshing)
            {
                return;
            }
            deviceChangePending = false;
            Utils.Log.Info("Serial ports changed, refreshing the list");
            tabDevices.RefreshPorts();
        }
    }
}
