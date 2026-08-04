using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Xml;

namespace InsightCanvas
{
    /// <summary>Diagnostics produced while reading or writing an Insight Canvas model document.</summary>
    public sealed class InsightModelSerializationReport
    {
        private readonly ReadOnlyCollection<string> warnings;
        private readonly ReadOnlyCollection<string> errors;

        internal InsightModelSerializationReport(string xml, InsightModel model, List<string> warnings, List<string> errors)
        {
            Xml = xml ?? string.Empty;
            Model = model;
            this.warnings = new ReadOnlyCollection<string>(warnings ?? new List<string>());
            this.errors = new ReadOnlyCollection<string>(errors ?? new List<string>());
        }

        /// <summary>Serialized XML for a write report, or an empty string for a read report.</summary>
        public string Xml { get; private set; }
        /// <summary>Restored model for a read report, or null for a write report.</summary>
        public InsightModel Model { get; private set; }
        /// <summary>Non-fatal omissions or compatibility notices.</summary>
        public IReadOnlyList<string> Warnings => warnings;
        /// <summary>Failures that prevented a complete read or write.</summary>
        public IReadOnlyList<string> Errors => errors;
        /// <summary>True when no fatal serialization error was recorded.</summary>
        public bool Succeeded => errors.Count == 0;
    }

    /// <summary>
    /// Deterministic XML interchange for pure model and display data. Runtime sources, icons, delegates,
    /// textures, callbacks, map references, and other game objects are intentionally not reconstructed.
    /// Schema version 2 adds badges, manual positions, explanations, action metadata, and source/icon ids.
    /// </summary>
    public static class InsightModelSerialization
    {
        public const int CurrentSchemaVersion = 2;

        /// <summary>Backward-compatible write API. Use SerializeWithDiagnostics to inspect omissions.</summary>
        public static string Serialize(InsightModelSnapshot snapshot)
        {
            return SerializeWithDiagnostics(snapshot).Xml;
        }

        /// <summary>Serializes pure model data and reports runtime-only omissions.</summary>
        public static InsightModelSerializationReport SerializeWithDiagnostics(InsightModelSnapshot snapshot)
        {
            List<string> warnings = new List<string>();
            List<string> errors = new List<string>();
            if (snapshot == null)
            {
                errors.Add("model: snapshot is null");
                return new InsightModelSerializationReport(string.Empty, null, warnings, errors);
            }

            CollectRuntimeWarnings(snapshot, warnings);
            try
            {
                StringBuilder builder = new StringBuilder();
                XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
                using (XmlWriter writer = XmlWriter.Create(builder, settings))
                {
                    writer.WriteStartElement("insightModel");
                    writer.WriteAttributeString("schemaVersion", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture));
                    WriteAttribute(writer, "id", snapshot.Id);
                    WriteAttribute(writer, "revision", snapshot.Revision.ToString(CultureInfo.InvariantCulture));
                    WriteEntities(writer, snapshot);
                    WriteManualPositions(writer, snapshot);
                    WriteRelations(writer, snapshot);
                    WriteMetrics(writer, snapshot);
                    WriteExplanations(writer, snapshot);
                    WriteActions(writer, snapshot);
                    WriteEvents(writer, snapshot);
                    writer.WriteEndElement();
                }
                return new InsightModelSerializationReport(builder.ToString(), null, warnings, errors);
            }
            catch (Exception exception)
            {
                errors.Add("model: serialization failed: " + exception.Message);
                return new InsightModelSerializationReport(string.Empty, null, warnings, errors);
            }
        }

        /// <summary>Backward-compatible read API. Runtime-only fields are restored in a disabled state.</summary>
        public static InsightModel Deserialize(string xml)
        {
            return DeserializeWithDiagnostics(xml).Model;
        }

