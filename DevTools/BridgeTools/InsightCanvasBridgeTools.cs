using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InsightCanvas;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;

namespace InsightCanvas.BridgeTools
{
    /// <summary>
    /// Development-only authenticated companion for the Feature Showcase acceptance suite.
    /// DevBridge2 owns the generation, lease, lifecycle, and operation boundary; this companion owns
    /// the mod-specific assertions and only mutates the temporary showcase window and document.
    /// </summary>
    public sealed class InsightCanvasBridgeTools
    {
        [Tool("insightcanvas/run_suite", Title = "Run Insight Canvas Feature Showcase suite",
            Description = "Render and exercise the ten-page Feature Showcase in the current real RimWorld game. " +
                          "The companion owns assertions; DevBridge2 owns lifecycle and the lease.",
            ResultDescription = "A bounded Feature Showcase assertion report and evidence summary.",
            Tags = new[] { "insightcanvas", "testing", "ui", "destructive" }, RequiresAuth = true)]
        public async Task<object> RunSuite(
            [ToolParameter(Description = "Real game ticks to advance before the showcase starts")]
            int warmupTicks = 120,
            [ToolParameter(Description = "Real game ticks to advance after the suite")]
            int settleTicks = 30,
            [ToolParameter(Description = "Capture one full-frame screenshot after the suite")]
            bool captureScreenshot = false,
            IRimBridgeContext context = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null)
                throw new InvalidOperationException("RimBridgeServer did not inject an execution context.");

            warmupTicks = Math.Max(0, Math.Min(warmupTicks, 2000));
            settleTicks = Math.Max(0, Math.Min(settleTicks, 1000));
            RimBridgeEvidenceManifest evidence = RimBridgeEvidence.CreateManifest(
                "InsightCanvas.FeatureShowcase.LiveSuite", Guid.NewGuid().ToString("N"));
            InsightCanvasSuite suite = new InsightCanvasSuite();

            AddBridgeAssertions(evidence, context);
            try
            {
                await AdvanceTicks(context, warmupTicks, "warmup", evidence, cancellationToken).ConfigureAwait(true);
                await context.MainThread.InvokeAsync(suite.CreateWindow).ConfigureAwait(true);
                await AdvanceTicks(context, InsightCanvasSuite.RenderWaitTicks, "initial-render", evidence,
                    cancellationToken).ConfigureAwait(true);
                await context.MainThread.InvokeAsync(suite.CheckOverviewAndInteract).ConfigureAwait(true);
                await AdvanceTicks(context, InsightCanvasSuite.RenderWaitTicks, "overview-interaction", evidence,
                    cancellationToken).ConfigureAwait(true);
                await context.MainThread.InvokeAsync(suite.CheckOverviewInteractionRendered).ConfigureAwait(true);

                for (int index = 1; index < suite.PageCount; index++)
                {
                    string pageId = suite.PageId(index);
                    await context.MainThread.InvokeAsync(() => suite.SelectPage(index)).ConfigureAwait(true);
                    await AdvanceTicks(context, InsightCanvasSuite.RenderWaitTicks, "page-" + pageId,
                        evidence, cancellationToken).ConfigureAwait(true);
                    await context.MainThread.InvokeAsync(() => suite.CheckPageAndInteract(pageId))
                        .ConfigureAwait(true);
                    await AdvanceTicks(context, InsightCanvasSuite.RenderWaitTicks, "page-" + pageId + "-interaction",
                        evidence, cancellationToken).ConfigureAwait(true);
                    await context.MainThread.InvokeAsync(() => suite.CheckPageInteractionRendered(pageId))
                        .ConfigureAwait(true);
                    if (pageId == "data")
                    {
                        await AdvanceTicks(context, InsightCanvasSuite.RenderWaitTicks, "page-data-comparison",
                            evidence, cancellationToken).ConfigureAwait(true);
                        await context.MainThread.InvokeAsync(suite.CheckDataComparisonRendered)
                            .ConfigureAwait(true);
                    }
                }

                await context.MainThread.InvokeAsync(suite.CheckSemanticViewAndPortableBoundaries)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                suite.Report.Fail("suite-execution", exception);
            }
            finally
            {
                if (suite.HasWindow)
                {
                    try
                    {
                        await context.MainThread.InvokeAsync(suite.CloseWindow).ConfigureAwait(true);
                    }
                    catch (Exception exception)
                    {
                        suite.Report.Fail("window-cleanup", exception);
                    }
                }
            }

            await AdvanceTicks(context, settleTicks, "settle", evidence, cancellationToken)
                .ConfigureAwait(true);
            suite.Report.Complete();
            evidence.environment.gameVersion = VersionControl.CurrentVersionString;
            evidence.environment.modVersion = typeof(InsightCanvasBridgeTools).Assembly.GetName().Version?.ToString() ?? string.Empty;
            evidence.environment.details = new
            {
                tool = "insightcanvas/run_suite",
                pageCount = suite.PageCount,
                bridgeClock = true,
                companionOwnsAssertions = true,
                coordinator = "DevBridge2"
            };
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("feature-showcase-suite", suite.Report.status == "PASS",
                "Feature Showcase live suite status: " + suite.Report.status,
                new
                {
                    suite.Report.assertionCount,
                    suite.Report.passedAssertions,
                    suite.Report.failedAssertionsCount,
                    suite.Report.blockedAssertionsCount
                }));
            if (suite.Report.failedAssertionsCount > 0)
            {
                evidence.errors.Add(new RimBridgeEvidenceError
                {
                    stage = "feature-showcase-suite",
                    message = string.Join("; ", suite.Report.failedAssertions.Take(32).ToArray())
                });
            }

            if (captureScreenshot)
                await CaptureScreenshot(context, evidence, cancellationToken).ConfigureAwait(true);

