using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Embeds a general-purpose Insight UI document in a caller-owned Rect.</summary>
    public sealed class InsightUiHost
    {
        private readonly object overlayOwnerToken = new object();

        public InsightUiHost(InsightUiDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public InsightUiDocument Document { get; private set; }
        public InsightUiDiagnostics Diagnostics => Document.Diagnostics;
        public InsightUiStateStore State => Document.State;

        public void Draw(Rect rect, float deltaTime = -1f)
        {
            using (InsightMapBridge.BeginOwner(overlayOwnerToken))
                InsightUiRenderer.Draw(rect, Document, deltaTime);
        }

        /// <summary>Runs an interaction with the same owner scope used during drawing.</summary>
        public void RunWithOverlayOwnership(Action action)
        {
            if (action == null) return;
            using (InsightMapBridge.BeginOwner(overlayOwnerToken)) action();
        }

        public void PostClose()
        {
            Document.CloseTransientOverlays();
            InsightMapBridge.ClearOwnerToken(overlayOwnerToken);
        }
    }

    /// <summary>Window shell for documents built from the same composable API as embedded views.</summary>
    public sealed class InsightUiWindow : Window
    {
        private readonly InsightUiHost host;

        public InsightUiWindow(InsightUiDocument document) : this(document?.Id, document) { }

        public InsightUiWindow(string title, InsightUiDocument document)
        {
            host = new InsightUiHost(document ?? throw new ArgumentNullException(nameof(document)));
            optionalTitle = string.IsNullOrWhiteSpace(title) ? document.Id : title;
            doCloseX = true;
            doCloseButton = false;
            resizeable = true;
            absorbInputAroundWindow = false;
            closeOnCancel = true;
        }

        public InsightUiDocument Document => host.Document;
        public InsightUiHost Host => host;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(1180f, UI.screenWidth * 0.88f),
            Mathf.Min(780f, UI.screenHeight * 0.84f));

        public override void DoWindowContents(Rect inRect)
        {
            host.Draw(inRect, Time.deltaTime);
        }

        public override void PostClose()
        {
            host.PostClose();
            base.PostClose();
        }
    }

    /// <summary>RimWorld/Unity implementation of the renderer contract.</summary>
    public sealed class RimWorldInsightUiPainter : IInsightUiPainter, IInsightUiCustomPainter,
        IInsightUiIconPainter, IInsightUiFocusPainter, IInsightUiDragPainter
    {
        private readonly Stack<Vector2> origins = new Stack<Vector2>();
        private Vector2 origin;

        internal void Reset()
        {
            origins.Clear();
            origin = Vector2.zero;
        }

        public InsightUiSize MeasureText(string text, InsightUiTextStyle style, float maxWidth, InsightUiFrame frame)
        {
            GameFont previousFont = Verse.Text.Font;
            bool previousWrap = Verse.Text.WordWrap;
            try
            {
                Verse.Text.Font = FontFor(style);
                Verse.Text.WordWrap = true;
                Vector2 size = Verse.Text.CalcSize(text ?? string.Empty);
                if (!float.IsPositiveInfinity(maxWidth) && maxWidth > 1f && size.x > maxWidth)
                    size.y = Verse.Text.CalcHeight(text ?? string.Empty, maxWidth);
                float scale = frame == null ? 1f : frame.TextScale(style);
                return new InsightUiSize(size.x * scale, size.y * scale);
            }
            finally
            {
                Verse.Text.Font = previousFont;
                Verse.Text.WordWrap = previousWrap;
            }
        }

        public void Surface(InsightRect rect, InsightUiStyle style, InsightUiFrame frame)
        {
            InsightColor fill = style.Background ?? (style.Elevated ? frame.Theme.ElevatedSurface : frame.Theme.Surface);
            if (style.Elevated && frame.Theme.Shadow.A > 0.001f)
                Widgets.DrawBoxSolid(ToRect(new InsightRect(rect.X + 2f, rect.Y + 3f, rect.Width, rect.Height)),
                    InsightDraw.Color(frame.ApplyOpacity(frame.Theme.Shadow)));
            Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(frame.ApplyOpacity(fill)));
            InsightColor border = style.Border ?? frame.Theme.SecondaryText.WithAlpha(0.38f);
            float width = style.BorderWidth <= 0f ? 0f : Math.Max(1f, style.BorderWidth);
            if (width > 0f)
            {
                Color previous = GUI.color;
                try
                {
                    GUI.color = InsightDraw.Color(frame.ApplyOpacity(border));
                    Widgets.DrawBox(ToRect(rect), Mathf.Clamp(Mathf.RoundToInt(width), 1, 8));
                }
                finally { GUI.color = previous; }
            }
        }

        public void Text(InsightRect rect, string text, InsightUiTextStyle style, InsightColor? color, bool wrap, InsightUiFrame frame)
        {
            GameFont previousFont = Verse.Text.Font;
            TextAnchor previousAnchor = Verse.Text.Anchor;
            bool previousWrap = Verse.Text.WordWrap;
            try
            {
                Verse.Text.Font = FontFor(style);
                Verse.Text.Anchor = TextAnchor.UpperLeft;
                Verse.Text.WordWrap = wrap;
                Color previous = GUI.color;
                Matrix4x4 previousMatrix = GUI.matrix;
                try
                {
                    GUI.color = InsightDraw.Color(frame.ApplyOpacity(color ?? frame.Theme.PrimaryText));
                    float scale = frame.TextScale(style);
                    if (Math.Abs(scale - 1f) > 0.001f)
                        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), ToRect(rect).center);
                    Widgets.Label(ToRect(rect), text ?? string.Empty);
                }
                finally
                {
                    GUI.matrix = previousMatrix;
                    GUI.color = previous;
                }
            }
            finally
            {
                Verse.Text.Font = previousFont;
                Verse.Text.Anchor = previousAnchor;
                Verse.Text.WordWrap = previousWrap;
            }
        }

        public void Progress(InsightRect rect, float value, InsightColor fill, InsightUiFrame frame)
        {
            InsightRect track = new InsightRect(rect.X, rect.Y + Math.Max(0f, (rect.Height - 8f) * 0.5f), rect.Width, 8f);
            Widgets.DrawBoxSolid(ToRect(track), InsightDraw.Color(frame.ApplyOpacity(frame.Theme.Background)));
            Widgets.DrawBoxSolid(ToRect(new InsightRect(track.X, track.Y, track.Width * Mathf.Clamp01(value), track.Height)),
                InsightDraw.Color(frame.ApplyOpacity(fill)));
        }

        public bool Button(InsightRect rect, string label, bool enabled, bool selected, InsightUiFrame frame)
        {
            Rect nativeRect = ToRect(rect);
            bool hovered = Event.current != null && nativeRect.Contains(Event.current.mousePosition);
            bool pressed = hovered && Event.current != null && Event.current.type == EventType.MouseDown;
            InsightColor fill = !enabled ? frame.Theme.Locked.WithAlpha(0.28f) :
                pressed ? frame.Theme.Hover.WithAlpha(0.46f) : selected ? frame.Theme.Selected.WithAlpha(0.32f) :
                hovered ? frame.Theme.Hover.WithAlpha(0.22f) : frame.Theme.Surface;
            InsightUiStyle style = new InsightUiStyle { Background = fill, Elevated = selected };
            Surface(rect, style, frame);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(enabled ? frame.Theme.PrimaryText : frame.Theme.SecondaryText));
                return Widgets.ButtonText(ToRect(new InsightRect(rect.X + 1f, rect.Y + 1f,
                    Math.Max(0f, rect.Width - 2f), Math.Max(0f, rect.Height - 2f))), label ?? string.Empty);
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        public bool Toggle(InsightRect rect, string label, bool value, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                bool next = value;
                Widgets.CheckboxLabeled(ToRect(rect), label ?? string.Empty, ref next);
                return next;
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        public float Slider(InsightRect rect, float value, float minimum, float maximum, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                return Widgets.HorizontalSlider(ToRect(rect), value, minimum, maximum, true);
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        public string TextField(InsightRect rect, string value, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                return Widgets.TextField(ToRect(rect), value ?? string.Empty);
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        public void Divider(InsightRect rect, InsightColor color, InsightUiFrame frame)
        {
            Color previous = GUI.color;
            try
            {
                InsightColor applied = frame.ApplyOpacity(color);
                GUI.color = InsightDraw.Color(applied);
                Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(applied));
            }
            finally { GUI.color = previous; }
        }

        public void Tooltip(InsightRect rect, string text, InsightUiFrame frame)
        {
            TooltipHandler.TipRegion(ToRect(rect), text);
        }

        public void BeginClip(InsightRect rect)
        {
            origins.Push(origin);
            Rect group = ToRect(rect);
            GUI.BeginGroup(group);
            origin += new Vector2(rect.X, rect.Y);
        }

        public void EndClip()
        {
            GUI.EndGroup();
            origin = origins.Count > 0 ? origins.Pop() : Vector2.zero;
        }

        public float ScrollOffset(InsightRect viewport, float contentHeight, float offset, string stateKey, InsightUiFrame frame)
        {
            float maximum = Mathf.Max(0f, contentHeight - viewport.Height);
            offset = Mathf.Clamp(offset, 0f, maximum);
            Rect localViewport = ToRect(viewport);
            if (Event.current != null && Event.current.type == EventType.ScrollWheel && localViewport.Contains(Event.current.mousePosition))
            {
                offset = Mathf.Clamp(offset + Event.current.delta.y * 24f, 0f, maximum);
                Event.current.Use();
            }
            if (maximum > 0.01f)
            {
                Rect track = ToRect(new InsightRect(viewport.Right - 5f, viewport.Y + 3f, 3f, Math.Max(0f, viewport.Height - 6f)));
                float thumbHeight = Mathf.Max(18f, track.height * viewport.Height / Math.Max(viewport.Height, contentHeight));
                float travel = Mathf.Max(0f, track.height - thumbHeight);
                float y = track.y + (maximum <= 0.01f ? 0f : travel * offset / maximum);
                Widgets.DrawBoxSolid(new Rect(track.x, y, track.width, thumbHeight), InsightDraw.Color(frame.Theme.Selected.WithAlpha(0.72f)));
            }
            return offset;
        }

        public void FillRect(InsightRect rect, InsightColor color, InsightUiFrame frame)
        {
            Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(frame.ApplyOpacity(color)));
        }

        public void Outline(InsightRect rect, InsightColor color, float width, InsightUiFrame frame)
        {
            Color previous = GUI.color;
            try
            {
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(color));
                Widgets.DrawBox(ToRect(rect), Math.Max(1, Mathf.RoundToInt(width)));
            }
            finally { GUI.color = previous; }
        }

        public void Line(float x1, float y1, float x2, float y2, InsightColor color, float width, InsightUiFrame frame)
        {
            Widgets.DrawLine(new Vector2(x1 - origin.x, y1 - origin.y),
                new Vector2(x2 - origin.x, y2 - origin.y), InsightDraw.Color(frame.ApplyOpacity(color)), Math.Max(1f, width));
        }

        public void Texture(InsightRect rect, object texture, InsightColor? tint, InsightUiFrame frame)
        {
            Texture2D image = texture as Texture2D;
            if (image == null) return;
            Color previous = GUI.color;
            GUI.color = InsightDraw.Color(frame.ApplyOpacity(tint ?? new InsightColor(1f, 1f, 1f, 1f)));
            try { GUI.DrawTexture(ToRect(rect), image, ScaleMode.ScaleToFit, true); }
            finally { GUI.color = previous; }
        }

        public void Icon(InsightRect rect, InsightUiIcon icon, InsightUiFrame frame)
        {
            if (icon == null) return;
            if (icon.Texture is Texture2D)
                Texture(rect, icon.Texture, null, frame);
            else
                Text(rect, icon.Fallback, InsightUiTextStyle.Label, frame.Theme.PrimaryText, false, frame);
        }

        public bool IconButton(InsightRect rect, InsightUiIcon icon, bool enabled, bool selected, InsightUiFrame frame)
        {
            Rect nativeRect = ToRect(rect);
            bool hovered = Event.current != null && nativeRect.Contains(Event.current.mousePosition);
            bool pressed = hovered && Event.current != null && Event.current.type == EventType.MouseDown;
            InsightColor fill = !enabled ? frame.Theme.Locked.WithAlpha(0.28f) :
                pressed ? frame.Theme.Hover.WithAlpha(0.46f) : selected ? frame.Theme.Selected.WithAlpha(0.32f) :
                hovered ? frame.Theme.Hover.WithAlpha(0.22f) : frame.Theme.Surface;
            Surface(rect, new InsightUiStyle { Background = fill, Elevated = selected }, frame);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                Texture2D image = icon?.Texture as Texture2D;
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(enabled ? frame.Theme.PrimaryText : frame.Theme.SecondaryText));
                if (image == null)
                    return Widgets.ButtonText(ToRect(new InsightRect(rect.X + 1f, rect.Y + 1f,
                        Math.Max(0f, rect.Width - 2f), Math.Max(0f, rect.Height - 2f))), icon?.Fallback ?? string.Empty);
                return GUI.Button(ToRect(new InsightRect(rect.X + 1f, rect.Y + 1f,
                    Math.Max(0f, rect.Width - 2f), Math.Max(0f, rect.Height - 2f))), image);
            }
            finally { GUI.enabled = previousEnabled; }
        }

        public void FocusRing(InsightRect rect, InsightUiFrame frame)
        {
            GUI.color = InsightDraw.Color(frame.ApplyOpacity(frame.Theme.Focus));
            Widgets.DrawBox(ToRect(new InsightRect(rect.X - 2f, rect.Y - 2f, rect.Width + 4f, rect.Height + 4f)), 2);
        }

        public float DragDivider(InsightRect divider, InsightRect bounds, InsightUiOrientation orientation, float ratio,
            string stateKey, InsightUiFrame frame)
        {
            Rect native = ToRect(divider);
            Event current = Event.current;
            bool dragging = frame.State.GetBool(stateKey + ".dragging", false);
            if (current != null && current.type == EventType.MouseDown && native.Contains(current.mousePosition))
            {
                dragging = true;
                frame.State.SetBool(stateKey + ".dragging", true);
                current.Use();
            }
            if (dragging && current != null && (current.type == EventType.MouseDrag || current.type == EventType.MouseMove))
            {
                float next = orientation == InsightUiOrientation.Horizontal
                    ? (current.mousePosition.x - ToRect(bounds).x) / Math.Max(1f, bounds.Width)
                    : (current.mousePosition.y - ToRect(bounds).y) / Math.Max(1f, bounds.Height);
                current.Use();
                return Math.Max(0.1f, Math.Min(0.9f, next));
            }
            if (dragging && current != null && (current.type == EventType.MouseUp || current.type == EventType.Ignore))
            {
                frame.State.SetBool(stateKey + ".dragging", false);
                current.Use();
            }
            return float.NaN;
        }

        private Rect ToRect(InsightRect rect)
        {
            return new Rect(rect.X - origin.x, rect.Y - origin.y, rect.Width, rect.Height);
        }

        private static GameFont FontFor(InsightUiTextStyle style)
        {
            switch (style)
            {
                case InsightUiTextStyle.Title: return GameFont.Medium;
                case InsightUiTextStyle.Heading: return GameFont.Small;
                case InsightUiTextStyle.Caption: return GameFont.Tiny;
                default: return GameFont.Small;
            }
        }
    }

    /// <summary>Static entry point for embedding a document in a caller-owned Rect.</summary>
    public static class InsightUiRenderer
    {
        [ThreadStatic]
        private static RimWorldInsightUiPainter sharedPainter;

        public static void Draw(Rect rect, InsightUiDocument document, float deltaTime = -1f)
        {
            if (document == null || document.Root == null) return;
            using (new InsightGuiStateScope())
            {
                document.Diagnostics.BeginFrame();
                document.Diagnostics.TrackDuplicateIds = document.TrackDuplicateIds;
                document.Focus.BeginFrame();
                RimWorldInsightUiPainter painter = sharedPainter ?? (sharedPainter = new RimWorldInsightUiPainter());
                painter.Reset();
                bool highContrast = document.HighContrast || InsightCanvasMod.Settings?.HighContrast == true;
                bool reducedMotion = document.ReducedMotion || InsightCanvasMod.Settings?.ReducedMotion == true;
                bool colorBlind = InsightCanvasMod.Settings?.ColorBlindFriendly == true;
                InsightColorBlindMode colorBlindMode = colorBlind ? InsightColorBlindMode.Deuteranopia : InsightColorBlindMode.None;
                InsightTheme theme = document.ResolveTheme(highContrast, colorBlindMode);
                float elapsed = deltaTime < 0f ? Time.deltaTime : deltaTime;
                document.Toasts.Advance(elapsed, reducedMotion);
                InsightUiFrame frame = new InsightUiFrame(theme, document.Density, highContrast, reducedMotion,
                    document.State, document.Diagnostics, elapsed, document.Focus, document.Effects, document.Toasts);
                frame.TextMeasurer = (text, style, maxWidth) => painter.MeasureText(text, style, maxWidth, frame);
                try
                {
                    frame.Focus.ProcessKeyboard(new RimWorldInsightUiInput(frame.Focus));
                    if (document.DrawBackground)
                        painter.Surface(new InsightRect(rect.x, rect.y, rect.width, rect.height),
                            new InsightUiStyle { Background = theme.Background, Elevated = false }, frame);
                    InsightUiConstraints constraints = new InsightUiConstraints(0f, rect.width, 0f, rect.height);
                    document.Root.Measure(constraints, frame);
                    document.Root.Arrange(new InsightRect(rect.x, rect.y, rect.width, rect.height), frame);
                    document.Root.Paint(painter, frame);
                    document.Focus.PruneFocus();
                    document.Diagnostics.RecordEffects(document.Effects.ActiveCount);
                }
                catch (Exception exception)
                {
                    document.Diagnostics.RecordRenderError();
                    Log.ErrorOnce("[Insight Canvas] Composable document '" + document.Id + "' failed: " + exception,
                        ("insight-ui-document:" + document.Id).GetHashCode());
                    GUI.color = Color.white;
                    Widgets.Label(rect, "Insight Canvas could not render this document. See the log for details.");
                }
                finally
                {
                    painter.Reset();
                }
            }
        }
    }

    /// <summary>Small Unity IMGUI adapter used only at the renderer boundary.</summary>
    internal sealed class RimWorldInsightUiInput : IInsightUiInput
    {
        private readonly InsightUiFocusState focus;

        public RimWorldInsightUiInput(InsightUiFocusState focus)
        {
            this.focus = focus;
        }

        public bool IsTextEditing => focus != null && focus.IsTextEditing;
        public bool TabPressed => IsKey(KeyCode.Tab);
        public bool ShiftTabPressed => TabPressed && Event.current.shift;
        public bool ActivatePressed => IsKey(KeyCode.Return) || IsKey(KeyCode.KeypadEnter) || IsKey(KeyCode.Space);

        public void ConsumeTab()
        {
            if (Event.current != null) Event.current.Use();
        }

        public void ConsumeActivation()
        {
            if (Event.current != null) Event.current.Use();
        }

        private static bool IsKey(KeyCode key)
        {
            return Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == key;
        }
    }
}
