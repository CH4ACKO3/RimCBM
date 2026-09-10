using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public enum CBMAbilityKind { Passive, Toggle, OneShot, Reactive }
    public enum CBMTargetKind { Self, Pawn, FriendlyPawn, HostilePawn, Thing }
    public enum CBMTrigger { DamageTaken, HealthBelow, BloodLossAbove, Custom }

    // Stable ids belong to a module definition, so one physical module may have several abilities.
    public class CBMAbilitySpec
    {
        public List<ResearchProjectDef> research = new List<ResearchProjectDef>();
        public string id, labelKey;
        public int warmupTicks = 60;
        public bool mapOnly;
        public bool? requiresViolence;
        public bool targetEventSource;
        public string eventId;
        public Type triggerWorkerClass = typeof(CBMTriggerWorker), dataClass;
        private CBMTriggerWorker triggerWorker;
        public CBMTriggerWorker TriggerWorker => triggerWorker ?? (triggerWorker = (CBMTriggerWorker)Activator.CreateInstance(triggerWorkerClass));
        public List<CBMAbilityEffect> effects = new List<CBMAbilityEffect>();
        public List<CBMTargetKind> targetKinds = new List<CBMTargetKind> { CBMTargetKind.Self };
        public float range = 1.5f, damageAmount;
        public DamageDef damageDef;
        public bool requireLineOfSight = true;
        public CBMAbilityKind kind;
        public bool defaultEnabled = true;
        public float idleWatts, activationWd, useWd, reserveWd;
        public int durationTicks, cooldownTicks = 600, priority;
        public CBMTrigger trigger;
        public float threshold = .5f, minimumDamage = 1f;
        public List<DamageDef> damageDefs = new List<DamageDef>();
        public List<StatModifier> statOffsets = new List<StatModifier>();
        public float healAmount;
        public Type workerClass = typeof(CBMAbilityWorker);
        private CBMAbilityWorker worker;
        public CBMAbilityWorker Worker => worker ?? (worker = (CBMAbilityWorker)Activator.CreateInstance(workerClass));
        public string Label => labelKey.NullOrEmpty() ? id : labelKey.Translate().ToString();
        public bool Continuous => kind == CBMAbilityKind.Passive || kind == CBMAbilityKind.Toggle;
        public virtual IEnumerable<string> ConfigErrors()
        {
            if (triggerWorkerClass == null || triggerWorkerClass.IsAbstract || !typeof(CBMTriggerWorker).IsAssignableFrom(triggerWorkerClass) || triggerWorkerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid trigger worker.";
            if (dataClass != null && (dataClass.IsAbstract || !typeof(CBMAbilityData).IsAssignableFrom(dataClass) || dataClass.GetConstructor(Type.EmptyTypes) == null)) yield return "Invalid ability data class.";
            if (trigger == CBMTrigger.Custom && eventId.NullOrEmpty()) yield return "Custom trigger requires eventId.";
            foreach (var effect in effects) foreach (var error in effect.ConfigErrors()) yield return error;
            if (warmupTicks < 0) yield return "Invalid warmup.";
            if (Continuous && (effects.Count > 0 || damageAmount > 0)) yield return "Continuous abilities cannot apply instant effects.";
            if (research.Any(r => r == null)) yield return "Unknown ability research.";
            if (targetKinds.Count == 0 || targetKinds.Any(t => !Enum.IsDefined(typeof(CBMTargetKind), t))) yield return "Invalid ability targets.";
            if (targetKinds.Any(t => t != CBMTargetKind.Self) && (kind != CBMAbilityKind.OneShot && kind != CBMAbilityKind.Reactive || statOffsets.Count > 0)) yield return "External targets require a triggered effect, not host stat offsets.";
            if (damageAmount > 0 && damageDef == null) yield return "Damage ability requires damageDef.";
            if (id.NullOrEmpty() || !Enum.IsDefined(typeof(CBMAbilityKind), kind) || !Enum.IsDefined(typeof(CBMTrigger), trigger)) yield return "Invalid ability id or kind/trigger.";
            if (workerClass == null || workerClass.IsAbstract || !typeof(CBMAbilityWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid ability worker class.";
            foreach (float n in new[] { idleWatts, activationWd, useWd, reserveWd, threshold, minimumDamage, healAmount, range, damageAmount })
                if (float.IsNaN(n) || float.IsInfinity(n) || n < 0) yield return "Ability values must be finite and nonnegative.";
            if (threshold > 1 || durationTicks < 0 || cooldownTicks < 0) yield return "Invalid ability threshold or timing.";
            if (Continuous && (healAmount > 0 || useWd > 0 || durationTicks > 0)) yield return "Continuous abilities use upkeep and stat offsets, not triggered effects.";
            if (!Continuous && (idleWatts > 0 || activationWd > 0 || statOffsets.Count > 0 && durationTicks == 0)) yield return "Triggered abilities use per-use costs; timed stats need durationTicks.";
        }
    }
    public class CBMAbilityState : IExposable
    {
        public string id;
        public bool enabled, started;
        public bool runtimeActive; // Runtime resources are reattached once after load.
        public CBMAbilityData data;
        public int poweredUntil, effectUntil, cooldownUntil, lastTriggerTick = -1, priority;
        public float reserveWd, threshold;
        public void ExposeData()
        {
            Scribe_Deep.Look(ref data, "data");
            Scribe_Values.Look(ref id, "id"); Scribe_Values.Look(ref enabled, "enabled");
            Scribe_Values.Look(ref started, "started"); Scribe_Values.Look(ref poweredUntil, "poweredUntil");
            Scribe_Values.Look(ref effectUntil, "effectUntil"); Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", -1);
            Scribe_Values.Look(ref priority, "priority"); Scribe_Values.Look(ref reserveWd, "reserveWd"); Scribe_Values.Look(ref threshold, "threshold");
        }
        public CBMAbilityState PortableCopy() => new CBMAbilityState { id = id, data = data?.Copy(), enabled = enabled, priority = priority,
            reserveWd = reserveWd, threshold = threshold, cooldownUntil = cooldownUntil, lastTriggerTick = lastTriggerTick };
    }
    public class CBMAbilityWorker
    {
        public virtual bool CanApply(CBMExecutionContext context) => CanApply(context.module, context.ability, context.target);
        public virtual void Apply(CBMExecutionContext context) => Apply(context.module, context.ability, context.target);
        public virtual void OnStarted(Hediff_CBMModule module, CBMAbilitySpec ability, CBMAbilityState state) { }
        public virtual void OnStopped(Hediff_CBMModule module, CBMAbilitySpec ability, CBMAbilityState state, CBMStopReason reason) { }
        public virtual void Tick(Hediff_CBMModule module, CBMAbilitySpec ability, CBMAbilityState state, int ticks) { }
        // Override for further effects; CanApply must not mutate or spend energy.
        public virtual bool CanApply(Hediff_CBMModule module, CBMAbilitySpec spec) => spec.healAmount <= 0 || Injuries(module.pawn).Any();
        public virtual bool CanApply(Hediff_CBMModule module, CBMAbilitySpec spec, Thing target) => target == module.pawn
            ? CanApply(module, spec) : spec.healAmount <= 0 || target is Pawn patient && Injuries(patient).Any();
        public virtual void Apply(Hediff_CBMModule module, CBMAbilitySpec spec, Thing target)
        {
            if (target == module.pawn) Apply(module, spec);
            else if (target is Pawn patient) Heal(patient, spec.healAmount * Mathf.Clamp01(module.durability) * CBMModes.Effect(module));
            if (spec.damageAmount > 0) target.TakeDamage(new DamageInfo(spec.damageDef, spec.damageAmount * Mathf.Clamp01(module.durability)
                * CBMModes.Effect(module), instigator: module.pawn));
        }
        public virtual void Apply(Hediff_CBMModule module, CBMAbilitySpec spec)
        {
            Heal(module.pawn, spec.healAmount * Mathf.Clamp01(module.durability) * CBMModes.Effect(module));
        }
        private static void Heal(Pawn patient, float remaining)
        {
            foreach (var injury in Injuries(patient).OrderByDescending(h => h.BleedRate).ThenByDescending(h => h.Severity).ToList())
            {
                if (remaining <= 0) break;
                float amount = Math.Min(injury.Severity, remaining); injury.Heal(amount); remaining -= amount;
            }
        }
        private static IEnumerable<Hediff_Injury> Injuries(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Severity > 0 && !h.IsPermanent());
    }
    public static class CBMAbilities
    {
        public static int Now => Find.TickManager?.TicksGame ?? 0;
        public static List<CBMAbilitySpec> Specs(Hediff_CBMModule m) => CBMLegacyAbilities.Specs(m.def.GetModExtension<CBMExtension>());
        public static CBMAbilityState State(Hediff_CBMModule m, CBMAbilitySpec a)
        {
            var state = m.abilityStates.FirstOrDefault(s => s.id == a.id);
            if (state == null)
            {
                state = new CBMAbilityState { id = a.id, enabled = a.defaultEnabled, priority = a.priority, reserveWd = a.reserveWd, threshold = a.threshold };
                if (a.id == CBMLegacyAbilities.UseId) { state.cooldownUntil = m.cooldownUntilTick; state.effectUntil = m.effectUntilTick; }
                m.abilityStates.Add(state);
            }
            if (state.data == null && a.dataClass != null) state.data = (CBMAbilityData)Activator.CreateInstance(a.dataClass);
            return state;
        }
        public static bool Available(Hediff_CBMModule m) => m.HardwareReady && ImplantService.Modules(m.pawn).Contains(m) && CBMAccess.Runtime(m) && CBMModes.Running(m) && CBMRuntime.Running(m.pawn) && m.empUntilTick <= Now;
        public static bool Enabled(Hediff_CBMModule m, CBMAbilitySpec a) => CBMRuntime.Allows(m.pawn, a) && CBMModes.Allows(m, a) && (a.kind == CBMAbilityKind.Passive || a.kind == CBMAbilityKind.OneShot || State(m, a).enabled);
        public static bool Effective(Hediff_CBMModule m, CBMAbilitySpec a)
        {
            if (!Available(m) || !Enabled(m, a)) return false;
            var s = State(m, a);
            return a.Continuous ? a.idleWatts == 0 && a.activationWd == 0 || s.poweredUntil > Now : s.effectUntil > Now;
        }
        public static IEnumerable<PowerRequest> PowerRequests(Hediff_CBMModule m)
        {
            foreach (var a in Specs(m).Where(a => a.Continuous).ToList())
            {
                var s = State(m, a); s.poweredUntil = 0;
                if (!Available(m) || !Enabled(m, a)) { s.started = false; continue; }
                float scale = CBMModes.Consumption(m);
                yield return new PowerRequest { Id = Key(m, a), Priority = m.powerPriority + s.priority,
                    StartupWd = s.started ? 0 : a.activationWd * scale,
                    RunningWd = a.idleWatts * CompPower.WattsToWattDaysPerTick * 60 * scale };
            }
        }
        private static string Key(Hediff_CBMModule m, CBMAbilitySpec a) => m.GetUniqueLoadID() + "/" + a.id;
        public static void ApplyPower(Hediff_CBMModule m, HashSet<string> supplied)
        {
            foreach (var a in Specs(m).Where(a => a.Continuous).ToList())
            {
                var s = State(m, a); s.started = supplied.Contains(Key(m, a)); s.poweredUntil = s.started ? Now + 60 : 0;
                CBMLifecycle.Step(m, a);
            }
        }
        public static RuleResult Toggle(Hediff_CBMModule m, CBMAbilitySpec a)
        {
            if (!ImplantService.CanEdit(m.pawn) || !m.HardwareReady) return RuleResult.Fail("ReadOnly");
            if (!Specs(m).Contains(a) || a.kind != CBMAbilityKind.Toggle && a.kind != CBMAbilityKind.Reactive) return RuleResult.Fail("AbilityMode");
            var s = State(m, a);
            if (!s.enabled && !CBMModes.Allows(m, a)) return RuleResult.Fail("ModeUnavailable");
            s.enabled = !s.enabled; if (!s.enabled) CBMLifecycle.Stop(m, a, CBMStopReason.Disabled); s.started = false; s.poweredUntil = s.effectUntil = 0;
            CBMStatEffects.Invalidate(m.pawn); return RuleResult.Ok();
        }
        public static RuleResult CanActivate(Hediff_CBMModule m, CBMAbilitySpec a, bool automatic = false, Thing target = null, bool planning = false, CBMAbilityEvent trigger = null)
        {
            if (!ImplantService.Modules(m.pawn).Contains(m)) return RuleResult.Fail("UnknownModule");
            if (!Specs(m).Contains(a) || (automatic ? a.kind != CBMAbilityKind.Reactive : a.kind != CBMAbilityKind.OneShot)) return RuleResult.Fail("AbilityMode");
            if (!automatic && (!ImplantService.CanEdit(m.pawn) || m.pawn.Downed || !m.pawn.Awake() || m.pawn.InMentalState)) return RuleResult.Fail("OperatorUnavailable");
            if ((a.requiresViolence ?? (a.damageAmount > 0 || a.effects.Any(e => e.Worker.IsViolent(e)))) && m.pawn.WorkTagIsDisabled(WorkTags.Violent)) return RuleResult.Fail("ViolenceDisabled");
            if (!CBMAccess.Researched(a.research)) return RuleResult.Fail("CBMResearchRequired");
            if (!CBMModes.Allows(m, a)) return RuleResult.Fail("ModeUnavailable");
            if (!Available(m) || !Enabled(m, a) || a.id == CBMLegacyAbilities.UseId && !m.Operational) return RuleResult.Fail("NotPowered");
            var s = State(m, a);
            if (Now < s.cooldownUntil || s.lastTriggerTick == Now || Now < s.effectUntil) return RuleResult.Fail("Cooldown");
            target = target ?? m.pawn;
            var targetCheck = CBMTargeting.Validate(m.pawn, a, target, planning);
            if (!targetCheck.Allowed) return targetCheck;
            var context = new CBMExecutionContext { module = m, ability = a, target = target, trigger = trigger, automatic = automatic };
            if (a.damageAmount > 0 && a.damageDef == null || (!a.Worker.CanApply(context) || a.effects.Any(e => !e.Worker.CanApply(context, e)))) return RuleResult.Fail("NoAbilityEffect");
            if (CBMEnergy.For(m.pawn).storedWd < a.useWd * CBMModes.Consumption(m) + (automatic ? s.reserveWd : 0)) return RuleResult.Fail("NotEnoughEnergy");
            return RuleResult.Ok();
        }
        public static RuleResult Activate(Hediff_CBMModule m, CBMAbilitySpec a, bool automatic = false, Thing target = null, CBMAbilityEvent trigger = null)
        {
            target = target ?? m.pawn;
            var result = CanActivate(m, a, automatic, target, trigger: trigger); if (!result.Allowed) return result;
            var s = State(m, a); var pool = CBMEnergy.For(m.pawn);
            pool.storedWd -= a.useWd * CBMModes.Consumption(m);
            s.lastTriggerTick = Now; s.cooldownUntil = Now + a.cooldownTicks; s.effectUntil = Now + a.durationTicks;
            if (a.id == CBMLegacyAbilities.UseId) { m.cooldownUntilTick = s.cooldownUntil; m.effectUntilTick = s.effectUntil; }
            var context = new CBMExecutionContext { module = m, ability = a, target = target, automatic = automatic, trigger = trigger };
            a.Worker.Apply(context);
            foreach (var effect in a.effects)
                if (!target.Destroyed && !(target is Pawn victim && victim.Dead)) effect.Worker.Apply(context, effect);
            CBMStatEffects.Invalidate(m.pawn); return RuleResult.Ok();
        }
        public static void Interrupt(Hediff_CBMModule m, CBMStopReason reason = CBMStopReason.Interrupted)
        {
            foreach (var a in Specs(m).Where(a => a.Continuous).ToList()) CBMLifecycle.Stop(m, a, reason);
            foreach (var s in m.abilityStates) { s.started = false; s.poweredUntil = s.effectUntil = 0; }
            CBMStatEffects.Invalidate(m.pawn);
        }
        private static readonly HashSet<Pawn> reacting = new HashSet<Pawn>();
        public static void React(Pawn pawn, DamageInfo? damage = null, float amount = 0)
            => Dispatch(pawn, new CBMAbilityEvent { id = damage.HasValue ? "DamageTaken" : "Periodic", damage = damage, damageAmount = amount, source = damage?.Instigator });
        public static void Dispatch(Pawn pawn, CBMAbilityEvent ev)
        {
            if (pawn == null || ev == null || !reacting.Add(pawn)) return;
            try
            {
                var candidates = ImplantService.Modules(pawn).SelectMany(m => Specs(m).Where(a => a.kind == CBMAbilityKind.Reactive)
                    .Select(a => new { m, a, s = State(m, a) })).OrderByDescending(c => c.m.powerPriority + c.s.priority)
                    .ThenBy(c => c.m.GetUniqueLoadID(), StringComparer.Ordinal).ThenBy(c => c.a.id, StringComparer.Ordinal).ToList();
                foreach (var c in candidates)
                {
                    if (!ImplantService.Modules(pawn).Contains(c.m) || !c.a.TriggerWorker.Matches(c.m, c.a, c.s, ev)) continue;
                    var target = c.a.TriggerWorker.Target(c.m, c.a, ev);
                    if (target != null) Activate(c.m, c.a, true, target, ev);
                }
            }
            finally { reacting.Remove(pawn); }
        }
        public static float Offset(Hediff_CBMModule m, StatDef stat) => Specs(m).Where(a => Effective(m, a))
            .Sum(a => a.statOffsets.Where(o => o.stat == stat).Sum(o => o.value)) * Mathf.Clamp01(m.durability) * CBMModes.Effect(m);
        public static string Status(Hediff_CBMModule m, CBMAbilitySpec a)
        {
            var s = State(m, a);
            if (!Available(m)) return CBMEnergy.Status(m);
            if (!Enabled(m, a)) return "RC_Off".Translate();
            if (Effective(m, a)) return "RC_On".Translate();
            if (s.cooldownUntil > Now) return "RC_AbilityCooldown".Translate((s.cooldownUntil - Now).ToStringTicksToPeriod());
            if (a.Continuous) return "RC_NotPowered".Translate();
            return (a.kind == CBMAbilityKind.Reactive ? "RC_Armed" : "RC_Ready").Translate();
        }
    }
    // Run after the health tracker has finished iterating its hediffs. Healing can remove injuries.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PostApplyDamage")]
    public static class CBMAbilityDamageHook
    {
        static void Postfix(Pawn ___pawn, DamageInfo dinfo, float totalDamageDealt) => CBMAbilities.React(___pawn, dinfo, totalDamageDealt);
    }
}
