using System;
using System.Collections.Generic;
using System.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Hints used by the stock view to coordinate a responsive component arrangement.</summary>
    public enum InsightComponentRole
    {
        Generic,
        Cards,
        Constellation,
        Explanation,
        Timeline
    }

    /// <summary>Per-frame counters exposed by the optional semantic views and available to integrations.</summary>
    public sealed class InsightDiagnostics
    {
        private readonly Stopwatch frameTimer = new Stopwatch();
        private readonly Stopwatch layoutTimer = new Stopwatch();
        private bool layoutRunning;

        public int Frame { get; private set; }
        public int LayoutPasses { get; private set; }
        public int VisibleElements { get; set; }
        public int CacheInvalidations { get; private set; }
        public int ApproximateAllocations { get; set; }
        /// <summary>Gets contained component render failures from the latest semantic frame.</summary>
        public int RenderErrors { get; private set; }
        public float LastLayoutMilliseconds { get; private set; }
        public float LastDrawMilliseconds { get; private set; }
        public float LastSnapshotMilliseconds { get; set; }

        public void BeginFrame()
        {
            Frame++;
            VisibleElements = 0;
            ApproximateAllocations = 0;
            RenderErrors = 0;
            frameTimer.Restart();
        }

        public void BeginLayout()
        {
            layoutTimer.Restart();
            layoutRunning = true;
        }

        public void EndLayout()
        {
            if (!layoutRunning) return;
            layoutTimer.Stop();
            LastLayoutMilliseconds = (float)layoutTimer.Elapsed.TotalMilliseconds;
            LayoutPasses++;
            layoutRunning = false;
        }

        public void Invalidate() => CacheInvalidations++;

        public void EndFrame()
        {
            if (!frameTimer.IsRunning) return;
            frameTimer.Stop();
            LastDrawMilliseconds = (float)frameTimer.Elapsed.TotalMilliseconds;
        }

        public string Summary()
        {
            return "layout " + LastLayoutMilliseconds.ToString("0.0") + " ms | draw " + LastDrawMilliseconds.ToString("0.0") +
                " ms | visible " + VisibleElements + " | invalidations " + CacheInvalidations + " | approx alloc " + ApproximateAllocations +
                " | errors " + RenderErrors;
        }

        internal void RecordRenderError() => RenderErrors = Math.Min(64, RenderErrors + 1);
    }

    /// <summary>Services passed to components without exposing their Unity renderer implementation.</summary>
    public sealed class InsightRenderContext
    {
        public InsightModelSnapshot Snapshot { get; private set; }
        public InsightContext Interaction { get; private set; }
        public InsightTheme Theme { get; private set; }
        public InsightDiagnostics Diagnostics { get; private set; }
        public InsightWindow Window { get; private set; }
        internal object OverlayOwnerToken { get; private set; }
        public float DeltaTime { get; private set; }
        /// <summary>Gets the document density inherited by a v2 semantic element.</summary>
        public InsightUiDensity Density { get; private set; }
        /// <summary>Gets whether the enclosing document requests high-contrast rendering.</summary>
        public bool HighContrast { get; private set; }
        /// <summary>Gets whether the enclosing document requests reduced motion.</summary>
        public bool ReducedMotion { get; private set; }
        /// <summary>Gets the enclosing document bounds.</summary>
        public InsightRect HostBounds { get; private set; }

        internal InsightRenderContext(InsightModelSnapshot snapshot, InsightContext interaction, InsightTheme theme,
            InsightDiagnostics diagnostics, InsightWindow window, object overlayOwnerToken, float deltaTime)
        {
            Update(snapshot, interaction, theme, diagnostics, window, overlayOwnerToken, deltaTime);
        }

        internal void Update(InsightModelSnapshot snapshot, InsightContext interaction, InsightTheme theme,
            InsightDiagnostics diagnostics, InsightWindow window, object overlayOwnerToken, float deltaTime)
        {
            Snapshot = snapshot;
            Interaction = interaction;
            Theme = theme;
            Diagnostics = diagnostics;
            Window = window;
            OverlayOwnerToken = overlayOwnerToken;
            DeltaTime = deltaTime;
            Density = InsightUiDensity.Normal;
            HighContrast = false;
            ReducedMotion = false;
            HostBounds = new InsightRect(0f, 0f, 0f, 0f);
        }

        internal void Update(InsightModelSnapshot snapshot, InsightContext interaction, InsightTheme theme,
            InsightDiagnostics diagnostics, InsightWindow window, object overlayOwnerToken, float deltaTime,
            InsightUiDensity density, bool highContrast, bool reducedMotion, InsightRect hostBounds)
        {
            Update(snapshot, interaction, theme, diagnostics, window, overlayOwnerToken, deltaTime);
            Density = density;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
            HostBounds = hostBounds;
        }
    }

    /// <summary>Public component contract for custom retained views.</summary>
    public interface IInsightComponent
    {
        string ComponentId { get; }
        InsightComponentRole Role { get; }
        float MinimumHeight { get; }
        void Draw(Rect rect, InsightRenderContext context);
        void Invalidate();
    }

    /// <summary>Convenient base for custom components with stable ids and invalidation.</summary>
    public abstract class InsightComponentBase : IInsightComponent
    {
        private bool invalidated = true;

        protected InsightComponentBase(string componentId, InsightComponentRole role = InsightComponentRole.Generic, float minimumHeight = 80f)
        {
            ComponentId = string.IsNullOrWhiteSpace(componentId) ? GetType().Name : componentId;
            Role = role;
            MinimumHeight = Math.Max(32f, minimumHeight);
        }

        public string ComponentId { get; private set; }
        public InsightComponentRole Role { get; private set; }
        public float MinimumHeight { get; private set; }
        protected bool IsInvalidated => invalidated;

        public void Draw(Rect rect, InsightRenderContext context)
        {
            using (new InsightGuiStateScope())
            {
                DrawComponent(rect, context);
                invalidated = false;
            }
        }

        public virtual void Invalidate() => invalidated = true;

        protected abstract void DrawComponent(Rect rect, InsightRenderContext context);
    }

    /// <summary>Retained component collection and responsive coordinator.</summary>
    public sealed class InsightView
    {
        private readonly List<IInsightComponent> components = new List<IInsightComponent>();
        private readonly InsightLayoutCache layoutCache = new InsightLayoutCache();
        private readonly List<Rect> arrangedRects = new List<Rect>();
        private int layoutRevision;
        private int arrangedRevision = -1;
        private float arrangedWidth = -1f;
        private float arrangedHeight = -1f;

        public static InsightView Create() => new InsightView();
        public IReadOnlyList<IInsightComponent> Components => components;

        /// <summary>Adds a component with a stable id. Duplicate ids are ignored to keep cache keys deterministic.</summary>
        public InsightView Add(IInsightComponent component)
        {
            if (component == null || Find(component.ComponentId) != null) return this;
            components.Add(component);
            layoutRevision++;
            return this;
        }

        public IInsightComponent Find(string componentId)
        {
            for (int i = 0; i < components.Count; i++)
                if (components[i].ComponentId == componentId) return components[i];
            return null;
        }

        public void Invalidate()
        {
            layoutRevision++;
            layoutCache.InvalidateAll();
            for (int i = 0; i < components.Count; i++) components[i].Invalidate();
        }

        internal void Draw(Rect rect, InsightRenderContext context)
        {
            if (components.Count == 0)
            {
                Widgets.Label(rect, "InsightCanvas_NoComponents".Translate());
                return;
            }
            context.Diagnostics.BeginLayout();
            IReadOnlyList<Rect> rects = Arrange(rect, context.Theme.Spacing);
            context.Diagnostics.EndLayout();
            for (int i = 0; i < components.Count; i++)
            {
                if (rects[i].width <= 1f || rects[i].height <= 1f) continue;
                try { components[i].Draw(rects[i], context); }
                catch (Exception exception)
                {
                    context.Diagnostics.RecordRenderError();
                    Log.ErrorOnce("[Insight Canvas] Component '" + components[i].ComponentId + "' failed: " + exception.Message,
                        ("insight-component:" + components[i].ComponentId).GetHashCode());
                    InsightDraw.Panel(rects[i], context.Theme);
                    GUI.color = InsightDraw.Color(context.Theme.Negative);
                    Widgets.Label(rects[i].ContractedBy(8f), "Insight Canvas component unavailable.");
                }
            }
        }

        private IReadOnlyList<Rect> Arrange(Rect rect, float gap)
        {
            if (arrangedRevision == layoutRevision && Math.Abs(arrangedWidth - rect.width) < 0.01f &&
                Math.Abs(arrangedHeight - rect.height) < 0.01f && arrangedRects.Count == components.Count) return arrangedRects;
            IReadOnlyList<InsightLayoutBox> boxes = layoutCache.Get("view", layoutRevision, rect.width, rect.height, () =>
            {
                List<InsightLayoutBox> result = new List<InsightLayoutBox>();
                if (components.Count == 1)
                {
                    result.Add(new InsightLayoutBox("0", new InsightRect(0f, 0f, rect.width, rect.height)));
                }
                else if (HasCoordinatedRoles())
                {
                    float topHeight = Math.Min(204f, Math.Max(150f, rect.height * 0.29f));
                    float bottomHeight = Math.Max(0f, rect.height - topHeight - gap);
                    result.Add(new InsightLayoutBox("0", new InsightRect(0f, 0f, rect.width, topHeight)));
                    if (components.Count == 4)
                    {
                        float timelineHeight = Math.Min(176f, Math.Max(120f, bottomHeight * 0.38f));
                        float middleHeight = Math.Max(0f, bottomHeight - timelineHeight - gap);
                        float leftWidth = Math.Max(0f, rect.width * 0.57f - gap * 0.5f);
                        float rightWidth = Math.Max(0f, rect.width - leftWidth - gap);
                        result.Add(new InsightLayoutBox("1", new InsightRect(0f, topHeight + gap, leftWidth, middleHeight)));
                        result.Add(new InsightLayoutBox("2", new InsightRect(leftWidth + gap, topHeight + gap, rightWidth, middleHeight)));
                        result.Add(new InsightLayoutBox("3", new InsightRect(0f, topHeight + gap + middleHeight + gap, rect.width, timelineHeight)));
                    }
                    else if (components.Count > 4)
                    {
                        float bottomRowHeight = Math.Min(176f, Math.Max(120f, bottomHeight * 0.38f));
                        float middleHeight = Math.Max(0f, rect.height - topHeight - bottomRowHeight - gap * 2f);
                        float leftWidth = Math.Max(0f, rect.width * 0.57f - gap * 0.5f);
                        float rightWidth = Math.Max(0f, rect.width - leftWidth - gap);
                        result.Add(new InsightLayoutBox("1", new InsightRect(0f, topHeight + gap, leftWidth, middleHeight)));
                        result.Add(new InsightLayoutBox("2", new InsightRect(leftWidth + gap, topHeight + gap, rightWidth, middleHeight)));
                        int bottomCount = components.Count - 3;
                        float bottomWidth = Math.Max(0f, (rect.width - gap * (bottomCount - 1)) / bottomCount);
                        for (int i = 0; i < bottomCount; i++)
                            result.Add(new InsightLayoutBox((i + 3).ToString(), new InsightRect(i * (bottomWidth + gap),
                                topHeight + gap + middleHeight + gap, bottomWidth, bottomRowHeight)));
                    }
                    else
                    {
                        float width = (rect.width - gap * (components.Count - 2)) / Math.Max(1, components.Count - 1);
                        for (int i = 1; i < components.Count; i++)
                            result.Add(new InsightLayoutBox(i.ToString(), new InsightRect((i - 1) * (width + gap), topHeight + gap, width, bottomHeight)));
                    }
                }
                else
                {
                    float rowHeight = Math.Max(0f, (rect.height - gap * (components.Count - 1)) / components.Count);
                    for (int i = 0; i < components.Count; i++)
                        result.Add(new InsightLayoutBox(i.ToString(), new InsightRect(0f, i * (rowHeight + gap), rect.width, rowHeight)));
                }
                return result;
            });
            arrangedRects.Clear();
            for (int i = 0; i < components.Count; i++)
            {
                InsightLayoutBox box = i < boxes.Count ? boxes[i] : new InsightLayoutBox(i.ToString(), new InsightRect(0f, 0f, 0f, 0f));
                arrangedRects.Add(new Rect(rect.x + box.Rect.X, rect.y + box.Rect.Y, box.Rect.Width, box.Rect.Height));
            }
            arrangedRevision = layoutRevision;
            arrangedWidth = rect.width;
            arrangedHeight = rect.height;
            return arrangedRects;
        }

        private bool HasCoordinatedRoles()
        {
            if (components.Count < 2) return false;
            for (int i = 0; i < components.Count; i++)
                if (components[i].Role != InsightComponentRole.Generic) return true;
            return false;
        }
    }

    /// <summary>Restores the global IMGUI state changed by a component.</summary>
    public sealed class InsightGuiStateScope : IDisposable
    {
        private readonly Color color = GUI.color;
        private readonly Color contentColor = GUI.contentColor;
        private readonly Color backgroundColor = GUI.backgroundColor;
        private readonly Matrix4x4 matrix = GUI.matrix;
        private readonly bool enabled = GUI.enabled;
        private readonly GameFont font = Text.Font;
        private readonly TextAnchor anchor = Text.Anchor;
        private readonly bool wordWrap = Text.WordWrap;
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GUI.color = color;
            GUI.contentColor = contentColor;
            GUI.backgroundColor = backgroundColor;
            GUI.matrix = matrix;
            GUI.enabled = enabled;
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
        }
    }

    /// <summary>Small code-drawn renderer; all colors come from semantic theme tokens.</summary>
    public static class InsightDraw
    {
        private static readonly Dictionary<string, Texture2D> panelTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static Color Color(InsightColor value) => new Color(value.R, value.G, value.B, value.A);

        public static void Panel(Rect rect, InsightTheme theme, bool elevated = false)
        {
            if (theme.Shadow.A > 0.001f) Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), Color(theme.Shadow));
            Texture2D texture = PanelTexture(theme.PanelTexturePath);
            if (texture != null) Widgets.DrawTextureFitted(rect, texture, 1f);
            else Widgets.DrawBoxSolid(rect, Color(elevated ? theme.ElevatedSurface : theme.Surface));
            Widgets.DrawBox(rect, 1);
        }

        public static void Header(Rect rect, InsightTheme theme, string title, string subtitle = null)
        {
            Panel(rect, theme, true);
            Text.Font = GameFont.Medium;
            GUI.color = Color(theme.PrimaryText);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 30f), title ?? string.Empty);
            Text.Font = GameFont.Tiny;
            GUI.color = Color(theme.SecondaryText);
            if (!string.IsNullOrEmpty(subtitle)) Widgets.Label(new Rect(rect.x + 10f, rect.y + 31f, rect.width - 20f, 17f), subtitle);
            Text.Font = GameFont.Small;
        }

        public static void Bar(Rect rect, float value, InsightColor fill, InsightTheme theme, string label = null)
        {
            Widgets.DrawBoxSolid(rect, Color(theme.Background));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Color(fill));
            Widgets.DrawBox(rect, 1);
            if (!string.IsNullOrEmpty(label))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color(theme.PrimaryText);
                Widgets.Label(rect, label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        public static void Badge(Rect rect, string text, InsightColor color, InsightTheme theme)
        {
            Widgets.DrawBoxSolid(rect, Color(color.WithAlpha(0.22f)));
            Widgets.DrawBox(rect, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color(theme.PrimaryText);
            Widgets.Label(rect, text ?? string.Empty);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void Icon(Rect rect, InsightEntity entity, InsightTheme theme, bool silhouette = false)
        {
            Widgets.DrawBoxSolid(rect, Color(theme.Background));
            Widgets.DrawBox(rect, 1);
            Texture2D texture = entity?.Icon as Texture2D;
            if (texture != null && !silhouette)
            {
                Widgets.DrawTextureFitted(rect.ContractedBy(4f), texture, 1f);
                return;
            }
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color(silhouette ? theme.Unknown : theme.Selected);
            string label = silhouette || entity == null || string.IsNullOrEmpty(entity.Label) ? "?" : entity.Label.Substring(0, 1).ToUpperInvariant();
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void Empty(Rect rect, InsightTheme theme, string message)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color(theme.SecondaryText);
            Widgets.Label(rect, message ?? string.Empty);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static string MetricText(InsightMetric metric, InsightDisclosure disclosure)
        {
            if (metric == null || disclosure == null || !metric.Known || !disclosure.ExactValuesVisible)
            {
                if (metric != null && metric.HasRange && disclosure != null && disclosure.IdentityVisible) return "~" + metric.Range.ToString();
                return "?";
            }
            return metric.Value.ToString("0.##") + (metric.HasRange && metric.Range.Size > 0.001f ? "  [" + metric.Range + "]" : string.Empty);
        }

        private static Texture2D PanelTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Texture2D texture;
            if (panelTextures.TryGetValue(path, out texture)) return texture;
            texture = ContentFinder<Texture2D>.Get(path, false);
            panelTextures[path] = texture;
            return texture;
        }
    }
}
