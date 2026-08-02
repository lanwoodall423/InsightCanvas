using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InsightCanvas
{
    /// <summary>Deterministic graph positions and layout metadata.</summary>
    public sealed class InsightGraphLayoutResult
    {
        private readonly IReadOnlyDictionary<string, InsightPoint> positions;

        internal InsightGraphLayoutResult(IReadOnlyDictionary<string, InsightPoint> positions, bool complete, int iterations)
        {
            this.positions = positions;
            Complete = complete;
            Iterations = iterations;
        }

        public IReadOnlyDictionary<string, InsightPoint> Positions => positions;
        public bool Complete { get; private set; }
        public int Iterations { get; private set; }
        public InsightPoint Position(string entityId)
        {
            InsightPoint value;
            return entityId != null && positions.TryGetValue(entityId, out value) ? value : new InsightPoint(0f, 0f);
        }
    }

    /// <summary>Incremental, snapshot-only layout session suitable for a frame budget.</summary>
    public sealed class InsightGraphLayoutSession
    {
        private readonly Dictionary<string, InsightPoint> positions = new Dictionary<string, InsightPoint>(StringComparer.Ordinal);
        private readonly List<InsightEntity> nodes = new List<InsightEntity>();
        private InsightModelSnapshot snapshot;
        private float width;
        private float height;
        private int iterations;
        private bool complete;
        private bool resultDirty = true;
        private InsightGraphLayoutResult cachedResult;

        public void Begin(InsightModelSnapshot value, float targetWidth, float targetHeight)
        {
            snapshot = value;
            width = Math.Max(1f, targetWidth);
            height = Math.Max(1f, targetHeight);
            iterations = 0;
            complete = value == null;
            resultDirty = true;
            cachedResult = null;
            positions.Clear();
            nodes.Clear();
            if (value == null) return;
            for (int i = 0; i < value.Entities.Count; i++) nodes.Add(value.Entities[i]);
            nodes.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            for (int i = 0; i < nodes.Count; i++)
            {
                InsightEntity node = nodes[i];
                positions[node.Id] = node.ManualPosition ?? InitialPosition(node.Id, i, nodes.Count, width, height);
            }
            if (nodes.Count <= 1) complete = true;
        }

        /// <summary>Runs at most the requested number of relaxation passes.</summary>
        public bool Step(int passBudget)
        {
            if (complete || snapshot == null) return true;
            int passes = passBudget < 1 ? 1 : passBudget;
            for (int pass = 0; pass < passes && !complete; pass++)
            {
                Relax();
                iterations++;
                resultDirty = true;
                if (iterations >= 14) complete = true;
            }
            return complete;
        }

        public InsightGraphLayoutResult Result()
        {
            if (!resultDirty && cachedResult != null) return cachedResult;
            cachedResult = new InsightGraphLayoutResult(new ReadOnlyDictionary<string, InsightPoint>(
                new Dictionary<string, InsightPoint>(positions, StringComparer.Ordinal)), complete, iterations);
            resultDirty = false;
            return cachedResult;
        }

        private void Relax()
        {
            Dictionary<string, InsightPoint> delta = new Dictionary<string, InsightPoint>(StringComparer.Ordinal);
            float ideal = Math.Max(24f, (float)Math.Sqrt(width * height / Math.Max(1, nodes.Count)) * 0.72f);
            for (int i = 0; i < nodes.Count; i++)
            {
                InsightEntity left = nodes[i];
                InsightPoint leftPosition = positions[left.Id];
                float forceX = 0f;
                float forceY = 0f;
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    InsightEntity right = nodes[j];
                    InsightPoint rightPosition = positions[right.Id];
                    float dx = leftPosition.X - rightPosition.X;
                    float dy = leftPosition.Y - rightPosition.Y;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < 0.01f)
                    {
                        dx = ((StableHash(left.Id) & 31) - 16) * 0.01f;
                        dy = ((StableHash(right.Id) & 31) - 16) * 0.01f;
                        distanceSquared = dx * dx + dy * dy + 0.01f;
                    }
                    float repulsion = ideal * ideal / distanceSquared * 0.11f;
                    forceX += dx * repulsion;
                    forceY += dy * repulsion;
                }
                for (int edge = 0; edge < snapshot.Relations.Count; edge++)
                {
                    InsightRelation relation = snapshot.Relations[edge];
                    string otherId = relation.FromId == left.Id ? relation.ToId : relation.ToId == left.Id ? relation.FromId : null;
                    if (otherId == null || !positions.ContainsKey(otherId)) continue;
                    InsightPoint other = positions[otherId];
                    float dx = other.X - leftPosition.X;
                    float dy = other.Y - leftPosition.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < 0.01f) distance = 0.01f;
                    float pull = (distance - ideal) * 0.018f * Math.Max(0.1f, Math.Abs(relation.Weight));
                    forceX += dx / distance * pull;
                    forceY += dy / distance * pull;
                }
                delta[left.Id] = new InsightPoint(forceX * 0.34f, forceY * 0.34f);
            }
            foreach (InsightEntity node in nodes)
            {
                InsightPoint current = positions[node.Id];
                InsightPoint movement = delta[node.Id];
                positions[node.Id] = new InsightPoint(Clamp(current.X + movement.X, 18f, width - 18f),
                    Clamp(current.Y + movement.Y, 18f, height - 18f));
            }
        }

        private static InsightPoint InitialPosition(string id, int index, int count, float width, float height)
        {
            float angle = (float)(index * Math.PI * 2.0 / Math.Max(1, count)) + (StableHash(id) % 17) * 0.015f;
            float radius = Math.Min(width, height) * (count <= 4 ? 0.27f : 0.37f);
            return new InsightPoint(width * 0.5f + (float)Math.Cos(angle) * radius,
                height * 0.5f + (float)Math.Sin(angle) * radius);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (maximum < minimum) return minimum;
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }

    /// <summary>Graph layout entry point with deterministic cached-friendly results.</summary>
    public static class InsightGraphLayout
    {
        public static InsightGraphLayoutResult Compute(InsightModelSnapshot snapshot, float width, float height, int iterations = 14)
        {
            InsightGraphLayoutSession session = new InsightGraphLayoutSession();
            session.Begin(snapshot, width, height);
            session.Step(iterations < 1 ? 1 : iterations);
            return session.Result();
        }

        public static bool AreNeighbors(InsightModelSnapshot snapshot, string leftId, string rightId)
        {
            if (snapshot == null || leftId == null || rightId == null) return false;
            for (int i = 0; i < snapshot.Relations.Count; i++)
            {
                InsightRelation relation = snapshot.Relations[i];
                if (relation.FromId == leftId && relation.ToId == rightId ||
                    relation.FromId == rightId && relation.ToId == leftId) return true;
            }
            return false;
        }
    }
}
