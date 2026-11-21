using System;
using System.Collections.Generic;
using Configuration;

namespace Controller
{
    static class Alarms
    {
        public static readonly TimeSpan NoReadingAlarm = TimeSpan.FromMinutes(1);

        public static void Reset(IEnumerable<Configuration.Device> devices)
        {
            DateTime now = DateTime.Now;
            foreach (Configuration.Device device in devices)
            {
                device.alarm = AlarmState.None;
                device.lastRead = now;
            }
        }

        public static void ReadFailed(Configuration.Device device, DateTime time)
        {
            if (device.alarm == AlarmState.NoReading || time - device.lastRead < NoReadingAlarm)
            {
                return;
            }
            device.alarm = AlarmState.NoReading;
            string message = string.Format("{0}: no reading since {1}", device.DisplayName, Utils.Formats.Clock(device.lastRead));
            Utils.Log.Warn("Alarm: {0}", message);
            Display.Event.RaiseAlarm(message);
        }

        private static AlarmState Evaluate(AlarmState current, float value, float? low, float? high, float hysteresis)
        {
            if (high.HasValue && (value > high.Value || (current == AlarmState.High && value > high.Value - hysteresis)))
            {
                return AlarmState.High;
            }
            if (low.HasValue && (value < low.Value || (current == AlarmState.Low && value < low.Value + hysteresis)))
            {
                return AlarmState.Low;
            }
            return AlarmState.None;
        }

        public static void Check(Configuration.Device device, DateTime time)
        {
            device.lastRead = time;
            if (device.alarm == AlarmState.NoReading)
            {
                device.alarm = AlarmState.None;
                Display.Event.RaiseMessageChanged(string.Format("{0}: reading again after a no-reading alarm", device.DisplayName));
            }

            float value = device.Value;
            float? low = device.Low;
            float? high = device.High;
            AlarmState state = Evaluate(device.alarm, value, low, high, Configuration.Alarms.Hysteresis);
            if (state == device.alarm)
            {
                return;
            }
            device.alarm = state;

            if (state == AlarmState.None)
            {
                Display.Event.RaiseMessageChanged(string.Format("{0}: back in range ({1:F1} °C)", device.DisplayName, value));
                return;
            }

            string message = state == AlarmState.High
                ? string.Format("{0}: {1:F1} °C, above {2:F1} °C", device.DisplayName, value, high.Value)
                : string.Format("{0}: {1:F1} °C, below {2:F1} °C", device.DisplayName, value, low.Value);
            Utils.Log.Warn("Alarm: {0}", message);
            Display.Event.RaiseAlarm(message);
        }
    }
}
