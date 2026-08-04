using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Interactive relationship graph with incremental deterministic layout.</summary>
    public sealed class InsightConstellation : InsightComponentBase
    {
        private readonly InsightGraphLayoutSession layoutSession = new InsightGraphLayoutSession();
        private InsightGraphLayoutResult layout;
        private int layoutRevision = -1;
        private float layoutWidth;
        private float layoutHeight;
        private int layoutNodeBudget = -1;
        private int layoutEdgeBudget = -1;
        private string layoutFilter;
        private Vector2 pan;
        private float zoom = 1f;
        private Vector2 targetPan;
        private float targetZoom = 1f;
        private bool dragging;
        private Vector2 dragPosition;
        private readonly List<ClusterVisual> clusterVisuals = new List<ClusterVisual>();
        private int clusterRevision = -1;
        private int clusterIterations = -1;
        private Vector2 clusterPan;
        private float clusterLayoutWidth = -1f;
        private float clusterLayoutHeight = -1f;
        private string clusterFilter;
        private int clusterNodeBudget = -1;
        private int clusterEdgeBudget = -1;

        public InsightConstellation(string componentId = "constellation") : base(componentId, InsightComponentRole.Constellation, 190f) { }

        protected override void DrawComponent(Rect rect, InsightRenderContext context)
        {
            InsightTheme theme = context.Theme;
            InsightDraw.Panel(rect, theme);
            Rect header = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 32f);
            Text.Font = GameFont.Medium;
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(header.x, header.y, header.width - 190f, 26f), "InsightCanvas_Constellation".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(header.xMax - 184f, header.y + 1f, 78f, 26f), "InsightCanvas_Fit".Translate())) Fit();
            if (Widgets.ButtonText(new Rect(header.xMax - 100f, header.y + 1f, 94f, 26f), "InsightCanvas_Focus".Translate())) FocusSelected(context);
            Rect viewport = new Rect(rect.x + 8f, header.yMax + 4f, rect.width - 16f, Mathf.Max(0f, rect.height - 48f));
            if (viewport.width <= 4f || viewport.height <= 4f) return;
            EnsureLayout(context, viewport.width, viewport.height);
            AdvanceMotion(context);
            HandleViewportInput(viewport, context);
            GUI.BeginGroup(viewport);
            try
            {
                Widgets.DrawBoxSolid(new Rect(0f, 0f, viewport.width, viewport.height), InsightDraw.Color(theme.Background));
                DrawGraph(viewport, context);
                if (layout != null && !layout.Complete)
                {
                    GUI.color = InsightDraw.Color(theme.SecondaryText);
                    Widgets.Label(new Rect(8f, viewport.height - 24f, viewport.width - 16f, 20f), "InsightCanvas_Layouting".Translate());
                }
            }
            finally { GUI.EndGroup(); }
            DrawLegend(new Rect(viewport.x + 8f, viewport.y + 8f, Mathf.Min(220f, viewport.width - 16f), 20f), context);
            context.Diagnostics.VisibleElements += layout.ActiveNodeCount + layout.ActiveEdgeCount;
        }

        private void EnsureLayout(InsightRenderContext context, float width, float height)
        {
            int nodeBudget = InsightCanvasMod.Settings?.NodeBudget ?? 180;
            int edgeBudget = InsightCanvasMod.Settings?.EdgeBudget ?? 360;
            string filter = context.Interaction.FilterText ?? string.Empty;
            if (layoutRevision != context.Snapshot.Revision || Math.Abs(layoutWidth - width) > 0.5f ||
                Math.Abs(layoutHeight - height) > 0.5f || layoutNodeBudget != nodeBudget || layoutEdgeBudget != edgeBudget ||
                layoutFilter != filter)
            {
                layoutRevision = context.Snapshot.Revision;
                layoutWidth = width;
                layoutHeight = height;
                layoutNodeBudget = nodeBudget;
                layoutEdgeBudget = edgeBudget;
                layoutFilter = filter;
                layoutSession.Begin(context.Snapshot, width, height, nodeBudget, edgeBudget,
                    context.Interaction.MatchesFilter);
                layout = layoutSession.LiveResult();
                pan = Vector2.zero;
                zoom = 1f;
                targetPan = Vector2.zero;
                targetZoom = 1f;
                context.Diagnostics.Invalidate();
            }
            layoutSession.Step(2);
            layout = layoutSession.LiveResult();
        }

        private void DrawGraph(Rect viewport, InsightRenderContext context)
        {
            if (layout == null || layout.ActiveNodeCount == 0)
            {
                InsightDraw.Empty(new Rect(0f, 20f, viewport.width, viewport.height - 40f), context.Theme, "InsightCanvas_NoRelationships".Translate());
                return;
            }
            Vector2 center = new Vector2(viewport.width * 0.5f, viewport.height * 0.5f);
            DrawEdges(center, context);
            bool cluster = zoom < 0.56f;
            if (cluster)
            {
                RefreshClusters(context, center);
                for (int i = 0; i < clusterVisuals.Count; i++)
                {
                    ClusterVisual visual = clusterVisuals[i];
                    if (visual.Members.Count == 1) DrawNode(visual.Position, visual.Members[0], context, 1);
                    else DrawCluster(visual.Position, visual.Members, context);
                }
            }
            else
            {
                for (int i = 0; i < layout.ActiveNodeIds.Count; i++)
                {
                    InsightEntity entity = context.Snapshot.Entity(layout.ActiveNodeIds[i]);
                    if (entity == null) continue;
                    DrawNode(ScreenPosition(entity.Id, center), entity, context, 1);
                }
            }
        }

        private void RefreshClusters(InsightRenderContext context, Vector2 center)
        {
            if (clusterRevision == context.Snapshot.Revision && clusterIterations == (layout?.Iterations ?? -1) &&
                clusterPan == pan && Math.Abs(clusterLayoutWidth - layoutWidth) < 0.5f &&
                Math.Abs(clusterLayoutHeight - layoutHeight) < 0.5f && clusterFilter == layoutFilter &&
                clusterNodeBudget == layoutNodeBudget && clusterEdgeBudget == layoutEdgeBudget && clusterVisuals.Count > 0) return;
            clusterVisuals.Clear();
            Dictionary<int, ClusterVisual> lookup = new Dictionary<int, ClusterVisual>();
            for (int i = 0; i < layout.ActiveNodeIds.Count; i++)
            {
                InsightEntity entity = context.Snapshot.Entity(layout.ActiveNodeIds[i]);
                if (entity == null) continue;
                Vector2 position = ScreenPosition(entity.Id, center);
                int key = Mathf.RoundToInt(position.x / 46f) * 100000 + Mathf.RoundToInt(position.y / 46f);
                ClusterVisual visual;
                if (!lookup.TryGetValue(key, out visual))
                {
                    visual = new ClusterVisual { Position = position };
                    lookup[key] = visual;
                    clusterVisuals.Add(visual);
                }
                visual.Members.Add(entity);
            }
            clusterRevision = context.Snapshot.Revision;
            clusterIterations = layout?.Iterations ?? -1;
            clusterPan = pan;
            clusterLayoutWidth = layoutWidth;
            clusterLayoutHeight = layoutHeight;
            clusterFilter = layoutFilter;
            clusterNodeBudget = layoutNodeBudget;
            clusterEdgeBudget = layoutEdgeBudget;
        }

        private void DrawEdges(Vector2 center, InsightRenderContext context)
        {
            for (int i = 0; i < layout.Edges.Count; i++)
            {
                InsightRelation relation = layout.Edges[i];
                InsightEntity from = context.Snapshot.Entity(relation.FromId);
                InsightEntity to = context.Snapshot.Entity(relation.ToId);
                if (from == null || to == null || !layout.ContainsNode(relation.FromId) || !layout.ContainsNode(relation.ToId)) continue;
                InsightDisclosure fromDisclosure = context.Interaction.DisclosureFor(from);
                InsightDisclosure toDisclosure = context.Interaction.DisclosureFor(to);
                if (!fromDisclosure.IdentityVisible && !toDisclosure.IdentityVisible) continue;
                bool relationDisclosed = fromDisclosure.CausalFactorsVisible || toDisclosure.CausalFactorsVisible;
                bool neighbor = context.Interaction.HoveredEntityId == relation.FromId || context.Interaction.HoveredEntityId == relation.ToId ||
                    context.Interaction.SelectedEntityId == relation.FromId || context.Interaction.SelectedEntityId == relation.ToId;
                float alpha = relation.Known && relationDisclosed ? Mathf.Clamp01(0.2f + relation.Confidence * 0.45f) : 0.18f;
                if (neighbor) alpha = 0.9f;
                Color color = InsightDraw.Color(context.Theme.RelationColor(relation.Type).WithAlpha(alpha));
                Vector2 start = ScreenPosition(relation.FromId, center);
                Vector2 end = ScreenPosition(relation.ToId, center);
                if (relation.Known && relationDisclosed) Widgets.DrawLine(start, end, color, neighbor ? 2.4f : 1.2f);
                else DrawDashed(start, end, color);
                if (zoom > 0.72f)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = color;
                    Widgets.Label(new Rect((start.x + end.x) * 0.5f - 42f, (start.y + end.y) * 0.5f - 9f, 84f, 18f),
                        relation.Known && relationDisclosed ? relation.Type : "? " + relation.Type);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                if (relation.Directed) DrawArrow(start, end, color);
            }
        }

        private void DrawNode(Vector2 position, InsightEntity entity, InsightRenderContext context, int radiusScale)
        {
            InsightDisclosure disclosure = context.Interaction.DisclosureFor(entity);
            float radius = Mathf.Clamp(14f * zoom, 7f, 18f) * radiusScale;
            Rect hit = new Rect(position.x - radius, position.y - radius, radius * 2f, radius * 2f);
            bool hovered = Mouse.IsOver(hit);
            if (hovered) context.Interaction.Hover(entity.Id);
            bool selected = context.Interaction.SelectedEntityId == entity.Id;
            bool neighbor = context.Interaction.HoveredEntityId == entity.Id ||
                layout.AreNeighbors(context.Interaction.HoveredEntityId, entity.Id);
            InsightColor fill = selected ? context.Theme.Selected : hovered || neighbor ? context.Theme.Hover : context.Theme.ElevatedSurface;
            Widgets.DrawBoxSolid(hit, InsightDraw.Color(fill));
            Widgets.DrawBox(hit, selected ? 2 : 1);
            InsightDraw.Icon(new Rect(position.x - radius * 0.58f, position.y - radius * 0.58f, radius * 1.16f, radius * 1.16f),
                entity, context.Theme, !disclosure.IdentityVisible);
            if (zoom > 0.58f)
            {
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = InsightDraw.Color(disclosure.IdentityVisible ? context.Theme.PrimaryText : context.Theme.Unknown);
                Widgets.Label(new Rect(position.x - 58f, position.y + radius + 2f, 116f, 22f),
                    disclosure.IdentityVisible ? entity.Label : "?");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            TooltipHandler.TipRegion(hit, disclosure.IdentityVisible ? entity.Label : "InsightCanvas_UnknownEntity".Translate());
            if (Widgets.ButtonInvisible(hit)) context.Interaction.Select(entity.Id);
        }

        private void DrawCluster(Vector2 position, List<InsightEntity> members, InsightRenderContext context)
        {
            float radius = Mathf.Clamp(15f + members.Count * 1.4f, 17f, 32f);
            Rect rect = new Rect(position.x - radius, position.y - radius, radius * 2f, radius * 2f);
            Widgets.DrawBoxSolid(rect, InsightDraw.Color(context.Theme.Selected.WithAlpha(0.72f)));
            Widgets.DrawBox(rect, 2);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = InsightDraw.Color(context.Theme.PrimaryText);
            Widgets.Label(rect, members.Count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect)) context.Interaction.Select(members[0].Id);
            TooltipHandler.TipRegion(rect, members.Count + " " + "InsightCanvas_Cluster".Translate());
        }

        private Vector2 ScreenPosition(string id, Vector2 center)
        {
            InsightPoint point = layout.Position(id);
            return center + (new Vector2(point.X, point.Y) - center) * zoom + pan;
        }

        private void HandleViewportInput(Rect viewport, InsightRenderContext context)
        {
            Event current = Event.current;
            if (current == null || !Mouse.IsOver(viewport)) return;
            if (current.type == EventType.ScrollWheel)
            {
                float oldZoom = zoom;
                float next = Mathf.Clamp(zoom * (current.delta.y > 0f ? 0.88f : 1.14f), 0.25f, 2.8f);
                Vector2 local = current.mousePosition - viewport.position;
                Vector2 center = new Vector2(viewport.width * 0.5f, viewport.height * 0.5f);
                Vector2 before = (local - center - pan) / oldZoom;
                zoom = next;
                pan = local - center - before * zoom;
                targetZoom = zoom;
                targetPan = pan;
                current.Use();
            }
            else if (current.type == EventType.MouseDown && current.button == 2)
            {
                dragging = true;
                dragPosition = current.mousePosition;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && dragging)
            {
                pan += current.mousePosition - dragPosition;
                targetPan = pan;
                dragPosition = current.mousePosition;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 2)
            {
                dragging = false;
                current.Use();
            }
        }

        private void Fit()
        {
            targetZoom = 1f;
            targetPan = Vector2.zero;
        }

        private void FocusSelected(InsightRenderContext context)
        {
            if (layout == null || string.IsNullOrEmpty(context.Interaction.SelectedEntityId)) return;
            Vector2 center = new Vector2(layoutWidth * 0.5f, layoutHeight * 0.5f);
            InsightPoint point = layout.Position(context.Interaction.SelectedEntityId);
            targetPan = center - new Vector2(point.X, point.Y) * zoom;
        }

        private void AdvanceMotion(InsightRenderContext context)
        {
            bool reduced = InsightCanvasMod.Settings?.ReducedMotion ?? false;
            pan = new Vector2(InsightMotion.Approach(pan.x, targetPan.x, context.DeltaTime, 9f, reduced),
                InsightMotion.Approach(pan.y, targetPan.y, context.DeltaTime, 9f, reduced));
            zoom = InsightMotion.Approach(zoom, targetZoom, context.DeltaTime, 9f, reduced);
        }

        private static void DrawDashed(Vector2 start, Vector2 end, Color color)
        {
            Vector2 difference = end - start;
            float length = difference.magnitude;
            if (length < 1f) return;
            Vector2 direction = difference / length;
            for (float offset = 0f; offset < length; offset += 10f)
                Widgets.DrawLine(start + direction * offset, start + direction * Mathf.Min(length, offset + 5f), color, 1f);
        }

        private static void DrawArrow(Vector2 start, Vector2 end, Color color)
        {
            Vector2 difference = end - start;
            if (difference.sqrMagnitude < 4f) return;
            Vector2 direction = difference.normalized;
            Vector2 point = end - direction * 12f;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Widgets.DrawLine(point, point - direction * 6f + side * 4f, color, 1f);
            Widgets.DrawLine(point, point - direction * 6f - side * 4f, color, 1f);
        }

        private void DrawLegend(Rect rect, InsightRenderContext context)
        {
            GUI.color = InsightDraw.Color(context.Theme.SecondaryText);
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, "InsightCanvas_GraphHint".Translate() + "  " + Mathf.RoundToInt(zoom * 100f) + "%");
            Text.Font = GameFont.Small;
        }

        private sealed class ClusterVisual
        {
            public Vector2 Position;
            public readonly List<InsightEntity> Members = new List<InsightEntity>();
        }
    }
}
