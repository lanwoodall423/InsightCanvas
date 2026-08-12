using System;
using InsightCanvas;
using Verse;

namespace InsightCanvasExample
{
    /// <summary>Binding a normal Verse ModSettings object without duplicating its values in UI state.</summary>
    public sealed class ModSettingsExample : ModSettings
    {
        public bool ShowHints = true;
        public int DensityIndex = 1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowHints, "showHints", true);
            Scribe_Values.Look(ref DensityIndex, "densityIndex", 1);
        }

        public InsightUiElement CreatePanel()
        {
            return InsightUi.Column("mod-settings",
                InsightUi.Label("title", "Example settings", InsightUiTextStyle.Title),
                InsightUi.Toggle("hints", "Show hints")
                    .Bind(() => ShowHints, value => ShowHints = value),
                InsightUi.Select("density", "Density",
                    new[] { "Comfortable", "Normal", "Compact" })
                    .Bind(() => DensityIndex,
                        value => DensityIndex = Math.Max(0, Math.Min(2, value))))
                .SetGap(8f)
                .SetPadding(12f);
        }

        public void Open()
        {
            Find.WindowStack.Add(new InsightUiWindow(
                new InsightUiDocument("Example settings", CreatePanel())));
        }
    }
}
