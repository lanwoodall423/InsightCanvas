using System;
using System.Collections.Generic;
using InsightCanvas;

internal static class Program
{
    private static int Main()
    {
        try
        {
            ModelValidation();
            ValidationCoverage();
            ModelClear();
            LayoutMath();
            TypographyGeometry();
            SurfaceRadiusPolicy();
            ComposableLayout();
            UiStateIsolation();
            ConsumerApiFoundations();
            ResponsiveGridAndVirtualization();
            UiThemeScope();
            ShowcaseNavigationAndResponsiveLayout();
            ShowcaseSettingsScope();
            ShowcaseDataDeterminism();
            SelectionPropagation();
            ExplanationCalculation();
            ThemeParsing();
            GraphDeterminism();
            GraphBudgeting();
            GraphFitAndHeaderGeometry();
            TimelineMath();
            OverlayOwnership();
            Serialization();
            SerializationOrdering();
            MotionSettings();
            Prompt2Foundations();
            Prompt3Foundations();
            Console.WriteLine("Insight Canvas core tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void ModelValidation()
    {
        InsightModel model = InsightModel.Create("test").Entity("a", "A").Entity("b", "B")
            .Relation("a", "b", "knows").Metric("a", "count", 4f);
        Assert(model.Validate().IsValid, "valid model rejected");
        InsightModel invalid = InsightModel.Create("invalid").Entity("a", "A").Entity("a", "duplicate").Relation("a", "missing", "contains");
        Assert(!invalid.Validate().IsValid && invalid.Validate().Errors.Count >= 2, "invalid model was not reported");
        InsightModel nonFinite = InsightModel.Create("nonfinite").Entity("a", "A").Metric("a", "bad", new InsightMetric("bad", float.NaN));
        Assert(!nonFinite.Validate().IsValid, "non-finite metric was not reported");
    }

    private static void ValidationCoverage()
    {
        InsightModel invalid = InsightModel.Create("validation").Entity("a", "A")
            .Relation("missing", "a", "broken")
            .Metric("missing", "orphan", 1f)
            .Metric("a", "duplicate", 1f)
            .Metric("a", "duplicate", 2f)
            .Explanation("missing", Explain.Value("Missing owner", 1f))
            .Event(new InsightEvent(string.Empty, 1, "Empty id"))
            .Event(new InsightEvent("same-event", 2, "First"))
            .Event(new InsightEvent("same-event", 3, "Duplicate"));
        invalid.Action("a", new InsightAction(string.Empty, "Empty action", null));
        invalid.Action("a", new InsightAction("same-action", "First", null));
        invalid.Action("a", new InsightAction("same-action", "Duplicate", null));
        invalid.Action("missing", new InsightAction("same-action", "Orphan", null));
        InsightModelValidation validation = invalid.Validate();
        Assert(!validation.IsValid, "expanded validation accepted invalid ids and references");
        Assert(Contains(validation.Errors, "relations id 'missing->a'") &&
            Contains(validation.Errors, "metrics id 'missing'") &&
            Contains(validation.Errors, "explanations id 'missing'"),
            "validation omitted missing owner/reference diagnostics");
        Assert(Contains(validation.Errors, "events id '<empty>'") &&
            Contains(validation.Errors, "events id 'same-event'") &&
            Contains(validation.Errors, "actions id '<empty>'") &&
            Contains(validation.Errors, "actions id 'same-action'") &&
            Contains(validation.Errors, "metrics id 'a/duplicate'"),
            "validation omitted duplicate or empty id diagnostics");

        InsightModelSerializationReport danglingPosition = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='positions'><entities><entity id='a' label='A'/></entities>" +
            "<manualPositions><position entity='missing' x='1' y='2'/></manualPositions>" +
            "<explanations><explanation owner='a' label='one' final='1'/><explanation owner='a' label='two' final='2'/>" +
            "</explanations></insightModel>");
        InsightModelValidation positionValidation = danglingPosition.Model.Validate();
        Assert(!positionValidation.IsValid && Contains(positionValidation.Errors, "manualPositions id 'missing'") &&
            Contains(positionValidation.Errors, "explanations id 'a': duplicate owner id"),
            "dangling manual position or duplicate explanation owner was not reported");
        InsightModelSerializationReport malformedPosition = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='positions'><entities><entity id='a' label='A'/></entities>" +
            "<manualPositions><position entity='a' x='not-a-number' y='2'/></manualPositions></insightModel>");
        Assert(Contains(malformedPosition.Model.Validate().Errors, "manualPositions id 'a': coordinates must be numeric"),
            "malformed serialized display coordinates were silently accepted");
    }

    private static void LayoutMath()
    {
        IReadOnlyList<InsightLayoutBox> row = InsightLayout.ArrangeRow(new InsightRect(0f, 0f, 100f, 20f),
            new[] { "fixed", "flex" }, new[] { InsightLayoutSpec.Fixed(20f), InsightLayoutSpec.Flexible(10f, 20f) }, 5f);
        Assert(Math.Abs(row[0].Rect.Width - 20f) < 0.01f && Math.Abs(row[1].Rect.Width - 75f) < 0.01f, "row flex allocation changed");
        IReadOnlyList<InsightLayoutBox> grid = InsightLayout.ArrangeGrid(new InsightRect(0f, 0f, 100f, 50f), 4, 2, 4f);
        Assert(grid.Count == 4 && Math.Abs(grid[0].Rect.Width - 48f) < 0.01f, "grid allocation changed");
        InsightRect centered = InsightLayout.Align(new InsightRect(0f, 0f, 100f, 50f), 40f, 20f, InsightAlignment.Center);
        Assert(Math.Abs(centered.X - 30f) < 0.01f && Math.Abs(centered.Y - 15f) < 0.01f, "alignment changed");
        InsightRect header = new InsightRect(0f, 0f, 1280f, 43f);
        InsightRect disclosure = InsightHeaderLayout.DisclosureControls(header);
        InsightRect tools = InsightHeaderLayout.ToolsButton(header);
        InsightRect reset = InsightHeaderLayout.ResetButton(header);
        Assert(disclosure.Width >= 329.99f && disclosure.Right <= tools.X - 8f &&
            tools.Right <= reset.X - 8f && reset.Right <= header.Right - 8f,
            "header controls overlap or exceed the window");
    }

    private static void TypographyGeometry()
    {
        InsightTheme theme = InsightTheme.Default.Clone();
        InsightUiFrame frame = new InsightUiFrame(theme, InsightUiDensity.Normal, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        InsightUiSize body = frame.MeasureText("Readable body copy", InsightUiTextStyle.Body, float.PositiveInfinity);
        InsightUiSize title = frame.MeasureText("Readable title", InsightUiTextStyle.Title, float.PositiveInfinity);
        Assert(frame.TextScale(InsightUiTextStyle.Title) > frame.TextScale(InsightUiTextStyle.Body) &&
            title.Height > body.Height, "semantic typography did not reserve a larger title slot");

        string longCopy = new string('x', 80);
        InsightUiSize unwrapped = frame.MeasureText(longCopy, InsightUiTextStyle.Body, float.PositiveInfinity);
        InsightUiSize wrapped = frame.MeasureText(longCopy, InsightUiTextStyle.Body, 120f);
        Assert(wrapped.Width <= 120.001f && wrapped.Height > unwrapped.Height,
            "wrapped text measurement did not preserve the effective width or reserve additional lines");

        InsightUiLabel heading = InsightUi.Label("typography-heading", "A heading with enough words to wrap", InsightUiTextStyle.Heading);
        InsightUiLabel paragraph = InsightUi.Label("typography-paragraph", longCopy, InsightUiTextStyle.Body);
        InsightUiStack column = InsightUi.Column("typography-column").SetGap(8f).Add(heading, paragraph);
        column.Measure(new InsightUiConstraints(0f, 180f, 0f, float.PositiveInfinity), frame);
        column.Arrange(new InsightRect(0f, 0f, 180f, column.MeasuredSize.Height), frame);
        Assert(heading.LayoutRect.Bottom <= paragraph.LayoutRect.Y + 0.01f,
            "heading and wrapped body text occupied overlapping arranged slots");

        InsightUiFrame compactFrame = new InsightUiFrame(theme, InsightUiDensity.Compact, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        InsightUiStack normalColumn = InsightUi.Column("normal-spacing").SetGap(8f).Add(
            InsightUi.Label("normal-first", "First"), InsightUi.Label("normal-second", "Second"));
        InsightUiStack compactColumn = InsightUi.Column("compact-spacing").SetGap(8f).Add(
            InsightUi.Label("compact-first", "First"), InsightUi.Label("compact-second", "Second"));
        normalColumn.Measure(new InsightUiConstraints(0f, 240f, 0f, float.PositiveInfinity), frame);
        normalColumn.Arrange(new InsightRect(0f, 0f, 240f, normalColumn.MeasuredSize.Height), frame);
        compactColumn.Measure(new InsightUiConstraints(0f, 240f, 0f, float.PositiveInfinity), compactFrame);
        compactColumn.Arrange(new InsightRect(0f, 0f, 240f, compactColumn.MeasuredSize.Height), compactFrame);
        Assert(Math.Abs(normalColumn.Children[0].MeasuredSize.Height - compactColumn.Children[0].MeasuredSize.Height) < 0.01f &&
            compactColumn.Children[1].LayoutRect.Y < normalColumn.Children[1].LayoutRect.Y,
            "density changed text geometry instead of only reducing layout spacing");

        InsightTheme largerCaptionTheme = theme.Clone();
        largerCaptionTheme.CaptionSize = 1.5f;
        InsightUiFrame largerCaptionFrame = new InsightUiFrame(largerCaptionTheme, InsightUiDensity.Normal, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        InsightUiBadge badge = InsightUi.Badge("typography-badge", "Ready");
        badge.Color = theme.Positive;
        InsightUiBadge largerBadge = InsightUi.Badge("typography-large-badge", "Ready");
        badge.Measure(new InsightUiConstraints(0f, 240f, 0f, 100f), frame);
        largerBadge.Measure(new InsightUiConstraints(0f, 240f, 0f, 100f), largerCaptionFrame);
        Assert(largerBadge.MeasuredSize.Height > badge.MeasuredSize.Height,
            "larger caption typography did not increase badge allocation");

        TestPainter painter = new TestPainter();
        frame.Focus.BeginFrame();
        badge.Arrange(new InsightRect(0f, 0f, badge.MeasuredSize.Width, badge.MeasuredSize.Height), frame);
        badge.Paint(painter, frame);
        Assert(painter.LastTextColor.HasValue && painter.LastTextColor.Value.Equals(frame.Theme.PrimaryText),
            "badge default foreground did not resolve to the active theme primary text");
        Assert(painter.LastSurfaceStyle.Border.HasValue && painter.LastSurfaceStyle.Border.Value.Equals(badge.Color.Value) &&
            painter.LastSurfaceStyle.Background.HasValue &&
            painter.LastSurfaceStyle.Background.Value.Equals(badge.Color.Value.WithAlpha(0.25f)),
            "badge semantic accent no longer controls its border and tinted background");
        InsightColor badgeOverride = new InsightColor(0.8f, 0.9f, 1f);
        badge.SetTextColor(badgeOverride);
        badge.Paint(painter, frame);
        Assert(painter.LastTextColor.HasValue && painter.LastTextColor.Value.Equals(badgeOverride),
            "badge explicit text foreground override was not applied");
        badge.SetTextColor(null);
        InsightTheme highContrastTheme = theme.WithAccessibility(true, InsightColorBlindMode.None);
        InsightUiFrame highContrastFrame = new InsightUiFrame(highContrastTheme, InsightUiDensity.Normal, true, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        badge.Measure(new InsightUiConstraints(0f, 240f, 0f, 100f), highContrastFrame);
        badge.Arrange(new InsightRect(0f, 0f, badge.MeasuredSize.Width, badge.MeasuredSize.Height), highContrastFrame);
        painter.TextCalls = 0;
        badge.Paint(painter, highContrastFrame);
        Assert(painter.LastTextColor.HasValue && painter.LastTextColor.Value.Equals(highContrastTheme.PrimaryText),
            "badge default foreground did not follow the high-contrast theme");
        Assert(painter.TextCalls == 1 && !painter.LastTextWrap &&
            painter.LastTextRect.X >= badge.LayoutRect.X - 0.01f &&
            painter.LastTextRect.Y >= badge.LayoutRect.Y - 0.01f &&
            painter.LastTextRect.Right <= badge.LayoutRect.Right + 0.01f &&
            painter.LastTextRect.Bottom <= badge.LayoutRect.Bottom + 0.01f,
            "badge caption was painted outside its measured content slot");
    }

    private static void SurfaceRadiusPolicy()
    {
        InsightTheme theme = InsightTheme.Default.Clone();
        InsightUiStyle inherited = new InsightUiStyle();
        Assert(InsightUiSurfaceMath.ResolveCornerRadius(inherited, theme) == 4f,
            "surface style did not inherit the default theme radius");

        theme.CornerRadius = 6f;
        Assert(InsightUiSurfaceMath.ResolveCornerRadius(inherited, theme) == 6f,
            "surface style did not inherit a scoped theme radius");

        inherited.CornerRadius = 8f;
        Assert(InsightUiSurfaceMath.ResolveCornerRadius(inherited, theme) == 8f,
            "explicit element radius did not take precedence over the theme");

        inherited.CornerRadius = 0f;
        Assert(InsightUiSurfaceMath.ResolveCornerRadius(inherited, theme) == 0f,
            "explicit square radius did not override the theme");

        Assert(InsightUiSurfaceMath.QuantizeCornerRadius(1f) == 0f &&
            InsightUiSurfaceMath.QuantizeCornerRadius(3f) == 2f &&
            InsightUiSurfaceMath.QuantizeCornerRadius(5f) == 4f &&
            InsightUiSurfaceMath.QuantizeCornerRadius(7f) == 6f &&
            InsightUiSurfaceMath.QuantizeCornerRadius(99f) == 8f &&
            InsightUiSurfaceMath.RoundedRadiusBucketCount == 9,
            "surface radius quantization exceeded its fixed cache policy");

        Assert(InsightUiSurfaceMath.ClampCornerRadius(8f, 22f, 22f) == 8f &&
            InsightUiSurfaceMath.ClampCornerRadius(8f, 10f, 22f) == 5f &&
            InsightUiSurfaceMath.ClampCornerRadius(8f, 3f, 32f) == 1.5f &&
            InsightUiSurfaceMath.ClampCornerRadius(8f, 0f, 32f) == 0f,
            "surface radius was not clamped to short or narrow target geometry");
        Assert(InsightUiSurfaceMath.InnerCornerRadius(4f, 1f) == 3f &&
            InsightUiSurfaceMath.InnerCornerRadius(8f, 2f) == 6f &&
            InsightUiSurfaceMath.InnerCornerRadius(2f, 5f) == 0f &&
            InsightUiSurfaceMath.InnerCornerRadius(4f, -1f) == 4f,
            "border inset did not preserve the true inner radius relationship");
        Assert(InsightUiSurfaceMath.RadiusBucket(0f) == 0 &&
            InsightUiSurfaceMath.RadiusBucket(0.25f) == 1 &&
            InsightUiSurfaceMath.RadiusBucket(1f) == 1 &&
            InsightUiSurfaceMath.RadiusBucket(3f) == 3 &&
            InsightUiSurfaceMath.RadiusBucket(7f) == 7 &&
            InsightUiSurfaceMath.RadiusBucket(99f) == 8,
            "internal rounded-mask buckets were not deterministic or bounded");
    }

    private static void ModelClear()
    {
        InsightModel model = InsightModel.Create("clear").Entity("a", "A").Entity("b", "B")
            .Relation("a", "b", "feeds").Metric("a", "value", 1f).Event(new InsightEvent("event", 1, "Changed"));
        int revision = model.Revision;
        model.Clear();
        InsightModelSnapshot snapshot = model.Snapshot();
        Assert(snapshot.Entities.Count == 0 && snapshot.Relations.Count == 0 && snapshot.Events.Count == 0 &&
            model.Revision > revision, "model clear did not replace published data");
    }

    private static void ComposableLayout()
    {
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f);
        InsightUiElement fixedPanel = InsightUi.Surface("fixed", InsightUi.Label("fixed-label", "Fixed"))
            .SetWidth(InsightLength.Fixed(120f));
        InsightUiElement flexiblePanel = InsightUi.Surface("flex", InsightUi.Label("flex-label", "Flexible"))
            .SetFlex(1f);
        InsightUiStack row = InsightUi.Row("row", fixedPanel, flexiblePanel).SetGap(8f);
        InsightUiSize measured = row.Measure(new InsightUiConstraints(0f, 400f, 0f, 120f), frame);
        row.Arrange(new InsightRect(0f, 0f, 400f, 120f), frame);
        Assert(measured.Width > 0f && row.Children[0].LayoutRect.Width >= 119f &&
            row.Children[1].LayoutRect.Width > row.Children[0].LayoutRect.Width &&
            row.Children[1].LayoutRect.Right <= 400.01f, "composable row did not honor fixed/flexible layout");

        InsightUiStack wrapped = InsightUi.Wrap("wrapped",
            InsightUi.Label("one", "one").SetWidth(InsightLength.Fixed(90f)),
            InsightUi.Label("two", "two").SetWidth(InsightLength.Fixed(90f)),
            InsightUi.Label("three", "three").SetWidth(InsightLength.Fixed(90f)));
        wrapped.SetGap(8f);
        wrapped.Measure(new InsightUiConstraints(0f, 190f, 0f, 200f), frame);
        wrapped.Arrange(new InsightRect(0f, 0f, 190f, 200f), frame);
        Assert(wrapped.Children[2].LayoutRect.Y > wrapped.Children[0].LayoutRect.Y, "wrapped layout did not create a second line");
    }

    private static void UiStateIsolation()
    {
        InsightUiDocument first = new InsightUiDocument("first", InsightUi.Empty("root"));
        InsightUiDocument second = new InsightUiDocument("second", InsightUi.Empty("root"));
        first.State.SetBool("panel.open", true);
        first.State.SetFloat("scroll", 42f);
        Assert(first.State.GetBool("panel.open") && Math.Abs(first.State.GetFloat("scroll") - 42f) < 0.001f &&
            !second.State.GetBool("panel.open") && Math.Abs(second.State.GetFloat("scroll")) < 0.001f,
            "composable state leaked between documents");
        int revision = first.Revision;
        first.Invalidate();
        Assert(first.Revision > revision && first.Diagnostics.Invalidations == 1, "document invalidation was not scoped");
    }

    private static void ConsumerApiFoundations()
    {
        bool externalToggle = false;
        InsightUiToggle toggle = InsightUi.Toggle("auto-work", "Auto work")
            .Bind(() => externalToggle, value => externalToggle = value);
        TestPainter painter = new TestPainter { ToggleChanges = 1 };
        Render(toggle, painter);
        Assert(externalToggle && toggle.Value, "controlled toggle did not write through its binding");

        externalToggle = false;
        Render(toggle, painter);
        Assert(!toggle.Value, "controlled toggle did not reflect an external model change");

        InsightUiToggle local = InsightUi.Toggle("local", "Local");
        painter.ToggleChanges = 1;
        Render(local, painter);
        Assert(local.Value, "uncontrolled toggle did not retain document-local state");

        float externalSlider = 0.25f;
        InsightUiSlider slider = InsightUi.Slider("volume", 0f, 0f, 1f)
            .Bind(() => externalSlider, value => externalSlider = value);
        painter.SliderValue = 0.75f;
        Render(slider, painter);
        Assert(Math.Abs(externalSlider - 0.75f) < 0.001f, "controlled slider did not write through its binding");

        string externalText = "old";
        InsightUiTextField field = InsightUi.TextField("name")
            .Bind(() => externalText, value => externalText = value);
        painter.TextValue = "new";
        Render(field, painter);
        Assert(externalText == "new", "controlled text field did not write through its binding");

        int externalSelection = 0;
        InsightUiSelect select = InsightUi.Select("mode", "Mode", new[] { "One", "Two", "Three" })
            .Bind(() => externalSelection, value => externalSelection = value);
        painter.ButtonClicks = 1;
        Render(select, painter);
        Assert(externalSelection == 1 && select.Current == "Two", "controlled select did not write through its binding");

        bool expanded = false;
        InsightUiExpander expander = InsightUi.Expander("details", "Details", InsightUi.Label("details-body", "Body"))
            .Bind(() => expanded, value => expanded = value);
        painter.ButtonClicks = 1;
        Render(expander, painter);
        Assert(expanded && expander.Expanded, "controlled expander did not write through its binding");

        string activeTab = "first";
        InsightUiTabs tabs = InsightUi.Tabs("tabs")
            .Add("first", "First", InsightUi.Label("first-content", "First content"))
            .Add("second", "Second", InsightUi.Label("second-content", "Second content"))
            .Bind(() => activeTab, value => activeTab = value);
        painter.ButtonClicks = 2;
        Render(tabs, painter);
        Assert(activeTab == "second" && tabs.ActiveTabId == "second", "controlled tabs did not write through their binding");

        string activePage = "first";
        InsightUiNavigation navigation = InsightUi.Navigation("navigation", 700f)
            .Add("first", "First", InsightUi.Label("page-first", "First page"))
            .Add("second", "Second", InsightUi.Label("page-second", "Second page"))
            .Bind(() => activePage, value => activePage = value);
        painter.ButtonClicks = 2;
        Render(navigation, painter);
        Assert(activePage == "second" && navigation.ActivePageId == "second",
            "controlled navigation did not write through its binding");

        InsightUiToggle audio = InsightUi.Toggle("volume", "Audio");
        InsightUiToggle gameplay = InsightUi.Toggle("volume", "Gameplay");
        InsightUiElement scoped = InsightUi.Column("scoped-root",
            InsightUi.Scope("audio", audio), InsightUi.Scope("gameplay", gameplay));
        painter.ToggleChanges = 1;
        InsightUiStateStore scopedState = new InsightUiStateStore();
        Render(scoped, painter, null, null, scopedState);
        Assert(audio.Value && !gameplay.Value && scopedState.GetBool("audio/volume.value") &&
            !scopedState.GetBool("gameplay/volume.value"), "scoped reusable controls were not isolated");

        InsightUiDiagnostics duplicateDiagnostics = new InsightUiDiagnostics { TrackDuplicateIds = true };
        InsightUiElement duplicate = InsightUi.Column("duplicate-root",
            InsightUi.Toggle("same", "First"), InsightUi.Toggle("same", "Second"));
        Render(duplicate, painter, duplicateDiagnostics);
        Assert(duplicateDiagnostics.DuplicateIds == 1 && duplicateDiagnostics.DuplicateIdPaths[0] == "same",
            "duplicate effective IDs were not reported");

        bool customPainted = false;
        InsightUiCustom custom = InsightUi.Custom("custom", context =>
        {
            customPainted = true;
            (context.Painter as IInsightUiCustomPainter)?.FillRect(context.Bounds,
                InsightTheme.Default.Selected, context.Frame);
        }, (constraints, frame) => new InsightUiSize(64f, 32f));
        painter.CustomDrawSupported = true;
        Render(custom, painter);
        Assert(customPainted && painter.FillRectCalls == 1, "custom drawing element did not use the painter escape hatch");

        InsightUiIcon icon = InsightUiIcon.FromTexture(new object(), "!")
            .WithTooltip("Warning")
            .WithAccessibleDescription("Warning icon");
        InsightUiIconElement iconElement = InsightUi.Icon("icon", icon);
        Render(iconElement, painter);
        Assert(icon.HasTexture && icon.Tooltip == "Warning" && painter.IconCalls == 1,
            "icon abstraction did not reach the renderer capability");
        InsightUiIconButton iconButton = InsightUi.IconButton("icon-button", icon);
        painter.ButtonClicks = 1;
        Render(iconButton, painter);
        Assert(painter.IconCalls == 2, "texture-backed icon button did not reach the renderer capability");

        InsightUiDocument focusDocument = new InsightUiDocument("focus", InsightUi.Column("focus-root",
            InsightUi.Button("first", "First"), InsightUi.Toggle("second", "Second"),
            InsightUi.TextField("third", "Third")));
        Render(focusDocument.Root, painter, focusDocument.Diagnostics, focusDocument.Focus);
        Assert(focusDocument.Focus.FocusableIds.Count == 3, "stock controls did not register focus order");
        focusDocument.Focus.RequestFocus("first");
        focusDocument.Focus.BeginFrame();
        Assert(focusDocument.Focus.Move(InsightUiFocusDirection.Forward) &&
            focusDocument.Focus.FocusedId == "second", "forward focus traversal changed");
        Assert(focusDocument.Focus.Move(InsightUiFocusDirection.Backward) &&
            focusDocument.Focus.FocusedId == "first", "backward focus traversal changed");
        TestInput tabInput = new TestInput { TabPressed = true };
        focusDocument.Focus.ProcessKeyboard(tabInput);
        Assert(tabInput.TabConsumed && focusDocument.Focus.FocusedId == "second",
            "Tab input did not advance document focus");
        TestInput activationInput = new TestInput { ActivatePressed = true };
        focusDocument.Focus.ProcessKeyboard(activationInput);
        Assert(activationInput.ActivationConsumed && focusDocument.Focus.ConsumeActivation("second"),
            "activation input was not routed to the focused control");
        TestInput textInput = new TestInput { IsTextEditing = true, TabPressed = true, ActivatePressed = true };
        focusDocument.Focus.ProcessKeyboard(textInput);
        Assert(!textInput.TabConsumed && !textInput.ActivationConsumed && focusDocument.Focus.FocusedId == "second",
            "text editing did not retain keyboard ownership");

        InsightUiToggle disabledToggle = InsightUi.Toggle("disabled-toggle", "Disabled");
        disabledToggle.Enabled = false;
        InsightUiButton disabledButton = InsightUi.Button("disabled-button", "Disabled");
        disabledButton.Enabled = false;
        InsightUiDocument disabledDocument = new InsightUiDocument("disabled-focus",
            InsightUi.Column("disabled-root", disabledToggle, disabledButton));
        painter.ToggleChanges = 1;
        painter.ButtonClicks = 1;
        Render(disabledDocument.Root, painter, disabledDocument.Diagnostics, disabledDocument.Focus,
            disabledDocument.State);
        Assert(!disabledToggle.Value && disabledDocument.Focus.FocusableIds.Count == 0,
            "disabled controls changed state or entered focus traversal");
    }

    private static void Render(InsightUiElement root, TestPainter painter,
        InsightUiDiagnostics diagnostics = null, InsightUiFocusState focus = null)
    {
        InsightUiStateStore state = new InsightUiStateStore();
        Render(root, painter, diagnostics, focus, state);
    }

    private static void Render(InsightUiElement root, TestPainter painter, InsightUiDiagnostics diagnostics,
        InsightUiFocusState focus, InsightUiStateStore state)
    {
        diagnostics = diagnostics ?? new InsightUiDiagnostics();
        focus = focus ?? new InsightUiFocusState();
        focus.BeginFrame();
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            state, diagnostics, 1f / 60f, focus);
        root.Measure(new InsightUiConstraints(0f, 480f, 0f, 240f), frame);
        root.Arrange(new InsightRect(0f, 0f, 480f, 240f), frame);
        root.Paint(painter, frame);
        focus.PruneFocus();
    }

    private static void ResponsiveGridAndVirtualization()
    {
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Compact, false, false,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 0.016f);
        InsightUiGrid grid = InsightUi.Grid("grid", 150f)
            .Add(InsightUi.Surface("a", InsightUi.Label("a-label", "A")),
                InsightUi.Surface("b", InsightUi.Label("b-label", "B")),
                InsightUi.Surface("c", InsightUi.Label("c-label", "C")),
                InsightUi.Surface("d", InsightUi.Label("d-label", "D")));
        grid.Measure(new InsightUiConstraints(0f, 420f, 0f, 400f), frame);
        grid.Arrange(new InsightRect(0f, 0f, 420f, 240f), frame);
        Assert(grid.Children[0].LayoutRect.X < grid.Children[1].LayoutRect.X &&
            grid.Children[2].LayoutRect.Y > grid.Children[0].LayoutRect.Y,
            "adaptive grid did not arrange rows and columns");
        InsightVirtualizedRange range = InsightVirtualization.Range(1000, 24f, 240f, 480f, 2);
        Assert(range.Start < 20 && range.Contains(20) && range.End > 25 && range.End < 1000,
            "virtualized range did not include the viewport with overscan");
        Assert(Math.Abs(InsightVirtualization.ContentHeight(10, 24f) - 240f) < 0.001f,
            "virtualized content height changed");
    }

    private static void UiThemeScope()
    {
        InsightUiDocument first = new InsightUiDocument("first-theme", InsightUi.Empty("root"));
        InsightUiDocument second = new InsightUiDocument("second-theme", InsightUi.Empty("root"));
        InsightColor original = second.Theme.Selected;
        first.Theme.Selected = new InsightColor(1f, 0f, 0f);
        first.HighContrast = true;
        InsightTheme accessible = first.Theme.WithAccessibility(true, InsightColorBlindMode.None);
        Assert(first.Theme.Selected.Equals(new InsightColor(1f, 0f, 0f)) && second.Theme.Selected.Equals(original) &&
            !accessible.PrimaryText.Equals(first.Theme.PrimaryText), "theme overrides were not scoped or transformed");
    }

    private static void ShowcaseNavigationAndResponsiveLayout()
    {
        InsightUiNavigation navigation = InsightUi.Navigation("showcase-navigation", 700f);
        for (int i = 0; i < 10; i++)
            navigation.Add("page-" + i, "Page " + i, InsightUi.Surface("page-surface-" + i,
                InsightUi.Label("page-label-" + i, "Page content " + i)));
        InsightUiDocument document = new InsightUiDocument("navigation-test", navigation);
        InsightUiFrame frame = new InsightUiFrame(document.Theme, document.Density, false, false,
            document.State, document.Diagnostics, 1f / 60f);
        InsightUiConstraints wide = new InsightUiConstraints(0f, 960f, 0f, 520f);
        navigation.Measure(wide, frame);
        navigation.Arrange(new InsightRect(0f, 0f, 960f, 520f), frame);
        Assert(!navigation.IsCompact && navigation.MeasuredSize.Width > 0f,
            "showcase navigation did not use its wide side rail");

        navigation.Select("page-7");
        document.State.SetString("showcase-navigation.active", "page-7");
        frame = new InsightUiFrame(document.Theme, document.Density, false, false,
            document.State, document.Diagnostics, 1f / 60f);
        InsightUiConstraints narrow = new InsightUiConstraints(0f, 480f, 0f, 520f);
        navigation.Measure(narrow, frame);
        navigation.Arrange(new InsightRect(0f, 0f, 480f, 520f), frame);
        Assert(navigation.IsCompact && navigation.ActivePageId == "page-7",
            "showcase navigation did not switch to compact mode or preserve selection");
    }

    private static void ShowcaseSettingsScope()
    {
        InsightUiDocument first = new InsightUiDocument("showcase-first", InsightUi.Empty("first-root"));
        InsightUiDocument second = new InsightUiDocument("showcase-second", InsightUi.Empty("second-root"));
        first.Density = InsightUiDensity.Compact;
        first.HighContrast = true;
        first.ReducedMotion = true;
        first.State.SetString("showcase-navigation.active", "diagnostics");
        Assert(second.Density == InsightUiDensity.Normal && !second.HighContrast && !second.ReducedMotion &&
            second.State.GetString("showcase-navigation.active", string.Empty) == string.Empty,
            "showcase settings leaked between documents");
    }

    private static void ShowcaseDataDeterminism()
    {
        IReadOnlyList<InsightShowcaseRecord> first = InsightFeatureShowcaseData.CreateRecords();
        IReadOnlyList<InsightShowcaseRecord> second = InsightFeatureShowcaseData.CreateRecords();
        Assert(first.Count == 64 && second.Count == first.Count, "showcase demo data count changed");
        for (int i = 0; i < first.Count; i++)
            Assert(first[i].Id == second[i].Id && first[i].Name == second[i].Name &&
                first[i].Group == second[i].Group && Math.Abs(first[i].Score - second[i].Score) < 0.0001f,
                "showcase demo data was not deterministic at record " + i);
        Assert(first[0].Matches("habitat") && !first[0].Matches("does-not-exist"),
            "showcase demo record filtering was incorrect");
    }

    private static void SelectionPropagation()
    {
        InsightContext context = new InsightContext();
        int changes = 0;
        context.Changed += () => changes++;
        context.Select("animal");
        context.Hover("animal");
        context.SetFilter("fish");
        Assert(context.SelectedEntityId == "animal" && context.FocusedEntityId == "animal" && changes == 3, "shared context did not propagate");
        context.Compare("other");
        Assert(context.ComparedEntityId == "other", "comparison target did not propagate");
        context.BeginFrame();
        context.EndFrame();
        Assert(context.HoveredEntityId == null, "hover was not cleared per frame");
    }

    private static void ExplanationCalculation()
    {
        InsightExplanationResult result = Explain.Value("Catch chance", 0.8f).Base(0.5f)
            .Factor("Knowledge", 1.2f).Factor("Mismatch", 0.5f).Clamp("Population cap", 0.1f, 0.8f)
            .Requirement("Lure available", true).Calculate();
        Assert(Math.Abs(result.ComputedValue - 0.3f) < 0.001f, "explanation multiplication changed");
        Assert(result.Segments.Count == 6 && result.Summary.Contains("Population cap"), "explanation segments missing");
    }

    private static void ThemeParsing()
    {
        InsightTheme theme = InsightTheme.FromXml("<theme id='test'><color name='selected' value='#123456'/><spacing value='12'/></theme>");
        Assert(theme.Id == "test" && Math.Abs(theme.Selected.R - 0x12 / 255f) < 0.001f && Math.Abs(theme.Spacing - 12f) < 0.01f, "theme XML parsing changed");
    }

    private static void GraphDeterminism()
    {
        InsightModel model = InsightModel.Create("graph").Entity("b", "B")
            .Entity(new InsightEntity("a", "A", manualPosition: new InsightPoint(50f, 60f)))
            .Entity("c", "C").Entity("d", "D")
            .Relation("a", "b", "knows").Relation("b", "c", "feeds").Relation("c", "a", "cycles");
        InsightGraphLayoutResult first = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        InsightGraphLayoutResult second = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f);
        Assert(first.Complete && first.Position("a").Equals(second.Position("a")) && first.Position("d").Equals(second.Position("d")) &&
            first.Position("a").Equals(new InsightPoint(50f, 60f)) && InsightGraphLayout.AreNeighbors(model.Snapshot(), "a", "b"),
            "graph layout is not deterministic or pinned");
    }

    private static void GraphBudgeting()
    {
        InsightModel model = InsightModel.Create("large-graph");
        for (int i = 0; i < 24; i++) model.Entity("node:" + i, "Node " + i);
        for (int i = 0; i < 24; i++)
            for (int j = i + 1; j < 24; j++) model.Relation("node:" + i, "node:" + j, "related");
        InsightGraphLayoutResult result = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f, 5, 7, 2);
        Assert(result.ActiveNodeCount <= 5 && result.Positions.Count <= 5 && result.ActiveEdgeCount <= 7 && result.Edges.Count <= 7,
            "graph layout exceeded node or edge budget");
        for (int i = 0; i < result.Edges.Count; i++)
            Assert(result.ContainsNode(result.Edges[i].FromId) && result.ContainsNode(result.Edges[i].ToId),
                "budgeted graph retained an edge outside its active node set");
    }

