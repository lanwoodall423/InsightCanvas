using System;
using InsightCanvas;
using UnityEngine;
using Verse;

namespace InsightCanvasExample
{
    /// <summary>Small v2 adapter example; copy either entry point into a consumer mod.</summary>
    public sealed class MinimalIntegrationPanel
    {
        private readonly InsightUiDocument document;
        private readonly InsightUiHost host;

        public MinimalIntegrationPanel()
        {
            InsightUiStack root = InsightUi.Column("example-root").SetGap(8f) as InsightUiStack;
            root.Add(InsightUi.Label("example-title", "Colony signals", InsightUiTextStyle.Heading),
                InsightUi.Surface("example-card", InsightUi.Column("example-card-body").SetGap(6f).Add(
                    InsightUi.Label("example-copy", "This panel does not require InsightModel or a fixed semantic dashboard.", InsightUiTextStyle.Body),
                    InsightUi.Progress("example-progress", 0.68f, InsightTheme.Default.Selected),
                    InsightUi.Row("example-actions").SetGap(6f).Add(
                        InsightUi.Button("example-refresh", "Refresh", Refresh),
                        InsightUi.IconButton("example-help", "?", ShowHelp))));
            document = new InsightUiDocument("Example embedded panel", root);
            host = new InsightUiHost(document);
        }

        public void Draw(Rect rect)
        {
            host.Draw(rect, Time.deltaTime);
        }

        public void Close()
        {
            host.PostClose();
        }

        private void Refresh() => Log.Message("Example panel refreshed.");
        private void ShowHelp() => Log.Message("Example panel help requested.");
    }

    public static class MinimalIntegrationExample
    {
        public static void OpenWindow()
        {
            InsightUiElement root = InsightUi.Column("example-window-root",
                InsightUi.Label("example-window-title", "Research brief", InsightUiTextStyle.Title),
                InsightUi.Label("example-window-copy", "The same public element tree can be hosted by an ordinary RimWorld Window.", InsightUiTextStyle.Body),
                InsightUi.Grid("example-window-grid", 180f)
                    .Add(InsightUi.Badge("example-ready", "Ready", InsightTheme.Default.Positive),
                        InsightUi.Progress("example-confidence", 0.82f, InsightTheme.Default.Selected)))
                .SetGap(10f)
                .SetPadding(12f);
            InsightUiDocument document = new InsightUiDocument("Example Window", root);
            Find.WindowStack.Add(new InsightUiWindow(document));
        }
    }
}
