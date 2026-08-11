using System;

namespace InsightCanvas
{
    /// <summary>
    /// A small, interactive gallery for the general-purpose UI layer. This is deliberately
    /// a consumer of the public composable API rather than a special renderer feature.
    /// </summary>
    public static class InsightFeatureShowcase
    {
        public static InsightUiWindow CreateWindow()
        {
            FeatureShowcaseDocument showcase = new FeatureShowcaseDocument();
            return new InsightUiWindow("Insight Canvas - Feature Showcase", showcase.Document);
        }

        private sealed class FeatureShowcaseDocument
        {
            private InsightUiTabs tabs;
            private InsightUiLabel actionStatus;

            public FeatureShowcaseDocument()
            {
                InsightUiElement root = BuildRoot();
                Document = new InsightUiDocument("Feature Showcase", root)
                {
                    Density = InsightUiDensity.Normal,
                    DrawBackground = true
                };
            }

            public InsightUiDocument Document { get; private set; }

            private InsightUiElement BuildRoot()
            {
                tabs = InsightUi.Tabs("showcase-tabs");
                tabs.Add("overview", "Overview", BuildOverview());
                tabs.Add("layout", "Layout", BuildLayout());
                tabs.Add("controls", "Controls", BuildControls());
                tabs.Add("virtual", "Virtualization", BuildVirtualization());
                tabs.Add("themes", "Themes", BuildThemes());

                InsightUiElement header = InsightUi.Surface("showcase-header",
                    InsightUi.Row("showcase-header-row",
                        InsightUi.Column("showcase-title",
                            InsightUi.Label("showcase-title-text", "Feature Showcase", InsightUiTextStyle.Title),
                            InsightUi.Label("showcase-subtitle", "Composable RimWorld UI primitives in one responsive document.", InsightUiTextStyle.Caption))
                            .SetFlex(1f),
                        InsightUi.Badge("showcase-version", "v2 foundation", new InsightColor(0.32f, 0.68f, 0.62f)),
                        InsightUi.IconButton("showcase-reset", "↺", Reset).SetTooltip("Reset showcase state")))
                    .SetPadding(12f, 9f);

                return InsightUi.Column("showcase-root", header, tabs)
                    .SetPadding(12f)
                    .SetGap(10f);
            }

            private InsightUiElement BuildOverview()
            {
                InsightUiGrid cards = InsightUi.Grid("overview-cards", 190f);
                cards.Add(
                    Card("overview-layout", "Responsive layout", "Rows, columns, grids, splits, padding, gaps, and flexible sizing adapt to the available Rect.",
                        0.82f, new InsightColor(0.36f, 0.64f, 0.76f)),
                    Card("overview-state", "Stable state", "Selection, tabs, expansion, focus, and scrolling belong to the document that owns them.",
                        0.64f, new InsightColor(0.61f, 0.49f, 0.8f)),
                    Card("overview-theme", "Scoped themes", "Warm charcoal surfaces, semantic accents, density presets, and accessibility settings stay local to the canvas.",
                        0.56f, new InsightColor(0.84f, 0.61f, 0.31f)),
                    Card("overview-embed", "Embeddable by design", "The same tree can draw into a caller-owned Rect or open as a conventional RimWorld Window.",
                        0.91f, new InsightColor(0.38f, 0.72f, 0.49f)));

                return InsightUi.Column("overview-content",
                    InsightUi.Breadcrumbs("overview-breadcrumbs", "Insight Canvas", "Feature Showcase", "Overview"),
                    InsightUi.Label("overview-heading", "A toolkit for authored interfaces", InsightUiTextStyle.Heading),
                    InsightUi.Label("overview-copy",
                        "Insight Canvas does not replace vanilla UI or impose a dashboard model. Mods opt into a small set of composable elements, then decide how much structure, density, and personality their screen needs."),
                    cards,
                    InsightUi.Surface("overview-note", InsightUi.Row("overview-note-row",
                        InsightUi.Badge("overview-note-badge", "Design rule", new InsightColor(0.85f, 0.67f, 0.3f)),
                        InsightUi.Label("overview-note-text", "Good defaults should make the first screen feel intentional, while every surface remains replaceable or embeddable."))).SetPadding(10f, 8f))
                    .SetGap(10f);
            }

