using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Density presets used by the composable UI layer.</summary>
    public enum InsightUiDensity
    {
        Comfortable,
        Normal,
        Compact
    }

    /// <summary>Primary axis used by stack containers.</summary>
    public enum InsightUiOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>Overflow behavior used by a stack or scroll container.</summary>
    public enum InsightUiWrapMode
    {
        NoWrap,
        Wrap
    }

    /// <summary>Semantic text roles understood by a renderer.</summary>
    public enum InsightUiTextStyle
    {
        Title,
        Heading,
        Body,
        Caption,
        Button,
        Label
    }

    /// <summary>Semantic tone used by persistent in-layout callouts.</summary>
    public enum InsightUiCalloutSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>Direction from which a SlideFade element enters or exits its final bounds.</summary>
    public enum InsightUiSlideDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>Direction used by document-level focus traversal.</summary>
    public enum InsightUiFocusDirection
    {
        Forward = 1,
        Backward = -1
    }

    /// <summary>Small, renderer-neutral icon description used by icon elements and buttons.</summary>
    public sealed class InsightUiIcon
    {
        private InsightUiIcon(string fallback, object texture)
        {
            Fallback = fallback ?? string.Empty;
            Texture = texture;
        }

        /// <summary>Text or glyph shown when no texture is available.</summary>
        public string Fallback { get; private set; }

        /// <summary>Consumer-supplied texture/image object, usually a Unity Texture in RimWorld.</summary>
        public object Texture { get; private set; }

        /// <summary>Optional tooltip shown when the icon is hovered.</summary>
        public string Tooltip { get; private set; }

        /// <summary>Optional accessible description for consumers that provide an accessibility layer.</summary>
        public string AccessibleDescription { get; private set; }

        /// <summary>Gets whether the icon carries a texture/image payload.</summary>
        public bool HasTexture => Texture != null;

        /// <summary>Creates an icon that renders a text or glyph fallback.</summary>
        public static InsightUiIcon FromText(string fallback) => new InsightUiIcon(fallback, null);

        /// <summary>Creates an icon backed by a consumer-supplied texture/image with an optional fallback.</summary>
        public static InsightUiIcon FromTexture(object texture, string fallback = "") =>
            new InsightUiIcon(fallback, texture);

        /// <summary>Sets the optional hover tooltip and returns this icon for fluent composition.</summary>
        public InsightUiIcon WithTooltip(string tooltip)
        {
            Tooltip = tooltip;
            return this;
        }

        /// <summary>Sets the optional accessible description and returns this icon for fluent composition.</summary>
        public InsightUiIcon WithAccessibleDescription(string description)
        {
            AccessibleDescription = description;
            return this;
        }
    }

    /// <summary>Minimal input snapshot consumed by focus-aware stock controls.</summary>
    public interface IInsightUiInput
    {
        /// <summary>Gets whether the currently focused control owns text editing.</summary>
        bool IsTextEditing { get; }
        /// <summary>Gets whether Tab was pressed during this input frame.</summary>
        bool TabPressed { get; }
        /// <summary>Gets whether Shift+Tab was pressed during this input frame.</summary>
        bool ShiftTabPressed { get; }
        /// <summary>Gets whether Enter, keypad Enter, or Space was pressed.</summary>
        bool ActivatePressed { get; }
        /// <summary>Marks the Tab input as handled.</summary>
        void ConsumeTab();
        /// <summary>Marks the activation input as handled.</summary>
        void ConsumeActivation();
    }

    /// <summary>Document-owned focus order and keyboard activation state.</summary>
    public sealed class InsightUiFocusState
    {
        private readonly List<string> focusableIds = new List<string>();
        private readonly List<string> previousFocusableIds = new List<string>();
        private readonly Dictionary<string, object> owners = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly HashSet<string> textInputIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> previousTextInputIds = new HashSet<string>(StringComparer.Ordinal);
        private string activationId;

        /// <summary>Gets the effective ID that currently owns keyboard focus.</summary>
        public string FocusedId { get; private set; }
        /// <summary>Gets the focus order registered during the latest paint pass.</summary>
        public IReadOnlyList<string> FocusableIds => focusableIds;
        /// <summary>Gets whether the focused ID was a text input in the previous frame.</summary>
        public bool IsTextEditing => !string.IsNullOrEmpty(FocusedId) && previousTextInputIds.Contains(FocusedId);

        /// <summary>Starts a new registration frame while retaining focus across stable IDs.</summary>
        public void BeginFrame()
        {
            previousFocusableIds.Clear();
            previousFocusableIds.AddRange(focusableIds);
            previousTextInputIds.Clear();
            foreach (string id in textInputIds) previousTextInputIds.Add(id);
            focusableIds.Clear();
            owners.Clear();
            textInputIds.Clear();
            activationId = null;
        }

        /// <summary>Returns whether the supplied effective ID currently has focus.</summary>
        public bool IsFocused(string id) => !string.IsNullOrEmpty(id) &&
            string.Equals(FocusedId, id, StringComparison.Ordinal);

        /// <summary>Requests focus for an effective ID.</summary>
        public bool RequestFocus(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            FocusedId = id;
            return true;
        }

        /// <summary>Clears focus and any pending keyboard activation.</summary>
        public void ClearFocus()
        {
            FocusedId = null;
            activationId = null;
        }

        /// <summary>Moves focus through the last or current registration order.</summary>
        public bool Move(InsightUiFocusDirection direction)
        {
            IReadOnlyList<string> order = focusableIds.Count == 0 ? previousFocusableIds : focusableIds;
            if (order.Count == 0)
            {
                FocusedId = null;
                return false;
            }

            int current = FocusedId == null ? -1 : IndexOf(order, FocusedId);
            int next;
            if (current < 0)
                next = direction == InsightUiFocusDirection.Backward ? order.Count - 1 : 0;
            else
                next = (current + (int)direction + order.Count) % order.Count;
            FocusedId = order[next];
            return true;
        }

        /// <summary>Consumes document-level Tab and activation input when text editing does not own it.</summary>
        public void ProcessKeyboard(IInsightUiInput input)
        {
            if (input == null || input.IsTextEditing) return;
            if (input.TabPressed)
            {
                Move(input.ShiftTabPressed ? InsightUiFocusDirection.Backward : InsightUiFocusDirection.Forward);
                input.ConsumeTab();
            }
            if (input.ActivatePressed && !string.IsNullOrEmpty(FocusedId))
            {
                activationId = FocusedId;
                input.ConsumeActivation();
            }
        }

        /// <summary>Consumes a pending activation for the supplied effective ID.</summary>
        public bool ConsumeActivation(string id)
        {
            if (!string.Equals(activationId, id, StringComparison.Ordinal)) return false;
            activationId = null;
            return true;
        }

        internal void Register(string id, object owner, InsightUiDiagnostics diagnostics, bool focusable, bool textInput)
        {
            if (string.IsNullOrEmpty(id)) return;
            object existing;
            if (owners.TryGetValue(id, out existing))
            {
                diagnostics?.RecordDuplicateId(id);
                return;
            }
            owners[id] = owner;
            if (focusable)
            {
                focusableIds.Add(id);
                if (textInput) textInputIds.Add(id);
            }
        }

        internal void PruneFocus()
        {
            if (!string.IsNullOrEmpty(FocusedId) && !focusableIds.Contains(FocusedId) &&
                !previousFocusableIds.Contains(FocusedId))
                FocusedId = null;
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    /// <summary>Engine-independent size used during measure and arrange.</summary>
    public struct InsightUiSize : IEquatable<InsightUiSize>
    {
        public float Width;
        public float Height;

        public InsightUiSize(float width, float height)
        {
            Width = Math.Max(0f, width);
            Height = Math.Max(0f, height);
        }

        public bool Equals(InsightUiSize other) => Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object obj) => obj is InsightUiSize && Equals((InsightUiSize)obj);
        public override int GetHashCode() => (Width.GetHashCode() * 397) ^ Height.GetHashCode();
        public override string ToString() => Width + "x" + Height;
    }

    /// <summary>Available bounds passed to an element's measure phase.</summary>
    public struct InsightUiConstraints
    {
        public float MinWidth;
        public float MaxWidth;
        public float MinHeight;
        public float MaxHeight;

        public InsightUiConstraints(float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            MinWidth = Math.Max(0f, minWidth);
            MaxWidth = maxWidth < MinWidth ? MinWidth : maxWidth;
            MinHeight = Math.Max(0f, minHeight);
            MaxHeight = maxHeight < MinHeight ? MinHeight : maxHeight;
        }

        public static InsightUiConstraints Unbounded => new InsightUiConstraints(0f, float.PositiveInfinity, 0f, float.PositiveInfinity);

        public InsightUiSize Constrain(InsightUiSize size)
        {
            return new InsightUiSize(Clamp(size.Width, MinWidth, MaxWidth), Clamp(size.Height, MinHeight, MaxHeight));
        }

        public InsightUiConstraints WithWidth(float minimum, float maximum) =>
            new InsightUiConstraints(minimum, maximum, MinHeight, MaxHeight);

        public InsightUiConstraints WithHeight(float minimum, float maximum) =>
            new InsightUiConstraints(MinWidth, MaxWidth, minimum, maximum);

        private static float Clamp(float value, float minimum, float maximum)
        {
            value = Math.Max(minimum, value);
            return float.IsPositiveInfinity(maximum) ? value : Math.Min(maximum, value);
        }
    }

    /// <summary>Four-sided padding that remains independent from Unity's Rect type.</summary>
    public struct InsightUiPadding : IEquatable<InsightUiPadding>
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public InsightUiPadding(float left, float top, float right, float bottom)
        {
            Left = Math.Max(0f, left);
            Top = Math.Max(0f, top);
            Right = Math.Max(0f, right);
            Bottom = Math.Max(0f, bottom);
        }

        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;

        public static InsightUiPadding All(float value) => new InsightUiPadding(value, value, value, value);
        public static InsightUiPadding Symmetric(float horizontal, float vertical) =>
            new InsightUiPadding(horizontal, vertical, horizontal, vertical);
        public static InsightUiPadding None => new InsightUiPadding(0f, 0f, 0f, 0f);

        public bool Equals(InsightUiPadding other) => Left.Equals(other.Left) && Top.Equals(other.Top) &&
            Right.Equals(other.Right) && Bottom.Equals(other.Bottom);
        public override bool Equals(object obj) => obj is InsightUiPadding && Equals((InsightUiPadding)obj);
        public override int GetHashCode() => (((Left.GetHashCode() * 397) ^ Top.GetHashCode()) * 397 ^
            Right.GetHashCode()) * 397 ^ Bottom.GetHashCode();
    }

    /// <summary>Shared layout and visual tokens for one composable element.</summary>
    public sealed class InsightUiStyle
    {
        public InsightLength Width { get; set; } = InsightLength.Auto();
        public InsightLength Height { get; set; } = InsightLength.Auto();
        public float MinimumWidth { get; set; }
        public float MaximumWidth { get; set; } = float.PositiveInfinity;
        public float MinimumHeight { get; set; }
        public float MaximumHeight { get; set; } = float.PositiveInfinity;
        public float Flex { get; set; }
        public InsightUiPadding Padding { get; set; } = InsightUiPadding.None;
        public float Gap { get; set; }
        public InsightAlignment HorizontalAlignment { get; set; } = InsightAlignment.Stretch;
        public InsightAlignment VerticalAlignment { get; set; } = InsightAlignment.Stretch;
        public InsightColor? Background { get; set; }
        public InsightColor? Border { get; set; }
        public float BorderWidth { get; set; } = 1f;
        /// <summary>Optional element corner radius in pixels; negative inherits the document theme and zero is square.</summary>
        public float CornerRadius { get; set; } = -1f;
        public bool Elevated { get; set; }
        public bool Clip { get; set; }

        public InsightUiStyle Clone()
        {
            return new InsightUiStyle
            {
                Width = Width,
                Height = Height,
                MinimumWidth = MinimumWidth,
                MaximumWidth = MaximumWidth,
                MinimumHeight = MinimumHeight,
                MaximumHeight = MaximumHeight,
                Flex = Flex,
                Padding = Padding,
                Gap = Gap,
                HorizontalAlignment = HorizontalAlignment,
                VerticalAlignment = VerticalAlignment,
                Background = Background,
                Border = Border,
                BorderWidth = BorderWidth,
                CornerRadius = CornerRadius,
                Elevated = Elevated,
                Clip = Clip
            };
        }

        internal InsightUiSize Constrain(InsightUiSize size, InsightUiConstraints constraints)
        {
            float minWidth = Math.Max(constraints.MinWidth, MinimumWidth);
            float maxWidth = Math.Min(constraints.MaxWidth, MaximumWidth);
            float minHeight = Math.Max(constraints.MinHeight, MinimumHeight);
            float maxHeight = Math.Min(constraints.MaxHeight, MaximumHeight);
            if (maxWidth < minWidth) maxWidth = minWidth;
            if (maxHeight < minHeight) maxHeight = minHeight;
            return new InsightUiConstraints(minWidth, maxWidth, minHeight, maxHeight).Constrain(size);
        }
    }

    /// <summary>Deterministic surface-radius policy shared by portable layout tests and the RimWorld painter.</summary>
    internal static class InsightUiSurfaceMath
    {
        // Public radii select 0/2/4/6/8, while borders may need exact inner radii such as 3 or 7.
        // Nine fixed buckets keep that geometry bounded without retaining per-widget textures.
        internal const int RoundedRadiusBucketCount = 9;
        internal const float MaximumRoundedRadius = 8f;

        internal static float ResolveCornerRadius(InsightUiStyle style, InsightTheme theme)
        {
            float requested = style != null && style.CornerRadius >= 0f
                ? style.CornerRadius : theme == null ? 0f : theme.CornerRadius;
            return QuantizeCornerRadius(requested);
        }

        internal static float QuantizeCornerRadius(float radius)
        {
            if (float.IsNaN(radius) || radius <= 1f) return 0f;
            if (radius <= 3f) return 2f;
            if (radius <= 5f) return 4f;
            if (radius <= 7f) return 6f;
            return MaximumRoundedRadius;
        }

        internal static float ClampCornerRadius(float radius, float width, float height)
        {
            if (float.IsNaN(radius) || float.IsNaN(width) || float.IsNaN(height) || radius <= 0f)
                return 0f;
            float minimumDimension = Math.Min(Math.Max(0f, width), Math.Max(0f, height));
            return Math.Min(Math.Min(radius, MaximumRoundedRadius), minimumDimension * 0.5f);
        }

        internal static float InnerCornerRadius(float outerRadius, float borderWidth)
        {
            if (float.IsNaN(outerRadius) || float.IsNaN(borderWidth) || outerRadius <= 0f)
                return 0f;
            return Math.Max(0f, outerRadius - Math.Max(0f, borderWidth));
        }

        internal static float EffectiveInnerCornerRadius(float outerRadius, float borderWidth, float width, float height)
        {
            float effectiveOuter = ClampCornerRadius(outerRadius, width, height);
            float minimumDimension = Math.Min(Math.Max(0f, width), Math.Max(0f, height));
            float inset = Math.Min(Math.Max(0f, float.IsNaN(borderWidth) ? 0f : borderWidth), minimumDimension * 0.5f);
            return ClampCornerRadius(InnerCornerRadius(effectiveOuter, inset),
                Math.Max(0f, width - inset * 2f), Math.Max(0f, height - inset * 2f));
        }

        internal static int RadiusBucket(float radius)
        {
            if (float.IsNaN(radius) || radius <= 0f) return 0;
            if (float.IsPositiveInfinity(radius) || radius >= MaximumRoundedRadius)
                return (int)MaximumRoundedRadius;
            if (radius < 1f) return 1;
            return Math.Max(0, Math.Min((int)MaximumRoundedRadius,
                (int)Math.Round(radius, MidpointRounding.AwayFromZero)));
        }
    }

    /// <summary>Portable alpha geometry used to create one cached Unity texture per integer radius.</summary>
    internal sealed class InsightUiRoundedMaskData
    {
        private readonly byte[] alpha;

        internal InsightUiRoundedMaskData(int size, int sourceCorner, byte[] alpha)
        {
            Size = size;
            SourceCorner = sourceCorner;
            this.alpha = alpha;
        }

        internal int Size { get; private set; }
        internal int SourceCorner { get; private set; }
        internal byte AlphaAt(int x, int y) => alpha[y * Size + x];
    }

    /// <summary>Fixed-size, supersampled rounded-rectangle mask generator.</summary>
    internal static class InsightUiRoundedMaskGenerator
    {
        internal const int TextureSize = 32;
        internal const int Supersampling = 4;
        internal const int MinimumOpaqueCenter = 8;

        internal static int SourceCornerForRadius(int geometryRadius)
        {
            geometryRadius = Math.Max(0, Math.Min((int)InsightUiSurfaceMath.MaximumRoundedRadius, geometryRadius));
            return geometryRadius == 0 ? 0 : geometryRadius + 4;
        }

        internal static InsightUiRoundedMaskData Create(int geometryRadius)
        {
            geometryRadius = Math.Max(0, Math.Min((int)InsightUiSurfaceMath.MaximumRoundedRadius, geometryRadius));
            int sourceCorner = SourceCornerForRadius(geometryRadius);
            byte[] alpha = new byte[TextureSize * TextureSize];
            if (sourceCorner == 0)
            {
                for (int i = 0; i < alpha.Length; i++) alpha[i] = 255;
                return new InsightUiRoundedMaskData(TextureSize, 0, alpha);
            }

            int sampleCount = Supersampling * Supersampling;
            double radiusSquared = sourceCorner * sourceCorner;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int inside = 0;
                    for (int sampleY = 0; sampleY < Supersampling; sampleY++)
                    {
                        double pointY = y + (sampleY + 0.5) / Supersampling;
                        double nearestY = pointY < sourceCorner ? sourceCorner :
                            pointY > TextureSize - sourceCorner ? TextureSize - sourceCorner : pointY;
                        for (int sampleX = 0; sampleX < Supersampling; sampleX++)
                        {
                            double pointX = x + (sampleX + 0.5) / Supersampling;
                            double nearestX = pointX < sourceCorner ? sourceCorner :
                                pointX > TextureSize - sourceCorner ? TextureSize - sourceCorner : pointX;
                            double deltaX = pointX - nearestX;
                            double deltaY = pointY - nearestY;
                            if (deltaX * deltaX + deltaY * deltaY <= radiusSquared) inside++;
                        }
                    }
                    alpha[y * TextureSize + x] = (byte)((inside * 255 + sampleCount / 2) / sampleCount);
                }
            }
            return new InsightUiRoundedMaskData(TextureSize, sourceCorner, alpha);
        }
    }

    /// <summary>Per-document state store. It is deliberately instance-owned, never global.</summary>
    public sealed class InsightUiStateStore
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        public int Count => values.Count;

        public bool GetBool(string key, bool fallback = false)
        {
            object value;
            return key != null && values.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }

        public void SetBool(string key, bool value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            object value;
            return key != null && values.TryGetValue(key, out value) && value is float ? (float)value : fallback;
        }

        public void SetFloat(string key, float value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value;
        }

        public int GetInt(string key, int fallback = 0)
        {
            object value;
            return key != null && values.TryGetValue(key, out value) && value is int ? (int)value : fallback;
        }

        public void SetInt(string key, int value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value;
        }

        public string GetString(string key, string fallback = "")
        {
            object value;
            return key != null && values.TryGetValue(key, out value) && value is string ? (string)value : fallback;
        }

        public void SetString(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value ?? string.Empty;
        }

        public T Get<T>(string key, T fallback = default(T))
        {
            object value;
            return key != null && values.TryGetValue(key, out value) && value is T ? (T)value : fallback;
        }

        public void Set<T>(string key, T value)
        {
            if (!string.IsNullOrEmpty(key)) values[key] = value;
        }

        public bool Remove(string key) => key != null && values.Remove(key);
        public void Clear() => values.Clear();
    }

    /// <summary>Engine-independent diagnostics for a composable document.</summary>
    public sealed class InsightUiDiagnostics
    {
        private readonly HashSet<string> duplicateIdSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> duplicateIdPaths = new List<string>();

        public int Frame { get; private set; }
        public int MeasurePasses { get; private set; }
        public int ArrangePasses { get; private set; }
        public int VisibleElements { get; private set; }
        public int Invalidations { get; private set; }
        public int RenderErrors { get; private set; }
        /// <summary>Gets the number of immutable semantic snapshots refreshed during the current frame.</summary>
        public int SemanticSnapshotRefreshes { get; private set; }
        /// <summary>Gets the number of semantic layout invalidations caused by retained input changes.</summary>
        public int SemanticLayoutInvalidations { get; private set; }
        /// <summary>Gets the number of semantic refreshes deferred until the next layout pass.</summary>
        public int SemanticDeferredRefreshes { get; private set; }
        /// <summary>Gets the number of semantic elements whose arranged bounds changed.</summary>
        public int SemanticResizes { get; private set; }
        /// <summary>Gets contained semantic render errors, capped to a deterministic per-frame bound.</summary>
        public int SemanticRenderErrors { get; private set; }
        public int LastMeasuredElementCount { get; private set; }
        /// <summary>Gets the number of elements in the most recent virtualized viewport.</summary>
        public int VirtualizedVisibleElements { get; private set; }
        /// <summary>Gets the number of cached elements retained by the most recent virtualized list.</summary>
        public int VirtualizedCachedElements { get; private set; }
        /// <summary>Gets the number of active document effects observed by the renderer.</summary>
        public int ActiveEffects { get; private set; }
        /// <summary>Enables duplicate effective-ID tracking for development diagnostics.</summary>
        public bool TrackDuplicateIds { get; set; }
        /// <summary>Gets the number of distinct duplicate effective IDs in the current frame.</summary>
        public int DuplicateIds { get; private set; }
        /// <summary>Gets the duplicate effective-ID paths in deterministic discovery order.</summary>
        public IReadOnlyList<string> DuplicateIdPaths => duplicateIdPaths;

        public void BeginFrame()
        {
            Frame++;
            VisibleElements = 0;
            LastMeasuredElementCount = 0;
            VirtualizedVisibleElements = 0;
            VirtualizedCachedElements = 0;
            ActiveEffects = 0;
            SemanticSnapshotRefreshes = 0;
            SemanticLayoutInvalidations = 0;
            SemanticDeferredRefreshes = 0;
            SemanticResizes = 0;
            SemanticRenderErrors = 0;
            DuplicateIds = 0;
            duplicateIdSet.Clear();
            duplicateIdPaths.Clear();
        }

        public void RecordMeasure() { MeasurePasses++; LastMeasuredElementCount++; }
        public void RecordArrange() { ArrangePasses++; }
        public void RecordVisible() { VisibleElements++; }
        public void RecordInvalidation() { Invalidations++; }
        public void RecordRenderError() { RenderErrors = Math.Min(64, RenderErrors + 1); }
        internal void RecordSemanticSnapshot() { SemanticSnapshotRefreshes = Math.Min(64, SemanticSnapshotRefreshes + 1); }
        internal void RecordSemanticLayoutInvalidation() { SemanticLayoutInvalidations = Math.Min(64, SemanticLayoutInvalidations + 1); }
        internal void RecordSemanticDeferredRefresh() { SemanticDeferredRefreshes = Math.Min(64, SemanticDeferredRefreshes + 1); }
        internal void RecordSemanticResize(string id) { SemanticResizes = Math.Min(64, SemanticResizes + 1); }
        internal void RecordSemanticRenderErrors(string id, int count)
        {
            if (count <= 0) return;
            SemanticRenderErrors = Math.Min(64, SemanticRenderErrors + count);
        }
        internal void RecordVirtualization(int visible, int cached)
        {
            VirtualizedVisibleElements = Math.Max(0, visible);
            VirtualizedCachedElements = Math.Max(0, cached);
        }
        internal void RecordEffects(int activeEffects) { ActiveEffects = Math.Max(0, activeEffects); }

        internal void RecordDuplicateId(string id)
        {
            if (!TrackDuplicateIds || string.IsNullOrEmpty(id) || !duplicateIdSet.Add(id)) return;
            DuplicateIds++;
            duplicateIdPaths.Add(id);
        }

        public string Summary()
        {
            return "frame " + Frame + " | measure " + MeasurePasses + " | arrange " + ArrangePasses +
                " | visible " + VisibleElements + " | invalidations " + Invalidations +
                " | virtualized " + VirtualizedVisibleElements + "/" + VirtualizedCachedElements +
                " | effects " + ActiveEffects + " | errors " + RenderErrors +
                " | semantic " + SemanticSnapshotRefreshes + "/" + SemanticLayoutInvalidations +
                "/" + SemanticDeferredRefreshes + "/" + SemanticRenderErrors;
        }
    }

    /// <summary>State and services passed through one measure, arrange, and paint cycle.</summary>
    public sealed class InsightUiFrame
    {
        private readonly List<string> scopeStack = new List<string>();
        private readonly Dictionary<string, string> keyCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<float> opacityStack = new List<float>();
        private readonly List<InsightContext> semanticContexts = new List<InsightContext>();
        private string scopePath = string.Empty;

        public InsightTheme Theme { get; private set; }
        public InsightUiDensity Density { get; private set; }
        public bool HighContrast { get; private set; }
        public bool ReducedMotion { get; private set; }
        public InsightUiStateStore State { get; private set; }
        public InsightUiDiagnostics Diagnostics { get; private set; }
        /// <summary>Gets the document-owned focus state.</summary>
        public InsightUiFocusState Focus { get; private set; }
        /// <summary>Gets the document-owned keyed transitions and feedback effects.</summary>
        public InsightUiEffects Effects { get; private set; }
        /// <summary>Gets the document-owned transient toast service.</summary>
        public InsightUiToastService Toasts { get; private set; }
        /// <summary>Gets the current nested visual opacity, used by fade/reveal elements.</summary>
        public float Opacity { get; private set; } = 1f;
        /// <summary>Gets the caller-owned bounds used by transient elements for safe placement.</summary>
        public InsightRect HostBounds { get; private set; }
        public float DeltaTime { get; private set; }
        /// <summary>Gets the enclosing host's transient overlay owner for semantic interaction callbacks.</summary>
        internal object OverlayOwnerToken { get; private set; }
        public Func<string, InsightUiTextStyle, float, InsightUiSize> TextMeasurer { get; set; }
        /// <summary>Optional normal RimWorld control measurement, without semantic typography scaling.</summary>
        public Func<string, InsightUiTextStyle, float, InsightUiSize> NativeTextMeasurer { get; set; }

        public InsightUiFrame(InsightTheme theme, InsightUiDensity density, bool highContrast, bool reducedMotion,
            InsightUiStateStore state, InsightUiDiagnostics diagnostics, float deltaTime,
            InsightUiFocusState focus = null, InsightUiEffects effects = null, InsightUiToastService toasts = null,
            InsightRect? hostBounds = null)
        {
            Theme = theme ?? InsightTheme.Default;
            Density = density;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
            State = state ?? new InsightUiStateStore();
            Diagnostics = diagnostics ?? new InsightUiDiagnostics();
            Focus = focus ?? new InsightUiFocusState();
            Effects = effects ?? new InsightUiEffects();
            Toasts = toasts ?? new InsightUiToastService();
            HostBounds = hostBounds ?? new InsightRect(0f, 0f, 0f, 0f);
            DeltaTime = deltaTime < 0f ? 0f : deltaTime;
        }

        /// <summary>Current reusable-component scope, or an empty string for ordinary documents.</summary>
        public string ScopePath => scopePath;

        /// <summary>Returns a stable effective identity for a local element ID.</summary>
        public string EffectiveId(string localId)
        {
            localId = localId ?? string.Empty;
            return string.IsNullOrEmpty(scopePath) ? localId : JoinScope(localId);
        }

        /// <summary>Returns a document-owned state key for a local control key.</summary>
        public string StateKey(string localKey)
        {
            localKey = localKey ?? string.Empty;
            if (string.IsNullOrEmpty(scopePath)) return localKey;
            string cacheKey = scopePath + "|" + localKey;
            string effective;
            if (!keyCache.TryGetValue(cacheKey, out effective))
            {
                effective = JoinScope(localKey);
                keyCache[cacheKey] = effective;
            }
            return effective;
        }

        internal void PushScope(string localScope)
        {
            localScope = localScope ?? string.Empty;
            scopeStack.Add(localScope);
            scopePath = string.IsNullOrEmpty(scopePath) ? localScope : scopePath + "/" + localScope;
        }

        internal void PopScope()
        {
            if (scopeStack.Count == 0) return;
            scopeStack.RemoveAt(scopeStack.Count - 1);
            scopePath = string.Empty;
            for (int i = 0; i < scopeStack.Count; i++)
            {
                if (i > 0) scopePath += "/";
                scopePath += scopeStack[i];
            }
        }

        internal void RegisterElement(InsightUiElement element, bool focusable, bool textInput)
        {
            string effectiveId = EffectiveId(element?.Id);
            Focus.Register(effectiveId, element, Diagnostics, focusable, textInput);
        }

        internal void SetOverlayOwnerToken(object ownerToken)
        {
            OverlayOwnerToken = ownerToken;
        }

        internal void RegisterSemanticContext(InsightContext context)
        {
            if (context == null) return;
            for (int i = 0; i < semanticContexts.Count; i++)
                if (ReferenceEquals(semanticContexts[i], context)) return;
            context.BeginFrame();
            semanticContexts.Add(context);
        }

        internal void EndSemanticContexts()
        {
            for (int i = semanticContexts.Count - 1; i >= 0; i--)
                semanticContexts[i].EndFrame();
            semanticContexts.Clear();
        }

        internal void RegisterInteractive(string localId, object owner, bool textInput = false)
        {
            Focus.Register(EffectiveId(localId), owner, Diagnostics, true, textInput);
        }

        private string JoinScope(string localId)
        {
            return string.IsNullOrEmpty(localId) ? scopePath : scopePath + "/" + localId;
        }

        public float Spacing(float value)
        {
            float multiplier = Density == InsightUiDensity.Compact ? 0.75f :
                Density == InsightUiDensity.Comfortable ? 1.25f : 1f;
            return Math.Max(0f, value * multiplier);
        }

        public InsightUiSize MeasureText(string text, InsightUiTextStyle style, float maxWidth)
        {
            if (TextMeasurer != null) return TextMeasurer(text ?? string.Empty, style, maxWidth);
            return EstimateText(text, style, maxWidth, TextScale(style));
        }

        /// <summary>Measures text using the normal control font geometry, without theme typography scaling.</summary>
        public InsightUiSize MeasureNativeText(string text, InsightUiTextStyle style, float maxWidth)
        {
            if (NativeTextMeasurer != null) return NativeTextMeasurer(text ?? string.Empty, style, maxWidth);
            return EstimateText(text, style, maxWidth, 1f);
        }

        private static InsightUiSize EstimateText(string text, InsightUiTextStyle style, float maxWidth, float scale)
        {
            float fontSize = BaseTextSize(style) * Math.Max(0.5f, scale);
            float width = (text ?? string.Empty).Length * fontSize * 0.52f;
            if (!float.IsPositiveInfinity(maxWidth) && maxWidth > 1f && width > maxWidth)
            {
                int lines = Math.Max(1, (int)Math.Ceiling(width / maxWidth));
                return new InsightUiSize(maxWidth, lines * fontSize * 1.25f);
            }
            return new InsightUiSize(width, fontSize * 1.25f);
        }

        internal static float BaseTextSize(InsightUiTextStyle style)
        {
            switch (style)
            {
                case InsightUiTextStyle.Title: return 24f;
                case InsightUiTextStyle.Heading: return 19f;
                case InsightUiTextStyle.Caption: return 12f;
                default: return 15f;
            }
        }

        /// <summary>Returns the theme typography multiplier for a semantic text role.</summary>
        public float TextScale(InsightUiTextStyle style)
        {
            switch (style)
            {
                case InsightUiTextStyle.Title: return Math.Max(0.5f, Theme.TitleSize);
                case InsightUiTextStyle.Caption: return Math.Max(0.5f, Theme.CaptionSize);
                case InsightUiTextStyle.Heading: return Math.Max(0.5f, (Theme.TitleSize + Theme.BodySize) * 0.5f);
                case InsightUiTextStyle.Body: return Math.Max(0.5f, Theme.BodySize);
                default: return Math.Max(0.5f, Theme.BodySize);
            }
        }

        /// <summary>Applies the current nested opacity to a renderer-neutral color.</summary>
        public InsightColor ApplyOpacity(InsightColor color) => color.WithAlpha(color.A * Opacity);

        internal void PushOpacity(float opacity)
        {
            opacityStack.Add(Opacity);
            Opacity = Math.Max(0f, Math.Min(1f, Opacity * opacity));
        }

        internal void PopOpacity()
        {
            if (opacityStack.Count == 0)
            {
                Opacity = 1f;
                return;
            }
            Opacity = opacityStack[opacityStack.Count - 1];
            opacityStack.RemoveAt(opacityStack.Count - 1);
        }
    }

    /// <summary>Renderer contract implemented by the RimWorld/Unity adapter and portable test doubles.</summary>
    public interface IInsightUiPainter
    {
        InsightUiSize MeasureText(string text, InsightUiTextStyle style, float maxWidth, InsightUiFrame frame);
        void Surface(InsightRect rect, InsightUiStyle style, InsightUiFrame frame);
        void Text(InsightRect rect, string text, InsightUiTextStyle style, InsightColor? color, bool wrap, InsightUiFrame frame);
        void Progress(InsightRect rect, float value, InsightColor fill, InsightUiFrame frame);
        bool Button(InsightRect rect, string label, bool enabled, bool selected, InsightUiFrame frame);
        bool Toggle(InsightRect rect, string label, bool value, bool enabled, InsightUiFrame frame);
        float Slider(InsightRect rect, float value, float minimum, float maximum, bool enabled, InsightUiFrame frame);
        string TextField(InsightRect rect, string value, bool enabled, InsightUiFrame frame);
        void Divider(InsightRect rect, InsightColor color, InsightUiFrame frame);
        void Tooltip(InsightRect rect, string text, InsightUiFrame frame);
        void BeginClip(InsightRect rect);
        void EndClip();
        float ScrollOffset(InsightRect viewport, float contentHeight, float offset, string stateKey, InsightUiFrame frame);
    }

    /// <summary>Optional drawing capabilities used by custom elements without expanding the core painter contract.</summary>
    public interface IInsightUiCustomPainter
    {
        /// <summary>Fills a rectangle with a renderer-neutral color.</summary>
        void FillRect(InsightRect rect, InsightColor color, InsightUiFrame frame);
        /// <summary>Draws a rectangle outline.</summary>
        void Outline(InsightRect rect, InsightColor color, float width, InsightUiFrame frame);
        /// <summary>Draws a line segment.</summary>
        void Line(float x1, float y1, float x2, float y2, InsightColor color, float width, InsightUiFrame frame);
        /// <summary>Draws a consumer-supplied texture when supported.</summary>
        void Texture(InsightRect rect, object texture, InsightColor? tint, InsightUiFrame frame);
    }

    /// <summary>Optional paint-only translation capability used by restrained motion primitives.</summary>
    public interface IInsightUiTranslationPainter
    {
        /// <summary>Pushes a temporary visual offset; layout coordinates remain unchanged.</summary>
        void PushTranslation(InsightPoint offset);
        /// <summary>Restores the translation active before the matching push.</summary>
        void PopTranslation();
    }

    /// <summary>Optional pointer hit-testing capability used by display-only hover context.</summary>
    public interface IInsightUiHoverPainter
    {
        /// <summary>Returns whether the current pointer is inside the supplied arranged bounds.</summary>
        bool IsPointerOver(InsightRect rect, InsightUiFrame frame);
    }

    /// <summary>Optional icon capability implemented by renderers that understand consumer-supplied textures.</summary>
    public interface IInsightUiIconPainter
    {
        /// <summary>Draws an icon using texture support or its fallback.</summary>
        void Icon(InsightRect rect, InsightUiIcon icon, InsightUiFrame frame);
        /// <summary>Draws an icon button and returns whether it was activated.</summary>
        bool IconButton(InsightRect rect, InsightUiIcon icon, bool enabled, bool selected, InsightUiFrame frame);
    }

    /// <summary>Optional focus visualization capability for stock controls.</summary>
    public interface IInsightUiFocusPainter
    {
        /// <summary>Draws the focus indication for a focused control.</summary>
        void FocusRing(InsightRect rect, InsightUiFrame frame);
    }

    /// <summary>Optional renderer capability for draggable split dividers.</summary>
    public interface IInsightUiDragPainter
    {
        /// <summary>Draws and optionally updates a divider ratio. Returns NaN when unchanged.</summary>
        float DragDivider(InsightRect divider, InsightRect bounds, InsightUiOrientation orientation, float ratio,
            string stateKey, InsightUiFrame frame);
    }

    /// <summary>Context passed to a consumer-owned custom drawing callback.</summary>
    public sealed class InsightUiCustomDrawContext
    {
        public InsightUiCustomDrawContext(InsightRect bounds, IInsightUiPainter painter, InsightUiFrame frame)
        {
            Bounds = bounds;
            Painter = painter;
            Frame = frame;
        }

        /// <summary>Gets the arranged bounds of the custom element.</summary>
        public InsightRect Bounds { get; private set; }
        /// <summary>Gets the active renderer contract.</summary>
        public IInsightUiPainter Painter { get; private set; }
        /// <summary>Gets the current measure/arrange/paint frame.</summary>
        public InsightUiFrame Frame { get; private set; }
    }

    /// <summary>Reusable document that can be embedded in a host Rect or placed in a Window.</summary>
    public sealed class InsightUiDocument
    {
        private InsightUiElement root;
        private InsightTheme theme;
        private InsightTheme accessibleTheme;
        private InsightTheme accessibleThemeSource;
        private bool accessibleThemeHighContrast;
        private InsightColorBlindMode accessibleThemeColorBlindMode;
        private int accessibleThemeRevision = -1;

        public InsightUiDocument(string id, InsightUiElement root)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "Insight Canvas" : id;
            theme = InsightTheme.Default;
            State = new InsightUiStateStore();
            Diagnostics = new InsightUiDiagnostics();
            Focus = new InsightUiFocusState();
            Effects = new InsightUiEffects();
            Toasts = new InsightUiToastService();
            Density = InsightUiDensity.Normal;
            this.root = root ?? InsightUi.Empty("root");
        }

        public string Id { get; private set; }
        /// <summary>Gets or replaces the document root; replacing it closes transient descendants of the old root.</summary>
        public InsightUiElement Root
        {
            get => root;
            set
            {
                if (ReferenceEquals(root, value)) return;
                root?.CloseTransient(State);
                root = value ?? InsightUi.Empty("root");
                Revision++;
                Diagnostics.RecordInvalidation();
                root.Invalidate();
            }
        }
        public InsightTheme Theme
        {
            get => theme;
            set
            {
                theme = value ?? InsightTheme.Default;
                accessibleTheme = null;
                accessibleThemeSource = null;
                accessibleThemeRevision = -1;
            }
        }
        public InsightUiDensity Density { get; set; }
        public bool HighContrast { get; set; }
        public bool ReducedMotion { get; set; }
        public bool DrawBackground { get; set; } = true;
        public InsightUiStateStore State { get; private set; }
        public InsightUiDiagnostics Diagnostics { get; private set; }
        public InsightUiFocusState Focus { get; private set; }
        /// <summary>Gets keyed transitions and feedback effects scoped to this document.</summary>
        public InsightUiEffects Effects { get; private set; }
        /// <summary>Gets transient toast feedback scoped to this document.</summary>
        public InsightUiToastService Toasts { get; private set; }
        /// <summary>Enables duplicate effective-ID diagnostics for this document.</summary>
        public bool TrackDuplicateIds { get; set; }
        public int Revision { get; private set; }

        public void Invalidate()
        {
            Revision++;
            accessibleTheme = null;
            accessibleThemeRevision = -1;
            Diagnostics.RecordInvalidation();
            Root?.Invalidate();
        }

        internal InsightTheme ResolveTheme(bool highContrast, InsightColorBlindMode colorBlindMode)
        {
            if (!highContrast && colorBlindMode == InsightColorBlindMode.None) return Theme;
            if (accessibleTheme != null && accessibleThemeSource == Theme &&
                accessibleThemeHighContrast == highContrast && accessibleThemeColorBlindMode == colorBlindMode &&
                accessibleThemeRevision == Revision)
                return accessibleTheme;
            accessibleTheme = Theme.WithAccessibility(highContrast, colorBlindMode);
            accessibleThemeSource = Theme;
            accessibleThemeHighContrast = highContrast;
            accessibleThemeColorBlindMode = colorBlindMode;
            accessibleThemeRevision = Revision;
            return accessibleTheme;
        }

        internal void CloseTransientOverlays()
        {
            Effects.Clear();
            Toasts.Clear();
            Root?.CloseTransient(State);
        }
    }
}
