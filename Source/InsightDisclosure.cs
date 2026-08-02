using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>
    /// Describes which fields a viewer is allowed to see. The framework does not prescribe rank names or count.
    /// </summary>
    public sealed class InsightDisclosure
    {
        public int Level { get; private set; }
        public string Label { get; private set; }
        public bool IdentityVisible { get; private set; }
        public bool ExactValuesVisible { get; private set; }
        public bool HistoryVisible { get; private set; }
        public bool CausalFactorsVisible { get; private set; }
        public bool PredictionsVisible { get; private set; }
        public float Confidence { get; private set; }

        public InsightDisclosure(int level, string label, bool identityVisible, bool exactValuesVisible,
            bool historyVisible, bool causalFactorsVisible, bool predictionsVisible, float confidence = 1f)
        {
            Level = level;
            Label = label ?? string.Empty;
            IdentityVisible = identityVisible;
            ExactValuesVisible = exactValuesVisible;
            HistoryVisible = historyVisible;
            CausalFactorsVisible = causalFactorsVisible;
            PredictionsVisible = predictionsVisible;
            Confidence = confidence < 0f ? 0f : confidence > 1f ? 1f : confidence;
        }

        public static InsightDisclosure Unknown(string label = "Unknown") =>
            new InsightDisclosure(0, label, false, false, false, false, false, 0f);
    }

    /// <summary>Supplies disclosure information for entity ids. Implement this in knowledge-driven mods.</summary>
    public interface IDisclosureProvider
    {
        InsightDisclosure ForEntity(InsightEntity entity);
    }

    /// <summary>A provider backed by a callback, useful for transient knowledge or preview modes.</summary>
    public sealed class DelegateDisclosureProvider : IDisclosureProvider
    {
        private readonly Func<InsightEntity, InsightDisclosure> resolver;

        public DelegateDisclosureProvider(Func<InsightEntity, InsightDisclosure> resolver)
        {
            this.resolver = resolver;
        }

        public InsightDisclosure ForEntity(InsightEntity entity)
        {
            InsightDisclosure value = resolver == null ? null : resolver(entity);
            return value ?? InsightDisclosure.Unknown();
        }
    }

    /// <summary>
    /// A generic ordered preview provider. Its labels and disclosure flags are supplied by the caller, not fixed by
    /// Insight Canvas, so a dependent mod can map its own epistemic system onto the renderer.
    /// </summary>
    public sealed class TieredDisclosureProvider : IDisclosureProvider
    {
        private readonly InsightDisclosure[] levels;
        private int activeLevel;

        public TieredDisclosureProvider(InsightDisclosure[] levels, int activeLevel = 0)
        {
            this.levels = levels ?? new InsightDisclosure[0];
            ActiveLevel = activeLevel;
        }

        public int ActiveLevel
        {
            get => activeLevel;
            set => activeLevel = levels.Length == 0 ? 0 : value < 0 ? 0 : value >= levels.Length ? levels.Length - 1 : value;
        }

        public InsightDisclosure ForEntity(InsightEntity entity)
        {
            if (levels.Length == 0) return InsightDisclosure.Unknown();
            return levels[activeLevel] ?? InsightDisclosure.Unknown();
        }

        public InsightDisclosure Level(int index)
        {
            return levels.Length == 0 ? InsightDisclosure.Unknown() : levels[index < 0 ? 0 : index >= levels.Length ? levels.Length - 1 : index];
        }

        public int Count => levels.Length;
    }

    /// <summary>Shared interaction state for all components in a view.</summary>
    public sealed class InsightContext
    {
        private string selectedEntityId;
        private string comparedEntityId;
        private string hoveredEntityId;
        private string focusedEntityId;
        private string filterText = string.Empty;
        private InsightTimeRange timeRange = new InsightTimeRange(1, 0);
        private bool hoverSeenThisFrame;
        private readonly Dictionary<string, InsightDisclosure> disclosureCache = new Dictionary<string, InsightDisclosure>(StringComparer.Ordinal);
        private IDisclosureProvider disclosureProvider;

        public event Action Changed;

        public int Revision { get; private set; }
        public string SelectedEntityId => selectedEntityId;
        public string ComparedEntityId => comparedEntityId;
        public string HoveredEntityId => hoveredEntityId;
        public string FocusedEntityId => focusedEntityId;
        public string FilterText => filterText;
        public InsightTimeRange TimeRange => timeRange;
        public IDisclosureProvider DisclosureProvider
        {
            get => disclosureProvider;
            set
            {
                if (disclosureProvider == value) return;
                disclosureProvider = value;
                disclosureCache.Clear();
                Touch();
            }
        }

        public InsightContext(IDisclosureProvider disclosureProvider = null)
        {
            DisclosureProvider = disclosureProvider ?? new DelegateDisclosureProvider(entity =>
                new InsightDisclosure(1, "Observed", true, true, false, false, false, 0.85f));
        }

        public void BeginFrame()
        {
            hoverSeenThisFrame = false;
        }

        public void EndFrame()
        {
            if (hoverSeenThisFrame || hoveredEntityId == null) return;
            hoveredEntityId = null;
            Touch();
        }

        public void Select(string entityId)
        {
            if (selectedEntityId == entityId) return;
            selectedEntityId = entityId;
            focusedEntityId = entityId;
            Touch();
        }

        public void Hover(string entityId)
        {
            if (hoverSeenThisFrame && hoveredEntityId == entityId) return;
            if (!hoverSeenThisFrame && hoveredEntityId == entityId)
            {
                hoverSeenThisFrame = true;
                return;
            }
            hoveredEntityId = entityId;
            hoverSeenThisFrame = true;
            Touch();
        }

        public void Focus(string entityId)
        {
            if (focusedEntityId == entityId) return;
            focusedEntityId = entityId;
            Touch();
        }

        /// <summary>Sets a second entity for comparison without changing the primary selection.</summary>
        public void Compare(string entityId)
        {
            if (comparedEntityId == entityId) return;
            comparedEntityId = entityId;
            Touch();
        }

        /// <summary>Clears the secondary comparison target.</summary>
        public void ClearComparison()
        {
            if (comparedEntityId == null) return;
            comparedEntityId = null;
            Touch();
        }

        public void SetFilter(string value)
        {
            value = value ?? string.Empty;
            if (filterText == value) return;
            filterText = value;
            Touch();
        }

        public void SetTimeRange(InsightTimeRange value)
        {
            if (timeRange.Equals(value)) return;
            timeRange = value;
            Touch();
        }

        public bool MatchesFilter(InsightEntity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(filterText)) return true;
            StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            return (entity.Label ?? string.Empty).IndexOf(filterText, comparison) >= 0 ||
                (entity.Id ?? string.Empty).IndexOf(filterText, comparison) >= 0 ||
                (entity.Category ?? string.Empty).IndexOf(filterText, comparison) >= 0;
        }

        public InsightDisclosure DisclosureFor(InsightEntity entity)
        {
            if (entity == null) return InsightDisclosure.Unknown();
            InsightDisclosure value;
            if (disclosureCache.TryGetValue(entity.Id, out value)) return value;
            value = DisclosureProvider?.ForEntity(entity) ?? InsightDisclosure.Unknown();
            disclosureCache[entity.Id] = value;
            return value;
        }

        /// <summary>Invalidates cached disclosure after a provider changes its internal level.</summary>
        public void NotifyDisclosureChanged()
        {
            disclosureCache.Clear();
            Touch();
        }

        private void Touch()
        {
            Revision++;
            Changed?.Invoke();
        }
    }
}
