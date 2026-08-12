using System;
using LudeonTK;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Optional user-facing accessibility and performance preferences.</summary>
    public sealed class InsightCanvasSettings : ModSettings
    {
        public bool ReducedMotion;
        public bool HighContrast;
        public bool ColorBlindFriendly;
        public bool ShowDiagnostics = true;
        public bool PreserveWindowOnMapAction;
        public int DisclosurePreview = 2;
        public int NodeBudget = 180;
        public int EdgeBudget = 360;
        public int TimelineSampleBudget = 600;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ReducedMotion, "reducedMotion", false);
            Scribe_Values.Look(ref HighContrast, "highContrast", false);
            Scribe_Values.Look(ref ColorBlindFriendly, "colorBlindFriendly", false);
            Scribe_Values.Look(ref ShowDiagnostics, "showDiagnostics", true);
            Scribe_Values.Look(ref PreserveWindowOnMapAction, "preserveWindowOnMapAction", false);
            Scribe_Values.Look(ref DisclosurePreview, "disclosurePreview", 2);
            Scribe_Values.Look(ref NodeBudget, "nodeBudget", 180);
            Scribe_Values.Look(ref EdgeBudget, "edgeBudget", 360);
            Scribe_Values.Look(ref TimelineSampleBudget, "timelineSampleBudget", 600);
            DisclosurePreview = Mathf.Clamp(DisclosurePreview, 0, 4);
            NodeBudget = Mathf.Clamp(NodeBudget, 32, 1000);
            EdgeBudget = Mathf.Clamp(EdgeBudget, 64, 2000);
            TimelineSampleBudget = Mathf.Clamp(TimelineSampleBudget, 100, 5000);
        }
    }

    /// <summary>Mod entry point. Insight Canvas deliberately has no Harmony dependency.</summary>
    public sealed class InsightCanvasMod : Mod
    {
        public static InsightCanvasSettings Settings { get; private set; }

        public InsightCanvasMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<InsightCanvasSettings>();
        }

        public override string SettingsCategory() => "Insight Canvas";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("InsightCanvas_ReducedMotion".Translate(), ref Settings.ReducedMotion,
                "InsightCanvas_ReducedMotionTip".Translate());
            listing.CheckboxLabeled("InsightCanvas_HighContrast".Translate(), ref Settings.HighContrast);
            listing.CheckboxLabeled("InsightCanvas_ColorBlind".Translate(), ref Settings.ColorBlindFriendly);
            listing.CheckboxLabeled("InsightCanvas_Diagnostics".Translate(), ref Settings.ShowDiagnostics);
            listing.CheckboxLabeled("InsightCanvas_PreserveMapWindow".Translate(), ref Settings.PreserveWindowOnMapAction);
            listing.Label("InsightCanvas_NodeBudget".Translate(Settings.NodeBudget));
            Settings.NodeBudget = (int)listing.Slider(Settings.NodeBudget, 32f, 1000f);
            listing.Label("InsightCanvas_EdgeBudget".Translate(Settings.EdgeBudget));
            Settings.EdgeBudget = (int)listing.Slider(Settings.EdgeBudget, 64f, 2000f);
            listing.GapLine(6f);
            if (Widgets.ButtonText(listing.GetRect(30f), "InsightCanvas_OpenFeatureShowcase".Translate())) OpenFeatureShowcase();
            listing.End();
        }

        /// <summary>Opens the opt-in general-purpose UI feature gallery.</summary>
        public static void OpenFeatureShowcase()
        {
            if (Find.WindowStack == null) return;
            Find.WindowStack.Add(InsightFeatureShowcase.CreateWindow());
        }

        [DebugAction("Insight Canvas", "Open Feature Showcase", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void OpenFeatureShowcaseDebugAction() => OpenFeatureShowcase();
    }
}
