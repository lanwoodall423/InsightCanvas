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

        /// <summary>Sets an element-specific radius; negative values restore the theme fallback and zero is square.</summary>
        public InsightUiElement SetCornerRadius(float radius)
        {
            Style.CornerRadius = float.IsNaN(radius) ? -1f : radius < 0f ? -1f : radius;
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
            if (!Visible)
            {
                CloseTransient(frame?.State);
                return;
            }
            if (LayoutRect.Width <= 0.01f || LayoutRect.Height <= 0.01f) return;
            frame.Diagnostics.RecordVisible();
            if (Focusable || StateBearing) frame.RegisterElement(this, Focusable, TextInput);
            if (!string.IsNullOrEmpty(TooltipText)) painter.Tooltip(LayoutRect, TooltipText, frame);
            PaintCore(painter, frame);
            IInsightUiFocusPainter focusPainter = painter as IInsightUiFocusPainter;
            if (Focusable && focusPainter != null && frame.Focus.IsFocused(frame.EffectiveId(Id)))
                focusPainter.FocusRing(LayoutRect, frame);
        }

        public virtual void Invalidate()
        {
            for (int i = 0; i < Children.Count; i++) Children[i]?.Invalidate();
        }

        internal virtual void CloseTransient(InsightUiStateStore state)
        {
            for (int i = 0; i < Children.Count; i++) Children[i]?.CloseTransient(state);
        }

        protected virtual InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(0f, 0f);
        protected virtual void ArrangeCore(InsightRect rect, InsightUiFrame frame) { }
        protected virtual void PaintCore(IInsightUiPainter painter, InsightUiFrame frame) { }
        protected virtual bool Focusable => false;
        protected virtual bool TextInput => false;
        protected virtual bool StateBearing => false;

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
        public static InsightUiScope Scope(string id, InsightUiElement child) => new InsightUiScope(id, child);
        /// <summary>Creates persistent explanatory content with a semantic status tone.</summary>
        public static InsightUiCallout Callout(string id, InsightUiCalloutSeverity severity, string title,
            string body = null) => new InsightUiCallout(id, severity, title, body);
        /// <summary>Creates a compact section hierarchy with optional supporting content.</summary>
        public static InsightUiSectionHeader SectionHeader(string id, string title, string subtitle = null,
            InsightUiIcon icon = null, InsightUiElement trailing = null, bool divider = false) =>
            new InsightUiSectionHeader(id, title, subtitle, icon, trailing, divider);
        /// <summary>Creates a normalized current/max meter built on the progress primitive.</summary>
        public static InsightUiMeter Meter(string id, float current, float maximum) =>
            new InsightUiMeter(id, current, maximum);
        /// <summary>Creates a compact inspector row with a trailing value.</summary>
        public static InsightUiStatRow StatRow(string id, string label, string value) =>
            new InsightUiStatRow(id, label, value);
        public static InsightUiLabel Label(string id, string text, InsightUiTextStyle style = InsightUiTextStyle.Body) =>
            new InsightUiLabel(id, text, style);
        public static InsightUiButton Button(string id, string label, Action onClick = null) => new InsightUiButton(id, label, onClick);
        public static InsightUiToggle Toggle(string id, string label, bool value = false, Action<bool> changed = null) =>
            new InsightUiToggle(id, label, value, changed);
        public static InsightUiSlider Slider(string id, float value, float minimum, float maximum, Action<float> changed = null) =>
            new InsightUiSlider(id, value, minimum, maximum, changed);
        public static InsightUiTextField TextField(string id, string value = "", Action<string> changed = null) =>
            new InsightUiTextField(id, value, changed);
        public static InsightUiSelect Select(string id, string label, string[] options, int selected = 0,
            Action<int, string> changed = null) => new InsightUiSelect(id, label, options, selected, changed);
        public static InsightUiExpander Expander(string id, string label, InsightUiElement content, bool expanded = false) =>
            new InsightUiExpander(id, label, content, expanded);
        public static InsightUiBadge Badge(string id, string text, InsightColor? color = null) =>
            new InsightUiBadge(id, text, color);
        public static InsightUiProgress Progress(string id, float value, InsightColor? color = null) =>
            new InsightUiProgress(id, value, color);
        public static InsightUiIconButton IconButton(string id, string icon, Action onClick = null) =>
            new InsightUiIconButton(id, icon, onClick);
        public static InsightUiIconButton IconButton(string id, InsightUiIcon icon, Action onClick = null) =>
            new InsightUiIconButton(id, icon, onClick);
        public static InsightUiIconElement Icon(string id, InsightUiIcon icon) => new InsightUiIconElement(id, icon);
        public static InsightUiCustom Custom(string id, Action<InsightUiCustomDrawContext> draw,
            Func<InsightUiConstraints, InsightUiFrame, InsightUiSize> measure = null) =>
            new InsightUiCustom(id, draw, measure);
        public static InsightUiBreadcrumbs Breadcrumbs(string id, params string[] labels) =>
            new InsightUiBreadcrumbs(id).Add(labels);
        public static InsightUiDivider Divider(string id = "divider") => new InsightUiDivider(id);
        public static InsightUiSpacer Spacer(string id, float width = 0f, float height = 0f) => new InsightUiSpacer(id, width, height);
        public static InsightUiGrid Grid(string id, float minimumColumnWidth = 180f) => new InsightUiGrid(id, minimumColumnWidth);
        public static InsightUiSplit Split(string id, InsightUiElement first, InsightUiElement second, float ratio = 0.5f) =>
            new InsightUiSplit(id, first, second, ratio);
        public static InsightUiScroll Scroll(string id, InsightUiElement child) => new InsightUiScroll(id, child);
        public static InsightUiTabs Tabs(string id) => new InsightUiTabs(id);
        public static InsightUiNavigation Navigation(string id, float breakpoint = 720f) =>
            new InsightUiNavigation(id, breakpoint);
        public static InsightUiVirtualList VirtualList(string id, int itemCount, float itemHeight,
            Func<int, InsightUiElement> itemFactory) => new InsightUiVirtualList(id, itemCount, itemHeight, itemFactory);
        public static InsightUiElement Empty(string id, string message = null) => new InsightUiLabel(id, message ?? string.Empty);
        public static InsightUiFade Fade(string id, bool visible, InsightUiElement content) =>
            new InsightUiFade(id, visible, content);
        public static InsightUiFade Reveal(string id, bool visible, InsightUiElement content) =>
            new InsightUiFade(id, visible, content);
        /// <summary>Creates a restrained paint-only slide and fade from one of four cardinal directions.</summary>
        public static InsightUiSlideFade SlideFade(string id, bool visible, InsightUiElement content,
            InsightUiSlideDirection direction = InsightUiSlideDirection.Down) =>
            new InsightUiSlideFade(id, visible, content, direction);
        public static InsightUiHighlight Highlight(string id, InsightUiElement content, InsightColor? color = null) =>
            new InsightUiHighlight(id, content, color);
        public static InsightUiPopover Popover(string id, InsightUiElement trigger, InsightUiElement content) =>
            new InsightUiPopover(id, trigger, content);
        /// <summary>Creates display-only rich hover context with document-scoped delay and cleanup.</summary>
        public static InsightUiHoverCard HoverCard(string id, InsightUiElement trigger, InsightUiElement content) =>
            new InsightUiHoverCard(id, trigger, content);
        public static InsightUiDropdown Dropdown(string id, string label, string[] options, int selected = 0,
            Action<int, string> changed = null) => new InsightUiDropdown(id, label, options, selected, changed);
        public static InsightUiSearchField SearchField(string id, string value = "", string placeholder = "Search",
            Action<string> changed = null) => new InsightUiSearchField(id, value, placeholder, changed);
        public static InsightUiSegmented Segmented(string id, string[] options, int selected = 0,
            Action<int, string> changed = null) => new InsightUiSegmented(id, options, selected, changed);
        public static InsightUiImage Image(string id, object texture, float width = 32f, float height = 32f,
            string fallback = "") => new InsightUiImage(id, texture, width, height, fallback);
        public static InsightUiToast Toast(string id) => new InsightUiToast(id);
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

        // Keep the fluent surface strongly typed for the stack factories. This avoids
        // consumer casts or a permissive extension that silently does nothing on leaves.
        public new InsightUiStack SetPadding(float value) { base.SetPadding(value); return this; }
        public new InsightUiStack SetPadding(float horizontal, float vertical)
        {
            base.SetPadding(horizontal, vertical);
            return this;
        }
        public new InsightUiStack SetPadding(float left, float top, float right, float bottom)
        {
            base.SetPadding(left, top, right, bottom);
            return this;
        }
        public new InsightUiStack SetGap(float gap) { base.SetGap(gap); return this; }
        public new InsightUiStack SetFlex(float flex) { base.SetFlex(flex); return this; }
        public new InsightUiStack SetWidth(InsightLength width) { base.SetWidth(width); return this; }
        public new InsightUiStack SetHeight(InsightLength height) { base.SetHeight(height); return this; }
        public new InsightUiStack SetMinSize(float width, float height)
        {
            base.SetMinSize(width, height);
            return this;
        }
        public new InsightUiStack SetMaxSize(float width, float height)
        {
            base.SetMaxSize(width, height);
            return this;
        }
        public new InsightUiStack SetBackground(InsightColor color, bool elevated = false)
        {
            base.SetBackground(color, elevated);
            return this;
        }
        public new InsightUiStack SetBorder(InsightColor color, float width = 1f)
        {
            base.SetBorder(color, width);
            return this;
        }
        public new InsightUiStack SetCornerRadius(float radius) { base.SetCornerRadius(radius); return this; }
        public new InsightUiStack SetClip(bool clip = true) { base.SetClip(clip); return this; }
        public new InsightUiStack SetTooltip(string tooltip) { base.SetTooltip(tooltip); return this; }
        public new InsightUiStack SetAlignment(InsightAlignment horizontal, InsightAlignment vertical)
        {
            base.SetAlignment(horizontal, vertical);
            return this;
        }

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

    /// <summary>Persistent, in-layout explanatory notice composed from ordinary Insight Canvas elements.</summary>
    public sealed class InsightUiCallout : InsightUiElement
    {
        private static readonly IReadOnlyList<InsightUiElement> EmptyChildren = new InsightUiElement[0];
        private readonly string rootId;
        private InsightUiElement root;
        private IReadOnlyList<InsightUiElement> childList = EmptyChildren;
        private InsightUiSurface surface;
        private InsightUiSurface accent;
        private InsightUiLabel titleLabel;
        private InsightUiLabel bodyLabel;

        public InsightUiCallout(string id, InsightUiCalloutSeverity severity, string title, string body = null)
            : base(id)
        {
            rootId = id ?? string.Empty;
            Severity = severity;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Rebuild();
        }

        /// <summary>Gets or sets the semantic tone used by the callout.</summary>
        public InsightUiCalloutSeverity Severity { get; set; }
        /// <summary>Gets or sets the short explanatory heading.</summary>
        public string Title { get; set; }
        /// <summary>Gets or sets the optional explanatory body.</summary>
        public string Body { get; set; }
        /// <summary>Gets the optional leading icon.</summary>
        public InsightUiIcon Icon { get; private set; }
        /// <summary>Gets the optional content element below the message.</summary>
        public InsightUiElement Content { get; private set; }
        /// <summary>Gets the optional action element below the message.</summary>
        public InsightUiElement Actions { get; private set; }
        /// <summary>Gets the single composed root exposed to traversal and diagnostics.</summary>
        public override IReadOnlyList<InsightUiElement> Children => childList;

        /// <summary>Adds an optional icon while retaining the normal icon fallback behavior.</summary>
        public InsightUiCallout SetIcon(InsightUiIcon icon)
        {
            Icon = icon;
            Rebuild();
            return this;
        }

        /// <summary>Adds arbitrary normal Insight Canvas content below the explanatory text.</summary>
        public InsightUiCallout SetContent(InsightUiElement content)
        {
            Content = content;
            Rebuild();
            return this;
        }

        /// <summary>Adds optional actions or another normal element below the callout body.</summary>
        public InsightUiCallout SetActions(InsightUiElement actions)
        {
            Actions = actions;
            Rebuild();
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            PrepareForFrame(frame);
            return root == null ? new InsightUiSize(0f, 0f) : root.Measure(constraints, frame);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            root?.Arrange(rect, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            PrepareForFrame(frame);
            root?.Paint(painter, frame);
        }

        private void PrepareForFrame(InsightUiFrame frame)
        {
            titleLabel.Text = Title ?? string.Empty;
            bodyLabel.Text = Body ?? string.Empty;
            bodyLabel.Visible = !string.IsNullOrWhiteSpace(bodyLabel.Text);
            InsightColor color = SeverityColor(Severity, frame);
            surface.Style.Background = frame.Theme.Surface.Blend(color, 0.18f);
            surface.Style.Border = color.WithAlpha(0.86f);
            surface.Style.BorderWidth = 1f;
            surface.Style.Elevated = true;
            accent.Style.Background = color;
            accent.Style.Border = null;
        }

        private void SetRoot(InsightUiElement element)
        {
            if (ReferenceEquals(root, element)) return;
            root = element;
            childList = root == null ? EmptyChildren : new[] { root };
        }

        private void Rebuild()
        {
            titleLabel = InsightUi.Label(rootId + ".title", Title, InsightUiTextStyle.Heading);
            bodyLabel = InsightUi.Label(rootId + ".body", Body, InsightUiTextStyle.Body);
            bodyLabel.Visible = !string.IsNullOrWhiteSpace(Body);
            InsightUiStack text = InsightUi.Column(rootId + ".text").SetGap(3f).Add(titleLabel, bodyLabel);
            InsightUiStack header = InsightUi.Row(rootId + ".header").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Stretch);
            if (Icon != null) header.Add(InsightUi.Icon(rootId + ".icon", Icon));
            header.Add(text);

            InsightUiStack content = InsightUi.Column(rootId + ".content").SetGap(7f).Add(header);
            if (Content != null) content.Add(Content);
            if (Actions != null) content.Add(Actions);

            accent = InsightUi.Surface(rootId + ".accent");
            accent.SetPadding(0f);
            accent.SetWidth(InsightLength.Fixed(3f));
            accent.SetCornerRadius(0f);
            accent.SetAlignment(InsightAlignment.Start, InsightAlignment.Stretch);

            InsightUiStack composition = InsightUi.Row(rootId + ".composition").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Stretch).Add(accent, content);
            surface = InsightUi.Surface(rootId + ".surface", composition);
            surface.Style.Elevated = true;
            SetRoot(surface);
        }

        private static InsightColor SeverityColor(InsightUiCalloutSeverity severity, InsightUiFrame frame)
        {
            switch (severity)
            {
                case InsightUiCalloutSeverity.Success: return frame.Theme.Positive;
                case InsightUiCalloutSeverity.Warning: return frame.Theme.Warning;
                case InsightUiCalloutSeverity.Error: return frame.Theme.Negative;
                default: return frame.Theme.Selected;
            }
        }
    }

    /// <summary>Reusable section title with optional icon, subtitle, trailing action, and divider.</summary>
    public sealed class InsightUiSectionHeader : InsightUiElement
    {
        private const float NarrowWidth = 420f;
        private static readonly IReadOnlyList<InsightUiElement> EmptyChildren = new InsightUiElement[0];
        private string rootId;
        private InsightUiElement root;
        private IReadOnlyList<InsightUiElement> childList = EmptyChildren;
        private InsightUiStack wideRoot;
        private InsightUiStack narrowRoot;
        private InsightUiLabel titleLabel;
        private InsightUiLabel subtitleLabel;

        public InsightUiSectionHeader(string id, string title, string subtitle = null,
            InsightUiIcon icon = null, InsightUiElement trailing = null, bool divider = false) : base(id)
        {
            rootId = id ?? string.Empty;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Icon = icon;
            Trailing = trailing;
            Divider = divider;
            Rebuild();
        }

        /// <summary>Gets or sets the primary heading.</summary>
        public string Title { get; set; }
        /// <summary>Gets or sets the optional supporting line.</summary>
        public string Subtitle { get; set; }
        /// <summary>Gets the optional leading icon.</summary>
        public InsightUiIcon Icon { get; private set; }
        /// <summary>Gets the optional trailing action or content element.</summary>
        public InsightUiElement Trailing { get; private set; }
        /// <summary>Gets whether a divider follows the header.</summary>
        public bool Divider { get; private set; }
        /// <summary>Gets the active wide or narrow root exposed to traversal and diagnostics.</summary>
        public override IReadOnlyList<InsightUiElement> Children => childList;

        /// <summary>Sets or clears the optional leading icon.</summary>
        public InsightUiSectionHeader SetIcon(InsightUiIcon icon)
        {
            Icon = icon;
            Rebuild();
            return this;
        }

        /// <summary>Sets or clears the optional trailing action or content element.</summary>
        public InsightUiSectionHeader SetTrailing(InsightUiElement trailing)
        {
            Trailing = trailing;
            Rebuild();
            return this;
        }

        /// <summary>Sets whether a theme-colored divider follows the header.</summary>
        public InsightUiSectionHeader SetDivider(bool divider = true)
        {
            Divider = divider;
            Rebuild();
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            PrepareForMeasure(constraints);
            PrepareForFrame();
            return root == null ? new InsightUiSize(0f, 0f) : root.Measure(constraints, frame);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            root?.Arrange(rect, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            PrepareForFrame();
            root?.Paint(painter, frame);
        }

        private void PrepareForMeasure(InsightUiConstraints constraints)
        {
            SetRoot(!float.IsPositiveInfinity(constraints.MaxWidth) && constraints.MaxWidth < NarrowWidth
                ? narrowRoot : wideRoot);
        }

        private void PrepareForFrame()
        {
            titleLabel.Text = Title ?? string.Empty;
            subtitleLabel.Text = Subtitle ?? string.Empty;
            subtitleLabel.Visible = !string.IsNullOrWhiteSpace(subtitleLabel.Text);
        }

        private void Rebuild()
        {
            titleLabel = InsightUi.Label(rootId + ".title", Title, InsightUiTextStyle.Heading);
            subtitleLabel = InsightUi.Label(rootId + ".subtitle", Subtitle, InsightUiTextStyle.Caption);
            subtitleLabel.Visible = !string.IsNullOrWhiteSpace(Subtitle);
            InsightUiStack text = InsightUi.Column(rootId + ".text").SetGap(2f).Add(titleLabel, subtitleLabel);

            InsightUiStack leading = InsightUi.Row(rootId + ".leading").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center);
            if (Icon != null) leading.Add(InsightUi.Icon(rootId + ".icon", Icon));
            leading.Add(text);

            InsightUiStack wideRow = InsightUi.Row(rootId + ".wide-row").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center).Add(leading);
            if (Trailing != null)
                wideRow.Add(InsightUi.Spacer(rootId + ".wide-spacer").SetFlex(1f), Trailing);
            wideRoot = InsightUi.Column(rootId + ".wide-root").SetGap(6f).Add(wideRow);

            InsightUiStack narrow = InsightUi.Column(rootId + ".narrow-root").SetGap(5f).Add(leading);
            if (Trailing != null)
                narrow.Add(InsightUi.Row(rootId + ".narrow-action").SetGap(4f).Add(
                    InsightUi.Spacer(rootId + ".narrow-spacer").SetFlex(1f), Trailing));
            narrowRoot = narrow;
            if (Divider)
            {
                wideRoot.Add(InsightUi.Divider(rootId + ".wide-divider"));
                narrowRoot.Add(InsightUi.Divider(rootId + ".narrow-divider"));
            }
            SetRoot(wideRoot);
        }

        private void SetRoot(InsightUiElement element)
        {
            if (ReferenceEquals(root, element)) return;
            root = element;
            childList = root == null ? EmptyChildren : new[] { root };
        }
    }

    /// <summary>Normalized capacity, power, heat, mood, or completion indicator built on Progress.</summary>
    public sealed class InsightUiMeter : InsightUiElement
    {
        private const float NarrowWidth = 300f;
        private static readonly IReadOnlyList<InsightUiElement> EmptyChildren = new InsightUiElement[0];
        private readonly string rootId;
        private InsightUiElement compositeRoot;
        private IReadOnlyList<InsightUiElement> childList = EmptyChildren;
        private readonly InsightUiStack root;
        private readonly InsightUiLabel wideLabel;
        private readonly InsightUiLabel wideValue;
        private readonly InsightUiLabel narrowLabel;
        private readonly InsightUiLabel narrowValue;
        private readonly InsightUiProgress progress;
        private readonly InsightUiStack wideHeader;
        private readonly InsightUiStack narrowValueRow;
        private readonly InsightUiStack narrowHeader;

        public InsightUiMeter(string id, float current, float maximum) : base(id)
        {
            rootId = id ?? string.Empty;
            Current = current;
            Maximum = maximum;
            wideLabel = InsightUi.Label(rootId + ".label", string.Empty, InsightUiTextStyle.Body);
            wideValue = InsightUi.Label(rootId + ".value", string.Empty, InsightUiTextStyle.Caption);
            narrowLabel = InsightUi.Label(rootId + ".narrow-label", string.Empty, InsightUiTextStyle.Body);
            narrowValue = InsightUi.Label(rootId + ".narrow-value", string.Empty, InsightUiTextStyle.Caption);
            wideHeader = InsightUi.Row(rootId + ".wide-header").SetGap(6f).Add(
                wideLabel, InsightUi.Spacer(rootId + ".wide-spacer").SetFlex(1f), wideValue);
            narrowValueRow = InsightUi.Row(rootId + ".narrow-value-row").SetGap(4f).Add(
                InsightUi.Spacer(rootId + ".narrow-spacer").SetFlex(1f), narrowValue);
            narrowHeader = InsightUi.Column(rootId + ".narrow-header").SetGap(3f).Add(narrowLabel, narrowValueRow);
            progress = InsightUi.Progress(rootId + ".progress", 0f);
            root = InsightUi.Column(rootId + ".root").SetGap(5f);
            SetRoot(root);
        }

        /// <summary>Gets or sets the current value before normalization.</summary>
        public float Current { get; set; }
        /// <summary>Gets or sets the positive capacity used for normalization.</summary>
        public float Maximum { get; set; }
        /// <summary>Gets the optional label shown above the progress track.</summary>
        public string Label { get; private set; }
        /// <summary>Gets the optional trailing value text.</summary>
        public string ValueText { get; private set; }
        /// <summary>Gets the optional custom progress color.</summary>
        public InsightColor? Color { get; private set; }
        /// <summary>Gets the clamped current-to-maximum ratio.</summary>
        public float NormalizedValue => Normalize(Current, Maximum);
        /// <summary>Gets the composed root and its progress primitive.</summary>
        public override IReadOnlyList<InsightUiElement> Children => childList;

        /// <summary>Sets the optional label shown above the progress track.</summary>
        public InsightUiMeter SetLabel(string label)
        {
            Label = label;
            return this;
        }

        /// <summary>Sets the optional trailing value text shown with the meter.</summary>
        public InsightUiMeter SetValueText(string valueText)
        {
            ValueText = valueText;
            return this;
        }

        /// <summary>Sets a custom fill color, or clears it to use the document accent.</summary>
        public InsightUiMeter SetColor(InsightColor? color)
        {
            Color = color;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            PrepareForMeasure(constraints);
            PrepareForFrame();
            return compositeRoot == null ? new InsightUiSize(0f, 0f) : compositeRoot.Measure(constraints, frame);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            compositeRoot?.Arrange(rect, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            PrepareForFrame();
            compositeRoot?.Paint(painter, frame);
        }

        private void PrepareForMeasure(InsightUiConstraints constraints)
        {
            bool compact = !float.IsPositiveInfinity(constraints.MaxWidth) && constraints.MaxWidth < NarrowWidth;
            root.Clear();
            bool hasHeader = !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(ValueText);
            if (hasHeader) root.Add(compact ? (InsightUiElement)narrowHeader : wideHeader);
            root.Add(progress);
            SetRoot(root);
        }

        private void PrepareForFrame()
        {
            string label = Label ?? string.Empty;
            string value = ValueText ?? string.Empty;
            wideLabel.Text = label;
            wideValue.Text = value;
            narrowLabel.Text = label;
            narrowValue.Text = value;
            wideLabel.Visible = !string.IsNullOrWhiteSpace(label);
            narrowLabel.Visible = wideLabel.Visible;
            wideValue.Visible = !string.IsNullOrWhiteSpace(value);
            narrowValue.Visible = wideValue.Visible;
            progress.Value = NormalizedValue;
            progress.Color = Color;
        }

        private void SetRoot(InsightUiElement element)
        {
            if (ReferenceEquals(compositeRoot, element)) return;
            compositeRoot = element;
            childList = compositeRoot == null ? EmptyChildren : new[] { compositeRoot };
        }

        internal static float Normalize(float current, float maximum)
        {
            if (float.IsNaN(current) || float.IsNaN(maximum) || maximum <= 0f ||
                float.IsInfinity(current) || float.IsInfinity(maximum)) return 0f;
            return Math.Max(0f, Math.Min(1f, current / maximum));
        }
    }

    /// <summary>Compact label/value inspector row with optional icon and secondary caption.</summary>
    public sealed class InsightUiStatRow : InsightUiElement
    {
        private const float NarrowWidth = 300f;
        private static readonly IReadOnlyList<InsightUiElement> EmptyChildren = new InsightUiElement[0];
        private readonly string rootId;
        private InsightUiElement root;
        private IReadOnlyList<InsightUiElement> childList = EmptyChildren;
        private InsightUiStack wideRoot;
        private InsightUiStack narrowRoot;
        private InsightUiLabel wideLabel;
        private InsightUiLabel narrowLabel;
        private InsightUiLabel wideValue;
        private InsightUiLabel narrowValue;
        private InsightUiLabel wideSecondary;
        private InsightUiLabel narrowSecondary;

        public InsightUiStatRow(string id, string label, string value) : base(id)
        {
            rootId = id ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Rebuild();
        }

        /// <summary>Gets or sets the primary label.</summary>
        public string Label { get; set; }
        /// <summary>Gets or sets the trailing value.</summary>
        public string Value { get; set; }
        /// <summary>Gets the optional supporting caption.</summary>
        public string Secondary { get; private set; }
        /// <summary>Gets the optional leading icon.</summary>
        public InsightUiIcon Icon { get; private set; }
        /// <summary>Gets the optional custom value color.</summary>
        public InsightColor? ValueColor { get; private set; }
        /// <summary>Gets the active wide or narrow root exposed to traversal and diagnostics.</summary>
        public override IReadOnlyList<InsightUiElement> Children => childList;

        /// <summary>Sets optional supporting text beneath the label.</summary>
        public InsightUiStatRow SetSecondary(string secondary)
        {
            Secondary = secondary;
            return this;
        }

        /// <summary>Sets or clears the optional leading icon.</summary>
        public InsightUiStatRow SetIcon(InsightUiIcon icon)
        {
            Icon = icon;
            Rebuild();
            return this;
        }

        /// <summary>Sets a custom value color, or clears it to use the normal text role.</summary>
        public InsightUiStatRow SetValueColor(InsightColor? color)
        {
            ValueColor = color;
            return this;
        }

        /// <summary>Sets the row tooltip while retaining the fluent row type.</summary>
        public new InsightUiStatRow SetTooltip(string tooltip)
        {
            base.SetTooltip(tooltip);
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            PrepareForMeasure(constraints);
            PrepareForFrame();
            return root == null ? new InsightUiSize(0f, 0f) : root.Measure(constraints, frame);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            root?.Arrange(rect, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            PrepareForFrame();
            root?.Paint(painter, frame);
        }

        private void PrepareForMeasure(InsightUiConstraints constraints)
        {
            SetRoot(!float.IsPositiveInfinity(constraints.MaxWidth) && constraints.MaxWidth < NarrowWidth
                ? narrowRoot : wideRoot);
        }

        private void PrepareForFrame()
        {
            string label = Label ?? string.Empty;
            string value = Value ?? string.Empty;
            string secondary = Secondary ?? string.Empty;
            wideLabel.Text = label;
            narrowLabel.Text = label;
            wideValue.Text = value;
            narrowValue.Text = value;
            wideSecondary.Text = secondary;
            narrowSecondary.Text = secondary;
            wideSecondary.Visible = !string.IsNullOrWhiteSpace(secondary);
            narrowSecondary.Visible = wideSecondary.Visible;
            wideValue.Color = ValueColor;
            narrowValue.Color = ValueColor;
        }

        private void Rebuild()
        {
            wideLabel = InsightUi.Label(rootId + ".label", Label, InsightUiTextStyle.Body);
            wideSecondary = InsightUi.Label(rootId + ".secondary", Secondary, InsightUiTextStyle.Caption);
            InsightUiStack wideText = InsightUi.Column(rootId + ".wide-text").SetGap(2f).Add(wideLabel, wideSecondary);
            wideValue = InsightUi.Label(rootId + ".value", Value, InsightUiTextStyle.Body);
            InsightUiStack wide = InsightUi.Row(rootId + ".wide-row").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center);
            if (Icon != null) wide.Add(InsightUi.Icon(rootId + ".icon", Icon));
            wide.Add(wideText, InsightUi.Spacer(rootId + ".wide-spacer").SetFlex(1f), wideValue);
            wideRoot = InsightUi.Column(rootId + ".wide-root").Add(wide);

            narrowLabel = InsightUi.Label(rootId + ".narrow-label", Label, InsightUiTextStyle.Body);
            narrowSecondary = InsightUi.Label(rootId + ".narrow-secondary", Secondary, InsightUiTextStyle.Caption);
            InsightUiStack narrowText = InsightUi.Column(rootId + ".narrow-text").SetGap(2f).Add(narrowLabel, narrowSecondary);
            narrowValue = InsightUi.Label(rootId + ".narrow-value", Value, InsightUiTextStyle.Body);
            InsightUiStack narrowLeading = InsightUi.Row(rootId + ".narrow-leading").SetGap(8f)
                .SetAlignment(InsightAlignment.Start, InsightAlignment.Center);
            if (Icon != null) narrowLeading.Add(InsightUi.Icon(rootId + ".narrow-icon", Icon));
            narrowLeading.Add(narrowText);
            InsightUiStack narrowValueRow = InsightUi.Row(rootId + ".narrow-value-row").SetGap(4f).Add(
                InsightUi.Spacer(rootId + ".narrow-spacer").SetFlex(1f), narrowValue);
            narrowRoot = InsightUi.Column(rootId + ".narrow-root").SetGap(3f).Add(narrowLeading, narrowValueRow);
            SetRoot(wideRoot);
        }

        private void SetRoot(InsightUiElement element)
        {
            if (ReferenceEquals(root, element)) return;
            root = element;
            childList = root == null ? EmptyChildren : new[] { root };
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

    /// <summary>Reusable-component scope that prefixes descendant state and focus identities.</summary>
    public sealed class InsightUiScope : InsightUiElement
    {
        private readonly InsightUiElement child;

        public InsightUiScope(string id, InsightUiElement child) : base(id)
        {
            this.child = child;
        }

        public InsightUiElement Child => child;
        public override IReadOnlyList<InsightUiElement> Children => child == null ? new InsightUiElement[0] : new[] { child };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (child == null) return new InsightUiSize(0f, 0f);
            frame.PushScope(Id);
            try { return child.Measure(constraints, frame); }
            finally { frame.PopScope(); }
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            if (child == null) return;
            frame.PushScope(Id);
            try { child.Arrange(rect, frame); }
            finally { frame.PopScope(); }
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (child == null) return;
            frame.PushScope(Id);
            try { child.Paint(painter, frame); }
            finally { frame.PopScope(); }
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
        public Func<string> TextProvider { get; private set; }
        public InsightUiTextStyle TextStyle { get; set; }
        public bool Wrap { get; set; }
        public InsightColor? Color { get; set; }

        public InsightUiLabel SetTextProvider(Func<string> provider)
        {
            TextProvider = provider;
            return this;
        }

        public string DisplayText => TextProvider == null ? (Text ?? string.Empty) : (TextProvider() ?? string.Empty);

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return frame.MeasureText(DisplayText, TextStyle, Wrap ? constraints.MaxWidth : float.PositiveInfinity);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            painter.Text(LayoutRect, DisplayText, TextStyle, Color, Wrap, frame);
        }
    }

    /// <summary>Consumer-owned element that participates in the normal layout and paint phases.</summary>
    public sealed class InsightUiCustom : InsightUiElement
    {
        private readonly Action<InsightUiCustomDrawContext> draw;
        private readonly Func<InsightUiConstraints, InsightUiFrame, InsightUiSize> measure;

        public InsightUiCustom(string id, Action<InsightUiCustomDrawContext> draw,
            Func<InsightUiConstraints, InsightUiFrame, InsightUiSize> measure = null) : base(id)
        {
            this.draw = draw;
            this.measure = measure;
        }

        public Action<InsightUiCustomDrawContext> DrawCallback => draw;
        public Func<InsightUiConstraints, InsightUiFrame, InsightUiSize> MeasureCallback => measure;

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return measure == null ? new InsightUiSize(0f, 0f) : measure(constraints, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            draw?.Invoke(new InsightUiCustomDrawContext(LayoutRect, painter, frame));
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
        public Func<bool> SelectedProvider { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            InsightUiSize text = frame.MeasureNativeText(Label, InsightUiTextStyle.Button, float.PositiveInfinity);
            return new InsightUiSize(text.Width + padding.Horizontal, Math.Max(28f, text.Height + padding.Vertical));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            bool selected = SelectedProvider == null ? Selected : SelectedProvider();
            bool clicked = painter.Button(LayoutRect, Label, Enabled, selected, frame);
            bool activated = Enabled && frame.Focus.ConsumeActivation(frame.EffectiveId(Id));
            if (Enabled && (clicked || activated))
            {
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
                OnClick?.Invoke();
            }
        }

        protected override bool Focusable => Enabled;
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
        /// <summary>Gets the optional explicit text color; null uses the active theme primary foreground.</summary>
        public InsightColor? TextColor { get; private set; }
        public InsightColor? Color { get; set; }

        /// <summary>Sets an explicit text color while leaving the semantic accent on the border and tint.</summary>
        public InsightUiBadge SetTextColor(InsightColor? color)
        {
            TextColor = color;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiPadding padding = ScaledPadding(frame);
            InsightUiSize size = frame.MeasureText(Text, InsightUiTextStyle.Caption, float.PositiveInfinity);
            return new InsightUiSize(size.Width + padding.Horizontal, Math.Max(22f, size.Height + padding.Vertical));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            InsightUiStyle style = Style.Clone();
            style.Background = (Color ?? frame.Theme.Selected).WithAlpha(0.25f);
            style.Border = Color ?? frame.Theme.Selected;
            painter.Surface(LayoutRect, style, frame);
            InsightUiPadding padding = ScaledPadding(frame);
            painter.Text(new InsightRect(LayoutRect.X + padding.Left, LayoutRect.Y + padding.Top,
                Math.Max(0f, LayoutRect.Width - padding.Horizontal), Math.Max(0f, LayoutRect.Height - padding.Vertical)), Text,
                InsightUiTextStyle.Caption, TextColor ?? frame.Theme.PrimaryText, false, frame);
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
        public bool Animated { get; set; } = true;
        public float TransitionSpeed { get; set; } = 10f;

        protected override bool StateBearing => Animated;

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            new InsightUiSize(Math.Min(240f, constraints.MaxWidth), 8f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float display = Value;
            if (Animated)
            {
                string key = frame.StateKey(Id + ".display");
                display = frame.State.GetFloat(key, Value);
                display = InsightMotion.Approach(display, Value, frame.DeltaTime, TransitionSpeed, frame.ReducedMotion);
                frame.State.SetFloat(key, display);
            }
            painter.Progress(LayoutRect, display, Color ?? frame.Theme.Selected, frame);
        }
    }

    /// <summary>Renderer-neutral icon display with a text/glyph fallback.</summary>
    public sealed class InsightUiIconElement : InsightUiElement
    {
        public InsightUiIconElement(string id, InsightUiIcon icon) : base(id)
        {
            Icon = icon ?? InsightUiIcon.FromText(string.Empty);
            Style.MinimumWidth = 24f;
            Style.MinimumHeight = 24f;
            if (!string.IsNullOrEmpty(Icon.Tooltip)) SetTooltip(Icon.Tooltip);
        }

        public InsightUiIcon Icon { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            string fallback = Icon == null ? string.Empty : Icon.Fallback;
            // The fallback is painted as a single-line icon label, so do not ask layout to reserve wrapped height.
            InsightUiSize text = frame.MeasureText(fallback, InsightUiTextStyle.Label, float.PositiveInfinity);
            return new InsightUiSize(Math.Max(24f, text.Width), Math.Max(24f, text.Height));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            IInsightUiIconPainter iconPainter = painter as IInsightUiIconPainter;
            if (iconPainter != null)
                iconPainter.Icon(LayoutRect, Icon, frame);
            else
                painter.Text(LayoutRect, Icon == null ? string.Empty : Icon.Fallback,
                    InsightUiTextStyle.Label, frame.Theme.PrimaryText, false, frame);
        }
    }

    /// <summary>Small icon/text action that shares button interaction and theme behavior.</summary>
    public sealed class InsightUiIconButton : InsightUiButton
    {
        public InsightUiIconButton(string id, string icon, Action onClick = null)
            : this(id, InsightUiIcon.FromText(icon), onClick)
        {
        }

        public InsightUiIconButton(string id, InsightUiIcon icon, Action onClick = null)
            : base(id, icon == null ? string.Empty : icon.Fallback, onClick)
        {
            IconModel = icon ?? InsightUiIcon.FromText(string.Empty);
            Style.MinimumWidth = 32f;
            Style.MaximumWidth = 42f;
            SetTooltip(IconModel.Tooltip ?? IconModel.AccessibleDescription ?? IconModel.Fallback);
        }

        /// <summary>Original fallback text, retained for source compatibility.</summary>
        public string Icon => IconModel.Fallback;
        public InsightUiIcon IconModel { get; set; }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            bool selected = SelectedProvider == null ? Selected : SelectedProvider();
            IInsightUiIconPainter iconPainter = painter as IInsightUiIconPainter;
            bool clicked = iconPainter != null
                ? iconPainter.IconButton(LayoutRect, IconModel, Enabled, selected, frame)
                : painter.Button(LayoutRect, IconModel == null ? string.Empty : IconModel.Fallback, Enabled, selected, frame);
            bool activated = Enabled && frame.Focus.ConsumeActivation(frame.EffectiveId(Id));
            if (Enabled && (clicked || activated))
            {
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
                OnClick?.Invoke();
            }
        }
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
            float height = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                InsightUiSize label = frame.MeasureText(labels[i], InsightUiTextStyle.Caption, float.PositiveInfinity);
                width += label.Width;
                height = Math.Max(height, label.Height);
                if (i + 1 < labels.Count)
                {
                    InsightUiSize separator = frame.MeasureText("/", InsightUiTextStyle.Caption, float.PositiveInfinity);
                    width += separator.Width + 10f;
                    height = Math.Max(height, separator.Height);
                }
            }
            return new InsightUiSize(width, Math.Max(1f, height));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float x = LayoutRect.X;
            for (int i = 0; i < labels.Count; i++)
            {
                InsightUiSize size = frame.MeasureText(labels[i], InsightUiTextStyle.Caption, float.PositiveInfinity);
                float available = Math.Max(0f, LayoutRect.Right - x);
                painter.Text(new InsightRect(x, LayoutRect.Y, Math.Min(size.Width, available), LayoutRect.Height), labels[i],
                    InsightUiTextStyle.Caption, i + 1 == labels.Count ? frame.Theme.PrimaryText : frame.Theme.SecondaryText,
                    false, frame);
                x += size.Width;
                if (i + 1 < labels.Count)
                {
                    painter.Text(new InsightRect(Math.Min(x + 5f, LayoutRect.Right), LayoutRect.Y,
                        Math.Min(8f, Math.Max(0f, LayoutRect.Right - x - 5f)), LayoutRect.Height), "/",
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

        private Func<bool> boundGetter;
        private Action<bool> boundSetter;

        /// <summary>Binds the toggle to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiToggle Bind(Func<bool> getter, Action<bool> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            // CheckboxLabeled uses RimWorld's normal control font; measure through the matching native path.
            InsightUiSize text = frame.MeasureNativeText(Label, InsightUiTextStyle.Body, float.PositiveInfinity);
            return new InsightUiSize(Math.Max(180f, text.Width + 30f), Math.Max(28f, text.Height));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = frame.StateKey(Id + ".value");
            bool value = boundGetter == null ? frame.State.GetBool(key, Value) : boundGetter();
            Value = value;
            bool changed = painter.Toggle(LayoutRect, Label, value, Enabled, frame);
            if (Enabled && frame.Focus.ConsumeActivation(frame.EffectiveId(Id))) changed = !value;
            if (Enabled && changed != value)
            {
                if (boundSetter == null) frame.State.SetBool(key, changed);
                else boundSetter(changed);
                Value = changed;
                Changed?.Invoke(changed);
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
            }
        }

        protected override bool Focusable => Enabled;
        protected override bool StateBearing => true;
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

        private Func<float> boundGetter;
        private Action<float> boundSetter;

        /// <summary>Binds the slider to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiSlider Bind(Func<float> getter, Action<float> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(220f, 28f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = frame.StateKey(Id + ".value");
            float value = boundGetter == null ? frame.State.GetFloat(key, Value) : boundGetter();
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            Value = value;
            float next = painter.Slider(LayoutRect, value, Minimum, Maximum, Enabled, frame);
            if (Enabled && Math.Abs(next - value) > 0.0001f)
            {
                if (boundSetter == null) frame.State.SetFloat(key, next);
                else boundSetter(next);
                Value = next;
                Changed?.Invoke(next);
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
            }
        }

        protected override bool Focusable => Enabled;
        protected override bool StateBearing => true;
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

        private Func<string> boundGetter;
        private Action<string> boundSetter;

        /// <summary>Binds the field to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiTextField Bind(Func<string> getter, Action<string> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) => new InsightUiSize(240f, 28f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = frame.StateKey(Id + ".value");
            string value = boundGetter == null ? frame.State.GetString(key, Value) : (boundGetter() ?? string.Empty);
            Value = value;
            string next = painter.TextField(LayoutRect, value, Enabled, frame) ?? string.Empty;
            if (Enabled && next != value)
            {
                if (boundSetter == null) frame.State.SetString(key, next);
                else boundSetter(next);
                Value = next;
                Changed?.Invoke(next);
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
            }
        }

        protected override bool Focusable => Enabled;
        protected override bool TextInput => true;
        protected override bool StateBearing => true;
    }

    /// <summary>Compact cycling selector for small deterministic option sets.</summary>
    public sealed class InsightUiSelect : InsightUiElement
    {
        private readonly InsightUiButton button;
        private readonly IReadOnlyList<InsightUiElement> children;
        private readonly string[] options;

        public InsightUiSelect(string id, string label, string[] options, int selected = 0,
            Action<int, string> changed = null) : base(id)
        {
            Label = label ?? string.Empty;
            this.options = options ?? new string[0];
            Selected = this.options.Length == 0 ? 0 : Math.Max(0, Math.Min(this.options.Length - 1, selected));
            Changed = changed;
            button = new InsightUiButton(id + ".button", string.Empty, Advance);
            button.Style.MinimumHeight = 30f;
            children = new[] { (InsightUiElement)button };
        }

        public string Label { get; set; }
        public int Selected { get; private set; }
        public bool Enabled { get; set; } = true;
        public Action<int, string> Changed { get; set; }
        public IReadOnlyList<string> Options => options;
        public string Current => options.Length == 0 ? string.Empty : options[Selected];
        public override IReadOnlyList<InsightUiElement> Children => children;

        private Func<int> boundGetter;
        private Action<int> boundSetter;

        /// <summary>Binds selection to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiSelect Bind(Func<int> getter, Action<int> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            Selected = SelectedIndex(frame);
            button.Label = string.IsNullOrEmpty(Label) ? Current : Label + ": " + Current;
            button.Enabled = Enabled;
            return button.Measure(constraints, frame);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame) => button.Arrange(rect, frame);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            Selected = SelectedIndex(frame);
            button.Label = string.IsNullOrEmpty(Label) ? Current : Label + ": " + Current;
            button.Enabled = Enabled;
            button.Paint(painter, frame);
            if (boundGetter == null) frame.State.SetInt(frame.StateKey(Id + ".selected"), Selected);
        }

        private void Advance()
        {
            if (options.Length == 0) return;
            Selected = (Selected + 1) % options.Length;
            boundSetter?.Invoke(Selected);
            Changed?.Invoke(Selected, Current);
        }

        private int SelectedIndex(InsightUiFrame frame)
        {
            return options.Length == 0 ? 0 : Math.Max(0, Math.Min(options.Length - 1,
                boundGetter == null ? frame.State.GetInt(frame.StateKey(Id + ".selected"), Selected) : boundGetter()));
        }

        protected override bool StateBearing => true;
    }

    /// <summary>Stateful disclosure section backed by the document state store.</summary>
    public sealed class InsightUiExpander : InsightUiElement
    {
        private readonly InsightUiButton header;
        private readonly InsightUiElement content;
        private readonly IReadOnlyList<InsightUiElement> children;

        public InsightUiExpander(string id, string label, InsightUiElement content, bool expanded = false) : base(id)
        {
            Label = label ?? string.Empty;
            this.content = content ?? InsightUi.Empty(id + ".empty", "Nothing to show.");
            Expanded = expanded;
            header = new InsightUiButton(id + ".header", Label, Toggle);
            header.Style.HorizontalAlignment = InsightAlignment.Stretch;
            children = new[] { (InsightUiElement)header, this.content };
        }

        public string Label { get; set; }
        public bool Expanded { get; private set; }
        public InsightUiElement Content => content;
        public override IReadOnlyList<InsightUiElement> Children => children;

        private Func<bool> boundGetter;
        private Action<bool> boundSetter;

        /// <summary>Binds expansion to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiExpander Bind(Func<bool> getter, Action<bool> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        public InsightUiExpander SetExpanded(bool expanded)
        {
            Expanded = expanded;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            bool expanded = ExpandedValue(frame);
            header.Label = (expanded ? "▾ " : "▸ ") + Label;
            InsightUiSize head = header.Measure(constraints, frame);
            if (!expanded) return head;
            InsightUiSize body = content.Measure(new InsightUiConstraints(0f, constraints.MaxWidth, 0f,
                Math.Max(0f, constraints.MaxHeight - head.Height - EffectiveGap(frame))), frame);
            return new InsightUiSize(Math.Max(head.Width, body.Width), head.Height + EffectiveGap(frame) + body.Height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            bool expanded = ExpandedValue(frame);
            InsightUiSize head = header.MeasuredSize;
            header.Arrange(new InsightRect(rect.X, rect.Y, rect.Width, head.Height), frame);
            if (expanded)
                content.Arrange(new InsightRect(rect.X, rect.Y + head.Height + EffectiveGap(frame), rect.Width,
                    Math.Max(0f, rect.Height - head.Height - EffectiveGap(frame))), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            bool expanded = ExpandedValue(frame);
            header.Label = (expanded ? "▾ " : "▸ ") + Label;
            header.Paint(painter, frame);
            if (boundGetter == null) frame.State.SetBool(frame.StateKey(Id + ".expanded"), Expanded);
            if (expanded) content.Paint(painter, frame);
        }

        private void Toggle()
        {
            Expanded = !Expanded;
            boundSetter?.Invoke(Expanded);
        }

        private bool ExpandedValue(InsightUiFrame frame)
        {
            Expanded = boundGetter == null ? frame.State.GetBool(frame.StateKey(Id + ".expanded"), Expanded) : boundGetter();
            return Expanded;
        }

        protected override bool Focusable => false;
        protected override bool StateBearing => true;
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
        /// <summary>Enables document-owned divider dragging when the renderer supports it.</summary>
        public bool Draggable { get; set; }
        /// <summary>Width of the interactive divider in draggable mode.</summary>
        public float DividerWidth { get; set; } = 6f;
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
            float ratio = CurrentRatio(frame);
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
            float ratio = CurrentRatio(frame);
            float gap = EffectiveGap(frame);
            InsightRect divider;
            if (Orientation == InsightUiOrientation.Horizontal)
            {
                float firstWidth = Math.Max(0f, (LayoutRect.Width - gap) * ratio);
                divider = new InsightRect(LayoutRect.X + firstWidth, LayoutRect.Y, gap, LayoutRect.Height);
            }
            else
            {
                float firstHeight = Math.Max(0f, (LayoutRect.Height - gap) * ratio);
                divider = new InsightRect(LayoutRect.X, LayoutRect.Y + firstHeight, LayoutRect.Width, gap);
            }
            painter.Divider(divider, frame.Theme.SecondaryText.WithAlpha(0.24f), frame);
            if (Draggable)
            {
                IInsightUiDragPainter drag = painter as IInsightUiDragPainter;
                if (drag != null)
                {
                    float next = drag.DragDivider(divider, LayoutRect, Orientation, ratio,
                        frame.StateKey(Id + ".ratio"), frame);
                    if (!float.IsNaN(next))
                    {
                        Ratio = Clamp(next, 0.1f, 0.9f);
                        frame.State.SetFloat(frame.StateKey(Id + ".ratio"), Ratio);
                        Invalidate();
                    }
                }
            }
            second?.Paint(painter, frame);
        }

        private float CurrentRatio(InsightUiFrame frame)
        {
            return Clamp(Draggable ? frame.State.GetFloat(frame.StateKey(Id + ".ratio"), Ratio) : Ratio, 0.1f, 0.9f);
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
        protected override bool StateBearing => true;

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (child == null) return new InsightUiSize(0f, 0f);
            InsightUiSize size = child.Measure(new InsightUiConstraints(constraints.MinWidth, constraints.MaxWidth, 0f,
                float.PositiveInfinity), frame);
            return new InsightUiSize(size.Width, Math.Min(size.Height, constraints.MaxHeight));
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            float offset = frame.State.GetFloat(frame.StateKey(Id + ".scrollY"), 0f);
            ArrangeChild(rect, offset, frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (child == null) return;
            string key = frame.StateKey(Id + ".scrollY");
            float offset = frame.State.GetFloat(key, 0f);
            offset = painter.ScrollOffset(LayoutRect, child.MeasuredSize.Height, offset, key, frame);
            frame.State.SetFloat(key, offset);
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
        protected override bool StateBearing => true;

        private Func<string> boundGetter;
        private Action<string> boundSetter;

        /// <summary>Binds the active tab to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiTabs Bind(Func<string> getter, Action<string> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

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
                    boundSetter?.Invoke(id);
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
                string controlId = Id + ".tab." + tab.Id;
                frame.RegisterInteractive(controlId, this);
                bool clicked = painter.Button(button, tab.Label, true, tab.Id == ActiveTabId, frame);
                if (frame.Focus.ConsumeActivation(frame.EffectiveId(controlId))) clicked = true;
                if (clicked)
                {
                    ActiveTabId = tab.Id;
                    frame.Focus.RequestFocus(frame.EffectiveId(controlId));
                    if (boundSetter == null) frame.State.SetString(frame.StateKey(Id + ".active"), ActiveTabId);
                    else boundSetter(ActiveTabId);
                }
            }
            Active(frame)?.Content.Paint(painter, frame);
        }

        private InsightUiTab Active(InsightUiFrame frame)
        {
            string stored = boundGetter == null
                ? frame.State.GetString(frame.StateKey(Id + ".active"), ActiveTabId)
                : boundGetter();
            if (!string.IsNullOrEmpty(stored)) ActiveTabId = stored;
            for (int i = 0; i < tabs.Count; i++) if (tabs[i].Id == ActiveTabId) return tabs[i];
            return tabs.Count == 0 ? null : tabs[0];
        }
    }

    /// <summary>Responsive page navigation: side rail when wide, wrapped compact bar when narrow.</summary>
    public sealed class InsightUiNavigation : InsightUiElement
    {
        private readonly List<InsightUiTab> pages = new List<InsightUiTab>();
        private readonly List<InsightUiElement> children = new List<InsightUiElement>();
        private string lastActivePageId;

        public InsightUiNavigation(string id, float breakpoint = 720f) : base(id)
        {
            Breakpoint = Math.Max(360f, breakpoint);
            SideWidth = 172f;
            CompactItemWidth = 96f;
            Style.Gap = 10f;
        }

        public float Breakpoint { get; set; }
        public float SideWidth { get; set; }
        public float CompactItemWidth { get; set; }
        public bool IsCompact { get; private set; }
        public string ActivePageId { get; private set; }
        public IReadOnlyList<InsightUiTab> Pages => pages;
        public override IReadOnlyList<InsightUiElement> Children => children;
        protected override bool StateBearing => true;

        private Func<string> boundGetter;
        private Action<string> boundSetter;

        /// <summary>Binds the active page to an external model; the getter remains authoritative on every frame.</summary>
        public InsightUiNavigation Bind(Func<string> getter, Action<string> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        public InsightUiNavigation Add(string id, string label, InsightUiElement content)
        {
            InsightUiTab page = new InsightUiTab(id, label, content);
            pages.Add(page);
            children.Add(page.Content);
            if (ActivePageId == null) ActivePageId = page.Id;
            return this;
        }

        public InsightUiNavigation Select(string id)
        {
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Id == id)
                {
                    ActivePageId = id;
                    boundSetter?.Invoke(id);
                    break;
                }
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float gap = EffectiveGap(frame);
            float width = float.IsPositiveInfinity(constraints.MaxWidth) ? 960f : constraints.MaxWidth;
            IsCompact = width < Breakpoint;
            InsightUiTab active = Active(frame);
            float navHeight = IsCompact ? CompactHeight(width, frame) : 0f;
            float contentWidth = IsCompact ? width : Math.Max(0f, width - SideWidth - gap);
            InsightUiSize content = active == null ? new InsightUiSize(0f, 0f) : active.Content.Measure(
                new InsightUiConstraints(0f, contentWidth, 0f, constraints.MaxHeight), frame);
            if (IsCompact)
                return new InsightUiSize(Math.Max(width, content.Width), navHeight + gap + content.Height);
            return new InsightUiSize(SideWidth + gap + content.Width, Math.Max(navHeight, content.Height));
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            IsCompact = rect.Width < Breakpoint;
            float gap = EffectiveGap(frame);
            InsightUiTab active = Active(frame);
            if (active == null) return;
            if (IsCompact)
            {
                float navHeight = CompactHeight(rect.Width, frame);
                active.Content.Arrange(new InsightRect(rect.X, rect.Y + navHeight + gap, rect.Width,
                    Math.Max(0f, rect.Height - navHeight - gap)), frame);
            }
            else
            {
                active.Content.Arrange(new InsightRect(rect.X + SideWidth + gap, rect.Y,
                    Math.Max(0f, rect.Width - SideWidth - gap), rect.Height), frame);
            }
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            IsCompact = LayoutRect.Width < Breakpoint;
            InsightUiTab active = Active(frame);
            float gap = EffectiveGap(frame);
            if (IsCompact)
            {
                float navHeight = CompactHeight(LayoutRect.Width, frame);
                PaintCompactNavigation(new InsightRect(LayoutRect.X, LayoutRect.Y, LayoutRect.Width, navHeight), painter, frame);
                active?.Content.Paint(painter, frame);
            }
            else
            {
                InsightRect rail = new InsightRect(LayoutRect.X, LayoutRect.Y, SideWidth, LayoutRect.Height);
                painter.Surface(rail, new InsightUiStyle { Background = frame.Theme.Surface, Elevated = true }, frame);
                PaintRail(rail, painter, frame);
                active?.Content.Paint(painter, frame);
            }
        }

        private InsightUiTab Active(InsightUiFrame frame)
        {
            string stored = boundGetter == null
                ? frame.State.GetString(frame.StateKey(Id + ".active"), ActivePageId)
                : boundGetter();
            if (!string.IsNullOrEmpty(stored)) ActivePageId = stored;
            InsightUiTab active = null;
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Id == ActivePageId) { active = pages[i]; break; }
            if (active == null && pages.Count > 0) active = pages[0];
            if (active != null && !string.IsNullOrEmpty(lastActivePageId) && lastActivePageId != active.Id)
            {
                for (int i = 0; i < pages.Count; i++)
                    if (pages[i].Id == lastActivePageId) { pages[i].Content.CloseTransient(frame.State); break; }
            }
            lastActivePageId = active?.Id;
            return active;
        }

        private void PaintRail(InsightRect rail, IInsightUiPainter painter, InsightUiFrame frame)
        {
            float y = rail.Y + 10f;
            for (int i = 0; i < pages.Count; i++)
            {
                InsightUiTab page = pages[i];
                InsightRect button = new InsightRect(rail.X + 8f, y, Math.Max(0f, rail.Width - 16f), 30f);
                string controlId = Id + ".page." + page.Id;
                frame.RegisterInteractive(controlId, this);
                bool clicked = painter.Button(button, page.Label, true, page.Id == ActivePageId, frame);
                if (frame.Focus.ConsumeActivation(frame.EffectiveId(controlId))) clicked = true;
                if (clicked)
                {
                    ActivePageId = page.Id;
                    frame.Focus.RequestFocus(frame.EffectiveId(controlId));
                    if (boundSetter == null) frame.State.SetString(frame.StateKey(Id + ".active"), ActivePageId);
                    else boundSetter(ActivePageId);
                }
                y += 30f + 5f;
            }
        }

        private void PaintCompactNavigation(InsightRect rect, IInsightUiPainter painter, InsightUiFrame frame)
        {
            int columns = CompactColumns(rect.Width);
            float gap = 5f;
            float width = (rect.Width - gap * (columns - 1)) / columns;
            for (int i = 0; i < pages.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                InsightRect button = new InsightRect(rect.X + column * (width + gap), rect.Y + row * 35f,
                    Math.Max(0f, width), 30f);
                InsightUiTab page = pages[i];
                string controlId = Id + ".page." + page.Id;
                frame.RegisterInteractive(controlId, this);
                bool clicked = painter.Button(button, page.Label, true, page.Id == ActivePageId, frame);
                if (frame.Focus.ConsumeActivation(frame.EffectiveId(controlId))) clicked = true;
                if (clicked)
                {
                    ActivePageId = page.Id;
                    frame.Focus.RequestFocus(frame.EffectiveId(controlId));
                    if (boundSetter == null) frame.State.SetString(frame.StateKey(Id + ".active"), ActivePageId);
                    else boundSetter(ActivePageId);
                }
            }
        }

        private float CompactHeight(float width, InsightUiFrame frame)
        {
            return CompactRows(width) * 35f - 5f;
        }

        private int CompactRows(float width) => (pages.Count + CompactColumns(width) - 1) / CompactColumns(width);

        private int CompactColumns(float width)
        {
            return Math.Max(1, Math.Min(pages.Count == 0 ? 1 : pages.Count,
                (int)Math.Floor((Math.Max(0f, width) + 5f) / (CompactItemWidth + 5f))));
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
        /// <summary>Maximum number of item elements retained outside the current visible range.</summary>
        public int CacheLimit { get; set; } = 96;
        /// <summary>Gets the number of item elements currently retained.</summary>
        public int CachedItemCount => itemElements.Count;
        public override IReadOnlyList<InsightUiElement> Children => visibleElements;
        protected override bool StateBearing => true;

        public InsightUiVirtualList Refresh()
        {
            itemElements.Clear();
            visibleElements.Clear();
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float height = float.IsPositiveInfinity(constraints.MaxHeight)
                ? Math.Min(InsightVirtualization.ContentHeight(ItemCount, ItemHeight), 480f)
                : constraints.MaxHeight;
            return new InsightUiSize(float.IsPositiveInfinity(constraints.MaxWidth) ? 280f : constraints.MaxWidth, height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            ArrangeVisible(rect, frame.State.GetFloat(frame.StateKey(Id + ".scrollY"), 0f), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string key = frame.StateKey(Id + ".scrollY");
            float offset = frame.State.GetFloat(key, 0f);
            offset = painter.ScrollOffset(LayoutRect, InsightVirtualization.ContentHeight(ItemCount, ItemHeight),
                offset, key, frame);
            frame.State.SetFloat(key, offset);
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
            EvictCache(range);
            frame.Diagnostics.RecordVirtualization(visibleElements.Count, itemElements.Count);
        }

        private void EvictCache(InsightVirtualizedRange range)
        {
            int limit = Math.Max(0, CacheLimit);
            if (itemElements.Count <= limit) return;
            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, InsightUiElement> pair in itemElements)
                remove.Add(pair.Key);
            remove.Sort((a, b) => CacheRemovalPriority(b, range).CompareTo(CacheRemovalPriority(a, range)));
            for (int i = 0; i < remove.Count && itemElements.Count > limit; i++)
                itemElements.Remove(remove[i]);
        }

        private static int CacheRemovalPriority(int index, InsightVirtualizedRange range)
        {
            if (!range.Contains(index)) return 1000000 + Math.Abs(index - range.Start);
            return Math.Abs(index - range.Start);
        }
    }

    /// <summary>Layout-preserving fade/reveal wrapper driven by a keyed document effect.</summary>
    public sealed class InsightUiFade : InsightUiElement
    {
        private readonly InsightUiElement content;

        public InsightUiFade(string id, bool visible, InsightUiElement content) : base(id)
        {
            VisibleTarget = visible;
            this.content = content ?? InsightUi.Empty(id + ".empty", string.Empty);
        }

        public bool VisibleTarget { get; set; }
        public InsightUiElement Content => content;
        public override IReadOnlyList<InsightUiElement> Children => new[] { content };

        public InsightUiFade SetVisible(bool visible)
        {
            VisibleTarget = visible;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            content.Measure(constraints, frame);

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame) => content.Arrange(rect, frame);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float opacity = frame.Effects.Transition(frame.EffectiveId(Id) + ".opacity",
                VisibleTarget ? 1f : 0f, frame.DeltaTime, 0.16f, frame.ReducedMotion, InsightMotionEasing.EaseOut);
            frame.PushOpacity(opacity);
            try
            {
                if (opacity > 0.001f) content.Paint(painter, frame);
            }
            finally
            {
                frame.PopOpacity();
            }
        }

        protected override bool StateBearing => true;
    }

    /// <summary>Layout-preserving slide and fade driven by one keyed document transition.</summary>
    public sealed class InsightUiSlideFade : InsightUiElement
    {
        private readonly InsightUiElement content;

        public InsightUiSlideFade(string id, bool visible, InsightUiElement content,
            InsightUiSlideDirection direction = InsightUiSlideDirection.Down) : base(id)
        {
            VisibleTarget = visible;
            Direction = direction;
            this.content = content ?? InsightUi.Empty(id + ".empty", string.Empty);
            Duration = 0.16f;
            Travel = 6f;
        }

        /// <summary>Gets or sets the target visibility; the final content bounds remain arranged either way.</summary>
        public bool VisibleTarget { get; set; }
        /// <summary>Gets the cardinal direction from which the content enters.</summary>
        public InsightUiSlideDirection Direction { get; private set; }
        /// <summary>Gets or sets the short transition duration in seconds.</summary>
        public float Duration { get; set; }
        /// <summary>Gets or sets the travel distance in logical pixels; six pixels is the default.</summary>
        public float Travel { get; set; }
        /// <summary>Gets the ordinary composed content.</summary>
        public InsightUiElement Content => content;
        /// <summary>Exposes the ordinary content for traversal and diagnostics.</summary>
        public override IReadOnlyList<InsightUiElement> Children => new[] { content };

        /// <summary>Changes the target visibility without changing layout geometry.</summary>
        public InsightUiSlideFade SetVisible(bool visible)
        {
            VisibleTarget = visible;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            content.Measure(constraints, frame);

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame) => content.Arrange(rect, frame);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            float duration = float.IsNaN(Duration) || float.IsInfinity(Duration) ? 0.16f : Math.Max(0.01f, Duration);
            float travel = float.IsNaN(Travel) || float.IsInfinity(Travel) ? 6f : Math.Max(0f, Travel);
            float progress = frame.Effects.Transition(frame.EffectiveId(Id) + ".slide",
                VisibleTarget ? 1f : 0f, frame.DeltaTime, duration, frame.ReducedMotion, InsightMotionEasing.EaseOut);
            InsightPoint offset = Offset(Direction, travel * (1f - progress));
            IInsightUiTranslationPainter translation = painter as IInsightUiTranslationPainter;
            bool translated = translation != null && (Math.Abs(offset.X) > 0.001f || Math.Abs(offset.Y) > 0.001f);
            frame.PushOpacity(progress);
            try
            {
                if (progress > 0.001f)
                {
                    if (translated) translation.PushTranslation(offset);
                    try { content.Paint(painter, frame); }
                    finally { if (translated) translation.PopTranslation(); }
                }
            }
            finally
            {
                frame.PopOpacity();
            }
        }

        private static InsightPoint Offset(InsightUiSlideDirection direction, float distance)
        {
            switch (direction)
            {
                case InsightUiSlideDirection.Up: return new InsightPoint(0f, -distance);
                case InsightUiSlideDirection.Left: return new InsightPoint(-distance, 0f);
                case InsightUiSlideDirection.Right: return new InsightPoint(distance, 0f);
                default: return new InsightPoint(0f, distance);
            }
        }

        protected override bool StateBearing => true;
    }

    /// <summary>Subtle keyed highlight flash for save, copy, or validation feedback.</summary>
    public sealed class InsightUiHighlight : InsightUiElement
    {
        private readonly InsightUiElement content;

        public InsightUiHighlight(string id, InsightUiElement content, InsightColor? color = null) : base(id)
        {
            this.content = content ?? InsightUi.Empty(id + ".empty", string.Empty);
            Color = color;
        }

        public InsightColor? Color { get; set; }
        public InsightUiElement Content => content;
        public override IReadOnlyList<InsightUiElement> Children => new[] { content };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            content.Measure(constraints, frame);

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame) => content.Arrange(rect, frame);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            content.Paint(painter, frame);
            float intensity = frame.Effects.FlashProgress(frame.EffectiveId(Id), frame.DeltaTime, frame.ReducedMotion);
            IInsightUiCustomPainter custom = painter as IInsightUiCustomPainter;
            if (custom != null && intensity > 0.001f)
                custom.FillRect(LayoutRect, (Color ?? frame.Theme.Focus).WithAlpha(0.16f * intensity), frame);
        }
    }

    /// <summary>Anchored transient panel that reuses ordinary content and document-owned open state.</summary>
    public sealed class InsightUiPopover : InsightUiElement
    {
        private readonly InsightUiElement trigger;
        private readonly InsightUiElement content;
        private bool toggleRequested;
        private bool requestedValue;
        private Func<bool> boundGetter;
        private Action<bool> boundSetter;

        public InsightUiPopover(string id, InsightUiElement trigger, InsightUiElement content) : base(id)
        {
            this.trigger = trigger ?? InsightUi.Empty(id + ".trigger", "Open");
            this.content = content ?? InsightUi.Empty(id + ".content", string.Empty);
            InsightUiButton button = this.trigger as InsightUiButton;
            if (button != null)
            {
                Action previous = button.OnClick;
                button.OnClick = () =>
                {
                    previous?.Invoke();
                    Toggle();
                };
            }
        }

        public InsightUiElement Trigger => trigger;
        public InsightUiElement Content => content;
        public bool IsOpen { get; private set; }
        public override IReadOnlyList<InsightUiElement> Children => new[] { trigger, content };

        /// <summary>Binds the transient open state to consumer-owned state.</summary>
        public InsightUiPopover Bind(Func<bool> getter, Action<bool> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        public InsightUiPopover SetOpen(bool open)
        {
            requestedValue = open;
            toggleRequested = true;
            return this;
        }

        public InsightUiPopover Toggle()
        {
            requestedValue = !IsOpen;
            toggleRequested = true;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            bool open = OpenValue(frame);
            InsightUiSize head = trigger.Measure(constraints, frame);
            if (!open) return head;
            InsightUiSize body = content.Measure(new InsightUiConstraints(0f, constraints.MaxWidth, 0f,
                Math.Max(0f, constraints.MaxHeight - head.Height - EffectiveGap(frame))), frame);
            return new InsightUiSize(Math.Max(head.Width, body.Width), head.Height + EffectiveGap(frame) + body.Height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            bool open = OpenValue(frame);
            InsightUiSize head = trigger.MeasuredSize;
            trigger.Arrange(new InsightRect(rect.X, rect.Y, rect.Width, head.Height), frame);
            if (open)
                content.Arrange(new InsightRect(rect.X, rect.Y + head.Height + EffectiveGap(frame), rect.Width,
                    Math.Max(0f, rect.Height - head.Height - EffectiveGap(frame))), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            bool open = OpenValue(frame);
            trigger.Paint(painter, frame);
            if (open)
            {
                painter.Surface(content.LayoutRect, new InsightUiStyle
                {
                    Background = frame.Theme.ElevatedSurface,
                    Border = frame.Theme.SecondaryText.WithAlpha(0.45f),
                    BorderWidth = 1f,
                    Elevated = true,
                    Padding = InsightUiPadding.All(6f)
                }, frame);
                content.Paint(painter, frame);
            }
            if (boundGetter == null) frame.State.SetBool(frame.StateKey(Id + ".open"), open);
        }

        internal override void CloseTransient(InsightUiStateStore state)
        {
            state.Remove(Id + ".open");
            IsOpen = false;
            toggleRequested = false;
            base.CloseTransient(state);
        }

        private bool OpenValue(InsightUiFrame frame)
        {
            if (toggleRequested)
            {
                IsOpen = requestedValue;
                toggleRequested = false;
                if (boundSetter != null) boundSetter(IsOpen);
                else frame.State.SetBool(frame.StateKey(Id + ".open"), IsOpen);
            }
            IsOpen = boundGetter == null ? frame.State.GetBool(frame.StateKey(Id + ".open"), IsOpen) : boundGetter();
            return IsOpen;
        }

        protected override bool StateBearing => true;
    }

    /// <summary>Display-only rich hover context that uses ordinary content and a small host-bound overlay.</summary>
    public sealed class InsightUiHoverCard : InsightUiElement
    {
        private const float DefaultHoverDelay = 0.18f;
        private const float DefaultCloseDelay = 0.12f;
        private const float CardGap = 6f;
        private const float CardPadding = 8f;
        private const float MaximumCardWidth = 320f;
        private const float MaximumCardHeight = 260f;
        private readonly InsightUiElement trigger;
        private readonly InsightUiElement content;
        private readonly InsightUiPadding padding = InsightUiPadding.All(CardPadding);
        private InsightRect cardRect;
        private bool isOpen;
        private float leaveElapsed;
        private int lastPaintedFrame = -1;
        private string lastOpenKey;
        private string lastElapsedKey;
        private string lastLeaveKey;
        private string lastFrameKey;

        public InsightUiHoverCard(string id, InsightUiElement trigger, InsightUiElement content) : base(id)
        {
            this.trigger = trigger ?? InsightUi.Empty(id + ".trigger", "Details");
            this.content = content ?? InsightUi.Empty(id + ".content", string.Empty);
            HoverDelay = DefaultHoverDelay;
            CloseDelay = DefaultCloseDelay;
        }

        /// <summary>Gets the ordinary trigger element.</summary>
        public InsightUiElement Trigger => trigger;
        /// <summary>Gets the ordinary display-only content element.</summary>
        public InsightUiElement Content => content;
        /// <summary>Gets whether the document currently presents this hover card.</summary>
        public bool IsOpen => isOpen;
        /// <summary>Gets or sets the brief pointer dwell before the card appears.</summary>
        public float HoverDelay { get; set; }
        /// <summary>Gets or sets the short grace period while moving from trigger to card.</summary>
        public float CloseDelay { get; set; }
        /// <summary>Gets the host-clamped arranged bounds of the transient card.</summary>
        public InsightRect CardRect => cardRect;
        /// <summary>Exposes the trigger and display-only content for traversal and diagnostics.</summary>
        public override IReadOnlyList<InsightUiElement> Children => new[] { trigger, content };

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            InsightUiSize triggerSize = trigger.Measure(constraints, frame);
            float maxWidth = MaximumCardWidth;
            float maxHeight = MaximumCardHeight;
            if (frame.HostBounds.Width > 0.01f)
                maxWidth = Math.Min(maxWidth, Math.Max(0f, frame.HostBounds.Width - padding.Horizontal));
            else if (!float.IsPositiveInfinity(constraints.MaxWidth))
                maxWidth = Math.Min(maxWidth, Math.Max(0f, constraints.MaxWidth - padding.Horizontal));
            if (frame.HostBounds.Height > 0.01f)
                maxHeight = Math.Min(maxHeight, Math.Max(0f, frame.HostBounds.Height - padding.Vertical));
            content.Measure(new InsightUiConstraints(0f, maxWidth, 0f, maxHeight), frame);
            return triggerSize;
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame)
        {
            trigger.Arrange(rect, frame);
            InsightUiSize desired = new InsightUiSize(content.MeasuredSize.Width + padding.Horizontal,
                content.MeasuredSize.Height + padding.Vertical);
            InsightRect host = HostBounds(rect, desired, frame);
            cardRect = Place(rect, desired, host);
            content.Arrange(new InsightRect(cardRect.X + padding.Left, cardRect.Y + padding.Top,
                Math.Max(0f, cardRect.Width - padding.Horizontal), Math.Max(0f, cardRect.Height - padding.Vertical)), frame);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            UpdateHoverState(painter as IInsightUiHoverPainter, frame);
            trigger.Paint(painter, frame);
            if (!isOpen || !content.Visible || content.LayoutRect.Width <= 0.01f || content.LayoutRect.Height <= 0.01f)
                return;

            painter.Surface(cardRect, new InsightUiStyle
            {
                Background = frame.Theme.ElevatedSurface,
                Border = frame.Theme.SecondaryText.WithAlpha(0.45f),
                BorderWidth = 1f,
                CornerRadius = frame.Theme.CornerRadius,
                Elevated = true,
                Padding = padding,
                Clip = true
            }, frame);
            painter.BeginClip(content.LayoutRect);
            try { content.Paint(painter, frame); }
            finally { painter.EndClip(); }
        }

        internal override void CloseTransient(InsightUiStateStore state)
        {
            RemoveState(state, lastOpenKey, Id + ".hover.open");
            RemoveState(state, lastElapsedKey, Id + ".hover.elapsed");
            RemoveState(state, lastLeaveKey, Id + ".hover.leave");
            RemoveState(state, lastFrameKey, Id + ".hover.frame");
            isOpen = false;
            leaveElapsed = 0f;
            lastPaintedFrame = -1;
            lastOpenKey = null;
            lastElapsedKey = null;
            lastLeaveKey = null;
            lastFrameKey = null;
            base.CloseTransient(state);
        }

        private void UpdateHoverState(IInsightUiHoverPainter hover, InsightUiFrame frame)
        {
            int currentFrame = frame.Diagnostics.Frame;
            lastOpenKey = frame.StateKey(Id + ".hover.open");
            lastElapsedKey = frame.StateKey(Id + ".hover.elapsed");
            lastLeaveKey = frame.StateKey(Id + ".hover.leave");
            lastFrameKey = frame.StateKey(Id + ".hover.frame");
            int storedFrame = frame.State.GetInt(lastFrameKey, -1);
            if ((lastPaintedFrame >= 0 && currentFrame > lastPaintedFrame + 1) ||
                (storedFrame >= 0 && storedFrame != currentFrame - 1))
                ResetState(frame);
            lastPaintedFrame = currentFrame;

            if (hover == null || !trigger.Visible || !content.Visible)
            {
                ResetState(frame);
                return;
            }

            bool triggerHovered = hover.IsPointerOver(trigger.LayoutRect, frame);
            bool cardHovered = isOpen && hover.IsPointerOver(cardRect, frame);
            bool inside = triggerHovered || cardHovered;
            float delta = Math.Max(0f, frame.DeltaTime);
            if (!isOpen)
            {
                leaveElapsed = 0f;
                float elapsed = frame.State.GetFloat(lastElapsedKey, 0f);
                elapsed = triggerHovered ? elapsed + delta : 0f;
                if (triggerHovered && elapsed >= SafeDelay(HoverDelay))
                {
                    isOpen = true;
                    elapsed = 0f;
                }
                frame.State.SetFloat(lastElapsedKey, elapsed);
            }
            else
            {
                leaveElapsed = inside ? 0f : leaveElapsed + delta;
                if (!inside && leaveElapsed >= SafeDelay(CloseDelay))
                {
                    isOpen = false;
                    leaveElapsed = 0f;
                    frame.State.SetFloat(lastElapsedKey, 0f);
                }
            }
            frame.State.SetBool(lastOpenKey, isOpen);
            frame.State.SetFloat(lastLeaveKey, leaveElapsed);
            frame.State.SetInt(lastFrameKey, currentFrame);
        }

        private void ResetState(InsightUiFrame frame)
        {
            ResetState(frame.State);
        }

        private void ResetState(InsightUiStateStore state)
        {
            isOpen = false;
            leaveElapsed = 0f;
            RemoveState(state, lastOpenKey, Id + ".hover.open");
            RemoveState(state, lastElapsedKey, Id + ".hover.elapsed");
            RemoveState(state, lastLeaveKey, Id + ".hover.leave");
            RemoveState(state, lastFrameKey, Id + ".hover.frame");
        }

        private static void RemoveState(InsightUiStateStore state, string key, string fallback)
        {
            state.Remove(key);
            if (!string.Equals(key, fallback, StringComparison.Ordinal)) state.Remove(fallback);
        }

        private static float SafeDelay(float delay) =>
            float.IsNaN(delay) || float.IsInfinity(delay) ? DefaultHoverDelay : Math.Max(0f, delay);

        private static InsightRect HostBounds(InsightRect triggerRect, InsightUiSize desired, InsightUiFrame frame)
        {
            if (frame.HostBounds.Width > 0.01f && frame.HostBounds.Height > 0.01f) return frame.HostBounds;
            return new InsightRect(triggerRect.X, triggerRect.Y, Math.Max(triggerRect.Width, desired.Width),
                Math.Max(triggerRect.Height, desired.Height));
        }

        private static InsightRect Place(InsightRect triggerRect, InsightUiSize desired, InsightRect host)
        {
            float width = Math.Min(desired.Width, Math.Max(0f, host.Width));
            float height = Math.Min(desired.Height, Math.Max(0f, host.Height));
            float x = triggerRect.X;
            float y = triggerRect.Bottom + CardGap;
            if (x + width > host.Right) x = triggerRect.Right - width;
            if (y + height > host.Bottom) y = triggerRect.Y - height - CardGap;
            x = Math.Max(host.X, Math.Min(x, host.Right - width));
            y = Math.Max(host.Y, Math.Min(y, host.Bottom - height));
            return new InsightRect(x, y, width, height);
        }

        protected override bool StateBearing => true;
    }

    /// <summary>Conventional dropdown menu with a compact header and transient option list.</summary>
    public sealed class InsightUiDropdown : InsightUiElement
    {
        private readonly string[] options;
        private Func<int> boundGetter;
        private Action<int> boundSetter;

        public InsightUiDropdown(string id, string label, string[] options, int selected = 0,
            Action<int, string> changed = null) : base(id)
        {
            Label = label ?? string.Empty;
            this.options = options ?? new string[0];
            Selected = this.options.Length == 0 ? 0 : Math.Max(0, Math.Min(this.options.Length - 1, selected));
            Changed = changed;
            Enabled = true;
            Style.MinimumHeight = 30f;
        }

        public string Label { get; set; }
        public bool Enabled { get; set; }
        public int Selected { get; private set; }
        public string Current => options.Length == 0 ? string.Empty : options[Selected];
        public IReadOnlyList<string> Options => options;
        public Action<int, string> Changed { get; set; }

        /// <summary>Binds the selected option to consumer-owned state.</summary>
        public InsightUiDropdown Bind(Func<int> getter, Action<int> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            Selected = SelectedIndex(frame);
            InsightUiSize header = frame.MeasureNativeText(Label + ": " + Current, InsightUiTextStyle.Button, float.PositiveInfinity);
            float height = Math.Max(30f, header.Height + 12f);
            if (OpenValue(frame)) height += options.Length * (frame.Spacing(26f) + frame.Spacing(2f)) + frame.Spacing(12f);
            return new InsightUiSize(Math.Max(180f, header.Width + frame.Spacing(28f)), height);
        }

        protected override void ArrangeCore(InsightRect rect, InsightUiFrame frame) { }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            Selected = SelectedIndex(frame);
            bool open = OpenValue(frame);
            float headerHeight = Math.Min(32f, LayoutRect.Height);
            InsightRect headerRect = new InsightRect(LayoutRect.X, LayoutRect.Y, LayoutRect.Width, headerHeight);
            bool clicked = painter.Button(headerRect, Label + ": " + Current + (open ? "  ▴" : "  ▾"), Enabled, false, frame);
            bool activated = Enabled && frame.Focus.ConsumeActivation(frame.EffectiveId(Id));
            if (Enabled && (clicked || activated))
            {
                open = !open;
                frame.State.SetBool(frame.StateKey(Id + ".open"), open);
                frame.Focus.RequestFocus(frame.EffectiveId(Id));
            }
            if (!open || options.Length == 0) return;
            float y = headerRect.Bottom + frame.Spacing(2f);
            float rowHeight = frame.Spacing(26f);
            painter.Surface(new InsightRect(LayoutRect.X, y, LayoutRect.Width,
                options.Length * (rowHeight + frame.Spacing(2f)) + frame.Spacing(8f)), new InsightUiStyle
            {
                Background = frame.Theme.ElevatedSurface,
                Border = frame.Theme.SecondaryText.WithAlpha(0.45f),
                Elevated = true,
                Padding = InsightUiPadding.All(4f)
            }, frame);
            for (int i = 0; i < options.Length; i++)
            {
                string optionId = frame.EffectiveId(Id + ".option." + i);
                frame.RegisterInteractive(Id + ".option." + i, this);
                InsightRect optionRect = new InsightRect(LayoutRect.X + 4f, y + 4f + i * (rowHeight + 2f),
                    Math.Max(0f, LayoutRect.Width - 8f), rowHeight);
                bool optionClicked = painter.Button(optionRect, options[i], Enabled, i == Selected, frame);
                bool optionActivated = Enabled && frame.Focus.ConsumeActivation(optionId);
                if (Enabled && (optionClicked || optionActivated))
                {
                    SetSelected(i);
                    if (boundSetter == null) frame.State.SetInt(frame.StateKey(Id + ".selected"), i);
                    frame.State.SetBool(frame.StateKey(Id + ".open"), false);
                    frame.Focus.RequestFocus(optionId);
                }
            }
        }

        internal override void CloseTransient(InsightUiStateStore state)
        {
            state.Remove(Id + ".open");
            base.CloseTransient(state);
        }

        private bool OpenValue(InsightUiFrame frame) => frame.State.GetBool(frame.StateKey(Id + ".open"), false);
        private int SelectedIndex(InsightUiFrame frame) => options.Length == 0 ? 0 : Math.Max(0, Math.Min(options.Length - 1,
            boundGetter == null ? frame.State.GetInt(frame.StateKey(Id + ".selected"), Selected) : boundGetter()));

        private void SetSelected(int index)
        {
            Selected = index;
            boundSetter?.Invoke(index);
            Changed?.Invoke(index, Current);
        }

        protected override bool Focusable => Enabled;
        protected override bool StateBearing => true;
    }

    /// <summary>Text field variant with placeholder and an optional clear affordance.</summary>
    public sealed class InsightUiSearchField : InsightUiElement
    {
        private string value;
        private Func<string> boundGetter;
        private Action<string> boundSetter;

        public InsightUiSearchField(string id, string value = "", string placeholder = "Search",
            Action<string> changed = null) : base(id)
        {
            this.value = value ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
            Changed = changed;
            Enabled = true;
            ShowClear = true;
            Style.MinimumHeight = 28f;
        }

        public string Placeholder { get; set; }
        public bool ShowClear { get; set; }
        public bool Enabled { get; set; }
        public Action<string> Changed { get; set; }
        public string Value => boundGetter == null ? value : (boundGetter() ?? string.Empty);

        /// <summary>Binds the search text to consumer-owned state.</summary>
        public InsightUiSearchField Bind(Func<string> getter, Action<string> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        public InsightUiSearchField Clear()
        {
            SetValue(string.Empty);
            return this;
        }

        /// <summary>Sets the current search text and invokes the normal change callback.</summary>
        public InsightUiSearchField SetText(string text)
        {
            SetValue(text);
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            new InsightUiSize(Math.Max(180f, Math.Min(320f, constraints.MaxWidth)), 28f);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            string current = Value;
            float clearWidth = ShowClear && current.Length > 0 ? 28f : 0f;
            InsightRect fieldRect = new InsightRect(LayoutRect.X, LayoutRect.Y,
                Math.Max(1f, LayoutRect.Width - clearWidth), LayoutRect.Height);
            string next = painter.TextField(fieldRect, current, Enabled, frame) ?? string.Empty;
            if (Enabled && next != current) SetValue(next);
            if (current.Length == 0 && next.Length == 0 && !string.IsNullOrEmpty(Placeholder))
                painter.Text(new InsightRect(fieldRect.X + 7f, fieldRect.Y + 5f, fieldRect.Width - 14f,
                    Math.Max(0f, fieldRect.Height - 10f)), Placeholder, InsightUiTextStyle.Caption,
                    frame.Theme.SecondaryText, false, frame);
            if (clearWidth > 0f)
            {
                InsightRect clearRect = new InsightRect(LayoutRect.Right - clearWidth, LayoutRect.Y, clearWidth, LayoutRect.Height);
                bool clicked = painter.Button(clearRect, "×", Enabled, false, frame);
                if (Enabled && clicked) SetValue(string.Empty);
            }
        }

        private void SetValue(string next)
        {
            next = next ?? string.Empty;
            if (boundSetter != null) boundSetter(next);
            else value = next;
            Changed?.Invoke(next);
        }

        protected override bool Focusable => Enabled;
        protected override bool TextInput => true;
        protected override bool StateBearing => true;
    }

    /// <summary>Compact mutually-exclusive radio/segmented selection.</summary>
    public sealed class InsightUiSegmented : InsightUiElement
    {
        private readonly string[] options;
        private Func<int> boundGetter;
        private Action<int> boundSetter;

        public InsightUiSegmented(string id, string[] options, int selected = 0,
            Action<int, string> changed = null) : base(id)
        {
            this.options = options ?? new string[0];
            Selected = this.options.Length == 0 ? 0 : Math.Max(0, Math.Min(this.options.Length - 1, selected));
            Changed = changed;
            Enabled = true;
            Style.MinimumHeight = 28f;
        }

        public int Selected { get; private set; }
        public bool Enabled { get; set; }
        public Action<int, string> Changed { get; set; }
        public IReadOnlyList<string> Options => options;

        /// <summary>Binds the selected segment to consumer-owned state.</summary>
        public InsightUiSegmented Bind(Func<int> getter, Action<int> setter)
        {
            boundGetter = getter;
            boundSetter = setter;
            return this;
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            float width = 0f;
            for (int i = 0; i < options.Length; i++) width += frame.MeasureNativeText(options[i], InsightUiTextStyle.Button, float.PositiveInfinity).Width + 20f;
            return new InsightUiSize(Math.Max(1f, Math.Min(constraints.MaxWidth, width)), 28f);
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            Selected = options.Length == 0 ? 0 : Math.Max(0, Math.Min(options.Length - 1,
                boundGetter == null ? frame.State.GetInt(frame.StateKey(Id + ".selected"), Selected) : boundGetter()));
            if (options.Length == 0) return;
            float width = LayoutRect.Width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                InsightRect rect = new InsightRect(LayoutRect.X + i * width, LayoutRect.Y, width, LayoutRect.Height);
                bool clicked = painter.Button(rect, options[i], Enabled, Selected == i, frame);
                string effectiveId = frame.EffectiveId(Id + ".option." + i);
                frame.RegisterInteractive(Id + ".option." + i, this);
                bool activated = Enabled && frame.Focus.ConsumeActivation(effectiveId);
                if (Enabled && (clicked || activated))
                {
                    Selected = i;
                    if (boundSetter != null) boundSetter(i);
                    else frame.State.SetInt(frame.StateKey(Id + ".selected"), i);
                    Changed?.Invoke(i, options[i]);
                    frame.Focus.RequestFocus(effectiveId);
                }
            }
        }

        protected override bool Focusable => Enabled;
        protected override bool StateBearing => true;
    }

    /// <summary>Renderer-neutral image/texture element with a text fallback.</summary>
    public sealed class InsightUiImage : InsightUiElement
    {
        public InsightUiImage(string id, object texture, float width, float height, string fallback) : base(id)
        {
            Texture = texture;
            Width = Math.Max(1f, width);
            Height = Math.Max(1f, height);
            Fallback = fallback ?? string.Empty;
            Style.MinimumWidth = Width;
            Style.MinimumHeight = Height;
        }

        public object Texture { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Fallback { get; set; }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame) =>
            new InsightUiSize(Width, Height);

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            InsightUiIcon icon = InsightUiIcon.FromTexture(Texture, Fallback);
            IInsightUiIconPainter iconPainter = painter as IInsightUiIconPainter;
            if (iconPainter != null) iconPainter.Icon(LayoutRect, icon, frame);
            else
            {
                IInsightUiCustomPainter custom = painter as IInsightUiCustomPainter;
                if (custom != null && Texture != null) custom.Texture(LayoutRect, Texture, null, frame);
                else painter.Text(LayoutRect, Fallback, InsightUiTextStyle.Label, frame.Theme.SecondaryText, false, frame);
            }
        }
    }

    /// <summary>Document-owned toast slot for transient success, warning, or error feedback.</summary>
    public sealed class InsightUiToast : InsightUiElement
    {
        public InsightUiToast(string id) : base(id)
        {
            Style.MinimumHeight = 36f;
            Style.Padding = InsightUiPadding.Symmetric(10f, 7f);
        }

        protected override InsightUiSize MeasureCore(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            if (!frame.Toasts.IsVisible) return new InsightUiSize(0f, 0f);
            InsightUiPadding padding = ScaledPadding(frame);
            float availableWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                ? 360f : Math.Max(1f, Math.Min(360f, constraints.MaxWidth));
            float textWidth = Math.Max(1f, availableWidth - padding.Horizontal);
            InsightUiSize text = frame.MeasureText(frame.Toasts.Message, InsightUiTextStyle.Body, textWidth);
            return new InsightUiSize(Math.Min(availableWidth, Math.Max(180f, text.Width + padding.Horizontal)),
                Math.Max(36f, text.Height + padding.Vertical));
        }

        protected override void PaintCore(IInsightUiPainter painter, InsightUiFrame frame)
        {
            if (!frame.Toasts.IsVisible) return;
            InsightColor color = frame.Toasts.Severity == InsightToastSeverity.Success ? frame.Theme.Positive :
                frame.Toasts.Severity == InsightToastSeverity.Warning ? frame.Theme.Warning :
                frame.Toasts.Severity == InsightToastSeverity.Error ? frame.Theme.Negative : frame.Theme.Selected;
            painter.Surface(LayoutRect, new InsightUiStyle
            {
                Background = frame.Theme.ElevatedSurface,
                Border = color,
                BorderWidth = 1f,
                Elevated = true,
                Padding = Style.Padding
            }, frame);
            InsightUiPadding padding = ScaledPadding(frame);
            painter.Text(new InsightRect(LayoutRect.X + padding.Left, LayoutRect.Y + padding.Top,
                Math.Max(0f, LayoutRect.Width - padding.Horizontal), Math.Max(0f, LayoutRect.Height - padding.Vertical)),
                frame.Toasts.Message, InsightUiTextStyle.Body, frame.Theme.PrimaryText, true, frame);
        }
    }
}
