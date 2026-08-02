using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace InsightCanvas
{
    /// <summary>Engine-independent RGBA token used by themes and test fixtures.</summary>
    public struct InsightColor : IEquatable<InsightColor>
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public InsightColor(float r, float g, float b, float a = 1f)
        {
            R = Clamp01(r);
            G = Clamp01(g);
            B = Clamp01(b);
            A = Clamp01(a);
        }

        public InsightColor WithAlpha(float alpha) => new InsightColor(R, G, B, alpha);

        public InsightColor Blend(InsightColor other, float amount)
        {
            amount = Clamp01(amount);
            return new InsightColor(R + (other.R - R) * amount, G + (other.G - G) * amount,
                B + (other.B - B) * amount, A + (other.A - A) * amount);
        }

        public float Luminance => R * 0.2126f + G * 0.7152f + B * 0.0722f;

        public static bool TryParse(string value, out InsightColor color)
        {
            color = new InsightColor(1f, 1f, 1f, 1f);
            if (string.IsNullOrWhiteSpace(value)) return false;
            string text = value.Trim();
            if (text[0] == '#') text = text.Substring(1);
            if (text.Length != 6 && text.Length != 8) return false;
            uint packed;
            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packed)) return false;
            if (text.Length == 6)
                color = new InsightColor(((packed >> 16) & 255) / 255f, ((packed >> 8) & 255) / 255f, (packed & 255) / 255f);
            else
                color = new InsightColor(((packed >> 24) & 255) / 255f, ((packed >> 16) & 255) / 255f,
                    ((packed >> 8) & 255) / 255f, (packed & 255) / 255f);
            return true;
        }

        public bool Equals(InsightColor other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
        public override bool Equals(object obj) => obj is InsightColor && Equals((InsightColor)obj);
        public override int GetHashCode() => (((R.GetHashCode() * 397) ^ G.GetHashCode()) * 397 ^ B.GetHashCode()) * 397 ^ A.GetHashCode();
        public override string ToString() => R.ToString("0.###", CultureInfo.InvariantCulture) + "," + G.ToString("0.###", CultureInfo.InvariantCulture) + "," + B.ToString("0.###", CultureInfo.InvariantCulture) + "," + A.ToString("0.###", CultureInfo.InvariantCulture);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>Optional chart transforms for users who cannot distinguish the default palette.</summary>
    public enum InsightColorBlindMode
    {
        None,
        Deuteranopia,
        Protanopia,
        Tritanopia
    }

    /// <summary>Semantic tokens for all stock Insight Canvas components.</summary>
    public sealed class InsightTheme
    {
        private readonly Dictionary<string, InsightColor> relationColors = new Dictionary<string, InsightColor>(StringComparer.OrdinalIgnoreCase);
        private readonly List<InsightColor> chartPalette = new List<InsightColor>();

        public string Id { get; set; } = "default";
        public InsightColor Background { get; set; }
        public InsightColor Surface { get; set; }
        public InsightColor ElevatedSurface { get; set; }
        public InsightColor PrimaryText { get; set; }
        public InsightColor SecondaryText { get; set; }
        public InsightColor Selected { get; set; }
        public InsightColor Hover { get; set; }
        public InsightColor Focus { get; set; }
        public InsightColor Positive { get; set; }
        public InsightColor Negative { get; set; }
        public InsightColor Warning { get; set; }
        public InsightColor Unknown { get; set; }
        public InsightColor Locked { get; set; }
        public InsightColor Shadow { get; set; }
        public float CornerRadius { get; set; }
        public float Spacing { get; set; }
        public float TitleSize { get; set; }
        public float BodySize { get; set; }
        public float CaptionSize { get; set; }
        public string PanelTexturePath { get; set; }
        public string BorderTexturePath { get; set; }
        public IReadOnlyList<InsightColor> ChartPalette => chartPalette;

        public InsightTheme()
        {
            Background = new InsightColor(0.055f, 0.065f, 0.07f);
            Surface = new InsightColor(0.105f, 0.12f, 0.125f);
            ElevatedSurface = new InsightColor(0.145f, 0.16f, 0.165f);
            PrimaryText = new InsightColor(0.91f, 0.9f, 0.84f);
            SecondaryText = new InsightColor(0.62f, 0.66f, 0.65f);
            Selected = new InsightColor(0.24f, 0.62f, 0.7f);
            Hover = new InsightColor(0.28f, 0.45f, 0.48f);
            Focus = new InsightColor(0.85f, 0.68f, 0.28f);
            Positive = new InsightColor(0.34f, 0.72f, 0.5f);
            Negative = new InsightColor(0.82f, 0.36f, 0.32f);
            Warning = new InsightColor(0.9f, 0.65f, 0.27f);
            Unknown = new InsightColor(0.48f, 0.5f, 0.52f);
            Locked = new InsightColor(0.34f, 0.36f, 0.38f);
            Shadow = new InsightColor(0f, 0f, 0f, 0.28f);
            CornerRadius = 4f;
            Spacing = 8f;
            TitleSize = 1.25f;
            BodySize = 1f;
            CaptionSize = 0.82f;
            chartPalette.Add(new InsightColor(0.28f, 0.72f, 0.78f));
            chartPalette.Add(new InsightColor(0.86f, 0.62f, 0.27f));
            chartPalette.Add(new InsightColor(0.55f, 0.78f, 0.4f));
            chartPalette.Add(new InsightColor(0.76f, 0.43f, 0.72f));
            chartPalette.Add(new InsightColor(0.86f, 0.43f, 0.35f));
        }

        /// <summary>Creates a polished baseline palette without requiring external art.</summary>
        public static InsightTheme Default => new InsightTheme();

        public void SetRelationColor(string relationType, InsightColor color)
        {
            if (!string.IsNullOrWhiteSpace(relationType)) relationColors[relationType] = color;
        }

        public InsightColor RelationColor(string relationType)
        {
            InsightColor color;
            return relationType != null && relationColors.TryGetValue(relationType, out color) ? color : Selected;
        }

        /// <summary>Returns an accessibility-adjusted copy. Status is never conveyed by this color alone in stock views.</summary>
        public InsightTheme WithAccessibility(bool highContrast, InsightColorBlindMode colorBlindMode)
        {
            InsightTheme copy = Clone();
            if (highContrast)
            {
                copy.Background = new InsightColor(0.015f, 0.02f, 0.025f);
                copy.Surface = new InsightColor(0.12f, 0.14f, 0.15f);
                copy.ElevatedSurface = new InsightColor(0.2f, 0.22f, 0.23f);
                copy.PrimaryText = new InsightColor(1f, 1f, 0.98f);
                copy.SecondaryText = new InsightColor(0.82f, 0.86f, 0.84f);
            }
            if (colorBlindMode != InsightColorBlindMode.None)
            {
                copy.chartPalette.Clear();
                copy.chartPalette.Add(new InsightColor(0.1f, 0.45f, 0.85f));
                copy.chartPalette.Add(new InsightColor(0.9f, 0.55f, 0.1f));
                copy.chartPalette.Add(new InsightColor(0.55f, 0.25f, 0.75f));
                copy.chartPalette.Add(new InsightColor(0.1f, 0.65f, 0.45f));
                copy.chartPalette.Add(new InsightColor(0.75f, 0.25f, 0.2f));
            }
            return copy;
        }

        public InsightTheme Clone()
        {
            InsightTheme copy = new InsightTheme
            {
                Id = Id,
                Background = Background,
                Surface = Surface,
                ElevatedSurface = ElevatedSurface,
                PrimaryText = PrimaryText,
                SecondaryText = SecondaryText,
                Selected = Selected,
                Hover = Hover,
                Focus = Focus,
                Positive = Positive,
                Negative = Negative,
                Warning = Warning,
                Unknown = Unknown,
                Locked = Locked,
                Shadow = Shadow,
                CornerRadius = CornerRadius,
                Spacing = Spacing,
                TitleSize = TitleSize,
                BodySize = BodySize,
                CaptionSize = CaptionSize,
                PanelTexturePath = PanelTexturePath,
                BorderTexturePath = BorderTexturePath
            };
            copy.relationColors.Clear();
            foreach (KeyValuePair<string, InsightColor> pair in relationColors) copy.relationColors[pair.Key] = pair.Value;
            copy.chartPalette.Clear();
            copy.chartPalette.AddRange(chartPalette);
            return copy;
        }

        /// <summary>Parses a compact optional theme XML file. Unknown tokens are ignored for forward compatibility.</summary>
        public static InsightTheme FromXml(string xml, InsightTheme fallback = null)
        {
            InsightTheme theme = fallback?.Clone() ?? Default;
            if (string.IsNullOrWhiteSpace(xml)) return theme;
            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xml);
                XmlNode root = document.DocumentElement;
                if (root == null) return theme;
                XmlAttribute id = root.Attributes?["id"];
                if (id != null && !string.IsNullOrWhiteSpace(id.Value)) theme.Id = id.Value;
                foreach (XmlNode node in root.SelectNodes(".//color"))
                {
                    string name = node.Attributes?["name"]?.Value;
                    string value = node.Attributes?["value"]?.Value ?? node.InnerText;
                    InsightColor color;
                    if (string.IsNullOrWhiteSpace(name) || !InsightColor.TryParse(value, out color)) continue;
                    ApplyColor(theme, name, color);
                }
                XmlNode spacing = root.SelectSingleNode(".//spacing");
                if (spacing?.Attributes?["value"] != null) theme.Spacing = ParseFloat(spacing.Attributes["value"].Value, theme.Spacing);
                XmlNode corner = root.SelectSingleNode(".//cornerRadius");
                if (corner?.Attributes?["value"] != null) theme.CornerRadius = ParseFloat(corner.Attributes["value"].Value, theme.CornerRadius);
                XmlNode typography = root.SelectSingleNode(".//typography");
                if (typography != null)
                {
                    theme.TitleSize = ParseFloat(typography.Attributes?["title"]?.Value, theme.TitleSize);
                    theme.BodySize = ParseFloat(typography.Attributes?["body"]?.Value, theme.BodySize);
                    theme.CaptionSize = ParseFloat(typography.Attributes?["caption"]?.Value, theme.CaptionSize);
                }
                XmlNode panel = root.SelectSingleNode(".//panelTexture");
                if (panel != null) theme.PanelTexturePath = panel.InnerText?.Trim();
                XmlNode border = root.SelectSingleNode(".//borderTexture");
                if (border != null) theme.BorderTexturePath = border.InnerText?.Trim();
            }
            catch (XmlException)
            {
                return theme;
            }
            return theme;
        }

        private static void ApplyColor(InsightTheme theme, string name, InsightColor color)
        {
            switch (name.ToLowerInvariant())
            {
                case "background": theme.Background = color; break;
                case "surface": theme.Surface = color; break;
                case "elevatedsurface": case "elevated": theme.ElevatedSurface = color; break;
                case "primarytext": case "text": theme.PrimaryText = color; break;
                case "secondarytext": case "secondary": theme.SecondaryText = color; break;
                case "selected": theme.Selected = color; break;
                case "hover": theme.Hover = color; break;
                case "focus": theme.Focus = color; break;
                case "positive": case "success": theme.Positive = color; break;
                case "negative": case "error": theme.Negative = color; break;
                case "warning": theme.Warning = color; break;
                case "unknown": theme.Unknown = color; break;
                case "locked": theme.Locked = color; break;
                default: if (name.StartsWith("relation.", StringComparison.OrdinalIgnoreCase)) theme.SetRelationColor(name.Substring(9), color); break;
            }
        }

        private static float ParseFloat(string value, float fallback)
        {
            float result;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? Math.Max(0f, result) : fallback;
        }
    }
}
