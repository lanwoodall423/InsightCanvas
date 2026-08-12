using InsightCanvas;
using Verse;

namespace InsightCanvasExample
{
    /// <summary>Smallest complete public-API Window integration.</summary>
    public static class MinimalWindowExample
    {
        public static void Open()
        {
            InsightUiStack root = InsightUi.Column("settings-root",
                InsightUi.Label("title", "Colony settings", InsightUiTextStyle.Title),
                InsightUi.Surface("settings-card", InsightUi.Column("settings-body",
                    InsightUi.Toggle("show-hints", "Show hints"),
                    InsightUi.Button("apply", "Apply"))))
                .SetGap(10f)
                .SetPadding(12f);

            InsightUiDocument document = new InsightUiDocument("Colony settings", root);
            Find.WindowStack.Add(new InsightUiWindow(document));
        }
    }
}
