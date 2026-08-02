using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InsightCanvas
{
    /// <summary>A small, Unity-free point used by layout providers.</summary>
    public struct InsightPoint : IEquatable<InsightPoint>
    {
        public float X;
        public float Y;

        public InsightPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(InsightPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is InsightPoint && Equals((InsightPoint)obj);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => X + "," + Y;
    }

    /// <summary>A numeric range. The bounds are normalized on construction.</summary>
    public struct InsightRange : IEquatable<InsightRange>
    {
        public float Minimum;
        public float Maximum;

        public InsightRange(float minimum, float maximum)
        {
            if (minimum <= maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }
            else
            {
                Minimum = maximum;
                Maximum = minimum;
            }
        }

        public float Size => Maximum - Minimum;
        public float Lerp(float amount) => Minimum + Size * amount;
        public bool Contains(float value) => value >= Minimum && value <= Maximum;
        public bool Equals(InsightRange other) => Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);
        public override bool Equals(object obj) => obj is InsightRange && Equals((InsightRange)obj);
        public override int GetHashCode() => (Minimum.GetHashCode() * 397) ^ Maximum.GetHashCode();
        public override string ToString() => Minimum + ".." + Maximum;
    }

    /// <summary>A time range in game ticks. An empty range means no explicit filter.</summary>
    public struct InsightTimeRange : IEquatable<InsightTimeRange>
    {
        public long Start;
        public long End;

        public InsightTimeRange(long start, long end)
        {
            if (start <= end)
            {
                Start = start;
                End = end;
            }
            else
            {
                Start = end;
                End = start;
            }
        }

        public bool IsEmpty => End < Start;
        public static InsightTimeRange Empty => new InsightTimeRange(1, 0);
        public bool Contains(long tick) => IsEmpty || tick >= Start && tick <= End;
        public bool Equals(InsightTimeRange other) => Start == other.Start && End == other.End;
        public override bool Equals(object obj) => obj is InsightTimeRange && Equals((InsightTimeRange)obj);
        public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();
    }

    /// <summary>Trend direction attached to a metric or history sample.</summary>
    public enum InsightTrend
    {
        Flat,
        Rising,
        Falling,
        Volatile
    }

    /// <summary>A value sample used by metric tracks and the event river.</summary>
    public sealed class InsightSample
    {
        public long Tick { get; private set; }
        public float Value { get; private set; }

        public InsightSample(long tick, float value)
        {
            Tick = tick;
            Value = value;
        }
    }

    /// <summary>Semantic metric data. Unknown values should use Known=false instead of a sentinel number.</summary>
    public sealed class InsightMetric
    {
        private readonly ReadOnlyCollection<InsightSample> history;

        public string Label { get; private set; }
        public float Value { get; private set; }
        public InsightRange Range { get; private set; }
        public bool HasRange { get; private set; }
        public bool Known { get; private set; }
        public float Confidence { get; private set; }
        public float? Threshold { get; private set; }
        public InsightTrend Trend { get; private set; }
        public IReadOnlyList<InsightSample> History => history;

        public InsightMetric(string label, float value, bool known = true)
            : this(label, value, new InsightRange(value, value), false, known, 1f, null, InsightTrend.Flat, null) { }

        public InsightMetric(string label, float value, InsightRange range, bool hasRange = true,
            bool known = true, float confidence = 1f, float? threshold = null,
            InsightTrend trend = InsightTrend.Flat, IEnumerable<InsightSample> history = null)
        {
            Label = label ?? string.Empty;
            Value = value;
            Range = range;
            HasRange = hasRange;
            Known = known;
            Confidence = Clamp01(confidence);
            Threshold = threshold;
            Trend = trend;
            List<InsightSample> copy = new List<InsightSample>();
            if (history != null)
                foreach (InsightSample sample in history)
                    if (sample != null) copy.Add(sample);
            this.history = new ReadOnlyCollection<InsightSample>(copy);
        }

        public static InsightMetric Unknown(string label, InsightRange approximateRange, float confidence)
        {
            return new InsightMetric(label, approximateRange.Lerp(0.5f), approximateRange, true, false,
                confidence, null, InsightTrend.Flat, null);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>An entity in a semantic visualization model.</summary>
    public sealed class InsightEntity
    {
        private readonly ReadOnlyCollection<string> badges;

        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Subtitle { get; private set; }
        public string Category { get; private set; }
        public object Source { get; private set; }
        public object Icon { get; private set; }
        public InsightPoint? ManualPosition { get; private set; }
        public IReadOnlyList<string> Badges => badges;

        public InsightEntity(string id, string label, string subtitle = null, string category = null,
            object source = null, object icon = null, IEnumerable<string> badges = null,
            InsightPoint? manualPosition = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Entity ids must not be empty.", nameof(id));
            Id = id;
            Label = label ?? id;
            Subtitle = subtitle ?? string.Empty;
            Category = category ?? string.Empty;
            Source = source;
            Icon = icon;
            ManualPosition = manualPosition;
            List<string> copy = new List<string>();
            if (badges != null)
                foreach (string badge in badges)
                    if (!string.IsNullOrWhiteSpace(badge)) copy.Add(badge);
            this.badges = new ReadOnlyCollection<string>(copy);
        }
    }

    /// <summary>A user-invokable action associated with an entity or event.</summary>
    public sealed class InsightAction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Tooltip { get; private set; }
        public bool Enabled { get; private set; }
        public bool CloseWindowAfterInvoke { get; private set; }
        public Action Callback { get; private set; }

        public InsightAction(string id, string label, Action callback, bool enabled = true, string tooltip = null,
            bool closeWindowAfterInvoke = false)
        {
            Id = id ?? string.Empty;
            Label = label ?? id ?? string.Empty;
            Callback = callback;
            Enabled = enabled;
            Tooltip = tooltip ?? string.Empty;
            CloseWindowAfterInvoke = closeWindowAfterInvoke;
        }

        /// <summary>Executes the callback if it is available. UI callers can safely invoke this method.</summary>
        public bool Invoke()
        {
            if (!Enabled || Callback == null) return false;
            Callback();
            return true;
        }
    }

    /// <summary>A directed or undirected semantic relation between entities.</summary>
    public sealed class InsightRelation
    {
        public string FromId { get; private set; }
        public string ToId { get; private set; }
        public string Type { get; private set; }
        public float Weight { get; private set; }
        public float Confidence { get; private set; }
        public bool Directed { get; private set; }
        public bool Known { get; private set; }

        public InsightRelation(string fromId, string toId, string type, float weight = 1f,
            bool directed = true, float confidence = 1f, bool known = true)
        {
            FromId = fromId ?? string.Empty;
            ToId = toId ?? string.Empty;
            Type = type ?? string.Empty;
            Weight = weight;
            Directed = directed;
            Confidence = confidence < 0f ? 0f : confidence > 1f ? 1f : confidence;
            Known = known;
        }
    }

    /// <summary>A timeline event optionally associated with one or more entities.</summary>
    public sealed class InsightEvent
    {
        private readonly ReadOnlyCollection<string> entityIds;

        public string Id { get; private set; }
        public long Tick { get; private set; }
        public string Label { get; private set; }
        public string Category { get; private set; }
        public float Severity { get; private set; }
        public bool Known { get; private set; }
        public string MapLinkId { get; private set; }
        public IReadOnlyList<string> EntityIds => entityIds;

        public InsightEvent(string id, long tick, string label, string category = null,
            IEnumerable<string> entityIds = null, float severity = 0f, bool known = true, string mapLinkId = null)
        {
            Id = id ?? string.Empty;
            Tick = tick;
            Label = label ?? string.Empty;
            Category = category ?? string.Empty;
            Severity = severity < 0f ? 0f : severity > 1f ? 1f : severity;
            Known = known;
            MapLinkId = mapLinkId;
            List<string> copy = new List<string>();
            if (entityIds != null)
                foreach (string entityId in entityIds)
                    if (!string.IsNullOrWhiteSpace(entityId)) copy.Add(entityId);
            this.entityIds = new ReadOnlyCollection<string>(copy);
        }
    }

    /// <summary>Result of model validation. Consumer mods can display or log every issue.</summary>
    public sealed class InsightModelValidation
    {
        private readonly ReadOnlyCollection<string> errors;
        private readonly ReadOnlyCollection<string> warnings;

        internal InsightModelValidation(List<string> errors, List<string> warnings)
        {
            this.errors = new ReadOnlyCollection<string>(errors ?? new List<string>());
            this.warnings = new ReadOnlyCollection<string>(warnings ?? new List<string>());
        }

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Count == 0;
    }

    /// <summary>
    /// Mutable model builder used by integrations. The window takes an immutable snapshot, so callers may
    /// continue collecting data without mutating a currently-rendered frame.
    /// </summary>
    public sealed class InsightModel
    {
        private readonly Dictionary<string, InsightEntity> entities = new Dictionary<string, InsightEntity>(StringComparer.Ordinal);
        private readonly List<InsightRelation> relations = new List<InsightRelation>();
        private readonly Dictionary<string, List<InsightMetric>> metrics = new Dictionary<string, List<InsightMetric>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<InsightAction>> actions = new Dictionary<string, List<InsightAction>>(StringComparer.Ordinal);
        private readonly Dictionary<string, InsightExplanation> explanations = new Dictionary<string, InsightExplanation>(StringComparer.Ordinal);
        private readonly List<InsightEvent> events = new List<InsightEvent>();
        private readonly List<string> authoringErrors = new List<string>();

        public string Id { get; private set; }
        public int Revision { get; private set; }

        private InsightModel(string id)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "InsightModel" : id;
        }

        /// <summary>Starts a fluent semantic model.</summary>
        public static InsightModel Create(string id) => new InsightModel(id);

        /// <summary>Removes the previous publication before a producer rebuilds its next snapshot.</summary>
        public InsightModel Clear()
        {
            entities.Clear();
            relations.Clear();
            metrics.Clear();
            actions.Clear();
            explanations.Clear();
            events.Clear();
            authoringErrors.Clear();
            Revision++;
            return this;
        }

        /// <summary>Adds an entity with a stable caller-owned id.</summary>
        public InsightModel Entity(InsightEntity entity)
        {
            if (entity == null) return this;
            if (entities.ContainsKey(entity.Id))
            {
                authoringErrors.Add("Duplicate entity id: " + entity.Id);
                return this;
            }
            entities.Add(entity.Id, entity);
            Revision++;
            return this;
        }

        /// <summary>Convenience overload for simple entities.</summary>
        public InsightModel Entity(string id, string label, string subtitle = null, string category = null)
        {
            return Entity(new InsightEntity(id, label, subtitle, category));
        }

        /// <summary>Adds a relation. Missing endpoints are reported by Validate rather than throwing during collection.</summary>
        public InsightModel Relation(string fromId, string toId, string type, float weight = 1f,
            bool directed = true, float confidence = 1f, bool known = true)
        {
            relations.Add(new InsightRelation(fromId, toId, type, weight, directed, confidence, known));
            Revision++;
            return this;
        }

        /// <summary>Adds a metric to an entity.</summary>
        public InsightModel Metric(string entityId, string label, InsightMetric metric)
        {
            if (metric == null) return this;
            List<InsightMetric> list;
            if (!metrics.TryGetValue(entityId ?? string.Empty, out list))
            {
                list = new List<InsightMetric>();
                metrics[entityId ?? string.Empty] = list;
            }
            list.Add(metric);
            Revision++;
            return this;
        }

        /// <summary>Convenience overload for a known scalar metric.</summary>
        public InsightModel Metric(string entityId, string label, float value)
        {
            return Metric(entityId, label, new InsightMetric(label, value));
        }

        /// <summary>Adds an action to an entity.</summary>
        public InsightModel Action(string entityId, string id, string label, Action callback,
            bool enabled = true, string tooltip = null)
        {
            return Action(entityId, new InsightAction(id, label, callback, enabled, tooltip));
        }

        /// <summary>Convenience overload that derives a stable action id from the entity and label.</summary>
        public InsightModel Action(string entityId, string label, Action callback)
        {
            return Action(entityId, InsightIds.Stable(entityId, label), label, callback);
        }

        /// <summary>Adds an already constructed action.</summary>
        public InsightModel Action(string entityId, InsightAction action)
        {
            if (action == null) return this;
            List<InsightAction> list;
            if (!actions.TryGetValue(entityId ?? string.Empty, out list))
            {
                list = new List<InsightAction>();
                actions[entityId ?? string.Empty] = list;
            }
            list.Add(action);
            Revision++;
            return this;
        }

        /// <summary>Associates an explanation with an entity.</summary>
        public InsightModel Explanation(string entityId, InsightExplanation explanation)
        {
            if (explanation == null) return this;
            explanations[entityId ?? string.Empty] = explanation;
            Revision++;
            return this;
        }

        /// <summary>Adds a timeline event.</summary>
        public InsightModel Event(InsightEvent insightEvent)
        {
            if (insightEvent == null) return this;
            events.Add(insightEvent);
            Revision++;
            return this;
        }

        /// <summary>Validates endpoints, references, ids, and finite numeric data.</summary>
        public InsightModelValidation Validate()
        {
            List<string> errors = new List<string>(authoringErrors);
            List<string> warnings = new List<string>();
            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < relations.Count; i++)
            {
                InsightRelation relation = relations[i];
                if (!entities.ContainsKey(relation.FromId)) errors.Add("Relation source is missing: " + relation.FromId);
                if (!entities.ContainsKey(relation.ToId)) errors.Add("Relation target is missing: " + relation.ToId);
                if (string.IsNullOrWhiteSpace(relation.Type)) warnings.Add("Relation has no type: " + relation.FromId + " -> " + relation.ToId);
                if (!Finite(relation.Weight) || !Finite(relation.Confidence)) errors.Add("Relation contains a non-finite value: " + relation.FromId + " -> " + relation.ToId);
            }
            foreach (KeyValuePair<string, List<InsightMetric>> pair in metrics)
            {
                if (!entities.ContainsKey(pair.Key)) errors.Add("Metrics reference a missing entity: " + pair.Key);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    InsightMetric metric = pair.Value[i];
                    if (float.IsNaN(metric.Value) || float.IsInfinity(metric.Value)) errors.Add("Metric is not finite: " + pair.Key + "/" + metric.Label);
                    if (!Finite(metric.Confidence) || metric.Confidence < 0f || metric.Confidence > 1f) errors.Add("Metric confidence is outside 0..1: " + pair.Key + "/" + metric.Label);
                    if (!Finite(metric.Range.Minimum) || !Finite(metric.Range.Maximum)) errors.Add("Metric range is not finite: " + pair.Key + "/" + metric.Label);
                    if (metric.Threshold.HasValue && !Finite(metric.Threshold.Value)) errors.Add("Metric threshold is not finite: " + pair.Key + "/" + metric.Label);
                    for (int sampleIndex = 0; sampleIndex < metric.History.Count; sampleIndex++)
                        if (!Finite(metric.History[sampleIndex].Value)) errors.Add("Metric history is not finite: " + pair.Key + "/" + metric.Label);
                }
            }
            foreach (KeyValuePair<string, List<InsightAction>> pair in actions)
            {
                if (!entities.ContainsKey(pair.Key)) errors.Add("Actions reference a missing entity: " + pair.Key);
            }
            foreach (InsightEvent insightEvent in events)
            {
                if (!eventIds.Add(insightEvent.Id)) errors.Add("Duplicate event id: " + insightEvent.Id);
                if (!Finite(insightEvent.Severity)) errors.Add("Event severity is not finite: " + insightEvent.Id);
                for (int i = 0; i < insightEvent.EntityIds.Count; i++)
                    if (!entities.ContainsKey(insightEvent.EntityIds[i])) warnings.Add("Event references a missing entity: " + insightEvent.EntityIds[i]);
            }
            return new InsightModelValidation(errors, warnings);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>Creates a stable, read-only data snapshot for rendering.</summary>
        public InsightModelSnapshot Snapshot()
        {
            Dictionary<string, IReadOnlyList<InsightMetric>> metricCopy = new Dictionary<string, IReadOnlyList<InsightMetric>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<InsightMetric>> pair in metrics)
                metricCopy[pair.Key] = new ReadOnlyCollection<InsightMetric>(new List<InsightMetric>(pair.Value));
            Dictionary<string, IReadOnlyList<InsightAction>> actionCopy = new Dictionary<string, IReadOnlyList<InsightAction>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<InsightAction>> pair in actions)
                actionCopy[pair.Key] = new ReadOnlyCollection<InsightAction>(new List<InsightAction>(pair.Value));
            Dictionary<string, InsightExplanation> explanationCopy = new Dictionary<string, InsightExplanation>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, InsightExplanation> pair in explanations)
                explanationCopy[pair.Key] = pair.Value.Clone();
            return new InsightModelSnapshot(Id, Revision,
                new ReadOnlyCollection<InsightEntity>(new List<InsightEntity>(entities.Values)),
                new ReadOnlyCollection<InsightRelation>(new List<InsightRelation>(relations)), metricCopy,
                actionCopy, new ReadOnlyDictionary<string, InsightExplanation>(explanationCopy),
                new ReadOnlyCollection<InsightEvent>(new List<InsightEvent>(events)));
        }

        /// <summary>Alias for Snapshot, useful at the boundary between collection and rendering.</summary>
        public InsightModelSnapshot Build() => Snapshot();
    }

    /// <summary>Immutable view of an InsightModel used by components during a frame.</summary>
    public sealed class InsightModelSnapshot
    {
        private readonly Dictionary<string, InsightEntity> byId;
        private readonly Dictionary<string, IReadOnlyList<InsightMetric>> metrics;
        private readonly Dictionary<string, IReadOnlyList<InsightAction>> actions;
        private readonly IReadOnlyDictionary<string, InsightExplanation> explanations;

        internal InsightModelSnapshot(string id, int revision, IReadOnlyList<InsightEntity> entities,
            IReadOnlyList<InsightRelation> relations, Dictionary<string, IReadOnlyList<InsightMetric>> metrics,
            Dictionary<string, IReadOnlyList<InsightAction>> actions, IReadOnlyDictionary<string, InsightExplanation> explanations,
            IReadOnlyList<InsightEvent> events)
        {
            Id = id;
            Revision = revision;
            Entities = entities;
            Relations = relations;
            this.metrics = metrics;
            this.actions = actions;
            this.explanations = explanations;
            Events = events;
            byId = new Dictionary<string, InsightEntity>(StringComparer.Ordinal);
            for (int i = 0; i < entities.Count; i++) byId[entities[i].Id] = entities[i];
        }

        public string Id { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<InsightEntity> Entities { get; private set; }
        public IReadOnlyList<InsightRelation> Relations { get; private set; }
        public IReadOnlyList<InsightEvent> Events { get; private set; }

        public InsightEntity Entity(string id)
        {
            InsightEntity entity;
            return id != null && byId.TryGetValue(id, out entity) ? entity : null;
        }

        public IReadOnlyList<InsightMetric> MetricsFor(string entityId)
        {
            IReadOnlyList<InsightMetric> result;
            return entityId != null && metrics.TryGetValue(entityId, out result) ? result : Empty<InsightMetric>();
        }

        public IReadOnlyList<InsightAction> ActionsFor(string entityId)
        {
            IReadOnlyList<InsightAction> result;
            return entityId != null && actions.TryGetValue(entityId, out result) ? result : Empty<InsightAction>();
        }

        public InsightExplanation ExplanationFor(string entityId)
        {
            InsightExplanation explanation;
            return entityId != null && explanations.TryGetValue(entityId, out explanation) ? explanation : null;
        }

        private static IReadOnlyList<T> Empty<T>() => EmptyValues<T>.Value;

        private static class EmptyValues<T>
        {
            public static readonly IReadOnlyList<T> Value = new T[0];
        }
    }

    /// <summary>Stable FNV-1a ids for integrations that do not already own a persistent id.</summary>
    public static class InsightIds
    {
        public static string Stable(string namespaceId, string naturalKey)
        {
            string value = (namespaceId ?? string.Empty) + "|" + (naturalKey ?? string.Empty);
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (namespaceId ?? "insight") + ":" + hash.ToString("x8");
            }
        }
    }
}
