using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    partial class MainForm
    {
        private NotifyIcon trayIcon;
        private Icon trayIdleIcon;
        private Icon trayRunningIcon;
        private Icon trayAlarmIcon;
        private Timer alarmSoundTimer;
        private const int ALARM_REPEAT_MINUTES = 5;
        private bool trayNoticeShown;
        private FormWindowState restoreState = FormWindowState.Normal;
        private const int TRAY_TEXT_MAX = 63;
        private const int TRAY_TEXT_MAX_SHELL = 127;

        private void InitializeTray()
        {
            Event.DeviceUpdated += new Event.DisplayEvent(OnDeviceUpdated);
            Event.AlarmRaised += new Event.MessageEvent(OnAlarmRaised);
            alarmSoundTimer = new Timer() { Interval = ALARM_REPEAT_MINUTES * 60 * 1000 };
            alarmSoundTimer.Tick += new EventHandler(OnAlarmSoundTimer);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripItem openItem = trayMenu.Items.Add("Open " + Consts.App.NAME, null, new EventHandler(OnTrayOpen));
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            trayMenu.Items.Add("Exit", null, new EventHandler(OnTrayExit));

            trayIcon = new NotifyIcon()
            {
                Icon = trayIdleIcon = new Icon(Helper.AppIcon, SystemInformation.SmallIconSize),
                Text = Consts.App.NAME,
                ContextMenuStrip = trayMenu,
                Visible = false,
            };
            trayIcon.DoubleClick += new EventHandler(OnTrayOpen);
        }

        private void MinimizeToTray()
        {
            Hide();
            trayIcon.Visible = true;
            if (!trayNoticeShown)
            {
                trayNoticeShown = true;
                string text = loggingTask.Running ? "Logging continues in the background." : Consts.App.NAME + " is still running.";
                trayIcon.ShowBalloonTip(3000, Consts.App.NAME, text, ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            minimizeOnStart = false;
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = restoreState;
            }
            Activate();
            trayIcon.Visible = false;
            taskbarBadge = -1;
            UpdateTaskbarBadge();
        }

        private void OnTrayOpen(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void OnTrayExit(object sender, EventArgs e)
        {
            RestoreFromTray();
            Close();
        }

        private void OnDeviceUpdated()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Event.DisplayEvent(OnDeviceUpdated));
                return;
            }
            UpdateTrayText();
        }

        private void OnAlarmRaised(string message)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Event.MessageEvent(OnAlarmRaised), message);
                return;
            }
            OnMessageChanged("Alarm: " + message, "Alarm: " + message);
            if (Configuration.Alarms.TrayNotice && trayIcon.Visible)
            {
                trayIcon.ShowBalloonTip(5000, Consts.App.NAME + " alarm", message, ToolTipIcon.Warning);
            }
            if (Configuration.Alarms.PlaySound)
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            alarmSoundTimer.Stop();
            alarmSoundTimer.Start();
        }

        private static bool AnyAlarmActive()
        {
            foreach (Configuration.Device device in Configuration.Communication.deviceList)
            {
                if (device.alarm != Configuration.AlarmState.None)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnAlarmSoundTimer(object sender, EventArgs e)
        {
            if (!loggingTask.Running || !AnyAlarmActive())
            {
                alarmSoundTimer.Stop();
                return;
            }
            if (Configuration.Alarms.PlaySound)
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        private void UpdateTrayIcon()
        {
            Icon icon;
            if (!loggingTask.Running)
            {
                icon = trayIdleIcon;
            }
            else if (AnyAlarmActive())
            {
                icon = trayAlarmIcon ?? (trayAlarmIcon = new Icon(Helper.AlarmIcon, SystemInformation.SmallIconSize));
            }
            else
            {
                icon = trayRunningIcon ?? (trayRunningIcon = new Icon(Helper.RunningIcon, SystemInformation.SmallIconSize));
            }
            if (trayIcon.Icon != icon)
            {
                trayIcon.Icon = icon;
            }
            UpdateLoggingIndicator();
            UpdateTaskbarBadge();
        }

        private void UpdateTrayText()
        {
            UpdateTrayIcon();
            Data.MeasurePoint mp = Controller.Logging.lastMeasurePoint;
            if (!loggingTask.Running || mp == null || mp.Data.Count == 0)
            {
                trayIcon.Text = Consts.App.NAME;
                return;
            }
            System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string>();
            foreach (System.Collections.Generic.KeyValuePair<owDevice.owDevice, float> entry in mp.Data)
            {
                Configuration.Device device = Configuration.Communication.FindDevice(entry.Key);
                bool alarm = device != null && device.alarm != Configuration.AlarmState.None;
                values.Add(string.Format("{0}: {1:F2}°C{2}", Configuration.Communication.GetDisplayName(entry.Key), entry.Value, alarm ? "!" : string.Empty));
            }
            System.Text.StringBuilder text = new System.Text.StringBuilder(Consts.App.NAME);
            for (int i = 0; i < values.Count; i++)
            {
                string line = "\n" + values[i];
                bool last = i == values.Count - 1;
                if (text.Length + line.Length + (last ? 0 : 2) > TRAY_TEXT_MAX_SHELL)
                {
                    text.Append("\n…");
                    break;
                }
                text.Append(line);
            }
            SetTrayText(text.ToString());
        }

        private void SetTrayText(string text)
        {
            if (text.Length <= TRAY_TEXT_MAX)
            {
                trayIcon.Text = text;
                return;
            }
            try
            {
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(NotifyIcon).GetField("text", flags).SetValue(trayIcon, text);
                if (trayIcon.Visible)
                {
                    typeof(NotifyIcon).GetMethod("UpdateIcon", flags).Invoke(trayIcon, new object[] { true });
                }
            }
            catch (Exception)
            {
                trayIcon.Text = text.Substring(0, TRAY_TEXT_MAX - 1) + "…";
            }
        }
    }
}