    private static void GraphFitAndHeaderGeometry()
    {
        InsightModel model = InsightModel.Create("fit").Entity(new InsightEntity("left", "Left",
                manualPosition: new InsightPoint(40f, 30f)))
            .Entity(new InsightEntity("right", "Right", manualPosition: new InsightPoint(360f, 210f)))
            .Entity(new InsightEntity("center", "Center", manualPosition: new InsightPoint(200f, 120f)));
        InsightGraphLayoutResult layout = InsightGraphLayout.Compute(model.Snapshot(), 400f, 240f, 10);
        InsightGraphFit fit = InsightGraphViewport.Fit(layout, 400f, 240f, 24f);
        Assert(fit.Zoom > 1f && fit.Zoom <= 2.8f, "Fit All did not calculate a bounded zoom");
        for (int i = 0; i < layout.ActiveNodeIds.Count; i++)
        {
            InsightPoint point = layout.Position(layout.ActiveNodeIds[i]);
            float x = 200f + (point.X - 200f) * fit.Zoom + fit.Pan.X;
            float y = 120f + (point.Y - 120f) * fit.Zoom + fit.Pan.Y;
            Assert(x >= 24f - 0.01f && x <= 376f + 0.01f && y >= 24f - 0.01f && y <= 216f + 0.01f,
                "Fit All left an active node outside the viewport bounds");
        }
    }

