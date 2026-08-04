using System;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Coordinates a model snapshot, shared interaction state, and retained components in a RimWorld Window.</summary>
    public sealed class InsightWindow : Window
    {
        private readonly InsightModel model;
        private readonly InsightView view;
        private readonly InsightContext context;
        private readonly InsightDiagnostics diagnostics = new InsightDiagnostics();
        private InsightModelSnapshot snapshot;
        private int snapshotRevision = -1;
        private InsightTheme theme;
        private InsightTheme themedOverride;
        private bool themedHighContrast;
        private bool themedColorBlind;
        private InsightRenderContext renderContext;
        private readonly object overlayOwnerToken = new object();

        public InsightWindow(InsightModel model, InsightView view, InsightContext context = null)
        {
            this.model = model ?? InsightModel.Create("Insight Canvas");
            this.view = view ?? InsightView.Create();
            this.context = context ?? new InsightContext();
            doCloseX = true;
            doCloseButton = false;
            resizeable = true;
            absorbInputAroundWindow = false;
            closeOnCancel = true;
        }

        public InsightModel Model => model;
        public InsightView View => view;
        public InsightContext Context => context;
        public InsightDiagnostics Diagnostics => diagnostics;
        public InsightModelSnapshot Snapshot => snapshot;
        public InsightTheme ThemeOverride { get; set; }
        internal object OverlayOwnerToken => overlayOwnerToken;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1280f, UI.screenWidth * 0.94f),
            Mathf.Min(860f, UI.screenHeight * 0.9f));

        public override void DoWindowContents(Rect inRect)
        {
            using (new InsightGuiStateScope())
            {
                diagnostics.BeginFrame();
                try
                {
                    Stopwatch snapshotTimer = Stopwatch.StartNew();
                    if (snapshot == null || snapshotRevision != model.Revision)
                    {
                        snapshot = model.Snapshot();
                        snapshotRevision = model.Revision;
                        view.Invalidate();
                        diagnostics.Invalidate();
                    }
                    snapshotTimer.Stop();
                    diagnostics.LastSnapshotMilliseconds = (float)snapshotTimer.Elapsed.TotalMilliseconds;
                    EnsureTheme();
                    context.BeginFrame();
                    using (InsightMapBridge.BeginOwner(overlayOwnerToken)) DrawWindow(inRect);
                }
                catch (Exception exception)
                {
                    Log.ErrorOnce("[Insight Canvas] Window failed to draw: " + exception, "insight-window-draw".GetHashCode());
                    GUI.color = Color.white;
                    Widgets.Label(inRect, "Insight Canvas could not render this snapshot. See the log for details.");
                }
                finally
                {
                    context.EndFrame();
                    diagnostics.EndFrame();
                }
            }
        }

        public override void PostClose()
        {
            InsightMapBridge.Clear(this);
            base.PostClose();
        }

        private void DrawWindow(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, InsightDraw.Color(theme.Background));
            Rect header = new Rect(rect.x, rect.y, rect.width, 43f);
            Widgets.DrawBoxSolid(header, InsightDraw.Color(theme.ElevatedSurface));
            Text.Font = GameFont.Medium;
            GUI.color = InsightDraw.Color(theme.PrimaryText);
            Widgets.Label(new Rect(header.x + 10f, header.y + 5f, header.width * 0.42f, 30f), model.Id);
            Text.Font = GameFont.Small;
            GUI.color = InsightDraw.Color(theme.SecondaryText);
            string selection = context.SelectedEntityId == null ? "InsightCanvas_NoSelection".Translate() :
                "InsightCanvas_Selected".Translate(context.SelectedEntityId);
            Widgets.Label(new Rect(header.x + header.width * 0.42f, header.y + 8f, header.width * 0.25f, 24f), selection);
            DrawDisclosureControls(new Rect(header.xMax - 440f, header.y + 6f, 330f, 30f));
            if (model.Id == "Insight Canvas Laboratory" && Widgets.ButtonText(new Rect(header.xMax - 144f, header.y + 6f, 64f, 30f), "InsightCanvas_Tools".Translate()))
                Find.WindowStack.Add(new InsightLaboratoryToolsWindow(this));
            if (Widgets.ButtonText(new Rect(header.xMax - 72f, header.y + 6f, 64f, 30f), "InsightCanvas_Reset".Translate()))
            {
                context.SetFilter(string.Empty);
                context.Select(null);
                context.ClearComparison();
                context.SetTimeRange(InsightTimeRange.Empty);
                view.Invalidate();
            }
            Rect body = new Rect(rect.x + 8f, header.yMax + 8f, rect.width - 16f,
                Mathf.Max(0f, rect.height - header.height - (InsightCanvasMod.Settings?.ShowDiagnostics == false ? 16f : 48f)));
            if (renderContext == null) renderContext = new InsightRenderContext(snapshot, context, theme, diagnostics, this,
                overlayOwnerToken, Time.deltaTime);
            else renderContext.Update(snapshot, context, theme, diagnostics, this, overlayOwnerToken, Time.deltaTime);
            view.Draw(body, renderContext);
            if (InsightCanvasMod.Settings?.ShowDiagnostics != false)
            {
                Rect footer = new Rect(rect.x + 10f, body.yMax + 5f, rect.width - 20f, 22f);
                GUI.color = InsightDraw.Color(theme.SecondaryText);
                Text.Font = GameFont.Tiny;
                Widgets.Label(footer, diagnostics.Summary());
                Text.Font = GameFont.Small;
            }
        }

        private void EnsureTheme()
        {
            bool highContrast = InsightCanvasMod.Settings?.HighContrast ?? false;
            bool colorBlind = InsightCanvasMod.Settings?.ColorBlindFriendly == true;
            if (theme != null && themedOverride == ThemeOverride && themedHighContrast == highContrast && themedColorBlind == colorBlind) return;
            themedOverride = ThemeOverride;
            themedHighContrast = highContrast;
            themedColorBlind = colorBlind;
            theme = (ThemeOverride ?? InsightTheme.Default).WithAccessibility(highContrast,
                colorBlind ? InsightColorBlindMode.Deuteranopia : InsightColorBlindMode.None);
        }

        private void DrawDisclosureControls(Rect rect)
        {
            TieredDisclosureProvider tiered = context.DisclosureProvider as TieredDisclosureProvider;
            if (tiered == null || tiered.Count == 0) return;
            float width = Mathf.Min(78f, (rect.width - 4f * (tiered.Count - 1)) / tiered.Count);
            for (int i = 0; i < tiered.Count; i++)
            {
                int level = i;
                InsightDisclosure disclosure = tiered.Level(i);
                if (Widgets.ButtonText(new Rect(rect.x + i * (width + 4f), rect.y, width, 28f), disclosure.Label,
                    active: tiered.ActiveLevel != i))
                {
                    tiered.ActiveLevel = level;
                    context.NotifyDisclosureChanged();
                    view.Invalidate();
                }
            }
        }
    }
}
