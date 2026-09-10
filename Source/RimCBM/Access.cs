using System;
using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public class CBMCompatibilityRule
    {
        public bool installationOnly;
        public float minBodySize, maxBodySize = float.MaxValue;
        public List<string> requiredAllTags = new List<string>(), requiredAnyTags = new List<string>(), forbiddenTags = new List<string>();
        public bool Matches(Pawn pawn, HashSet<string> tags) => pawn.BodySize >= minBodySize && pawn.BodySize <= maxBodySize
            && TagQuery.Matches(tags, requiredAllTags, requiredAnyTags, forbiddenTags);
    }
    // Assign host tags through data without treating them as socket capabilities or implant identity tags.
    public class CBMHostTagDef : Def
    {
        public List<ThingDef> races = new List<ThingDef>(), apparel = new List<ThingDef>();
        public List<TraitDef> traits = new List<TraitDef>();
        public List<GeneDef> genes = new List<GeneDef>();
        public List<HediffDef> hediffs = new List<HediffDef>();
        public List<BodyPartDef> parts = new List<BodyPartDef>();
        public List<string> tags = new List<string>();
        public Type workerClass = typeof(CBMHostTagWorker);
        private CBMHostTagWorker worker;
        public CBMHostTagWorker Worker => worker ?? (worker = (CBMHostTagWorker)Activator.CreateInstance(workerClass));
        public override IEnumerable<string> ConfigErrors()
        {
            if (tags.Count == 0 || tags.Any(string.IsNullOrEmpty) || workerClass == null || workerClass.IsAbstract
                || !typeof(CBMHostTagWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid host tag provider.";
        }
    }
    public class CBMHostTagWorker
    {
        public virtual bool Matches(CBMHostTagDef def, Pawn pawn, BodyPartRecord part) => (def.races.Count == 0 || def.races.Contains(pawn.def))
            && (def.parts.Count == 0 || def.parts.Contains(part.def))
            && (def.traits.Count == 0 || pawn.story?.traits.allTraits.Any(t => def.traits.Contains(t.def)) == true)
            && (def.genes.Count == 0 || pawn.genes?.GenesListForReading.Any(g => g.Active && def.genes.Contains(g.def)) == true)
            && (def.hediffs.Count == 0 || pawn.health.hediffSet.hediffs.Any(h => def.hediffs.Contains(h.def)))
            && (def.apparel.Count == 0 || pawn.apparel?.WornApparel.Any(a => def.apparel.Contains(a.def)) == true);
        public virtual IEnumerable<string> Tags(CBMHostTagDef def, Pawn pawn, BodyPartRecord part) => def.tags;
    }
    public static class CBMAccess
    {
        public static bool Researched(IEnumerable<ResearchProjectDef> projects) => projects.All(p => p != null && p.IsFinished);
        public static HashSet<string> HostTags(Pawn pawn, BodyPartRecord part)
        {
            var tags = new HashSet<string> { "Race." + pawn.def.defName, "BodyPart." + part.def.defName, "Gender." + pawn.gender };
            bool artificial = false;
            for (var p = part; p != null; p = p.parent)
                if (pawn.health.hediffSet.hediffs.Any(h => h.Part == p && h is Hediff_AddedPart)) artificial = true;
            tags.Add(artificial ? "Part.Artificial" : "Part.Natural");
            var profile = ImplantService.Profile(pawn, part); if (profile != null) tags.UnionWith(profile.hostTags);
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                tags.Add("Hediff." + h.def.defName);
                if (h.Part == part) tags.Add("PartHediff." + h.def.defName);
            }
            if (pawn.story?.traits != null) foreach (var trait in pawn.story.traits.allTraits) { tags.Add("Trait." + trait.def.defName); tags.Add("Trait." + trait.def.defName + "." + trait.Degree); }
            if (pawn.genes != null) foreach (var g in pawn.genes.GenesListForReading.Where(g => g.Active)) tags.Add("Gene." + g.def.defName);
            if (pawn.apparel != null) foreach (var a in pawn.apparel.WornApparel) tags.Add("Apparel." + a.def.defName);
            foreach (var def in DefDatabase<CBMHostTagDef>.AllDefsListForReading)
            {
                var worker = def.Worker;
                if (worker.Matches(def, pawn, part)) tags.UnionWith(worker.Tags(def, pawn, part));
            }
            return tags;
        }
        public static RuleResult Check(Pawn pawn, BodyPartRecord part, HediffDef def, bool installation)
        {
            var ext = def.GetModExtension<CBMExtension>();
            if (installation && CBMItems.Implant(def.GetModExtension<CBMExtension>().item) != def) return RuleResult.Fail("ItemUnavailable");
            if (installation && !Researched(ext.installResearch)) return RuleResult.Fail("CBMResearchRequired");
            var rules = ext.compatibility.Where(r => installation || !r.installationOnly).ToList();
            if (part == null || pawn.health.hediffSet.PartIsMissing(part)) return RuleResult.Fail("HostIncompatible");
            if (rules.Count > 0)
            {
                var tags = HostTags(pawn, part);
                if (rules.Any(r => !r.Matches(pawn, tags))) return RuleResult.Fail("HostIncompatible");
            }
            return RuleResult.Ok();
        }
        public static bool Runtime(Hediff_CBMModule module) => Runtime(module, new HashSet<Hediff_CBMModule>());
        private static bool Runtime(Hediff_CBMModule module, HashSet<Hediff_CBMModule> path)
        {
            if (!path.Add(module)) return false;
            if (!Check(module.pawn, module.Part, module.def, false).Allowed) { path.Remove(module); return false; }
            var ext = module.def.GetModExtension<CBMExtension>();
            bool okay = ext.dependencies.All(dep => ImplantService.Modules(module.pawn).Any(other => other != module && other.HardwareReady
                && (dep.scope == LimitScope.Pawn || other.Part == module.Part)
                && (dep.modules.Contains(other.def) || dep.tags.Intersect(other.def.GetModExtension<CBMExtension>().tags).Any()) && Runtime(other, path)));
            path.Remove(module); return okay;
        }
        public static string Description(CBMExtension e)
        {
            var lines = new List<string>();
            if (e.installResearch.Count > 0) lines.Add("RC_CBMResearch".Translate() + ": " + string.Join(", ", e.installResearch.Select(r => r.LabelCap.ToString())));
            foreach (var dep in e.dependencies) lines.Add("RC_Dependencies".Translate() + " (" + dep.scope + "): " + string.Join(", ", dep.modules.Select(d => d.LabelCap.ToString()).Concat(dep.tags)));
            foreach (var rule in e.compatibility) lines.Add((rule.installationOnly ? "RC_InstallOnly" : "RC_InstallAndRun").Translate() + ": "
                + "RC_HostRule".Translate(string.Join(", ", rule.requiredAllTags), string.Join(", ", rule.requiredAnyTags), string.Join(", ", rule.forbiddenTags), rule.minBodySize, rule.maxBodySize == float.MaxValue ? "∞" : rule.maxBodySize.ToString()));
            return string.Join("\n", lines);
        }
    }
    public class CBMModeSpec
    {
        public string id, labelKey, exclusiveGroup;
        public LimitScope scope = LimitScope.Pawn;
        public List<string> abilities = new List<string>();
        public List<ResearchProjectDef> research = new List<ResearchProjectDef>();
        public float generationMultiplier = 1, consumptionMultiplier = 1, effectMultiplier = 1;
        public string Label => labelKey.NullOrEmpty() ? id : labelKey.Translate().ToString();
    }
    public static class CBMModes
    {
        public const string Off = "off";
        public static string Id(Hediff_CBMModule m) => m.modeId ?? m.def.GetModExtension<CBMExtension>().defaultMode;
        public static CBMModeSpec Current(Hediff_CBMModule m) => m.def.GetModExtension<CBMExtension>().modes.FirstOrDefault(s => s.id == Id(m));
        public static bool Running(Hediff_CBMModule m) => m.def.GetModExtension<CBMExtension>().modes.Count == 0 || Current(m) is CBMModeSpec spec && CBMAccess.Researched(spec.research)
            && !ImplantService.Modules(m.pawn).Any(other => other.loadID < m.loadID && other.HardwareReady && Current(other) is CBMModeSpec otherMode
                && CBMAccess.Researched(otherMode.research) && Conflict(m, spec, other, otherMode));
        public static bool Allows(Hediff_CBMModule m, CBMAbilitySpec a) => CBMAccess.Researched(a.research)
            && (m.def.GetModExtension<CBMExtension>().modes.Count == 0 || Running(m) && (a.id == CBMLegacyAbilities.UseId || Current(m).abilities.Contains(a.id)));
        public static float Consumption(Hediff_CBMModule m) => CBMTagEffects.Consumption(m.def) * (Current(m)?.consumptionMultiplier ?? 1);
        public static float Effect(Hediff_CBMModule m) => CBMTagEffects.Effect(m.def) * (Current(m)?.effectMultiplier ?? 1);
        public static float Generation(Hediff_CBMModule m) => Current(m)?.generationMultiplier ?? 1;
        private static bool Conflict(Hediff_CBMModule a, CBMModeSpec am, Hediff_CBMModule b, CBMModeSpec bm) => a != b && am != null && bm != null
            && !am.exclusiveGroup.NullOrEmpty() && am.exclusiveGroup == bm.exclusiveGroup
            && (am.scope == LimitScope.Pawn || bm.scope == LimitScope.Pawn || a.Part == b.Part);
        public static RuleResult CanSelect(Hediff_CBMModule m, string id)
        {
            if (!ImplantService.CanEdit(m.pawn) || !ImplantService.Modules(m.pawn).Contains(m)) return RuleResult.Fail("ReadOnly");
            if (id == Off) return RuleResult.Ok();
            var mode = m.def.GetModExtension<CBMExtension>().modes.FirstOrDefault(s => s.id == id);
            if (mode == null) return RuleResult.Fail("UnknownMode");
            if (!CBMAccess.Researched(mode.research)) return RuleResult.Fail("CBMResearchRequired");
            return m.HardwareReady && CBMAccess.Runtime(m) ? RuleResult.Ok() : RuleResult.Fail("HostIncompatible");
        }
        private static void Set(Hediff_CBMModule m, string id)
        {
            m.modeId = id; m.energyStarted = false; m.poweredUntilTick = m.effectUntilTick = 0;
            CBMAbilities.Interrupt(m); CBMStatEffects.Invalidate(m.pawn); PanelSnapshot.Invalidate();
        }
        public static RuleResult Select(Hediff_CBMModule m, string id)
        {
            var check = CanSelect(m, id); if (!check.Allowed) return check;
            var mode = m.def.GetModExtension<CBMExtension>().modes.FirstOrDefault(s => s.id == id);
            foreach (var other in ImplantService.Modules(m.pawn).ToList()) if (Conflict(m, mode, other, Current(other))) Set(other, Off);
            if (Id(m) != id) Set(m, id); return RuleResult.Ok();
        }
        public static void Reconcile(Pawn pawn)
        {
            var accepted = new List<Hediff_CBMModule>();
            foreach (var m in ImplantService.Modules(pawn).OrderBy(m => m.loadID))
            {
                if (!m.HardwareReady || Current(m) == null || !CBMAccess.Researched(Current(m).research)) continue;
                if (accepted.Any(o => Conflict(m, Current(m), o, Current(o)))) Set(m, Off); else accepted.Add(m);
            }
        }
        public static IEnumerable<FloatMenuOption> Options(Pawn pawn)
        {
            foreach (var m in ImplantService.Modules(pawn).Where(m => m.def.GetModExtension<CBMExtension>().modes.Count > 0))
                yield return new FloatMenuOption("RC_Mode".Translate() + ": " + m.def.LabelCap + " · " + m.Part.LabelCap + " · " + (Current(m)?.Label ?? "RC_Off".Translate().ToString()), () => {
                    var options = new List<FloatMenuOption> { new FloatMenuOption("RC_Off".Translate(), () => CBMTargeting.Report(Select(m, Off))) };
                    foreach (var mode in m.def.GetModExtension<CBMExtension>().modes)
                    {
                        var result = CanSelect(m, mode.id);
                        options.Add(new FloatMenuOption(mode.Label + (result.Allowed ? "" : " (" + ("RC_Error_" + result.Error).Translate().ToString() + ")"), result.Allowed ? (Action)(() => CBMTargeting.Report(Select(m, mode.id))) : null));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                });
        }
    }
}