            RimBridgeEvidence.Complete(evidence);
            return new InsightCanvasBridgeRunResult
            {
                success = suite.Report.status == "PASS" && evidence.success,
                error = suite.Report.status == "PASS"
                    ? null
                    : "Feature Showcase suite failed: " +
                      string.Join("; ", suite.Report.failedAssertions.Take(8).ToArray()),
                evidence = new InsightCanvasEvidenceSummary
                {
                    success = evidence.success,
                    runId = evidence.runId,
                    assertionCount = evidence.assertions.Count,
                    errorCount = evidence.errors.Count
                },
                report = InsightCanvasSuiteReportSummary.From(suite.Report)
            };
        }

        [Tool("insightcanvas/get_test_surface", Title = "Describe Insight Canvas test surface",
            Description = "Return the ownership boundary and covered Feature Showcase surfaces.",
            Tags = new[] { "insightcanvas", "testing", "read-only" })]
        public object GetTestSurface()
        {
            return new
            {
                success = true,
                owner = "Insight Canvas RimBridge companion",
                coordinator = "DevBridge2",
                bridge = "RimBridgeServer",
                pages = new[] { "overview", "foundations", "layout", "controls", "workspaces", "data", "motion", "themes", "advanced", "diagnostics" },
                oldGameComponentRunnerRemoved = true,
                requestFiles = false
            };
        }

        private static async Task AdvanceTicks(IRimBridgeContext context, int ticks, string stage,
            RimBridgeEvidenceManifest evidence, CancellationToken cancellationToken)
        {
            if (ticks <= 0) return;
            RimBridgeTickResult result = await context.Game.RunForTicksAsync(ticks,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000, ForceNormalSpeed = true, PauseWhenDone = true },
                cancellationToken).ConfigureAwait(true);
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("ticks-" + stage, result != null && result.Success,
                result?.Message ?? "RimBridgeServer returned no tick result.",
                result == null ? null : new
                {
                    result.RequestedTicks,
                    result.CompletedTicks,
                    result.StartTicksGame,
                    result.EndTicksGame
                }));
            if (result == null || !result.Success)
                throw new InvalidOperationException("RimBridgeServer could not advance the game during " + stage + ".");
        }

        private static void AddBridgeAssertions(RimBridgeEvidenceManifest evidence, IRimBridgeContext context)
        {
            IReadOnlyList<RimBridgeToolDescriptor> tools = context.Tools.List();
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-tool-discovery", tools != null && tools.Count > 0,
                "RimBridgeServer tool surface was discovered.", new { toolCount = tools?.Count ?? 0 }));
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-live-game-info",
                context.Tools.Exists("rimworld/get_game_info"),
                "RimBridgeServer live game inspection is available."));
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-tick-control", context.Game != null,
                "RimBridgeServer real tick control was injected."));
        }

        private static async Task CaptureScreenshot(IRimBridgeContext context, RimBridgeEvidenceManifest evidence,
            CancellationToken cancellationToken)
        {
            if (!context.Tools.Exists("rimworld/take_screenshot"))
            {
                evidence.assertions.Add(RimBridgeEvidence.Fail("bridge-screenshot",
                    "RimBridgeServer screenshot capability is unavailable."));
                return;
            }

            RimBridgeToolCallResult<object> screenshot = await context.Tools.CallAsync<object>(
                "rimworld/take_screenshot",
                new { fileName = "insightcanvas-feature-showcase-" + evidence.runId, suppressMessage = true },
                new RimBridgeToolCallOptions { TimeoutMs = 30000 }, cancellationToken).ConfigureAwait(true);
            evidence.assertions.Add(RimBridgeEvidence.ToolSucceeded("bridge-screenshot", screenshot));
        }
    }

    public sealed class InsightCanvasBridgeRunResult
    {
        public bool success { get; set; }
        public string error { get; set; }
        public InsightCanvasEvidenceSummary evidence { get; set; }
        public InsightCanvasSuiteReportSummary report { get; set; }
    }

    public sealed class InsightCanvasEvidenceSummary
    {
        public bool success { get; set; }
        public string runId { get; set; }
        public int assertionCount { get; set; }
        public int errorCount { get; set; }
    }

    public sealed class InsightCanvasSuiteReportSummary
    {
        public string schemaVersion { get; set; }
        public string suite { get; set; }
        public string status { get; set; }
        public int assertionCount { get; set; }
        public int passedAssertions { get; set; }
        public int failedAssertionsCount { get; set; }
        public int blockedAssertionsCount { get; set; }
        public int startTick { get; set; }
        public int endTick { get; set; }
        public int elapsedTicks { get; set; }
        public List<string> failedAssertions { get; set; } = new List<string>();
        public List<string> exceptionDetails { get; set; } = new List<string>();

        public static InsightCanvasSuiteReportSummary From(InsightCanvasSuiteReport report)
        {
            return new InsightCanvasSuiteReportSummary
            {
                schemaVersion = report?.schemaVersion ?? string.Empty,
                suite = report?.suite ?? string.Empty,
                status = report?.status ?? "FAIL",
                assertionCount = report?.assertionCount ?? 0,
                passedAssertions = report?.passedAssertions ?? 0,
                failedAssertionsCount = report?.failedAssertionsCount ?? 0,
                blockedAssertionsCount = report?.blockedAssertionsCount ?? 0,
                startTick = report?.startTick ?? 0,
                endTick = report?.endTick ?? 0,
                elapsedTicks = report?.elapsedTicks ?? 0,
                failedAssertions = (report?.failedAssertions ?? new List<string>()).Take(32).ToList(),
                exceptionDetails = (report?.exceptionDetails ?? new List<string>()).Take(4).ToList()
            };
        }
    }

    public sealed class InsightCanvasSuiteReport
    {
        public string schemaVersion = "1";
        public string suite = "InsightCanvas.FeatureShowcase.LiveSuite";
        public string status = "PASS";
        public int assertionCount;
        public int passedAssertions;
        public int failedAssertionsCount;
        public int blockedAssertionsCount;
        public int startTick;
        public int endTick;
        public int elapsedTicks;
        public List<string> failedAssertions = new List<string>();
        public List<string> exceptionDetails = new List<string>();
        public List<InsightCanvasSuiteAssertion> assertions = new List<InsightCanvasSuiteAssertion>();

        public InsightCanvasSuiteReport()
        {
            startTick = Find.TickManager?.TicksGame ?? 0;
        }

        public void Check(string id, Func<string> action)
        {
            assertionCount++;
            try
            {
                string detail = action() ?? "ok";
                passedAssertions++;
                assertions.Add(new InsightCanvasSuiteAssertion { id = id, status = "PASS", detail = detail });
            }
            catch (InsightCanvasSuiteBlockedException exception)
            {
                blockedAssertionsCount++;
                assertions.Add(new InsightCanvasSuiteAssertion { id = id, status = "BLOCKED", detail = exception.Message });
            }
            catch (Exception exception)
            {
                Fail(id, exception);
            }
        }

        public void Fail(string id, Exception exception)
        {
            status = "FAIL";
            failedAssertionsCount++;
            failedAssertions.Add(id);
            exceptionDetails.Add(exception.ToString());
            assertions.Add(new InsightCanvasSuiteAssertion
            {
                id = id,
                status = "FAIL",
                detail = exception.Message,
                exception = exception.ToString()
            });
        }

        public void Complete()
        {
            endTick = Find.TickManager?.TicksGame ?? startTick;
            elapsedTicks = Math.Max(0, endTick - startTick);
            if (failedAssertionsCount > 0) status = "FAIL";
            else if (blockedAssertionsCount > 0) status = "BLOCKED";
        }
    }

    public sealed class InsightCanvasSuiteAssertion
    {
        public string id;
        public string status;
        public string detail;
        public string exception;
    }

    internal sealed class InsightCanvasSuiteBlockedException : Exception
    {
        public InsightCanvasSuiteBlockedException(string message) : base(message) { }
    }

    internal sealed class InsightCanvasSuite
    {
        internal const int RenderWaitTicks = 15;
        private readonly string[] pageIds =
        {
            "overview", "foundations", "layout", "controls", "workspaces", "data",
            "motion", "themes", "advanced", "diagnostics"
        };

        private InsightUiWindow window;
        private InsightUiNavigation navigation;
        private int overviewInvalidations;
        private int dataFilterInvalidations;

        internal InsightCanvasSuiteReport Report { get; } = new InsightCanvasSuiteReport();
        internal bool HasWindow => window != null;
        internal int PageCount => pageIds.Length;
        internal string PageId(int index) => pageIds[index];

        internal void CreateWindow()
        {
            Require(Find.CurrentMap != null, "Feature Showcase requires a playable current map.");
            window = InsightFeatureShowcase.CreateWindow();
            Require(window != null && window.Document != null && window.Document.Root != null,
                "Feature Showcase did not create a composable document.");
            navigation = FindElement(window.Document.Root, "showcase-navigation") as InsightUiNavigation;
            Require(navigation != null && navigation.Pages.Count == pageIds.Length,
                "Feature Showcase did not expose its ten documented pages through public navigation.");
            Require(window.Document.Id == "feature-showcase", "Feature Showcase document identity changed.");
            window.Document.TrackDuplicateIds = true;
            Require(Find.WindowStack != null, "RimWorld WindowStack was unavailable.");
            Find.WindowStack.Add(window);
            Report.Check("window-created-and-added", () => "Feature Showcase window created and added to WindowStack.");
        }

        internal void CheckOverviewAndInteract()
        {
            Report.Check("window-rendered-overview", () => AssertRenderedState("overview"));
            Report.Check("overview-typography-and-badges", CheckOverviewTypography);
            Report.Check("semantic-badge-foreground", CheckSemanticBadgeForeground);
            Report.Check("overview-composites-rendered", CheckOverviewComposites);
            Report.Check("rounded-surfaces-rendered", CheckRoundedSurfaces);
            Report.Check("overview-interactions-applied", ExerciseOverview);
            overviewInvalidations = window.Document.Diagnostics.Invalidations;
            window.Document.Invalidate();
        }

        internal void CheckOverviewInteractionRendered()
        {
            Report.Check("overview-interactions-rendered", () =>
            {
                string rendered = AssertRenderedState("overview");
                Require(window.Document.Diagnostics.Invalidations > overviewInvalidations,
                    "overview interaction did not invalidate the document.");
                return rendered + "; interaction invalidation observed";
            });
        }

        internal void SelectPage(int index)
        {
            string pageId = PageId(index);
            Report.Check("navigation-select-" + pageId, () =>
            {
                navigation.Select(pageId);
                window.Document.State.SetString(navigation.Id + ".active", pageId);
                window.Document.Invalidate();
                Require(navigation.ActivePageId == pageId,
                    "navigation selected '" + navigation.ActivePageId + "' instead of '" + pageId + "'.");
                return "selected " + pageId;
            });
        }

        internal void CheckPageAndInteract(string pageId)
        {
            Report.Check("page-" + pageId + "-rendered", () => AssertRenderedState(pageId));
            if (pageId == "foundations") Report.Check("foundations-typography-rendered", CheckFoundationsTypography);
            if (pageId == "controls") Report.Check("controls-hover-card-rendered", CheckHoverCardDogfood);
            if (pageId == "motion") Report.Check("motion-slide-fade-rendered", CheckSlideFadeDogfood);

            if (pageId == "data")
            {
                Report.Check("data-filter-applied", ExerciseDataFilter);
                dataFilterInvalidations = window.Document.Diagnostics.Invalidations;
                window.Document.Invalidate();
                return;
            }

            if (PageHasInteraction(pageId))
            {
                Report.Check("page-" + pageId + "-interaction-applied", () => ExercisePage(pageId));
                window.Document.Invalidate();
            }
        }

        internal void CheckPageInteractionRendered(string pageId)
        {
            if (pageId == "data")
            {
                Report.Check("data-filter-rendered", () =>
                {
                    string rendered = AssertRenderedState(pageId);
                    Require(window.Document.Diagnostics.Invalidations > dataFilterInvalidations,
                        "data filter did not invalidate the document.");
                    Report.Check("data-selection-applied", ExerciseDataSelection);
                    window.Document.Invalidate();
                    return rendered + "; filter interaction rendered";
                });
                return;
            }

            Report.Check("page-" + pageId + "-interaction-rendered", () => AssertRenderedState(pageId));
            if (pageId == "themes") Report.Check("themes-scoped-typography-rendered", CheckThemeTypographyState);
            if (pageId == "diagnostics") Report.Check("diagnostics-surface-healthy", CheckDiagnosticsSurface);
        }

        internal void CheckDataComparisonRendered()
        {
            Report.Check("data-comparison-rendered", AssertDataComparison);
        }

        internal void CheckSemanticViewAndPortableBoundaries()
        {
            Report.Check("semantic-sample", CheckSemanticSample);
            Report.Check("semantic-v2-public-api", CheckSemanticV2PublicApi);
            Report.Check("responsive-navigation-wide-and-narrow", CheckResponsiveLayout);
            Report.Check("responsive-composites-wide-and-narrow", CheckCompositeResponsiveLayout);
            Report.Check("virtualization-bounds", CheckVirtualizationBounds);
            Report.Check("document-state-isolation", CheckDocumentStateIsolation);
            Report.Check("map-action-available", CheckMapActionAvailable);
            Report.Check("unity-input-adapters", CheckUnityInputAdapters);
        }

        internal void CloseWindow()
        {
            Report.Check("window-close-and-overlay-cleanup", () =>
            {
                InsightMapOverlayComponent overlay = Find.CurrentMap?.GetComponent<InsightMapOverlayComponent>();
                InsightUiButton mapAction = FindElement(window.Document.Root, "overview-map-action") as InsightUiButton;
                Require(mapAction != null && mapAction.OnClick != null,
                    "overview map action was not available for cleanup verification.");
                window.Host.RunWithOverlayOwnership(mapAction.OnClick);
                int beforeClose = overlay == null ? 0 : overlay.EntryCount;
                Require(overlay == null || beforeClose > 0,
                    "the showcase map action did not leave an owner-scoped overlay to clean up.");
                window.Close(false);
                window.Host.PostClose();
                Require(overlay == null || overlay.EntryCount == 0,
                    "closing the Feature Showcase did not clear owner-scoped overlays.");
                return "window closed and owner-scoped overlays cleared";
            });
            window = null;
        }

        private string ExerciseOverview()
        {
            window.Document.State.SetBool("overview-inspector.expanded", true);
            InsightUiButton mapAction = FindElement(window.Document.Root, "overview-map-action") as InsightUiButton;
            Require(mapAction != null && mapAction.OnClick != null,
                "overview map action was not composed as a public button.");
            InsightMapOverlayComponent overlay = Find.CurrentMap?.GetComponent<InsightMapOverlayComponent>();
            int baseline = overlay == null ? -1 : overlay.EntryCount;
            window.Host.RunWithOverlayOwnership(mapAction.OnClick);
            if (overlay != null)
                Require(overlay.EntryCount > baseline, "map-linked action did not register an owner-scoped overlay.");
            return "overview expander and owner-scoped map action applied";
        }

        private string CheckOverviewTypography()
        {
            InsightUiLabel title = FindElement(window.Document.Root, "showcase-title") as InsightUiLabel;
            InsightUiLabel subtitle = FindElement(window.Document.Root, "showcase-subtitle") as InsightUiLabel;
            InsightUiBadge heroBadge = FindElement(window.Document.Root, "overview-badge") as InsightUiBadge;
            InsightUiBadge layoutBadge = FindElement(window.Document.Root, "overview-layout-badge") as InsightUiBadge;
            Require(title != null && subtitle != null && heroBadge != null && layoutBadge != null,
                "overview typography or badge elements were not composed through the public API.");
            Require(title.MeasuredSize.Height > subtitle.MeasuredSize.Height && title.LayoutRect.Height > 0f,
                "overview title and subtitle did not retain distinct measured typography geometry.");
            Require(heroBadge.MeasuredSize.Height >= 22f && layoutBadge.MeasuredSize.Height >= 22f &&
                heroBadge.LayoutRect.Width + 0.01f >= heroBadge.MeasuredSize.Width &&
                layoutBadge.LayoutRect.Width + 0.01f >= layoutBadge.MeasuredSize.Width,
                "overview badge allocation was smaller than its measured caption content.");
            return "title, subtitle, and intrinsic badges have valid geometry";
        }

        private string CheckSemanticBadgeForeground()
        {
            string[] ids = { "overview-badge", "overview-layout-badge", "overview-state-badge", "overview-access-badge" };
            for (int i = 0; i < ids.Length; i++)
            {
                InsightUiBadge badge = FindElement(window.Document.Root, ids[i]) as InsightUiBadge;
                Require(badge != null && badge.Color.HasValue && !badge.TextColor.HasValue,
                    "badge '" + ids[i] + "' did not retain semantic accent/default foreground separation.");
                AssertFiniteGeometry(badge);
            }
            return "semantic accents retain the default foreground contract";
        }

        private string CheckOverviewComposites()
        {
            InsightUiSectionHeader header = FindElement(window.Document.Root, "overview-composite-header") as InsightUiSectionHeader;
            InsightUiCallout callout = FindElement(window.Document.Root, "overview-power-callout") as InsightUiCallout;
            InsightUiMeter meter = FindElement(window.Document.Root, "overview-reserve-meter") as InsightUiMeter;
            InsightUiStatRow stat = FindElement(window.Document.Root, "overview-power-stat") as InsightUiStatRow;
            InsightUiSurface accent = FindElement(window.Document.Root, "overview-power-callout.accent") as InsightUiSurface;
            Require(header != null && callout != null && meter != null && stat != null && accent != null,
                "overview did not compose all four Feature Showcase composites through the public API.");
            Require(header.LayoutRect.Width > 0f && callout.LayoutRect.Height > 0f && meter.LayoutRect.Height > 0f &&
                stat.LayoutRect.Height > 0f && meter.NormalizedValue > 0f && stat.Secondary == "Workshop reserve" &&
                accent.Style.Background.Equals(window.Document.Theme.Warning),
                "overview composite geometry or runtime theme resolution was invalid.");
            AssertFiniteGeometry(header);
            AssertFiniteGeometry(callout);
            AssertFiniteGeometry(meter);
            AssertFiniteGeometry(stat);
            return "section header, callout, meter, stat, and theme accent rendered";
        }

        private string CheckFoundationsTypography()
        {
            InsightUiLabel title = FindElement(window.Document.Root, "foundation-title") as InsightUiLabel;
            InsightUiLabel body = FindElement(window.Document.Root, "foundation-body") as InsightUiLabel;
            InsightUiBadge badge = FindElement(window.Document.Root, "foundation-ready") as InsightUiBadge;
            InsightUiSurface themeRadius = FindElement(window.Document.Root, "foundation-theme-radius") as InsightUiSurface;
            InsightUiSurface roundedRadius = FindElement(window.Document.Root, "foundation-rounded-radius") as InsightUiSurface;
            InsightUiSurface squareRadius = FindElement(window.Document.Root, "foundation-square-radius") as InsightUiSurface;
            Require(title != null && body != null && badge != null && themeRadius != null &&
                roundedRadius != null && squareRadius != null,
                "foundations typography sample was not rendered through the public API.");
            Require(title.MeasuredSize.Height > body.MeasuredSize.Height && badge.MeasuredSize.Height >= 22f &&
                badge.LayoutRect.Width + 0.01f >= badge.MeasuredSize.Width,
                "foundations typography hierarchy or badge geometry was inconsistent.");
            Require(themeRadius.Style.CornerRadius < 0f && roundedRadius.Style.CornerRadius == 8f &&
                squareRadius.Style.CornerRadius == 0f,
                "foundations surface radius precedence examples were not configured.");
            return "typography hierarchy and radius precedence rendered";
        }

        private string CheckRoundedSurfaces()
        {
            string[] ids = { "overview-layout-badge", "overview-state-badge", "overview-access-badge",
                "overview-card-layout", "overview-card-state", "overview-card-access" };
            for (int i = 0; i < ids.Length; i++)
            {
                InsightUiElement element = FindElement(window.Document.Root, ids[i]);
                Require(element != null && element.LayoutRect.Width > 0f && element.LayoutRect.Height > 0f,
                    "rounded showcase surface '" + ids[i] + "' was not rendered with positive geometry.");
                AssertFiniteGeometry(element);
            }
            for (int i = 0; i < 3; i++)
            {
                InsightUiBadge badge = FindElement(window.Document.Root, ids[i]) as InsightUiBadge;
                Require(badge != null && badge.LayoutRect.Height >= 22f &&
                    Math.Abs(badge.LayoutRect.Width - badge.MeasuredSize.Width) < 0.01f &&
                    badge.Style.HorizontalAlignment == InsightAlignment.Start,
                    "Overview Badge did not preserve compact intrinsic geometry.");
            }
            return "rounded surfaces and compact badge allocation are finite";
        }

        private string CheckHoverCardDogfood()
        {
            InsightUiHoverCard hover = FindElement(window.Document.Root, "controls-hover-card") as InsightUiHoverCard;
            Require(hover != null && hover.Trigger != null && hover.Content != null,
                "controls did not compose the public display-only HoverCard.");
            Require(hover.HoverDelay > 0f && hover.HoverDelay <= 0.25f && hover.CloseDelay > 0f &&
                hover.CloseDelay <= 0.2f && hover.CardRect.Width >= 0f && hover.CardRect.Height >= 0f,
                "HoverCard timing or arranged bounds were outside the restrained contract.");
            AssertFiniteGeometry(hover);
            AssertFiniteGeometry(hover.Trigger);
            AssertFiniteGeometry(hover.Content);
            return "hover card timing and trigger/content geometry are valid";
        }

        private string CheckUnityInputAdapters()
        {
            RimWorldInsightUiPainter painter = new RimWorldInsightUiPainter();
            IInsightUiTranslationPainter translation = painter as IInsightUiTranslationPainter;
            IInsightUiHoverPainter hover = painter as IInsightUiHoverPainter;
            Require(translation != null && hover != null,
                "RimWorld painter did not expose the optional SlideFade and HoverCard capabilities.");
            translation.PushTranslation(new InsightPoint(6f, -4f));
            translation.PopTranslation();
            InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
                new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f,
                hostBounds: new InsightRect(0f, 0f, 320f, 180f));
            bool pointerResult = hover.IsPointerOver(new InsightRect(0f, 0f, 20f, 20f), frame);
            if (Event.current == null)
                Require(!pointerResult, "RimWorld painter reported pointer input outside a Unity GUI event.");
            return "Unity painter translation and hover adapters are available";
        }

        private string CheckSlideFadeDogfood()
        {
            InsightUiSlideFade slide = FindElement(window.Document.Root, "motion-slide-fade") as InsightUiSlideFade;
            Require(slide != null && slide.Direction == InsightUiSlideDirection.Down &&
                slide.Duration >= 0.1f && slide.Duration <= 0.2f && slide.Travel >= 4f && slide.Travel <= 8f,
                "Motion page did not dogfood the restrained public SlideFade component.");
            AssertFiniteGeometry(slide);
            slide.SetVisible(true);
            window.Document.Invalidate();
            return "SlideFade configuration and geometry are valid";
        }

        private string CheckThemeTypographyState()
        {
            Require(window.Document.Density == InsightUiDensity.Compact && window.Document.HighContrast &&
                window.Document.ReducedMotion, "theme page did not retain its live accessibility and density state.");
            InsightUiLabel status = FindElement(window.Document.Root, "themes-status") as InsightUiLabel;
            Require(status != null && status.MeasuredSize.Height > 0f && status.LayoutRect.Height > 0f,
                "theme variant status text did not receive a rendered layout slot.");
            Require(window.Document.Theme.CornerRadius == 6f || window.Document.Theme.CornerRadius == 2f,
                "theme showcase did not apply a distinct corner-radius token.");
            InsightUiDocument isolated = new InsightUiDocument("feature-showcase-theme-isolation", InsightUi.Empty("root"));
            Require(isolated.Density == InsightUiDensity.Normal && !isolated.HighContrast && !isolated.ReducedMotion &&
                isolated.Theme.Selected.Equals(InsightTheme.Default.Selected),
                "showcase theme or density settings leaked into a second document.");
            return "theme, density, contrast, and reduced-motion state stayed document-scoped";
        }

        private string ExercisePage(string pageId)
        {
            if (pageId == "layout")
            {
                InsightUiSlider width = FindElement(window.Document.Root, "layout-width") as InsightUiSlider;
                Require(width != null && width.Changed != null, "layout width simulation slider was not interactive.");
                width.Value = 320f;
                width.Changed(320f);
                window.Document.State.SetFloat("layout-width.value", 320f);
                return "layout width changed to 320px";
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
                    "controls page did not expose its interactive public elements.");
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
                return "toggle, select, text, button, and expander callbacks applied";
            }
            if (pageId == "workspaces")
            {
                InsightUiTabs tabs = FindElement(window.Document.Root, "workspace-tabs") as InsightUiTabs;
                Require(tabs != null, "workspace tabs were not composed as a public tab element.");
                tabs.Select("inspector");
                window.Document.State.SetString("workspace-tabs.active", "inspector");
                Require(tabs.ActiveTabId == "inspector", "workspace tab selection did not persist.");
                return "workspace inspector tab selected";
            }
            if (pageId == "motion")
            {
                InsightUiButton advance = FindElement(window.Document.Root, "motion-advance") as InsightUiButton;
                InsightUiToggle reduced = FindElement(window.Document.Root, "motion-reduced-toggle") as InsightUiToggle;
                InsightUiExpander reveal = FindElement(window.Document.Root, "motion-reveal") as InsightUiExpander;
                InsightUiSlideFade slide = FindElement(window.Document.Root, "motion-slide-fade") as InsightUiSlideFade;
                Require(advance?.OnClick != null && reduced?.Changed != null && reveal != null && slide != null,
                    "motion page did not expose its feedback controls.");
                advance.OnClick();
                reduced.Changed(true);
                reveal.SetExpanded(true);
                slide.SetVisible(true);
                window.Document.State.SetBool("motion-reduced-toggle.value", true);
                window.Document.State.SetBool("motion-reveal.expanded", true);
                return "motion progress, reduced-motion, reveal, and slide callbacks applied";
            }
            if (pageId == "themes")
            {
                InsightUiButton night = FindElement(window.Document.Root, "themes-night") as InsightUiButton;
                InsightUiSelect density = FindElement(window.Document.Root, "themes-density") as InsightUiSelect;
                InsightUiToggle contrast = FindElement(window.Document.Root, "themes-contrast") as InsightUiToggle;
                InsightUiToggle reduced = FindElement(window.Document.Root, "themes-reduced") as InsightUiToggle;
                Require(night?.OnClick != null && density?.Changed != null && contrast?.Changed != null &&
                    reduced?.Changed != null, "theme page did not expose scoped settings controls.");
                night.OnClick();
                density.Changed(2, "Compact");
                contrast.Changed(true);
                reduced.Changed(true);
                window.Document.State.SetInt("themes-density.selected", 2);
                window.Document.State.SetBool("themes-contrast.value", true);
                window.Document.State.SetBool("themes-reduced.value", true);
                Require(window.Document.Density == InsightUiDensity.Compact && window.Document.HighContrast &&
                    window.Document.ReducedMotion, "theme, density, contrast, and motion settings were not applied.");
                return "night theme, compact density, contrast, and reduced motion applied";
            }
            if (pageId == "advanced")
            {
                InsightUiButton graph = FindElement(window.Document.Root, "advanced-graph-action") as InsightUiButton;
                Require(graph?.OnClick != null, "advanced graph widget action was not interactive.");
                graph.OnClick();
                return "advanced graph action invoked";
            }
            if (pageId == "diagnostics")
            {
                InsightUiButton invalidate = FindElement(window.Document.Root, "diagnostics-invalidate") as InsightUiButton;
                Require(invalidate?.OnClick != null, "diagnostics invalidation action was not interactive.");
                invalidate.OnClick();
                return "diagnostics invalidation action invoked";
            }
            return "page has no additional interaction";
        }

        private string ExerciseDataFilter()
        {
            InsightUiSearchField search = FindElement(window.Document.Root, "data-search") as InsightUiSearchField;
            Require(search?.Changed != null, "data search field was not interactive.");
            search.SetText("Research");
            return "data search filtered to Research";
        }

        private string ExerciseDataSelection()
        {
            InsightUiButton first = FindElement(window.Document.Root, "data-record-button-record-2") as InsightUiButton;
            InsightUiButton second = FindElement(window.Document.Root, "data-record-button-record-6") as InsightUiButton;
            Require(first?.OnClick != null && second?.OnClick != null,
                "filtered virtualized records did not expose selectable rows.");
            first.OnClick();
            second.OnClick();
            return "two filtered records selected for comparison";
        }

        private string AssertDataComparison()
        {
            AssertRenderedState("data");
            InsightUiButton first = FindElement(window.Document.Root, "data-record-button-record-2") as InsightUiButton;
            InsightUiButton second = FindElement(window.Document.Root, "data-record-button-record-6") as InsightUiButton;
            Require(first?.SelectedProvider != null && second?.SelectedProvider != null &&
                first.SelectedProvider() && second.SelectedProvider(),
                "data comparison did not retain both selected records.");
            Require(window.Document.Diagnostics.VirtualizedVisibleElements > 0 &&
                window.Document.Diagnostics.VirtualizedCachedElements > 0,
                "data page did not report virtualized visible and cached elements.");
            return "filtered comparison retained both selections and virtualization diagnostics";
        }

        private string CheckDiagnosticsSurface()
        {
            InsightUiLabel summary = FindElement(window.Document.Root, "diagnostics-summary") as InsightUiLabel;
            InsightUiLabel errors = FindElement(window.Document.Root, "diagnostics-errors") as InsightUiLabel;
            Require(summary != null && summary.TextProvider != null && errors != null,
                "diagnostics page did not expose summary and render-error surfaces.");
            Require(window.Document.Diagnostics.RenderErrors == 0 &&
                window.Document.Diagnostics.DuplicateIds == 0 &&
                summary.DisplayText.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0 &&
                errors.DisplayText.IndexOf("none", StringComparison.OrdinalIgnoreCase) >= 0,
                "diagnostics page reported render errors, duplicate IDs, or an invalid summary.");
            return window.Document.Diagnostics.Summary();
        }

        private string AssertRenderedState(string pageId)
        {
            Require(navigation.ActivePageId == pageId,
                "expected page '" + pageId + "' but navigation rendered '" + navigation.ActivePageId + "'.");
            InsightUiDiagnostics diagnostics = window.Document.Diagnostics;
            Require(diagnostics.Frame > 0 && diagnostics.MeasurePasses > 0 && diagnostics.ArrangePasses > 0 &&
                diagnostics.VisibleElements > 0, "page '" + pageId + "' did not populate layout/render diagnostics.");
            Require(diagnostics.RenderErrors == 0 && diagnostics.DuplicateIds == 0,
                "page '" + pageId + "' captured render errors or duplicate IDs.");
            AssertFiniteGeometry(window.Document.Root);
            return "frame " + diagnostics.Frame + "; measure " + diagnostics.MeasurePasses +
                "; arrange " + diagnostics.ArrangePasses + "; visible " + diagnostics.VisibleElements;
        }

        private static string CheckSemanticSample()
        {
            InsightModel model = InsightShowcaseData.CreateDemoModel();
            InsightModelValidation validation = model.Validate();
            Require(validation.IsValid, "showcase semantic sample was invalid: " + string.Join("; ", validation.Errors));
            InsightModelSnapshot snapshot = model.Snapshot();
            Require(snapshot.Entities.Count == 10 && snapshot.Relations.Count == 10 && snapshot.Events.Count == 7,
                "showcase semantic sample counts changed.");
            InsightGraphLayoutResult layout = InsightGraphLayout.Compute(snapshot, 720f, 480f, 180, 360, 18);
            Require(layout.ActiveNodeCount == snapshot.Entities.Count && layout.ActiveEdgeCount <= 360 && layout.Complete &&
                layout.Positions.Count == layout.ActiveNodeCount && layout.Positions.Values.All(point =>
                    IsFinite(point.X) && IsFinite(point.Y)),
                "optional graph widget did not include the expected active nodes.");
            return "semantic sample validated with ten entities, ten relations, and seven events";
        }

        private static string CheckSemanticV2PublicApi()
        {
            InsightModel model = InsightModel.Create("feature-showcase-v2").Entity("site:alpha", "Alpha");
            InsightContext firstContext = new InsightContext();
            InsightContext secondContext = new InsightContext();
            InsightUiSemanticView first = InsightUi.SemanticView("showcase-semantic-1", model, InsightView.Create(), firstContext);
            InsightUiSemanticView second = InsightUi.SemanticView("showcase-semantic-2", model, InsightView.Create(), secondContext);
            InsightUiDocument document = new InsightUiDocument("feature-showcase-semantic-document",
                InsightUi.Column("showcase-semantic-root", InsightUi.Label("showcase-semantic-label", "Ordinary"), first, second));
            document.Diagnostics.BeginFrame();
            InsightUiFrame frame = new InsightUiFrame(document.Theme, document.Density, document.HighContrast,
                document.ReducedMotion, document.State, document.Diagnostics, 1f / 60f,
                hostBounds: new InsightRect(0f, 0f, 480f, 240f));
            document.Root.Measure(new InsightUiConstraints(0f, 480f, 0f, 240f), frame);
            document.Root.Arrange(new InsightRect(0f, 0f, 480f, 240f), frame);
            Require(ReferenceEquals(first.Model, model) && ReferenceEquals(second.Model, model) &&
                ReferenceEquals(first.Context, firstContext) && ReferenceEquals(second.Context, secondContext) &&
                first.Snapshot != null && second.Snapshot != null && first.SnapshotRevision == model.Revision &&
                second.SnapshotRevision == model.Revision && document.Diagnostics.SemanticSnapshotRefreshes == 2,
                "v2 SemanticView did not retain shared model, independent contexts, and immutable snapshots through Measure.");
            InsightUiDocument secondDocument = new InsightUiDocument("feature-showcase-semantic-cache",
                InsightUi.Column("cache-root", first));
            InsightModelSnapshot firstSnapshot = first.Snapshot;
            secondDocument.Diagnostics.BeginFrame();
            InsightUiFrame secondFrame = new InsightUiFrame(secondDocument.Theme, secondDocument.Density,
                secondDocument.HighContrast, secondDocument.ReducedMotion, secondDocument.State,
                secondDocument.Diagnostics, 1f / 60f, hostBounds: new InsightRect(0f, 0f, 480f, 240f));
            secondDocument.Root.Measure(new InsightUiConstraints(0f, 480f, 0f, 240f), secondFrame);
            Require(ReferenceEquals(first.Snapshot, firstSnapshot),
                "v2 SemanticView rebuilt an immutable snapshot without a model revision change.");
            secondDocument.State.SetBool("retained", true);
            int revision = secondDocument.Revision;
            secondDocument.Root = InsightUi.Empty("replacement");
            Require(secondDocument.Revision > revision && secondDocument.State.GetBool("retained"),
                "v2 document root replacement did not retain state.");
            first.ReplaceModel(InsightModel.Create("feature-showcase-replacement"));
            first.ReplaceView(InsightView.Create());
            first.ReplaceContext(new InsightContext());
            Require(first.Snapshot == null && first.SnapshotRevision == -1,
                "v2 source replacement did not defer its new snapshot until the next Measure.");
            return "retained semantic views preserved references, cached snapshots, state, and replacement deferral";
        }

        private static string CheckResponsiveLayout()
        {
            InsightUiNavigation testNavigation = InsightUi.Navigation("feature-showcase-responsive", 700f);
            for (int i = 0; i < 10; i++)
                testNavigation.Add("page-" + i, "Page " + i, InsightUi.Empty("page-content-" + i, "page"));
            InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
                new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
            testNavigation.Measure(new InsightUiConstraints(0f, 1024f, 0f, 640f), frame);
            testNavigation.Arrange(new InsightRect(0f, 0f, 1024f, 640f), frame);
            Require(!testNavigation.IsCompact && testNavigation.MeasuredSize.Width > 0f,
                "wide navigation did not arrange a side rail.");
            testNavigation.Measure(new InsightUiConstraints(0f, 420f, 0f, 640f), frame);
            testNavigation.Arrange(new InsightRect(0f, 0f, 420f, 640f), frame);
            Require(testNavigation.IsCompact && testNavigation.MeasuredSize.Height > 0f,
                "narrow navigation did not arrange a compact top bar.");
            return "wide side rail and narrow compact navigation both arranged";
        }

        private static string CheckCompositeResponsiveLayout()
        {
            InsightUiSectionHeader header = InsightUi.SectionHeader("feature-showcase-section", "Storage",
                "Configure stockpile behavior", InsightUiIcon.FromText("S"),
                InsightUi.Button("feature-showcase-section-action", "Reset"), true);
            InsightUiElement[] elements =
            {
                InsightUi.Callout("feature-showcase-callout", InsightUiCalloutSeverity.Warning, "Power reserve",
                    "Stored energy is low.").SetIcon(InsightUiIcon.FromText("!")),
                header,
                InsightUi.Meter("feature-showcase-meter", 620f, 1000f).SetLabel("Stored power")
                    .SetValueText("620 / 1000 Wd"),
                InsightUi.StatRow("feature-showcase-stat", "Stored power", "620 / 1000 Wd")
                    .SetSecondary("Workshop reserve").SetIcon(InsightUiIcon.FromText("P"))
            };
            float[] widths = { 640f, 260f };
            for (int widthIndex = 0; widthIndex < widths.Length; widthIndex++)
            {
                float width = widths[widthIndex];
                InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default,
                    width < 420f ? InsightUiDensity.Compact : InsightUiDensity.Comfortable, false, false,
                    new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
                for (int elementIndex = 0; elementIndex < elements.Length; elementIndex++)
                {
                    InsightUiElement element = elements[elementIndex];
                    element.Measure(new InsightUiConstraints(0f, width, 0f, 320f), frame);
                    element.Arrange(new InsightRect(0f, 0f, width, 320f), frame);
                    AssertFiniteGeometry(element);
                }
                Require((width < 420f) == (FindElement(header, "feature-showcase-section.narrow-root") != null),
                    "section header did not select its responsive composition.");
            }
            return "composite components retained finite geometry at wide and narrow widths";
        }

        private static string CheckVirtualizationBounds()
        {
            InsightVirtualizedRange visible = InsightVirtualization.Range(400, 32f, 240f, 640f, 2);
            Require(visible.Contains(20) && visible.End < 400 && visible.Start >= 0,
                "virtualized range was outside its bounded collection.");
            Require(Math.Abs(InsightVirtualization.ContentHeight(400, 32f) - 12800f) < 0.001f,
                "virtualized content height was not deterministic.");
            return "range and content height stayed within collection bounds";
        }

        private static string CheckDocumentStateIsolation()
        {
            InsightUiDocument first = new InsightUiDocument("feature-showcase-first", InsightUi.Empty("root"));
            InsightUiDocument second = new InsightUiDocument("feature-showcase-second", InsightUi.Empty("root"));
            first.State.SetBool("selected", true);
            first.Density = InsightUiDensity.Compact;
            first.HighContrast = true;
            first.ReducedMotion = true;
            Require(first.State.GetBool("selected") && !second.State.GetBool("selected") &&
                second.Density == InsightUiDensity.Normal && !second.HighContrast && !second.ReducedMotion,
                "composable UI state or accessibility settings leaked between documents.");
            return "document state and accessibility settings remained isolated";
        }

        private static string CheckMapActionAvailable()
        {
            InsightMapReference center = InsightMapBridge.ForCell(Find.CurrentMap, Find.CurrentMap.Center);
            InsightAction flash = InsightMapBridge.Flash("feature-showcase-flash", center, 1f);
            Require(flash.Enabled, "map flash action was not enabled for the loaded map.");
            return "current-map flash action is enabled";
        }

        private static bool PageHasInteraction(string pageId)
        {
            return pageId == "layout" || pageId == "controls" || pageId == "workspaces" || pageId == "motion" ||
                pageId == "themes" || pageId == "advanced" || pageId == "diagnostics";
        }

        private static void AssertFiniteGeometry(InsightUiElement element)
        {
            if (element == null) return;
            InsightRect rect = element.LayoutRect;
            InsightUiSize size = element.MeasuredSize;
            Require(IsFinite(rect.X) && IsFinite(rect.Y) && IsFinite(rect.Width) && IsFinite(rect.Height) &&
                rect.Width >= -0.01f && rect.Height >= -0.01f && IsFinite(size.Width) && IsFinite(size.Height) &&
                size.Width >= -0.01f && size.Height >= -0.01f,
                "element '" + element.Id + "' had invalid measured or arranged geometry.");
            IReadOnlyList<InsightUiElement> children = element.Children;
            for (int i = 0; i < children.Count; i++) AssertFiniteGeometry(children[i]);
        }

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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
