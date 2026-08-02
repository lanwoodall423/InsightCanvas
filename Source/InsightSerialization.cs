using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace InsightCanvas
{
    /// <summary>
    /// Optional interchange serialization for immutable snapshots. Insight Canvas does not automatically persist
    /// temporary view state or source objects into RimWorld saves.
    /// </summary>
    public static class InsightModelSerialization
    {
        public static string Serialize(InsightModelSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            StringBuilder builder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                writer.WriteStartElement("insightModel");
                writer.WriteAttributeString("id", snapshot.Id ?? string.Empty);
                writer.WriteAttributeString("revision", snapshot.Revision.ToString(CultureInfo.InvariantCulture));
                writer.WriteStartElement("entities");
                for (int i = 0; i < snapshot.Entities.Count; i++)
                {
                    InsightEntity entity = snapshot.Entities[i];
                    writer.WriteStartElement("entity");
                    writer.WriteAttributeString("id", entity.Id);
                    writer.WriteAttributeString("label", entity.Label);
                    writer.WriteAttributeString("subtitle", entity.Subtitle);
                    writer.WriteAttributeString("category", entity.Category);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("relations");
                for (int i = 0; i < snapshot.Relations.Count; i++)
                {
                    InsightRelation relation = snapshot.Relations[i];
                    writer.WriteStartElement("relation");
                    writer.WriteAttributeString("from", relation.FromId);
                    writer.WriteAttributeString("to", relation.ToId);
                    writer.WriteAttributeString("type", relation.Type);
                    writer.WriteAttributeString("weight", relation.Weight.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("directed", relation.Directed.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("confidence", relation.Confidence.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("known", relation.Known.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("metrics");
                for (int entityIndex = 0; entityIndex < snapshot.Entities.Count; entityIndex++)
                {
                    InsightEntity entity = snapshot.Entities[entityIndex];
                    IReadOnlyList<InsightMetric> values = snapshot.MetricsFor(entity.Id);
                    for (int metricIndex = 0; metricIndex < values.Count; metricIndex++)
                    {
                        InsightMetric metric = values[metricIndex];
                        writer.WriteStartElement("metric");
                        writer.WriteAttributeString("entity", entity.Id);
                        writer.WriteAttributeString("label", metric.Label);
                        writer.WriteAttributeString("value", metric.Value.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("minimum", metric.Range.Minimum.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("maximum", metric.Range.Maximum.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("hasRange", metric.HasRange.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("known", metric.Known.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("confidence", metric.Confidence.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("threshold", metric.Threshold.HasValue ? metric.Threshold.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty);
                        writer.WriteAttributeString("trend", metric.Trend.ToString());
                        for (int sampleIndex = 0; sampleIndex < metric.History.Count; sampleIndex++)
                        {
                            InsightSample sample = metric.History[sampleIndex];
                            writer.WriteStartElement("sample");
                            writer.WriteAttributeString("tick", sample.Tick.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("value", sample.Value.ToString("R", CultureInfo.InvariantCulture));
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement();
                writer.WriteStartElement("events");
                for (int i = 0; i < snapshot.Events.Count; i++)
                {
                    InsightEvent insightEvent = snapshot.Events[i];
                    writer.WriteStartElement("event");
                    writer.WriteAttributeString("id", insightEvent.Id);
                    writer.WriteAttributeString("tick", insightEvent.Tick.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("label", insightEvent.Label);
                    writer.WriteAttributeString("category", insightEvent.Category);
                    writer.WriteAttributeString("severity", insightEvent.Severity.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("known", insightEvent.Known.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("mapLink", insightEvent.MapLinkId ?? string.Empty);
                    for (int j = 0; j < insightEvent.EntityIds.Count; j++)
                    {
                        writer.WriteStartElement("entity");
                        writer.WriteAttributeString("id", insightEvent.EntityIds[j]);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            return builder.ToString();
        }

        public static InsightModel Deserialize(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return InsightModel.Create("Empty");
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            XmlElement root = document.DocumentElement;
            InsightModel model = InsightModel.Create(root?.GetAttribute("id"));
            XmlNodeList entities = root?.SelectNodes("./entities/entity");
            if (entities != null)
                foreach (XmlNode node in entities)
                    model.Entity(new InsightEntity(node.Attributes?["id"]?.Value ?? string.Empty,
                        node.Attributes?["label"]?.Value, node.Attributes?["subtitle"]?.Value,
                        node.Attributes?["category"]?.Value));
            XmlNodeList relations = root?.SelectNodes("./relations/relation");
            if (relations != null)
                foreach (XmlNode node in relations)
                    model.Relation(Attribute(node, "from"), Attribute(node, "to"), Attribute(node, "type"),
                        Float(node, "weight", 1f), Bool(node, "directed", true), Float(node, "confidence", 1f), Bool(node, "known", true));
            XmlNodeList metrics = root?.SelectNodes("./metrics/metric");
            if (metrics != null)
                foreach (XmlNode node in metrics)
                {
                    List<InsightSample> history = new List<InsightSample>();
                    XmlNodeList samples = node.SelectNodes("./sample");
                    if (samples != null)
                        foreach (XmlNode sample in samples)
                            history.Add(new InsightSample(Long(sample, "tick", 0L), Float(sample, "value", 0f)));
                    InsightTrend trend;
                    if (!Enum.TryParse(Attribute(node, "trend"), out trend)) trend = InsightTrend.Flat;
                    float? threshold = NullableFloat(node, "threshold");
                    model.Metric(Attribute(node, "entity"), Attribute(node, "label"), new InsightMetric(Attribute(node, "label"),
                        Float(node, "value", 0f), new InsightRange(Float(node, "minimum", 0f), Float(node, "maximum", 0f)),
                        Bool(node, "hasRange", true), Bool(node, "known", true), Float(node, "confidence", 1f), threshold, trend, history));
                }
            XmlNodeList events = root?.SelectNodes("./events/event");
            if (events != null)
                foreach (XmlNode node in events)
                {
                    List<string> ids = new List<string>();
                    XmlNodeList childEntities = node.SelectNodes("./entity");
                    if (childEntities != null)
                        foreach (XmlNode child in childEntities) ids.Add(Attribute(child, "id"));
                    model.Event(new InsightEvent(Attribute(node, "id"), Long(node, "tick", 0L), Attribute(node, "label"),
                        Attribute(node, "category"), ids, Float(node, "severity", 0f), Bool(node, "known", true), Attribute(node, "mapLink")));
                }
            return model;
        }

        private static string Attribute(XmlNode node, string name) => node.Attributes?[name]?.Value ?? string.Empty;

        private static float Float(XmlNode node, string name, float fallback)
        {
            float value;
            return float.TryParse(Attribute(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

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
