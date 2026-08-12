using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Portable deterministic records shared by the showcase and its regression checks.</summary>
    public sealed class InsightShowcaseRecord
    {
        public InsightShowcaseRecord(string id, string name, string group, string tag, float score, InsightColor color)
        {
            Id = id;
            Name = name;
            Group = group;
            Tag = tag;
            Score = score;
            Color = color;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Group { get; private set; }
        public string Tag { get; private set; }
        public float Score { get; private set; }
        public InsightColor Color { get; private set; }

        public bool Matches(string filter)
        {
            filter = filter ?? string.Empty;
            return Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                Group.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                Tag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class InsightFeatureShowcaseData
    {
        public static IReadOnlyList<InsightShowcaseRecord> CreateRecords()
        {
            string[] groups = { "Habitat", "Pawn", "Research", "Supply" };
            InsightColor[] colors = { InsightTheme.Default.Positive, InsightTheme.Default.Selected,
                InsightTheme.Default.Warning, InsightTheme.Default.Focus };
            List<InsightShowcaseRecord> result = new List<InsightShowcaseRecord>();
            for (int i = 0; i < 64; i++)
            {
                int group = i % groups.Length;
                result.Add(new InsightShowcaseRecord("record-" + i, "Record " + (i + 1).ToString("00"),
                    groups[group], "tag-" + (i % 5), 0.24f + ((i * 17) % 68) / 100f, colors[group]));
            }
            return result;
        }
    }
}
