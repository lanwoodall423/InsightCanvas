using System;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>
    /// Embeds the retained Insight Canvas view inside a host window such as a RimWorld main tab.
    /// The host owns the immutable snapshot boundary and shared interaction state; it does not query game state.
    /// </summary>
    public sealed class InsightCanvasHost
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

        public InsightCanvasHost(InsightModel model, InsightView view, InsightContext context = null)
        {
            this.model = model ?? InsightModel.Create("Insight Canvas");
            this.view = view ?? InsightView.Create();
            this.context = context ?? new InsightContext();
        }

        public InsightModel Model => model;
        public InsightView View => view;
        public InsightContext Context => context;
        public InsightDiagnostics Diagnostics => diagnostics;
        public InsightModelSnapshot Snapshot => snapshot;
        public InsightTheme ThemeOverride { get; set; }

        /// <summary>Draws the retained view using a stable model snapshot for this frame.</summary>
        public void Draw(Rect rect, float deltaTime = -1f)
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
                    float frameDelta = deltaTime < 0f ? Time.deltaTime : deltaTime;
                    if (renderContext == null)
                        renderContext = new InsightRenderContext(snapshot, context, theme, diagnostics, null, frameDelta);
                    else
                        renderContext.Update(snapshot, context, theme, diagnostics, null, frameDelta);
                    view.Draw(rect, renderContext);
                }
                catch (Exception exception)
                {
                    Log.ErrorOnce("[Insight Canvas] Embedded view failed to draw: " + exception,
                        "insight-host-draw".GetHashCode());
                    GUI.color = Color.white;
                    Widgets.Label(rect, "Insight Canvas could not render this snapshot. See the log for details.");
                }
                finally
                {
                    context.EndFrame();
                    diagnostics.EndFrame();
                }
            }
        }

        /// <summary>Clears temporary map previews owned by the embedded view.</summary>
        public void PostClose() => InsightMapBridge.Clear();

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
    }
}
