using System;
using System.Collections.Generic;
using InsightCanvas;

internal static class Program
{
    private static int Main()
    {
        try
        {
            ModelValidation();
            ValidationCoverage();
            ModelClear();
            LayoutMath();
            ComposableLayout();
            UiStateIsolation();
            ResponsiveGridAndVirtualization();
            UiThemeScope();
            ShowcaseNavigationAndResponsiveLayout();
            ShowcaseSettingsScope();
            ShowcaseDataDeterminism();
            SelectionPropagation();
            ExplanationCalculation();
            ThemeParsing();
            GraphDeterminism();
            GraphBudgeting();
            GraphFitAndHeaderGeometry();
            TimelineMath();
            OverlayOwnership();
            Serialization();
            SerializationOrdering();
            MotionSettings();
            Console.WriteLine("Insight Canvas core tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void ModelValidation()
    {
        InsightModel model = InsightModel.Create("test").Entity("a", "A").Entity("b", "B")
            .Relation("a", "b", "knows").Metric("a", "count", 4f);
        Assert(model.Validate().IsValid, "valid model rejected");
        InsightModel invalid = InsightModel.Create("invalid").Entity("a", "A").Entity("a", "duplicate").Relation("a", "missing", "contains");
        Assert(!invalid.Validate().IsValid && invalid.Validate().Errors.Count >= 2, "invalid model was not reported");
        InsightModel nonFinite = InsightModel.Create("nonfinite").Entity("a", "A").Metric("a", "bad", new InsightMetric("bad", float.NaN));
        Assert(!nonFinite.Validate().IsValid, "non-finite metric was not reported");
    }

    private static void ValidationCoverage()
    {
        InsightModel invalid = InsightModel.Create("validation").Entity("a", "A")
            .Relation("missing", "a", "broken")
            .Metric("missing", "orphan", 1f)
            .Metric("a", "duplicate", 1f)
            .Metric("a", "duplicate", 2f)
            .Explanation("missing", Explain.Value("Missing owner", 1f))
            .Event(new InsightEvent(string.Empty, 1, "Empty id"))
            .Event(new InsightEvent("same-event", 2, "First"))
            .Event(new InsightEvent("same-event", 3, "Duplicate"));
        invalid.Action("a", new InsightAction(string.Empty, "Empty action", null));
        invalid.Action("a", new InsightAction("same-action", "First", null));
        invalid.Action("a", new InsightAction("same-action", "Duplicate", null));
        invalid.Action("missing", new InsightAction("same-action", "Orphan", null));
        InsightModelValidation validation = invalid.Validate();
        Assert(!validation.IsValid, "expanded validation accepted invalid ids and references");
        Assert(Contains(validation.Errors, "relations id 'missing->a'") &&
            Contains(validation.Errors, "metrics id 'missing'") &&
            Contains(validation.Errors, "explanations id 'missing'"),
            "validation omitted missing owner/reference diagnostics");
        Assert(Contains(validation.Errors, "events id '<empty>'") &&
            Contains(validation.Errors, "events id 'same-event'") &&
            Contains(validation.Errors, "actions id '<empty>'") &&
            Contains(validation.Errors, "actions id 'same-action'") &&
            Contains(validation.Errors, "metrics id 'a/duplicate'"),
            "validation omitted duplicate or empty id diagnostics");

        InsightModelSerializationReport danglingPosition = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='positions'><entities><entity id='a' label='A'/></entities>" +
            "<manualPositions><position entity='missing' x='1' y='2'/></manualPositions>" +
            "<explanations><explanation owner='a' label='one' final='1'/><explanation owner='a' label='two' final='2'/>" +
            "</explanations></insightModel>");
        InsightModelValidation positionValidation = danglingPosition.Model.Validate();
        Assert(!positionValidation.IsValid && Contains(positionValidation.Errors, "manualPositions id 'missing'") &&
            Contains(positionValidation.Errors, "explanations id 'a': duplicate owner id"),
            "dangling manual position or duplicate explanation owner was not reported");
        InsightModelSerializationReport malformedPosition = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='positions'><entities><entity id='a' label='A'/></entities>" +
            "<manualPositions><position entity='a' x='not-a-number' y='2'/></manualPositions></insightModel>");
        Assert(Contains(malformedPosition.Model.Validate().Errors, "manualPositions id 'a': coordinates must be numeric"),
            "malformed serialized display coordinates were silently accepted");
    }

    private static void LayoutMath()
    {
        IReadOnlyList<InsightLayoutBox> row = InsightLayout.ArrangeRow(new InsightRect(0f, 0f, 100f, 20f),
            new[] { "fixed", "flex" }, new[] { InsightLayoutSpec.Fixed(20f), InsightLayoutSpec.Flexible(10f, 20f) }, 5f);
        Assert(Math.Abs(row[0].Rect.Width - 20f) < 0.01f && Math.Abs(row[1].Rect.Width - 75f) < 0.01f, "row flex allocation changed");
        IReadOnlyList<InsightLayoutBox> grid = InsightLayout.ArrangeGrid(new InsightRect(0f, 0f, 100f, 50f), 4, 2, 4f);
        Assert(grid.Count == 4 && Math.Abs(grid[0].Rect.Width - 48f) < 0.01f, "grid allocation changed");
        InsightRect centered = InsightLayout.Align(new InsightRect(0f, 0f, 100f, 50f), 40f, 20f, InsightAlignment.Center);
        Assert(Math.Abs(centered.X - 30f) < 0.01f && Math.Abs(centered.Y - 15f) < 0.01f, "alignment changed");
        InsightRect header = new InsightRect(0f, 0f, 1280f, 43f);
        InsightRect disclosure = InsightHeaderLayout.DisclosureControls(header);
        InsightRect tools = InsightHeaderLayout.ToolsButton(header);
        InsightRect reset = InsightHeaderLayout.ResetButton(header);
        Assert(disclosure.Width >= 329.99f && disclosure.Right <= tools.X - 8f &&
            tools.Right <= reset.X - 8f && reset.Right <= header.Right - 8f,
            "header controls overlap or exceed the window");
    }

    private static void ModelClear()
    {
        InsightModel model = InsightModel.Create("clear").Entity("a", "A").Entity("b", "B")
            .Relation("a", "b", "feeds").Metric("a", "value", 1f).Event(new InsightEvent("event", 1, "Changed"));
        int revision = model.Revision;
        model.Clear();
        InsightModelSnapshot snapshot = model.Snapshot();
        Assert(snapshot.Entities.Count == 0 && snapshot.Relations.Count == 0 && snapshot.Events.Count == 0 &&
            model.Revision > revision, "model clear did not replace published data");
    }

    private static void ComposableLayout()
    {
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        InsightUiElement fixedPanel = InsightUi.Surface("fixed", InsightUi.Label("fixed-label", "Fixed"))
            .SetWidth(InsightLength.Fixed(120f));
        InsightUiElement flexiblePanel = InsightUi.Surface("flex", InsightUi.Label("flex-label", "Flexible"))
            .SetFlex(1f);
        InsightUiStack row = InsightUi.Row("row", fixedPanel, flexiblePanel).SetGap(8f) as InsightUiStack;
        InsightUiSize measured = row.Measure(new InsightUiConstraints(0f, 400f, 0f, 120f), frame);
        row.Arrange(new InsightRect(0f, 0f, 400f, 120f), frame);
        Assert(measured.Width > 0f && row.Children[0].LayoutRect.Width >= 119f &&
            row.Children[1].LayoutRect.Width > row.Children[0].LayoutRect.Width &&
            row.Children[1].LayoutRect.Right <= 400.01f, "composable row did not honor fixed/flexible layout");

        InsightUiStack wrapped = InsightUi.Wrap("wrapped",
            InsightUi.Label("one", "one").SetWidth(InsightLength.Fixed(90f)),
            InsightUi.Label("two", "two").SetWidth(InsightLength.Fixed(90f)),
            InsightUi.Label("three", "three").SetWidth(InsightLength.Fixed(90f)));
        wrapped.SetGap(8f);
        wrapped.Measure(new InsightUiConstraints(0f, 190f, 0f, 200f), frame);
        wrapped.Arrange(new InsightRect(0f, 0f, 190f, 200f), frame);
        Assert(wrapped.Children[2].LayoutRect.Y > wrapped.Children[0].LayoutRect.Y, "wrapped layout did not create a second line");
    }

    private static void UiStateIsolation()
    {
        InsightUiDocument first = new InsightUiDocument("first", InsightUi.Empty("root"));
        InsightUiDocument second = new InsightUiDocument("second", InsightUi.Empty("root"));
        first.State.SetBool("panel.open", true);
        first.State.SetFloat("scroll", 42f);
        Assert(first.State.GetBool("panel.open") && Math.Abs(first.State.GetFloat("scroll") - 42f) < 0.001f &&
            !second.State.GetBool("panel.open") && Math.Abs(second.State.GetFloat("scroll")) < 0.001f,
            "composable state leaked between documents");
        int revision = first.Revision;
        first.Invalidate();
        Assert(first.Revision > revision && first.Diagnostics.Invalidations == 1, "document invalidation was not scoped");
    }

    private static void ResponsiveGridAndVirtualization()
    {
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Compact, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 0.016f);
        InsightUiGrid grid = InsightUi.Grid("grid", 150f)
            .Add(InsightUi.Surface("a", InsightUi.Label("a-label", "A")),
                InsightUi.Surface("b", InsightUi.Label("b-label", "B")),
                InsightUi.Surface("c", InsightUi.Label("c-label", "C")),
                InsightUi.Surface("d", InsightUi.Label("d-label", "D")));
        grid.Measure(new InsightUiConstraints(0f, 420f, 0f, 400f), frame);
        grid.Arrange(new InsightRect(0f, 0f, 420f, 240f), frame);
        Assert(grid.Children[0].LayoutRect.X < grid.Children[1].LayoutRect.X &&
            grid.Children[2].LayoutRect.Y > grid.Children[0].LayoutRect.Y,
            "adaptive grid did not arrange rows and columns");
        InsightVirtualizedRange range = InsightVirtualization.Range(1000, 24f, 240f, 480f, 2);
        Assert(range.Start < 20 && range.Contains(20) && range.End > 25 && range.End < 1000,
            "virtualized range did not include the viewport with overscan");
        Assert(Math.Abs(InsightVirtualization.ContentHeight(10, 24f) - 240f) < 0.001f,
            "virtualized content height changed");
    }

