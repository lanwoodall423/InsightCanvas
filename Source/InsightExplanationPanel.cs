using System;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Interactive waterfall and plain-language explanation for the shared selection.</summary>
    public sealed class InsightExplanationPanel : InsightComponentBase
    {
        private string hoveredLabel;
        private InsightExplanation cachedExplanation;
        private InsightExplanationResult cachedResult;
        private int cachedRevision = -1;

        public InsightExplanationPanel(string componentId = "explanation") : base(componentId, InsightComponentRole.Explanation, 170f) { }

        protected override void DrawComponent(Rect rect, InsightRenderContext context)
        {
            InsightTheme theme = context.Theme;
            hoveredLabel = null;
            InsightDraw.Panel(rect, theme);
            Rect inner = rect.ContractedBy(10f);
            Text.Font = GameFont.Medium;
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f), "InsightCanvas_Explanation".Translate());
            Text.Font = GameFont.Small;
            string selectedId = context.Interaction.SelectedEntityId;
            InsightEntity entity = context.Snapshot.Entity(selectedId);
            InsightExplanation explanation = context.Snapshot.ExplanationFor(selectedId);
            if (entity == null || explanation == null)
            {
                InsightDraw.Empty(new Rect(inner.x, inner.y + 34f, inner.width, inner.height - 34f), theme,
                    entity == null ? "InsightCanvas_SelectEntity".Translate() : "InsightCanvas_NoExplanation".Translate());
                return;
            }
            InsightDisclosure disclosure = context.Interaction.DisclosureFor(entity);
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            Widgets.Label(new Rect(inner.x, inner.y + 30f, inner.width, 20f), entity.Label + "  |  " + disclosure.Label);
            if (cachedExplanation != explanation || cachedRevision != context.Snapshot.Revision)
            {
                cachedExplanation = explanation;
                cachedRevision = context.Snapshot.Revision;
                cachedResult = explanation.Calculate();
                context.Diagnostics.Invalidate();
            }
            InsightExplanationResult result = cachedResult;
            float y = inner.y + 53f;
            Text.WordWrap = true;
            float summaryHeight = Mathf.Clamp(Text.CalcHeight(result.Summary, inner.width), 24f, 62f);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(inner.x, y, inner.width, summaryHeight), result.Summary);
            Text.WordWrap = false;
            y += summaryHeight + 7f;
            DrawWaterfall(new Rect(inner.x, y, inner.width, 54f), result, disclosure, context);
            y += 62f;
            DrawRequirements(new Rect(inner.x, y, inner.width, inner.yMax - y), result, disclosure, context);
        }

        private void DrawWaterfall(Rect rect, InsightExplanationResult result, InsightDisclosure disclosure, InsightRenderContext context)
        {
            InsightTheme theme = context.Theme;
            if (!disclosure.ExactValuesVisible)
            {
                InsightDraw.Bar(new Rect(rect.x, rect.y + 10f, rect.width, 24f), 0.5f, theme.Unknown, theme, "?  " + "InsightCanvas_Approximate".Translate());
                GUI.color = InsightDraw.Color(theme.SecondaryText);
                Widgets.Label(new Rect(rect.x, rect.y + 35f, rect.width, 18f), "InsightCanvas_ExactValuesHidden".Translate());
                return;
            }
            int visibleCount = 0;
            float total = 0f;
            for (int i = 0; i < result.Segments.Count; i++)
            {
                InsightExplanationSegment segment = result.Segments[i];
                if (disclosure.CausalFactorsVisible || segment.Kind == InsightExplanationSegmentKind.Base || segment.Kind == InsightExplanationSegmentKind.Final)
                {
                    visibleCount++;
                    total += Mathf.Max(0.1f, Mathf.Abs(segment.After - segment.Before));
                }
            }
            if (visibleCount == 0) return;
            float x = rect.x;
            int visibleIndex = 0;
            for (int i = 0; i < result.Segments.Count; i++)
            {
                InsightExplanationSegment segment = result.Segments[i];
                if (!disclosure.CausalFactorsVisible && segment.Kind != InsightExplanationSegmentKind.Base && segment.Kind != InsightExplanationSegmentKind.Final) continue;
                float width = rect.width * Mathf.Max(0.08f, Mathf.Abs(segment.After - segment.Before) / total);
                if (visibleIndex == visibleCount - 1) width = rect.xMax - x;
                InsightColor token = ColorFor(segment, theme);
                Rect bar = new Rect(x, rect.y + 8f, Mathf.Max(4f, width - 2f), 24f);
                Widgets.DrawBoxSolid(bar, InsightDraw.Color(token.WithAlpha(segment.Known ? 0.9f : 0.38f)));
                Widgets.DrawBox(bar, segment.RequirementMet ? 1 : 2);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InsightDraw.Color(theme.PrimaryText);
                Widgets.Label(bar, Symbol(segment) + " " + segment.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(bar, Detail(segment));
                if (Mouse.IsOver(bar)) hoveredLabel = segment.Label;
                x += width;
                visibleIndex++;
            }
            if (result.HasComputationMismatch)
            {
                GUI.color = InsightDraw.Color(theme.Warning);
                Widgets.Label(new Rect(rect.x, rect.y + 35f, rect.width, 18f), "InsightCanvas_DerivationMismatch".Translate());
            }
            if (!string.IsNullOrEmpty(hoveredLabel))
            {
                GUI.color = InsightDraw.Color(theme.SecondaryText);
                Widgets.Label(new Rect(rect.x, rect.y + 35f, rect.width, 18f), hoveredLabel);
            }
        }

        private void DrawRequirements(Rect rect, InsightExplanationResult result, InsightDisclosure disclosure, InsightRenderContext context)
        {
            if (!disclosure.CausalFactorsVisible)
            {
                GUI.color = InsightDraw.Color(context.Theme.Unknown);
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "?  " + "InsightCanvas_CausalFactorsHidden".Translate());
                return;
            }
            float y = rect.y;
            for (int i = 0; i < result.Segments.Count && y + 22f <= rect.yMax; i++)
            {
                InsightExplanationSegment segment = result.Segments[i];
                if (segment.Kind != InsightExplanationSegmentKind.Requirement && segment.Kind != InsightExplanationSegmentKind.Uncertainty) continue;
                GUI.color = InsightDraw.Color(segment.Kind == InsightExplanationSegmentKind.Requirement && !segment.RequirementMet ? context.Theme.Negative : context.Theme.Warning);
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), Symbol(segment) + "  " + Detail(segment));
                y += 24f;
            }
        }

        private static InsightColor ColorFor(InsightExplanationSegment segment, InsightTheme theme)
        {
            if (segment.Kind == InsightExplanationSegmentKind.Requirement && !segment.RequirementMet) return theme.Negative;
            if (segment.Kind == InsightExplanationSegmentKind.Uncertainty) return theme.Unknown;
            if (segment.Kind == InsightExplanationSegmentKind.Clamp) return theme.Warning;
            if (segment.Kind == InsightExplanationSegmentKind.Base || segment.Kind == InsightExplanationSegmentKind.Final) return theme.Selected;
            return segment.After >= segment.Before ? theme.Positive : theme.Negative;
        }

        private static string Symbol(InsightExplanationSegment segment)
        {
            switch (segment.Kind)
            {
                case InsightExplanationSegmentKind.Factor: return segment.After >= segment.Before ? "+" : "-";
                case InsightExplanationSegmentKind.Additive: return segment.Amount >= 0f ? "+" : "-";
                case InsightExplanationSegmentKind.Clamp: return "CAP";
                case InsightExplanationSegmentKind.Requirement: return segment.RequirementMet ? "OK" : "!";
                case InsightExplanationSegmentKind.Uncertainty: return "?";
                default: return ">";
            }
        }

        private static string Detail(InsightExplanationSegment segment)
        {
            if (segment.Kind == InsightExplanationSegmentKind.Clamp) return segment.Label + " [" + segment.Range + "]";
            if (segment.Kind == InsightExplanationSegmentKind.Uncertainty) return segment.Label + " [" + segment.Range + "]";
            if (segment.Kind == InsightExplanationSegmentKind.Requirement) return segment.Label + (segment.RequirementMet ? " (met)" : " (unmet)");
            return segment.Label + ": " + segment.Before.ToString("0.##") + " -> " + segment.After.ToString("0.##");
        }
    }
}
