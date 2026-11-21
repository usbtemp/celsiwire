using System;

namespace Display
{
    public class Event
    {
        public delegate void DisplayEvent();
        public delegate void MessageEvent(string s);
        public delegate void StateEvent(bool state);
        public delegate void StatusEvent(string text, string fullText);

        public static event DisplayEvent DeviceBusy;
        public static event DisplayEvent DeviceAvailable;

        public static event StatusEvent MessageChanged;

        public static event DisplayEvent DeviceUpdated;
        public static event DisplayEvent Loaded;
        public static event DisplayEvent ConfigurationLoaded;
        public static event StateEvent PortsRefreshing;
        public static event MessageEvent AlarmRaised;
        public static event DisplayEvent LoggingStarted;

        public static void RaiseDeviceBusy()
        {
            DeviceBusy?.Invoke();
        }

        public static void RaiseDeviceAvailable()
        {
            DeviceAvailable?.Invoke();
        }

        public static void RaiseMessageChanged(string s)
        {
            RaiseMessageChanged(s, null);
        }

        public static void RaiseMessageChanged(string text, string fullText)
        {
            MessageChanged?.Invoke(text, fullText ?? text);
            Utils.Log.Info(fullText ?? text);
        }

        public static void RaiseDeviceUpdated()
        {
            DeviceUpdated?.Invoke();
        }

        public static void RaiseLoggingStarted()
        {
            LoggingStarted?.Invoke();
        }

        public static void RaiseAlarm(string message)
        {
            AlarmRaised?.Invoke(message);
        }

        public static void RaisePortsRefreshing(bool refreshing)
        {
            PortsRefreshing?.Invoke(refreshing);
        }

        public static void RaiseConfigurationLoaded()
        {
            ConfigurationLoaded?.Invoke();
        }

        public static void RaiseLoaded()
        {
            Loaded?.Invoke();
        }
    }
}
