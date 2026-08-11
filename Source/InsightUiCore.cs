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
        public int Frame { get; private set; }
        public int MeasurePasses { get; private set; }
        public int ArrangePasses { get; private set; }
        public int VisibleElements { get; private set; }
        public int Invalidations { get; private set; }
        public int RenderErrors { get; private set; }
        public int LastMeasuredElementCount { get; private set; }

        public void BeginFrame()
        {
            Frame++;
            VisibleElements = 0;
            LastMeasuredElementCount = 0;
        }

        public void RecordMeasure() { MeasurePasses++; LastMeasuredElementCount++; }
        public void RecordArrange() { ArrangePasses++; }
        public void RecordVisible() { VisibleElements++; }
        public void RecordInvalidation() { Invalidations++; }
        public void RecordRenderError() { RenderErrors++; }

        public string Summary()
        {
            return "frame " + Frame + " | measure " + MeasurePasses + " | arrange " + ArrangePasses +
                " | visible " + VisibleElements + " | invalidations " + Invalidations + " | errors " + RenderErrors;
        }
    }

    /// <summary>State and services passed through one measure, arrange, and paint cycle.</summary>
    public sealed class InsightUiFrame
    {
        public InsightTheme Theme { get; private set; }
        public InsightUiDensity Density { get; private set; }
        public bool HighContrast { get; private set; }
        public bool ReducedMotion { get; private set; }
        public InsightUiStateStore State { get; private set; }
        public InsightUiDiagnostics Diagnostics { get; private set; }
        public float DeltaTime { get; private set; }
        public Func<string, InsightUiTextStyle, float, InsightUiSize> TextMeasurer { get; set; }

        public InsightUiFrame(InsightTheme theme, InsightUiDensity density, bool highContrast, bool reducedMotion,
            InsightUiStateStore state, InsightUiDiagnostics diagnostics, float deltaTime)
        {
            Theme = theme ?? InsightTheme.Default;
            Density = density;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
            State = state ?? new InsightUiStateStore();
            Diagnostics = diagnostics ?? new InsightUiDiagnostics();
            DeltaTime = deltaTime < 0f ? 0f : deltaTime;
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
            float fontSize = style == InsightUiTextStyle.Title ? 24f : style == InsightUiTextStyle.Heading ? 19f :
                style == InsightUiTextStyle.Caption ? 12f : 15f;
            float width = (text ?? string.Empty).Length * fontSize * 0.52f;
            if (!float.IsPositiveInfinity(maxWidth) && maxWidth > 1f && width > maxWidth)
            {
                int lines = Math.Max(1, (int)Math.Ceiling(width / maxWidth));
                return new InsightUiSize(maxWidth, lines * fontSize * 1.25f);
            }
            return new InsightUiSize(width, fontSize * 1.25f);
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

    /// <summary>Reusable document that can be embedded in a host Rect or placed in a Window.</summary>
    public sealed class InsightUiDocument
    {
        public InsightUiDocument(string id, InsightUiElement root)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "Insight Canvas" : id;
            Root = root ?? InsightUi.Empty("root");
            Theme = InsightTheme.Default;
            State = new InsightUiStateStore();
            Diagnostics = new InsightUiDiagnostics();
            Density = InsightUiDensity.Normal;
        }

        public string Id { get; private set; }
        public InsightUiElement Root { get; set; }
        public InsightTheme Theme { get; set; }
        public InsightUiDensity Density { get; set; }
        public bool HighContrast { get; set; }
        public bool ReducedMotion { get; set; }
        public bool DrawBackground { get; set; } = true;
        public InsightUiStateStore State { get; private set; }
        public InsightUiDiagnostics Diagnostics { get; private set; }
        public int Revision { get; private set; }

        public void Invalidate()
        {
            Revision++;
            Diagnostics.RecordInvalidation();
            Root?.Invalidate();
        }
    }
}
