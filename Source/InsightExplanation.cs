using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace InsightCanvas
{
    /// <summary>Semantic step in a value derivation.</summary>
    public enum InsightExplanationSegmentKind
    {
        Base,
        Factor,
        Additive,
        Clamp,
        Requirement,
        Uncertainty,
        Final
    }

    /// <summary>A calculated waterfall segment. Before/After are suitable for either bars or text.</summary>
    public sealed class InsightExplanationSegment
    {
        public string Label { get; private set; }
        public InsightExplanationSegmentKind Kind { get; private set; }
        public float Before { get; private set; }
        public float After { get; private set; }
        public float Amount { get; private set; }
        public float Confidence { get; private set; }
        public bool Known { get; private set; }
        public bool RequirementMet { get; private set; }
        public InsightRange Range { get; private set; }
        public bool HasRange { get; private set; }

        internal InsightExplanationSegment(string label, InsightExplanationSegmentKind kind, float before, float after,
            float amount, float confidence, bool known, bool requirementMet, InsightRange range, bool hasRange)
        {
            Label = label ?? string.Empty;
            Kind = kind;
            Before = before;
            After = after;
            Amount = amount;
            Confidence = confidence < 0f ? 0f : confidence > 1f ? 1f : confidence;
            Known = known;
            RequirementMet = requirementMet;
            Range = range;
            HasRange = hasRange;
        }
    }

    /// <summary>Calculated explanation data and a generated plain-language summary.</summary>
    public sealed class InsightExplanationResult
    {
        private readonly ReadOnlyCollection<InsightExplanationSegment> segments;

        internal InsightExplanationResult(float declaredFinalValue, float computedValue, List<InsightExplanationSegment> segments, string summary)
        {
            DeclaredFinalValue = declaredFinalValue;
            ComputedValue = computedValue;
            this.segments = new ReadOnlyCollection<InsightExplanationSegment>(segments);
            Summary = summary ?? string.Empty;
        }

        public float DeclaredFinalValue { get; private set; }
        public float ComputedValue { get; private set; }
        public float FinalValue => float.IsNaN(DeclaredFinalValue) ? ComputedValue : DeclaredFinalValue;
        public bool HasComputationMismatch => !float.IsNaN(DeclaredFinalValue) && Math.Abs(DeclaredFinalValue - ComputedValue) > 0.0005f;
        public IReadOnlyList<InsightExplanationSegment> Segments => segments;
        public string Summary { get; private set; }
    }

    /// <summary>Structured derivation built through Explain.Value.</summary>
    public sealed class InsightExplanation
    {
        private readonly List<Operation> operations = new List<Operation>();
        private readonly float declaredFinalValue;
        private readonly string label;
        private float baseValue;
        private bool hasBase;

        internal InsightExplanation(string label, float finalValue)
        {
            this.label = label ?? string.Empty;
            declaredFinalValue = finalValue;
        }

        /// <summary>Display label for this explanation.</summary>
        public string Label => label;

        internal float DeclaredFinalValue => declaredFinalValue;
        internal bool HasBase => hasBase;
        internal float BaseValue => baseValue;

        internal IReadOnlyList<InsightExplanationOperationData> SerializationOperations()
        {
            List<InsightExplanationOperationData> result = new List<InsightExplanationOperationData>(operations.Count);
            for (int i = 0; i < operations.Count; i++)
            {
                Operation operation = operations[i];
                result.Add(new InsightExplanationOperationData(operation.Kind, operation.Label, operation.Amount,
                    operation.Confidence, operation.Known, operation.RequirementMet, operation.Range,
                    operation.Kind == InsightExplanationSegmentKind.Clamp || operation.Kind == InsightExplanationSegmentKind.Uncertainty));
            }
            return result;
        }

        internal InsightExplanation Clone()
        {
            InsightExplanation copy = new InsightExplanation(label, declaredFinalValue)
            {
                baseValue = baseValue,
                hasBase = hasBase
            };
            for (int i = 0; i < operations.Count; i++)
            {
                Operation source = operations[i];
                copy.operations.Add(new Operation
                {
                    Kind = source.Kind,
                    Label = source.Label,
                    Amount = source.Amount,
                    Confidence = source.Confidence,
                    Known = source.Known,
                    RequirementMet = source.RequirementMet,
                    Range = source.Range
                });
            }
            return copy;
        }

        /// <summary>Sets the starting value.</summary>
        public InsightExplanation Base(float value)
        {
            baseValue = value;
            hasBase = true;
            return this;
        }

        /// <summary>Multiplies the running value by a named factor.</summary>
        public InsightExplanation Factor(string factorLabel, float multiplier, float confidence = 1f, bool known = true)
        {
            operations.Add(Operation.Factor(factorLabel, multiplier, confidence, known));
            return this;
        }

        /// <summary>Adds a named additive adjustment.</summary>
        public InsightExplanation Add(string factorLabel, float amount, float confidence = 1f, bool known = true)
        {
            operations.Add(Operation.Add(factorLabel, amount, confidence, known));
            return this;
        }

        /// <summary>Clamps the running value and records the reason as a cap or floor.</summary>
        public InsightExplanation Clamp(string reason, float minimum, float maximum)
        {
            operations.Add(Operation.Clamp(reason, minimum, maximum));
            return this;
        }

        /// <summary>Records a requirement without hiding the rest of the derivation.</summary>
        public InsightExplanation Requirement(string requirementLabel, bool met, string detail = null)
        {
            operations.Add(Operation.Requirement(requirementLabel, met, detail));
            return this;
        }

        /// <summary>Records an uncertain result range and confidence.</summary>
        public InsightExplanation Uncertain(float minimum, float maximum, float confidence, string reason = null)
        {
            operations.Add(Operation.Uncertain(new InsightRange(minimum, maximum), confidence, reason));
            return this;
        }

        /// <summary>Calculates the waterfall and summary without touching game state.</summary>
        public InsightExplanationResult Calculate()
        {
            float current = hasBase ? baseValue : 0f;
            List<InsightExplanationSegment> segments = new List<InsightExplanationSegment>();
            if (hasBase)
                segments.Add(new InsightExplanationSegment("Base", InsightExplanationSegmentKind.Base, 0f, current, current, 1f, true, true, new InsightRange(current, current), false));
            for (int i = 0; i < operations.Count; i++)
            {
                Operation operation = operations[i];
                float before = current;
                switch (operation.Kind)
                {
                    case InsightExplanationSegmentKind.Factor:
                        current *= operation.Amount;
                        segments.Add(new InsightExplanationSegment(operation.Label, operation.Kind, before, current,
                            operation.Amount, operation.Confidence, operation.Known, true, default(InsightRange), false));
                        break;
                    case InsightExplanationSegmentKind.Additive:
                        current += operation.Amount;
                        segments.Add(new InsightExplanationSegment(operation.Label, operation.Kind, before, current,
                            operation.Amount, operation.Confidence, operation.Known, true, default(InsightRange), false));
                        break;
                    case InsightExplanationSegmentKind.Clamp:
                        current = Math.Max(operation.Range.Minimum, Math.Min(operation.Range.Maximum, current));
                        segments.Add(new InsightExplanationSegment(operation.Label, operation.Kind, before, current,
                            current - before, 1f, true, true, operation.Range, true));
                        break;
                    case InsightExplanationSegmentKind.Requirement:
                        segments.Add(new InsightExplanationSegment(operation.Label, operation.Kind, before, before, 0f,
                            1f, true, operation.RequirementMet, default(InsightRange), false));
                        break;
                    case InsightExplanationSegmentKind.Uncertainty:
                        segments.Add(new InsightExplanationSegment(operation.Label, operation.Kind, before, before, 0f,
                            operation.Confidence, operation.Known, true, operation.Range, true));
                        break;
                }
            }
            if (!float.IsNaN(declaredFinalValue))
                segments.Add(new InsightExplanationSegment("Result", InsightExplanationSegmentKind.Final, current, declaredFinalValue,
                    declaredFinalValue - current, 1f, true, true, default(InsightRange), false));
            return new InsightExplanationResult(declaredFinalValue, current, segments, BuildSummary(segments, declaredFinalValue, current));
        }

        private string BuildSummary(List<InsightExplanationSegment> segments, float finalValue, float computedValue)
        {
            StringBuilder summary = new StringBuilder();
            summary.Append(label);
            summary.Append(" starts at ");
            summary.Append(Format(hasBase ? baseValue : 0f));
            for (int i = 0; i < segments.Count; i++)
            {
                InsightExplanationSegment segment = segments[i];
                if (segment.Kind == InsightExplanationSegmentKind.Base || segment.Kind == InsightExplanationSegmentKind.Final) continue;
                summary.Append(", ");
                if (segment.Kind == InsightExplanationSegmentKind.Factor)
                    summary.Append(segment.Amount >= 1f ? "improved by " : "reduced by ").Append(segment.Label);
                else if (segment.Kind == InsightExplanationSegmentKind.Additive)
                    summary.Append(segment.Amount >= 0f ? "added by " : "penalized by ").Append(segment.Label);
                else if (segment.Kind == InsightExplanationSegmentKind.Clamp)
                    summary.Append("bounded by ").Append(segment.Label);
                else if (segment.Kind == InsightExplanationSegmentKind.Requirement)
                    summary.Append(segment.RequirementMet ? "met requirement " : "blocked by requirement ").Append(segment.Label);
                else if (segment.Kind == InsightExplanationSegmentKind.Uncertainty)
                    summary.Append("remains uncertain because of ").Append(segment.Label);
            }
            summary.Append("; expected result ").Append(Format(float.IsNaN(finalValue) ? computedValue : finalValue)).Append(".");
            return summary.ToString();
        }

        private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private sealed class Operation
        {
            public InsightExplanationSegmentKind Kind;
            public string Label;
            public float Amount;
            public float Confidence;
            public bool Known;
            public bool RequirementMet;
            public InsightRange Range;

            public static Operation Factor(string label, float amount, float confidence, bool known) =>
                new Operation { Kind = InsightExplanationSegmentKind.Factor, Label = label, Amount = amount, Confidence = confidence, Known = known };

            public static Operation Add(string label, float amount, float confidence, bool known) =>
                new Operation { Kind = InsightExplanationSegmentKind.Additive, Label = label, Amount = amount, Confidence = confidence, Known = known };

            public static Operation Clamp(string label, float minimum, float maximum) =>
                new Operation { Kind = InsightExplanationSegmentKind.Clamp, Label = label, Range = new InsightRange(minimum, maximum) };

            public static Operation Requirement(string label, bool met, string detail) =>
                new Operation { Kind = InsightExplanationSegmentKind.Requirement, Label = string.IsNullOrWhiteSpace(detail) ? label : label + ": " + detail, RequirementMet = met };

            public static Operation Uncertain(InsightRange range, float confidence, string reason) =>
                new Operation { Kind = InsightExplanationSegmentKind.Uncertainty, Label = string.IsNullOrWhiteSpace(reason) ? "unknown information" : reason, Range = range, Confidence = confidence, Known = false };
        }
    }

    internal sealed class InsightExplanationOperationData
    {
        internal readonly InsightExplanationSegmentKind Kind;
        internal readonly string Label;
        internal readonly float Amount;
        internal readonly float Confidence;
        internal readonly bool Known;
        internal readonly bool RequirementMet;
        internal readonly InsightRange Range;
        internal readonly bool HasRange;

        internal InsightExplanationOperationData(InsightExplanationSegmentKind kind, string label, float amount,
            float confidence, bool known, bool requirementMet, InsightRange range, bool hasRange)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Amount = amount;
            Confidence = confidence;
            Known = known;
            RequirementMet = requirementMet;
            Range = range;
            HasRange = hasRange;
        }
    }

    /// <summary>Entry point for readable value derivations.</summary>
    public static class Explain
    {
        public static InsightExplanation Value(string label, float finalValue) => new InsightExplanation(label, finalValue);
    }
}
