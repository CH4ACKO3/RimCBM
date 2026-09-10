using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public enum CBMEffectKind { Damage, Heal, Tend, AddHediff, ReduceHediff, Projectile, Custom }
    // Effects execute in XML order after a single validated payment. Custom workers use the same contract.
    public class CBMAbilityEffect
    {
        public CBMEffectKind kind;
        public float amount = 1, armorPenetration, quality = .5f;
        public DamageDef damageDef;
        public HediffDef hediff;
        public ThingDef projectile;
        public Type workerClass = typeof(CBMEffectWorker);
        private CBMEffectWorker worker;
        public CBMEffectWorker Worker => worker ?? (worker = (CBMEffectWorker)Activator.CreateInstance(workerClass));
        public virtual IEnumerable<string> ConfigErrors()
        {
            if (!Enum.IsDefined(typeof(CBMEffectKind), kind) || new[] { amount, armorPenetration, quality }.Any(n => n < 0 || float.IsNaN(n) || float.IsInfinity(n))) yield return "Invalid ability effect values.";
            if (kind == CBMEffectKind.Custom && workerClass == typeof(CBMEffectWorker)) yield return "Custom effect requires a custom worker.";
            if (kind == CBMEffectKind.Damage && damageDef == null) yield return "Damage effect needs damageDef.";
            if ((kind == CBMEffectKind.AddHediff || kind == CBMEffectKind.ReduceHediff) && hediff == null) yield return "Health effect needs hediff.";
            if (kind == CBMEffectKind.Projectile && (projectile?.projectile == null || !typeof(Projectile).IsAssignableFrom(projectile.thingClass))) yield return "Projectile effect needs a projectile ThingDef.";
            if (workerClass == null || workerClass.IsAbstract || !typeof(CBMEffectWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid ability effect worker.";
        }
    }
    public class CBMEffectWorker
    {
        // Custom map effects must override this flag; do not access Map in world-safe workers.
        public virtual bool MapOnly => false;
        public virtual bool RequiresMap(CBMAbilityEffect effect) => MapOnly || effect.kind == CBMEffectKind.Damage || effect.kind == CBMEffectKind.Projectile;
        public virtual bool IsViolent(CBMAbilityEffect effect) => effect.kind == CBMEffectKind.Damage || effect.kind == CBMEffectKind.Projectile;
        public virtual bool CanApply(CBMExecutionContext context, CBMAbilityEffect effect) => CanApply(context.module, effect, context.target);
        public virtual void Apply(CBMExecutionContext context, CBMAbilityEffect effect) => Apply(context.module, effect, context.target);
        public virtual bool CanApply(Hediff_CBMModule module, CBMAbilityEffect effect, Thing target)
        {
            if (target == null || target.Destroyed || target is Pawn dead && dead.Dead) return false;
            if (effect.kind == CBMEffectKind.Damage || effect.kind == CBMEffectKind.Projectile)
                return module.pawn.Spawned && target.Spawned && module.pawn.Map == target.Map;
            var pawn = target as Pawn; if (pawn == null) return false;
            switch (effect.kind)
            {
                case CBMEffectKind.Heal: return Injuries(pawn).Any();
                case CBMEffectKind.Tend: return pawn.health.hediffSet.hediffs.Any(h => h.TendableNow());
                case CBMEffectKind.ReduceHediff: return pawn.health.hediffSet.hediffs.Any(h => h.def == effect.hediff && h.Severity > 0);
                default: return true;
            }
        }
        public virtual void Apply(Hediff_CBMModule module, CBMAbilityEffect effect, Thing target)
        {
            float scale = Mathf.Clamp01(module.durability) * CBMModes.Effect(module), remaining = effect.amount * scale;
            var pawn = target as Pawn;
            switch (effect.kind)
            {
                case CBMEffectKind.Damage:
                    target.TakeDamage(new DamageInfo(effect.damageDef, remaining, effect.armorPenetration, instigator: module.pawn)); break;
                case CBMEffectKind.Projectile:
                    var shot = (Projectile)GenSpawn.Spawn(effect.projectile, module.pawn.Position, module.pawn.Map);
                    shot.Launch(module.pawn, target, target, ProjectileHitFlags.All); break;
                case CBMEffectKind.Heal:
                    foreach (var injury in Injuries(pawn).OrderByDescending(h => h.BleedRate).ThenByDescending(h => h.Severity).ToList())
                    { if (remaining <= 0) break; float healed = Math.Min(remaining, injury.Severity); injury.Heal(healed); remaining -= healed; }
                    break;
                case CBMEffectKind.Tend:
                    int batch = 0;
                    foreach (var h in pawn.health.hediffSet.hediffs.Where(h => h.TendableNow()).ToList()) h.Tended(effect.quality * scale, 1, batch++);
                    break;
                case CBMEffectKind.AddHediff:
                    var existing = pawn.health.hediffSet.GetFirstHediffOfDef(effect.hediff);
                    if (existing == null) { existing = HediffMaker.MakeHediff(effect.hediff, pawn); existing.Severity = remaining; pawn.health.AddHediff(existing); }
                    else existing.Severity += remaining;
                    break;
                case CBMEffectKind.ReduceHediff:
                    foreach (var h in pawn.health.hediffSet.hediffs.Where(h => h.def == effect.hediff).ToList())
                    { if (remaining <= 0) break; float reduced = Math.Min(h.Severity, remaining); h.Severity -= reduced; remaining -= reduced; if (h.Severity <= 0) pawn.health.RemoveHediff(h); }
                    break;
            }
        }
        private static IEnumerable<Hediff_Injury> Injuries(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => !h.IsPermanent() && h.Severity > 0);
    }
}
