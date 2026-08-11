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

        public void PostClose()
        {
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
    public sealed class RimWorldInsightUiPainter : IInsightUiPainter
    {
        private readonly Stack<Vector2> origins = new Stack<Vector2>();
        private Vector2 origin;

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
                return new InsightUiSize(size.x, size.y);
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
            if (frame.Theme.Shadow.A > 0.001f)
                Widgets.DrawBoxSolid(ToRect(new InsightRect(rect.X + 2f, rect.Y + 3f, rect.Width, rect.Height)), InsightDraw.Color(frame.Theme.Shadow));
            Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(fill));
            InsightColor border = style.Border ?? frame.Theme.SecondaryText.WithAlpha(0.38f);
            GUI.color = InsightDraw.Color(border);
            Widgets.DrawBox(ToRect(rect), 1);
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
                GUI.color = InsightDraw.Color(color ?? frame.Theme.PrimaryText);
                Widgets.Label(ToRect(rect), text ?? string.Empty);
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
            Widgets.DrawBoxSolid(ToRect(track), InsightDraw.Color(frame.Theme.Background));
            Widgets.DrawBoxSolid(ToRect(new InsightRect(track.X, track.Y, track.Width * Mathf.Clamp01(value), track.Height)),
                InsightDraw.Color(fill));
        }

        public bool Button(InsightRect rect, string label, bool enabled, bool selected, InsightUiFrame frame)
        {
            InsightColor fill = selected ? frame.Theme.Selected.WithAlpha(0.32f) : frame.Theme.Surface;
            InsightUiStyle style = new InsightUiStyle { Background = fill, Elevated = selected };
            Surface(rect, style, frame);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            try
            {
                GUI.color = InsightDraw.Color(enabled ? frame.Theme.PrimaryText : frame.Theme.SecondaryText);
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
            GUI.color = InsightDraw.Color(color);
            Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(color));
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
        public static void Draw(Rect rect, InsightUiDocument document, float deltaTime = -1f)
        {
            if (document == null || document.Root == null) return;
            using (new InsightGuiStateScope())
            {
                document.Diagnostics.BeginFrame();
                RimWorldInsightUiPainter painter = new RimWorldInsightUiPainter();
                bool highContrast = document.HighContrast || InsightCanvasMod.Settings?.HighContrast == true;
                bool reducedMotion = document.ReducedMotion || InsightCanvasMod.Settings?.ReducedMotion == true;
                bool colorBlind = InsightCanvasMod.Settings?.ColorBlindFriendly == true;
                InsightTheme theme = (document.Theme ?? InsightTheme.Default).WithAccessibility(highContrast,
                    colorBlind ? InsightColorBlindMode.Deuteranopia : InsightColorBlindMode.None);
                InsightUiFrame frame = new InsightUiFrame(theme, document.Density, highContrast, reducedMotion,
                    document.State, document.Diagnostics, deltaTime < 0f ? Time.deltaTime : deltaTime);
                frame.TextMeasurer = (text, style, maxWidth) => painter.MeasureText(text, style, maxWidth, frame);
                try
                {
                    if (document.DrawBackground)
                        painter.Surface(new InsightRect(rect.x, rect.y, rect.width, rect.height),
                            new InsightUiStyle { Background = theme.Background, Elevated = false }, frame);
                    InsightUiConstraints constraints = new InsightUiConstraints(0f, rect.width, 0f, rect.height);
                    document.Root.Measure(constraints, frame);
                    document.Root.Arrange(new InsightRect(rect.x, rect.y, rect.width, rect.height), frame);
                    document.Root.Paint(painter, frame);
                }
                catch (Exception exception)
                {
                    document.Diagnostics.RecordRenderError();
                    Log.ErrorOnce("[Insight Canvas] Composable document '" + document.Id + "' failed: " + exception,
                        ("insight-ui-document:" + document.Id).GetHashCode());
                    GUI.color = Color.white;
                    Widgets.Label(rect, "Insight Canvas could not render this document. See the log for details.");
                }
            }
        }
    }
}
