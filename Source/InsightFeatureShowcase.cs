using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace InsightCanvas
{
    /// <summary>
    /// The installed, public-API-only demonstration application for Insight Canvas.
    /// Every page is composed from InsightUi elements so it also serves as an integration reference.
    /// </summary>
    public static class InsightFeatureShowcase
    {
        public static InsightUiWindow CreateWindow()
        {
            FeatureShowcaseDocument application = new FeatureShowcaseDocument();
            return new InsightUiWindow("Feature Showcase", application.Document);
        }

        private sealed class FeatureShowcaseDocument
        {
            private readonly IReadOnlyList<InsightShowcaseRecord> records;
            private readonly List<int> filteredRecords = new List<int>();
            private readonly List<string> selectedRecords = new List<string>();
            private readonly InsightModel optionalModel;
            private readonly InsightGraphLayoutResult graphResult;
            private readonly IReadOnlyList<InsightTimelineCluster> timelineClusters;
            private InsightUiNavigation navigation;
            private InsightUiVirtualList recordList;
            private InsightUiLabel filterEmptyState;
            private InsightUiLabel comparisonLabel;
            private InsightUiLabel mapStatus;
            private InsightUiLabel interactionStatus;
            private InsightUiLabel themeStatus;
            private InsightUiLabel motionStatus;
            private InsightUiProgress motionProgress;
            private InsightUiExpander overviewInspector;
            private InsightUiExpander motionReveal;
            private InsightUiSlideFade motionSlide;
            private InsightUiHoverCard controlsHoverCard;
            private InsightUiElement layoutPreview;
            private float simulatedWidth = 560f;
            private float motionTarget = 0.42f;
            private string themeName = "RimWorld+";

            public FeatureShowcaseDocument()
            {
                Document = new InsightUiDocument("feature-showcase", null);
                records = InsightFeatureShowcaseData.CreateRecords();
                optionalModel = InsightShowcaseData.CreateDemoModel();
                InsightModelSnapshot snapshot = optionalModel.Snapshot();
                graphResult = InsightGraphLayout.Compute(snapshot, 640f, 260f, 180, 360, 18);
                timelineClusters = InsightTimelineMath.Cluster(snapshot.Events, InsightTimelineMath.Bounds(snapshot.Events), 640f, 8);
                for (int i = 0; i < records.Count; i++) filteredRecords.Add(i);
                Document.Root = BuildRoot();
            }

            public InsightUiDocument Document { get; private set; }

            private InsightUiElement BuildRoot()
            {
                navigation = InsightUi.Navigation("showcase-navigation", 760f);
                navigation.Add("overview", "Overview", Page("overview", "Overview", "A framework for making RimWorld interfaces clearer, calmer, and more useful.", BuildOverview()));
                navigation.Add("foundations", "Foundations", Page("foundations", "Foundations", "Tokens and primitives keep a family of mod interfaces visually coherent.", BuildFoundations()));
                navigation.Add("layout", "Layout", Page("layout", "Layout", "Measure, arrange, and paint adapt to the space a caller actually provides.", BuildLayout()));
                navigation.Add("controls", "Controls", Page("controls", "Controls", "Interactive elements expose ordinary callbacks and document-scoped state.", BuildControls()));
                navigation.Add("workspaces", "Navigation", Page("workspaces", "Navigation and Workspaces", "Compose navigation, tabs, tools, and inspector panes without a fixed dashboard.", BuildWorkspaces()));
                navigation.Add("data", "Data Display", Page("data", "Data Display", "Deterministic records demonstrate filtering, comparison, metrics, and virtualization.", BuildDataDisplay()));
                navigation.Add("motion", "Motion", Page("motion", "Motion and Feedback", "Short, interruptible transitions communicate change without competing with play.", BuildMotion()));
                navigation.Add("themes", "Themes", Page("themes", "Themes and Accessibility", "Theme, density, contrast, and motion preferences stay inside this document.", BuildThemes()));
                navigation.Add("advanced", "Advanced", Page("advanced", "Advanced Widgets", "Semantic visualizations remain optional widgets for mods that need them.", BuildAdvanced()));
                navigation.Add("diagnostics", "Diagnostics", Page("diagnostics", "Diagnostics", "See the current render health and responsive state while building a consumer.", BuildDiagnostics()));

                InsightUiStack root = InsightUi.Column("showcase-root").SetGap(8f);
                root.SetPadding(10f);
                root.Add(BuildHeader(), navigation.SetFlex(1f), InsightUi.Toast("showcase-toast"));
                return root;
            }

            private InsightUiElement BuildHeader()
            {
                InsightUiStack header = InsightUi.Row("showcase-header").SetGap(10f);
                header.SetPadding(8f, 6f);
                header.Style.Elevated = true;
                InsightUiLabel title = InsightUi.Label("showcase-title", "Feature Showcase", InsightUiTextStyle.Title);
                InsightUiLabel subtitle = InsightUi.Label("showcase-subtitle", "Composable RimWorld UI, in context", InsightUiTextStyle.Caption);
                InsightUiStack titleBlock = InsightUi.Column("showcase-title-block").SetGap(1f);
                titleBlock.Add(title, subtitle);
                InsightUiLabel page = InsightUi.Label("showcase-page", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => navigation == null ? string.Empty : "Viewing " + navigation.ActivePageId);
                page.Color = InsightTheme.Default.SecondaryText;
                InsightUiBadge badge = InsightUi.Badge("showcase-v2", "v2", InsightTheme.Default.Selected);
                InsightUiButton reset = InsightUi.IconButton("showcase-reset", "↺", Reset);
                reset.SetTooltip("Reset this showcase document");
                header.Add(titleBlock, page, InsightUi.Spacer("header-spacer", 0f, 1f).SetFlex(1f), badge, reset);
                return header;
            }

            private InsightUiElement Page(string id, string title, string subtitle, InsightUiElement content)
            {
                InsightUiStack body = InsightUi.Column(id + ".body").SetGap(10f);
                body.Add(InsightUi.Breadcrumbs(id + ".crumbs", "Feature Showcase", title),
                    InsightUi.Label(id + ".title", title, InsightUiTextStyle.Heading),
                    InsightUi.Label(id + ".subtitle", subtitle, InsightUiTextStyle.Body), content);
                InsightUiScroll scroll = InsightUi.Scroll(id + ".scroll", body);
                scroll.SetFlex(1f);
                return scroll;
            }

            private InsightUiElement BuildOverview()
            {
                InsightUiStack column = InsightUi.Column("overview-content").SetGap(10f);
                InsightUiStack hero = InsightUi.Column("overview-hero").SetGap(8f);
                hero.Add(InsightUi.Badge("overview-badge", "RimWorld+ UI toolkit", InsightTheme.Default.Selected),
                    InsightUi.Label("overview-copy", "Insight Canvas gives mod authors a shared visual language without taking over vanilla windows. Build a document once, embed it in a Rect or open it as a normal Window.", InsightUiTextStyle.Body),
                    InsightUi.Label("overview-rule", "Opt in by composition; keep ownership of your screen.", InsightUiTextStyle.Caption));
                column.Add(InsightUi.Highlight("overview-hero-highlight", Card("overview-hero-card", hero)));

                InsightUiGrid cards = InsightUi.Grid("overview-cards", 190f);
                cards.Add(Card("overview-card-layout", InsightUi.Column("overview-card-layout-body").SetGap(6f).Add(
                        InsightUi.Label("overview-layout-title", "Responsive composition", InsightUiTextStyle.Heading),
                        InsightUi.Label("overview-layout-copy", "Rows, grids, splits, scrolling, and min/max sizing respond to the caller's Rect.", InsightUiTextStyle.Body),
                        InsightUi.Badge("overview-layout-badge", "measure → arrange → paint", InsightTheme.Default.Positive))),
                    Card("overview-card-state", InsightUi.Column("overview-card-state-body").SetGap(6f).Add(
                        InsightUi.Label("overview-state-title", "Stable state", InsightUiTextStyle.Heading),
                        InsightUi.Label("overview-state-copy", "Selection, tabs, expansion, focus keys, and scroll offsets belong to each document.", InsightUiTextStyle.Body),
                        InsightUi.Badge("overview-state-badge", "no cross-window leaks", InsightTheme.Default.Selected))),
                    Card("overview-card-access", InsightUi.Column("overview-card-access-body").SetGap(6f).Add(
                        InsightUi.Label("overview-access-title", "Scoped polish", InsightUiTextStyle.Heading),
                        InsightUi.Label("overview-access-copy", "Warm charcoal surfaces, restrained depth, density, contrast, and reduced motion are explicit choices.", InsightUiTextStyle.Body),
                        InsightUi.Badge("overview-access-badge", "RimWorld+ default", InsightTheme.Default.Warning))));
                column.Add(cards);

                InsightUiSectionHeader compositeHeader = InsightUi.SectionHeader("overview-composite-header",
                    "Persistent composition", "Small composites for common RimWorld information surfaces.",
                    InsightUiIcon.FromText("◆").WithAccessibleDescription("Composition examples"));
                InsightUiStatRow powerStat = InsightUi.StatRow("overview-power-stat", "Stored power", "620 / 1000 Wd")
                    .SetSecondary("Workshop reserve").SetIcon(InsightUiIcon.FromText("⚡"))
                    .SetValueColor(InsightTheme.Default.Positive)
                    .SetTooltip("A compact key/value row can live inside any card or inspector.");
                InsightUiCallout notice = InsightUi.Callout("overview-power-callout", InsightUiCalloutSeverity.Warning,
                    "Insufficient power", "This workbench will stop when stored energy is exhausted.")
                    .SetIcon(InsightUiIcon.FromText("!").WithAccessibleDescription("Warning"))
                    .SetContent(powerStat)
                    .SetActions(InsightUi.Row("overview-callout-actions").SetGap(7f).Add(
                        InsightUi.Button("overview-callout-inspect", "Inspect reserve", () => SetInteraction("Reserve inspector opened"))));
                InsightUiMeter reserveMeter = InsightUi.Meter("overview-reserve-meter", 620f, 1000f)
                    .SetLabel("Stored power").SetValueText("620 / 1000 Wd").SetColor(InsightTheme.Default.Warning);
                column.Add(compositeHeader, notice, reserveMeter);

                overviewInspector = InsightUi.Expander("overview-inspector", "Expand a compact card into a richer inspector",
                    Card("overview-inspector-content", InsightUi.Column("overview-inspector-body").SetGap(6f).Add(
                        InsightUi.Label("overview-inspector-heading", "The same surface can become a working view.", InsightUiTextStyle.Heading),
                        InsightUi.Label("overview-inspector-copy", "Use stable IDs for selection and disclosure, then let the caller decide when a deep dive is appropriate.", InsightUiTextStyle.Body),
                        InsightUi.Progress("overview-inspector-progress", 0.76f, InsightTheme.Default.Positive),
                        InsightUi.Badge("overview-inspector-status", "ready for integration", InsightTheme.Default.Positive))), false);
                column.Add(overviewInspector);

                InsightUiStack actions = InsightUi.Row("overview-actions").SetGap(8f);
                actions.Add(InsightUi.Button("overview-layout-action", "Explore layout", () => GoTo("layout")),
                    InsightUi.Button("overview-controls-action", "Try controls", () => GoTo("controls")),
                    InsightUi.Button("overview-map-action", "Trigger map link", TriggerMapAction),
                    InsightUi.Button("overview-toast-action", "Show feedback", () =>
                    {
                        Document.Toasts.Show("Saved to this document", InsightToastSeverity.Success);
                        Document.Effects.Flash("overview-hero-highlight");
                        SetInteraction("Toast and highlight feedback shown");
                    }));
                mapStatus = InsightUi.Label("overview-map-status", string.Empty, InsightUiTextStyle.Caption);
                mapStatus.SetTextProvider(() => CurrentMapStatus());
                column.Add(actions, mapStatus);
                return column;
            }

            private InsightUiElement BuildFoundations()
            {
                InsightUiStack column = InsightUi.Column("foundations-content").SetGap(10f);
                InsightUiGrid colors = InsightUi.Grid("foundations-colors", 150f);
                colors.Add(Swatch("foundation-background", "Background", InsightTheme.Default.Background),
                    Swatch("foundation-surface", "Surface", InsightTheme.Default.Surface),
                    Swatch("foundation-elevated", "Elevated", InsightTheme.Default.ElevatedSurface),
                    Swatch("foundation-selected", "Selected", InsightTheme.Default.Selected),
                    Swatch("foundation-positive", "Positive", InsightTheme.Default.Positive),
                    Swatch("foundation-warning", "Warning", InsightTheme.Default.Warning),
                    Swatch("foundation-negative", "Negative", InsightTheme.Default.Negative));
                InsightUiStack type = InsightUi.Column("foundations-type").SetGap(4f);
                type.Add(InsightUi.Label("foundation-title", "Title hierarchy", InsightUiTextStyle.Title),
                    InsightUi.Label("foundation-heading", "Heading hierarchy", InsightUiTextStyle.Heading),
                    InsightUi.Label("foundation-body", "Body text stays readable at normal UI scale.", InsightUiTextStyle.Body),
                    InsightUi.Label("foundation-caption", "Caption · secondary context · 12px intent", InsightUiTextStyle.Caption));
                InsightUiSectionHeader primitivesHeader = InsightUi.SectionHeader("foundation-primitives-header",
                    "Surface primitives", "Cards, notices, metrics, and status rows share the same theme tokens.",
                    InsightUiIcon.FromText("◈"), divider: true);
                InsightUiGrid primitives = InsightUi.Grid("foundations-primitives", 190f);
                primitives.Add(Card("foundation-surface-card", InsightUi.Column("foundation-surface-body").SetGap(6f).Add(
                        InsightUi.Label("foundation-card-title", "Surface", InsightUiTextStyle.Heading),
                        InsightUi.Label("foundation-card-copy", "Surfaces establish grouping without ornamental noise.", InsightUiTextStyle.Body))),
                    Card("foundation-status-card", InsightUi.Column("foundation-status-body").SetGap(6f).Add(
                        InsightUi.Label("foundation-status-title", "Semantic status", InsightUiTextStyle.Heading),
                        InsightUi.Row("foundation-status-row").SetGap(5f).Add(
                            InsightUi.Badge("foundation-ready", "Ready", InsightTheme.Default.Positive),
                            InsightUi.Badge("foundation-review", "Review", InsightTheme.Default.Warning),
                            InsightUi.Badge("foundation-blocked", "Blocked", InsightTheme.Default.Negative)))),
                    Card("foundation-density-card", InsightUi.Column("foundation-density-body").SetGap(6f).Add(
                        InsightUi.Label("foundation-density-title", "Density", InsightUiTextStyle.Heading),
                        InsightUi.Label("foundation-density-copy", "Comfortable, normal, and compact density are document settings, not global skin edits.", InsightUiTextStyle.Body),
                        InsightUi.Row("foundation-icon-row").SetGap(8f).Add(
                            InsightUi.Icon("foundation-icon", InsightUiIcon.FromText("◈").WithTooltip("Text fallback icon")),
                            InsightUi.Image("foundation-image", null, 28f, 28f, "IMG")))));
                InsightUiSurface themeRadius = InsightUi.Surface("foundation-theme-radius", InsightUi.Column("foundation-theme-radius-body").SetGap(4f).Add(
                    InsightUi.Label("foundation-theme-radius-title", "Theme radius", InsightUiTextStyle.Heading),
                    InsightUi.Label("foundation-theme-radius-copy", "Uses the document theme corner radius.", InsightUiTextStyle.Caption)));
                themeRadius.SetCornerRadius(-1f);
                themeRadius.Style.Elevated = false;
                InsightUiSurface rounded = InsightUi.Surface("foundation-rounded-radius", InsightUi.Column("foundation-rounded-radius-body").SetGap(4f).Add(
                    InsightUi.Label("foundation-rounded-radius-title", "Rounded override", InsightUiTextStyle.Heading),
                    InsightUi.Label("foundation-rounded-radius-copy", "An explicit 8 px radius.", InsightUiTextStyle.Caption)));
                rounded.SetCornerRadius(8f);
                InsightUiSurface square = InsightUi.Surface("foundation-square-radius", InsightUi.Column("foundation-square-radius-body").SetGap(4f).Add(
                    InsightUi.Label("foundation-square-radius-title", "Square override", InsightUiTextStyle.Heading),
                    InsightUi.Label("foundation-square-radius-copy", "An explicit zero radius.", InsightUiTextStyle.Caption)));
                square.SetCornerRadius(0f);
                square.Style.Elevated = false;
                InsightUiGrid radiusExamples = InsightUi.Grid("foundation-radius-examples", 190f).Add(themeRadius, rounded, square);
                column.Add(type, primitivesHeader, InsightUi.Divider("foundation-divider"), colors, primitives,
                    InsightUi.Label("foundation-radius-note", "Surfaces share one restrained radius contract: theme fallback, element override, or square.", InsightUiTextStyle.Caption),
                    radiusExamples);
                return column;
            }

            private InsightUiElement BuildLayout()
            {
                InsightUiStack column = InsightUi.Column("layout-content").SetGap(10f);
                InsightUiLabel widthLabel = InsightUi.Label("layout-width-label", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => "Simulated preview width: " + ((int)simulatedWidth) + " px");
                InsightUiSlider widthSlider = InsightUi.Slider("layout-width", simulatedWidth, 260f, 760f, value =>
                {
                    simulatedWidth = value;
                    layoutPreview?.SetWidth(InsightLength.Fixed(simulatedWidth));
                    Document.Invalidate();
                });
                layoutPreview = Card("layout-preview", InsightUi.Column("layout-preview-body").SetGap(8f).Add(
                    InsightUi.Label("layout-preview-title", "Nested, wrapped composition", InsightUiTextStyle.Heading),
                    InsightUi.Wrap("layout-wrap", InsightUi.Badge("layout-wrap-one", "Card", InsightTheme.Default.Selected),
                        InsightUi.Badge("layout-wrap-two", "Wrap", InsightTheme.Default.Positive),
                        InsightUi.Badge("layout-wrap-three", "Grid", InsightTheme.Default.Warning),
                        InsightUi.Badge("layout-wrap-four", "Split", InsightTheme.Default.Focus)).SetGap(6f),
                    InsightUi.Label("layout-preview-note", "Drag the width slider. The preview has an explicit min/max-aware width and wraps without a four-panel assumption.", InsightUiTextStyle.Body)));
                layoutPreview.SetAlignment(InsightAlignment.Start, InsightAlignment.Start).SetWidth(InsightLength.Fixed(simulatedWidth));
                column.Add(Card("layout-simulation", InsightUi.Column("layout-simulation-body").SetGap(6f).Add(
                        InsightUi.Label("layout-simulation-title", "Width simulation", InsightUiTextStyle.Heading), widthSlider, widthLabel)), layoutPreview);

                InsightUiGrid adaptive = InsightUi.Grid("layout-adaptive-grid", 170f);
                for (int i = 0; i < 6; i++) adaptive.Add(Card("layout-grid-card-" + i,
                    InsightUi.Column("layout-grid-body-" + i).SetGap(4f).Add(
                        InsightUi.Label("layout-grid-title-" + i, "Adaptive card " + (i + 1), InsightUiTextStyle.Heading),
                        InsightUi.Label("layout-grid-copy-" + i, i % 2 == 0 ? "Wide enough to breathe." : "Moves to the next row gracefully.", InsightUiTextStyle.Caption))));
                InsightUiSplit split = InsightUi.Split("layout-split",
                    Card("layout-split-left", InsightUi.Column("layout-split-left-body").SetGap(5f).Add(
                        InsightUi.Label("layout-split-left-title", "Inspector pane", InsightUiTextStyle.Heading),
                        InsightUi.Label("layout-split-left-copy", "A split is just another public element. Its ratio is clamped and its panes remain independently composable.", InsightUiTextStyle.Body))),
                    ScrollSample("layout-split-right", 10), 0.38f);
                split.Draggable = true;
                column.Add(InsightUi.Label("layout-adaptive-title", "Adaptive grid", InsightUiTextStyle.Heading), adaptive,
                    InsightUi.Label("layout-split-title", "Draggable split pane with scrolling", InsightUiTextStyle.Heading), split);
                return column;
            }

            private InsightUiElement BuildControls()
            {
                InsightUiStack column = InsightUi.Column("controls-content").SetGap(10f);
                interactionStatus = InsightUi.Label("controls-status", "No control activated yet.", InsightUiTextStyle.Caption);
                InsightUiToggle highContrast = InsightUi.Toggle("controls-contrast", "High contrast", Document.HighContrast,
                    value => { Document.HighContrast = value; SetInteraction("High contrast " + (value ? "enabled" : "disabled")); Document.Invalidate(); });
                InsightUiToggle reducedMotion = InsightUi.Toggle("controls-motion", "Reduced motion", Document.ReducedMotion,
                    value => { Document.ReducedMotion = value; SetInteraction("Reduced motion " + (value ? "enabled" : "disabled")); Document.Invalidate(); });
                InsightUiTextField textField = InsightUi.TextField("controls-text", "Try typing here", value => SetInteraction("Text input: " + value));
                InsightUiSelect selector = InsightUi.Select("controls-selector", "Mode", new[] { "Observe", "Plan", "Act" }, 0,
                    (index, value) => SetInteraction("Selector changed to " + value));
                InsightUiDropdown dropdown = InsightUi.Dropdown("controls-dropdown", "Priority",
                    new[] { "Low", "Normal", "Urgent" }, 1, (index, value) => SetInteraction("Dropdown changed to " + value));
                InsightUiSegmented segmented = InsightUi.Segmented("controls-segmented",
                    new[] { "Draft", "Review", "Live" }, 1, (index, value) => SetInteraction("Segment selected: " + value));
                InsightUiStack settings = InsightUi.Column("controls-settings").SetGap(7f);
                 InsightUiButton overflowButton = InsightUi.Button("controls-overflow-trigger", "Open popover",
                     () => SetInteraction("Popover action selected"));
                InsightUiPopover overflow = InsightUi.Popover("controls-overflow", overflowButton,
                    Card("controls-overflow-card", InsightUi.Column("controls-overflow-body").SetGap(5f).Add(
                        InsightUi.Label("controls-overflow-title", "Context menu", InsightUiTextStyle.Heading),
                        InsightUi.Button("controls-overflow-copy", "Copy summary", () =>
                        {
                            Document.Toasts.Show("Summary copied", InsightToastSeverity.Success);
                            SetInteraction("Popover action: copied summary");
                         }),
                         InsightUi.Button("controls-overflow-dismiss", "Dismiss", () => SetInteraction("Popover dismissed")))));
                controlsHoverCard = InsightUi.HoverCard("controls-hover-card",
                    InsightUi.Label("controls-hover-trigger", "Hover for context", InsightUiTextStyle.Caption)
                        .SetTooltip("Brief context appears after a short hover delay"),
                    InsightUi.Column("controls-hover-content").SetGap(4f).Add(
                        InsightUi.Label("controls-hover-title", "Context card", InsightUiTextStyle.Heading),
                        InsightUi.Label("controls-hover-copy", "Display-only help can stay near its trigger without taking focus or changing layout.", InsightUiTextStyle.Body)));
                settings.Add(highContrast, reducedMotion, selector, dropdown, segmented, textField, overflow,
                    controlsHoverCard, interactionStatus);

                InsightUiButton disabled = InsightUi.Button("controls-disabled", "Disabled action", () => SetInteraction("This should not run"));
                disabled.Enabled = false;
                InsightUiButton selected = InsightUi.Button("controls-selected", "Selected action", () => SetInteraction("Selected action activated"));
                selected.Selected = true;
                InsightUiButton warning = InsightUi.Button("controls-warning", "Review warning", () => SetInteraction("Warning action activated"));
                warning.SetBackground(InsightTheme.Default.Warning.WithAlpha(0.26f));
                InsightUiButton destructive = InsightUi.Button("controls-destructive", "Destructive action", () => SetInteraction("Destructive action requires confirmation"));
                destructive.SetBackground(InsightTheme.Default.Negative.WithAlpha(0.26f));
                InsightUiStack actions = InsightUi.Column("controls-actions").SetGap(7f);
                actions.Add(InsightUi.Row("controls-action-row").SetGap(7f).Add(
                        InsightUi.Button("controls-primary", "Primary", () => SetInteraction("Primary action activated")),
                        InsightUi.IconButton("controls-icon", "★", () => SetInteraction("Icon action activated")),
                        disabled), selected, warning, destructive);

                InsightUiExpander expander = InsightUi.Expander("controls-expander", "Expand advanced control states",
                    InsightUi.Column("controls-expander-content").SetGap(7f).Add(
                        InsightUi.Label("controls-expander-copy", "Expansion is stateful and isolated to this document.", InsightUiTextStyle.Body),
                        InsightUi.Toggle("controls-toggle", "A nested toggle", false, value => SetInteraction("Nested toggle: " + value)),
                        InsightUi.Progress("controls-progress", 0.63f, InsightTheme.Default.Selected)), false);
                column.Add(Card("controls-settings-card", settings), Card("controls-actions-card", actions), expander);
                return column;
            }

            private InsightUiElement BuildWorkspaces()
            {
                InsightUiStack column = InsightUi.Column("workspaces-content").SetGap(10f);
                InsightUiBreadcrumbs crumbs = InsightUi.Breadcrumbs("workspace-breadcrumbs", "Colony", "Knowledge", "Current workspace");
                InsightUiTextField command = InsightUi.TextField("workspace-command", string.Empty, value =>
                    SetInteraction(string.IsNullOrWhiteSpace(value) ? "Command palette ready" : "Command query: " + value));
                InsightUiStack toolbar = InsightUi.Row("workspace-toolbar").SetGap(7f);
                toolbar.Add(InsightUi.Button("workspace-refresh", "Refresh", () => SetInteraction("Workspace refreshed")),
                    InsightUi.Button("workspace-inspect", "Inspect", () => GoTo("diagnostics")),
                    InsightUi.IconButton("workspace-command-icon", "⌕", () => SetInteraction("Command palette focused")), command);

                InsightUiTabs tabs = InsightUi.Tabs("workspace-tabs");
                tabs.Add("board", "Board", Card("workspace-board", InsightUi.Column("workspace-board-body").SetGap(6f).Add(
                        InsightUi.Label("workspace-board-title", "Board view", InsightUiTextStyle.Heading),
                        InsightUi.Label("workspace-board-copy", "Tabs are useful inside a page while the outer navigation handles workspace scale.", InsightUiTextStyle.Body),
                        InsightUi.Badge("workspace-board-badge", "active", InsightTheme.Default.Positive))))
                    .Add("inspector", "Inspector", Card("workspace-inspector", InsightUi.Column("workspace-inspector-body").SetGap(6f).Add(
                        InsightUi.Label("workspace-inspector-title", "Inspector pane", InsightUiTextStyle.Heading),
                        InsightUi.Label("workspace-inspector-copy", "Selection and detail panes can be composed without requiring a semantic model.", InsightUiTextStyle.Body),
                        InsightUi.Expander("workspace-inspector-expand", "Reveal metadata", InsightUi.Label("workspace-meta", "Stable IDs make this pane safe to reuse."), true))))
                    .Add("compare", "Compare", ComparisonCard("workspace-comparison"));
                InsightUiSplit comparison = InsightUi.Split("workspace-split", tabs, Card("workspace-side-inspector",
                    InsightUi.Column("workspace-side-body").SetGap(6f).Add(
                        InsightUi.Label("workspace-side-title", "Context inspector", InsightUiTextStyle.Heading),
                        InsightUi.Label("workspace-side-copy", "Side panes can collapse into the compact navigation mode at smaller widths.", InsightUiTextStyle.Body),
                        InsightUi.Badge("workspace-side-status", "scoped", InsightTheme.Default.Selected))), 0.68f);
                column.Add(crumbs, toolbar, comparison);
                return column;
            }

            private InsightUiElement BuildDataDisplay()
            {
                InsightUiStack column = InsightUi.Column("data-content").SetGap(10f);
                InsightUiSearchField search = InsightUi.SearchField("data-search", string.Empty, "Filter records", ApplyFilter);
                search.SetTooltip("Filter deterministic records by name, group, or tag");
                InsightUiStack searchRow = InsightUi.Row("data-search-row").SetGap(8f);
                searchRow.Add(InsightUi.Label("data-search-label", "Search records", InsightUiTextStyle.Heading), search);
                filterEmptyState = InsightUi.Label("data-empty", string.Empty, InsightUiTextStyle.Caption);
                filterEmptyState.SetTextProvider(() => filteredRecords.Count == 0 ? "No records match. Clear the search to restore the list." :
                    filteredRecords.Count + " records · select two for comparison");
                recordList = InsightUi.VirtualList("data-virtual-list", filteredRecords.Count, 42f, CreateRecordRow);
                recordList.Overscan = 3;
                recordList.CacheLimit = 48;
                InsightUiSurface listCard = InsightUi.Surface("data-list-card", InsightUi.Column("data-list-body").SetGap(7f).Add(
                    searchRow, filterEmptyState, recordList));
                listCard.SetPadding(10f);
                InsightUiGrid placeholders = InsightUi.Grid("data-loading-placeholders", 150f);
                for (int i = 0; i < 3; i++)
                    placeholders.Add(Card("data-loading-card-" + i, InsightUi.Column("data-loading-body-" + i).SetGap(5f).Add(
                        InsightUi.Label("data-loading-label-" + i, "Loading preview", InsightUiTextStyle.Caption),
                        InsightUi.Progress("data-loading-progress-" + i, 0.32f + i * 0.12f, InsightTheme.Default.SecondaryText))));
                column.Add(listCard, ComparisonCard("data-comparison"),
                    InsightUi.Label("data-table-heading", "Structured metrics", InsightUiTextStyle.Heading), MetricsTable(),
                    InsightUi.Label("data-loading-heading", "Loading-like placeholders", InsightUiTextStyle.Heading), placeholders);
                return column;
            }

            private InsightUiElement BuildMotion()
            {
                InsightUiStack column = InsightUi.Column("motion-content").SetGap(10f);
                motionStatus = InsightUi.Label("motion-status", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => "Motion target " + ((int)(motionTarget * 100f)) + "% · " + (Document.ReducedMotion ? "reduced motion" : "smooth transition"));
                motionProgress = InsightUi.Progress("motion-progress", motionTarget, InsightTheme.Default.Selected);
                InsightUiButton advance = InsightUi.Button("motion-advance", "Reveal next state", () =>
                {
                     motionTarget = motionTarget >= 0.99f ? 0.18f : Math.Min(0.99f, motionTarget + 0.18f);
                     motionProgress.Value = motionTarget;
                     motionSlide.VisibleTarget = !motionSlide.VisibleTarget;
                     Document.Effects.Flash("motion-progress-highlight");
                    Document.Invalidate();
                });
                InsightUiButton select = InsightUi.Button("motion-select", "Select milestone", () => SetInteraction("Milestone selected with a short reveal"));
                 motionReveal = InsightUi.Expander("motion-reveal", "Expand reveal panel",
                    InsightUi.Column("motion-reveal-body").SetGap(6f).Add(
                        InsightUi.Label("motion-reveal-copy", "Expansion, selection, and progress changes use the same document frame and remain interruptible.", InsightUiTextStyle.Body),
                         InsightUi.Badge("motion-reveal-badge", "100–200 ms intent", InsightTheme.Default.Positive)), false);
                motionSlide = InsightUi.SlideFade("motion-slide-fade", false,
                    Card("motion-slide-card", InsightUi.Column("motion-slide-body").SetGap(5f).Add(
                        InsightUi.Label("motion-slide-title", "SlideFade detail", InsightUiTextStyle.Heading),
                        InsightUi.Label("motion-slide-copy", "A short paint-only reveal keeps its final layout slot while the content settles into place.", InsightUiTextStyle.Caption),
                        InsightUi.Badge("motion-slide-badge", "6 px · 160 ms", InsightTheme.Default.Selected))),
                    InsightUiSlideDirection.Down);
                InsightUiToggle toggle = InsightUi.Toggle("motion-reduced-toggle", "Reduced motion", Document.ReducedMotion, value =>
                {
                    Document.ReducedMotion = value;
                    Document.Invalidate();
                });
                InsightUiHighlight motionProgressHighlight = InsightUi.Highlight("motion-progress-highlight",
                    motionProgress, InsightTheme.Default.Focus);
                 column.Add(Card("motion-demo-card", InsightUi.Column("motion-demo-body").SetGap(8f).Add(
                     InsightUi.Label("motion-demo-title", "Feedback without noise", InsightUiTextStyle.Heading), motionStatus, motionProgressHighlight,
                     InsightUi.Row("motion-buttons").SetGap(7f).Add(advance, select), toggle)), motionReveal, motionSlide);
                return column;
            }

            private InsightUiElement BuildThemes()
            {
                InsightUiStack column = InsightUi.Column("themes-content").SetGap(10f);
                themeStatus = InsightUi.Label("themes-status", string.Empty, InsightUiTextStyle.Caption)
                    .SetTextProvider(() => themeName + " · radius " + Document.Theme.CornerRadius.ToString("0.#") + " · " +
                        Document.Density + " density · " + (Document.HighContrast ? "high contrast" : "standard contrast"));
                InsightUiStack themeButtons = InsightUi.Row("themes-buttons").SetGap(7f);
                themeButtons.Add(InsightUi.Button("themes-rimworld", "RimWorld+", () => ApplyTheme("RimWorld+")),
                    InsightUi.Button("themes-field", "Field Notes", () => ApplyTheme("Field Notes")),
                    InsightUi.Button("themes-night", "Night Watch", () => ApplyTheme("Night Watch")));
                InsightUiSelect density = InsightUi.Select("themes-density", "Density", new[] { "Comfortable", "Normal", "Compact" }, 1,
                    (index, value) => { Document.Density = (InsightUiDensity)index; Document.Invalidate(); });
                InsightUiToggle contrast = InsightUi.Toggle("themes-contrast", "High contrast", Document.HighContrast, value =>
                {
                    Document.HighContrast = value;
                    Document.Invalidate();
                });
                InsightUiToggle reduced = InsightUi.Toggle("themes-reduced", "Reduced motion", Document.ReducedMotion, value =>
                {
                    Document.ReducedMotion = value;
                    Document.Invalidate();
                });
                InsightUiGrid swatches = InsightUi.Grid("themes-swatch-grid", 180f).Add(
                    Swatch("themes-surface", "Surface token", Document.Theme.Surface),
                    Swatch("themes-accent", "Accent token", Document.Theme.Selected),
                    Swatch("themes-positive", "Positive token", Document.Theme.Positive),
                    Swatch("themes-warning", "Warning token", Document.Theme.Warning));
                column.Add(Card("themes-settings-card", InsightUi.Column("themes-settings-body").SetGap(7f).Add(
                    themeStatus, themeButtons, density, contrast, reduced)), swatches,
                    InsightUi.Label("themes-scope-note", "Open a second consumer to verify these settings remain scoped to this document; the global GUI skin is never changed.", InsightUiTextStyle.Caption));
                return column;
            }

            private InsightUiElement BuildAdvanced()
            {
                InsightUiStack column = InsightUi.Column("advanced-content").SetGap(10f);
                InsightUiLabel graph = InsightUi.Label("advanced-graph", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Graph widget: " + graphResult.ActiveNodeCount + " nodes, " + graphResult.ActiveEdgeCount + " edges, " + graphResult.Iterations + " deterministic iterations.");
                InsightUiLabel timeline = InsightUi.Label("advanced-timeline", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Timeline widget: " + timelineClusters.Count + " deterministic clusters from " + optionalModel.Snapshot().Events.Count + " events.");
                InsightUiStack optional = InsightUi.Column("advanced-optional-stack").SetGap(8f);
                optional.Add(Card("advanced-graph-card", InsightUi.Column("advanced-graph-body").SetGap(6f).Add(
                        InsightUi.Label("advanced-graph-title", "Constellation graph", InsightUiTextStyle.Heading), graph,
                        InsightUi.Button("advanced-graph-action", "Recompute widget", () => SetInteraction("Graph widget recomputed deterministically")))),
                    Card("advanced-timeline-card", InsightUi.Column("advanced-timeline-body").SetGap(6f).Add(
                        InsightUi.Label("advanced-timeline-title", "Event river", InsightUiTextStyle.Heading), timeline,
                        InsightUi.Progress("advanced-timeline-progress", 0.68f, InsightTheme.Default.Warning))),
                    Card("advanced-explanation-card", InsightUi.Column("advanced-explanation-body").SetGap(6f).Add(
                        InsightUi.Label("advanced-explanation-title", "Explanation waterfall", InsightUiTextStyle.Heading),
                        InsightUi.Label("advanced-explanation-copy", "These widgets can consume semantic data, but the surrounding UI remains ordinary composable elements.", InsightUiTextStyle.Body),
                        InsightUi.Badge("advanced-extension-badge", "optional extension", InsightTheme.Default.Selected))),
                    Card("advanced-map-card", InsightUi.Column("advanced-map-body").SetGap(6f).Add(
                        InsightUi.Label("advanced-map-title", "Map link", InsightUiTextStyle.Heading),
                        InsightUi.Label("advanced-map-copy", "Trigger a transient map focus when a playable map exists, or receive a useful no-map state.", InsightUiTextStyle.Body),
                        InsightUi.Button("advanced-map-action", "Preview map link", TriggerMapAction))));
                column.Add(InsightUi.Label("advanced-note", "Advanced widgets are adapters, not the framework's required data architecture.", InsightUiTextStyle.Caption), optional);
                return column;
            }

            private InsightUiElement BuildDiagnostics()
            {
                InsightUiStack column = InsightUi.Column("diagnostics-content").SetGap(8f);
                InsightUiLabel summary = InsightUi.Label("diagnostics-summary", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => Document.Diagnostics.Summary());
                InsightUiLabel theme = InsightUi.Label("diagnostics-theme", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Theme: " + themeName + " · density: " + Document.Density);
                InsightUiLabel responsive = InsightUi.Label("diagnostics-responsive", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Navigation: " + (navigation.IsCompact ? "compact top navigation" : "wide side navigation") + " · breakpoint " + navigation.Breakpoint + " px");
                InsightUiLabel selected = InsightUi.Label("diagnostics-selected", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Selected page: " + navigation.ActivePageId + " · records selected: " + selectedRecords.Count);
                InsightUiLabel errors = InsightUi.Label("diagnostics-errors", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => Document.Diagnostics.RenderErrors == 0 ? "Render errors: none" :
                        "Render errors captured: " + Document.Diagnostics.RenderErrors);
                errors.Color = InsightTheme.Default.Negative;
                InsightUiLabel cache = InsightUi.Label("diagnostics-cache", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => "Virtual list cache: " + Document.Diagnostics.VirtualizedCachedElements +
                        " retained · visible " + Document.Diagnostics.VirtualizedVisibleElements +
                        " · active effects " + Document.Diagnostics.ActiveEffects);
                InsightUiStack metrics = InsightUi.Column("diagnostics-metrics").SetGap(6f);
                metrics.Add(summary, theme, responsive, selected, cache, errors,
                    InsightUi.Label("diagnostics-scope", "All values belong to this InsightUiDocument; no global GUI skin state is retained.", InsightUiTextStyle.Caption));
                InsightUiButton invalidate = InsightUi.Button("diagnostics-invalidate", "Record an invalidation", () => Document.Invalidate());
                column.Add(Card("diagnostics-card", metrics), invalidate,
                    InsightUi.Label("diagnostics-errors-note", "If a render exception occurs, the renderer records it and paints a safe fallback instead of leaving Unity GUI state corrupted.", InsightUiTextStyle.Caption));
                return column;
            }

            private InsightUiElement MetricsTable()
            {
                InsightUiStack table = InsightUi.Column("data-metrics-table").SetGap(4f);
                string[] labels = { "Population", "Confidence", "Momentum", "Coverage", "Risk" };
                for (int i = 0; i < labels.Length; i++)
                {
                    InsightUiStack row = InsightUi.Row("data-metric-row-" + i).SetGap(8f);
                    row.Add(InsightUi.Label("data-metric-label-" + i, labels[i], InsightUiTextStyle.Body),
                        InsightUi.Progress("data-metric-progress-" + i, 0.28f + i * 0.13f,
                            i == 4 ? InsightTheme.Default.Warning : InsightTheme.Default.Selected),
                        InsightUi.Badge("data-metric-badge-" + i, (int)(28f + i * 13f) + "%", InsightTheme.Default.SecondaryText));
                    table.Add(row);
                }
                return table;
            }

            private InsightUiElement ComparisonCard(string id)
            {
                comparisonLabel = InsightUi.Label(id + "-label", string.Empty, InsightUiTextStyle.Body)
                    .SetTextProvider(() => selectedRecords.Count < 2 ? "Select two records to compare." :
                        FindRecord(selectedRecords[0]).Name + "  ↔  " + FindRecord(selectedRecords[1]).Name +
                        " · shared signal " + SharedSignal());
                return Card(id, InsightUi.Column(id + "-body").SetGap(6f).Add(
                    InsightUi.Label(id + "-title", "Comparison inspector", InsightUiTextStyle.Heading), comparisonLabel,
                    InsightUi.Badge(id + "-badge", "two-record comparison", InsightTheme.Default.Selected)));
            }

            private InsightUiElement CreateRecordRow(int filteredIndex)
            {
                int recordIndex = filteredIndex >= 0 && filteredIndex < filteredRecords.Count ? filteredRecords[filteredIndex] : 0;
                InsightShowcaseRecord record = records[recordIndex];
                InsightUiButton button = InsightUi.Button("data-record-button-" + record.Id, record.Name,
                    () => SelectRecord(record.Id));
                button.SelectedProvider = () => selectedRecords.Contains(record.Id);
                button.SetTooltip("Select " + record.Name + " for comparison");
                InsightUiStack row = InsightUi.Row("data-record-row-" + record.Id).SetGap(7f);
                row.Add(button, InsightUi.Badge("data-record-group-" + record.Id, record.Group, record.Color),
                    InsightUi.Progress("data-record-progress-" + record.Id, record.Score, record.Color));
                return row;
            }

            private void ApplyFilter(string value)
            {
                string filter = value ?? string.Empty;
                filteredRecords.Clear();
                for (int i = 0; i < records.Count; i++)
                    if (string.IsNullOrEmpty(filter) || records[i].Matches(filter)) filteredRecords.Add(i);
                if (recordList != null) recordList.ItemCount = filteredRecords.Count;
                recordList?.Refresh();
                Document.Invalidate();
            }

            private void SelectRecord(string id)
            {
                if (selectedRecords.Contains(id)) return;
                if (selectedRecords.Count == 2) selectedRecords.RemoveAt(0);
                selectedRecords.Add(id);
                SetInteraction("Selected " + FindRecord(id).Name + " for comparison");
                Document.Invalidate();
            }

            private void TriggerMapAction()
            {
                if (Find.CurrentMap == null)
                {
                    SetInteraction("No playable map: map action is available once a map exists.");
                    return;
                }
                IntVec3 center = Find.CurrentMap.Center;
                InsightMapReference reference = InsightMapBridge.ForCell(Find.CurrentMap, center);
                InsightMapBridge.Flash("feature-showcase-map", reference).Invoke();
                SetInteraction("Map link flashed at the current map center.");
            }

            private string CurrentMapStatus()
            {
                return Find.CurrentMap == null ? "No playable map currently exists; the map-link action remains safe." :
                    "Playable map ready; map-linked previews can target the current center.";
            }

            private void ApplyTheme(string name)
            {
                InsightTheme theme = InsightTheme.Default.Clone();
                if (name == "Field Notes")
                {
                    theme.Background = new InsightColor(0.12f, 0.12f, 0.10f);
                    theme.Surface = new InsightColor(0.19f, 0.18f, 0.14f);
                    theme.ElevatedSurface = new InsightColor(0.24f, 0.22f, 0.16f);
                    theme.Selected = new InsightColor(0.72f, 0.52f, 0.22f);
                    theme.CornerRadius = 6f;
                }
                else if (name == "Night Watch")
                {
                    theme.Background = new InsightColor(0.055f, 0.075f, 0.10f);
                    theme.Surface = new InsightColor(0.10f, 0.13f, 0.17f);
                    theme.ElevatedSurface = new InsightColor(0.14f, 0.18f, 0.23f);
                    theme.Selected = new InsightColor(0.30f, 0.62f, 0.72f);
                    theme.CornerRadius = 2f;
                }
                Document.Theme = theme;
                themeName = name;
                Document.Invalidate();
            }

            private void GoTo(string page)
            {
                navigation.Select(page);
                Document.State.SetString(navigation.Id + ".active", page);
                Document.Invalidate();
            }

            private void SetInteraction(string text)
            {
                if (interactionStatus != null) interactionStatus.Text = text;
                Document.Invalidate();
            }

            private void Reset()
            {
                Document.State.Clear();
                Document.Theme = InsightTheme.Default.Clone();
                Document.Density = InsightUiDensity.Normal;
                Document.HighContrast = false;
                Document.ReducedMotion = false;
                themeName = "RimWorld+";
                motionTarget = 0.42f;
                motionProgress.Value = motionTarget;
                overviewInspector?.SetExpanded(false);
                motionReveal?.SetExpanded(false);
                selectedRecords.Clear();
                ApplyFilter(string.Empty);
                navigation.Select("overview");
                Document.State.SetString(navigation.Id + ".active", "overview");
                Document.Invalidate();
            }

            private InsightUiElement Card(string id, InsightUiElement content)
            {
                InsightUiSurface surface = InsightUi.Surface(id, content);
                surface.SetPadding(11f);
                return surface;
            }

            private InsightUiElement Swatch(string id, string label, InsightColor color)
            {
                InsightUiLabel text = InsightUi.Label(id + "-label", label, InsightUiTextStyle.Body);
                text.Color = color.Luminance > 0.45f ? new InsightColor(0.08f, 0.08f, 0.07f) : new InsightColor(0.94f, 0.92f, 0.87f);
                return InsightUi.Surface(id, text).SetPadding(10f).SetBackground(color);
            }

            private InsightUiElement ScrollSample(string id, int count)
            {
                InsightUiStack items = InsightUi.Column(id + "-items").SetGap(5f);
                for (int i = 0; i < count; i++) items.Add(InsightUi.Label(id + "-item-" + i,
                    "Scrollable row " + (i + 1) + " · content remains reachable", InsightUiTextStyle.Caption));
                return InsightUi.Scroll(id + "-scroll", Card(id + "-card", items));
            }

            private InsightShowcaseRecord FindRecord(string id)
            {
                for (int i = 0; i < records.Count; i++) if (records[i].Id == id) return records[i];
                return records[0];
            }

            private string SharedSignal()
            {
                if (selectedRecords.Count < 2) return "—";
                return ((FindRecord(selectedRecords[0]).Score + FindRecord(selectedRecords[1]).Score) * 50f).ToString("0") + "%";
            }

        }
    }
}
