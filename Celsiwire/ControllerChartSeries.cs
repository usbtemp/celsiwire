using Data;
using System;
using System.Collections.Generic;

namespace Controller
{
    /* Measurements of the last span kept in memory, averaged into a fixed number of buckets. */
    public class ChartSeries
    {
        private class Bucket
        {
            public readonly long Index;
            private readonly DateTime middle;
            private DateTime latest;
            private readonly Dictionary<owDevice.owDevice, double> sums = new Dictionary<owDevice.owDevice, double>();
            private readonly Dictionary<owDevice.owDevice, int> counts = new Dictionary<owDevice.owDevice, int>();

            public Bucket(long index, TimeSpan size)
            {
                Index = index;
                middle = new DateTime(index * size.Ticks + size.Ticks / 2);
            }

            /* The middle of the bucket (e.g. 11:38:30 for 11:38:00-11:38:59), or the newest measurement while it is earlier. */
            public DateTime Time
            {
                get { return latest < middle ? latest : middle; }
            }

            public void Add(MeasurePoint mp)
            {
                if (mp.Time > latest)
                {
                    latest = mp.Time;
                }
                foreach (KeyValuePair<owDevice.owDevice, float> entry in mp.Data)
                {
                    sums.TryGetValue(entry.Key, out double sum);
                    counts.TryGetValue(entry.Key, out int count);
                    sums[entry.Key] = sum + entry.Value;
                    counts[entry.Key] = count + 1;
                }
            }

            public ChartSample ToSample()
            {
                Dictionary<owDevice.owDevice, float> values = new Dictionary<owDevice.owDevice, float>();
                foreach (KeyValuePair<owDevice.owDevice, double> entry in sums)
                {
                    values[entry.Key] = (float)(entry.Value / counts[entry.Key]);
                }
                return new ChartSample(Time, values);
            }
        }

        public readonly string Name;
        public readonly TimeSpan Span;
        public readonly TimeSpan BucketSize;
        private readonly List<Bucket> buckets = new List<Bucket>();

        public ChartSeries(string name, TimeSpan span, int bucketCount)
        {
            Name = name;
            Span = span;
            BucketSize = TimeSpan.FromTicks(span.Ticks / bucketCount);
        }

        public void Add(MeasurePoint mp)
        {
            lock (buckets)
            {
                long index = mp.Time.Ticks / BucketSize.Ticks;
                Bucket last = buckets.Count > 0 ? buckets[buckets.Count - 1] : null;
                if (last == null || last.Index != index)
                {
                    last = new Bucket(index, BucketSize);
                    buckets.Add(last);
                }
                last.Add(mp);

                DateTime oldest = mp.Time - Span;
                int remove = 0;
                while (remove < buckets.Count && buckets[remove].Time < oldest)
                {
                    remove++;
                }
                buckets.RemoveRange(0, remove);
            }
        }

        public List<ChartSample> Snapshot()
        {
            lock (buckets)
            {
                List<ChartSample> samples = new List<ChartSample>(buckets.Count);
                foreach (Bucket bucket in buckets)
                {
                    samples.Add(bucket.ToSample());
                }
                return samples;
            }
        }
    }
}
