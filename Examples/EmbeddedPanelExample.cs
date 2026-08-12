using InsightCanvas;
using UnityEngine;

namespace InsightCanvasExample
{
    /// <summary>Caller-owned Rect integration with document-local state and cleanup.</summary>
    public sealed class EmbeddedPanelExample
    {
        private readonly InsightUiHost host;
        private bool showHints = true;

        public EmbeddedPanelExample()
        {
            InsightUiStack root = InsightUi.Column("panel-root",
                InsightUi.Label("title", "Research brief", InsightUiTextStyle.Heading),
                InsightUi.Toggle("show-hints", "Show hints")
                    .Bind(() => showHints, value => showHints = value),
                InsightUi.Button("refresh", "Refresh"))
                .SetGap(8f)
                .SetPadding(12f);
            host = new InsightUiHost(new InsightUiDocument("Research panel", root));
        }

        public void Draw(Rect rect)
        {
            host.Draw(rect, Time.deltaTime);
        }

        public void Close()
        {
            host.PostClose();
        }
    }
}