            private InsightUiElement BuildLayout()
            {
                InsightUiElement left = InsightUi.Surface("layout-left",
                    InsightUi.Column("layout-left-content",
                        InsightUi.Label("layout-left-title", "Composition", InsightUiTextStyle.Heading),
                        InsightUi.Label("layout-left-copy", "This pane is a normal element inside a split. Resize the Window to see the same tree recompute its geometry."),
                        InsightUi.Divider("layout-left-divider"),
                        InsightUi.Label("layout-left-row", "Row + flex", InsightUiTextStyle.Label),
                        InsightUi.Label("layout-left-column", "Column + gap", InsightUiTextStyle.Label),
                        InsightUi.Label("layout-left-grid", "Adaptive grid", InsightUiTextStyle.Label),
                        InsightUi.Label("layout-left-scroll", "Scroll region", InsightUiTextStyle.Label),
                        InsightUi.Label("layout-left-split", "Split pane", InsightUiTextStyle.Label)))
                    .SetPadding(12f, 10f);

                InsightUiStack rows = InsightUi.Column("layout-scroll-content");
                rows.SetGap(8f);
                for (int i = 0; i < 14; i++)
                {
                    rows.Add(InsightUi.Surface("layout-row-" + i,
                        InsightUi.Row("layout-row-content-" + i,
                            InsightUi.Badge("layout-row-badge-" + i, "0" + ((i % 5) + 1)),
                            InsightUi.Label("layout-row-label-" + i,
                                "Stable element identity keeps this row's state attached while its parent is resized."))).SetPadding(9f, 7f));
                }

                InsightUiElement right = InsightUi.Surface("layout-right",
                    InsightUi.Column("layout-right-content",
                        InsightUi.Label("layout-right-title", "Measured content", InsightUiTextStyle.Heading),
                        InsightUi.Label("layout-right-copy", "The scroll container clips its child and stores the offset in document state."),
                        InsightUi.Scroll("layout-scroll", rows).SetFlex(1f)))
                    .SetPadding(12f, 10f);

                InsightUiSplit split = InsightUi.Split("layout-split", left, right, 0.34f);
                split.SetFlex(1f);
                return InsightUi.Column("layout-page",
                    InsightUi.Label("layout-page-heading", "Layout is a relationship, not a fixed dashboard", InsightUiTextStyle.Heading),
                    InsightUi.Label("layout-page-copy", "The showcase uses the same measure, arrange, and paint phases that consumer mods receive."),
                    split).SetGap(8f);
            }

            private InsightUiElement BuildControls()
            {
                actionStatus = InsightUi.Label("controls-status", "Ready for an interaction.", InsightUiTextStyle.Caption);
                InsightUiToggle motion = InsightUi.Toggle("controls-motion", "Reduced motion", false, value =>
                {
                    if (Document != null) Document.ReducedMotion = value;
                });
                InsightUiToggle contrast = InsightUi.Toggle("controls-contrast", "High contrast", false, value =>
                {
                    if (Document != null) Document.HighContrast = value;
                });
                InsightUiSlider density = InsightUi.Slider("controls-density", 1f, 0f, 2f, value =>
                {
                    if (Document != null) Document.Density = value < 0.67f ? InsightUiDensity.Compact :
                        value > 1.33f ? InsightUiDensity.Comfortable : InsightUiDensity.Normal;
                });
                InsightUiTextField note = InsightUi.TextField("controls-note", "Try typing here", value =>
                {
                    if (actionStatus != null) actionStatus.Text = "Draft: " + value;
                });

                return InsightUi.Column("controls-page",
                    InsightUi.Label("controls-heading", "Controls that carry state without global singletons", InsightUiTextStyle.Heading),
                    InsightUi.Label("controls-copy", "Every control below uses a stable id and stores transient interaction state in this document."),
                    InsightUi.Surface("controls-settings", InsightUi.Column("controls-settings-content",
                        motion,
                        contrast,
                        InsightUi.Label("controls-density-label", "Density (compact → comfortable)", InsightUiTextStyle.Caption),
                        density.SetWidth(InsightLength.Percent(1f)),
                        note.SetWidth(InsightLength.Percent(1f)),
                        InsightUi.Row("controls-actions",
                            InsightUi.Button("controls-action", "Apply sample action", () =>
                            {
                                if (actionStatus != null) actionStatus.Text = "Action invoked at the current density.";
                            }),
                            InsightUi.IconButton("controls-icon-action", "+", () =>
                            {
                                if (actionStatus != null) actionStatus.Text = "Context action invoked.";
                            }))).SetGap(8f)).SetPadding(12f, 10f),
                    InsightUi.Progress("controls-progress", 0.68f, new InsightColor(0.35f, 0.69f, 0.61f)),
                    actionStatus).SetGap(10f);
            }

