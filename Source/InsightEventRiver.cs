using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Zoomable event timeline with deterministic low-zoom clustering and metric tracks.</summary>
    public sealed class InsightEventRiver : InsightComponentBase
    {
        private InsightTimeRange visibleRange = new InsightTimeRange(1, 0);
        private InsightTimeRange allRange = new InsightTimeRange(1, 0);
        private string selectedEventId;
        private IReadOnlyList<InsightTimelineCluster> cachedClusters = new InsightTimelineCluster[0];
        private int clusterRevision = -1;
        private InsightTimeRange clusterRange = new InsightTimeRange(1, 0);
        private float clusterWidth = -1f;
        private int clusterBudget;

        public InsightEventRiver(string componentId = "event-river") : base(componentId, InsightComponentRole.Timeline, 120f) { }

        protected override void DrawComponent(Rect rect, InsightRenderContext context)
        {
            InsightTheme theme = context.Theme;
            InsightDraw.Panel(rect, theme);
            Rect header = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 30f);
            Text.Font = GameFont.Medium;
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(header.x, header.y, header.width - 300f, 26f), "InsightCanvas_EventRiver".Translate());
            Text.Font = GameFont.Small;
            EnsureRange(context);
            if (Widgets.ButtonText(new Rect(header.xMax - 292f, header.y + 1f, 82f, 26f), "InsightCanvas_AllTime".Translate()))
                context.Interaction.SetTimeRange(allRange);
            if (Widgets.ButtonText(new Rect(header.xMax - 204f, header.y + 1f, 92f, 26f), "InsightCanvas_ZoomIn".Translate()))
                SetZoom(context, 1.8f, allRange.Start + (allRange.End - allRange.Start) / 2);
            if (Widgets.ButtonText(new Rect(header.xMax - 106f, header.y + 1f, 100f, 26f), "InsightCanvas_ZoomOut".Translate()))
                SetZoom(context, 0.55f, allRange.Start + (allRange.End - allRange.Start) / 2);
            InsightTimeRange range = context.Interaction.TimeRange.IsEmpty ? visibleRange : context.Interaction.TimeRange;
            if (range.IsEmpty)
            {
                InsightDraw.Empty(new Rect(rect.x + 8f, header.yMax + 4f, rect.width - 16f, rect.height - 46f), theme, "InsightCanvas_NoEvents".Translate());
                return;
            }
            visibleRange = range;
            Rect plot = new Rect(rect.x + 10f, header.yMax + 4f, rect.width - 20f, Mathf.Max(0f, rect.height - 48f));
            DrawTracks(plot, context, range);
            DrawEvents(plot, context, range);
            HandleZoom(plot, context, range);
            context.Diagnostics.VisibleElements += context.Snapshot.Events.Count;
        }

        private void EnsureRange(InsightRenderContext context)
        {
            if (!allRange.IsEmpty && allRange.Equals(InsightTimelineMath.Bounds(context.Snapshot.Events))) return;
            allRange = InsightTimelineMath.Bounds(context.Snapshot.Events);
            visibleRange = allRange;
            if (!allRange.IsEmpty && context.Interaction.TimeRange.IsEmpty) context.Interaction.SetTimeRange(allRange);
        }

        private void DrawEvents(Rect plot, InsightRenderContext context, InsightTimeRange range)
        {
            float eventY = plot.y + plot.height * 0.6f;
            Widgets.DrawLineHorizontal(plot.x, eventY, plot.width, InsightDraw.Color(context.Theme.SecondaryText.WithAlpha(0.55f)));
            IReadOnlyList<InsightTimelineCluster> clusters = GetClusters(context, range, plot.width);
            for (int i = 0; i < clusters.Count; i++)
            {
                InsightTimelineCluster cluster = clusters[i];
                float x = plot.x + InsightTimelineMath.Position(cluster.Start, range, plot.width);
                float radius = cluster.Count > 1 ? Mathf.Min(14f, 5f + Mathf.Sqrt(cluster.Count)) : 5f;
                InsightEvent representative = cluster.Representative;
                bool disclosed = EventIsDisclosed(representative, context);
                bool selected = disclosed && representative != null && representative.Id == selectedEventId;
                Color color = EventColor(representative, context.Theme, selected, disclosed);
                Rect marker = new Rect(x - radius, eventY - radius, radius * 2f, radius * 2f);
                Widgets.DrawBoxSolid(marker, color);
                Widgets.DrawBox(marker, selected ? 2 : 1);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InsightDraw.Color(context.Theme.PrimaryText);
                if (cluster.Count > 1) Widgets.Label(marker, cluster.Count.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                Rect hit = new Rect(x - Mathf.Max(16f, radius), eventY - 24f, Mathf.Max(32f, radius * 2f), 48f);
                if (Mouse.IsOver(hit))
                {
                    GUI.color = InsightDraw.Color(context.Theme.PrimaryText);
                    Widgets.Label(new Rect(Mathf.Clamp(x - 70f, plot.x, plot.xMax - 140f), eventY - 46f, 140f, 20f),
                        cluster.Count > 1 ? cluster.Count + " " + "InsightCanvas_Events".Translate().ToString() : disclosed ? representative?.Label ?? "?" : "?  " + "InsightCanvas_UnknownEvent".Translate().ToString());
                }
                TooltipHandler.TipRegion(hit, cluster.Count > 1 ? cluster.Count + " events" : disclosed ? representative?.Label ?? "Unknown event" : "Unknown event");
                bool compareInput = Event.current != null && Event.current.shift;
                if (Widgets.ButtonInvisible(hit) && representative != null)
                {
                    selectedEventId = representative.Id;
                    if (representative.EntityIds.Count > 0)
                    {
                        if (compareInput) context.Interaction.Compare(representative.EntityIds[0]);
                        else context.Interaction.Select(representative.EntityIds[0]);
                    }
                }
                InsightMapReference mapReference = representative == null ? null : InsightMapBridge.ResolveLink(representative.MapLinkId);
                if (mapReference != null && Widgets.ButtonText(new Rect(x + 10f, eventY - 8f, 34f, 16f), "InsightCanvas_Map".Translate()))
                    InsightMapBridge.Focus("event:" + representative.Id, mapReference).Invoke();
            }
            DrawAxis(plot, eventY, context, range);
        }

        private void DrawTracks(Rect plot, InsightRenderContext context, InsightTimeRange range)
        {
            string selected = context.Interaction.SelectedEntityId;
            if (string.IsNullOrEmpty(selected)) return;
            int track = 0;
            track = DrawTracksFor(plot, context, range, selected, ref track, 0);
            string compared = context.Interaction.ComparedEntityId;
            if (!string.IsNullOrEmpty(compared) && compared != selected && track < 2)
                DrawTracksFor(plot, context, range, compared, ref track, 1);
        }

        private int DrawTracksFor(Rect plot, InsightRenderContext context, InsightTimeRange range, string entityId, ref int track, int colorOffset)
        {
            InsightEntity entity = context.Snapshot.Entity(entityId);
            if (entity == null || !context.Interaction.DisclosureFor(entity).HistoryVisible) return track;
            IReadOnlyList<InsightMetric> metrics = context.Snapshot.MetricsFor(entityId);
            for (int i = 0; i < metrics.Count && track < 2; i++)
            {
                InsightMetric metric = metrics[i];
                if (metric.History.Count < 2) continue;
                float y = plot.y + 20f + track * 24f;
                InsightSample previous = null;
                int sampleBudget = InsightCanvasMod.Settings?.TimelineSampleBudget ?? 600;
                int firstSample = Mathf.Max(0, metric.History.Count - sampleBudget);
                for (int sampleIndex = firstSample; sampleIndex < metric.History.Count; sampleIndex++)
                {
                    InsightSample sample = metric.History[sampleIndex];
                    if (previous != null)
                    {
                        Vector2 start = new Vector2(plot.x + InsightTimelineMath.Position(previous.Tick, range, plot.width), y);
                        Vector2 end = new Vector2(plot.x + InsightTimelineMath.Position(sample.Tick, range, plot.width), y - Mathf.Clamp(sample.Value - previous.Value, -1f, 1f) * 12f);
                        Widgets.DrawLine(start, end, InsightDraw.Color(context.Theme.ChartPalette[(track + colorOffset) % context.Theme.ChartPalette.Count]), 2f);
                    }
                    previous = sample;
                }
                GUI.color = InsightDraw.Color(context.Theme.SecondaryText);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(plot.x, y - 19f, 150f, 18f), (colorOffset > 0 ? "compare: " : string.Empty) + metric.Label);
                Text.Font = GameFont.Small;
                track++;
            }
            return track;
        }

        private IReadOnlyList<InsightTimelineCluster> GetClusters(InsightRenderContext context, InsightTimeRange range, float width)
        {
            int budget = Mathf.Clamp((InsightCanvasMod.Settings?.TimelineSampleBudget ?? 600) / 4, 20, 240);
            if (clusterRevision == context.Snapshot.Revision && clusterRange.Equals(range) && Math.Abs(clusterWidth - width) < 0.5f && clusterBudget == budget)
                return cachedClusters;
            cachedClusters = InsightTimelineMath.Cluster(context.Snapshot.Events, range, width, budget);
            clusterRevision = context.Snapshot.Revision;
            clusterRange = range;
            clusterWidth = width;
            clusterBudget = budget;
            context.Diagnostics.Invalidate();
            return cachedClusters;
        }

        private void DrawAxis(Rect plot, float eventY, InsightRenderContext context, InsightTimeRange range)
        {
            int tickCount = 5;
            for (int i = 0; i <= tickCount; i++)
            {
                float x = plot.x + plot.width * i / tickCount;
                GUI.color = InsightDraw.Color(context.Theme.SecondaryText.WithAlpha(0.35f));
                Widgets.DrawLineVertical(x, eventY + 7f, Mathf.Max(0f, plot.yMax - eventY - 7f));
                long tick = range.Start + (long)((range.End - range.Start) * (i / (float)tickCount));
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = InsightDraw.Color(context.Theme.SecondaryText);
                Widgets.Label(new Rect(x - 45f, eventY + 8f, 90f, 18f), tick.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void HandleZoom(Rect plot, InsightRenderContext context, InsightTimeRange range)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel || !Mouse.IsOver(plot)) return;
            float factor = current.delta.y > 0f ? 1.35f : 0.74f;
            long cursor = range.Start + (long)((current.mousePosition.x - plot.x) / Mathf.Max(1f, plot.width) * (range.End - range.Start));
            SetZoom(context, factor, cursor);
            current.Use();
        }

        private void SetZoom(InsightRenderContext context, float factor, long cursor)
        {
            InsightTimeRange range = context.Interaction.TimeRange.IsEmpty ? allRange : context.Interaction.TimeRange;
            context.Interaction.SetTimeRange(InsightTimelineMath.Zoom(range, factor, cursor));
        }

        private static bool EventIsDisclosed(InsightEvent insightEvent, InsightRenderContext context)
        {
            if (insightEvent == null || !insightEvent.Known) return false;
            if (insightEvent.EntityIds.Count == 0) return true;
            InsightEntity entity = context.Snapshot.Entity(insightEvent.EntityIds[0]);
            return entity == null || context.Interaction.DisclosureFor(entity).IdentityVisible;
        }

        private static Color EventColor(InsightEvent insightEvent, InsightTheme theme, bool selected, bool disclosed)
        {
            if (selected) return InsightDraw.Color(theme.Focus);
            if (!disclosed || insightEvent == null || !insightEvent.Known) return InsightDraw.Color(theme.Unknown);
            int index = Math.Abs((insightEvent.Category ?? string.Empty).GetHashCode()) % Math.Max(1, theme.ChartPalette.Count);
            return InsightDraw.Color(theme.ChartPalette[index]);
        }
    }
}
