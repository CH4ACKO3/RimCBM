using System;
using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public class CBMTagEffectDef : Def
    {
        public string tag;
        public bool empSensitive;
        public int empDurationTicks = 600;
        public float consumptionMultiplier = 1f, effectMultiplier = 1f;
        public override IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrEmpty(tag) || empDurationTicks < 0 || consumptionMultiplier < 0 || effectMultiplier < 0
                || float.IsNaN(consumptionMultiplier) || float.IsInfinity(consumptionMultiplier)
                || float.IsNaN(effectMultiplier) || float.IsInfinity(effectMultiplier)) yield return "Invalid CBM tag effect.";
        }
    }
    public static class CBMTagEffects
    {
        private static readonly Dictionary<HediffDef, List<CBMTagEffectDef>> cache = new Dictionary<HediffDef, List<CBMTagEffectDef>>();
        public static List<CBMTagEffectDef> For(HediffDef def)
        {
            if (!cache.TryGetValue(def, out var effects))
            {
                var tags = def.GetModExtension<CBMExtension>()?.tags;
                effects = DefDatabase<CBMTagEffectDef>.AllDefsListForReading.Where(e => tags?.Contains(e.tag) == true).OrderBy(e => e.defName).ToList();
                cache[def] = effects;
            }
            return effects;
        }
        public static float Consumption(HediffDef def) => For(def).Aggregate(1f, (n, e) => n * e.consumptionMultiplier);
        public static float Effect(HediffDef def) => For(def).Aggregate(1f, (n, e) => n * e.effectMultiplier);
        public static void Invalidate() => cache.Clear();
    }
    public class CBMEnergyRecord : IExposable
    {
        public Pawn pawn;
        public float storedWd;
        public bool autoCharge, chargeRequested;
        public float chargeThreshold = .25f, chargeTarget = .9f;
        public float capacityWd, generatedWd, consumedWd;
        public void ExposeData() { Scribe_References.Look(ref pawn, "pawn"); Scribe_Values.Look(ref storedWd, "storedWd"); Scribe_Values.Look(ref autoCharge, "autoCharge"); Scribe_Values.Look(ref chargeRequested, "chargeRequested"); Scribe_Values.Look(ref chargeThreshold, "chargeThreshold", .25f); Scribe_Values.Look(ref chargeTarget, "chargeTarget", .9f); }
    }
    public class CBMEnergyComponent : GameComponent
    {
        private List<CBMEnergyRecord> records = new List<CBMEnergyRecord>();
        public CBMEnergyComponent(Game game) { }
        public CBMEnergyRecord Get(Pawn pawn)
        {
            var record = records.FirstOrDefault(r => r.pawn == pawn);
            if (record == null) { record = new CBMEnergyRecord { pawn = pawn }; records.Add(record); }
            record.capacityWd = ImplantService.Modules(pawn).Where(m => m.HardwareReady)
                .Sum(m => m.def.GetModExtension<CBMExtension>().energyCapacityWd * UnityEngine.Mathf.Clamp01(m.durability));
            record.storedWd = PowerRules.Clamp(record.storedWd, record.capacityWd);
            return record;
        }
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) records.RemoveAll(r => r?.pawn == null || r.pawn.Destroyed);
            Scribe_Collections.Look(ref records, "cbmEnergy", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { records = records ?? new List<CBMEnergyRecord>(); records.RemoveAll(r => r?.pawn == null); }
        }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList())
                if (ImplantService.Modules(pawn).Any()) Step(pawn);
        }
        public void Step(Pawn pawn)
        {
            CBMModes.Reconcile(pawn);
            var record = Get(pawn);
            record.generatedWd = record.consumedWd = 0;
            int now = Find.TickManager.TicksGame;
            var modules = ImplantService.Modules(pawn).ToList();
            foreach (var m in modules)
            {
                m.poweredUntilTick = 0;
                if (!m.CanRun) { m.energyStarted = false; continue; }
                var ext = m.def.GetModExtension<CBMExtension>();
                if (ext.generationWatts <= 0) continue;
                float generation = Math.Min(ext.generationWatts * CBMModes.Generation(m) * CompPower.WattsToWattDaysPerTick * 60f * m.durability, Math.Max(0, record.capacityWd - record.storedWd));
                if (ext.nutritionPerWd > 0)
                {
                    var food = pawn.needs?.food;
                    generation = food == null ? 0 : Math.Min(generation, Math.Max(0, food.CurLevel - food.MaxLevel * .2f) / ext.nutritionPerWd);
                }
                generation = Math.Min(generation, CBMSupply.AvailableEnergy(m));
                float startCost = m.energyStarted ? 0 : ext.activationWd * CBMModes.Consumption(m);
                if (generation <= 0 || record.storedWd < startCost) { m.energyStarted = false; continue; }
                record.storedWd -= startCost; record.consumedWd += startCost;
                if (ext.nutritionPerWd > 0) pawn.needs.food.CurLevel -= generation * ext.nutritionPerWd;
                CBMSupply.ConsumeEnergy(m, generation);
                record.storedWd += generation; record.generatedWd += generation;
                m.energyStarted = true; m.poweredUntilTick = now + 60;
            }
            var consumers = modules.Where(m => m.CanRun && m.def.GetModExtension<CBMExtension>().generationWatts <= 0).ToList();
            float before = record.storedWd;
            var requests = consumers.Select(m => {
                var ext = m.def.GetModExtension<CBMExtension>(); float scale = CBMModes.Consumption(m);
                return new PowerRequest { Id = m.GetUniqueLoadID(), Priority = m.powerPriority,
                    StartupWd = m.energyStarted ? 0 : ext.activationWd * scale, RunningWd = ext.idleWatts * CompPower.WattsToWattDaysPerTick * 60f * scale };
            }).Concat(modules.SelectMany(CBMAbilities.PowerRequests));
            var supplied = PowerRules.Allocate(ref record.storedWd, requests);
            foreach (var m in modules) CBMAbilities.ApplyPower(m, supplied);
            CBMAbilities.React(pawn);
            record.consumedWd += before - record.storedWd;
            foreach (var m in consumers)
            { m.energyStarted = supplied.Contains(m.GetUniqueLoadID()); if (m.energyStarted) m.poweredUntilTick = now + 60; }
            CBMStatEffects.Invalidate(pawn);
        }
    }
    public static class CBMEnergy
    {
        public static string Display(Pawn pawn) { var energy = For(pawn); return energy.storedWd.ToString("0.##") + " Wd / " + energy.capacityWd.ToString("0.##") + " Wd"; }
        public static CBMEnergyComponent Component => Current.Game.GetComponent<CBMEnergyComponent>();
        public static CBMEnergyRecord For(Pawn pawn) => Component.Get(pawn);
        public static RuleResult Toggle(Hediff_CBMModule module)
        {
            if (module.def.GetModExtension<CBMExtension>().abilities.Count > 0) return RuleResult.Fail("AbilityMode");
            if (!ImplantService.CanEdit(module.pawn) || !module.HardwareReady) return RuleResult.Fail("ReadOnly");
            module.active = !module.active;
            module.poweredUntilTick = 0; module.energyStarted = false; module.effectUntilTick = 0;
            CBMStatEffects.Invalidate(module.pawn);
            return RuleResult.Ok();
        }
        public static RuleResult CanUse(Hediff_CBMModule module)
        {
            var a = CBMLegacyAbilities.Use(module);
            return a == null ? RuleResult.Fail("AbilityMode") : CBMCasting.CanQueue(module, a);
        }
        public static RuleResult Use(Hediff_CBMModule module)
        {
            var a = CBMLegacyAbilities.Use(module);
            return a == null ? RuleResult.Fail("AbilityMode") : CBMCasting.Queue(module, a);
        }
        public static void Emp(Hediff_CBMModule module)
        {
            if (!module.HardwareReady) return;
            int duration = CBMTagEffects.For(module.def).Where(e => e.empSensitive).Select(e => e.empDurationTicks).DefaultIfEmpty(0).Max();
            if (duration <= 0) return;
            module.empUntilTick = Math.Max(module.empUntilTick, Find.TickManager.TicksGame + duration);
            module.poweredUntilTick = module.effectUntilTick = 0; module.energyStarted = false;
            CBMAbilities.Interrupt(module);
            CBMStatEffects.Invalidate(module.pawn);
        }
        public static string Status(Hediff_CBMModule m)
        {
            if (m.retainedRemnant) return "RC_Remnant".Translate();
            if (ConditionRules.Burnt(m.condition, m.durability)) return "RC_State_BurntOut".Translate();
            if (m.empUntilTick > Find.TickManager.TicksGame) return "RC_EMP".Translate();
            if (!m.HardwareReady) return "RC_PendingShort".Translate();
            if (!CBMRuntime.Running(m.pawn)) return "RC_RuntimePaused".Translate();
            if (!CBMAccess.Runtime(m)) return "RC_CompatibilityBlocked".Translate();
            if (!CBMModes.Running(m)) return "RC_Off".Translate();
            if (m.def.GetModExtension<CBMExtension>().abilities.Count == 0 && !m.active) return "RC_Off".Translate();
            if (CBMSupply.Spec(m) != null && m.def.GetModExtension<CBMExtension>().generationWatts > 0 && CBMSupply.AvailableEnergy(m) <= 0) return "RC_FuelEmpty".Translate();
            return (m.Operational ? "RC_On" : "RC_NotPowered").Translate();
        }
    }
    [StaticConstructorOnStartup]
    public static class CBMStatEffects
    {
        private static readonly List<StatDef> stats;
        static CBMStatEffects()
        {
            stats = DefDatabase<HediffDef>.AllDefsListForReading.Select(d => d.GetModExtension<CBMExtension>()).Where(e => e != null)
                .SelectMany(e => e.statOffsets.Concat(e.burstStatOffsets).Concat(e.abilities.SelectMany(a => a.statOffsets))).Select(s => s.stat).Distinct().ToList();
            foreach (var stat in stats)
            {
                if (stat.parts == null) stat.parts = new List<StatPart>();
                if (!stat.parts.OfType<StatPart_CBM>().Any()) stat.parts.Insert(stat.parts.FindIndex(p => p is StatPart_CBMBurden) is int index && index >= 0 ? index : stat.parts.Count, new StatPart_CBM { parentStat = stat });
            }
        }
        public static void Invalidate(Pawn pawn) { CBMBurden.Invalidate(pawn); foreach (var stat in stats) stat.Worker.ClearCacheForThing(pawn); }
        public static float Offset(Pawn pawn, StatDef stat)
        {
            if (pawn == null) return 0;
            return ImplantService.Modules(pawn).Sum(m => {
                float abilities = CBMAbilities.Offset(m, stat);
                if (!m.Operational) return abilities;
                var ext = m.def.GetModExtension<CBMExtension>();
                float amount = ext.statOffsets.Where(s => s.stat == stat).Sum(s => s.value);
                if (m.effectUntilTick > Find.TickManager.TicksGame) amount += ext.burstStatOffsets.Where(s => s.stat == stat).Sum(s => s.value);
                return abilities + amount * m.ConditionFactor * CBMModes.Effect(m);
            });
        }
    }
    public class StatPart_CBM : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val) { val += CBMStatEffects.Offset(req.Thing as Pawn, parentStat); }
        public override string ExplanationPart(StatRequest req)
        { float offset = CBMStatEffects.Offset(req.Thing as Pawn, parentStat); return offset == 0 ? null : ("RC_Implants".Translate() + ": ") + offset.ToString("+0.##;-0.##"); }
    }
}