    private static void UiThemeScope()
    {
        InsightUiDocument first = new InsightUiDocument("first-theme", InsightUi.Empty("root"));
        InsightUiDocument second = new InsightUiDocument("second-theme", InsightUi.Empty("root"));
        InsightColor original = second.Theme.Selected;
        first.Theme.Selected = new InsightColor(1f, 0f, 0f);
        first.HighContrast = true;
        InsightTheme accessible = first.Theme.WithAccessibility(true, InsightColorBlindMode.None);
        Assert(first.Theme.Selected.Equals(new InsightColor(1f, 0f, 0f)) && second.Theme.Selected.Equals(original) &&
            !accessible.PrimaryText.Equals(first.Theme.PrimaryText), "theme overrides were not scoped or transformed");
    }

    private static void ShowcaseNavigationAndResponsiveLayout()
    {
        InsightUiNavigation navigation = InsightUi.Navigation("showcase-navigation", 700f);
        for (int i = 0; i < 10; i++)
            navigation.Add("page-" + i, "Page " + i, InsightUi.Surface("page-surface-" + i,
                InsightUi.Label("page-label-" + i, "Page content " + i)));
        InsightUiDocument document = new InsightUiDocument("navigation-test", navigation);
        InsightUiFrame frame = new InsightUiFrame(document.Theme, document.Density, false, false,
            document.State, document.Diagnostics, 1f / 60f);
        InsightUiConstraints wide = new InsightUiConstraints(0f, 960f, 0f, 520f);
        navigation.Measure(wide, frame);
        navigation.Arrange(new InsightRect(0f, 0f, 960f, 520f), frame);
        Assert(!navigation.IsCompact && navigation.MeasuredSize.Width > 0f,
            "showcase navigation did not use its wide side rail");

        navigation.Select("page-7");
        document.State.SetString("showcase-navigation.active", "page-7");
        frame = new InsightUiFrame(document.Theme, document.Density, false, false,
            document.State, document.Diagnostics, 1f / 60f);
        InsightUiConstraints narrow = new InsightUiConstraints(0f, 480f, 0f, 520f);
        navigation.Measure(narrow, frame);
        navigation.Arrange(new InsightRect(0f, 0f, 480f, 520f), frame);
        Assert(navigation.IsCompact && navigation.ActivePageId == "page-7",
            "showcase navigation did not switch to compact mode or preserve selection");
    }

