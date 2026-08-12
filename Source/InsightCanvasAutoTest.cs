using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>
    /// Runs the installed-mod acceptance suite inside RimWorld. It is inert during ordinary launches because all
    /// three DevBridge2 quicktest environment values are required before the component can start.
    /// </summary>
    public sealed class InsightCanvasAutoTestComponent : GameComponent
    {
        private const int WindowWaitTicks = 600;
        private const int InitialFrame = 0;
        private const int OverviewInteractionFrame = 1;
        private const int PageFrame = 2;
        private const int PageInteractionFrame = 3;
        private const int DataComparisonFrame = 4;

        private readonly List<InsightAutoTestCase> cases = new List<InsightAutoTestCase>();
        private bool completed;
        private bool started;
        private int waitedTicks;
        private int stage;
        private int currentPageIndex;
        private int interactionStep;
        private int lastObservedFrame = -1;
        private string runtimeRoot;
        private string failureInformation;
        private int mapOverlayBaseline = -1;
        private InsightUiWindow window;
        private InsightUiNavigation showcaseNavigation;

        public InsightCanvasAutoTestComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (completed)
                return;

            if (!IsDevBridgeQuicktest())
            {
                completed = true;
                return;
            }

            if (!GenScene.InPlayScene || Current.Game == null || Find.CurrentMap == null || Find.TickManager == null)
                return;

            if (!started)
            {
                StartSuite();
                return;
            }

            AdvanceSuite();
        }

        private bool IsDevBridgeQuicktest()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVBRIDGE_GENERATION"));
        }

        private void StartSuite()
        {
            started = true;
            runtimeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            WriteResult("RUNNING", "playable map detected; starting mod-owned Feature Showcase acceptance suite");
            try
            {
                RunCase("semantic-sample", CheckSemanticSample);
                RunCase("responsive-layout", CheckResponsiveLayout);
                RunCase("virtualization-bounds", CheckVirtualizationBounds);
                RunCase("document-state-isolation", CheckDocumentStateIsolation);
                RunCase("map-action-available", CheckMapActionAvailable);
                RunCase("window-created", CreateShowcaseWindow);
                RunCase("window-added", AddShowcaseWindow);
                stage = InitialFrame;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void AdvanceSuite()
        {
            if (window == null || window.Document == null)
            {
                Fail(new InvalidOperationException("Feature Showcase window disappeared before the suite completed"));
                return;
            }

            int frame = window.Document.Diagnostics.Frame;
            if (frame == 0 || frame <= lastObservedFrame)
            {
                waitedTicks++;
                if (waitedTicks > WindowWaitTicks)
                    Fail(new TimeoutException("Feature Showcase did not produce the next rendered frame within " + WindowWaitTicks + " game ticks"));
                return;
            }

            waitedTicks = 0;
            lastObservedFrame = frame;
            try
            {
                if (stage == InitialFrame)
                {
                    RunCase("window-rendered-overview", () => AssertRenderedState("overview"));
                    RunCase("overview-typography-and-badges", CheckOverviewTypography);
                    RunCase("overview-interactions-applied", ExerciseOverview);
                    stage = OverviewInteractionFrame;
                    window.Document.Invalidate();
                    return;
                }

                if (stage == OverviewInteractionFrame)
                {
                    RunCase("overview-interactions-rendered", () => AssertRenderedState("overview"));
                    GoToPage(1);
                    stage = PageFrame;
                    return;
                }

                if (stage == PageFrame)
                {
                    string pageId = showcaseNavigation.Pages[currentPageIndex].Id;
                    RunCase("page-" + pageId + "-rendered", () => AssertRenderedState(pageId));
                    if (pageId == "foundations")
                        RunCase("foundations-typography-rendered", CheckFoundationsTypography);
                    if (pageId == "data")
                    {
                        RunCase("data-filter-applied", ExerciseDataFilter);
                        interactionStep = 1;
                        stage = PageInteractionFrame;
                        window.Document.Invalidate();
                        return;
                    }

                    if (PageHasInteraction(pageId))
                    {
                        RunCase("page-" + pageId + "-interaction-applied", () => ExercisePage(pageId));
                        stage = PageInteractionFrame;
                        window.Document.Invalidate();
                        return;
                    }

                    AdvanceToNextPageOrFinish();
                    return;
                }

                if (stage == PageInteractionFrame)
                {
                    string pageId = showcaseNavigation.Pages[currentPageIndex].Id;
                    if (pageId == "data" && interactionStep == 1)
                    {
                        RunCase("data-filter-rendered", () => AssertRenderedState(pageId));
                        RunCase("data-selection-applied", ExerciseDataSelection);
                        interactionStep = 2;
                        stage = DataComparisonFrame;
                        window.Document.Invalidate();
                        return;
                    }

                    RunCase("page-" + pageId + "-interaction-rendered", () => AssertRenderedState(pageId));
                    if (pageId == "themes")
                        RunCase("themes-scoped-typography-rendered", CheckThemeTypographyState);
                    AdvanceToNextPageOrFinish();
                    return;
                }

                if (stage == DataComparisonFrame)
                {
                    RunCase("data-comparison-rendered", AssertDataComparison);
                    AdvanceToNextPageOrFinish();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void CreateShowcaseWindow()
        {
            window = InsightFeatureShowcase.CreateWindow();
            Require(window != null && window.Document != null && window.Document.Root != null,
                "Feature Showcase did not create a composable document");
            showcaseNavigation = FindElement(window.Document.Root, "showcase-navigation") as InsightUiNavigation;
            Require(showcaseNavigation != null && showcaseNavigation.Pages.Count == 10,
                "Feature Showcase did not expose its ten documented pages through public navigation");
            Require(window.Document.Id == "feature-showcase", "Feature Showcase document identity changed");
        }

        private void AddShowcaseWindow()
        {
            Require(Find.WindowStack != null, "RimWorld WindowStack was unavailable");
            Find.WindowStack.Add(window);
            Require(window != null, "Feature Showcase was not created for WindowStack insertion");
        }

        private void ExerciseOverview()
        {
            window.Document.State.SetBool("overview-inspector.expanded", true);
            InsightUiButton mapAction = FindElement(window.Document.Root, "overview-map-action") as InsightUiButton;
            Require(mapAction != null && mapAction.OnClick != null, "overview map action was not composed as a public button");
            InsightMapOverlayComponent overlay = Find.CurrentMap?.GetComponent<InsightMapOverlayComponent>();
            if (overlay != null) mapOverlayBaseline = overlay.EntryCount;
            window.Host.RunWithOverlayOwnership(mapAction.OnClick);
            if (overlay != null)
            {
                Require(overlay.EntryCount > mapOverlayBaseline,
                    "map-linked action did not register an owner-scoped overlay");
            }
        }

        private void CheckOverviewTypography()
        {
            InsightUiLabel title = FindElement(window.Document.Root, "showcase-title") as InsightUiLabel;
            InsightUiLabel subtitle = FindElement(window.Document.Root, "showcase-subtitle") as InsightUiLabel;
            InsightUiBadge heroBadge = FindElement(window.Document.Root, "overview-badge") as InsightUiBadge;
            InsightUiBadge layoutBadge = FindElement(window.Document.Root, "overview-layout-badge") as InsightUiBadge;
            Require(title != null && subtitle != null && heroBadge != null && layoutBadge != null,
                "overview typography or badge elements were not composed through the public API");
            Require(title.MeasuredSize.Height > subtitle.MeasuredSize.Height && title.LayoutRect.Height > 0f,
                "overview title and subtitle did not retain distinct measured typography geometry");
            Require(heroBadge.MeasuredSize.Height >= 22f && layoutBadge.MeasuredSize.Height >= 22f &&
                heroBadge.LayoutRect.Width + 0.01f >= heroBadge.MeasuredSize.Width &&
                layoutBadge.LayoutRect.Width + 0.01f >= layoutBadge.MeasuredSize.Width,
                "overview badge allocation was smaller than its measured caption content");
        }

        private void CheckFoundationsTypography()
        {
            InsightUiLabel title = FindElement(window.Document.Root, "foundation-title") as InsightUiLabel;
            InsightUiLabel body = FindElement(window.Document.Root, "foundation-body") as InsightUiLabel;
            InsightUiBadge badge = FindElement(window.Document.Root, "foundation-ready") as InsightUiBadge;
            InsightUiSurface themeRadius = FindElement(window.Document.Root, "foundation-theme-radius") as InsightUiSurface;
            InsightUiSurface roundedRadius = FindElement(window.Document.Root, "foundation-rounded-radius") as InsightUiSurface;
            InsightUiSurface squareRadius = FindElement(window.Document.Root, "foundation-square-radius") as InsightUiSurface;
            Require(title != null && body != null && badge != null && themeRadius != null &&
                roundedRadius != null && squareRadius != null,
                "foundations typography sample was not rendered through the public API");
            Require(title.MeasuredSize.Height > body.MeasuredSize.Height && badge.MeasuredSize.Height >= 22f &&
                badge.LayoutRect.Width + 0.01f >= badge.MeasuredSize.Width,
                "foundations typography hierarchy or badge geometry was inconsistent");
            Require(themeRadius.Style.CornerRadius < 0f && roundedRadius.Style.CornerRadius == 8f &&
                squareRadius.Style.CornerRadius == 0f,
                "foundations surface radius precedence examples were not configured");
        }

        private void CheckThemeTypographyState()
        {
            Require(window.Document.Density == InsightUiDensity.Compact && window.Document.HighContrast &&
                window.Document.ReducedMotion, "theme page did not retain its live accessibility and density state");
            InsightUiLabel status = FindElement(window.Document.Root, "themes-status") as InsightUiLabel;
            Require(status != null && status.MeasuredSize.Height > 0f && status.LayoutRect.Height > 0f,
                "theme variant status text did not receive a rendered layout slot");
            Require(window.Document.Theme.CornerRadius == 6f || window.Document.Theme.CornerRadius == 2f,
                "theme showcase did not apply a distinct corner-radius token");
            InsightUiDocument isolated = new InsightUiDocument("autotest-theme-isolation", InsightUi.Empty("root"));
            Require(isolated.Density == InsightUiDensity.Normal && !isolated.HighContrast && !isolated.ReducedMotion &&
                isolated.Theme.Selected.Equals(InsightTheme.Default.Selected),
                "showcase theme or density settings leaked into a second document");
        }

        private void ExercisePage(string pageId)
        {
            if (pageId == "layout")
            {
                InsightUiSlider width = FindElement(window.Document.Root, "layout-width") as InsightUiSlider;
                Require(width != null && width.Changed != null, "layout width simulation slider was not interactive");
                width.Value = 320f;
                width.Changed(320f);
                window.Document.State.SetFloat("layout-width.value", 320f);
                return;
            }

            if (pageId == "controls")
            {
                InsightUiToggle contrast = FindElement(window.Document.Root, "controls-contrast") as InsightUiToggle;
                InsightUiToggle motion = FindElement(window.Document.Root, "controls-motion") as InsightUiToggle;
                InsightUiSelect selector = FindElement(window.Document.Root, "controls-selector") as InsightUiSelect;
                InsightUiTextField text = FindElement(window.Document.Root, "controls-text") as InsightUiTextField;
                InsightUiButton primary = FindElement(window.Document.Root, "controls-primary") as InsightUiButton;
                InsightUiExpander expander = FindElement(window.Document.Root, "controls-expander") as InsightUiExpander;
                Require(contrast?.Changed != null && motion?.Changed != null && selector?.Changed != null &&
                    text?.Changed != null && primary?.OnClick != null && expander != null,
                    "controls page did not expose its interactive public elements");
                contrast.Changed(true);
                motion.Changed(true);
                selector.Changed(1, "Plan");
                text.Value = "automated input";
                text.Changed(text.Value);
                primary.OnClick();
                expander.SetExpanded(true);
                window.Document.State.SetBool("controls-contrast.value", true);
                window.Document.State.SetBool("controls-motion.value", true);
                window.Document.State.SetInt("controls-selector.selected", 1);
                window.Document.State.SetString("controls-text.value", text.Value);
                window.Document.State.SetBool("controls-expander.expanded", true);
                return;
            }

            if (pageId == "workspaces")
            {
                InsightUiTabs tabs = FindElement(window.Document.Root, "workspace-tabs") as InsightUiTabs;
                Require(tabs != null, "workspace tabs were not composed as a public tab element");
                tabs.Select("inspector");
                window.Document.State.SetString("workspace-tabs.active", "inspector");
                return;
            }

            if (pageId == "motion")
            {
                InsightUiButton advance = FindElement(window.Document.Root, "motion-advance") as InsightUiButton;
                InsightUiToggle reduced = FindElement(window.Document.Root, "motion-reduced-toggle") as InsightUiToggle;
                InsightUiExpander reveal = FindElement(window.Document.Root, "motion-reveal") as InsightUiExpander;
                Require(advance?.OnClick != null && reduced?.Changed != null && reveal != null,
                    "motion page did not expose its feedback controls");
                advance.OnClick();
                reduced.Changed(true);
                reveal.SetExpanded(true);
                window.Document.State.SetBool("motion-reduced-toggle.value", true);
                window.Document.State.SetBool("motion-reveal.expanded", true);
                return;
            }

            if (pageId == "themes")
            {
                InsightUiButton night = FindElement(window.Document.Root, "themes-night") as InsightUiButton;
                InsightUiSelect density = FindElement(window.Document.Root, "themes-density") as InsightUiSelect;
                InsightUiToggle contrast = FindElement(window.Document.Root, "themes-contrast") as InsightUiToggle;
                InsightUiToggle reduced = FindElement(window.Document.Root, "themes-reduced") as InsightUiToggle;
                Require(night?.OnClick != null && density?.Changed != null && contrast?.Changed != null &&
                    reduced?.Changed != null, "theme page did not expose scoped settings controls");
                night.OnClick();
                density.Changed(2, "Compact");
                contrast.Changed(true);
                reduced.Changed(true);
                window.Document.State.SetInt("themes-density.selected", 2);
                window.Document.State.SetBool("themes-contrast.value", true);
                window.Document.State.SetBool("themes-reduced.value", true);
                Require(window.Document.Density == InsightUiDensity.Compact && window.Document.HighContrast &&
                    window.Document.ReducedMotion, "theme, density, contrast, and motion settings were not applied");
                return;
            }

            if (pageId == "advanced")
            {
                InsightUiButton graph = FindElement(window.Document.Root, "advanced-graph-action") as InsightUiButton;
                Require(graph?.OnClick != null, "advanced graph widget action was not interactive");
                graph.OnClick();
                return;
            }

            if (pageId == "diagnostics")
            {
                InsightUiButton invalidate = FindElement(window.Document.Root, "diagnostics-invalidate") as InsightUiButton;
                Require(invalidate?.OnClick != null, "diagnostics invalidation action was not interactive");
                invalidate.OnClick();
            }
        }

        private void ExerciseDataFilter()
        {
            InsightUiSearchField search = FindElement(window.Document.Root, "data-search") as InsightUiSearchField;
            Require(search?.Changed != null, "data search field was not interactive");
            search.SetText("Research");
        }

        private void ExerciseDataSelection()
        {
            InsightUiButton first = FindElement(window.Document.Root, "data-record-button-record-2") as InsightUiButton;
            InsightUiButton second = FindElement(window.Document.Root, "data-record-button-record-6") as InsightUiButton;
            Require(first?.OnClick != null && second?.OnClick != null,
                "filtered virtualized records did not expose selectable rows");
            first.OnClick();
            second.OnClick();
        }

        private void AssertDataComparison()
        {
            AssertRenderedState("data");
            InsightUiButton first = FindElement(window.Document.Root, "data-record-button-record-2") as InsightUiButton;
            InsightUiButton second = FindElement(window.Document.Root, "data-record-button-record-6") as InsightUiButton;
            Require(first?.SelectedProvider != null && second?.SelectedProvider != null &&
                first.SelectedProvider() && second.SelectedProvider(),
                "data comparison did not retain both selected records");
        }

        private bool PageHasInteraction(string pageId)
        {
            return pageId == "layout" || pageId == "controls" || pageId == "workspaces" || pageId == "motion" ||
                pageId == "themes" || pageId == "advanced" || pageId == "diagnostics";
        }

        private void AdvanceToNextPageOrFinish()
        {
            if (currentPageIndex >= showcaseNavigation.Pages.Count - 1)
            {
                FinishSuite();
                return;
            }
            currentPageIndex++;
            GoToPage(currentPageIndex);
            stage = PageFrame;
            interactionStep = 0;
        }

        private void GoToPage(int index)
        {
            currentPageIndex = index;
            string pageId = showcaseNavigation.Pages[index].Id;
            showcaseNavigation.Select(pageId);
            window.Document.State.SetString(showcaseNavigation.Id + ".active", pageId);
            window.Document.Invalidate();
        }

        private void FinishSuite()
        {
            RunCase("window-close-and-overlay-cleanup", () =>
            {
                InsightMapOverlayComponent overlay = Find.CurrentMap?.GetComponent<InsightMapOverlayComponent>();
                int beforeClose = overlay == null ? 0 : overlay.EntryCount;
                Require(overlay == null || mapOverlayBaseline < 0 || beforeClose > mapOverlayBaseline,
                    "the showcase map action did not leave an overlay to clean up");
                window.Close(false);
                window.Host.PostClose();
                Require(overlay == null || mapOverlayBaseline < 0 || overlay.EntryCount == mapOverlayBaseline,
                    "closing the Feature Showcase did not clear owner-scoped overlays");
            });
            completed = true;
            WriteResult("PASS", "Feature Showcase rendered all ten pages and completed the mod-owned acceptance suite");
            Log.Message("[InsightCanvas AutoTest] PASS: Feature Showcase acceptance suite completed.");
        }

        private void CheckSemanticSample()
        {
            InsightModel model = InsightShowcaseData.CreateDemoModel();
            InsightModelValidation validation = model.Validate();
            Require(validation.IsValid, "showcase semantic sample was invalid: " + string.Join("; ", validation.Errors));
            InsightModelSnapshot snapshot = model.Snapshot();
            Require(snapshot.Entities.Count == 10 && snapshot.Relations.Count == 10 && snapshot.Events.Count == 7,
                "showcase semantic sample counts changed");
            InsightGraphLayoutResult layout = InsightGraphLayout.Compute(snapshot, 720f, 480f, 180, 360, 4);
            Require(layout.ActiveNodeCount == snapshot.Entities.Count && layout.ActiveEdgeCount <= 360,
                "optional graph widget did not include the expected active nodes");
            InsightGraphFit fit = InsightGraphViewport.Fit(layout, 720f, 480f, 32f);
            Require(fit.Zoom >= 0.25f && fit.Zoom <= 2.8f && IsFinite(fit.Pan.X) && IsFinite(fit.Pan.Y),
                "optional graph widget returned an invalid viewport transform");
        }

        private void CheckResponsiveLayout()
        {
            InsightUiNavigation navigation = InsightUi.Navigation("autotest-navigation", 700f);
            for (int i = 0; i < 10; i++)
                navigation.Add("page-" + i, "Page " + i, InsightUi.Empty("page-content-" + i, "page"));
            InsightUiStateStore state = new InsightUiStateStore();
            InsightUiDiagnostics diagnostics = new InsightUiDiagnostics();
            InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
                state, diagnostics, 1f / 60f);
            navigation.Measure(new InsightUiConstraints(0f, 1024f, 0f, 640f), frame);
            navigation.Arrange(new InsightRect(0f, 0f, 1024f, 640f), frame);
            Require(!navigation.IsCompact && navigation.MeasuredSize.Width > 0f,
                "wide navigation did not arrange a side rail");
            navigation.Measure(new InsightUiConstraints(0f, 420f, 0f, 640f), frame);
            navigation.Arrange(new InsightRect(0f, 0f, 420f, 640f), frame);
            Require(navigation.IsCompact && navigation.MeasuredSize.Height > 0f,
                "narrow navigation did not arrange a compact top bar");
        }

        private static void CheckVirtualizationBounds()
        {
            InsightVirtualizedRange visible = InsightVirtualization.Range(400, 32f, 240f, 640f, 2);
            Require(visible.Contains(20) && visible.End < 400 && visible.Start >= 0,
                "virtualized range was outside its bounded collection");
            Require(Math.Abs(InsightVirtualization.ContentHeight(400, 32f) - 12800f) < 0.001f,
                "virtualized content height was not deterministic");
        }

        private static void CheckDocumentStateIsolation()
        {
            InsightUiDocument first = new InsightUiDocument("autotest-first", InsightUi.Empty("root"));
            InsightUiDocument second = new InsightUiDocument("autotest-second", InsightUi.Empty("root"));
            first.State.SetBool("selected", true);
            first.Density = InsightUiDensity.Compact;
            first.HighContrast = true;
            first.ReducedMotion = true;
            Require(first.State.GetBool("selected") && !second.State.GetBool("selected") &&
                second.Density == InsightUiDensity.Normal && !second.HighContrast && !second.ReducedMotion,
                "composable UI state or accessibility settings leaked between documents");
        }

        private static void CheckMapActionAvailable()
        {
            InsightMapReference center = InsightMapBridge.ForCell(Find.CurrentMap, Find.CurrentMap.Center);
            InsightAction flash = InsightMapBridge.Flash("insight-autotest-flash", center, 1f);
            Require(flash.Enabled, "map flash action was not enabled for the loaded map");
        }

        private void AssertRenderedState(string pageId)
        {
            Require(showcaseNavigation.ActivePageId == pageId,
                "expected page '" + pageId + "' but navigation rendered '" + showcaseNavigation.ActivePageId + "'");
            InsightUiDiagnostics diagnostics = window.Document.Diagnostics;
            Require(diagnostics.Frame > 0 && diagnostics.MeasurePasses > 0 && diagnostics.ArrangePasses > 0 &&
                diagnostics.VisibleElements > 0, "page '" + pageId + "' did not populate layout/render diagnostics");
            Require(diagnostics.RenderErrors == 0, "page '" + pageId + "' captured " + diagnostics.RenderErrors + " render error(s)");
            AssertFiniteGeometry(window.Document.Root);
        }

        private static void AssertFiniteGeometry(InsightUiElement element)
        {
            if (element == null) return;
            InsightRect rect = element.LayoutRect;
            InsightUiSize size = element.MeasuredSize;
            Require(IsFinite(rect.X) && IsFinite(rect.Y) && IsFinite(rect.Width) && IsFinite(rect.Height) &&
                rect.Width >= -0.01f && rect.Height >= -0.01f && IsFinite(size.Width) && IsFinite(size.Height) &&
                size.Width >= -0.01f && size.Height >= -0.01f,
                "element '" + element.Id + "' had invalid measured or arranged geometry");
            IReadOnlyList<InsightUiElement> children = element.Children;
            for (int i = 0; i < children.Count; i++) AssertFiniteGeometry(children[i]);
        }

        private void RunCase(string name, Action action)
        {
            try
            {
                action();
                cases.Add(new InsightAutoTestCase(name, "PASS", "completed", window?.Document.Diagnostics.Frame ?? 0));
            }
            catch (Exception exception)
            {
                cases.Add(new InsightAutoTestCase(name, "FAIL", exception.Message, window?.Document.Diagnostics.Frame ?? 0));
                WriteResult("RUNNING", "case failed: " + name);
                throw new InvalidOperationException("case '" + name + "' failed: " + exception.Message, exception);
            }
            WriteResult("RUNNING", "completed case: " + name);
        }

        private void Fail(Exception exception)
        {
            completed = true;
            failureInformation = exception.GetType().Name + ": " + exception.Message;
            try
            {
                if (window != null)
                {
                    window.Close(false);
                    window.Host.PostClose();
                }
            }
            catch (Exception cleanupException)
            {
                failureInformation += " Cleanup: " + cleanupException.Message;
            }
            WriteResult("FAIL", failureInformation);
            Log.Error("[InsightCanvas AutoTest] FAIL: " + failureInformation + "\n" + exception);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static InsightUiElement FindElement(InsightUiElement root, string id)
        {
            if (root == null) return null;
            if (root.Id == id) return root;
            IReadOnlyList<InsightUiElement> children = root.Children;
            for (int i = 0; i < children.Count; i++)
            {
                InsightUiElement match = FindElement(children[i], id);
                if (match != null) return match;
            }
            return null;
        }

        private void WriteResult(string status, string message)
        {
            if (string.IsNullOrWhiteSpace(runtimeRoot)) return;
            try
            {
                string runtime = Path.Combine(Path.GetFullPath(runtimeRoot), "Runtime");
                Directory.CreateDirectory(runtime);
                string path = Path.Combine(runtime, "insightcanvas-autotest.json");
                string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                StringBuilder json = new StringBuilder();
                json.Append("{\n  \"status\": \"").Append(Escape(status)).Append("\",\n");
                json.Append("  \"message\": \"").Append(Escape(message)).Append("\",\n");
                json.Append("  \"generation\": \"").Append(Escape(Environment.GetEnvironmentVariable("DEVBRIDGE_GENERATION"))).Append("\",\n");
                json.Append("  \"processId\": ").Append(ProcessId()).Append(",\n");
                json.Append("  \"failure\": \"").Append(Escape(failureInformation)).Append("\",\n");
                json.Append("  \"cases\": [");
                for (int i = 0; i < cases.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append("\n    ").Append(cases[i].ToJson());
                }
                if (cases.Count > 0) json.Append("\n  ");
                json.Append("]\n}\n");
                File.WriteAllText(temporary, json.ToString(), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, null);
                        return;
                    }
                    catch
                    {
                        File.Delete(path);
                    }
                }
                File.Move(temporary, path);
            }
            catch (Exception exception)
            {
                Log.Warning("[InsightCanvas AutoTest] Could not write result: " + exception.Message);
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string ProcessId() => System.Diagnostics.Process.GetCurrentProcess().Id.ToString();

        private sealed class InsightAutoTestCase
        {
            public InsightAutoTestCase(string name, string status, string message, int frame)
            {
                Name = name;
                Status = status;
                Message = message;
                Frame = frame;
            }

            private string Name { get; }
            private string Status { get; }
            private string Message { get; }
            private int Frame { get; }

            public string ToJson()
            {
                return "{\"name\":\"" + Escape(Name) + "\",\"status\":\"" + Escape(Status) +
                    "\",\"message\":\"" + Escape(Message) + "\",\"frame\":" + Frame + "}";
            }
        }
    }
}
