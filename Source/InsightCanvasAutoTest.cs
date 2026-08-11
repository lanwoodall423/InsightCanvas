using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>
    /// Runs a small installed-mod smoke test when DevBridge2 launches RimWorld with -quicktest.
    /// The component is inert during normal launches because DEVBRIDGE_ROOT is not present.
    /// </summary>
    public sealed class InsightCanvasAutoTestComponent : GameComponent
    {
        private const int WindowWaitTicks = 300;
        private bool completed;
        private bool started;
        private int waitedTicks;
        private InsightUiWindow window;
        private InsightUiTabs showcaseTabs;
        private int nextShowcaseTab;

        public InsightCanvasAutoTestComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (completed)
                return;

            string runtimeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            if (string.IsNullOrWhiteSpace(runtimeRoot))
            {
                completed = true;
                return;
            }

            if (!GenScene.InPlayScene || Current.Game == null || Find.CurrentMap == null || Find.TickManager == null)
                return;

            if (!started)
            {
                started = true;
                try
                {
                    WriteResult(runtimeRoot, "RUNNING", "map loaded; starting installed-mod smoke test");
                    RunDataChecks();
                    window = InsightFeatureShowcase.CreateWindow();
                    Require(window != null && window.Document != null && window.Document.Root != null,
                        "feature showcase window did not create a composable document");
                    showcaseTabs = FindElement(window.Document.Root, "showcase-tabs") as InsightUiTabs;
                    Require(showcaseTabs != null && showcaseTabs.Tabs.Count == 5,
                        "feature showcase did not create its five documented pages");
                    nextShowcaseTab = 1;
                    if (Find.WindowStack == null)
                        throw new InvalidOperationException("RimWorld window stack was unavailable");
                    Find.WindowStack.Add(window);
                }
                catch (Exception exception)
                {
                    Fail(runtimeRoot, exception);
                }
                return;
            }

            if (window != null && window.Document.Diagnostics.Frame > 0)
            {
                try
                {
                    Require(window.Document.Diagnostics.ArrangePasses > 0,
                        "feature showcase did not complete an arrange pass");
                    Require(window.Document.Diagnostics.VisibleElements > 0,
                        "feature showcase did not paint a visible element");
                    Require(window.Document.Diagnostics.RenderErrors == 0,
                        "feature showcase reported " + window.Document.Diagnostics.RenderErrors + " render error(s)");
                    if (nextShowcaseTab < showcaseTabs.Tabs.Count)
                    {
                        showcaseTabs.Select(showcaseTabs.Tabs[nextShowcaseTab].Id);
                        nextShowcaseTab++;
                        window.Document.Invalidate();
                        return;
                    }
                    window.Close(false);
                    completed = true;
                    WriteResult(runtimeRoot, "PASS", "map-load smoke test passed; Feature Showcase rendered successfully");
                    Log.Message("[InsightCanvas AutoTest] PASS: map-load smoke test completed.");
                }
                catch (Exception exception)
                {
                    Fail(runtimeRoot, exception);
                }
                return;
            }

            waitedTicks++;
            if (waitedTicks > WindowWaitTicks)
                Fail(runtimeRoot, new TimeoutException("Feature Showcase did not render within " + WindowWaitTicks + " game ticks"));
        }

        private static void RunDataChecks()
        {
            InsightModel model = InsightLaboratory.CreateDemoModel();
            InsightModelValidation validation = model.Validate();
            Require(validation.IsValid, "laboratory demo model was invalid: " + string.Join("; ", validation.Errors));

            InsightModelSnapshot snapshot = model.Snapshot();
            Require(snapshot.Entities.Count == 10 && snapshot.Relations.Count == 10 && snapshot.Events.Count == 7,
                "laboratory demo model counts changed");

            InsightGraphLayoutResult layout = InsightGraphLayout.Compute(snapshot, 720f, 480f, 180, 360, 4);
            Require(layout.ActiveNodeCount == snapshot.Entities.Count && layout.ActiveEdgeCount <= 360,
                "demo graph layout did not include the expected active nodes");
            InsightGraphFit fit = InsightGraphViewport.Fit(layout, 720f, 480f, 32f);
            Require(fit.Zoom >= 0.25f && fit.Zoom <= 2.8f && !float.IsNaN(fit.Pan.X) && !float.IsNaN(fit.Pan.Y),
                "demo graph fit returned an invalid viewport transform");

            AssertHeader(new InsightRect(0f, 0f, 480f, 43f));
            AssertHeader(new InsightRect(0f, 0f, 720f, 43f));
            AssertHeader(new InsightRect(0f, 0f, 1280f, 43f));

            InsightMapReference center = InsightMapBridge.ForCell(Find.CurrentMap, Find.CurrentMap.Center);
            InsightAction flash = InsightMapBridge.Flash("insight-autotest-flash", center, 1f);
            Require(flash.Enabled, "map flash action was not enabled for the loaded map");
            flash.Invoke();
            InsightMapBridge.Clear();

            InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
                new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
            InsightUiStack row = InsightUi.Row("autotest-row",
                InsightUi.Surface("autotest-fixed", InsightUi.Label("autotest-fixed-label", "fixed")).SetWidth(InsightLength.Fixed(96f)),
                InsightUi.Surface("autotest-flex", InsightUi.Label("autotest-flex-label", "flex")).SetFlex(1f));
            row.SetGap(8f);
            row.Measure(new InsightUiConstraints(0f, 420f, 0f, 120f), frame);
            row.Arrange(new InsightRect(0f, 0f, 420f, 120f), frame);
            Require(row.Children[1].LayoutRect.Width > row.Children[0].LayoutRect.Width,
                "composable UI flex allocation was not applied");
            InsightVirtualizedRange visible = InsightVirtualization.Range(400, 32f, 240f, 640f, 2);
            Require(visible.Contains(20) && visible.End < 400, "composable UI virtualization range was invalid");

            InsightUiDocument first = new InsightUiDocument("autotest-first", InsightUi.Empty("root"));
            InsightUiDocument second = new InsightUiDocument("autotest-second", InsightUi.Empty("root"));
            first.State.SetBool("selected", true);
            Require(first.State.GetBool("selected") && !second.State.GetBool("selected"),
                "composable UI state leaked between documents");
        }

        private static void AssertHeader(InsightRect header)
        {
            InsightRect disclosure = InsightHeaderLayout.DisclosureControls(header);
            InsightRect tools = InsightHeaderLayout.ToolsButton(header);
            InsightRect reset = InsightHeaderLayout.ResetButton(header);
            Require(disclosure.Width >= 0f && disclosure.Right <= tools.X - 8f &&
                tools.Right <= reset.X - 8f && reset.Right <= header.Right - 8f,
                "header controls overlap at width " + header.Width);
        }

        private void Fail(string runtimeRoot, Exception exception)
        {
            completed = true;
            try
            {
                if (window != null)
                    window.Close(false);
            }
            catch
            {
                // Preserve the original failure in the result file.
            }

            string message = exception.GetType().Name + ": " + exception.Message;
            WriteResult(runtimeRoot, "FAIL", message);
            Log.Error("[InsightCanvas AutoTest] FAIL: " + message + "\n" + exception);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static InsightUiElement FindElement(InsightUiElement root, string id)
        {
            if (root == null) return null;
            if (root.Id == id) return root;
            IReadOnlyList<InsightUiElement> children = root.Children;
            for (int i = 0; i < children.Count; i++)
            {
                InsightUiElement match = FindElement(children[i], id);
                if (match != null) return match;
            }
            return null;
        }

        private static void WriteResult(string runtimeRoot, string status, string message)
        {
            try
            {
                string runtime = Path.Combine(Path.GetFullPath(runtimeRoot), "Runtime");
                Directory.CreateDirectory(runtime);
                string path = Path.Combine(runtime, "insightcanvas-autotest.json");
                string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                string json = "{\n" +
                    "  \"status\": \"" + Escape(status) + "\",\n" +
                    "  \"message\": \"" + Escape(message) + "\",\n" +
                    "  \"generation\": \"" + Escape(Environment.GetEnvironmentVariable("DEVBRIDGE_GENERATION")) + "\",\n" +
                    "  \"processId\": " + ProcessId() + "\n" +
                    "}";
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, null);
                        return;
                    }
                    catch
                    {
                        File.Delete(path);
                    }
                }
                File.Move(temporary, path);
            }
            catch (Exception exception)
            {
                Log.Warning("[InsightCanvas AutoTest] Could not write result: " + exception.Message);
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string ProcessId()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        }
    }
}