    private static void ShowcaseSettingsScope()
    {
        InsightUiDocument first = new InsightUiDocument("showcase-first", InsightUi.Empty("first-root"));
        InsightUiDocument second = new InsightUiDocument("showcase-second", InsightUi.Empty("second-root"));
        first.Density = InsightUiDensity.Compact;
        first.HighContrast = true;
        first.ReducedMotion = true;
        first.State.SetString("showcase-navigation.active", "diagnostics");
        Assert(second.Density == InsightUiDensity.Normal && !second.HighContrast && !second.ReducedMotion &&
            second.State.GetString("showcase-navigation.active", string.Empty) == string.Empty,
            "showcase settings leaked between documents");
    }

    private static void ShowcaseDataDeterminism()
    {
        IReadOnlyList<InsightShowcaseRecord> first = InsightFeatureShowcaseData.CreateRecords();
        IReadOnlyList<InsightShowcaseRecord> second = InsightFeatureShowcaseData.CreateRecords();
        Assert(first.Count == 64 && second.Count == first.Count, "showcase demo data count changed");
        for (int i = 0; i < first.Count; i++)
            Assert(first[i].Id == second[i].Id && first[i].Name == second[i].Name &&
                first[i].Group == second[i].Group && Math.Abs(first[i].Score - second[i].Score) < 0.0001f,
                "showcase demo data was not deterministic at record " + i);
        Assert(first[0].Matches("habitat") && !first[0].Matches("does-not-exist"),
            "showcase demo record filtering was incorrect");
    }

