using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Secondary laboratory inspector for theme, layout, accessibility, and stress previews.</summary>
    public sealed class InsightLaboratoryToolsWindow : Window
    {
        private readonly InsightWindow source;
        private Vector2 scroll;
        private string themeXml = "<theme id='laboratory'><color name='selected' value='#4faebe'/><spacing value='8'/></theme>";
        private float simulatedWidth = 1100f;
        private float simulatedHeight = 680f;
        private float simulatedScale = 1f;

        public InsightLaboratoryToolsWindow(InsightWindow source)
        {
            this.source = source;
            doCloseX = true;
            doCloseButton = false;
            resizeable = true;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(620f, UI.screenWidth * 0.8f), Mathf.Min(760f, UI.screenHeight * 0.86f));

        public override void DoWindowContents(Rect inRect)
        {
            using (new InsightGuiStateScope())
            {
                InsightTheme theme = (source?.ThemeOverride ?? InsightTheme.Default).WithAccessibility(
                    InsightCanvasMod.Settings?.HighContrast ?? false,
                    InsightCanvasMod.Settings?.ColorBlindFriendly == true ? InsightColorBlindMode.Deuteranopia : InsightColorBlindMode.None);
                Widgets.DrawBoxSolid(inRect, InsightDraw.Color(theme.Background));
                Rect outer = inRect.ContractedBy(8f);
                Rect view = new Rect(0f, 0f, outer.width - 16f, 1040f);
                Widgets.BeginScrollView(outer, ref scroll, view);
                try
                {
                    float y = 0f;
                    y = DrawHeader(view, y, theme);
                    y = DrawThemeEditor(view, y, theme);
                    y = DrawAccessibility(view, y, theme);
                    y = DrawResolutionPreview(view, y, theme);
                    y = DrawInspector(view, y, theme);
                    DrawStressControls(view, y, theme);
                }
                finally { Widgets.EndScrollView(); }
            }
        }

        private float DrawHeader(Rect view, float y, InsightTheme theme)
        {
            InsightDraw.Header(new Rect(0f, y, view.width, 54f), theme, "InsightCanvas_LaboratoryTools".Translate(),
                "InsightCanvas_ToolsSubtitle".Translate());
            return y + 62f;
        }

        private float DrawThemeEditor(Rect view, float y, InsightTheme theme)
        {
            Rect panel = new Rect(0f, y, view.width, 190f);
            InsightDraw.Panel(panel, theme, true);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f), "InsightCanvas_ThemePreview".Translate());
            themeXml = Widgets.TextField(new Rect(panel.x + 10f, panel.y + 38f, panel.width - 20f, 28f), themeXml);
            if (Widgets.ButtonText(new Rect(panel.x + 10f, panel.y + 72f, 126f, 28f), "InsightCanvas_ApplyTheme".Translate()))
            {
                InsightTheme parsed = InsightTheme.FromXml(themeXml, source.ThemeOverride ?? InsightTheme.Default);
                source.ThemeOverride = parsed;
                source.View.Invalidate();
            }
            if (Widgets.ButtonText(new Rect(panel.x + 144f, panel.y + 72f, 126f, 28f), "InsightCanvas_ResetTheme".Translate()))
            {
                source.ThemeOverride = null;
                source.View.Invalidate();
            }
            InsightColor[] swatches = { theme.Background, theme.Surface, theme.Selected, theme.Positive, theme.Warning, theme.Unknown };
            for (int i = 0; i < swatches.Length; i++)
            {
                Rect swatch = new Rect(panel.x + 10f + i * 54f, panel.y + 116f, 44f, 34f);
                Widgets.DrawBoxSolid(swatch, InsightDraw.Color(swatches[i]));
                Widgets.DrawBox(swatch);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InsightDraw.Color(theme.PrimaryText);
                Widgets.Label(swatch, i == 0 ? "bg" : i == 1 ? "surface" : i == 2 ? "select" : i == 3 ? "+" : i == 4 ? "!" : "?");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            return y + panel.height + 8f;
        }

        private float DrawAccessibility(Rect view, float y, InsightTheme theme)
        {
            Rect panel = new Rect(0f, y, view.width, 128f);
            InsightDraw.Panel(panel, theme, true);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f), "InsightCanvas_AccessibilityPreview".Translate());
            bool reduced = InsightCanvasMod.Settings?.ReducedMotion ?? false;
            Widgets.CheckboxLabeled(new Rect(panel.x + 10f, panel.y + 38f, panel.width - 20f, 28f), "InsightCanvas_ReducedMotion".Translate(), ref reduced);
            if (InsightCanvasMod.Settings != null) InsightCanvasMod.Settings.ReducedMotion = reduced;
            TieredDisclosureProvider provider = source.Context.DisclosureProvider as TieredDisclosureProvider;
            if (provider != null)
            {
                GUI.color = InsightDraw.Color(theme.SecondaryText);
                Widgets.Label(new Rect(panel.x + 10f, panel.y + 72f, 110f, 22f), "InsightCanvas_DisclosurePreview".Translate());
                float width = Mathf.Min(82f, (panel.width - 128f - provider.Count * 4f) / Math.Max(1, provider.Count));
                for (int i = 0; i < provider.Count; i++)
                {
                    int level = i;
                    if (Widgets.ButtonText(new Rect(panel.x + 122f + i * (width + 4f), panel.y + 69f, width, 28f), provider.Level(i).Label,
                        active: provider.ActiveLevel != i))
                    {
                        provider.ActiveLevel = level;
                        source.Context.NotifyDisclosureChanged();
                        source.View.Invalidate();
                    }
                }
            }
            return y + panel.height + 8f;
        }

        private float DrawResolutionPreview(Rect view, float y, InsightTheme theme)
        {
            Rect panel = new Rect(0f, y, view.width, 270f);
            InsightDraw.Panel(panel, theme, true);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f), "InsightCanvas_ResolutionPreview".Translate());
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 36f, 100f, 20f), "Width " + Mathf.RoundToInt(simulatedWidth));
            simulatedWidth = Widgets.HorizontalSlider(new Rect(panel.x + 112f, panel.y + 38f, panel.width - 124f, 20f), simulatedWidth, 480f, 1920f, true);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 64f, 100f, 20f), "Height " + Mathf.RoundToInt(simulatedHeight));
            simulatedHeight = Widgets.HorizontalSlider(new Rect(panel.x + 112f, panel.y + 66f, panel.width - 124f, 20f), simulatedHeight, 320f, 1200f, true);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 92f, 100f, 20f), "UI scale " + simulatedScale.ToString("0.00"));
            simulatedScale = Widgets.HorizontalSlider(new Rect(panel.x + 112f, panel.y + 94f, panel.width - 124f, 20f), simulatedScale, 0.75f, 1.5f, true);
            Rect preview = new Rect(panel.x + 10f, panel.y + 126f, panel.width - 20f, 128f);
            Widgets.DrawBoxSolid(preview, InsightDraw.Color(theme.Background));
            float scale = Mathf.Min(preview.width / simulatedWidth, preview.height / simulatedHeight) * simulatedScale;
            float previewWidth = simulatedWidth * scale;
            float previewHeight = simulatedHeight * scale;
            Rect simulated = new Rect(preview.center.x - previewWidth * 0.5f, preview.center.y - previewHeight * 0.5f, previewWidth, previewHeight);
            Widgets.DrawBoxSolid(simulated, InsightDraw.Color(theme.Surface));
            Widgets.DrawBox(simulated);
            IReadOnlyList<InsightLayoutBox> boxes = InsightLayout.ArrangeGrid(new InsightRect(0f, 0f, simulated.width - 16f, simulated.height - 16f), 4, 2, 4f, 8f, "preview");
            for (int i = 0; i < boxes.Count; i++)
            {
                InsightLayoutBox box = boxes[i];
                Rect item = new Rect(simulated.x + 8f + box.Rect.X, simulated.y + 8f + box.Rect.Y, box.Rect.Width, box.Rect.Height);
                Widgets.DrawBoxSolid(item, InsightDraw.Color(i == 0 ? theme.Selected.WithAlpha(0.55f) : theme.ElevatedSurface));
            }
            return y + panel.height + 8f;
        }

        private float DrawInspector(Rect view, float y, InsightTheme theme)
        {
            Rect panel = new Rect(0f, y, view.width, 188f);
            InsightDraw.Panel(panel, theme, true);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f), "InsightCanvas_InteractionInspector".Translate());
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 38f, panel.width - 20f, 20f), "selected: " + (source.Context.SelectedEntityId ?? "none"));
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 60f, panel.width - 20f, 20f), "hovered: " + (source.Context.HoveredEntityId ?? "none"));
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 82f, panel.width - 20f, 20f), "focused: " + (source.Context.FocusedEntityId ?? "none"));
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 104f, panel.width - 20f, 20f), "compared: " + (source.Context.ComparedEntityId ?? "none"));
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 126f, panel.width - 20f, 20f), "filter: " + (source.Context.FilterText ?? ""));
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 148f, panel.width - 20f, 20f), "time: " + (source.Context.TimeRange.IsEmpty ? "all" : source.Context.TimeRange.Start + ".." + source.Context.TimeRange.End));
            TooltipHandler.TipRegion(panel, source.Diagnostics.Summary());
            return y + panel.height + 8f;
        }

        private void DrawStressControls(Rect view, float y, InsightTheme theme)
        {
            Rect panel = new Rect(0f, y, view.width, 122f);
            InsightDraw.Panel(panel, theme, true);
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 24f), "InsightCanvas_StressDatasets".Translate());
            if (Widgets.ButtonText(new Rect(panel.x + 10f, panel.y + 40f, 180f, 30f), "InsightCanvas_OpenGraphStress".Translate())) OpenStress(120, 240);
            if (Widgets.ButtonText(new Rect(panel.x + 198f, panel.y + 40f, 180f, 30f), "InsightCanvas_OpenTimelineStress".Translate())) OpenStress(24, 1200);
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            Widgets.Label(new Rect(panel.x + 10f, panel.y + 78f, panel.width - 20f, 20f), source.Diagnostics.Summary());
        }

        private static void OpenStress(int nodes, int events)
        {
            InsightModel model = InsightLaboratory.CreateStressModel(nodes, events);
            InsightWindow window = new InsightWindow(model, InsightView.Create().Add(new InsightCardGrid()).Add(new InsightConstellation())
                .Add(new InsightExplanationPanel()).Add(new InsightEventRiver()), new InsightContext());
            if (Find.WindowStack != null) Find.WindowStack.Add(window);
        }
    }
}
