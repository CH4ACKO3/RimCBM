using System;
using RimWorld;
using Verse;

namespace RimCBM
{
    public enum CBMStopReason { Disabled, Unavailable, PowerLost, Interrupted, Removed, Death }

    public abstract class CBMAbilityData : IExposable
    {
        public abstract void ExposeData();
        public abstract CBMAbilityData Copy();
    }
    public sealed class CBMAbilityEvent
    {
        public string id;
        public DamageInfo? damage;
        public float damageAmount;
        public Thing source;
        public object payload;
    }
    public sealed class CBMExecutionContext
    {
        public Hediff_CBMModule module;
        public CBMAbilitySpec ability;
        public Thing target;
        public CBMAbilityEvent trigger;
        public bool automatic;
    }
    public class CBMTriggerWorker
    {
        // Pure query: custom events enter through CBMAbilities.Dispatch, sharing ordering and recursion guards.
        public virtual bool Matches(Hediff_CBMModule module, CBMAbilitySpec ability, CBMAbilityState state, CBMAbilityEvent ev)
        {
            if (ability.trigger == CBMTrigger.Custom) return !string.IsNullOrEmpty(ability.eventId) && ability.eventId == ev.id;
            if (ev.damage.HasValue) return ability.trigger == CBMTrigger.DamageTaken && ev.damageAmount >= ability.minimumDamage
                && (ability.damageDefs.Count == 0 || ability.damageDefs.Contains(ev.damage.Value.Def));
            if (ev.id != "Periodic") return false;
            return ability.trigger == CBMTrigger.HealthBelow && module.pawn.health.summaryHealth.SummaryHealthPercent < state.threshold
                || ability.trigger == CBMTrigger.BloodLossAbove && (module.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0) >= state.threshold;
        }
        public virtual Thing Target(Hediff_CBMModule module, CBMAbilitySpec ability, CBMAbilityEvent ev) => ability.targetEventSource ? ev.source : module.pawn;
    }
    public static class CBMLifecycle
    {
        public static void Stop(Hediff_CBMModule module, CBMAbilitySpec ability, CBMStopReason reason)
        {
            var state = CBMAbilities.State(module, ability);
            if (!state.runtimeActive) return;
            state.runtimeActive = false; // Clear before callback: callback may mutate health or interrupt this module.
            ability.Worker.OnStopped(module, ability, state, reason);
        }
        public static void Step(Hediff_CBMModule module, CBMAbilitySpec ability)
        {
            var state = CBMAbilities.State(module, ability);
            if (!CBMAbilities.Effective(module, ability))
            {
                Stop(module, ability, !CBMAbilities.Available(module) ? CBMStopReason.Unavailable
                    : !CBMAbilities.Enabled(module, ability) ? CBMStopReason.Disabled : CBMStopReason.PowerLost);
                return;
            }
            if (!state.runtimeActive)
            {
                state.runtimeActive = true;
                ability.Worker.OnStarted(module, ability, state);
            }
            if (state.runtimeActive && CBMAbilities.Effective(module, ability)) ability.Worker.Tick(module, ability, state, 60);
        }
    }
}