    private static void SelectionPropagation()
    {
        InsightContext context = new InsightContext();
        int changes = 0;
        context.Changed += () => changes++;
        context.Select("animal");
        context.Hover("animal");
        context.SetFilter("fish");
        Assert(context.SelectedEntityId == "animal" && context.FocusedEntityId == "animal" && changes == 3, "shared context did not propagate");
        context.Compare("other");
        Assert(context.ComparedEntityId == "other", "comparison target did not propagate");
        context.BeginFrame();
        context.EndFrame();
        Assert(context.HoveredEntityId == null, "hover was not cleared per frame");
    }

    private static void ExplanationCalculation()
    {
        InsightExplanationResult result = Explain.Value("Catch chance", 0.8f).Base(0.5f)
            .Factor("Knowledge", 1.2f).Factor("Mismatch", 0.5f).Clamp("Population cap", 0.1f, 0.8f)
            .Requirement("Lure available", true).Calculate();
        Assert(Math.Abs(result.ComputedValue - 0.3f) < 0.001f, "explanation multiplication changed");
        Assert(result.Segments.Count == 6 && result.Summary.Contains("Population cap"), "explanation segments missing");
    }

    private static void ThemeParsing()
    {
        InsightTheme theme = InsightTheme.FromXml("<theme id='test'><color name='selected' value='#123456'/><spacing value='12'/></theme>");
        Assert(theme.Id == "test" && Math.Abs(theme.Selected.R - 0x12 / 255f) < 0.001f && Math.Abs(theme.Spacing - 12f) < 0.01f, "theme XML parsing changed");
    }

    private static void GraphDeterminism()
    {
        InsightModel model = InsightModel.Create("graph").Entity("b", "B")
            .Entity(new InsightEntity("a", "A", manualPosition: new InsightPoint(50f, 60f)))
            .Entity("c", "C").Entity("d", "D")
            .Relation("a", "b", "knows").Relation("b", "c", "feeds").Relation("c", "a", "cycles");
        InsightGraphLayoutResult first = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        InsightGraphLayoutResult second = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        Assert(first.Complete && first.Position("a").Equals(second.Position("a")) && first.Position("d").Equals(second.Position("d")) &&
            first.Position("a").Equals(new InsightPoint(50f, 60f)) && InsightGraphLayout.AreNeighbors(model.Snapshot(), "a", "b"),
            "graph layout is not deterministic or pinned");
    }