    private static void TimelineMath()
    {
        InsightTimeRange empty = InsightTimeRange.Empty;
        InsightTimeRange zero = new InsightTimeRange(0, 0);
        InsightTimeRange normal = new InsightTimeRange(10, 100);
        InsightTimeRange reversed = new InsightTimeRange(100, 10);
        Assert(empty.IsEmpty && !empty.Contains(0) && !empty.Contains(long.MinValue), "empty time range is not explicit");
        Assert(!zero.IsEmpty && zero.Contains(0) && !zero.Equals(empty), "empty time range collides with a valid zero range");
        Assert(!normal.IsEmpty && normal.Contains(10) && normal.Contains(100) && !normal.Contains(101),
            "normal time range boundaries changed");
        Assert(!reversed.IsEmpty && reversed.Start == 10 && reversed.End == 100 && reversed.Contains(10) && reversed.Contains(100),
            "reversed time range bounds did not normalize");
        List<InsightEvent> events = new List<InsightEvent>
        {
            new InsightEvent("before", -500000, "Before"), new InsightEvent("start", 10, "Start"),
            new InsightEvent("end", 100, "End"), new InsightEvent("after", 9000000, "After")
        };
        InsightTimeRange range = InsightTimelineMath.Bounds(events);
        IReadOnlyList<InsightTimelineCluster> clusters = InsightTimelineMath.Cluster(events, range, 100f, 20);
        int allEventCount = 0;
        for (int i = 0; i < clusters.Count; i++) allEventCount += clusters[i].Count;
        Assert(range.Start == -500000 && range.End == 9000000 && allEventCount == events.Count, "timeline bounds changed");
        IReadOnlyList<InsightTimelineCluster> defaultClusters = InsightTimelineMath.Cluster(events, empty, 100f, 20);
        int defaultEventCount = 0;
        for (int i = 0; i < defaultClusters.Count; i++) defaultEventCount += defaultClusters[i].Count;
        Assert(defaultEventCount == events.Count, "default timeline filtering excluded events");
        IReadOnlyList<InsightTimelineCluster> boundaryClusters = InsightTimelineMath.Cluster(events, normal, 100f, 20);
        int boundaryEventCount = 0;
        for (int i = 0; i < boundaryClusters.Count; i++) boundaryEventCount += boundaryClusters[i].Count;
        Assert(boundaryEventCount == 2, "timeline filtering is not boundary-inclusive");
        InsightContext context = new InsightContext();
        Assert(context.TimeRange.IsEmpty, "default timeline state is not no-filter");
        InsightTimeRange effective = InsightTimelineMath.EffectiveRange(context.TimeRange, range);
        Assert(effective.Equals(range) && InsightTimelineMath.Cluster(events, effective, 100f, 20).Count > 0,
            "default timeline filtering lost arbitrary ticks");
        context.SetTimeRange(reversed);
        Assert(!context.TimeRange.IsEmpty && context.TimeRange.Start == 10 && context.TimeRange.End == 100,
            "selected timeline range changed");
        context.SetTimeRange(InsightTimeRange.Empty);
        Assert(context.TimeRange.IsEmpty && InsightTimelineMath.EffectiveRange(context.TimeRange, range).Equals(range),
            "reset timeline state is not no-filter");
        InsightTimeRange zoomed = InsightTimelineMath.Zoom(range, 2f, 55);
        Assert(zoomed.End - zoomed.Start < range.End - range.Start, "timeline zoom changed");
        InsightTimeRange extreme = new InsightTimeRange(long.MinValue, long.MaxValue);
        InsightTimeRange extremeZoom = InsightTimelineMath.Zoom(extreme, 2f, 0L);
        Assert(InsightTimelineMath.TickAt(extreme, 0.5) == 0L && !extremeZoom.IsEmpty && extremeZoom.Contains(0L),
            "timeline arithmetic overflowed at long tick boundaries");
        List<InsightEvent> clusteredEvents = new List<InsightEvent>
        {
            new InsightEvent("same-1", 10, "A"), new InsightEvent("same-2", 10, "B"),
            new InsightEvent("separate", 100, "C")
        };
        IReadOnlyList<InsightTimelineCluster> deterministicClusters = InsightTimelineMath.Cluster(clusteredEvents,
            InsightTimelineMath.Bounds(clusteredEvents), 100f, 20);
        Assert(deterministicClusters.Count == 2 && deterministicClusters[0].Count == 2,
            "timeline clustering changed");
    }

