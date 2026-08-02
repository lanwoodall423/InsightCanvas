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
            ModelClear();
            LayoutMath();
            SelectionPropagation();
            ExplanationCalculation();
            ThemeParsing();
            GraphDeterminism();
            TimelineMath();
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
        InsightModel model = InsightModel.Create("graph").Entity("b", "B").Entity("a", "A").Entity("c", "C").Entity("d", "D")
            .Relation("a", "b", "knows").Relation("b", "c", "feeds").Relation("c", "a", "cycles");
        InsightGraphLayoutResult first = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        InsightGraphLayoutResult second = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        Assert(first.Complete && first.Position("a").Equals(second.Position("a")) && first.Position("d").Equals(second.Position("d")) &&
            InsightGraphLayout.AreNeighbors(model.Snapshot(), "a", "b"), "graph layout is not deterministic");
    }

    private static void TimelineMath()
    {
        List<InsightEvent> events = new List<InsightEvent>
        {
            new InsightEvent("1", 10, "A"), new InsightEvent("2", 10, "B"), new InsightEvent("3", 100, "C")
        };
        InsightTimeRange range = InsightTimelineMath.Bounds(events);
        IReadOnlyList<InsightTimelineCluster> clusters = InsightTimelineMath.Cluster(events, range, 100f, 20);
        Assert(clusters.Count == 2 && clusters[0].Count == 2, "timeline clustering changed");
        Assert(InsightTimelineMath.Zoom(range, 2f, 55).End - InsightTimelineMath.Zoom(range, 2f, 55).Start < range.End - range.Start, "timeline zoom changed");
    }

    private static void Serialization()
    {
        InsightModel model = InsightModel.Create("roundtrip").Entity("a", "A").Entity("b", "B").Relation("a", "b", "contains")
            .Metric("a", "value", new InsightMetric("value", 0.75f, new InsightRange(0.2f, 0.9f), true, true, 0.8f, 0.5f, InsightTrend.Rising,
                new[] { new InsightSample(1, 0.4f), new InsightSample(2, 0.75f) }))
            .Event(new InsightEvent("event", 12, "Changed", "test", new[] { "a" }));
        string xml = InsightModelSerialization.Serialize(model.Snapshot());
        InsightModel copy = InsightModelSerialization.Deserialize(xml);
        Assert(copy.Snapshot().Entities.Count == 2 && copy.Snapshot().Relations.Count == 1 && copy.Snapshot().Events.Count == 1 &&
            Math.Abs(copy.Snapshot().MetricsFor("a")[0].Value - 0.75f) < 0.001f && copy.Snapshot().MetricsFor("a")[0].History.Count == 2,
            "model serialization changed");
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
}
