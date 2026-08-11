using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Base class for composable, measure/arrange/paint UI elements.</summary>
    public abstract class InsightUiElement
    {
        private static readonly IReadOnlyList<InsightUiElement> NoChildren = new InsightUiElement[0];

        protected InsightUiElement(string id)
        {
            Id = string.IsNullOrWhiteSpace(id) ? GetType().Name : id;
            Style = new InsightUiStyle();
            Visible = true;
        }

        public string Id { get; private set; }
        public InsightUiStyle Style { get; private set; }
        public bool Visible { get; set; }
        public InsightRect LayoutRect { get; private set; }
        public InsightUiSize MeasuredSize { get; private set; }
        public string TooltipText { get; private set; }
        public virtual IReadOnlyList<InsightUiElement> Children => NoChildren;

        public InsightUiElement SetPadding(float value)
        {
            Style.Padding = InsightUiPadding.All(value);
            return this;
        }

        public InsightUiElement SetPadding(float horizontal, float vertical)
        {
            Style.Padding = InsightUiPadding.Symmetric(horizontal, vertical);
            return this;
        }

        public InsightUiElement SetPadding(float left, float top, float right, float bottom)
        {
            Style.Padding = new InsightUiPadding(left, top, right, bottom);
            return this;
        }

        public InsightUiElement SetGap(float gap)
        {
            Style.Gap = Math.Max(0f, gap);
            return this;
        }

        public InsightUiElement SetFlex(float flex)
        {
            Style.Flex = Math.Max(0f, flex);
            return this;
        }

        public InsightUiElement SetWidth(InsightLength width)
        {
            Style.Width = width;
            return this;
        }

        public InsightUiElement SetHeight(InsightLength height)
        {
            Style.Height = height;
            return this;
        }

        public InsightUiElement SetMinSize(float width, float height)
        {
            Style.MinimumWidth = Math.Max(0f, width);
            Style.MinimumHeight = Math.Max(0f, height);
            return this;
        }

        public InsightUiElement SetMaxSize(float width, float height)
        {
            Style.MaximumWidth = width <= 0f ? float.PositiveInfinity : width;
            Style.MaximumHeight = height <= 0f ? float.PositiveInfinity : height;
            return this;
        }

        public InsightUiElement SetBackground(InsightColor color, bool elevated = false)
        {
            Style.Background = color;
            Style.Elevated = elevated;
            return this;
        }

        public InsightUiElement SetBorder(InsightColor color, float width = 1f)
        {
            Style.Border = color;
            Style.BorderWidth = Math.Max(0f, width);
            return this;
        }

        public InsightUiElement SetClip(bool clip = true)
        {
            Style.Clip = clip;
            return this;
        }

        public InsightUiElement SetTooltip(string tooltip)
        {
            TooltipText = tooltip;
            return this;
        }

        public InsightUiElement SetAlignment(InsightAlignment horizontal, InsightAlignment vertical)
        {
            Style.HorizontalAlignment = horizontal;
            Style.VerticalAlignment = vertical;
            return this;
        }

        public InsightUiSize Measure(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (!Visible)
            {
                MeasuredSize = new InsightUiSize(0f, 0f);
                return MeasuredSize;
            }
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            frame.Diagnostics.RecordMeasure();
            InsightUiSize measured = MeasureCore(constraints, frame);
            if (Style.Width.Kind == InsightLengthKind.Fixed)
                measured.Width = Style.Width.Value;
            else if (Style.Width.Kind == InsightLengthKind.Percent && !float.IsPositiveInfinity(constraints.MaxWidth))
                measured.Width = constraints.MaxWidth * Style.Width.Value;
            if (Style.Height.Kind == InsightLengthKind.Fixed)
                measured.Height = Style.Height.Value;
            else if (Style.Height.Kind == InsightLengthKind.Percent && !float.IsPositiveInfinity(constraints.MaxHeight))
                measured.Height = constraints.MaxHeight * Style.Height.Value;
            MeasuredSize = Style.Constrain(measured, constraints);
            return MeasuredSize;
        }

        public void Arrange(InsightRect rect, InsightUiFrame frame)
        {
            if (!Visible) return;
            LayoutRect = rect;
            frame?.Diagnostics.RecordArrange();
            ArrangeCore(rect, frame);
        }

        public void Paint(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (!Visible || LayoutRect.Width <= 0.01f || LayoutRect.Height <= 0.01f) return;
            frame.Diagnostics.RecordVisible();
            if (!string.IsNullOrEmpty(TooltipText)) painter.Tooltip(LayoutRect, TooltipText, frame);
            PaintCore(painter, frame);
        }

        public virtual void Invalidate()
        {
            for (int i = 0; i < Children.Count; i++) Children[i]?.Invalidate();
        }

        protected virtual InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(0f, 0f);
        protected virtual void ArrangeCore(InsightRect rect, InsightUiFrame frame) { }
        protected virtual void PaintCore(IInsightUiPainter painter, InsightUiFrame frame) { }

        internal InsightLayoutSpec MainSpec(InsightUiOrientation orientation)
        {
            InsightLength length = orientation == InsightUiOrientation.Horizontal ? Style.Width : Style.Height;
            float minimum = orientation == InsightUiOrientation.Horizontal ? Style.MinimumWidth : Style.MinimumHeight;
            float maximum = orientation == InsightUiOrientation.Horizontal ? Style.MaximumWidth : Style.MaximumHeight;
            float preferred = orientation == InsightUiOrientation.Horizontal ? MeasuredSize.Width : MeasuredSize.Height;
            if (Style.Flex > 0f) return InsightLayoutSpec.Flexible(minimum, preferred, Style.Flex);
            if (length.Kind == InsightLengthKind.Fixed)
                return InsightLayoutSpec.Fixed(length.Value, minimum);
            if (length.Kind == InsightLengthKind.Percent)
                return new InsightLayoutSpec(length, minimum, preferred, maximum);
            return InsightLayoutSpec.Auto(minimum, preferred);
        }

        internal InsightRect AlignCrossAxis(InsightRect slot, InsightUiOrientation orientation)
        {
            if (orientation == InsightUiOrientation.Horizontal)
            {
                float height = Style.VerticalAlignment == InsightAlignment.Stretch ? slot.Height : MeasuredSize.Height;
                return InsightLayout.Align(slot, slot.Width, height, Style.VerticalAlignment);
            }
            float width = Style.HorizontalAlignment == InsightAlignment.Stretch ? slot.Width : MeasuredSize.Width;
            return InsightLayout.Align(slot, width, slot.Height, Style.HorizontalAlignment);
        }

        protected InsightUiPadding ScaledPadding(InsightUiFrame frame)
        {
            InsightUiPadding padding = Style.Padding;
            return new InsightUiPadding(frame.Spacing(padding.Left), frame.Spacing(padding.Top),
                frame.Spacing(padding.Right), frame.Spacing(padding.Bottom));
        }

        protected float EffectiveGap(InsightUiFrame frame)
        {
            return frame.Spacing(Style.Gap > 0f ? Style.Gap : frame.Theme.Spacing);
        }
    }

    /// <summary>Convenient factory methods for the public composable API.</summary>
    public static class InsightUi
    {
        public static InsightUiStack Row(string id, params InsightUiElement[] children) =>
            new InsightUiStack(id, InsightUiOrientation.Horizontal, InsightUiWrapMode.NoWrap).Add(children);

        public static InsightUiStack Column(string id, params InsightUiElement[] children) =>
            new InsightUiStack(id, InsightUiOrientation.Vertical, InsightUiWrapMode.NoWrap).Add(children);

        public static InsightUiStack Wrap(string id, params InsightUiElement[] children) =>
            new InsightUiStack(id, InsightUiOrientation.Horizontal, InsightUiWrapMode.Wrap).Add(children);

        public static InsightUiSurface Surface(string id, InsightUiElement child = null) => new InsightUiSurface(id, child);
        public static InsightUiLabel Label(string id, string text, InsightUiTextStyle style = InsightUiTextStyle.Body) =>
            new InsightUiLabel(id, text, style);
        public static InsightUiButton Button(string id, string label, Action onClick = null) => new InsightUiButton(id, label, onClick);
        public static InsightUiToggle Toggle(string id, string label, bool value = false, Action<bool> changed = null) =>
            new InsightUiToggle(id, label, value, changed);
        public static InsightUiSlider Slider(string id, float value, float minimum, float maximum, Action<float> changed = null) =>
            new InsightUiSlider(id, value, minimum, maximum, changed);
        public static InsightUiTextField TextField(string id, string value = "", Action<string> changed = null) =>
            new InsightUiTextField(id, value, changed);
        public static InsightUiBadge Badge(string id, string text, InsightColor? color = null) =>
            new InsightUiBadge(id, text, color);
        public static InsightUiProgress Progress(string id, float value, InsightColor? color = null) =>
            new InsightUiProgress(id, value, color);
        public static InsightUiIconButton IconButton(string id, string icon, Action onClick = null) =>
            new InsightUiIconButton(id, icon, onClick);
        public static InsightUiBreadcrumbs Breadcrumbs(string id, params string[] labels) =>
            new InsightUiBreadcrumbs(id).Add(labels);
        public static InsightUiDivider Divider(string id = "divider") => new InsightUiDivider(id);
        public static InsightUiSpacer Spacer(string id, float width = 0f, float height = 0f) => new InsightUiSpacer(id, width, height);
        public static InsightUiGrid Grid(string id, float minimumColumnWidth = 180f) => new InsightUiGrid(id, minimumColumnWidth);
        public static InsightUiSplit Split(string id, InsightUiElement first, InsightUiElement second, float ratio = 0.5f) =>
            new InsightUiSplit(id, first, second, ratio);
        public static InsightUiScroll Scroll(string id, InsightUiElement child) => new InsightUiScroll(id, child);
        public static InsightUiTabs Tabs(string id) => new InsightUiTabs(id);
        public static InsightUiVirtualList VirtualList(string id, int itemCount, float itemHeight,
            Func<int, InsightUiElement> itemFactory) => new InsightUiVirtualList(id, itemCount, itemHeight, itemFactory);
        public static InsightUiElement Empty(string id, string message = null) => new InsightUiLabel(id, message ?? string.Empty);
    }

    /// <summary>Simple row/column container with flex, min/max, and optional wrapping.</summary>
    public sealed class InsightUiStack : InsightUiElement
    {
        private readonly List<InsightUiElement> children = new List<InsightUiElement>();

        public InsightUiStack(string id, InsightUiOrientation orientation, InsightUiWrapMode wrapMode = InsightUiWrapMode.NoWrap)
            : base(id)
        {
            Orientation = orientation;
            WrapMode = wrapMode;
        }

        public InsightUiOrientation Orientation { get; private set; }
        public InsightUiWrapMode WrapMode { get; private set; }
        public override IReadOnlyList<InsightUiElement> Children => children;

        public InsightUiStack Add(params InsightUiElement[] elements)
        {
            if (elements == null) return this;
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] != null) children.Add(elements[i]);
            return this;
        }

        public InsightUiStack Clear()
        {
            children.Clear();
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            float availableWidth = Math.Max(0f, constraints.MaxWidth - padding.Horizontal);
            float availableHeight = Math.Max(0f, constraints.MaxHeight - padding.Vertical);
            InsightUiConstraints inner = new InsightUiConstraints(0f, availableWidth, 0f, availableHeight);
            float gap = EffectiveGap(frame);
            if (children.Count == 0) return new InsightUiSize(padding.Horizontal, padding.Vertical);

            if (WrapMode == InsightUiWrapMode.Wrap && Orientation == InsightUiOrientation.Horizontal)
            {
                float lineWidth = 0f;
                float lineHeight = 0f;
                float totalHeight = 0f;
                float widest = 0f;
                for (int i = 0; i < children.Count; i++)
                {
                    InsightUiSize size = children[i].Measure(inner, frame);
                    float next = lineWidth <= 0f ? size.Width : lineWidth + gap + size.Width;
                    if (lineWidth > 0f && !float.IsPositiveInfinity(availableWidth) && next > availableWidth)
                    {
                        widest = Math.Max(widest, lineWidth);
                        totalHeight += lineHeight + (totalHeight > 0f ? gap : 0f);
                        lineWidth = size.Width;
                        lineHeight = size.Height;
                    }
                    else
                    {
                        lineWidth = next;
                        lineHeight = Math.Max(lineHeight, size.Height);
                    }
                }
                widest = Math.Max(widest, lineWidth);
                totalHeight += lineHeight;
                return new InsightUiSize(padding.Horizontal + widest, padding.Vertical + totalHeight);
            }

            float width = 0f;
            float height = 0f;
            for (int i = 0; i < children.Count; i++)
            {
                InsightUiSize size = children[i].Measure(inner, frame);
                if (Orientation == InsightUiOrientation.Horizontal)
                {
                    width += size.Width;
                    height = Math.Max(height, size.Height);
                }
                else
                {
                    width = Math.Max(width, size.Width);
                    height += size.Height;
                }
            }
            if (Orientation == InsightUiOrientation.Horizontal) width += gap * Math.Max(0, children.Count - 1);
            else height += gap * Math.Max(0, children.Count - 1);
            return new InsightUiSize(padding.Horizontal + width, padding.Vertical + height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            if (children.Count == 0) return;
            InsightUiPadding padding = ScaledPadding(frame);
            InsightRect inner = new InsightRect(rect.X + padding.Left, rect.Y + padding.Top,
                Math.Max(0f, rect.Width - padding.Horizontal), Math.Max(0f, rect.Height - padding.Vertical));
            float gap = EffectiveGap(frame);
            if (WrapMode == InsightUiWrapMode.Wrap && Orientation == InsightUiOrientation.Horizontal)
            {
                ArrangeWrapped(inner, gap, frame);
                return;
            }

            List<string> ids = new List<string>(children.Count);
            List<InsightLayoutSpec> specs = new List<InsightLayoutSpec>(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                ids.Add(children[i].Id);
                specs.Add(children[i].MainSpec(Orientation));
            }
            IReadOnlyList<InsightLayoutBox> boxes = Orientation == InsightUiOrientation.Horizontal
                ? InsightLayout.ArrangeRow(inner, ids, specs, gap)
                : InsightLayout.ArrangeColumn(inner, ids, specs, gap);
            for (int i = 0; i < children.Count && i < boxes.Count; i++)
                children[i].Arrange(children[i].AlignCrossAxis(boxes[i].Rect, Orientation), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            for (int i = 0; i < children.Count; i++) children[i].Paint(painter, frame);
        }

        private void ArrangeWrapped(InsightRect rect, float gap, InsightUiFrame frame)
        {
            float x = rect.X;
            float y = rect.Y;
            float lineHeight = 0f;
            for (int i = 0; i < children.Count; i++)
            {
                InsightUiElement child = children[i];
                float width = child.MeasuredSize.Width;
                if (x > rect.X && x + width > rect.Right)
                {
                    x = rect.X;
                    y += lineHeight + gap;
                    lineHeight = 0f;
                }
                InsightRect slot = new InsightRect(x, y, Math.Min(width, rect.Right - x), child.MeasuredSize.Height);
                child.Arrange(child.AlignCrossAxis(slot, Orientation), frame);
                x += slot.Width + gap;
                lineHeight = Math.Max(lineHeight, slot.Height);
            }
        }
    }

    /// <summary>Surface/card element that scopes padding, background, border, and elevation.</summary>
    public sealed class InsightUiSurface : InsightUiElement
    {
        private readonly InsightUiElement child;

        public InsightUiSurface(string id, InsightUiElement child) : base(id)
        {
            this.child = child;
            Style.Padding = InsightUiPadding.All(10f);
            Style.Elevated = true;
        }

        public InsightUiElement Child => child;
        public override IReadOnlyList<InsightUiElement> Children => child == null ? new InsightUiElement[0] : new[] { child };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            InsightUiSize size = child == null ? new InsightUiSize(0f, 0f) : child.Measure(
                new InsightUiConstraints(0f, Math.Max(0f, constraints.MaxWidth - padding.Horizontal), 0f,
                    Math.Max(0f, constraints.MaxHeight - padding.Vertical)), frame);
            return new InsightUiSize(size.Width + padding.Horizontal, size.Height + padding.Vertical);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            if (child == null) return;
            InsightUiPadding padding = ScaledPadding(frame);
            child.Arrange(new InsightRect(rect.X + padding.Left, rect.Y + padding.Top,
                Math.Max(0f, rect.Width - padding.Horizontal), Math.Max(0f, rect.Height - padding.Vertical)), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            painter.Surface(LayoutRect, Style, frame);
            child?.Paint(painter, frame);
        }
    }

    /// <summary>Text element with renderer-provided measurement.</summary>
    public sealed class InsightUiLabel : InsightUiElement
    {
        public InsightUiLabel(string id, string text, InsightUiTextStyle style = InsightUiTextStyle.Body) : base(id)
        {
            Text = text ?? string.Empty;
            TextStyle = style;
            Wrap = true;
        }

        public string Text { get; set; }
        public InsightUiTextStyle TextStyle { get; set; }
        public bool Wrap { get; set; }
        public InsightColor? Color { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return frame.MeasureText(Text, TextStyle, constraints.MaxWidth);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            painter.Text(LayoutRect, Text, TextStyle, Color, Wrap, frame);
        }
    }

    /// <summary>Action button or selectable row.</summary>
    public class InsightUiButton : InsightUiElement
    {
        public InsightUiButton(string id, string label, Action onClick = null) : base(id)
        {
            Label = label ?? string.Empty;
            OnClick = onClick;
            Style.Padding = InsightUiPadding.Symmetric(10f, 6f);
            Style.MinimumHeight = 28f;
        }

        public string Label { get; set; }
        public Action OnClick { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Selected { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            InsightUiSize text = frame.MeasureText(Label, InsightUiTextStyle.Button, constraints.MaxWidth);
            return new InsightUiSize(text.Width + padding.Horizontal, Math.Max(28f, text.Height + padding.Vertical));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (painter.Button(LayoutRect, Label, Enabled, Selected, frame)) OnClick?.Invoke();
        }
    }

    /// <summary>Compact semantic status pill.</summary>
    public sealed class InsightUiBadge : InsightUiElement
    {
        public InsightUiBadge(string id, string text, InsightColor? color = null) : base(id)
        {
            Text = text ?? string.Empty;
            Color = color;
            Style.Padding = InsightUiPadding.Symmetric(7f, 3f);
            Style.MinimumHeight = 22f;
        }

        public string Text { get; set; }
        public InsightColor? Color { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            InsightUiSize size = frame.MeasureText(Text, InsightUiTextStyle.Caption, constraints.MaxWidth);
            return new InsightUiSize(size.Width + padding.Horizontal, Math.Max(22f, size.Height + padding.Vertical));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            InsightUiStyle style = Style.Clone();
            style.Background = (Color ?? frame.Theme.Selected).WithAlpha(0.25f);
            style.Border = Color ?? frame.Theme.Selected;
            painter.Surface(LayoutRect, style, frame);
            painter.Text(new InsightRect(LayoutRect.X + 7f, LayoutRect.Y + 3f,
                Math.Max(0f, LayoutRect.Width - 14f), Math.Max(0f, LayoutRect.Height - 6f)), Text,
                InsightUiTextStyle.Caption, Color ?? frame.Theme.PrimaryText, false, frame);
        }
    }

    /// <summary>Compact progress indicator with a themed track and fill.</summary>
    public sealed class InsightUiProgress : InsightUiElement
    {
        public InsightUiProgress(string id, float value, InsightColor? color = null) : base(id)
        {
            Value = value;
            Color = color;
            Style.MinimumHeight = 8f;
        }

        public float Value { get; set; }
        public InsightColor? Color { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            new InsightUiSize(Math.Min(240f, constraints.MaxWidth), 8f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame) =>
            painter.Progress(LayoutRect, Value, Color ?? frame.Theme.Selected, frame);
    }

    /// <summary>Small icon/text action that shares button interaction and theme behavior.</summary>
    public sealed class InsightUiIconButton : InsightUiButton
    {
        public InsightUiIconButton(string id, string icon, Action onClick = null) : base(id, icon, onClick)
        {
            Icon = icon ?? string.Empty;
            Style.MinimumWidth = 32f;
            Style.MaximumWidth = 42f;
            SetTooltip(icon);
        }

        public string Icon { get; private set; }
    }

    /// <summary>Non-interactive breadcrumb trail for nested screens and inspectors.</summary>
    public sealed class InsightUiBreadcrumbs : InsightUiElement
    {
        private readonly List<string> labels = new List<string>();

        public InsightUiBreadcrumbs(string id) : base(id) { }
        public IReadOnlyList<string> Labels => labels;

        public InsightUiBreadcrumbs Add(params string[] values)
        {
            if (values == null) return this;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) labels.Add(values[i]);
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float width = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                width += frame.MeasureText(labels[i], InsightUiTextStyle.Caption, constraints.MaxWidth).Width;
                if (i + 1 < labels.Count) width += frame.MeasureText("/", InsightUiTextStyle.Caption, constraints.MaxWidth).Width + 10f;
            }
            return new InsightUiSize(width, 20f);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float x = LayoutRect.X;
            for (int i = 0; i < labels.Count; i++)
            {
                InsightUiSize size = frame.MeasureText(labels[i], InsightUiTextStyle.Caption, LayoutRect.Width);
                painter.Text(new InsightRect(x, LayoutRect.Y, size.Width, LayoutRect.Height), labels[i],
                    InsightUiTextStyle.Caption, i + 1 == labels.Count ? frame.Theme.PrimaryText : frame.Theme.SecondaryText,
                    false, frame);
                x += size.Width;
                if (i + 1 < labels.Count)
                {
                    painter.Text(new InsightRect(x + 5f, LayoutRect.Y, 8f, LayoutRect.Height), "/",
                        InsightUiTextStyle.Caption, frame.Theme.SecondaryText, false, frame);
                    x += 18f;
                }
            }
        }
    }

    /// <summary>Stateful checkbox/toggle control.</summary>
    public sealed class InsightUiToggle : InsightUiElement
    {
        public InsightUiToggle(string id, string label, bool value, Action<bool> changed = null) : base(id)
        {
            Label = label ?? string.Empty;
            Value = value;
            Changed = changed;
            Style.MinimumHeight = 28f;
        }

        public string Label { get; set; }
        public bool Value { get; set; }
        public bool Enabled { get; set; } = true;
        public Action<bool> Changed { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiSize text = frame.MeasureText(Label, InsightUiTextStyle.Body, constraints.MaxWidth);
            return new InsightUiSize(Math.Max(180f, text.Width + 30f), Math.Max(28f, text.Height));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = Id + ".value";
            bool value = frame.State.GetBool(key, Value);
            bool changed = painter.Toggle(LayoutRect, Label, value, Enabled, frame);
            if (changed != value)
            {
                frame.State.SetBool(key, changed);
                Value = changed;
                Changed?.Invoke(changed);
            }
        }
    }

    /// <summary>Stateful scalar slider.</summary>
    public sealed class InsightUiSlider : InsightUiElement
    {
        public InsightUiSlider(string id, float value, float minimum, float maximum, Action<float> changed = null) : base(id)
        {
            Value = value;
            Minimum = Math.Min(minimum, maximum);
            Maximum = Math.Max(minimum, maximum);
            Changed = changed;
            Style.MinimumHeight = 28f;
        }

        public float Value { get; set; }
        public float Minimum { get; private set; }
        public float Maximum { get; private set; }
        public bool Enabled { get; set; } = true;
        public Action<float> Changed { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(220f, 28f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = Id + ".value";
            float value = frame.State.GetFloat(key, Value);
            float next = painter.Slider(LayoutRect, value, Minimum, Maximum, Enabled, frame);
            if (Math.Abs(next - value) > 0.0001f)
            {
                frame.State.SetFloat(key, next);
                Value = next;
                Changed?.Invoke(next);
            }
        }
    }

    /// <summary>Stateful text field.</summary>
    public sealed class InsightUiTextField : InsightUiElement
    {
        public InsightUiTextField(string id, string value, Action<string> changed = null) : base(id)
        {
            Value = value ?? string.Empty;
            Changed = changed;
            Style.MinimumHeight = 28f;
        }

        public string Value { get; set; }
        public bool Enabled { get; set; } = true;
        public Action<string> Changed { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(240f, 28f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = Id + ".value";
            string value = frame.State.GetString(key, Value);
            string next = painter.TextField(LayoutRect, value, Enabled, frame) ?? string.Empty;
            if (next != value)
            {
                frame.State.SetString(key, next);
                Value = next;
                Changed?.Invoke(next);
            }
        }
    }

    /// <summary>One-pixel visual separator.</summary>
    public sealed class InsightUiDivider : InsightUiElement
    {
        public InsightUiDivider(string id) : base(id)
        {
            Style.MinimumHeight = 1f;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(1f, 1f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            painter.Divider(LayoutRect, frame.Theme.SecondaryText.WithAlpha(0.35f), frame);
        }
    }

    /// <summary>Fixed or flexible empty space.</summary>
    public sealed class InsightUiSpacer : InsightUiElement
    {
        public InsightUiSpacer(string id, float width, float height) : base(id)
        {
            Style.MinimumWidth = Math.Max(0f, width);
            Style.MinimumHeight = Math.Max(0f, height);
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            new InsightUiSize(Style.MinimumWidth, Style.MinimumHeight);
    }

    /// <summary>Adaptive grid that chooses columns from available width.</summary>
    public sealed class InsightUiGrid : InsightUiElement
    {
        private readonly List<InsightUiElement> children = new List<InsightUiElement>();

        public InsightUiGrid(string id, float minimumColumnWidth) : base(id)
        {
            MinimumColumnWidth = Math.Max(1f, minimumColumnWidth);
            Style.Gap = 8f;
        }

        public float MinimumColumnWidth { get; set; }
        public override IReadOnlyList<InsightUiElement> Children => children;

        public InsightUiGrid Add(params InsightUiElement[] elements)
        {
            if (elements == null) return this;
            for (int i = 0; i < elements.Length; i++) if (elements[i] != null) children.Add(elements[i]);
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (children.Count == 0) return new InsightUiSize(0f, 0f);
            float width = float.IsPositiveInfinity(constraints.MaxWidth) ? MinimumColumnWidth : constraints.MaxWidth;
            int columns = Math.Max(1, (int)Math.Floor((width + EffectiveGap(frame)) / (MinimumColumnWidth + EffectiveGap(frame))));
            float cellWidth = Math.Max(1f, (width - EffectiveGap(frame) * (columns - 1)) / columns);
            int rows = (children.Count + columns - 1) / columns;
            float[] rowHeights = new float[rows];
            for (int i = 0; i < children.Count; i++)
            {
                InsightUiSize size = children[i].Measure(new InsightUiConstraints(0f, cellWidth, 0f, constraints.MaxHeight), frame);
                rowHeights[i / columns] = Math.Max(rowHeights[i / columns], size.Height);
            }
            float height = 0f;
            for (int i = 0; i < rowHeights.Length; i++) height += rowHeights[i];
            height += EffectiveGap(frame) * Math.Max(0, rows - 1);
            return new InsightUiSize(width, height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            if (children.Count == 0) return;
            float gap = EffectiveGap(frame);
            int columns = Math.Max(1, (int)Math.Floor((rect.Width + gap) / (MinimumColumnWidth + gap)));
            float cellWidth = Math.Max(0f, (rect.Width - gap * (columns - 1)) / columns);
            int rows = (children.Count + columns - 1) / columns;
            float[] rowHeights = new float[rows];
            for (int i = 0; i < children.Count; i++) rowHeights[i / columns] = Math.Max(rowHeights[i / columns], children[i].MeasuredSize.Height);
            float y = rect.Y;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= children.Count) break;
                    InsightRect slot = new InsightRect(rect.X + column * (cellWidth + gap), y, cellWidth, rowHeights[row]);
                    children[index].Arrange(children[index].AlignCrossAxis(slot, InsightUiOrientation.Vertical), frame);
                }
                y += rowHeights[row] + gap;
            }
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            for (int i = 0; i < children.Count; i++) children[i].Paint(painter, frame);
        }
    }

    /// <summary>Two-pane container with a stable, clamped split ratio.</summary>
    public sealed class InsightUiSplit : InsightUiElement
    {
        private readonly InsightUiElement first;
        private readonly InsightUiElement second;

        public InsightUiSplit(string id, InsightUiElement first, InsightUiElement second, float ratio) : base(id)
        {
            this.first = first;
            this.second = second;
            Ratio = Clamp(ratio, 0.1f, 0.9f);
            Style.Gap = 8f;
        }

        public float Ratio { get; set; }
        public InsightUiOrientation Orientation { get; set; } = InsightUiOrientation.Horizontal;
        public override IReadOnlyList<InsightUiElement> Children => new[] { first, second };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiSize a = first?.Measure(constraints, frame) ?? new InsightUiSize(0f, 0f);
            InsightUiSize b = second?.Measure(constraints, frame) ?? new InsightUiSize(0f, 0f);
            float gap = EffectiveGap(frame);
            return Orientation == InsightUiOrientation.Horizontal
                ? new InsightUiSize(a.Width + b.Width + gap, Math.Max(a.Height, b.Height))
                : new InsightUiSize(Math.Max(a.Width, b.Width), a.Height + b.Height + gap);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            float gap = EffectiveGap(frame);
            float ratio = Clamp(Ratio, 0.1f, 0.9f);
            if (Orientation == InsightUiOrientation.Horizontal)
            {
                float firstWidth = Math.Max(0f, (rect.Width - gap) * ratio);
                InsightRect a = new InsightRect(rect.X, rect.Y, firstWidth, rect.Height);
                InsightRect b = new InsightRect(rect.X + firstWidth + gap, rect.Y, Math.Max(0f, rect.Width - firstWidth - gap), rect.Height);
                first?.Arrange(a, frame);
                second?.Arrange(b, frame);
            }
            else
            {
                float firstHeight = Math.Max(0f, (rect.Height - gap) * ratio);
                InsightRect a = new InsightRect(rect.X, rect.Y, rect.Width, firstHeight);
                InsightRect b = new InsightRect(rect.X, rect.Y + firstHeight + gap, rect.Width, Math.Max(0f, rect.Height - firstHeight - gap));
                first?.Arrange(a, frame);
                second?.Arrange(b, frame);
            }
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            first?.Paint(painter, frame);
            second?.Paint(painter, frame);
        }

        private static float Clamp(float value, float minimum, float maximum) => value < minimum ? minimum : value > maximum ? maximum : value;
    }

    /// <summary>Scrollable content whose offset is stored by this document, not globally.</summary>
    public sealed class InsightUiScroll : InsightUiElement
    {
        private readonly InsightUiElement child;

        public InsightUiScroll(string id, InsightUiElement child) : base(id)
        {
            this.child = child;
            Style.Clip = true;
        }

        public InsightUiElement Child => child;
        public override IReadOnlyList<InsightUiElement> Children => child == null ? new InsightUiElement[0] : new[] { child };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (child == null) return new InsightUiSize(0f, 0f);
            InsightUiSize size = child.Measure(new InsightUiConstraints(constraints.MinWidth, constraints.MaxWidth, 0f,
                float.PositiveInfinity), frame);
            return new InsightUiSize(size.Width, Math.Min(size.Height, constraints.MaxHeight));
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            float offset = frame.State.GetFloat(Id + ".scrollY", 0f);
            ArrangeChild(rect, offset, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (child == null) return;
            float offset = frame.State.GetFloat(Id + ".scrollY", 0f);
            offset = painter.ScrollOffset(LayoutRect, child.MeasuredSize.Height, offset, Id + ".scrollY", frame);
            frame.State.SetFloat(Id + ".scrollY", offset);
            ArrangeChild(LayoutRect, offset, frame);
            painter.BeginClip(LayoutRect);
            child.Paint(painter, frame);
            painter.EndClip();
        }

        private void ArrangeChild(InsightRect rect, float offset, InsightUiFrame frame)
        {
            child.Arrange(new InsightRect(rect.X, rect.Y - Math.Max(0f, offset), rect.Width, child.MeasuredSize.Height), frame);
        }
    }

    /// <summary>One tab in an InsightUiTabs container.</summary>
    public sealed class InsightUiTab
    {
        public InsightUiTab(string id, string label, InsightUiElement content)
        {
            Id = id ?? string.Empty;
            Label = label ?? id ?? string.Empty;
            Content = content ?? InsightUi.Empty(id + ".empty", "No content");
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public InsightUiElement Content { get; private set; }
    }

    /// <summary>Stateful tab strip whose content remains an ordinary composable element.</summary>
    public sealed class InsightUiTabs : InsightUiElement
    {
        private readonly List<InsightUiTab> tabs = new List<InsightUiTab>();
        private readonly List<InsightUiElement> children = new List<InsightUiElement>();

        public InsightUiTabs(string id) : base(id)
        {
            Style.Gap = 6f;
        }

        public IReadOnlyList<InsightUiTab> Tabs => tabs;
        public string ActiveTabId { get; private set; }
        public override IReadOnlyList<InsightUiElement> Children => children;

        public InsightUiTabs Add(string id, string label, InsightUiElement content)
        {
            InsightUiTab tab = new InsightUiTab(id, label, content);
            tabs.Add(tab);
            children.Add(tab.Content);
            if (ActiveTabId == null) ActiveTabId = tab.Id;
            return this;
        }

        public InsightUiTabs Select(string id)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Id == id)
                {
                    ActiveTabId = id;
                    break;
                }
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiTab active = Active(frame);
            float headerHeight = 32f;
            InsightUiSize content = active?.Content.Measure(new InsightUiConstraints(0f, constraints.MaxWidth, 0f,
                Math.Max(0f, constraints.MaxHeight - headerHeight - EffectiveGap(frame))), frame) ?? new InsightUiSize(0f, 0f);
            return new InsightUiSize(Math.Max(content.Width, tabs.Count * 90f), headerHeight + EffectiveGap(frame) + content.Height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            InsightRect header = new InsightRect(rect.X, rect.Y, rect.Width, 32f);
            InsightUiTab active = Active(frame);
            active?.Content.Arrange(new InsightRect(rect.X, rect.Y + 32f + EffectiveGap(frame), rect.Width,
                Math.Max(0f, rect.Height - 32f - EffectiveGap(frame))), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            InsightRect header = new InsightRect(LayoutRect.X, LayoutRect.Y, LayoutRect.Width, 32f);
            float width = tabs.Count == 0 ? header.Width : header.Width / tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                InsightUiTab tab = tabs[i];
                InsightRect button = new InsightRect(header.X + i * width, header.Y, width - 4f, header.Height);
                if (painter.Button(button, tab.Label, true, tab.Id == ActiveTabId, frame))
                {
                    ActiveTabId = tab.Id;
                    frame.State.SetString(Id + ".active", ActiveTabId);
                }
            }
            Active(frame)?.Content.Paint(painter, frame);
        }

        private InsightUiTab Active(InsightUiFrame frame)
        {
            string stored = frame.State.GetString(Id + ".active", ActiveTabId);
            if (!string.IsNullOrEmpty(stored)) ActiveTabId = stored;
            for (int i = 0; i < tabs.Count; i++) if (tabs[i].Id == ActiveTabId) return tabs[i];
            return tabs.Count == 0 ? null : tabs[0];
        }
    }

    /// <summary>Visible item range used by virtualized list implementations.</summary>
    public struct InsightVirtualizedRange
    {
        public int Start;
        public int End;

        public InsightVirtualizedRange(int start, int end)
        {
            Start = Math.Max(0, start);
            End = Math.Max(Start, end);
        }

        public int Count => End - Start;
        public bool Contains(int index) => index >= Start && index < End;
    }

    /// <summary>Pure virtualization math shared by list widgets and portable tests.</summary>
    public static class InsightVirtualization
    {
        public static InsightVirtualizedRange Range(int itemCount, float itemHeight, float viewportHeight, float scrollOffset,
            int overscan = 2)
        {
            itemCount = Math.Max(0, itemCount);
            itemHeight = Math.Max(0.01f, itemHeight);
            viewportHeight = Math.Max(0f, viewportHeight);
            scrollOffset = Math.Max(0f, scrollOffset);
            overscan = Math.Max(0, overscan);
            int start = Math.Max(0, (int)Math.Floor(scrollOffset / itemHeight) - overscan);
            int visible = (int)Math.Ceiling(viewportHeight / itemHeight) + overscan * 2 + 1;
            return new InsightVirtualizedRange(Math.Min(start, itemCount), Math.Min(itemCount, start + visible));
        }

        public static float ContentHeight(int itemCount, float itemHeight) => Math.Max(0, itemCount) * Math.Max(0f, itemHeight);
    }

    /// <summary>Fixed-height virtualized list. Only the visible range is measured, arranged, and painted.</summary>
    public sealed class InsightUiVirtualList : InsightUiElement
    {
        private readonly Dictionary<int, InsightUiElement> itemElements = new Dictionary<int, InsightUiElement>();
        private readonly List<InsightUiElement> visibleElements = new List<InsightUiElement>();
        private readonly Func<int, InsightUiElement> itemFactory;

        public InsightUiVirtualList(string id, int itemCount, float itemHeight, Func<int, InsightUiElement> itemFactory)
            : base(id)
        {
            ItemCount = Math.Max(0, itemCount);
            ItemHeight = Math.Max(1f, itemHeight);
            this.itemFactory = itemFactory;
            Overscan = 2;
            Style.Clip = true;
        }

        public int ItemCount { get; set; }
        public float ItemHeight { get; set; }
        public int Overscan { get; set; }
        public override IReadOnlyList<InsightUiElement> Children => visibleElements;

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float height = float.IsPositiveInfinity(constraints.MaxHeight)
                ? Math.Min(InsightVirtualization.ContentHeight(ItemCount, ItemHeight), 480f)
                : constraints.MaxHeight;
            return new InsightUiSize(float.IsPositiveInfinity(constraints.MaxWidth) ? 280f : constraints.MaxWidth, height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            ArrangeVisible(rect, frame.State.GetFloat(Id + ".scrollY", 0f), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float offset = frame.State.GetFloat(Id + ".scrollY", 0f);
            offset = painter.ScrollOffset(LayoutRect, InsightVirtualization.ContentHeight(ItemCount, ItemHeight),
                offset, Id + ".scrollY", frame);
            frame.State.SetFloat(Id + ".scrollY", offset);
            ArrangeVisible(LayoutRect, offset, frame);
            painter.BeginClip(LayoutRect);
            for (int i = 0; i < visibleElements.Count; i++) visibleElements[i].Paint(painter, frame);
            painter.EndClip();
        }

        private void ArrangeVisible(InsightRect rect, float offset, InsightUiFrame frame)
        {
            visibleElements.Clear();
            InsightVirtualizedRange range = InsightVirtualization.Range(ItemCount, ItemHeight, rect.Height, offset, Overscan);
            for (int index = range.Start; index < range.End; index++)
            {
                InsightUiElement element;
                if (!itemElements.TryGetValue(index, out element))
                {
                    element = itemFactory == null ? null : itemFactory(index);
                    if (element != null) itemElements[index] = element;
                }
                if (element == null) continue;
                element.Measure(new InsightUiConstraints(0f, rect.Width, ItemHeight, ItemHeight), frame);
                element.Arrange(new InsightRect(rect.X, rect.Y + index * ItemHeight - offset, rect.Width, ItemHeight), frame);
                visibleElements.Add(element);
            }
        }
    }
}
