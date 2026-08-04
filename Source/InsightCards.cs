using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Responsive, virtualized entity card overview.</summary>
    public sealed class InsightCardGrid : InsightComponentBase
    {
        private readonly List<InsightEntity> filtered = new List<InsightEntity>();
        private Vector2 scroll;
        private int filteredRevision = -1;
        private string filteredText;
        private bool compact;
        private readonly Dictionary<string, float> badgeWidths = new Dictionary<string, float>(StringComparer.Ordinal);

        public InsightCardGrid(string componentId = "cards") : base(componentId, InsightComponentRole.Cards, 150f) { }

        protected override void DrawComponent(Rect rect, InsightRenderContext context)
        {
            InsightTheme theme = context.Theme;
            InsightDraw.Panel(rect, theme);
            Rect heading = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 32f);
            Text.Font = GameFont.Medium;
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(heading.x, heading.y, heading.width * 0.42f, 28f), "InsightCanvas_CardOverview".Translate());
            Text.Font = GameFont.Small;
            Rect filterRect = new Rect(Mathf.Max(heading.x + 180f, heading.xMax - 310f), heading.y + 1f, 220f, 28f);
            string filter = Widgets.TextField(filterRect, context.Interaction.FilterText ?? string.Empty);
            if (filter != context.Interaction.FilterText) context.Interaction.SetFilter(filter);
            if (Widgets.ButtonText(new Rect(filterRect.xMax + 6f, filterRect.y + 1f, 78f, 26f), compact ? "InsightCanvas_Expand".Translate() : "InsightCanvas_Compact".Translate()))
                compact = !compact;
            RefreshEntities(context);
            Rect listOuter = new Rect(rect.x + 8f, heading.yMax + 4f, rect.width - 16f, Mathf.Max(0f, rect.height - 48f));
            float gap = 7f;
            float minimumCardWidth = compact ? 190f : 230f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((listOuter.width + gap) / (minimumCardWidth + gap)), 1, 4);
            float cardWidth = Mathf.Max(120f, (listOuter.width - gap * (columns - 1) - 16f) / columns);
            float cardHeight = compact ? 98f : 132f;
            int rows = (filtered.Count + columns - 1) / columns;
            Rect view = new Rect(0f, 0f, listOuter.width - 16f, Mathf.Max(listOuter.height, rows * (cardHeight + gap)));
            Widgets.BeginScrollView(listOuter, ref scroll, view);
            try
            {
                int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / (cardHeight + gap)) - 1);
                int lastRow = Mathf.Min(rows, Mathf.CeilToInt((scroll.y + listOuter.height) / (cardHeight + gap)) + 1);
                for (int row = firstRow; row < lastRow; row++)
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        if (index >= filtered.Count) break;
                        Rect card = new Rect(column * (cardWidth + gap), row * (cardHeight + gap), cardWidth, cardHeight);
                        DrawCard(card, filtered[index], context, compact);
                    }
            }
            finally { Widgets.EndScrollView(); }
            if (filtered.Count == 0) InsightDraw.Empty(listOuter, theme, "InsightCanvas_NoMatches".Translate());
            HandleKeyboard(context);
            context.Diagnostics.VisibleElements += filtered.Count;
        }

        private void RefreshEntities(InsightRenderContext context)
        {
            string filter = context.Interaction.FilterText ?? string.Empty;
            if (filteredRevision == context.Snapshot.Revision && filteredText == filter) return;
            filtered.Clear();
            for (int i = 0; i < context.Snapshot.Entities.Count; i++)
            {
                InsightEntity entity = context.Snapshot.Entities[i];
                if (context.Interaction.MatchesFilter(entity)) filtered.Add(entity);
            }
            filteredRevision = context.Snapshot.Revision;
            filteredText = filter;
            context.Diagnostics.ApproximateAllocations++;
        }

        private void DrawCard(Rect rect, InsightEntity entity, InsightRenderContext context, bool compactLayout)
        {
            InsightContext interaction = context.Interaction;
            InsightTheme theme = context.Theme;
            InsightDisclosure disclosure = interaction.DisclosureFor(entity);
            bool selected = interaction.SelectedEntityId == entity.Id;
            bool compared = interaction.ComparedEntityId == entity.Id;
            bool focused = interaction.FocusedEntityId == entity.Id;
            bool hovered = Mouse.IsOver(rect);
            if (hovered) interaction.Hover(entity.Id);
            InsightDraw.Panel(rect, theme, selected || compared || focused);
            if (selected) Widgets.DrawHighlightSelected(rect);
            else if (hovered) Widgets.DrawHighlight(rect);
            if (compared && !selected) Widgets.DrawBox(rect, 2);
            Rect icon = new Rect(rect.x + 8f, rect.y + 8f, compactLayout ? 42f : 52f, compactLayout ? 42f : 52f);
            InsightDraw.Icon(icon, entity, theme, !disclosure.IdentityVisible);
            float textX = icon.xMax + 8f;
            float actionWidth = context.Snapshot.ActionsFor(entity.Id).Count > 0 ? 28f : 0f;
            Text.Font = GameFont.Small;
            GUI.color = InsightDraw.Color(disclosure.IdentityVisible ? theme.PrimaryText : theme.Unknown);
            Widgets.Label(new Rect(textX, rect.y + 7f, rect.width - textX + rect.x - actionWidth - 8f, 22f),
                disclosure.IdentityVisible ? entity.Label : "InsightCanvas_UnknownEntity".Translate().ToString());
            Text.Font = GameFont.Tiny;
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            Widgets.Label(new Rect(textX, rect.y + 29f, rect.width - textX + rect.x - actionWidth - 8f, 18f),
                disclosure.IdentityVisible ? entity.Subtitle : "InsightCanvas_Unidentified".Translate().ToString());
            Text.Font = GameFont.Small;
            if (disclosure.IdentityVisible) DrawBadges(entity, rect, textX, compactLayout, context);
            IReadOnlyList<InsightMetric> metrics = context.Snapshot.MetricsFor(entity.Id);
            float metricY = compactLayout ? rect.y + 60f : rect.y + 74f;
            int count = Mathf.Min(2, metrics.Count);
            for (int i = 0; i < count; i++) DrawMetric(new Rect(rect.x + 8f + i * (rect.width - 16f) / count, metricY,
                (rect.width - 20f) / Math.Max(1, count), compactLayout ? 28f : 44f), metrics[i], disclosure, context);
            if (count == 0)
            {
                GUI.color = InsightDraw.Color(theme.SecondaryText);
                Widgets.Label(new Rect(rect.x + 8f, metricY, rect.width - 16f, 24f), "InsightCanvas_NoMetrics".Translate());
            }
            if (context.Snapshot.ActionsFor(entity.Id).Count > 0)
            {
                Rect actions = new Rect(rect.xMax - 30f, rect.y + 6f, 24f, 24f);
                if (Widgets.ButtonText(actions, "..."))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    IReadOnlyList<InsightAction> values = context.Snapshot.ActionsFor(entity.Id);
                    for (int i = 0; i < values.Count; i++)
                    {
                        InsightAction action = values[i];
                        options.Add(new FloatMenuOption(action.Label, () => Invoke(action, context.Window, context.OverlayOwnerToken)));
                    }
                    if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            TooltipHandler.TipRegion(rect, disclosure.IdentityVisible ? entity.Label : "InsightCanvas_UnknownEntity".Translate());
            bool compareInput = Event.current != null && Event.current.shift;
            if (Widgets.ButtonInvisible(rect))
            {
                if (compareInput) interaction.Compare(entity.Id);
                else interaction.Select(entity.Id);
            }
        }

        private void DrawBadges(InsightEntity entity, Rect rect, float textX, bool compactLayout, InsightRenderContext context)
        {
            if (entity.Badges.Count == 0) return;
            float x = textX;
            float y = compactLayout ? rect.y + 51f : rect.y + 52f;
            for (int i = 0; i < entity.Badges.Count && x < rect.xMax - 8f; i++)
            {
                float width;
                if (!badgeWidths.TryGetValue(entity.Badges[i], out width))
                {
                    width = Mathf.Min(80f, Mathf.Max(30f, Text.CalcSize(entity.Badges[i]).x + 12f));
                    badgeWidths[entity.Badges[i]] = width;
                }
                InsightDraw.Badge(new Rect(x, y, width, 18f), entity.Badges[i], context.Theme.Selected, context.Theme);
                x += width + 4f;
            }
        }

        private void DrawMetric(Rect rect, InsightMetric metric, InsightDisclosure disclosure, InsightRenderContext context)
        {
            GUI.color = InsightDraw.Color(context.Theme.SecondaryText);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 16f), metric.Label);
            Text.Font = GameFont.Small;
            GUI.color = InsightDraw.Color(metric.Known ? context.Theme.PrimaryText : context.Theme.Unknown);
            string value = InsightDraw.MetricText(metric, disclosure);
            Widgets.Label(new Rect(rect.x, rect.y + 13f, rect.width, 20f), value);
            float fraction = metric.HasRange && metric.Range.Size > 0.001f ? (metric.Value - metric.Range.Minimum) / metric.Range.Size : metric.Value;
            if (disclosure.ExactValuesVisible && metric.Known) InsightDraw.Bar(new Rect(rect.x, rect.y + 35f, rect.width, 6f), fraction,
                metric.Trend == InsightTrend.Falling ? context.Theme.Negative : context.Theme.Selected, context.Theme);
        }

        private void HandleKeyboard(InsightRenderContext context)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || filtered.Count == 0) return;
            int selected = 0;
            for (int i = 0; i < filtered.Count; i++) if (filtered[i].Id == context.Interaction.SelectedEntityId) { selected = i; break; }
            int next = selected;
            if (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.DownArrow) next++;
            else if (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.UpArrow) next--;
            else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                context.Interaction.Select(filtered[selected].Id);
                current.Use();
                return;
            }
            else return;
            next = (next + filtered.Count) % filtered.Count;
            context.Interaction.Select(filtered[next].Id);
            current.Use();
        }

        private static void Invoke(InsightAction action, InsightWindow window, object ownerToken)
        {
            try
            {
                if (action == null) return;
                using (InsightMapBridge.BeginOwner(ownerToken))
                {
                    if (!action.Invoke()) return;
                }
                if (action.CloseWindowAfterInvoke) window?.Close(false);
            }
            catch (Exception exception) { Log.ErrorOnce("[Insight Canvas] Action failed: " + exception.Message, (action?.Id ?? "action").GetHashCode()); }
        }
    }
}
