using Data;
using System;
using System.Collections.Generic;

namespace Controller
{
    /* Chart data for the whole program run; it is kept when logging is stopped and started again. */
    public static class ChartHistory
    {
        public static readonly ChartSeries Day = new ChartSeries("day", TimeSpan.FromDays(1), 1440);
        public static readonly ChartSeries Week = new ChartSeries("week", TimeSpan.FromDays(7), 2016);
        public static readonly ChartSeries Month = new ChartSeries("month", TimeSpan.FromDays(30), 1440);
        private static readonly ChartSeries[] all = { Day, Week, Month };
        private static readonly List<owDevice.owDevice> devices = new List<owDevice.owDevice>();

        public static void Add(MeasurePoint mp)
        {
            if (mp.Data.Count == 0)
            {
                return;
            }
            lock (devices)
            {
                foreach (owDevice.owDevice device in mp.Data.Keys)
                {
                    if (!devices.Contains(device))
                    {
                        devices.Add(device);
                    }
                }
            }
            foreach (ChartSeries series in all)
            {
                series.Add(mp);
            }
        }

        /* Sensors in the order they were first measured, so each keeps its color. */
        public static List<owDevice.owDevice> Devices()
        {
            lock (devices)
            {
                return new List<owDevice.owDevice>(devices);
            }
        }
    }
}
