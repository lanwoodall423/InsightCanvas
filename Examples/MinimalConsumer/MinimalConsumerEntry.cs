using InsightCanvas;
using Verse;

namespace InsightCanvasExample
{
    /// <summary>Independent consumer compiled against the released-style framework DLL.</summary>
    public static class MinimalConsumerEntry
    {
        public static void Open()
        {
            InsightUiElement root = InsightUi.Column("consumer-root",
                InsightUi.Label("consumer-title", "Consumer settings", InsightUiTextStyle.Title),
                InsightUi.Button("consumer-apply", "Apply"));
            Find.WindowStack.Add(new InsightUiWindow(new InsightUiDocument("Consumer settings", root)));
        }
    }
}
