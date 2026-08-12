using InsightCanvas;

namespace InsightCanvasExample
{
    /// <summary>Renderer-neutral code-drawn preview using an optional public capability.</summary>
    public static class CustomDrawingExample
    {
        public static InsightUiElement CreatePreview()
        {
            return InsightUi.Custom("preview", context =>
            {
                IInsightUiCustomPainter canvas = context.Painter as IInsightUiCustomPainter;
                if (canvas == null) return;
                canvas.FillRect(context.Bounds, context.Frame.Theme.Surface, context.Frame);
                canvas.Line(context.Bounds.X, context.Bounds.Bottom,
                    context.Bounds.Right, context.Bounds.Y,
                    context.Frame.Theme.Selected, 2f, context.Frame);
            }, (constraints, frame) => new InsightUiSize(160f, 64f));
        }
    }
}
