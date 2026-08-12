using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Optional deterministic semantic dataset used by the Feature Showcase advanced-widget page.</summary>
    public static class InsightShowcaseData
    {
        /// <summary>Builds a stable ecology and lineage dataset without querying a map during repaint.</summary>
        public static InsightModel CreateDemoModel()
        {
            InsightModel model = InsightModel.Create("Feature Showcase Semantic Data");
            InsightEntity river = new InsightEntity("habitat:river", "Ashwater River", "A cold, fast freshwater habitat", "Habitat",
                badges: new[] { "freshwater", "seasonal" }, manualPosition: new InsightPoint(420f, 160f));
            InsightEntity trout = new InsightEntity("species:trout", "Silver Trout", "Migratory fish", "Species",
                badges: new[] { "prey", "migratory" }, manualPosition: new InsightPoint(250f, 90f));
            InsightEntity pike = new InsightEntity("species:pike", "Marsh Pike", "Ambush predator", "Species",
                badges: new[] { "predator" }, manualPosition: new InsightPoint(590f, 92f));
            InsightEntity reeds = new InsightEntity("plant:reeds", "River Reeds", "Spawning cover", "Plant",
                badges: new[] { "shelter" }, manualPosition: new InsightPoint(250f, 245f));
            InsightEntity otter = new InsightEntity("animal:otter", "River Otter", "A clever shoreline hunter", "Animal",
                badges: new[] { "threat", "social" }, manualPosition: new InsightPoint(590f, 245f));
            InsightEntity lure = new InsightEntity("tool:lure", "Amber Lure", "Colony fishing tool", "Technology",
                badges: new[] { "crafted" }, manualPosition: new InsightPoint(420f, 310f));
            InsightEntity fieldStudy = new InsightEntity("study:ecology", "Ashwater Survey", "A growing field study", "Knowledge",
                badges: new[] { "in progress" }, manualPosition: new InsightPoint(420f, 420f));
            InsightEntity researcher = new InsightEntity("pawn:marin", "Marin", "Field researcher", "Pawn",
                badges: new[] { "animals 8", "curious" }, manualPosition: new InsightPoint(120f, 380f));
            InsightEntity spawning = new InsightEntity("location:spawning", "North spawning shelf", "A likely seasonal location", "Location",
                badges: new[] { "approximate" }, manualPosition: new InsightPoint(730f, 380f));
            InsightEntity winter = new InsightEntity("season:winter", "Winter runoff", "A pressure on the food web", "Condition",
                badges: new[] { "warning" }, manualPosition: new InsightPoint(420f, 520f));
            model.Entity(river).Entity(trout).Entity(pike).Entity(reeds).Entity(otter).Entity(lure).Entity(fieldStudy)
                .Entity(researcher).Entity(spawning).Entity(winter);
            model.Relation(river.Id, trout.Id, "contains", 1.2f, false)
                .Relation(river.Id, reeds.Id, "supports", 0.9f, true)
                .Relation(reeds.Id, trout.Id, "shelters", 1.1f)
                .Relation(trout.Id, pike.Id, "threatens", 0.8f)
                .Relation(trout.Id, otter.Id, "feeds", 0.72f)
                .Relation(lure.Id, trout.Id, "attracts", 1.15f)
                .Relation(researcher.Id, fieldStudy.Id, "studies", 1f)
                .Relation(fieldStudy.Id, spawning.Id, "reveals", 0.7f, true, 0.68f, true)
                .Relation(winter.Id, river.Id, "changes", 0.45f, true, 0.58f, true)
                .Relation(otter.Id, pike.Id, "competes", 0.52f, false, 0.58f, false);
            model.Metric(trout.Id, "Population", new InsightMetric("Population", 38f, new InsightRange(24f, 54f), true, true, 0.86f,
                20f, InsightTrend.Falling, new[] { new InsightSample(1000, 52f), new InsightSample(1800, 46f), new InsightSample(2600, 38f) }));
            model.Metric(trout.Id, "Catch chance", new InsightMetric("Catch chance", 0.73f, new InsightRange(0.55f, 0.86f), true, true, 0.78f,
                0.6f, InsightTrend.Rising, new[] { new InsightSample(1000, 0.42f), new InsightSample(1800, 0.61f), new InsightSample(2600, 0.73f) }));
            model.Metric(pike.Id, "Pressure", new InsightMetric("Pressure", 0.64f, new InsightRange(0.48f, 0.75f), true, true, 0.72f, 0.7f, InsightTrend.Rising));
            model.Metric(otter.Id, "Confidence", InsightMetric.Unknown("Confidence", new InsightRange(0.35f, 0.76f), 0.54f));
            model.Metric(fieldStudy.Id, "Completion", new InsightMetric("Completion", 0.58f, new InsightRange(0f, 1f), true, true, 0.92f, 0.75f, InsightTrend.Rising,
                new[] { new InsightSample(1000, 0.12f), new InsightSample(1800, 0.41f), new InsightSample(2600, 0.58f) }));
            model.Metric(river.Id, "Water clarity", new InsightMetric("Water clarity", 0.68f, new InsightRange(0.4f, 0.82f), true, true, 0.7f, null, InsightTrend.Volatile));
            InsightExplanation troutExplanation = Explain.Value("Catch chance", 0.73f).Base(0.5f)
                .Factor("Knowledge", 1.24f)
                .Factor("Expertise", 1.15f)
                .Factor("Lure mismatch", 0.7f)
                .Clamp("Population scarcity", 0.12f, 0.73f)
                .Requirement("Lure available", true)
                .Uncertain(0.62f, 0.82f, 0.78f, "unseen movement");
            model.Explanation(trout.Id, troutExplanation);
            model.Explanation(fieldStudy.Id, Explain.Value("Study progress", 0.58f).Base(0.18f).Factor("Marin's field skill", 1.8f)
                .Add("seasonal access", 0.04f).Clamp("survey scope", 0f, 1f).Requirement("sample recorded", true));
            AddActions(model, trout, spawning, river);
            AddActions(model, pike, spawning, river);
            AddActions(model, river, spawning, river);
            AddActions(model, fieldStudy, spawning, river);
            model.Event(new InsightEvent("event:arrival", 1000, "Survey party reached the river", "Discovery", new[] { researcher.Id, river.Id }, 0.4f));
            model.Event(new InsightEvent("event:spawn", 1320, "Spawning shelf observed", "Observation", new[] { trout.Id, spawning.Id }, 0.55f));
            model.Event(new InsightEvent("event:catch", 1680, "A silver trout was caught", "Outcome", new[] { trout.Id, lure.Id }, 0.85f));
            model.Event(new InsightEvent("event:pressure", 2020, "Pike pressure increased", "Threat", new[] { pike.Id, trout.Id }, 0.72f));
            model.Event(new InsightEvent("event:study", 2380, "Study connected reeds to spawning", "Explanation", new[] { fieldStudy.Id, reeds.Id, trout.Id }, 0.62f));
            model.Event(new InsightEvent("event:runoff", 2700, "Winter runoff changed the river", "Condition", new[] { winter.Id, river.Id }, 0.58f));
            model.Event(new InsightEvent("event:unknown", 2920, "An unconfirmed movement was recorded", "Uncertainty", new[] { otter.Id }, 0.28f, false));
            return model;
        }

        /// <summary>Creates a bounded deterministic dataset for graph and timeline stress checks.</summary>
        public static InsightModel CreateStressModel(int nodeCount, int eventCount)
        {
            nodeCount = Mathf.Clamp(nodeCount, 1, 1000);
            eventCount = Mathf.Clamp(eventCount, 0, 5000);
            InsightModel model = InsightModel.Create("Insight Canvas Stress Dataset");
            for (int i = 0; i < nodeCount; i++)
                model.Entity(new InsightEntity("stress:" + i, "Stress node " + i, "Synthetic relationship node", i % 3 == 0 ? "Cluster A" : "Cluster B",
                    badges: i % 11 == 0 ? new[] { "uncertain" } : null));
            for (int i = 0; i < nodeCount; i++)
            {
                model.Relation("stress:" + i, "stress:" + ((i * 7 + 3) % nodeCount), i % 2 == 0 ? "depends" : "influences",
                    0.4f + (i % 7) * 0.12f, true, (i % 10) / 10f, i % 13 != 0);
                if (i % 5 == 0) model.Relation("stress:" + i, "stress:" + ((i + 1) % nodeCount), "cycles", 0.7f, false, 0.62f);
                model.Metric("stress:" + i, "Signal", new InsightMetric("Signal", (i % 100) / 100f, new InsightRange(0f, 1f), true,
                    i % 13 != 0, 0.5f + (i % 5) * 0.1f, 0.5f, i % 2 == 0 ? InsightTrend.Rising : InsightTrend.Falling));
            }
            for (int i = 0; i < eventCount; i++)
                model.Event(new InsightEvent("stress-event:" + i, i * 17L, i % 9 == 0 ? "Clustered event" : "Synthetic event",
                    i % 2 == 0 ? "Signal" : "Change", new[] { "stress:" + (i % nodeCount) }, (i % 10) / 10f, i % 17 != 0));
            return model;
        }

        private static void AddActions(InsightModel model, InsightEntity entity, InsightEntity cellEntity, InsightEntity mapEntity)
        {
            model.Action(entity.Id, new InsightAction("focus-" + entity.Id, "InsightCanvas_Focus".Translate(), () => PreviewMapLink(entity.Label), true,
                "InsightCanvas_FocusTip".Translate()));
            model.Action(entity.Id, new InsightAction("flash-" + entity.Id, "InsightCanvas_PreviewMap".Translate(), () => PreviewMapLink(cellEntity.Label), true));
            model.Action(entity.Id, new InsightAction("compare-" + entity.Id, "InsightCanvas_Compare".Translate(), () =>
                Messages.Message("Insight Canvas: compare mode is shared through the current selection context.", MessageTypeDefOf.NeutralEvent, false)));
        }

        private static void PreviewMapLink(string label)
        {
            if (Find.CurrentMap != null)
            {
                IntVec3 center = Find.CurrentMap.Center;
                InsightMapReference reference = InsightMapBridge.ForCell(Find.CurrentMap, center);
                InsightMapBridge.Focus("showcase-focus", reference).Invoke();
                InsightMapBridge.Flash("showcase-flash", reference).Invoke();
                Messages.Message("Insight Canvas: previewing " + label + " near the current map center.", MessageTypeDefOf.NeutralEvent, false);
            }
            else Messages.Message("Insight Canvas: mock map link for " + label + " (open a map to focus a real target).", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
