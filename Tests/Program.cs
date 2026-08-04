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
            SelectionPropagation();
            ExplanationCalculation();
            ThemeParsing();
            GraphDeterminism();
            GraphBudgeting();
            TimelineMath();
            OverlayOwnership();
            Serialization();
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
            Contains(writeReport.Warnings, "callback omitted"), "runtime serialization diagnostics are incomplete");
        Assert(writeReport.Xml == InsightModelSerialization.Serialize(original), "serialized output is not deterministic");

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
            copiedAction.CloseWindowAfterInvoke && !copiedAction.Enabled && copiedAction.Callback == null && !copiedAction.Invoke(),
            "runtime action was restored as invokable");

        InsightModelSerializationReport legacy = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel id='legacy'><entities><entity id='a' label='A'/></entities><relations/>" +
            "<metrics/><events><event id='e' tick='9' label='Old'><entity id='a'/></event></events></insightModel>");
        Assert(legacy.Succeeded && legacy.Model.Snapshot().Entity("a") != null && legacy.Model.Snapshot().Events.Count == 1 &&
            Contains(legacy.Warnings, "unversioned"), "existing unversioned model format no longer loads");
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