            private InsightUiElement BuildVirtualization()
            {
                InsightUiElement list = InsightUi.VirtualList("virtual-list", 400, 32f, index =>
                    InsightUi.Row("virtual-item-" + index,
                        InsightUi.Badge("virtual-index-" + index, (index + 1).ToString("000")),
                        InsightUi.Label("virtual-label-" + index,
                            "Only visible rows are measured and painted; this item has stable identity " + index + "."))
                        .SetPadding(8f, 4f)
                        .SetGap(8f));
                return InsightUi.Column("virtual-page",
                    InsightUi.Label("virtual-heading", "Virtualization for long mod-owned collections", InsightUiTextStyle.Heading),
                    InsightUi.Label("virtual-copy", "Scroll this list to see the visible range stay bounded while the content height remains deterministic."),
                    list.SetFlex(1f)).SetGap(8f);
            }

            private InsightUiElement BuildThemes()
            {
                return InsightUi.Column("themes-page",
                    InsightUi.Label("themes-heading", "Scoped visual language", InsightUiTextStyle.Heading),
                    InsightUi.Label("themes-copy", "The default theme is intentionally restrained: warm charcoal surfaces, readable neutrals, muted accents, and shallow depth."),
                    InsightUi.Grid("themes-grid", 190f).Add(
                        Card("theme-warm", "Warm charcoal", "The default RimWorld+ surface and text hierarchy.", 0.8f, new InsightColor(0.33f, 0.65f, 0.72f)),
                        Card("theme-density", "Density presets", "Compact, normal, and comfortable spacing use the same component tree.", 0.58f, new InsightColor(0.72f, 0.48f, 0.72f)),
                        Card("theme-accessibility", "Accessibility", "High contrast, color-blind palette transforms, and reduced motion stay opt-in.", 0.72f, new InsightColor(0.87f, 0.65f, 0.28f))),
                    InsightUi.Breadcrumbs("themes-breadcrumbs", "Theme", "Scoped", "Document")).SetGap(10f);
            }

            private InsightUiElement Card(string id, string title, string copy, float progress, InsightColor accent)
            {
                return InsightUi.Surface(id, InsightUi.Column(id + ".content",
                    InsightUi.Row(id + ".title-row",
                        InsightUi.Label(id + ".title", title, InsightUiTextStyle.Heading).SetFlex(1f),
                        InsightUi.Badge(id + ".badge", "ready", accent)),
                    InsightUi.Label(id + ".copy", copy, InsightUiTextStyle.Caption),
                    InsightUi.Progress(id + ".progress", progress, accent))).SetPadding(11f, 10f).SetGap(8f);
            }

            private void Reset()
            {
                if (Document == null) return;
                Document.Theme = InsightTheme.Default;
                Document.Density = InsightUiDensity.Normal;
                Document.HighContrast = false;
                Document.ReducedMotion = false;
                Document.State.Clear();
                if (actionStatus != null) actionStatus.Text = "Showcase state reset.";
                Document.Invalidate();
            }
        }
    }
}
