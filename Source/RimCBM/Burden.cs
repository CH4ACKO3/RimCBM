using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public enum CBMLoadGroup { Head, Hands, Arms, Torso, Legs, Feet, Total }
    public class CBMLoadEffect
    {
        public StatDef stat;
        public float strength = 1;
        public bool increases;
    }
    public class CBMLoadProfileDef : Def
    {
        public CBMLoadGroup group;
        public List<BodyPartDef> bodyParts = new List<BodyPartDef>();
        public float freeKg;
        public SimpleCurve penaltyCurve;
        public List<CBMLoadEffect> effects = new List<CBMLoadEffect>();
        public override IEnumerable<string> ConfigErrors()
        {
            if (freeKg < 0 || float.IsNaN(freeKg) || float.IsInfinity(freeKg) || !Enum.IsDefined(typeof(CBMLoadGroup), group)) yield return "Invalid implant burden threshold/group.";
            if (penaltyCurve == null || penaltyCurve.PointsCount == 0) yield return "Missing burden curve.";
            else
            {
                float lastX = -1, lastY = -1;
                foreach (var point in penaltyCurve.Points)
                {
                    if (float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y)
                        || point.x < 0 || point.y < 0 || point.x <= lastX || point.y < lastY) yield return "Burden curve must be finite, nonnegative and monotonic.";
                    lastX = point.x; lastY = point.y;
                }
            }
            if (DefDatabase<CBMLoadProfileDef>.AllDefsListForReading.Count(p => p.group == group) > 1) yield return "Only one burden profile per group is allowed.";
            if (effects.Any(e => e.stat == null || e.strength < 0 || float.IsNaN(e.strength) || float.IsInfinity(e.strength))) yield return "Invalid burden stat effect.";
        }
    }
    public class CBMLoadStatDef : StatDef { public CBMLoadGroup group; }
    public static class CBMBurden
    {
        public static IEnumerable<CBMLoadProfileDef> Profiles => DefDatabase<CBMLoadProfileDef>.AllDefsListForReading;
        public static string Label(CBMLoadGroup group) => ("RC_Load_" + group).Translate();
        public static CBMLoadGroup Group(BodyPartRecord part)
        {
            for (var current = part; current != null; current = current.parent)
            {
                var profile = Profiles.FirstOrDefault(p => p.group != CBMLoadGroup.Total && p.bodyParts.Contains(current.def));
                if (profile != null) return profile.group;
            }
            return CBMLoadGroup.Total; // Unmapped anatomy still contributes to total, without guessing a limb.
        }
        public static float[] Loads(Pawn pawn)
        {
            var result = new float[7];
            if (pawn?.health == null) return result;
            foreach (var module in ImplantService.Modules(pawn))
            {
                float mass = module.def.GetModExtension<CBMExtension>().item.GetStatValueAbstract(StatDefOf.Mass) + CBMSupply.FuelMass(module);
                result[(int)CBMLoadGroup.Total] += mass;
                var group = Group(module.Part);
                if (group != CBMLoadGroup.Total) result[(int)group] += mass;
            }
            return result;
        }
        // Vanilla adult human: raw race Mass 60 kg and BodySize 1.
        public static float ReferenceMassKg => DefDatabase<CBMSettingsDef>.GetNamed("RimCBM_Settings").referenceMassKg;
        public static float BaseMass(Pawn pawn) => pawn?.def?.statBases.GetStatValueFromList(StatDefOf.Mass, StatDefOf.Mass.defaultBaseValue) ?? ReferenceMassKg;
        public static float Scale(float baseMass, float bodySize)
        {
            if (float.IsNaN(baseMass) || float.IsInfinity(baseMass) || float.IsNaN(bodySize) || float.IsInfinity(bodySize)) return 1;
            return (float)Math.Max(.001, Math.Min(1000000, (double)Math.Max(0, baseMass) * Math.Max(0, bodySize) / ReferenceMassKg));
        }
        public static float Scale(Pawn pawn) => pawn == null ? 1 : Scale(BaseMass(pawn), pawn.BodySize);
        public static float Penalty(CBMLoadProfileDef profile, float kg, float scale = 1)
        {
            // Scale the entire curve as well as the allowance: equal relative load means equal penalty.
            float excess = Mathf.Max(0, kg / Mathf.Max(.001f, scale) - profile.freeKg);
            return excess <= 0 ? 0 : Mathf.Max(0, profile.penaltyCurve.Evaluate(excess));
        }
        public static float Factor(Pawn pawn, StatDef stat)
        {
            if (pawn == null) return 1;
            var loads = Loads(pawn);
            float reduce = 0, increase = 0;
            foreach (var profile in Profiles)
                foreach (var effect in profile.effects.Where(e => e.stat == stat))
                {
                    float penalty = Penalty(profile, loads[(int)profile.group], Scale(pawn)) * effect.strength;
                    if (effect.increases) increase += penalty; else reduce += penalty;
                }
            return Mathf.Clamp((1 + increase) / (1 + reduce), .25f, 3f);
        }
        public static string Description(Pawn pawn)
        {
            var loads = Loads(pawn);
            var lines = Profiles.OrderBy(p => p.group).Select(p => "RC_LoadRow".Translate(Label(p.group), loads[(int)p.group].ToString("0.##"), (p.freeKg * Scale(pawn)).ToString("0.##")).ToString()).ToList();
            foreach (var stat in Profiles.SelectMany(p => p.effects).Select(e => e.stat).Distinct())
            {
                float factor = Factor(pawn, stat);
                if (Math.Abs(factor - 1) > .00001f) lines.Add(stat.LabelCap + " ×" + factor.ToString("0.###"));
            }
            lines.Insert(0, "RC_LoadBodyScale".Translate(BaseMass(pawn).ToString("0.##"), (pawn?.BodySize ?? 1).ToString("0.###"), Scale(pawn).ToString("0.###"), ReferenceMassKg.ToString("0.##")));
            lines.Add("RC_LoadHelp".Translate());
            return string.Join("\n", lines);
        }
        public static void Invalidate(Pawn pawn)
        {
            if (pawn == null) return;
            foreach (var stat in Profiles.SelectMany(p => p.effects).Select(e => e.stat).Distinct()) stat.Worker.ClearCacheForThing(pawn);
        }
    }
    public class StatWorker_CBMLoad : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true) => CBMBurden.Loads(req.Thing as Pawn)[(int)((CBMLoadStatDef)stat).group];
        public override bool ShouldShowFor(StatRequest req) => req.Thing is Pawn pawn && ImplantService.Modules(pawn).Any();
        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense) => CBMBurden.Description(req.Thing as Pawn);
    }
    public class StatPart_CBMBurden : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val) => val *= CBMBurden.Factor(req.Thing as Pawn, parentStat);
        public override string ExplanationPart(StatRequest req)
        {
            float factor = CBMBurden.Factor(req.Thing as Pawn, parentStat);
            return Math.Abs(factor - 1) < .00001f ? null : "RC_LoadPenalty".Translate() + ": ×" + factor.ToString("0.###") + "\n" + CBMBurden.Description(req.Thing as Pawn);
        }
    }
}
