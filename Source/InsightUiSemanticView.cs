using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace InsightCanvas
{
    /// <summary>
    /// Retained v2 element that hosts an existing InsightModel/InsightView pair with caller-owned interaction state.
    /// The model snapshot is refreshed during Measure when the model revision changes; Paint only consumes that
    /// immutable snapshot. Semantic views are runtime composition objects and are intentionally excluded from model
    /// serialization together with views, contexts, delegates, map references, and live game objects.
    /// </summary>
    public sealed class InsightUiSemanticView : InsightUiElement
    {
        private readonly InsightUiSemanticSource source;
        private readonly InsightUiSemanticLifecycle lifecycle;

        public InsightUiSemanticView(string id, InsightModel model, InsightView view, InsightContext context = null)
            : base(id)
        {
            source = new InsightUiSemanticSource(model, view, context);
            lifecycle = new InsightUiSemanticLifecycle(source);
        }

        /// <summary>Gets the retained mutable model supplied by the caller.</summary>
        public InsightModel Model => source.Model;

        /// <summary>Gets the retained semantic component view supplied by the caller.</summary>
        public InsightView View => source.View;

        /// <summary>Gets the exact caller-owned interaction context used by this element.</summary>
        public InsightContext Context => source.Context;

        /// <summary>Gets the immutable snapshot currently safe for the paint phase, or null before first Measure.</summary>
        public InsightModelSnapshot Snapshot => source.Snapshot;

        /// <summary>Gets the model revision represented by Snapshot, or -1 before first Measure.</summary>
        public int SnapshotRevision => source.SnapshotRevision;

        /// <summary>Gets semantic diagnostics retained for this element.</summary>
        public InsightDiagnostics Diagnostics => source.Diagnostics;

        /// <summary>Replaces the retained model; the new snapshot is deferred until the next Measure phase.</summary>
        public InsightUiSemanticView ReplaceModel(InsightModel model)
        {
            source.ReplaceModel(model);
            return this;
        }

        /// <summary>Replaces the retained component view; its layout is rebuilt on the next Measure phase.</summary>
        public InsightUiSemanticView ReplaceView(InsightView view)
        {
            source.ReplaceView(view);
            return this;
        }

        /// <summary>Replaces the retained interaction context without copying or serializing its state.</summary>
        public InsightUiSemanticView ReplaceContext(InsightContext context)
        {
            source.ReplaceContext(context);
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return lifecycle.Measure(Id, constraints, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            lifecycle.Paint(Id, LayoutRect, painter, frame);
        }

        protected override bool StateBearing => true;

        public override void Invalidate()
        {
            source.Invalidate();
            base.Invalidate();
        }
    }

    /// <summary>Factory additions for retained semantic elements in composable v2 documents.</summary>
    public static partial class InsightUi
    {
        /// <summary>
        /// Creates a stable-ID semantic element. Null inputs receive the same safe defaults as the v1 host, while a
        /// non-null context is retained by reference so selection, disclosure, focus, filter, and time-range changes
        /// remain shared with the caller.
        /// </summary>
        public static InsightUiSemanticView SemanticView(string id, InsightModel model, InsightView view,
            InsightContext context = null)
        {
            return new InsightUiSemanticView(id, model, view, context);
        }
    }

    /// <summary>Retained source adapter kept separate from the composable element and renderer boundary.</summary>
    internal sealed class InsightUiSemanticSource : IInsightUiSemanticSource
    {
        private InsightModel model;
        private InsightView view;
        private InsightContext context;
        private InsightModelSnapshot snapshot;
        private InsightRenderContext renderContext;
        private int snapshotRevision = -1;
        private int observedContextRevision = -1;
        private int diagnosticsFrame = -1;
        private bool pendingLayoutInvalidation = true;

        public InsightUiSemanticSource(InsightModel model, InsightView view, InsightContext context)
        {
            this.model = model ?? InsightModel.Create("Insight Canvas");
            this.view = view ?? InsightView.Create();
            this.context = context ?? new InsightContext();
            Diagnostics = new InsightDiagnostics();
        }

        public InsightModel Model => model;
        public InsightView View => view;
        public InsightContext Context => context;
        public InsightModelSnapshot Snapshot => snapshot;
        public int SnapshotRevision => snapshotRevision;
        public InsightDiagnostics Diagnostics { get; private set; }
        public int RenderErrorCount => Diagnostics.RenderErrors;

        public void ReplaceModel(InsightModel value)
        {
            value = value ?? InsightModel.Create("Insight Canvas");
            if (ReferenceEquals(model, value)) return;
            model = value;
            snapshot = null;
            snapshotRevision = -1;
            renderContext = null;
            pendingLayoutInvalidation = true;
        }

        public void ReplaceView(InsightView value)
        {
            value = value ?? InsightView.Create();
            if (ReferenceEquals(view, value)) return;
            view = value;
            renderContext = null;
            pendingLayoutInvalidation = true;
        }

        public void ReplaceContext(InsightContext value)
        {
            value = value ?? new InsightContext();
            if (ReferenceEquals(context, value)) return;
            context = value;
            observedContextRevision = -1;
            renderContext = null;
            pendingLayoutInvalidation = true;
        }

        public InsightUiSemanticPreparation PrepareForLayout(InsightUiFrame frame)
        {
            RegisterFrame(frame);
            bool snapshotChanged = false;
            bool layoutInvalidated = pendingLayoutInvalidation;
            pendingLayoutInvalidation = false;

            if (snapshot == null || snapshotRevision != model.Revision)
            {
                Stopwatch timer = Stopwatch.StartNew();
                InsightModelSnapshot next = model.Snapshot();
                timer.Stop();
                snapshot = next;
                snapshotRevision = next == null ? model.Revision : next.Revision;
                Diagnostics.LastSnapshotMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
                snapshotChanged = true;
                layoutInvalidated = true;
                renderContext = null;
            }

            if (observedContextRevision != context.Revision)
            {
                if (observedContextRevision >= 0) layoutInvalidated = true;
                observedContextRevision = context.Revision;
            }

            if (layoutInvalidated)
            {
                view.Invalidate();
                Diagnostics.Invalidate();
            }
            return new InsightUiSemanticPreparation
            {
                SnapshotChanged = snapshotChanged,
                LayoutInvalidated = layoutInvalidated
            };
        }

        public InsightUiSemanticPreparation PrepareForPaint(InsightUiFrame frame)
        {
            RegisterFrame(frame);
            bool deferred = snapshot == null || snapshotRevision != model.Revision ||
                observedContextRevision != context.Revision;
            if (deferred) pendingLayoutInvalidation = true;
            return new InsightUiSemanticPreparation { DeferredRefresh = deferred };
        }

        public InsightUiSize Measure(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float height = 0f;
            IReadOnlyList<IInsightComponent> components = view.Components;
            for (int i = 0; i < components.Count; i++)
            {
                if (i > 0) height += frame.Spacing(frame.Theme.Spacing);
                height += Math.Max(0f, components[i]?.MinimumHeight ?? 0f);
            }
            float width = float.IsPositiveInfinity(constraints.MaxWidth) ? constraints.MinWidth : constraints.MaxWidth;
            return constraints.Constrain(new InsightUiSize(width, height));
        }

        public void Paint(InsightRect bounds, IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (snapshot == null)
                throw new InvalidOperationException("InsightUiSemanticView must be measured before it is painted.");
            if (!(painter is RimWorldInsightUiPainter))
                throw new InvalidOperationException("InsightUiSemanticView requires the Insight Canvas RimWorld painter at the renderer boundary.");

            if (renderContext == null)
                renderContext = new InsightRenderContext(snapshot, context, frame.Theme, Diagnostics, null,
                    frame.OverlayOwnerToken, frame.DeltaTime);
            renderContext.Update(snapshot, context, frame.Theme, Diagnostics, null, frame.OverlayOwnerToken,
                frame.DeltaTime, frame.Density, frame.HighContrast, frame.ReducedMotion, frame.HostBounds);

            using (new InsightGuiStateScope())
            {
                view.Draw(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), renderContext);
            }
        }

        public void Invalidate()
        {
            pendingLayoutInvalidation = true;
        }

        private void RegisterFrame(InsightUiFrame frame)
        {
            frame.RegisterSemanticContext(context);
            if (diagnosticsFrame == frame.Diagnostics.Frame) return;
            Diagnostics.BeginFrame();
            diagnosticsFrame = frame.Diagnostics.Frame;
        }
    }
}
