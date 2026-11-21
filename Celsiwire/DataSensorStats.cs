using System;

namespace Data
{
    public class SensorStats
    {
        public float Min { get; private set; }
        public float Max { get; private set; }
        public DateTime MinTime { get; private set; }
        public DateTime MaxTime { get; private set; }
        public DateTime Since { get; private set; }
        public int Count { get; private set; }
        private double sum;

        public float Average
        {
            get { return Count > 0 ? (float)(sum / Count) : 0; }
        }

        public void Add(float value, DateTime time)
        {
            if (Count == 0)
            {
                Since = time;
            }

            if (Count == 0 || value < Min)
            {
                Min = value;
                MinTime = time;
            }

            if (Count == 0 || value > Max)
            {
                Max = value;
                MaxTime = time;
            }

            sum += value;
            Count++;
        }

        public void Reset()
        {
            Count = 0;
            sum = 0;
        }
    }
}
