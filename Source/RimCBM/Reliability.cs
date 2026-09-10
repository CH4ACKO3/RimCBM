using System;
using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public class CBMFaultState : IExposable
    {
        public string id;
        public int nextTick;
        public void ExposeData() { Scribe_Values.Look(ref id, "id"); Scribe_Values.Look(ref nextTick, "nextTick"); }
        public CBMFaultState Copy() => new CBMFaultState { id = id, nextTick = nextTick };
    }
    public class CBMFaultSpec
    {
        public string id, labelKey = "RC_FaultDischarge";
        public float maxDurability = .75f, chance = .25f, damageAmount = 2, energyWd = .5f;
        public bool affectsBurnt = true, requiresActive, requiresEnergy = true;
        public IntRange intervalTicks = new IntRange(600, 1800);
        public Type workerClass = typeof(CBMFaultWorker_Discharge);
        public virtual IEnumerable<string> ConfigErrors()
        {
            if (id.NullOrEmpty() || intervalTicks.min < 60 || intervalTicks.max < intervalTicks.min || maxDurability < 0 || maxDurability >= 1 || chance < 0 || chance > 1
                || new[] { maxDurability, chance, damageAmount, energyWd }.Any(v => v < 0 || float.IsNaN(v) || float.IsInfinity(v))) yield return "Invalid residual implant fault.";
            if (workerClass == null || workerClass.IsAbstract || !typeof(CBMFaultWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid implant fault worker.";
        }
    }
    public abstract class CBMFaultWorker { public virtual bool MapOnly => true; public abstract void Apply(Hediff_CBMModule module, CBMFaultSpec spec); }
    public class CBMFaultWorker_Discharge : CBMFaultWorker
    {
        public override bool MapOnly => false;
        public override void Apply(Hediff_CBMModule module, CBMFaultSpec spec)
        {
            var pawn = module.pawn;
            var energy = CBMEnergy.For(pawn);
            if (spec.requiresEnergy && energy.storedWd < spec.energyWd) return;
            energy.storedWd = Mathf.Max(0, energy.storedWd - spec.energyWd);
            if (spec.damageAmount <= 0 || pawn.health.hediffSet.PartIsMissing(module.Part)) return;
            var damage = new DamageInfo(DamageDefOf.Burn, spec.damageAmount, 0, -1, pawn, module.Part);
            damage.SetIgnoreArmor(true); damage.SetAllowDamagePropagation(false);
            // Internal fault injury must not recursively hit the implant that produced it.
            CBMCombat.Suppressed++;
            CBMDamageEvents.EffectDepth++;
            try { pawn.TakeDamage(damage); }
            finally { CBMDamageEvents.EffectDepth--; CBMCombat.Suppressed--; }
        }
    }
    public class CBMReliabilityComponent : GameComponent
    {
        public CBMReliabilityComponent(Game game) { }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList())
                if (!pawn.Dead && ImplantService.Modules(pawn).Any()) Step(pawn);
        }
        public static void Step(Pawn pawn)
        {
            if (!CBMRuntime.Running(pawn)) return;
            int now = Find.TickManager.TicksGame;
            var fire = pawn.Spawned ? pawn.GetAttachment(ThingDefOf.Fire) as Fire : null;
            foreach (var module in ImplantService.Modules(pawn).ToList())
            {
                if (pawn.Dead) break;
                if (!ImplantService.Modules(pawn).Contains(module)) continue;
                var ext = module.def.GetModExtension<CBMExtension>();
                if (fire != null && !ConditionRules.Burnt(module.condition, module.durability))
                {
                    float loss = DefDatabase<CBMSettingsDef>.GetNamed("RimCBM_Settings").burningDurabilityPerSecond * ext.combat.fireDamageMultiplier * Mathf.Clamp(fire.fireSize, .25f, 2);
                    CBMConditionUtility.Damage(module, loss, CBMDamageCause.Fire);
                }
                if (pawn.Dead || !ImplantService.Modules(pawn).Contains(module)) continue;
                foreach (var fault in ext.faults)
                {
                    var state = module.faultStates.FirstOrDefault(s => s.id == fault.id);
                    bool eligible = module.durability <= fault.maxDurability && (fault.affectsBurnt || !ConditionRules.Burnt(module.condition, module.durability)) && (!fault.requiresActive || module.active);
                    if (!eligible) { if (state != null) state.nextTick = 0; continue; }
                    if (state == null) { state = new CBMFaultState { id = fault.id }; module.faultStates.Add(state); }
                    if (state.nextTick <= 0) { state.nextTick = now + fault.intervalTicks.RandomInRange; continue; }
                    if (now < state.nextTick) continue;
                    state.nextTick = now + fault.intervalTicks.RandomInRange; // One attempt, never catch up missed offline ticks.
                    var worker = (CBMFaultWorker)Activator.CreateInstance(fault.workerClass);
                    if ((!worker.MapOnly || pawn.Spawned) && Rand.Chance(fault.chance)) worker.Apply(module, fault);
                    if (pawn.Dead || !ImplantService.Modules(pawn).Contains(module)) break;
                }
            }
        }
    }
}
