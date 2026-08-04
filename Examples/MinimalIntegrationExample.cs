using InsightCanvas;
using RimWorld;
using Verse;

namespace InsightCanvasExample
{
    /// <summary>Small adapter example; keep this file outside the framework project and copy the pattern into a consumer mod.</summary>
    public static class MinimalIntegrationExample
    {
        public static void Open()
        {
            InsightEntity pawn = new InsightEntity("example:pawn", "Example pawn", category: "Pawn");
            InsightEntity target = new InsightEntity("example:target", "Example target", category: "Object");
            InsightModel model = InsightModel.Create("Example.Insight")
                .Entity(pawn)
                .Entity(target)
                .Relation(pawn.Id, target.Id, "studies")
                .Metric(pawn.Id, "Confidence", new InsightMetric("Confidence", 0.6f, new InsightRange(0.2f, 0.9f)))
                .Action(pawn.Id, "message", "Explain", () => Messages.Message("The example action ran.", MessageTypeDefOf.NeutralEvent, false));

            InsightModelValidation validation = model.Validate();
            for (int i = 0; i < validation.Errors.Count; i++) Log.Error(validation.Errors[i]);

            // This is a safe data snapshot, not a complete runtime save. The action callback is omitted,
            // and a loaded action is disabled until this mod explicitly rebinds its callback.
            InsightModelSerializationReport saved = InsightModelSerialization.SerializeWithDiagnostics(model.Snapshot());
            for (int i = 0; i < saved.Warnings.Count; i++) Log.Warning(saved.Warnings[i]);
            string xml = saved.Xml;
            Find.WindowStack.Add(new InsightWindow(model, InsightView.Create().Add(new InsightCardGrid()).Add(new InsightConstellation())));
        }
    }
}
