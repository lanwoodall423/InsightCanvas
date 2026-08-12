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
        internal const int RoundedSurfaceCacheCapacity = InsightUiSurfaceMath.RoundedRadiusBucketCount;
        private static readonly Texture2D[] roundedSurfaceMasks = new Texture2D[RoundedSurfaceCacheCapacity];
        private readonly Stack<Vector2> origins = new Stack<Vector2>();
        private readonly GUIStyle[] textStyles = new GUIStyle[6];
        private readonly int[] textStyleSizes = new int[6];
        private readonly Font[] textStyleFonts = new Font[6];
        private readonly GUIContent textContent = new GUIContent();
        private GUISkin textStyleSkin;
        private Vector2 origin;

        internal void Reset()
        {
            origins.Clear();
            origin = Vector2.zero;
        }

        public InsightUiSize MeasureText(string text, InsightUiTextStyle style, float maxWidth, InsightUiFrame frame)
        {
            return MeasureTextWithScale(text, style, maxWidth, frame == null ? 1f : frame.TextScale(style), frame);
        }

        internal InsightUiSize MeasureNativeText(string text, InsightUiTextStyle style, float maxWidth, InsightUiFrame frame)
        {
            return MeasureTextWithScale(text, style, maxWidth, 1f, frame);
        }

        private InsightUiSize MeasureTextWithScale(string text, InsightUiTextStyle style, float maxWidth,
            float scale, InsightUiFrame frame)
        {
            GUIStyle effective = EffectiveTextStyle(style, scale);
            bool previousWrap = effective.wordWrap;
            try
            {
                effective.wordWrap = true;
                textContent.text = text ?? string.Empty;
                GUIContent content = textContent;
                Vector2 size = effective.CalcSize(content);
                if (!float.IsPositiveInfinity(maxWidth) && maxWidth > 1f && size.x > maxWidth)
                {
                    size.x = maxWidth;
                    size.y = effective.CalcHeight(content, maxWidth);
                }
                return new InsightUiSize(Math.Max(0f, size.x), Math.Max(0f, size.y));
            }
            finally { effective.wordWrap = previousWrap; }
        }

        public void Surface(InsightRect rect, InsightUiStyle style, InsightUiFrame frame)
        {
            InsightColor fill = style.Background ?? (style.Elevated ? frame.Theme.ElevatedSurface : frame.Theme.Surface);
            InsightColor border = style.Border ?? frame.Theme.SecondaryText.WithAlpha(0.38f);
            float width = style.BorderWidth <= 0f ? 0f : Math.Max(1f, style.BorderWidth);
            float radius = InsightUiSurfaceMath.ResolveCornerRadius(style, frame.Theme);
            Color previous = GUI.color;
            try
            {
                if (style.Elevated && frame.Theme.Shadow.A > 0.001f)
                {
                    DrawSurfaceShape(new InsightRect(rect.X + 2f, rect.Y + 3f, rect.Width, rect.Height),
                        radius, frame.ApplyOpacity(frame.Theme.Shadow));
                }
                if (radius <= 0.01f)
                {
                    Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(frame.ApplyOpacity(fill)));
                    if (width > 0f)
                    {
                        GUI.color = InsightDraw.Color(frame.ApplyOpacity(border));
                        Widgets.DrawBox(ToRect(rect), Mathf.Clamp(Mathf.RoundToInt(width), 1, 8));
                    }
                    return;
                }

                if (width > 0f)
                {
                    DrawSurfaceShape(rect, radius, frame.ApplyOpacity(border));
                    float inset = Math.Min(width, Math.Min(rect.Width, rect.Height) * 0.5f);
                    InsightRect inner = new InsightRect(rect.X + inset, rect.Y + inset,
                        Math.Max(0f, rect.Width - inset * 2f), Math.Max(0f, rect.Height - inset * 2f));
                    if (inner.Width > 0.01f && inner.Height > 0.01f)
                        DrawSurfaceShape(inner, Math.Max(0f, radius - inset), frame.ApplyOpacity(fill));
                }
                else DrawSurfaceShape(rect, radius, frame.ApplyOpacity(fill));
            }
            finally { GUI.color = previous; }
        }

        private void DrawSurfaceShape(InsightRect rect, float radius, InsightColor color)
        {
            radius = InsightUiSurfaceMath.QuantizeCornerRadius(radius);
            if (radius <= 0.01f)
            {
                Widgets.DrawBoxSolid(ToRect(rect), InsightDraw.Color(color));
                return;
            }

            Texture2D mask = RoundedSurfaceMask(radius);
            Color previous = GUI.color;
            try
            {
                GUI.color = InsightDraw.Color(color);
                DrawNineSlice(ToRect(rect), mask, radius);
            }
            finally { GUI.color = previous; }
        }

        private static Texture2D RoundedSurfaceMask(float radius)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(radius * 0.5f), 0, roundedSurfaceMasks.Length - 1);
            Texture2D mask = roundedSurfaceMasks[index];
            if (mask != null) return mask;

            int corner = index * 2;
            int size = corner * 2 + 1;
            mask = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                name = "InsightCanvas Rounded Surface Mask " + corner,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onHorizontalCenter = x >= corner && x <= size - 1 - corner;
                    bool onVerticalCenter = y >= corner && y <= size - 1 - corner;
                    float alphaValue;
                    if (onHorizontalCenter || onVerticalCenter)
                        alphaValue = 1f;
                    else
                    {
                        float nearestX = x < corner ? corner : size - 1 - corner;
                        float nearestY = y < corner ? corner : size - 1 - corner;
                        float distance = Mathf.Sqrt((x - nearestX) * (x - nearestX) + (y - nearestY) * (y - nearestY));
                        alphaValue = Mathf.Clamp01(corner + 0.5f - distance);
                    }
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaValue * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            roundedSurfaceMasks[index] = mask;
            return mask;
        }

        private static void DrawNineSlice(Rect target, Texture2D texture, float radius)
        {
            float corner = Mathf.Min(radius, Mathf.Min(target.width, target.height) * 0.5f);
            if (corner <= 0.01f)
            {
                GUI.DrawTexture(target, texture, ScaleMode.StretchToFill, true);
                return;
            }

            float sourceCorner = radius;
            float sourceSize = sourceCorner * 2f + 1f;
            float u0 = 0f;
            float u1 = sourceCorner / sourceSize;
            float u2 = (sourceCorner + 1f) / sourceSize;
            float u3 = 1f;
            float v0 = 0f;
            float v1 = sourceCorner / sourceSize;
            float v2 = (sourceCorner + 1f) / sourceSize;
            float v3 = 1f;
            float x0 = target.x;
            float x1 = target.x + corner;
            float x2 = target.x + target.width - corner;
            float x3 = target.x + target.width;
            float y0 = target.y;
            float y1 = target.y + corner;
            float y2 = target.y + target.height - corner;
            float y3 = target.y + target.height;

            DrawTextureSlice(new Rect(x0, y0, corner, corner), texture, new Rect(u0, v0, u1 - u0, v1 - v0));
            DrawTextureSlice(new Rect(x1, y0, Math.Max(0f, x2 - x1), corner), texture, new Rect(u1, v0, u2 - u1, v1 - v0));
            DrawTextureSlice(new Rect(x2, y0, corner, corner), texture, new Rect(u2, v0, u3 - u2, v1 - v0));
            DrawTextureSlice(new Rect(x0, y1, corner, Math.Max(0f, y2 - y1)), texture, new Rect(u0, v1, u1 - u0, v2 - v1));
            DrawTextureSlice(new Rect(x1, y1, Math.Max(0f, x2 - x1), Math.Max(0f, y2 - y1)), texture, new Rect(u1, v1, u2 - u1, v2 - v1));
            DrawTextureSlice(new Rect(x2, y1, corner, Math.Max(0f, y2 - y1)), texture, new Rect(u2, v1, u3 - u2, v2 - v1));
            DrawTextureSlice(new Rect(x0, y2, corner, corner), texture, new Rect(u0, v2, u1 - u0, v3 - v2));
            DrawTextureSlice(new Rect(x1, y2, Math.Max(0f, x2 - x1), corner), texture, new Rect(u1, v2, u2 - u1, v3 - v2));
            DrawTextureSlice(new Rect(x2, y2, corner, corner), texture, new Rect(u2, v2, u3 - u2, v3 - v2));
        }

        private static void DrawTextureSlice(Rect target, Texture2D texture, Rect uv)
        {
            if (target.width > 0.01f && target.height > 0.01f)
                GUI.DrawTextureWithTexCoords(target, texture, uv, true);
        }

        public void Text(InsightRect rect, string text, InsightUiTextStyle style, InsightColor? color, bool wrap, InsightUiFrame frame)
        {
            GUIStyle effective = EffectiveTextStyle(style, frame == null ? 1f : frame.TextScale(style));
            bool previousWrap = effective.wordWrap;
            Color previous = GUI.color;
            try
            {
                effective.wordWrap = wrap;
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(color ?? frame.Theme.PrimaryText));
                textContent.text = text ?? string.Empty;
                GUI.Label(ToRect(rect), textContent, effective);
            }
            finally { effective.wordWrap = previousWrap; GUI.color = previous; }
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
            Color previousColor = GUI.color;
            GameFont previousFont = Verse.Text.Font;
            GUI.enabled = enabled;
            try
            {
                Verse.Text.Font = FontFor(InsightUiTextStyle.Button);
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(enabled ? frame.Theme.PrimaryText : frame.Theme.SecondaryText));
                return Widgets.ButtonText(ToRect(new InsightRect(rect.X + 1f, rect.Y + 1f,
                    Math.Max(0f, rect.Width - 2f), Math.Max(0f, rect.Height - 2f))), label ?? string.Empty);
            }
            finally
            {
                Verse.Text.Font = previousFont;
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
            }
        }

        public bool Toggle(InsightRect rect, string label, bool value, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GameFont previousFont = Verse.Text.Font;
            GUI.enabled = enabled;
            try
            {
                Verse.Text.Font = FontFor(InsightUiTextStyle.Body);
                bool next = value;
                Widgets.CheckboxLabeled(ToRect(rect), label ?? string.Empty, ref next);
                return next;
            }
            finally
            {
                Verse.Text.Font = previousFont;
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
            }
        }

        public float Slider(InsightRect rect, float value, float minimum, float maximum, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.enabled = enabled;
            try
            {
                return Widgets.HorizontalSlider(ToRect(rect), value, minimum, maximum, true);
            }
            finally
            {
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
            }
        }

        public string TextField(InsightRect rect, string value, bool enabled, InsightUiFrame frame)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GameFont previousFont = Verse.Text.Font;
            GUI.enabled = enabled;
            try
            {
                Verse.Text.Font = FontFor(InsightUiTextStyle.Body);
                return Widgets.TextField(ToRect(rect), value ?? string.Empty);
            }
            finally
            {
                Verse.Text.Font = previousFont;
                GUI.color = previousColor;
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
            Color previousColor = GUI.color;
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
            finally
            {
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
            }
        }

        public void FocusRing(InsightRect rect, InsightUiFrame frame)
        {
            Color previous = GUI.color;
            try
            {
                GUI.color = InsightDraw.Color(frame.ApplyOpacity(frame.Theme.Focus));
                Widgets.DrawBox(ToRect(new InsightRect(rect.X - 2f, rect.Y - 2f, rect.Width + 4f, rect.Height + 4f)), 2);
            }
            finally { GUI.color = previous; }
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

        private GUIStyle EffectiveTextStyle(InsightUiTextStyle semanticStyle, float scale)
        {
            if (GUI.skin != textStyleSkin)
            {
                textStyleSkin = GUI.skin;
                Array.Clear(textStyles, 0, textStyles.Length);
                Array.Clear(textStyleSizes, 0, textStyleSizes.Length);
                Array.Clear(textStyleFonts, 0, textStyleFonts.Length);
            }

            int index = Math.Max(0, Math.Min(textStyles.Length - 1, (int)semanticStyle));
            int fontSize = Math.Max(1, Mathf.RoundToInt(InsightUiFrame.BaseTextSize(semanticStyle) * Math.Max(0.5f, scale)));
            GameFont previousFont = Verse.Text.Font;
            try
            {
                Verse.Text.Font = FontFor(semanticStyle);
                GUIStyle template = GUI.skin == null ? new GUIStyle() : GUI.skin.label;
                Font font = template.font;
                if (textStyles[index] == null || textStyleSizes[index] != fontSize || textStyleFonts[index] != font)
                {
                    GUIStyle effective = new GUIStyle(template)
                    {
                        fontSize = fontSize,
                        alignment = TextAnchor.UpperLeft,
                        clipping = TextClipping.Clip,
                        wordWrap = true
                    };
                    textStyles[index] = effective;
                    textStyleSizes[index] = fontSize;
                    textStyleFonts[index] = font;
                }
                return textStyles[index];
            }
            finally { Verse.Text.Font = previousFont; }
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
                frame.NativeTextMeasurer = (text, style, maxWidth) => painter.MeasureNativeText(text, style, maxWidth, frame);
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