    private static void OverlayOwnership()
    {
        object ownerA = new object();
        object ownerB = new object();
        List<OverlayTestEntry> entries = new List<OverlayTestEntry>
        {
            new OverlayTestEntry("a-1", ownerA), new OverlayTestEntry("b-1", ownerB),
            new OverlayTestEntry("a-2", ownerA)
        };
        int removed = InsightOverlayOwnership.ClearOwner(entries, ownerA, entry => entry.OwnerToken);
        Assert(removed == 2 && entries.Count == 1 && entries[0].Id == "b-1", "owner-scoped overlay clearing removed another owner");
        Assert(InsightOverlayOwnership.ClearOwner(entries, ownerA, entry => entry.OwnerToken) == 0 && entries.Count == 1,
            "owner clearing was not idempotent");
        Assert(InsightOverlayOwnership.ClearOwner(entries, ownerB, entry => entry.OwnerToken) == 1 && entries.Count == 0,
            "second overlay owner could not be cleared");
    }

    private static void Serialization()
    {
        InsightModel model = InsightModel.Create("roundtrip")
            .Entity(new InsightEntity("a", "A", "first", "people", new object(), new object(),
                new[] { "important", "known" }, new InsightPoint(25f, 40f), "source:a", "icons/a.png"))
            .Entity("b", "B").Relation("a", "b", "contains", 0.6f, false, 0.75f, false)
            .Metric("a", "value", new InsightMetric("value", 0.75f, new InsightRange(0.2f, 0.9f), true, true, 0.8f, 0.5f, InsightTrend.Rising,
                new[] { new InsightSample(1, 0.4f), new InsightSample(2, 0.75f) }))
            .Action("a", new InsightAction("inspect", "Inspect", () => { }, true, "Show details", true))
            .Explanation("a", Explain.Value("Score", 0.6f).Base(0.5f).Factor("knowledge", 1.2f, 0.8f, true)
                .Add("penalty", -0.1f, 0.7f, false).Clamp("cap", 0f, 1f).Requirement("ready", true, "prepared")
                .Uncertain(0.2f, 0.8f, 0.5f, "missing data"))
            .Event(new InsightEvent("event", 12, "Changed", "test", new[] { "a", "b" }, 0.4f, false, "map-link"));
        InsightModelSnapshot original = model.Snapshot();
        InsightModelSerializationReport writeReport = InsightModelSerialization.SerializeWithDiagnostics(original);
        Assert(writeReport.Succeeded && writeReport.Xml.Contains("schemaVersion=\"2\"") &&
            Contains(writeReport.Warnings, "Source omitted") && Contains(writeReport.Warnings, "Icon/texture omitted") &&
            Contains(writeReport.Warnings, "callback omitted"),
            "runtime serialization diagnostics are incomplete");
        Assert(writeReport.Xml == InsightModelSerialization.Serialize(original), "serialized output is not deterministic");
        InsightAction originalAction = original.ActionsFor("a")[0];
        Assert(originalAction.ConfiguredEnabled && originalAction.Enabled, "live action did not retain configured intent");

        InsightModelSerializationReport readReport = InsightModelSerialization.DeserializeWithDiagnostics(writeReport.Xml);
        Assert(readReport.Succeeded && readReport.Model != null && readReport.Warnings.Count >= 3,
            "full model did not load with runtime omission diagnostics");
        InsightModelSnapshot copy = readReport.Model.Snapshot();
        InsightEntity copiedEntity = copy.Entity("a");
        Assert(copy.Id == "roundtrip" && copy.Entities.Count == 2 && copy.Relations.Count == 1 &&
            copiedEntity.Badges.Count == 2 && copiedEntity.ManualPosition.HasValue &&
            copiedEntity.ManualPosition.Value.Equals(new InsightPoint(25f, 40f)) &&
            copiedEntity.SourceId == "source:a" && copiedEntity.IconId == "icons/a.png" &&
            copiedEntity.Source == null && copiedEntity.Icon == null, "entity display data did not round-trip safely");
        Assert(copy.MetricsFor("a").Count == 1 && Math.Abs(copy.MetricsFor("a")[0].Value - 0.75f) < 0.001f &&
            copy.MetricsFor("a")[0].History.Count == 2 && copy.Events.Count == 1 &&
            copy.Events[0].EntityIds.Count == 2 && copy.Events[0].MapLinkId == "map-link" &&
            copy.Relations[0].Directed == false && !copy.Relations[0].Known, "model values did not round-trip");
        InsightExplanationResult originalExplanation = original.ExplanationFor("a").Calculate();
        InsightExplanationResult copiedExplanation = copy.ExplanationFor("a").Calculate();
        Assert(copiedExplanation.Segments.Count == originalExplanation.Segments.Count &&
            Math.Abs(copiedExplanation.ComputedValue - originalExplanation.ComputedValue) < 0.001f,
            "explanation data did not round-trip");
        InsightAction copiedAction = copy.ActionsFor("a")[0];
        Assert(copiedAction.Id == "inspect" && copiedAction.Label == "Inspect" && copiedAction.Tooltip == "Show details" &&
            copiedAction.ConfiguredEnabled && copiedAction.CloseWindowAfterInvoke && !copiedAction.Enabled &&
            copiedAction.Callback == null && !copiedAction.Invoke(),
            "runtime action was restored as invokable");

        bool reboundInvoked = false;
        readReport.Model.RebindAction("a", "inspect", () => reboundInvoked = true);
        InsightAction rebound = readReport.Model.Snapshot().ActionsFor("a")[0];
        Assert(rebound.ConfiguredEnabled && rebound.Enabled && rebound.Invoke() && reboundInvoked,
            "callback rebinding did not restore runtime executability");

        InsightModelSerializationReport legacy = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel id='legacy'><entities><entity id='a' label='A'/></entities><relations/>" +
            "<metrics/><events><event id='e' tick='9' label='Old'><entity id='a'/></event></events></insightModel>");
        Assert(legacy.Succeeded && legacy.Model.Snapshot().Entity("a") != null && legacy.Model.Snapshot().Events.Count == 1 &&
            Contains(legacy.Warnings, "unversioned"), "existing unversioned model format no longer loads");