    private static void GraphBudgeting()
    {
        InsightModel model = InsightModel.Create("large-graph");
        for (int i = 0; i < 24; i++) model.Entity("node:" + i, "Node " + i);
        for (int i = 0; i < 24; i++)
            for (int j = i + 1; j < 24; j++) model.Relation("node:" + i, "node:" + j, "related");
        InsightGraphLayoutResult result = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f, 5, 7, 2);
        Assert(result.ActiveNodeCount <= 5 && result.Positions.Count <= 5 && result.ActiveEdgeCount <= 7 && result.Edges.Count <= 7,
            "graph layout exceeded node or edge budget");
        for (int i = 0; i < result.Edges.Count; i++)
            Assert(result.ContainsNode(result.Edges[i].FromId) && result.ContainsNode(result.Edges[i].ToId),
                "budgeted graph retained an edge outside its active node set");
    }

    private static void GraphFitAndHeaderGeometry()
    {
        InsightModel model = InsightModel.Create("fit").Entity(new InsightEntity("left", "Left",
                manualPosition: new InsightPoint(40f, 30f)))
            .Entity(new InsightEntity("right", "Right", manualPosition: new InsightPoint(360f, 210f)))
            .Entity(new InsightEntity("center", "Center", manualPosition: new InsightPoint(200f, 120f)));
        InsightGraphLayoutResult layout = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f, 10);
        InsightGraphFit fit = InsightGraphViewport.Fit(layout, 400f, 240f, 24f);
        Assert(fit.Zoom > 1f && fit.Zoom <= 2.8f, "Fit All did not calculate a bounded zoom");
        for (int i = 0; i < layout.ActiveNodeIds.Count; i++)
        {
            InsightPoint point = layout.Position(layout.ActiveNodeIds[i]);
            float x = 200f + (point.X - 200f) * fit.Zoom + fit.Pan.X;
            float y = 120f + (point.Y - 120f) * fit.Zoom + fit.Pan.Y;
            Assert(x >= 24f - 0.01f && x <= 376f + 0.01f && y >= 24f - 0.01f && y <= 216f + 0.01f,
                "Fit All left an active node outside the viewport bounds");
        }
    }

    private static void TimelineMath()
    {
        InsightTimeRange empty = InsightTimeRange.Empty;
        InsightTimeRange zero = new InsightTimeRange(0, 0);
        InsightTimeRange normal = new InsightTimeRange(10, 100);
        InsightTimeRange reversed = new InsightTimeRange(100, 10);
        Assert(empty.IsEmpty && !empty.Contains(0) && !empty.Contains(long.MinValue), "empty time range is not explicit");
        Assert(!zero.IsEmpty && zero.Contains(0) && !zero.Equals(empty), "empty time range collides with a valid zero range");
        Assert(!normal.IsEmpty && normal.Contains(10) && normal.Contains(100) && !normal.Contains(101),
            "normal time range boundaries changed");
        Assert(!reversed.IsEmpty && reversed.Start == 10 && reversed.End == 100 && reversed.Contains(10) && reversed.Contains(100),
            "reversed time range bounds did not normalize");
        List<InsightEvent> events = new List<InsightEvent>
        {
            new InsightEvent("before", -500000, "Before"), new InsightEvent("start", 10, "Start"),
            new InsightEvent("end", 100, "End"), new InsightEvent("after", 9000000, "After")
        };
        InsightTimeRange range = InsightTimelineMath.Bounds(events);
        IReadOnlyList<InsightTimelineCluster> clusters = InsightTimelineMath.Cluster(events, range, 100f, 20);
        int allEventCount = 0;
        for (int i = 0; i < clusters.Count; i++) allEventCount += clusters[i].Count;
        Assert(range.Start == -500000 && range.End == 9000000 && allEventCount == events.Count, "timeline bounds changed");
        IReadOnlyList<InsightTimelineCluster> defaultClusters = InsightTimelineMath.Cluster(events, empty, 100f, 20);
        int defaultEventCount = 0;
        for (int i = 0; i < defaultClusters.Count; i++) defaultEventCount += defaultClusters[i].Count;
        Assert(defaultEventCount == events.Count, "default timeline filtering excluded events");
        IReadOnlyList<InsightTimelineCluster> boundaryClusters = InsightTimelineMath.Cluster(events, normal, 100f, 20);
        int boundaryEventCount = 0;
        for (int i = 0; i < boundaryClusters.Count; i++) boundaryEventCount += boundaryClusters[i].Count;
        Assert(boundaryEventCount == 2, "timeline filtering is not boundary-inclusive");
        InsightContext context = new InsightContext();
        Assert(context.TimeRange.IsEmpty, "default timeline state is not no-filter");
        InsightTimeRange effective = InsightTimelineMath.EffectiveRange(context.TimeRange, range);
        Assert(effective.Equals(range) && InsightTimelineMath.Cluster(events, effective, 100f, 20).Count > 0,
            "default timeline filtering lost arbitrary ticks");
        context.SetTimeRange(reversed);
        Assert(!context.TimeRange.IsEmpty && context.TimeRange.Start == 10 && context.TimeRange.End == 100,
            "selected timeline range changed");
        context.SetTimeRange(InsightTimeRange.Empty);
        Assert(context.TimeRange.IsEmpty && InsightTimelineMath.EffectiveRange(context.TimeRange, range).Equals(range),
            "reset timeline state is not no-filter");
        InsightTimeRange zoomed = InsightTimelineMath.Zoom(range, 2f, 55);
        Assert(zoomed.End - zoomed.Start < range.End - range.Start, "timeline zoom changed");
        InsightTimeRange extreme = new InsightTimeRange(long.MinValue, long.MaxValue);
        InsightTimeRange extremeZoom = InsightTimelineMath.Zoom(extreme, 2f, 0L);
        Assert(InsightTimelineMath.TickAt(extreme, 0.5) == 0L && !extremeZoom.IsEmpty && extremeZoom.Contains(0L),
            "timeline arithmetic overflowed at long tick boundaries");
        List<InsightEvent> clusteredEvents = new List<InsightEvent>
        {
            new InsightEvent("same-1", 10, "A"), new InsightEvent("same-2", 10, "B"),
            new InsightEvent("separate", 100, "C")
        };
        IReadOnlyList<InsightTimelineCluster> deterministicClusters = InsightTimelineMath.Cluster(clusteredEvents,
            InsightTimelineMath.Bounds(clusteredEvents), 100f, 20);
        Assert(deterministicClusters.Count == 2 && deterministicClusters[0].Count == 2,
            "timeline clustering changed");
    }

    private static void OverlayOwnership()
    {
        object ownerA = new object();
        object ownerB = new object();
        List<OverlayTestEntry> entries = new List<OverlayTestEntry>
        {
            new OverlayTestEntry("a-1", ownerA), new OverlayTestEntry("b-1", ownerB),
            new OverlayTestEntry("a-2", ownerA)
        };
        int removed = InsightOverlayOwnership.ClearOwner(entries, ownerA, entry => entry.OwnerToken);
        Assert(removed == 2 && entries.Count == 1 && entries[0].Id == "b-1", "owner-scoped overlay clearing removed another owner");
        Assert(InsightOverlayOwnership.ClearOwner(entries, ownerA, entry => entry.OwnerToken) == 0 && entries.Count == 1,
            "owner clearing was not idempotent");
        Assert(InsightOverlayOwnership.ClearOwner(entries, ownerB, entry => entry.OwnerToken) == 1 && entries.Count == 0,
            "second overlay owner could not be cleared");
    }

    private static void Serialization()
    {
        InsightModel model = InsightModel.Create("roundtrip")
            .Entity(new InsightEntity("a", "A", "first", "people", new object(), new object(),
                new[] { "important", "known" }, new InsightPoint(25f, 40f), "source:a", "icons/a.png"))
            .Entity("b", "B").Relation("a", "b", "contains", 0.6f, false, 0.75f, false)
            .Metric("a", "value", new InsightMetric("value", 0.75f, new InsightRange(0.2f, 0.9f), true, true, 0.8f, 0.5f, InsightTrend.Rising,
                new[] { new InsightSample(1, 0.4f), new InsightSample(2, 0.75f) }))
            .Action("a", new InsightAction("inspect", "Inspect", () => { }, true, "Show details", true))
            .Explanation("a", Explain.Value("Score", 0.6f).Base(0.5f).Factor("knowledge", 1.2f, 0.8f, true)
                .Add("penalty", -0.1f, 0.7f, false).Clamp("cap", 0f, 1f).Requirement("ready", true, "prepared")
                .Uncertain(0.2f, 0.8f, 0.5f, "missing data"))
            .Event(new InsightEvent("event", 12, "Changed", "test", new[] { "a", "b" }, 0.4f, false, "map-link"));
        InsightModelSnapshot original = model.Snapshot();
        InsightModelSerializationReport writeReport = InsightModelSerialization.SerializeWithDiagnostics(original);
        Assert(writeReport.Succeeded && writeReport.Xml.Contains("schemaVersion=\"2\"") &&
            Contains(writeReport.Warnings, "Source omitted") && Contains(writeReport.Warnings, "Icon/texture omitted") &&
            Contains(writeReport.Warnings, "callback omitted"),
            "runtime serialization diagnostics are incomplete");
        Assert(writeReport.Xml == InsightModelSerialization.Serialize(original), "serialized output is not deterministic");
        InsightAction originalAction = original.ActionsFor("a")[0];
        Assert(originalAction.ConfiguredEnabled && originalAction.Enabled, "live action did not retain configured intent");

        InsightModelSerializationReport readReport = InsightModelSerialization.DeserializeWithDiagnostics(writeReport.Xml);
        Assert(readReport.Succeeded && readReport.Model != null && readReport.Warnings.Count >= 3,
            "full model did not load with runtime omission diagnostics");
        InsightModelSnapshot copy = readReport.Model.Snapshot();
        InsightEntity copiedEntity = copy.Entity("a");
        Assert(copy.Id == "roundtrip" && copy.Entities.Count == 2 && copy.Relations.Count == 1 &&
            copiedEntity.Badges.Count == 2 && copiedEntity.ManualPosition.HasValue &&
            copiedEntity.ManualPosition.Value.Equals(new InsightPoint(25f, 40f)) &&
            copiedEntity.SourceId == "source:a" && copiedEntity.IconId == "icons/a.png" &&
            copiedEntity.Source == null && copiedEntity.Icon == null, "entity display data did not round-trip safely");
        Assert(copy.MetricsFor("a").Count == 1 && Math.Abs(copy.MetricsFor("a")[0].Value - 0.75f) < 0.001f &&
            copy.MetricsFor("a")[0].History.Count == 2 && copy.Events.Count == 1 &&
            copy.Events[0].EntityIds.Count == 2 && copy.Events[0].MapLinkId == "map-link" &&
            copy.Relations[0].Directed == false && !copy.Relations[0].Known, "model values did not round-trip");
        InsightExplanationResult originalExplanation = original.ExplanationFor("a").Calculate();
        InsightExplanationResult copiedExplanation = copy.ExplanationFor("a").Calculate();
        Assert(copiedExplanation.Segments.Count == originalExplanation.Segments.Count &&
            Math.Abs(copiedExplanation.ComputedValue - originalExplanation.ComputedValue) < 0.001f,
            "explanation data did not round-trip");
        InsightAction copiedAction = copy.ActionsFor("a")[0];
        Assert(copiedAction.Id == "inspect" && copiedAction.Label == "Inspect" && copiedAction.Tooltip == "Show details" &&
            copiedAction.ConfiguredEnabled && copiedAction.CloseWindowAfterInvoke && !copiedAction.Enabled &&
            copiedAction.Callback == null && !copiedAction.Invoke(),
            "runtime action was restored as invokable");

        bool reboundInvoked = false;
        readReport.Model.RebindAction("a", "inspect", () => reboundInvoked = true);
        InsightAction rebound = readReport.Model.Snapshot().ActionsFor("a")[0];
        Assert(rebound.ConfiguredEnabled && rebound.Enabled && rebound.Invoke() && reboundInvoked,
            "callback rebinding did not restore runtime executability");

        InsightModelSerializationReport legacy = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel id='legacy'><entities><entity id='a' label='A'/></entities><relations/>" +
            "<metrics/><events><event id='e' tick='9' label='Old'><entity id='a'/></event></events></insightModel>");
        Assert(legacy.Succeeded && legacy.Model.Snapshot().Entity("a") != null && legacy.Model.Snapshot().Events.Count == 1 &&
            Contains(legacy.Warnings, "unversioned"), "existing unversioned model format no longer loads");

        InsightModelSerializationReport schemaV2 = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='v2'><entities><entity id='a' label='A'/></entities>" +
            "<actions><action entity='a' id='old-action' label='Old' enabled='true' callback='omitted'/></actions></insightModel>");
        InsightAction oldAction = schemaV2.Model.Snapshot().ActionsFor("a")[0];
        Assert(schemaV2.Succeeded && oldAction.ConfiguredEnabled && !oldAction.Enabled && oldAction.Callback == null &&
            Contains(schemaV2.Warnings, "rebind"), "schema-v2 action intent was not loaded safely");
    }

    private static void SerializationOrdering()
    {
        InsightModel first = OrderedModel(false);
        InsightModel second = OrderedModel(true);
        string firstXml = InsightModelSerialization.Serialize(first.Snapshot());
        string secondXml = InsightModelSerialization.Serialize(second.Snapshot());
        Assert(firstXml == secondXml, "equivalent models with different insertion orders serialized differently");
    }

    private static InsightModel OrderedModel(bool reverse)
    {
        InsightModel model = InsightModel.Create("ordering");
        InsightEntity[] entities =
        {
            new InsightEntity("z", "Z", manualPosition: new InsightPoint(3f, 4f)),
            new InsightEntity("a", "A", manualPosition: new InsightPoint(1f, 2f)),
            new InsightEntity("m", "M", manualPosition: new InsightPoint(2f, 3f))
        };
        if (reverse)
        {
            model.Entity(entities[2]).Entity(entities[0]).Entity(entities[1]);
            model.Relation("a", "m", "link").Relation("z", "a", "link");
            model.Metric("m", "score", 2f).Metric("a", "score", 1f);
            model.Action("m", new InsightAction("m-action", "M action", null, false));
            model.Action("a", new InsightAction("a-action", "A action", null, false));
            model.Explanation("m", Explain.Value("M", 2f)).Explanation("a", Explain.Value("A", 1f));
            model.Event(new InsightEvent("event", 1, "Event", entityIds: new[] { "a", "z" }));
        }
        else
        {
            model.Entity(entities[0]).Entity(entities[1]).Entity(entities[2]);
            model.Relation("z", "a", "link").Relation("a", "m", "link");
            model.Metric("a", "score", 1f).Metric("m", "score", 2f);
            model.Action("a", new InsightAction("a-action", "A action", null, false));
            model.Action("m", new InsightAction("m-action", "M action", null, false));
            model.Explanation("a", Explain.Value("A", 1f)).Explanation("m", Explain.Value("M", 2f));
            model.Event(new InsightEvent("event", 1, "Event", entityIds: new[] { "z", "a" }));
        }
        return model;
    }

    private static void MotionSettings()
    {
        Assert(Math.Abs(InsightMotion.Approach(0f, 10f, 0.1f, 8f, true) - 10f) < 0.001f, "reduced motion did not settle immediately");
        float value = InsightMotion.Approach(0f, 10f, 0.1f, 8f, false);
        Assert(value > 0f && value < 10f, "delta-time motion changed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool Contains(IReadOnlyList<string> messages, string text)
    {
        for (int i = 0; i < messages.Count; i++)
            if (messages[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private sealed class OverlayTestEntry
    {
        internal readonly string Id;
        internal readonly object OwnerToken;

        internal OverlayTestEntry(string id, object ownerToken)
        {
            Id = id;
            OwnerToken = ownerToken;
        }
    }
}
