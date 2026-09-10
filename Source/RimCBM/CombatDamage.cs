using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public class CBMSettingsDef : Def
    {
        public float referenceMassKg = 60, burningDurabilityPerSecond = .01f;
        public override IEnumerable<string> ConfigErrors()
        {
            if (burningDurabilityPerSecond < 0 || float.IsNaN(burningDurabilityPerSecond) || float.IsInfinity(burningDurabilityPerSecond)) yield return "Invalid burning damage rate.";
            if (referenceMassKg <= 0 || float.IsNaN(referenceMassKg) || float.IsInfinity(referenceMassKg)) yield return "referenceMassKg must be finite and positive.";
        }
    }
    public enum CBMDamageCause { Combat, Surgery, SlotRemoved, SlotDestroyed, Other, Fire }
    public enum CBMDamageTrigger { Damaged, Burnout }
    public class CBMDamageMultiplier { public DamageDef damageDef; public float multiplier = 1; }
    public class CBMCombatProfile
    {
        public float hitChance = .3f, hitWeight = 1, hitPoints = 40, damageMultiplier = 1, fireDamageMultiplier = 1;
        public List<CBMDamageMultiplier> damageFactors = new List<CBMDamageMultiplier>();
        public IEnumerable<string> ConfigErrors()
        {
            if (hitChance < 0 || hitChance > 1 || hitPoints <= 0 || new[] { hitChance, hitWeight, hitPoints, damageMultiplier, fireDamageMultiplier }.Any(v => v < 0 || float.IsNaN(v) || float.IsInfinity(v))) yield return "Invalid implant combat profile.";
            if (damageFactors.Any(e => e.damageDef == null || e.multiplier < 0 || float.IsNaN(e.multiplier) || float.IsInfinity(e.multiplier))) yield return "Invalid damage type factor.";
        }
    }
    public class CBMDamageEffect
    {
        public CBMDamageTrigger trigger;
        public float chance = 1, fireSize = .3f;
        public bool requiresActive;
        public List<CBMDamageCause> causes = new List<CBMDamageCause>(); // Empty = all causes.
        public Type workerClass = typeof(CBMDamageEffect_Ignite);
        public IEnumerable<string> ConfigErrors()
        {
            if (float.IsNaN(chance) || chance < 0 || chance > 1 || float.IsNaN(fireSize) || float.IsInfinity(fireSize) || fireSize <= 0
                || !Enum.IsDefined(typeof(CBMDamageTrigger), trigger) || causes.Any(c => !Enum.IsDefined(typeof(CBMDamageCause), c))) yield return "Invalid implant damage effect.";
            if (workerClass == null || workerClass.IsAbstract || !typeof(CBMDamageEffectWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid damage effect worker.";
        }
    }
    public class CBMDamageContext
    {
        public Hediff_CBMModule module;
        public CBMDamageCause cause;
        public DamageInfo? damage;
        public float previousDurability, lostDurability;
        public bool burntNow, wasActive;
    }
    public abstract class CBMDamageEffectWorker { public abstract void Apply(CBMDamageContext context, CBMDamageEffect effect); }
    public class CBMDamageEffect_Ignite : CBMDamageEffectWorker
    {
        public override void Apply(CBMDamageContext context, CBMDamageEffect effect)
        {
            var pawn = context.module.pawn;
            Thing target = pawn.Dead ? (Thing)pawn.Corpse : pawn;
            if (target?.Spawned == true) target.TryAttachFire(effect.fireSize, context.damage?.Instigator);
        }
    }
    public static class CBMDamageEvents
    {
        [ThreadStatic] public static int EffectDepth;
        public static void Notify(Hediff_CBMModule module, float before, bool wasBurnt, bool wasActive, CBMDamageCause cause, DamageInfo? damage = null)
        {
            float lost = Mathf.Max(0, before - module.durability);
            bool burnt = !wasBurnt && ConditionRules.Burnt(module.condition, module.durability);
            if (EffectDepth > 0 || lost <= 0 && !burnt) return;
            var ext = module.def.GetModExtension<CBMExtension>();
            var context = new CBMDamageContext { module = module, cause = cause, damage = damage, previousDurability = before, lostDurability = lost, burntNow = burnt, wasActive = wasActive };
            int now = Find.TickManager?.TicksGame ?? 0;
            bool cooling = module.damageEffectUntilTick > now;
            bool attempted = false;
            EffectDepth++;
            try
            {
                foreach (var effect in ext.damageEffects)
                {
                    if (effect.trigger == CBMDamageTrigger.Burnout ? !burnt : lost <= 0 || cooling) continue;
                    if (effect.requiresActive && !wasActive || effect.causes.Count > 0 && !effect.causes.Contains(cause)) continue;
                    attempted = true;
                    if (Rand.Chance(effect.chance)) ((CBMDamageEffectWorker)Activator.CreateInstance(effect.workerClass)).Apply(context, effect);
                }
            }
            finally
            {
                if (attempted) module.damageEffectUntilTick = now + ext.damageEffectCooldownTicks;
                EffectDepth--;
            }
        }
    }
    public class CBMHitContext
    {
        public Pawn pawn;
        public BodyPartRecord hitPart;
        public DamageInfo damage;
        public float actualDamage;
    }
    // CE can replace the resolver and call ApplyResolvedHit with its own post-armor hit data.
    public class CBMHitResolver
    {
        public virtual Hediff_CBMModule Select(CBMHitContext context)
        {
            var host = context.hitPart;
            while (host != null && !DefDatabase<CBMGridProfileDef>.AllDefsListForReading.Any(p => p.bodyPart == host.def)) host = host.parent;
            if (host == null) return null;
            var candidates = ImplantService.Modules(context.pawn).Where(m => m.Part == host && !m.lossPending && !ConditionRules.Burnt(m.condition, m.durability)
                && m.def.GetModExtension<CBMExtension>().combat.hitWeight > 0).ToList();
            if (candidates.Count == 0) return null;
            var chosen = candidates.RandomElementByWeight(m => m.def.GetModExtension<CBMExtension>().combat.hitWeight * m.def.GetModExtension<CBMExtension>().width * m.def.GetModExtension<CBMExtension>().height);
            return Rand.Chance(chosen.def.GetModExtension<CBMExtension>().combat.hitChance) ? chosen : null;
        }
        public virtual float DurabilityDamage(CBMHitContext context, Hediff_CBMModule module)
        {
            var p = module.def.GetModExtension<CBMExtension>().combat;
            float factor = p.damageFactors.FirstOrDefault(f => f.damageDef == context.damage.Def)?.multiplier ?? 1;
            return context.actualDamage * p.damageMultiplier * factor / p.hitPoints;
        }
    }
    public static class CBMCombat
    {
        public static CBMHitResolver Resolver = new CBMHitResolver();
        public static bool UseVanillaHook = true;
        [ThreadStatic] public static int Suppressed;
        public static void ApplyResolvedHit(CBMHitContext context)
        {
            if (Suppressed > 0 || CBMDamageEvents.EffectDepth > 0 || context?.pawn?.health == null || context.hitPart == null
                || context.actualDamage <= 0 || float.IsNaN(context.actualDamage) || float.IsInfinity(context.actualDamage)
                || context.damage.Def == null || !context.damage.Def.harmsHealth || context.damage.Def == DamageDefOf.EMP || context.damage.Def == DamageDefOf.SurgicalCut) return;
            var module = Resolver.Select(context);
            if (module == null || !ImplantService.Modules(context.pawn).Contains(module)) return;
            CBMConditionUtility.Damage(module, Resolver.DurabilityDamage(context, module), CBMDamageCause.Combat, context.damage);
        }
    }
    [HarmonyPatch]
    public static class CBMResolvedInjuryHook
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury", new[] { typeof(Pawn), typeof(Hediff_Injury), typeof(DamageInfo), typeof(DamageWorker.DamageResult) });
        static void Postfix(Pawn pawn, Hediff_Injury injury, DamageInfo dinfo, float __result)
        {
            if (CBMCombat.UseVanillaHook) CBMCombat.ApplyResolvedHit(new CBMHitContext { pawn = pawn, hitPart = injury.Part, damage = dinfo, actualDamage = __result });
        }
    }
}