        InsightModelSerializationReport schemaV2 = InsightModelSerialization.DeserializeWithDiagnostics(
            "<insightModel schemaVersion='2' id='v2'><entities><entity id='a' label='A'/></entities>" +
            "<actions><action entity='a' id='old-action' label='Old' enabled='true' callback='omitted'/></actions></insightModel>");
        InsightAction oldAction = schemaV2.Model.Snapshot().ActionsFor("a")[0];
        Assert(schemaV2.Succeeded && oldAction.ConfiguredEnabled && !oldAction.Enabled && oldAction.Callback == null &&
            Contains(schemaV2.Warnings, "rebind"), "schema-v2 action intent was not loaded safely");
    }

    private static void SerializationOrdering()
    {
        InsightModel first = OrderedModel(false);
        InsightModel second = OrderedModel(true);
        string firstXml = InsightModelSerialization.Serialize(first.Snapshot());
        string secondXml = InsightModelSerialization.Serialize(second.Snapshot());
        Assert(firstXml == secondXml, "equivalent models with different insertion orders serialized differently");
    }

    private static InsightModel OrderedModel(bool reverse)
    {
        InsightModel model = InsightModel.Create("ordering");
        InsightEntity[] entities =
        {
            new InsightEntity("z", "Z", manualPosition: new InsightPoint(3f, 4f)),
            new InsightEntity("a", "A", manualPosition: new InsightPoint(1f, 2f)),
            new InsightEntity("m", "M", manualPosition: new InsightPoint(2f, 3f))
        };
        if (reverse)
        {
            model.Entity(entities[2]).Entity(entities[0]).Entity(entities[1]);
            model.Relation("a", "m", "link").Relation("z", "a", "link");
            model.Metric("m", "score", 2f).Metric("a", "score", 1f);
            model.Action("m", new InsightAction("m-action", "M action", null, false));
            model.Action("a", new InsightAction("a-action", "A action", null, false));
            model.Explanation("m", Explain.Value("M", 2f)).Explanation("a", Explain.Value("A", 1f));
            model.Event(new InsightEvent("event", 1, "Event", entityIds: new[] { "a", "z" }));
        }
        else
        {
            model.Entity(entities[0]).Entity(entities[1]).Entity(entities[2]);
            model.Relation("z", "a", "link").Relation("a", "m", "link");
            model.Metric("a", "score", 1f).Metric("m", "score", 2f);
            model.Action("a", new InsightAction("a-action", "A action", null, false));
            model.Action("m", new InsightAction("m-action", "M action", null, false));
            model.Explanation("a", Explain.Value("A", 1f)).Explanation("m", Explain.Value("M", 2f));
            model.Event(new InsightEvent("event", 1, "Event", entityIds: new[] { "z", "a" }));
        }
        return model;
    }

    private static void MotionSettings()
    {
        Assert(Math.Abs(InsightMotion.Approach(0f, 10f, 0.1f, 8f, true) - 10f) < 0.001f, "reduced motion did not settle immediately");
        float value = InsightMotion.Approach(0f, 10f, 0.1f, 8f, false);
        Assert(value > 0f && value < 10f, "delta-time motion changed");
    }

    private static void Prompt2Foundations()
    {
        InsightTheme promptTheme = InsightTheme.Default.Clone();
        promptTheme.Warning = new InsightColor(0.91f, 0.48f, 0.16f);
        InsightUiCallout callout = InsightUi.Callout("prompt2-callout", InsightUiCalloutSeverity.Warning,
            "Initial title", "Initial body").SetIcon(InsightUiIcon.FromText("!"));
        callout.Title = "Updated title";
        callout.Body = "Updated body";
        TestPainter compositePainter = new TestPainter();
        RenderAtWidth(callout, 520f, 180f, promptTheme, InsightUiDensity.Normal, compositePainter);
        InsightUiSurface calloutAccent = FindElement(callout, "prompt2-callout.accent") as InsightUiSurface;
        InsightUiLabel calloutTitle = FindElement(callout, "prompt2-callout.title") as InsightUiLabel;
        InsightUiLabel calloutBody = FindElement(callout, "prompt2-callout.body") as InsightUiLabel;
        Assert(calloutAccent != null && calloutAccent.Style.Background.Equals(promptTheme.Warning) &&
            calloutTitle != null && calloutTitle.DisplayText == "Updated title" &&
            calloutBody != null && calloutBody.DisplayText == "Updated body",
            "callout severity or dynamic content did not resolve through the frame theme");

        Assert(Math.Abs(InsightUi.Meter("meter-half", 50f, 100f).NormalizedValue - 0.5f) < 0.001f &&
            InsightUi.Meter("meter-low", -10f, 100f).NormalizedValue == 0f &&
            InsightUi.Meter("meter-high", 120f, 100f).NormalizedValue == 1f &&
            InsightUi.Meter("meter-invalid", 1f, 0f).NormalizedValue == 0f,
            "meter normalization did not clamp capacity values deterministically");

        InsightUiSectionHeader section = InsightUi.SectionHeader("prompt2-section", "Storage", "Configure stockpile behavior")
            .SetIcon(InsightUiIcon.FromText("S"))
            .SetTrailing(InsightUi.Button("prompt2-section-action", "Reset"))
            .SetDivider();
        InsightUiStatRow stat = InsightUi.StatRow("prompt2-stat", "Stored power", "620 / 1000 Wd")
            .SetSecondary("Workshop reserve")
            .SetIcon(InsightUiIcon.FromText("P"))
            .SetValueColor(promptTheme.Positive)
            .SetTooltip("Stored workshop reserve");
        Assert(section.Divider && FindElement(section, "prompt2-section.icon") != null &&
            FindElement(section, "prompt2-section-action") != null && stat.Secondary == "Workshop reserve" &&
            stat.Icon != null && stat.ValueColor.Equals(promptTheme.Positive),
            "section header optional fields or stat row composition were not retained");

        InsightUiElement[] composites = { callout, section, stat,
            InsightUi.Meter("meter-layout", 620f, 1000f).SetLabel("Stored power").SetValueText("620 / 1000 Wd") };
        for (int i = 0; i < composites.Length; i++)
        {
            RenderAtWidth(composites[i], 620f, 220f, promptTheme, InsightUiDensity.Comfortable, new TestPainter());
            AssertFiniteTree(composites[i], "wide composite " + i);
            RenderAtWidth(composites[i], 260f, 320f, promptTheme, InsightUiDensity.Compact, new TestPainter());
            AssertFiniteTree(composites[i], "narrow composite " + i);
        }

        Assert(section.Children.Count == 1 && stat.Children.Count == 1,
            "composites did not expose one stable public root child");

        float linear = InsightMotion.Eased(0.5f, InsightMotionEasing.Linear);
        float smooth = InsightMotion.Eased(0.5f, InsightMotionEasing.Smooth);
        float easeOut = InsightMotion.Eased(0.5f, InsightMotionEasing.EaseOut);
        Assert(Math.Abs(linear - 0.5f) < 0.001f && smooth > 0.49f && smooth < 0.51f &&
            easeOut > smooth && easeOut < 1f, "compact easing math changed");

        InsightUiEffects effects = new InsightUiEffects();
        effects.Transition("fade", 0f, 0.016f, 0.16f, false);
        float moving = effects.Transition("fade", 1f, 0.016f, 0.16f, false, InsightMotionEasing.EaseOut);
        Assert(moving > 0f && moving < 1f, "keyed transition did not progress incrementally");
        Assert(Math.Abs(effects.Transition("fade", 0f, 0.016f, 0.16f, true) - 0f) < 0.001f,
            "reduced motion did not settle keyed transitions");
        effects.Flash("save", 0.2f);
        Assert(effects.FlashProgress("save", 0.016f, false) > 0f &&
            effects.FlashProgress("save", 0.016f, true) > 0f, "keyed flash did not honor progression or reduced motion");
        effects.Flash("long", 1f);
        float longFlash = effects.FlashProgress("long", 0.25f, false);
        Assert(longFlash > 0.74f && longFlash < 0.76f, "flash intensity ignored its configured duration");

        InsightUiStateStore state = new InsightUiStateStore();
        InsightUiDiagnostics diagnostics = new InsightUiDiagnostics();
        TestPainter painter = new TestPainter();
        InsightUiVirtualList list = InsightUi.VirtualList("bounded-list", 10000, 24f,
            index => InsightUi.Label("row-" + index, "Row " + index));
        list.Overscan = 2;
        list.CacheLimit = 12;
        state.SetFloat("bounded-list.scrollY", 0f);
        Render(list, painter, diagnostics, null, state);
        int firstCache = list.CachedItemCount;
        state.SetFloat("bounded-list.scrollY", 9000f);
        Render(list, painter, diagnostics, null, state);
        Assert(firstCache > 0 && list.CachedItemCount <= 12 &&
            diagnostics.VirtualizedVisibleElements > 0 && diagnostics.VirtualizedCachedElements <= 12,
            "virtualized cache was not bounded relative to the viewport");
        list.Refresh();
        Assert(list.CachedItemCount == 0, "virtualized refresh did not clear the bounded cache");

        bool customPainted = false;
        InsightUiFade fade = InsightUi.Fade("prompt2-fade", true, InsightUi.Custom("prompt2-custom",
            context => customPainted = context.Frame.Opacity > 0.99f,
            (constraints, frame) => new InsightUiSize(40f, 24f)));
        Render(fade, painter, null, null, new InsightUiStateStore());
        Assert(customPainted, "fade effect did not render its content");

        InsightUiSurface styled = InsightUi.Surface("styled", InsightUi.Label("styled-label", "Scaled"));
        styled.SetBorder(new InsightColor(1f, 1f, 1f), 3f);
        Render(styled, painter);
        Assert(painter.LastSurfaceStyle != null && Math.Abs(painter.LastSurfaceStyle.BorderWidth - 3f) < 0.001f,
            "surface border token was not passed to the painter");

        int dropdownSelection = 1;
        InsightUiDropdown dropdown = InsightUi.Dropdown("prompt2-dropdown", "Priority",
            new[] { "Low", "Normal", "Urgent" }, 1, (index, value) => dropdownSelection = index);
        InsightUiStateStore widgetState = new InsightUiStateStore();
        widgetState.SetBool("prompt2-dropdown.open", true);
        painter.ClickLabels.Add("Urgent");
        Render(dropdown, painter, null, null, widgetState);
        Assert(dropdown.Selected == 2 && dropdownSelection == 2 && !widgetState.GetBool("prompt2-dropdown.open", true),
            "dropdown did not select an option and close");

        string searchValue = string.Empty;
        InsightUiSearchField search = InsightUi.SearchField("prompt2-search", string.Empty, "Filter",
            value => searchValue = value);
        painter.TextValue = "colonist";
        Render(search, painter);
        Assert(search.Value == "colonist" && searchValue == "colonist", "search field did not propagate text");
        painter.ClickLabels.Add("×");
        Render(search, painter);
        Assert(search.Value == string.Empty, "search field clear action did not reset text");

        InsightUiSegmented segmented = InsightUi.Segmented("prompt2-segmented",
            new[] { "Draft", "Review", "Live" }, 0);
        painter.ClickLabels.Add("Live");
        Render(segmented, painter);
        Assert(segmented.Selected == 2, "segmented selection did not choose the clicked option");

        InsightUiPopover popover = InsightUi.Popover("prompt2-popover",
            InsightUi.Button("prompt2-trigger", "More"), InsightUi.Label("prompt2-content", "Actions"));
        InsightUiStateStore popoverState = new InsightUiStateStore();
        popover.SetOpen(true);
        Render(popover, painter, null, null, popoverState);
        Assert(popoverState.GetBool("prompt2-popover.open", false), "popover did not open through document state");
        popover.SetOpen(false);
        Render(popover, painter, null, null, popoverState);
        Assert(!popoverState.GetBool("prompt2-popover.open", true), "popover did not close through document state");

        InsightUiSplit draggable = InsightUi.Split("prompt2-split", InsightUi.Label("left", "Left"),
            InsightUi.Label("right", "Right"), 0.4f);
        draggable.Draggable = true;
        InsightUiStateStore splitState = new InsightUiStateStore();
        painter.DragRatio = 0.7f;
        Render(draggable, painter, null, null, splitState);
        Assert(Math.Abs(splitState.GetFloat("prompt2-split.ratio", 0f) - 0.7f) < 0.001f,
            "draggable split did not persist its ratio");

        InsightUiToastService toasts = new InsightUiToastService();
        toasts.Show("Saved", InsightToastSeverity.Success, 0.2f);
        Assert(toasts.IsVisible && toasts.Severity == InsightToastSeverity.Success, "toast service did not show feedback");
        toasts.Advance(0.3f, false);
        Assert(!toasts.IsVisible, "toast service did not expire transient feedback");
    }

    private static void Prompt3Foundations()
    {
        InsightUiSlideFade outer = InsightUi.SlideFade("prompt3-outer", true,
            InsightUi.SlideFade("prompt3-inner", true, InsightUi.Label("prompt3-slide-label", "Inspector details"),
                InsightUiSlideDirection.Right), InsightUiSlideDirection.Down);
        TestPainter painter = new TestPainter();
        InsightUiStateStore state = new InsightUiStateStore();
        InsightUiEffects effects = new InsightUiEffects();
        InsightUiDiagnostics diagnostics = new InsightUiDiagnostics();
        InsightUiFrame frame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            state, diagnostics, 1f / 60f, effects: effects, hostBounds: new InsightRect(0f, 0f, 640f, 360f));
        diagnostics.BeginFrame();
        outer.Measure(new InsightUiConstraints(0f, 640f, 0f, 360f), frame);
        outer.Arrange(new InsightRect(0f, 0f, 640f, 360f), frame);
        InsightRect finalBounds = outer.LayoutRect;
        outer.Paint(painter, frame);
        outer.VisibleTarget = false;
        ((InsightUiSlideFade)outer.Content).VisibleTarget = false;
        diagnostics.BeginFrame();
        outer.Measure(new InsightUiConstraints(0f, 640f, 0f, 360f), frame);
        outer.Arrange(new InsightRect(0f, 0f, 640f, 360f), frame);
        outer.Paint(painter, frame);
        Assert(outer.LayoutRect.Equals(finalBounds), "SlideFade changed layout bounds during animation");
        Assert(painter.TranslationDepth == 0, "SlideFade leaked translation state after nested painting");
        Assert(painter.MaximumTranslationDepth >= 2, "SlideFade did not compose nested translation scopes");
        Assert(Math.Abs(painter.LastTranslation.X) > 0f || Math.Abs(painter.LastTranslation.Y) > 0f,
            "SlideFade did not apply restrained cardinal movement");

        InsightUiEffects interrupted = new InsightUiEffects();
        interrupted.Transition("interrupt", 0f, 0.016f, 0.16f, false);
        float entering = interrupted.Transition("interrupt", 1f, 0.016f, 0.16f, false, InsightMotionEasing.EaseOut);
        float leaving = interrupted.Transition("interrupt", 0f, 0.016f, 0.16f, false, InsightMotionEasing.EaseOut);
        Assert(entering > 0f && entering < 1f && leaving < entering && leaving > 0f,
            "SlideFade transition interruption did not reverse from its current value");

        InsightUiSlideFade reduced = InsightUi.SlideFade("prompt3-reduced", false,
            InsightUi.Label("prompt3-reduced-label", "Reduced motion"), InsightUiSlideDirection.Left);
        TestPainter reducedPainter = new TestPainter();
        InsightUiFrame reducedFrame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, true,
            new InsightUiStateStore(), new InsightUiDiagnostics(), 1f / 60f,
            effects: new InsightUiEffects(), hostBounds: new InsightRect(0f, 0f, 300f, 120f));
        reducedFrame.Diagnostics.BeginFrame();
        reduced.Measure(new InsightUiConstraints(0f, 300f, 0f, 120f), reducedFrame);
        reduced.Arrange(new InsightRect(0f, 0f, 300f, 120f), reducedFrame);
        reduced.Paint(reducedPainter, reducedFrame);
        reduced.VisibleTarget = true;
        reducedFrame.Diagnostics.BeginFrame();
        reduced.Measure(new InsightUiConstraints(0f, 300f, 0f, 120f), reducedFrame);
        reduced.Arrange(new InsightRect(0f, 0f, 300f, 120f), reducedFrame);
        reduced.Paint(reducedPainter, reducedFrame);
        Assert(reducedPainter.TranslationDepth == 0 && reducedPainter.LastTranslation.Equals(new InsightPoint(0f, 0f)),
            "reduced motion did not eliminate SlideFade movement");

        InsightUiHoverCard hoverCard = InsightUi.HoverCard("prompt3-hover", InsightUi.Button("prompt3-hover-trigger", "Power"),
            InsightUi.Label("prompt3-hover-content", "Stored power and reserve details."));
        InsightUiStateStore hoverState = new InsightUiStateStore();
        InsightUiDiagnostics hoverDiagnostics = new InsightUiDiagnostics();
        TestPainter hoverPainter = new TestPainter { PointerOver = true };
        InsightUiFrame hoverFrame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            hoverState, hoverDiagnostics, 0.1f, effects: new InsightUiEffects(),
            hostBounds: new InsightRect(0f, 0f, 200f, 100f));
        hoverDiagnostics.BeginFrame();
        hoverCard.Measure(new InsightUiConstraints(0f, 40f, 0f, 30f), hoverFrame);
        hoverCard.Arrange(new InsightRect(160f, 70f, 40f, 30f), hoverFrame);
        hoverCard.Paint(hoverPainter, hoverFrame);
        Assert(!hoverCard.IsOpen, "hover card opened before its delay elapsed");
        hoverDiagnostics.BeginFrame();
        hoverCard.Measure(new InsightUiConstraints(0f, 40f, 0f, 30f), hoverFrame);
        hoverCard.Arrange(new InsightRect(160f, 70f, 40f, 30f), hoverFrame);
        hoverCard.Paint(hoverPainter, hoverFrame);
        Assert(hoverCard.IsOpen && hoverCard.CardRect.X >= 0f && hoverCard.CardRect.Right <= 200f &&
            hoverCard.CardRect.Y >= 0f && hoverCard.CardRect.Bottom <= 100f,
            "hover card did not open after delay or clamp to host edges");
        hoverPainter.PointerOver = false;
        hoverFrame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            hoverState, hoverDiagnostics, 0.13f, effects: hoverFrame.Effects,
            hostBounds: new InsightRect(0f, 0f, 200f, 100f));
        hoverDiagnostics.BeginFrame();
        hoverCard.Measure(new InsightUiConstraints(0f, 40f, 0f, 30f), hoverFrame);
        hoverCard.Arrange(new InsightRect(160f, 70f, 40f, 30f), hoverFrame);
        hoverCard.Paint(hoverPainter, hoverFrame);
        Assert(!hoverCard.IsOpen, "hover card did not close after leaving its trigger and grace period");

        hoverPainter.PointerOver = true;
        hoverFrame = new InsightUiFrame(InsightTheme.Default, InsightUiDensity.Normal, false, false,
            hoverState, hoverDiagnostics, 0.2f, effects: new InsightUiEffects(),
            hostBounds: new InsightRect(0f, 0f, 200f, 100f));
        hoverDiagnostics.BeginFrame();
        hoverCard.Measure(new InsightUiConstraints(0f, 40f, 0f, 30f), hoverFrame);
        hoverCard.Arrange(new InsightRect(160f, 0f, 40f, 30f), hoverFrame);
        hoverCard.Paint(hoverPainter, hoverFrame);
        Assert(hoverCard.IsOpen, "hover card did not reopen for cleanup test");
        InsightUiDocument document = new InsightUiDocument("prompt3-document", hoverCard);
        document.State.SetBool("prompt3-hover.hover.open", true);
        document.Root = InsightUi.Empty("replacement", "Replacement");
        Assert(!document.State.GetBool("prompt3-hover.hover.open", false),
            "replacing a document root left stale hover state behind");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RenderAtWidth(InsightUiElement root, float width, float height, InsightTheme theme,
        InsightUiDensity density, TestPainter painter)
    {
        InsightUiDiagnostics diagnostics = new InsightUiDiagnostics();
        InsightUiFrame frame = new InsightUiFrame(theme, density, false, false,
            new InsightUiStateStore(), diagnostics, 1f / 60f);
        diagnostics.BeginFrame();
        root.Measure(new InsightUiConstraints(0f, width, 0f, height), frame);
        root.Arrange(new InsightRect(0f, 0f, width, height), frame);
        root.Paint(painter, frame);
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

    private static void AssertFiniteTree(InsightUiElement root, string name)
    {
        InsightRect rect = root.LayoutRect;
        Assert(!float.IsNaN(rect.X) && !float.IsNaN(rect.Y) && !float.IsNaN(rect.Width) &&
            !float.IsNaN(rect.Height) && !float.IsInfinity(rect.X) && !float.IsInfinity(rect.Y) &&
            !float.IsInfinity(rect.Width) && !float.IsInfinity(rect.Height) && rect.Width >= 0f && rect.Height >= 0f,
            name + " produced invalid geometry");
        InsightUiStack stack = root as InsightUiStack;
        if (stack != null)
        {
            for (int i = 0; i < stack.Children.Count; i++)
            {
                InsightUiElement first = stack.Children[i];
                if (!first.Visible) continue;
                for (int j = i + 1; j < stack.Children.Count; j++)
                {
                    InsightUiElement second = stack.Children[j];
                    if (!second.Visible) continue;
                    bool overlap = first.LayoutRect.X < second.LayoutRect.Right - 0.01f &&
                        second.LayoutRect.X < first.LayoutRect.Right - 0.01f &&
                        first.LayoutRect.Y < second.LayoutRect.Bottom - 0.01f &&
                        second.LayoutRect.Y < first.LayoutRect.Bottom - 0.01f;
                    Assert(!overlap, name + " has overlapping stack children");
                }
            }
        }
        for (int i = 0; i < root.Children.Count; i++)
            AssertFiniteTree(root.Children[i], name);
    }

    private static bool Contains(IReadOnlyList<string> messages, string text)
    {
        for (int i = 0; i < messages.Count; i++)
            if (messages[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private sealed class TestPainter : IInsightUiPainter, IInsightUiCustomPainter, IInsightUiIconPainter,
        IInsightUiDragPainter, IInsightUiTranslationPainter, IInsightUiHoverPainter
    {
        public int ToggleChanges;
        public float SliderValue = float.NaN;
        public string TextValue;
        public int ButtonClicks;
        public bool CustomDrawSupported;
        public int FillRectCalls;
        public int IconCalls;
        public InsightUiStyle LastSurfaceStyle;
        public InsightColor? LastTextColor;
        public int TextCalls;
        public InsightRect LastTextRect;
        public bool LastTextWrap;
        public readonly HashSet<string> ClickLabels = new HashSet<string>(StringComparer.Ordinal);
        public float DragRatio = float.NaN;
        public int TranslationDepth;
        public int MaximumTranslationDepth;
        public InsightPoint LastTranslation;
        public bool PointerOver;

        public InsightUiSize MeasureText(string text, InsightUiTextStyle style, float maxWidth, InsightUiFrame frame)
        {
            float width = (text ?? string.Empty).Length * 7f;
            return new InsightUiSize(Math.Min(float.IsPositiveInfinity(maxWidth) ? width : maxWidth, width), 18f);
        }

        public void Surface(InsightRect rect, InsightUiStyle style, InsightUiFrame frame) { LastSurfaceStyle = style; }
        public void Text(InsightRect rect, string text, InsightUiTextStyle style, InsightColor? color, bool wrap, InsightUiFrame frame)
        {
            TextCalls++;
            LastTextRect = rect;
            LastTextWrap = wrap;
            LastTextColor = color;
        }
        public void Progress(InsightRect rect, float value, InsightColor fill, InsightUiFrame frame) { }

        public bool Button(InsightRect rect, string label, bool enabled, bool selected, InsightUiFrame frame)
        {
            if (!enabled) return false;
            if (ClickLabels.Remove(label)) return true;
            if (ButtonClicks <= 0) return false;
            ButtonClicks--;
            return true;
        }

        public bool Toggle(InsightRect rect, string label, bool value, bool enabled, InsightUiFrame frame)
        {
            if (enabled && ToggleChanges > 0)
            {
                ToggleChanges--;
                return !value;
            }
            return value;
        }

        public float Slider(InsightRect rect, float value, float minimum, float maximum, bool enabled, InsightUiFrame frame)
        {
            return enabled && !float.IsNaN(SliderValue) ? Math.Max(minimum, Math.Min(maximum, SliderValue)) : value;
        }

        public string TextField(InsightRect rect, string value, bool enabled, InsightUiFrame frame)
        {
            if (enabled && TextValue != null)
            {
                string result = TextValue;
                TextValue = null;
                return result;
            }
            return value;
        }

        public void Divider(InsightRect rect, InsightColor color, InsightUiFrame frame) { }
        public void Tooltip(InsightRect rect, string text, InsightUiFrame frame) { }
        public void BeginClip(InsightRect rect) { }
        public void EndClip() { }
        public float ScrollOffset(InsightRect viewport, float contentHeight, float offset, string stateKey, InsightUiFrame frame) => offset;

        public void PushTranslation(InsightPoint offset)
        {
            TranslationDepth++;
            MaximumTranslationDepth = Math.Max(MaximumTranslationDepth, TranslationDepth);
            LastTranslation = offset;
        }

        public void PopTranslation() { TranslationDepth = Math.Max(0, TranslationDepth - 1); }

        public bool IsPointerOver(InsightRect rect, InsightUiFrame frame) => PointerOver;

        public void FillRect(InsightRect rect, InsightColor color, InsightUiFrame frame) { FillRectCalls++; }
        public void Outline(InsightRect rect, InsightColor color, float width, InsightUiFrame frame) { }
        public void Line(float x1, float y1, float x2, float y2, InsightColor color, float width, InsightUiFrame frame) { }
        public void Texture(InsightRect rect, object texture, InsightColor? tint, InsightUiFrame frame) { }

        public void Icon(InsightRect rect, InsightUiIcon icon, InsightUiFrame frame) { IconCalls++; }
        public bool IconButton(InsightRect rect, InsightUiIcon icon, bool enabled, bool selected, InsightUiFrame frame)
        {
            IconCalls++;
            return Button(rect, icon?.Fallback, enabled, selected, frame);
        }

        public float DragDivider(InsightRect divider, InsightRect bounds, InsightUiOrientation orientation, float ratio,
            string stateKey, InsightUiFrame frame) => DragRatio;
    }

    private sealed class TestInput : IInsightUiInput
    {
        public bool IsTextEditing { get; set; }
        public bool TabPressed { get; set; }
        public bool ShiftTabPressed { get; set; }
        public bool ActivatePressed { get; set; }
        public bool TabConsumed { get; private set; }
        public bool ActivationConsumed { get; private set; }

        public void ConsumeTab() { TabConsumed = true; TabPressed = false; }
        public void ConsumeActivation() { ActivationConsumed = true; ActivatePressed = false; }
    }

    private sealed class OverlayTestEntry
    {
        internal readonly string Id;
        internal readonly object OwnerToken;

        internal OverlayTestEntry(string id, object ownerToken)
        {
            Id = id;
            OwnerToken = ownerToken;
        }
    }
}
