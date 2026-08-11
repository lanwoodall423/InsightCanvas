using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InsightCanvas
{
    /// <summary>Viewport transform calculated by the graph's Fit All action.</summary>
    internal struct InsightGraphFit
    {
        internal InsightGraphFit(float zoom, InsightPoint pan)
        {
            Zoom = zoom;
            Pan = pan;
        }

        internal float Zoom;
        internal InsightPoint Pan;
    }

    /// <summary>Unity-free graph viewport fitting math.</summary>
    internal static class InsightGraphViewport
    {
        internal static InsightGraphFit Fit(InsightGraphLayoutResult layout, float width, float height, float margin)
        {
            width = Math.Max(1f, width);
            height = Math.Max(1f, height);
            margin = Math.Max(0f, margin);
            if (layout == null || layout.ActiveNodeCount == 0) return new InsightGraphFit(1f, new InsightPoint());

            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            for (int i = 0; i < layout.ActiveNodeIds.Count; i++)
            {
                InsightPoint point = layout.Position(layout.ActiveNodeIds[i]);
                minimumX = Math.Min(minimumX, point.X);
                minimumY = Math.Min(minimumY, point.Y);
                maximumX = Math.Max(maximumX, point.X);
                maximumY = Math.Max(maximumY, point.Y);
            }

            float spanX = Math.Max(1f, maximumX - minimumX);
            float spanY = Math.Max(1f, maximumY - minimumY);
            float availableWidth = Math.Max(1f, width - margin * 2f);
            float availableHeight = Math.Max(1f, height - margin * 2f);
            float zoom = Math.Min(availableWidth / spanX, availableHeight / spanY);
            if (float.IsNaN(zoom) || float.IsInfinity(zoom)) zoom = 1f;
            zoom = Math.Max(0.25f, Math.Min(2.8f, zoom));

            InsightPoint boundsCenter = new InsightPoint((minimumX + maximumX) * 0.5f, (minimumY + maximumY) * 0.5f);
            InsightPoint viewportCenter = new InsightPoint(width * 0.5f, height * 0.5f);
            return new InsightGraphFit(zoom, new InsightPoint(
                (viewportCenter.X - boundsCenter.X) * zoom,
                (viewportCenter.Y - boundsCenter.Y) * zoom));
        }
    }

    /// <summary>Deterministic graph positions and layout metadata.</summary>
    public sealed class InsightGraphLayoutResult
    {
        private readonly IReadOnlyDictionary<string, InsightPoint> positions;
        private readonly IReadOnlyList<string> activeNodeIds;
        private readonly IReadOnlyList<InsightRelation> edges;

        internal InsightGraphLayoutResult(IReadOnlyDictionary<string, InsightPoint> positions,
            IReadOnlyList<string> activeNodeIds, IReadOnlyList<InsightRelation> edges, bool complete, int iterations)
        {
            this.positions = positions;
            this.activeNodeIds = activeNodeIds;
            this.edges = edges;
            Complete = complete;
            Iterations = iterations;
        }

        public IReadOnlyDictionary<string, InsightPoint> Positions => positions;
        public IReadOnlyList<string> ActiveNodeIds => activeNodeIds;
        public IReadOnlyList<InsightRelation> Edges => edges;
        public int ActiveNodeCount => activeNodeIds.Count;
        public int ActiveEdgeCount => edges.Count;
        public bool Complete { get; private set; }
        public int Iterations { get; private set; }

        public InsightPoint Position(string entityId)
        {
            InsightPoint value;
            return entityId != null && positions.TryGetValue(entityId, out value) ? value : new InsightPoint(0f, 0f);
        }

        public bool ContainsNode(string entityId) => entityId != null && positions.ContainsKey(entityId);

        public bool AreNeighbors(string leftId, string rightId)
        {
            if (leftId == null || rightId == null) return false;
            for (int i = 0; i < edges.Count; i++)
            {
                InsightRelation relation = edges[i];
                if (relation.FromId == leftId && relation.ToId == rightId ||
                    relation.FromId == rightId && relation.ToId == leftId) return true;
            }
            return false;
        }

        internal void UpdateProgress(bool complete, int iterations)
        {
            Complete = complete;
            Iterations = iterations;
        }
    }

    /// <summary>Incremental, snapshot-only layout session suitable for a frame budget.</summary>
    public sealed class InsightGraphLayoutSession
    {
        private Dictionary<string, InsightPoint> positions = new Dictionary<string, InsightPoint>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> nodeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<InsightEntity> nodes = new List<InsightEntity>();
        private readonly List<InsightPoint> pointValues = new List<InsightPoint>();
        private readonly List<InsightPoint> delta = new List<InsightPoint>();
        private readonly List<InsightRelation> edges = new List<InsightRelation>();
        private readonly List<string> activeNodeIds = new List<string>();
        private readonly List<List<InsightGraphNeighbor>> adjacency = new List<List<InsightGraphNeighbor>>();
        private IReadOnlyDictionary<string, InsightPoint> positionView;
        private IReadOnlyList<string> activeNodeView = new string[0];
        private IReadOnlyList<InsightRelation> edgeView = new InsightRelation[0];
        private InsightModelSnapshot snapshot;
        private float width;
        private float height;
        private int iterations;
        private bool complete;
        private bool resultDirty = true;
        private InsightGraphLayoutResult cachedResult;
        private InsightGraphLayoutResult liveResult;

        public InsightGraphLayoutSession()
        {
            positionView = new ReadOnlyDictionary<string, InsightPoint>(positions);
        }

        public int ActiveNodeCount => nodes.Count;
        public int ActiveEdgeCount => edges.Count;

        public void Begin(InsightModelSnapshot value, float targetWidth, float targetHeight)
        {
            Begin(value, targetWidth, targetHeight, int.MaxValue, int.MaxValue, null);
        }

        public void Begin(InsightModelSnapshot value, float targetWidth, float targetHeight, int nodeBudget, int edgeBudget)
        {
            Begin(value, targetWidth, targetHeight, nodeBudget, edgeBudget, null);
        }

        public void Begin(InsightModelSnapshot value, float targetWidth, float targetHeight, int nodeBudget, int edgeBudget,
            Func<InsightEntity, bool> nodeFilter)
        {
            snapshot = value;
            width = Math.Max(1f, targetWidth);
            height = Math.Max(1f, targetHeight);
            iterations = 0;
            complete = value == null;
            resultDirty = true;
            cachedResult = null;
            liveResult = null;
            positions = new Dictionary<string, InsightPoint>(StringComparer.Ordinal);
            positionView = new ReadOnlyDictionary<string, InsightPoint>(positions);
            nodeIndices.Clear();
            nodes.Clear();
            pointValues.Clear();
            delta.Clear();
            edges.Clear();
            activeNodeIds.Clear();
            for (int i = 0; i < adjacency.Count; i++) adjacency[i].Clear();
            nodeBudget = Math.Max(0, nodeBudget);
            edgeBudget = Math.Max(0, edgeBudget);
            if (value == null)
            {
                UpdateReadOnlyViews();
                return;
            }

            for (int i = 0; i < value.Entities.Count; i++)
            {
                InsightEntity entity = value.Entities[i];
                if (nodeFilter == null || nodeFilter(entity)) nodes.Add(entity);
            }
            nodes.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            if (nodes.Count > nodeBudget) nodes.RemoveRange(nodeBudget, nodes.Count - nodeBudget);
            while (adjacency.Count < nodes.Count) adjacency.Add(new List<InsightGraphNeighbor>());
            for (int i = 0; i < nodes.Count; i++)
            {
                InsightEntity node = nodes[i];
                nodeIndices[node.Id] = i;
                activeNodeIds.Add(node.Id);
                InsightPoint point = node.ManualPosition ?? InitialPosition(node.Id, i, nodes.Count, width, height);
                pointValues.Add(point);
                delta.Add(new InsightPoint());
                positions[node.Id] = point;
            }

            for (int i = 0; i < value.Relations.Count && edges.Count < edgeBudget; i++)
            {
                InsightRelation relation = value.Relations[i];
                int fromIndex;
                int toIndex;
                if (!nodeIndices.TryGetValue(relation.FromId, out fromIndex) ||
                    !nodeIndices.TryGetValue(relation.ToId, out toIndex)) continue;
                edges.Add(relation);
                adjacency[fromIndex].Add(new InsightGraphNeighbor(toIndex, relation));
                if (fromIndex != toIndex) adjacency[toIndex].Add(new InsightGraphNeighbor(fromIndex, relation));
            }
            UpdateReadOnlyViews();
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
                new Dictionary<string, InsightPoint>(positions, StringComparer.Ordinal)), activeNodeView, edgeView, complete, iterations);
            resultDirty = false;
            return cachedResult;
        }

        internal InsightGraphLayoutResult LiveResult()
        {
            if (liveResult == null)
                liveResult = new InsightGraphLayoutResult(positionView, activeNodeView, edgeView, complete, iterations);
            else
                liveResult.UpdateProgress(complete, iterations);
            return liveResult;
        }

        private void Relax()
        {
            float ideal = Math.Max(24f, (float)Math.Sqrt(width * height / Math.Max(1, nodes.Count)) * 0.72f);
            for (int i = 0; i < nodes.Count; i++)
            {
                InsightEntity left = nodes[i];
                InsightPoint leftPosition = pointValues[i];
                float forceX = 0f;
                float forceY = 0f;
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    InsightEntity right = nodes[j];
                    InsightPoint rightPosition = pointValues[j];
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
                List<InsightGraphNeighbor> neighbors = adjacency[i];
                for (int edge = 0; edge < neighbors.Count; edge++)
                {
                    InsightGraphNeighbor neighbor = neighbors[edge];
                    InsightPoint other = pointValues[neighbor.Index];
                    float dx = other.X - leftPosition.X;
                    float dy = other.Y - leftPosition.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < 0.01f) distance = 0.01f;
                    float pull = (distance - ideal) * 0.018f * Math.Max(0.1f, Math.Abs(neighbor.Relation.Weight));
                    forceX += dx / distance * pull;
                    forceY += dy / distance * pull;
                }
                delta[i] = new InsightPoint(forceX * 0.34f, forceY * 0.34f);
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].ManualPosition.HasValue)
                {
                    pointValues[i] = nodes[i].ManualPosition.Value;
                    positions[nodes[i].Id] = pointValues[i];
                    continue;
                }
                InsightPoint current = pointValues[i];
                InsightPoint movement = delta[i];
                InsightPoint next = new InsightPoint(Clamp(current.X + movement.X, 18f, width - 18f),
                    Clamp(current.Y + movement.Y, 18f, height - 18f));
                pointValues[i] = next;
                positions[nodes[i].Id] = next;
            }
        }

        private void UpdateReadOnlyViews()
        {
            activeNodeView = new ReadOnlyCollection<string>(new List<string>(activeNodeIds));
            edgeView = new ReadOnlyCollection<InsightRelation>(new List<InsightRelation>(edges));
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

        private struct InsightGraphNeighbor
        {
            internal readonly int Index;
            internal readonly InsightRelation Relation;

            internal InsightGraphNeighbor(int index, InsightRelation relation)
            {
                Index = index;
                Relation = relation;
            }
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

        /// <summary>Computes a layout over only the requested active node and edge budgets.</summary>
        public static InsightGraphLayoutResult Compute(InsightModelSnapshot snapshot, float width, float height,
            int nodeBudget, int edgeBudget, int iterations = 14)
        {
            InsightGraphLayoutSession session = new InsightGraphLayoutSession();
            session.Begin(snapshot, width, height, nodeBudget, edgeBudget);
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
