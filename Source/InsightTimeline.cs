using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InsightCanvas
{
    /// <summary>A metric line that can be rendered beside event markers.</summary>
    public sealed class InsightMetricTrack
    {
        private readonly ReadOnlyCollection<InsightSample> samples;

        public string Id { get; private set; }
        public string Label { get; private set; }
        public string EntityId { get; private set; }
        public IReadOnlyList<InsightSample> Samples => samples;

        public InsightMetricTrack(string id, string label, string entityId, IEnumerable<InsightSample> samples)
        {
            Id = id ?? string.Empty;
            Label = label ?? id ?? string.Empty;
            EntityId = entityId ?? string.Empty;
            List<InsightSample> copy = new List<InsightSample>();
            if (samples != null)
                foreach (InsightSample sample in samples)
                    if (sample != null) copy.Add(sample);
            copy.Sort((left, right) => left.Tick.CompareTo(right.Tick));
            this.samples = new ReadOnlyCollection<InsightSample>(copy);
        }
    }

    /// <summary>One visible event or a deterministic low-zoom aggregate.</summary>
    public sealed class InsightTimelineCluster
    {
        private readonly ReadOnlyCollection<InsightEvent> events;

        internal InsightTimelineCluster(long start, long end, IReadOnlyList<InsightEvent> values)
        {
            Start = start;
            End = end;
            List<InsightEvent> copy = new List<InsightEvent>();
            for (int i = 0; i < values.Count; i++) copy.Add(values[i]);
            events = new ReadOnlyCollection<InsightEvent>(copy);
        }

        public long Start { get; private set; }
        public long End { get; private set; }
        public int Count => events.Count;
        public IReadOnlyList<InsightEvent> Events => events;
        public InsightEvent Representative => events.Count == 0 ? null : events[events.Count / 2];
    }

    /// <summary>Timeline math and zoom-level aggregation without Unity or game-state access.</summary>
    public static class InsightTimelineMath
    {
        public static InsightTimeRange Bounds(IReadOnlyList<InsightEvent> events)
        {
            if (events == null || events.Count == 0) return InsightTimeRange.Empty;
            long start = events[0].Tick;
            long end = start;
            for (int i = 1; i < events.Count; i++)
            {
                if (events[i].Tick < start) start = events[i].Tick;
                if (events[i].Tick > end) end = events[i].Tick;
            }
            return new InsightTimeRange(start, end);
        }

        /// <summary>Resolves an empty selection as the complete event range rather than as a tick sentinel.</summary>
        public static InsightTimeRange EffectiveRange(InsightTimeRange selectedRange, InsightTimeRange allRange)
        {
            return selectedRange.IsEmpty ? allRange : selectedRange;
        }

        public static float Position(long tick, InsightTimeRange range, float width)
        {
            if (range.IsEmpty || range.End == range.Start) return width * 0.5f;
            double offset = (double)tick - range.Start;
            double span = (double)range.End - range.Start;
            return (float)(offset / span * width);
        }

        public static long TickAt(InsightTimeRange range, double fraction)
        {
            if (range.IsEmpty) return 0L;
            fraction = Math.Max(0d, Math.Min(1d, fraction));
            double value = range.Start + ((double)range.End - range.Start) * fraction;
            if (value <= long.MinValue) return long.MinValue;
            if (value >= long.MaxValue) return long.MaxValue;
            return (long)value;
        }

        public static IReadOnlyList<InsightTimelineCluster> Cluster(IReadOnlyList<InsightEvent> source,
            InsightTimeRange range, float width, int maximumClusters)
        {
            List<InsightTimelineCluster> result = new List<InsightTimelineCluster>();
            if (source == null || source.Count == 0 || maximumClusters <= 0) return result;
            if (range.IsEmpty) range = Bounds(source);
            List<InsightEvent> sorted = new List<InsightEvent>();
            for (int i = 0; i < source.Count; i++)
                if (range.Contains(source[i].Tick)) sorted.Add(source[i]);
            sorted.Sort((left, right) => left.Tick == right.Tick ? string.CompareOrdinal(left.Id, right.Id) : left.Tick.CompareTo(right.Tick));
            if (sorted.Count == 0) return result;
            float pixelsPerCluster = Math.Max(8f, width / Math.Max(1, maximumClusters));
            List<InsightEvent> bucket = new List<InsightEvent>();
            long bucketStart = sorted[0].Tick;
            float bucketX = Position(bucketStart, range, width);
            for (int i = 0; i < sorted.Count; i++)
            {
                InsightEvent item = sorted[i];
                float x = Position(item.Tick, range, width);
                if (bucket.Count > 0 && x - bucketX > pixelsPerCluster)
                {
                    result.Add(new InsightTimelineCluster(bucketStart, bucket[bucket.Count - 1].Tick, bucket));
                    bucket.Clear();
                    bucketStart = item.Tick;
                    bucketX = x;
                }
                bucket.Add(item);
                if (result.Count >= maximumClusters - 1 && i < sorted.Count - 1)
                {
                    for (int j = i + 1; j < sorted.Count; j++) bucket.Add(sorted[j]);
                    break;
                }
            }
            if (bucket.Count > 0) result.Add(new InsightTimelineCluster(bucketStart, bucket[bucket.Count - 1].Tick, bucket));
            return result;
        }

        public static InsightTimeRange Zoom(InsightTimeRange range, float factor, long cursor)
        {
            if (range.IsEmpty) return range;
            factor = factor <= 0f ? 1f : factor;
            double span = Math.Max(1d, (double)range.End - range.Start);
            double newSpan = Math.Max(1d, span / factor);
            if (newSpan < (double)long.MaxValue) newSpan = Math.Truncate(newSpan);
            double t = ((double)cursor - range.Start) / span;
            double offset = newSpan < (double)long.MaxValue ? Math.Truncate(newSpan * t) : newSpan * t;
            double startValue = cursor - offset;
            double endValue = startValue + newSpan;
            if (startValue < long.MinValue)
            {
                endValue += long.MinValue - startValue;
                startValue = long.MinValue;
            }
            if (endValue > long.MaxValue)
            {
                startValue -= endValue - long.MaxValue;
                endValue = long.MaxValue;
            }
            startValue = Math.Max(long.MinValue, Math.Min(long.MaxValue, startValue));
            endValue = Math.Max(long.MinValue, Math.Min(long.MaxValue, endValue));
            return new InsightTimeRange(ToTick(startValue), ToTick(endValue));
        }

        private static long ToTick(double value)
        {
            if (value <= long.MinValue) return long.MinValue;
            if (value >= long.MaxValue) return long.MaxValue;
            return (long)value;
        }
    }
}
