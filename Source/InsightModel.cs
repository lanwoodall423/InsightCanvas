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
        private readonly bool hasBounds;

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
            hasBounds = true;
        }

        public bool IsEmpty => !hasBounds;
        public static InsightTimeRange Empty => default(InsightTimeRange);
        public bool Contains(long tick) => !IsEmpty && tick >= Start && tick <= End;
        public bool Equals(InsightTimeRange other)
        {
            return IsEmpty ? other.IsEmpty : !other.IsEmpty && Start == other.Start && End == other.End;
        }

        public override bool Equals(object obj) => obj is InsightTimeRange && Equals((InsightTimeRange)obj);
        public override int GetHashCode() => IsEmpty ? 17 : (Start.GetHashCode() * 397) ^ End.GetHashCode();
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
        /// <summary>Optional pure-data source identifier. Source itself is runtime-only.</summary>
        public string SourceId { get; private set; }
        /// <summary>Optional pure-data icon identifier or path. Icon itself is runtime-only.</summary>
        public string IconId { get; private set; }
        public InsightPoint? ManualPosition { get; private set; }
        public IReadOnlyList<string> Badges => badges;

        public InsightEntity(string id, string label, string subtitle = null, string category = null,
            object source = null, object icon = null, IEnumerable<string> badges = null,
            InsightPoint? manualPosition = null, string sourceId = null, string iconId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Entity ids must not be empty.", nameof(id));
            Id = id;
            Label = label ?? id;
            Subtitle = subtitle ?? string.Empty;
            Category = category ?? string.Empty;
            Source = source;
            Icon = icon;
            SourceId = sourceId ?? string.Empty;
            IconId = iconId ?? string.Empty;
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
                AddAuthoringError("entities id '" + entity.Id + "': duplicate id");
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
            foreach (KeyValuePair<string, InsightEntity> pair in entities)
            {
                InsightEntity entity = pair.Value;
                if (string.IsNullOrWhiteSpace(entity.Id)) AddError(errors, "entities", entity.Id, "id must not be empty");
                if (!entities.ContainsKey(entity.Id)) AddError(errors, "entities", entity.Id, "entity is not indexed by its own id");
                if (string.IsNullOrWhiteSpace(entity.Label)) AddWarning(warnings, "entities", entity.Id, "label is empty");
                if (!string.IsNullOrEmpty(entity.SourceId) && string.IsNullOrWhiteSpace(entity.SourceId))
                    AddWarning(warnings, "entities", entity.Id, "source identifier is whitespace");
                if (!string.IsNullOrEmpty(entity.IconId) && string.IsNullOrWhiteSpace(entity.IconId))
                    AddWarning(warnings, "entities", entity.Id, "icon identifier is whitespace");
                if (entity.ManualPosition.HasValue &&
                    (!Finite(entity.ManualPosition.Value.X) || !Finite(entity.ManualPosition.Value.Y)))
                    AddError(errors, "manualPositions", entity.Id, "position coordinates must be finite");
                for (int badgeIndex = 0; badgeIndex < entity.Badges.Count; badgeIndex++)
                    if (string.IsNullOrWhiteSpace(entity.Badges[badgeIndex]))
                        AddWarning(warnings, "badges", entity.Id, "badge text is empty");
            }

            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < relations.Count; i++)
            {
                InsightRelation relation = relations[i];
                string relationId = (relation.FromId ?? string.Empty) + "->" + (relation.ToId ?? string.Empty);
                if (!entities.ContainsKey(relation.FromId)) AddError(errors, "relations", relationId, "source entity reference is missing: " + relation.FromId);
                if (!entities.ContainsKey(relation.ToId)) AddError(errors, "relations", relationId, "target entity reference is missing: " + relation.ToId);
                if (string.IsNullOrWhiteSpace(relation.Type)) AddWarning(warnings, "relations", relationId, "type is empty");
                if (!Finite(relation.Weight) || !Finite(relation.Confidence)) AddError(errors, "relations", relationId, "weight and confidence must be finite");
            }
            foreach (KeyValuePair<string, List<InsightMetric>> pair in metrics)
            {
                if (!entities.ContainsKey(pair.Key)) AddError(errors, "metrics", pair.Key, "owner entity reference is missing");
                HashSet<string> metricIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    InsightMetric metric = pair.Value[i];
                    string metricId = pair.Key + "/" + (metric.Label ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(metric.Label)) AddWarning(warnings, "metrics", metricId, "label is empty");
                    else if (!metricIds.Add(metric.Label)) AddError(errors, "metrics", metricId, "duplicate metric label for owner");
                    if (!Finite(metric.Value)) AddError(errors, "metrics", metricId, "value must be finite");
                    if (!Finite(metric.Confidence) || metric.Confidence < 0f || metric.Confidence > 1f) AddError(errors, "metrics", metricId, "confidence must be in 0..1");
                    if (!Finite(metric.Range.Minimum) || !Finite(metric.Range.Maximum)) AddError(errors, "metrics", metricId, "range must be finite");
                    if (metric.Threshold.HasValue && !Finite(metric.Threshold.Value)) AddError(errors, "metrics", metricId, "threshold must be finite");
                    for (int sampleIndex = 0; sampleIndex < metric.History.Count; sampleIndex++)
                        if (metric.History[sampleIndex] == null || !Finite(metric.History[sampleIndex].Value))
                            AddError(errors, "metrics", metricId, "history sample value must be finite");
                }
            }
            foreach (KeyValuePair<string, List<InsightAction>> pair in actions)
            {
                if (!entities.ContainsKey(pair.Key)) AddError(errors, "actions", pair.Key, "owner entity reference is missing");
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    InsightAction action = pair.Value[i];
                    string actionId = action.Id ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(actionId)) AddError(errors, "actions", actionId, "id must not be empty");
                    else if (!actionIds.Add(actionId)) AddError(errors, "actions", actionId, "duplicate id");
                    if (string.IsNullOrWhiteSpace(action.Label)) AddWarning(warnings, "actions", actionId, "label is empty");
                    if (action.Enabled && action.Callback == null) AddWarning(warnings, "actions", actionId, "enabled action has no callback");
                }
            }
            foreach (KeyValuePair<string, InsightExplanation> pair in explanations)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) AddError(errors, "explanations", pair.Key, "owner id must not be empty");
                else if (!entities.ContainsKey(pair.Key)) AddError(errors, "explanations", pair.Key, "owner must reference an existing entity");
                InsightExplanation explanation = pair.Value;
                if (explanation == null)
                {
                    AddError(errors, "explanations", pair.Key, "explanation is null");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(explanation.Label)) AddWarning(warnings, "explanations", pair.Key, "label is empty");
                InsightExplanationResult result = explanation.Calculate();
                if (!Finite(result.ComputedValue)) AddError(errors, "explanations", pair.Key, "computed value must be finite");
                if (!float.IsNaN(result.DeclaredFinalValue) && !Finite(result.DeclaredFinalValue))
                    AddError(errors, "explanations", pair.Key, "declared final value must be finite or NaN");
                for (int segmentIndex = 0; segmentIndex < result.Segments.Count; segmentIndex++)
                {
                    InsightExplanationSegment segment = result.Segments[segmentIndex];
                    if (string.IsNullOrWhiteSpace(segment.Label)) AddWarning(warnings, "explanations", pair.Key, "segment label is empty");
                    if (!Finite(segment.Before) || !Finite(segment.After) || !Finite(segment.Amount) || !Finite(segment.Confidence))
                        AddError(errors, "explanations", pair.Key, "segment contains a non-finite value");
                    if (segment.HasRange && (!Finite(segment.Range.Minimum) || !Finite(segment.Range.Maximum)))
                        AddError(errors, "explanations", pair.Key, "segment range must be finite");
                }
            }
            foreach (InsightEvent insightEvent in events)
            {
                string eventId = insightEvent.Id ?? string.Empty;
                if (string.IsNullOrWhiteSpace(eventId)) AddError(errors, "events", eventId, "id must not be empty");
                else if (!eventIds.Add(eventId)) AddError(errors, "events", eventId, "duplicate id");
                if (string.IsNullOrWhiteSpace(insightEvent.Label)) AddWarning(warnings, "events", eventId, "label is empty");
                if (!Finite(insightEvent.Severity)) AddError(errors, "events", eventId, "severity must be finite");
                for (int i = 0; i < insightEvent.EntityIds.Count; i++)
                    if (!entities.ContainsKey(insightEvent.EntityIds[i]))
                        AddWarning(warnings, "events", eventId, "entity reference is missing: " + insightEvent.EntityIds[i]);
            }
            return new InsightModelValidation(errors, warnings);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void AddError(List<string> messages, string collection, string id, string reason)
        {
            messages.Add(collection + " id '" + (string.IsNullOrEmpty(id) ? "<empty>" : id) + "': " + reason);
        }

        private static void AddWarning(List<string> messages, string collection, string id, string reason)
        {
            messages.Add(collection + " id '" + (string.IsNullOrEmpty(id) ? "<empty>" : id) + "': " + reason);
        }

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

        internal void AddAuthoringError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) authoringErrors.Add(message);
        }
    }

    /// <summary>Immutable view of an InsightModel used by components during a frame.</summary>
    public sealed class InsightModelSnapshot
    {
        private readonly Dictionary<string, InsightEntity> byId;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<InsightMetric>> metrics;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<InsightAction>> actions;
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
            this.metrics = new ReadOnlyDictionary<string, IReadOnlyList<InsightMetric>>(
                metrics ?? new Dictionary<string, IReadOnlyList<InsightMetric>>(StringComparer.Ordinal));
            this.actions = new ReadOnlyDictionary<string, IReadOnlyList<InsightAction>>(
                actions ?? new Dictionary<string, IReadOnlyList<InsightAction>>(StringComparer.Ordinal));
            this.explanations = explanations ?? new ReadOnlyDictionary<string, InsightExplanation>(
                new Dictionary<string, InsightExplanation>(StringComparer.Ordinal));
            Events = events;
            byId = new Dictionary<string, InsightEntity>(StringComparer.Ordinal);
            for (int i = 0; i < entities.Count; i++) byId[entities[i].Id] = entities[i];
        }

        public string Id { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<InsightEntity> Entities { get; private set; }
        public IReadOnlyList<InsightRelation> Relations { get; private set; }
        public IReadOnlyList<InsightEvent> Events { get; private set; }
        /// <summary>All metric lists keyed by their owning entity id.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<InsightMetric>> Metrics => metrics;
        /// <summary>All action lists keyed by their owning entity id. Callbacks remain runtime-only.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<InsightAction>> Actions => actions;
        /// <summary>All explanations keyed by their owning entity id.</summary>
        public IReadOnlyDictionary<string, InsightExplanation> Explanations => explanations;

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
