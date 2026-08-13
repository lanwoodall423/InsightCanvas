using System;

namespace InsightCanvas
{
    /// <summary>Bounded lifecycle information returned by a retained semantic source.</summary>
    internal struct InsightUiSemanticPreparation
    {
        public bool SnapshotChanged;
        public bool LayoutInvalidated;
        public bool DeferredRefresh;
    }

    /// <summary>Renderer-neutral seam used by the v2 semantic element and portable tests.</summary>
    internal interface IInsightUiSemanticSource
    {
        InsightUiSemanticPreparation PrepareForLayout(InsightUiFrame frame);
        InsightUiSemanticPreparation PrepareForPaint(InsightUiFrame frame);
        InsightUiSize Measure(InsightUiConstraints constraints, InsightUiFrame frame);
        void Paint(InsightRect bounds, IInsightUiPainter painter, InsightUiFrame frame);
        void Invalidate();
        int RenderErrorCount { get; }
    }

    /// <summary>Shared retained-element lifecycle for semantic sources.</summary>
    internal sealed class InsightUiSemanticLifecycle
    {
        private readonly IInsightUiSemanticSource source;
        private bool hasPaintedBounds;
        private InsightRect lastPaintedBounds;

        public InsightUiSemanticLifecycle(IInsightUiSemanticSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public InsightUiSize Measure(string elementId, InsightUiConstraints constraints, InsightUiFrame frame)
        {
            try
            {
                InsightUiSemanticPreparation preparation = source.PrepareForLayout(frame);
                RecordPreparation(frame, preparation);
                return source.Measure(constraints, frame);
            }
            catch (Exception)
            {
                frame.Diagnostics.RecordSemanticRenderErrors(elementId, 1);
                return new InsightUiSize(Math.Max(0f, constraints.MinWidth), Math.Max(0f, constraints.MinHeight));
            }
        }

        public void Paint(string elementId, InsightRect bounds, IInsightUiPainter painter, InsightUiFrame frame)
        {
            try
            {
                InsightUiSemanticPreparation preparation = source.PrepareForPaint(frame);
                RecordPreparation(frame, preparation);
                if (hasPaintedBounds && !lastPaintedBounds.Equals(bounds))
                    frame.Diagnostics.RecordSemanticResize(elementId);
                lastPaintedBounds = bounds;
                hasPaintedBounds = true;

                int errorsBefore = source.RenderErrorCount;
                source.Paint(bounds, painter, frame);
                int errorsAfter = source.RenderErrorCount;
                if (errorsAfter > errorsBefore)
                    frame.Diagnostics.RecordSemanticRenderErrors(elementId, errorsAfter - errorsBefore);
            }
            catch (Exception)
            {
                frame.Diagnostics.RecordSemanticRenderErrors(elementId, 1);
                DrawFallback(elementId, bounds, painter, frame);
            }
        }

        public void Invalidate() => source.Invalidate();

        private static void RecordPreparation(InsightUiFrame frame, InsightUiSemanticPreparation preparation)
        {
            if (preparation.SnapshotChanged) frame.Diagnostics.RecordSemanticSnapshot();
            if (preparation.LayoutInvalidated) frame.Diagnostics.RecordSemanticLayoutInvalidation();
            if (preparation.DeferredRefresh) frame.Diagnostics.RecordSemanticDeferredRefresh();
        }

        private static void DrawFallback(string elementId, InsightRect bounds, IInsightUiPainter painter,
            InsightUiFrame frame)
        {
            if (painter == null) return;
            try
            {
                InsightUiStyle style = new InsightUiStyle
                {
                    Background = frame.Theme.ElevatedSurface,
                    Border = frame.Theme.Negative,
                    BorderWidth = 1f,
                    Padding = InsightUiPadding.All(8f)
                };
                painter.Surface(bounds, style, frame);
                painter.Text(bounds, "Insight Canvas semantic view unavailable.", InsightUiTextStyle.Caption,
                    frame.Theme.Negative, true, frame);
            }
            catch (Exception)
            {
                // The source failure is already recorded. A broken renderer must not escape the contained path.
            }
        }
    }
}
