using System;
using System.Collections.Generic;

namespace Data
{
    /* One averaged chart point: the mean time and the mean value of each sensor read in its bucket. */
    public class ChartSample
    {
        public readonly DateTime Time;
        public readonly Dictionary<owDevice.owDevice, float> Values;

        public ChartSample(DateTime time, Dictionary<owDevice.owDevice, float> values)
        {
            Time = time;
            Values = values;
        }
    }
}
