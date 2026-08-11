using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Unity-free rectangle used by the retained layout layer.</summary>
    public struct InsightRect : IEquatable<InsightRect>
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public InsightRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width < 0f ? 0f : width;
            Height = height < 0f ? 0f : height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public InsightPoint Center => new InsightPoint(X + Width * 0.5f, Y + Height * 0.5f);
        public bool Contains(InsightPoint point) => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
        public bool Equals(InsightRect other) => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object obj) => obj is InsightRect && Equals((InsightRect)obj);
        public override int GetHashCode() => (((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Width.GetHashCode()) * 397 ^ Height.GetHashCode();
    }

    /// <summary>How a dimension participates in responsive allocation.</summary>
    public enum InsightLengthKind
    {
        Fixed,
        Percent,
        Auto,
        Flexible
    }

    /// <summary>Alignment used when a child does not consume its full allocated slot.</summary>
    public enum InsightAlignment
    {
        Start,
        Center,
        End,
        Stretch
    }

    /// <summary>Overflow policy selected by a retained container.</summary>
    public enum InsightOverflow
    {
        Clip,
        Scroll,
        Wrap
    }

    /// <summary>Viewport/content pair for scroll and clip containers.</summary>
    public struct InsightOverflowLayout
    {
        public InsightRect Viewport;
        public InsightRect Content;
        public InsightOverflow Policy;

        public InsightOverflowLayout(InsightRect viewport, InsightRect content, InsightOverflow policy)
        {
            Viewport = viewport;
            Content = content;
            Policy = policy;
        }
    }

    /// <summary>One responsive dimension.</summary>
    public struct InsightLength
    {
        public InsightLengthKind Kind;
        public float Value;

        public InsightLength(InsightLengthKind kind, float value)
        {
            Kind = kind;
            Value = value < 0f ? 0f : value;
        }

        public static InsightLength Fixed(float value) => new InsightLength(InsightLengthKind.Fixed, value);
        public static InsightLength Percent(float value) => new InsightLength(InsightLengthKind.Percent, value);
        public static InsightLength Auto() => new InsightLength(InsightLengthKind.Auto, 0f);
        public static InsightLength Flex(float weight = 1f) => new InsightLength(InsightLengthKind.Flexible, weight <= 0f ? 1f : weight);
    }

    /// <summary>Minimum, preferred, and flexible sizing hints for one retained component.</summary>
    public struct InsightLayoutSpec
    {
        public InsightLength Length;
        public float Minimum;
        public float Preferred;
        public float Maximum;

        public InsightLayoutSpec(InsightLength length, float minimum = 0f, float preferred = 0f, float maximum = 0f)
        {
            Length = length;
            Minimum = Math.Max(0f, minimum);
            Preferred = Math.Max(Minimum, preferred);
            Maximum = maximum <= 0f ? float.PositiveInfinity : Math.Max(Preferred, maximum);
        }

        public static InsightLayoutSpec Fixed(float value, float minimum = 0f) =>
            new InsightLayoutSpec(InsightLength.Fixed(value), minimum, value, value);

        public static InsightLayoutSpec Flexible(float minimum, float preferred, float weight = 1f) =>
            new InsightLayoutSpec(InsightLength.Flex(weight), minimum, preferred, 0f);

        public static InsightLayoutSpec Auto(float minimum, float preferred) =>
            new InsightLayoutSpec(InsightLength.Auto(), minimum, preferred, 0f);
    }

    /// <summary>Identifies the resulting rectangle in a layout pass.</summary>
    public sealed class InsightLayoutBox
    {
        public string Id { get; private set; }
        public InsightRect Rect { get; private set; }

        public InsightLayoutBox(string id, InsightRect rect)
        {
            Id = id ?? string.Empty;
            Rect = rect;
        }
    }

    /// <summary>Responsive row, column, grid, overlay, and padding math.</summary>
    public static class InsightLayout
    {
        public static IReadOnlyList<InsightLayoutBox> ArrangeRow(InsightRect rect, IReadOnlyList<string> ids,
            IReadOnlyList<InsightLayoutSpec> specs, float gap = 0f)
        {
            return ArrangeLinear(rect, ids, specs, true, gap);
        }

        public static IReadOnlyList<InsightLayoutBox> ArrangeColumn(InsightRect rect, IReadOnlyList<string> ids,
            IReadOnlyList<InsightLayoutSpec> specs, float gap = 0f)
        {
            return ArrangeLinear(rect, ids, specs, false, gap);
        }

        public static IReadOnlyList<InsightLayoutBox> ArrangeGrid(InsightRect rect, int count, int columns, float gap = 0f,
            float padding = 0f, string idPrefix = "item")
        {
            List<InsightLayoutBox> result = new List<InsightLayoutBox>();
            if (count <= 0) return result;
            columns = columns < 1 ? 1 : columns;
            InsightRect inner = new InsightRect(rect.X + padding, rect.Y + padding,
                Math.Max(0f, rect.Width - padding * 2f), Math.Max(0f, rect.Height - padding * 2f));
            int rows = (count + columns - 1) / columns;
            float cellWidth = columns == 0 ? inner.Width : Math.Max(0f, (inner.Width - gap * (columns - 1)) / columns);
            float cellHeight = rows == 0 ? inner.Height : Math.Max(0f, (inner.Height - gap * (rows - 1)) / rows);
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                result.Add(new InsightLayoutBox((idPrefix ?? "item") + i,
                    new InsightRect(inner.X + column * (cellWidth + gap), inner.Y + row * (cellHeight + gap), cellWidth, cellHeight)));
            }
            return result;
        }

        public static InsightRect Padding(InsightRect rect, float left, float top, float right, float bottom)
        {
            return new InsightRect(rect.X + left, rect.Y + top,
                Math.Max(0f, rect.Width - left - right), Math.Max(0f, rect.Height - top - bottom));
        }

        public static InsightRect Overlay(InsightRect parent, InsightRect child)
        {
            return new InsightRect(parent.X + child.X, parent.Y + child.Y, child.Width, child.Height);
        }

        public static InsightRect Align(InsightRect slot, float width, float height, InsightAlignment alignment)
        {
            if (alignment == InsightAlignment.Stretch) return slot;
            width = Math.Min(slot.Width, Math.Max(0f, width));
            height = Math.Min(slot.Height, Math.Max(0f, height));
            float x = alignment == InsightAlignment.Center ? slot.X + (slot.Width - width) * 0.5f :
                alignment == InsightAlignment.End ? slot.Right - width : slot.X;
            float y = alignment == InsightAlignment.Center ? slot.Y + (slot.Height - height) * 0.5f :
                alignment == InsightAlignment.End ? slot.Bottom - height : slot.Y;
            return new InsightRect(x, y, width, height);
        }

        public static InsightOverflowLayout Overflow(InsightRect viewport, InsightRect content, InsightOverflow policy)
        {
            if (policy == InsightOverflow.Clip)
                content = new InsightRect(content.X, content.Y, Math.Min(viewport.Width, content.Width), Math.Min(viewport.Height, content.Height));
            return new InsightOverflowLayout(viewport, content, policy);
        }

        private static IReadOnlyList<InsightLayoutBox> ArrangeLinear(InsightRect rect, IReadOnlyList<string> ids,
            IReadOnlyList<InsightLayoutSpec> specs, bool horizontal, float gap)
        {
            List<InsightLayoutBox> result = new List<InsightLayoutBox>();
            if (ids == null || specs == null || ids.Count == 0 || specs.Count == 0) return result;
            int count = Math.Min(ids.Count, specs.Count);
            gap = Math.Max(0f, gap);
            float extent = horizontal ? rect.Width : rect.Height;
            float available = Math.Max(0f, extent - gap * (count - 1));
            float[] sizes = new float[count];
            float fixedTotal = 0f;
            float flexTotal = 0f;
            for (int i = 0; i < count; i++)
            {
                InsightLayoutSpec spec = specs[i];
                float size;
                switch (spec.Length.Kind)
                {
                    case InsightLengthKind.Fixed: size = spec.Length.Value; break;
                    case InsightLengthKind.Percent: size = available * spec.Length.Value; break;
                    case InsightLengthKind.Auto: size = spec.Preferred; break;
                    default: size = 0f; flexTotal += Math.Max(0.0001f, spec.Length.Value); break;
                }
                sizes[i] = Clamp(size, spec.Minimum, spec.Maximum);
                if (spec.Length.Kind != InsightLengthKind.Flexible) fixedTotal += sizes[i];
            }
            float remaining = Math.Max(0f, available - fixedTotal);
            for (int i = 0; i < count; i++)
            {
                InsightLayoutSpec spec = specs[i];
                if (spec.Length.Kind == InsightLengthKind.Flexible)
                    sizes[i] = Clamp(remaining * spec.Length.Value / flexTotal, spec.Minimum, spec.Maximum);
            }
            float cursor = horizontal ? rect.X : rect.Y;
            for (int i = 0; i < count; i++)
            {
                InsightRect item = horizontal
                    ? new InsightRect(cursor, rect.Y, sizes[i], rect.Height)
                    : new InsightRect(rect.X, cursor, rect.Width, sizes[i]);
                result.Add(new InsightLayoutBox(ids[i] ?? string.Empty, item));
                cursor += sizes[i] + gap;
            }
            return result;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }

    /// <summary>Shared geometry for the window header controls.</summary>
    internal static class InsightHeaderLayout
    {
        internal static InsightRect DisclosureControls(InsightRect header)
        {
            float right = Math.Max(header.X + 8f, header.Right - 152f);
            float left = Math.Max(header.X + 8f, Math.Min(right, header.Right - 482f));
            return new InsightRect(left, header.Y + 6f, Math.Max(0f, Math.Min(330f, right - left)), 30f);
        }

        internal static InsightRect ToolsButton(InsightRect header) =>
            new InsightRect(header.Right - 144f, header.Y + 6f, 64f, 30f);

        internal static InsightRect ResetButton(InsightRect header) =>
            new InsightRect(header.Right - 72f, header.Y + 6f, 64f, 30f);
    }

    /// <summary>Explicitly invalidated cache for measured component geometry.</summary>
    public sealed class InsightLayoutCache
    {
        private readonly Dictionary<string, CacheEntry> entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

        public IReadOnlyList<InsightLayoutBox> Get(string key, int revision, float width, float height,
            Func<IReadOnlyList<InsightLayoutBox>> factory)
        {
            key = key ?? string.Empty;
            CacheEntry entry;
            if (factory == null) return new InsightLayoutBox[0];
            if (entries.TryGetValue(key, out entry) && entry.Revision == revision &&
                entry.Width.Equals(width) && entry.Height.Equals(height)) return entry.Boxes;
            IReadOnlyList<InsightLayoutBox> boxes = factory() ?? new InsightLayoutBox[0];
            entries[key] = new CacheEntry { Revision = revision, Width = width, Height = height, Boxes = boxes };
            return boxes;
        }

        public void Invalidate(string key)
        {
            if (key == null) return;
            entries.Remove(key);
        }

        public void InvalidateAll() => entries.Clear();

        private sealed class CacheEntry
        {
            public int Revision;
            public float Width;
            public float Height;
            public IReadOnlyList<InsightLayoutBox> Boxes;
        }
    }
}