        /// <summary>Reads schema 2 or the prior unversioned format without reconstructing runtime objects.</summary>
        public static InsightModelSerializationReport DeserializeWithDiagnostics(string xml)
        {
            List<string> warnings = new List<string>();
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(xml))
            {
                warnings.Add("model: empty document restored as an empty model");
                return new InsightModelSerializationReport(string.Empty, InsightModel.Create("Empty"), warnings, errors);
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xml);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "insightModel")
                {
                    errors.Add("model: root element must be insightModel");
                    return new InsightModelSerializationReport(string.Empty, InsightModel.Create("Empty"), warnings, errors);
                }

                int schemaVersion = ReadSchemaVersion(root, warnings);
                if (schemaVersion > CurrentSchemaVersion)
                    warnings.Add("model: newer schema version " + schemaVersion + " read using supported fields");
                InsightModel model = InsightModel.Create(Attribute(root, "id"));
                Dictionary<string, InsightPoint> manualPositions = ReadManualPositions(root, model, warnings);
                ReadEntities(root, model, manualPositions, warnings);
                ReadRelations(root, model);
                ReadMetrics(root, model);
                ReadExplanations(root, model, warnings);
                ReadActions(root, model, warnings);
                ReadEvents(root, model);
                return new InsightModelSerializationReport(string.Empty, model, warnings, errors);
            }
            catch (Exception exception)
            {
                errors.Add("model: deserialization failed: " + exception.Message);
                return new InsightModelSerializationReport(string.Empty, InsightModel.Create("Empty"), warnings, errors);
            }
        }

        private static void CollectRuntimeWarnings(InsightModelSnapshot snapshot, List<string> warnings)
        {
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                InsightEntity entity = snapshot.Entities[i];
                if (entity.Source != null)
                    warnings.Add("entities id '" + entity.Id + "': runtime Source omitted; SourceId is preserved");
                if (entity.Icon != null)
                    warnings.Add("entities id '" + entity.Id + "': runtime Icon/texture omitted; IconId is preserved");
            }
            foreach (string owner in SortedKeys(snapshot.Actions))
            {
                IReadOnlyList<InsightAction> actions = snapshot.Actions[owner];
                for (int i = 0; i < actions.Count; i++)
                    if (actions[i].Callback != null)
                        warnings.Add("actions id '" + actions[i].Id + "': callback omitted and restored disabled");
            }
        }

        private static void WriteEntities(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("entities");
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                InsightEntity entity = snapshot.Entities[i];
                writer.WriteStartElement("entity");
                WriteAttribute(writer, "id", entity.Id);
                WriteAttribute(writer, "label", entity.Label);
                WriteAttribute(writer, "subtitle", entity.Subtitle);
                WriteAttribute(writer, "category", entity.Category);
                WriteAttribute(writer, "sourceId", entity.SourceId);
                WriteAttribute(writer, "iconId", entity.IconId);
                WriteAttribute(writer, "sourceRuntime", entity.Source == null ? "none" : "omitted");
                WriteAttribute(writer, "iconRuntime", entity.Icon == null ? "none" : "omitted");
                writer.WriteStartElement("badges");
                for (int badgeIndex = 0; badgeIndex < entity.Badges.Count; badgeIndex++)
                {
                    writer.WriteStartElement("badge");
                    WriteAttribute(writer, "value", entity.Badges[badgeIndex]);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteManualPositions(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("manualPositions");
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                InsightEntity entity = snapshot.Entities[i];
                if (!entity.ManualPosition.HasValue) continue;
                InsightPoint point = entity.ManualPosition.Value;
                writer.WriteStartElement("position");
                WriteAttribute(writer, "entity", entity.Id);
                WriteAttribute(writer, "x", Float(point.X));
                WriteAttribute(writer, "y", Float(point.Y));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteRelations(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("relations");
            for (int i = 0; i < snapshot.Relations.Count; i++)
            {
                InsightRelation relation = snapshot.Relations[i];
                writer.WriteStartElement("relation");
                WriteAttribute(writer, "from", relation.FromId);
                WriteAttribute(writer, "to", relation.ToId);
                WriteAttribute(writer, "type", relation.Type);
                WriteAttribute(writer, "weight", Float(relation.Weight));
                WriteAttribute(writer, "directed", Bool(relation.Directed));
                WriteAttribute(writer, "confidence", Float(relation.Confidence));
                WriteAttribute(writer, "known", Bool(relation.Known));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteMetrics(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("metrics");
            foreach (string owner in SortedKeys(snapshot.Metrics))
            {
                IReadOnlyList<InsightMetric> values = snapshot.Metrics[owner];
                for (int metricIndex = 0; metricIndex < values.Count; metricIndex++)
                {
                    InsightMetric metric = values[metricIndex];
                    writer.WriteStartElement("metric");
                    WriteAttribute(writer, "entity", owner);
                    WriteAttribute(writer, "label", metric.Label);
                    WriteAttribute(writer, "value", Float(metric.Value));
                    WriteAttribute(writer, "minimum", Float(metric.Range.Minimum));
                    WriteAttribute(writer, "maximum", Float(metric.Range.Maximum));
                    WriteAttribute(writer, "hasRange", Bool(metric.HasRange));
                    WriteAttribute(writer, "known", Bool(metric.Known));
                    WriteAttribute(writer, "confidence", Float(metric.Confidence));
                    WriteAttribute(writer, "threshold", metric.Threshold.HasValue ? Float(metric.Threshold.Value) : string.Empty);
                    WriteAttribute(writer, "trend", metric.Trend.ToString());
                    for (int sampleIndex = 0; sampleIndex < metric.History.Count; sampleIndex++)
                    {
                        InsightSample sample = metric.History[sampleIndex];
                        writer.WriteStartElement("sample");
                        WriteAttribute(writer, "tick", sample.Tick.ToString(CultureInfo.InvariantCulture));
                        WriteAttribute(writer, "value", Float(sample.Value));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static void WriteExplanations(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("explanations");
            foreach (string owner in SortedKeys(snapshot.Explanations))
            {
                InsightExplanation explanation = snapshot.Explanations[owner];
                if (explanation == null) continue;
                writer.WriteStartElement("explanation");
                WriteAttribute(writer, "owner", owner);
                WriteAttribute(writer, "label", explanation.Label);
                WriteAttribute(writer, "final", Float(explanation.DeclaredFinalValue));
                WriteAttribute(writer, "hasBase", Bool(explanation.HasBase));
                if (explanation.HasBase) WriteAttribute(writer, "base", Float(explanation.BaseValue));
                IReadOnlyList<InsightExplanationOperationData> operations = explanation.SerializationOperations();
                for (int i = 0; i < operations.Count; i++)
                {
                    InsightExplanationOperationData operation = operations[i];
                    writer.WriteStartElement("operation");
                    WriteAttribute(writer, "kind", operation.Kind.ToString());
                    WriteAttribute(writer, "label", operation.Label);
                    WriteAttribute(writer, "amount", Float(operation.Amount));
                    WriteAttribute(writer, "confidence", Float(operation.Confidence));
                    WriteAttribute(writer, "known", Bool(operation.Known));
                    WriteAttribute(writer, "requirementMet", Bool(operation.RequirementMet));
                    WriteAttribute(writer, "hasRange", Bool(operation.HasRange));
                    if (operation.HasRange)
                    {
                        WriteAttribute(writer, "minimum", Float(operation.Range.Minimum));
                        WriteAttribute(writer, "maximum", Float(operation.Range.Maximum));
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteActions(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("actions");
            foreach (string owner in SortedKeys(snapshot.Actions))
            {
                IReadOnlyList<InsightAction> actions = snapshot.Actions[owner];
                for (int i = 0; i < actions.Count; i++)
                {
                    InsightAction action = actions[i];
                    writer.WriteStartElement("action");
                    WriteAttribute(writer, "entity", owner);
                    WriteAttribute(writer, "id", action.Id);
                    WriteAttribute(writer, "label", action.Label);
                    WriteAttribute(writer, "tooltip", action.Tooltip);
                    WriteAttribute(writer, "enabled", Bool(action.Enabled));
                    WriteAttribute(writer, "closeWindowAfterInvoke", Bool(action.CloseWindowAfterInvoke));
                    WriteAttribute(writer, "callback", action.Callback == null ? "none" : "omitted");
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }

        private static void WriteEvents(XmlWriter writer, InsightModelSnapshot snapshot)
        {
            writer.WriteStartElement("events");
            for (int i = 0; i < snapshot.Events.Count; i++)
            {
                InsightEvent insightEvent = snapshot.Events[i];
                writer.WriteStartElement("event");
                WriteAttribute(writer, "id", insightEvent.Id);
                WriteAttribute(writer, "tick", insightEvent.Tick.ToString(CultureInfo.InvariantCulture));
                WriteAttribute(writer, "label", insightEvent.Label);
                WriteAttribute(writer, "category", insightEvent.Category);
                WriteAttribute(writer, "severity", Float(insightEvent.Severity));
                WriteAttribute(writer, "known", Bool(insightEvent.Known));
                WriteAttribute(writer, "mapLink", insightEvent.MapLinkId);
                for (int j = 0; j < insightEvent.EntityIds.Count; j++)
                {
                    writer.WriteStartElement("entity");
                    WriteAttribute(writer, "id", insightEvent.EntityIds[j]);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static int ReadSchemaVersion(XmlElement root, List<string> warnings)
        {
            string value = Attribute(root, "schemaVersion");
            if (string.IsNullOrEmpty(value))
            {
                warnings.Add("model: unversioned document read as legacy schema version 1");
                return 1;
            }
            int version;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || version <= 0)
            {
                warnings.Add("model: invalid schemaVersion; read as legacy schema version 1");
                return 1;
            }
            return version;
        }

        private static Dictionary<string, InsightPoint> ReadManualPositions(XmlElement root, InsightModel model, List<string> warnings)
        {
            Dictionary<string, InsightPoint> result = new Dictionary<string, InsightPoint>(StringComparer.Ordinal);
            XmlNodeList positions = root.SelectNodes("./manualPositions/position");
            if (positions == null) return result;
            for (int i = 0; i < positions.Count; i++)
            {
                XmlNode node = positions[i];
                string entityId = Attribute(node, "entity");
                float x;
                float y;
                bool validX = TryFloat(node, "x", out x);
                bool validY = TryFloat(node, "y", out y);
                if (string.IsNullOrWhiteSpace(entityId))
                {
                    model.AddAuthoringError("manualPositions id '<empty>': entity reference must not be empty");
                    continue;
                }
                if (!validX || !validY)
                    model.AddAuthoringError("manualPositions id '" + entityId + "': coordinates must be numeric");
                if (!Finite(x) || !Finite(y))
                    model.AddAuthoringError("manualPositions id '" + entityId + "': coordinates must be finite");
                if (result.ContainsKey(entityId))
                    model.AddAuthoringError("manualPositions id '" + entityId + "': duplicate id");
                else result[entityId] = new InsightPoint(x, y);
            }
            return result;
        }

        private static void ReadEntities(XmlElement root, InsightModel model, Dictionary<string, InsightPoint> manualPositions,
            List<string> warnings)
        {
            XmlNodeList entities = root.SelectNodes("./entities/entity");
            if (entities == null) return;
            for (int i = 0; i < entities.Count; i++)
            {
                XmlNode node = entities[i];
                string id = Attribute(node, "id");
                InsightPoint point;
                InsightPoint? manualPosition = manualPositions.TryGetValue(id, out point) ? point : (InsightPoint?)null;
                List<string> badges = new List<string>();
                XmlNodeList badgeNodes = node.SelectNodes("./badges/badge");
                if (badgeNodes != null)
                    for (int badgeIndex = 0; badgeIndex < badgeNodes.Count; badgeIndex++)
                        badges.Add(Attribute(badgeNodes[badgeIndex], "value"));
                try
                {
                    InsightEntity entity = new InsightEntity(id, Attribute(node, "label"), Attribute(node, "subtitle"),
                        Attribute(node, "category"), null, null, badges, manualPosition,
                        Attribute(node, "sourceId"), Attribute(node, "iconId"));
                    model.Entity(entity);
                    if (Attribute(node, "sourceRuntime") == "omitted")
                        warnings.Add("entities id '" + id + "': runtime Source was omitted and remains null");
                    if (Attribute(node, "iconRuntime") == "omitted")
                        warnings.Add("entities id '" + id + "': runtime Icon/texture was omitted and remains null");
                }
                catch (Exception exception)
                {
                    model.AddAuthoringError("entities id '" + id + "': could not be restored: " + exception.Message);
                }
            }
            HashSet<string> entityIds = new HashSet<string>(StringComparer.Ordinal);
            InsightModelSnapshot snapshot = model.Snapshot();
            for (int i = 0; i < snapshot.Entities.Count; i++) entityIds.Add(snapshot.Entities[i].Id);
            foreach (KeyValuePair<string, InsightPoint> pair in manualPositions)
                if (!entityIds.Contains(pair.Key))
                    model.AddAuthoringError("manualPositions id '" + pair.Key + "': entity reference is missing");
        }

        private static void ReadRelations(XmlElement root, InsightModel model)
        {
            XmlNodeList relations = root.SelectNodes("./relations/relation");
            if (relations == null) return;
            for (int i = 0; i < relations.Count; i++)
            {
                XmlNode node = relations[i];
                model.Relation(Attribute(node, "from"), Attribute(node, "to"), Attribute(node, "type"),
                    Float(node, "weight", 1f), Bool(node, "directed", true), Float(node, "confidence", 1f), Bool(node, "known", true));
            }
        }

        private static void ReadMetrics(XmlElement root, InsightModel model)
        {
            XmlNodeList metrics = root.SelectNodes("./metrics/metric");
            if (metrics == null) return;
            for (int i = 0; i < metrics.Count; i++)
            {
                XmlNode node = metrics[i];
                List<InsightSample> history = new List<InsightSample>();
                XmlNodeList samples = node.SelectNodes("./sample");
                if (samples != null)
                    for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                        history.Add(new InsightSample(Long(samples[sampleIndex], "tick", 0L), Float(samples[sampleIndex], "value", 0f)));
                InsightTrend trend;
                if (!Enum.TryParse(Attribute(node, "trend"), out trend)) trend = InsightTrend.Flat;
                string label = Attribute(node, "label");
                model.Metric(Attribute(node, "entity"), label, new InsightMetric(label,
                    Float(node, "value", 0f), new InsightRange(Float(node, "minimum", 0f), Float(node, "maximum", 0f)),
                    Bool(node, "hasRange", true), Bool(node, "known", true), Float(node, "confidence", 1f),
                    NullableFloat(node, "threshold"), trend, history));
            }
        }

        private static void ReadExplanations(XmlElement root, InsightModel model, List<string> warnings)
        {
            XmlNodeList explanations = root.SelectNodes("./explanations/explanation");
            if (explanations == null) return;
            HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < explanations.Count; i++)
            {
                XmlNode node = explanations[i];
                string owner = Attribute(node, "owner");
                if (!owners.Add(owner)) model.AddAuthoringError("explanations id '" + owner + "': duplicate owner id");
                InsightExplanation explanation = Explain.Value(Attribute(node, "label"), Float(node, "final", float.NaN));
                if (Bool(node, "hasBase", false)) explanation.Base(Float(node, "base", 0f));
                XmlNodeList operations = node.SelectNodes("./operation");
                if (operations != null)
                    for (int operationIndex = 0; operationIndex < operations.Count; operationIndex++)
                        ReadExplanationOperation(operations[operationIndex], explanation, warnings);
                model.Explanation(owner, explanation);
            }
        }

        private static void ReadExplanationOperation(XmlNode node, InsightExplanation explanation, List<string> warnings)
        {
            InsightExplanationSegmentKind kind;
            if (!Enum.TryParse(Attribute(node, "kind"), out kind))
            {
                warnings.Add("explanations: unknown operation kind omitted");
                return;
            }
            string label = Attribute(node, "label");
            switch (kind)
            {
                case InsightExplanationSegmentKind.Factor:
                    explanation.Factor(label, Float(node, "amount", 1f), Float(node, "confidence", 1f), Bool(node, "known", true));
                    break;
                case InsightExplanationSegmentKind.Additive:
                    explanation.Add(label, Float(node, "amount", 0f), Float(node, "confidence", 1f), Bool(node, "known", true));
                    break;
                case InsightExplanationSegmentKind.Clamp:
                    explanation.Clamp(label, Float(node, "minimum", 0f), Float(node, "maximum", 0f));
                    break;
                case InsightExplanationSegmentKind.Requirement:
                    explanation.Requirement(label, Bool(node, "requirementMet", true));
                    break;
                case InsightExplanationSegmentKind.Uncertainty:
                    explanation.Uncertain(Float(node, "minimum", 0f), Float(node, "maximum", 0f),
                        Float(node, "confidence", 1f), label);
                    break;
                default:
                    warnings.Add("explanations: non-operation segment kind '" + kind + "' omitted");
                    break;
            }
        }

        private static void ReadActions(XmlElement root, InsightModel model, List<string> warnings)
        {
            XmlNodeList actions = root.SelectNodes("./actions/action");
            if (actions == null) return;
            for (int i = 0; i < actions.Count; i++)
            {
                XmlNode node = actions[i];
                string id = Attribute(node, "id");
                string runtime = Attribute(node, "callback");
                if (runtime != "none" || Bool(node, "enabled", false))
                    warnings.Add("actions id '" + id + "': runtime callback is not restored; action disabled");
                model.Action(Attribute(node, "entity"), new InsightAction(id, Attribute(node, "label"), null, false,
                    Attribute(node, "tooltip"), Bool(node, "closeWindowAfterInvoke", false)));
            }
        }

        private static void ReadEvents(XmlElement root, InsightModel model)
        {
            XmlNodeList events = root.SelectNodes("./events/event");
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                XmlNode node = events[i];
                List<string> ids = new List<string>();
                XmlNodeList childEntities = node.SelectNodes("./entity");
                if (childEntities != null)
                    for (int entityIndex = 0; entityIndex < childEntities.Count; entityIndex++)
                        ids.Add(Attribute(childEntities[entityIndex], "id"));
                model.Event(new InsightEvent(Attribute(node, "id"), Long(node, "tick", 0L), Attribute(node, "label"),
                    Attribute(node, "category"), ids, Float(node, "severity", 0f), Bool(node, "known", true), Attribute(node, "mapLink")));
            }
        }

        private static List<string> SortedKeys<T>(IReadOnlyDictionary<string, T> values)
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, T> pair in values) keys.Add(pair.Key);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static void WriteAttribute(XmlWriter writer, string name, string value)
        {
            writer.WriteAttributeString(name, value ?? string.Empty);
        }

        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Bool(bool value) => value ? "true" : "false";

        private static string Attribute(XmlNode node, string name) => node.Attributes?[name]?.Value ?? string.Empty;

        private static float Float(XmlNode node, string name, float fallback)
        {
            float value;
            return TryFloat(node, name, out value) ? value : fallback;
        }

        private static bool TryFloat(XmlNode node, string name, out float value)
        {
            return float.TryParse(Attribute(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static long Long(XmlNode node, string name, long fallback)
        {
            long value;
            return long.TryParse(Attribute(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool Bool(XmlNode node, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(Attribute(node, name), out value) ? value : fallback;
        }

        private static float? NullableFloat(XmlNode node, string name)
        {
            string value = Attribute(node, name);
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : (float?)null;
        }
    }
}
