using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public class CompProperties_CBMCondition : CompProperties
    {
        public CompProperties_CBMCondition() { compClass = typeof(CompCBMCondition); }
    }
    public class CompCBMCondition : ThingComp
    {
        public string modeId;
        public CBMFuelState fuelState = new CBMFuelState();
        public List<CBMFaultState> faultStates = new List<CBMFaultState>();
        public int damageEffectUntilTick, legacyCooldownUntilTick;
        public List<CBMAbilityState> abilityStates = new List<CBMAbilityState>();
        public CBMCondition condition;
        public float durability = 1f;
        public bool active = true;
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref modeId, "cbmMode");
            Scribe_Deep.Look(ref fuelState, "cbmFuel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && fuelState == null) fuelState = new CBMFuelState();
            Scribe_Collections.Look(ref faultStates, "cbmFaultStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && faultStates == null) faultStates = new List<CBMFaultState>();
            Scribe_Values.Look(ref damageEffectUntilTick, "cbmDamageEffectUntil");
            Scribe_Values.Look(ref legacyCooldownUntilTick, "cbmLegacyCooldownUntil");
            Scribe_Collections.Look(ref abilityStates, "cbmAbilities", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && abilityStates == null) abilityStates = new List<CBMAbilityState>();
            Scribe_Values.Look(ref condition, "cbmCondition");
            Scribe_Values.Look(ref durability, "cbmDurability", 1f);
            Scribe_Values.Look(ref active, "cbmActive", true);
        }
        public override bool AllowStackWith(Thing other)
        {
            var comp = other.TryGetComp<CompCBMCondition>();
            return comp != null && modeId == comp.modeId && fuelState.units == 0 && comp.fuelState.units == 0 && !fuelState.automatic && !comp.fuelState.automatic && fuelState.preferred == comp.fuelState.preferred && fuelState.threshold == comp.fuelState.threshold && fuelState.target == comp.fuelState.target && comp.condition == condition && comp.durability == durability && comp.active == active && comp.legacyCooldownUntilTick == legacyCooldownUntilTick && comp.damageEffectUntilTick == damageEffectUntilTick && faultStates.Count == 0 && comp.faultStates.Count == 0 && abilityStates.Count == 0 && comp.abilityStates.Count == 0;
        }
        public override void PostSplitOff(Thing piece)
        {
            var comp = piece.TryGetComp<CompCBMCondition>();
            if (comp != null) { comp.legacyCooldownUntilTick = legacyCooldownUntilTick; comp.modeId = modeId; comp.fuelState = fuelState.Copy(); comp.faultStates = faultStates.Select(s => s.Copy()).ToList(); comp.damageEffectUntilTick = damageEffectUntilTick; comp.condition = condition; comp.durability = durability; comp.active = active; comp.abilityStates = abilityStates.Select(s => s.PortableCopy()).ToList(); }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode) yield break;
            yield return new Command_Action { defaultLabel = "DEV: " + "RC_State_Dirty".Translate(), action = () => { condition |= CBMCondition.Dirty | CBMCondition.Unsterile; PanelSnapshot.Invalidate(); } };
            yield return new Command_Action { defaultLabel = "DEV: " + "RC_State_Damaged".Translate(), action = () => { durability = System.Math.Max(0, durability - .35f); if (durability == 0) condition |= CBMCondition.BurntOut; PanelSnapshot.Invalidate(); } };
            yield return new Command_Action { defaultLabel = "DEV: " + "RC_State_BurntOut".Translate(), action = () => { condition |= CBMCondition.BurntOut; durability = 0; active = false; PanelSnapshot.Invalidate(); } };
            yield return new Command_Action { defaultLabel = "DEV: " + "RC_State_Intact".Translate(), action = () => { condition = CBMCondition.None; durability = 1; active = true; PanelSnapshot.Invalidate(); } };
        }
        public override string TransformLabel(string label)
            => condition == CBMCondition.None && durability >= 1 ? label : label + " (" + CBMConditionUtility.Label(condition, durability) + ")";
        public override string CompInspectStringExtra() => CBMConditionUtility.Label(condition, durability) + " · " + durability.ToStringPercent() + (fuelState.units > 0 ? "\n" + fuelState.item?.LabelCap.ToString() + ": " + fuelState.units.ToString("0.##") : "");
    }
    public static class CBMConditionUtility
    {
        public static string Label(CBMCondition condition, float durability)
        {
            var labels = new List<string>();
            if ((condition & CBMCondition.Dirty) != 0) labels.Add("RC_State_Dirty".Translate());
            if ((condition & CBMCondition.Unsterile) != 0) labels.Add("RC_State_Unsterile".Translate());
            if (ConditionRules.Burnt(condition, durability)) labels.Add("RC_State_BurntOut".Translate());
            else if (durability < 1) labels.Add("RC_State_Damaged".Translate());
            return labels.Count == 0 ? "RC_State_Intact".Translate().ToString() : string.Join(" / ", labels);
        }
        public static RuleResult CanInstall(Thing item)
        {
            var comp = item?.TryGetComp<CompCBMCondition>();
            var error = comp == null ? null : ConditionRules.InstallError(comp.condition, comp.durability);
            return error == null ? RuleResult.Ok() : RuleResult.Fail(error);
        }
        public static Thing MakeItem(Hediff_CBMModule module, bool contaminated = false)
        {
            var item = ThingMaker.MakeThing(module.def.GetModExtension<CBMExtension>().item);
            var comp = item.TryGetComp<CompCBMCondition>();
            if (comp != null)
            {
                comp.condition = module.condition | (contaminated ? CBMCondition.Dirty | CBMCondition.Unsterile : CBMCondition.None);
                comp.legacyCooldownUntilTick = module.cooldownUntilTick;
                comp.modeId = module.modeId;
                comp.fuelState = module.fuelState.Copy();
                comp.faultStates = module.faultStates.Select(s => s.Copy()).ToList();
                comp.damageEffectUntilTick = module.damageEffectUntilTick;
                comp.durability = module.durability; comp.active = module.active;
                comp.abilityStates = module.abilityStates.Select(s => s.PortableCopy()).ToList();
            }
            return item;
        }
        public static void ApplyItem(Thing item, Hediff_CBMModule module)
        {
            var comp = item?.TryGetComp<CompCBMCondition>();
            if (comp == null) return;
            module.cooldownUntilTick = comp.legacyCooldownUntilTick;
            module.modeId = comp.modeId;
            module.fuelState = comp.fuelState.Copy();
            module.faultStates = comp.faultStates.Select(s => s.Copy()).ToList();
            module.damageEffectUntilTick = comp.damageEffectUntilTick;
            module.abilityStates = comp.abilityStates.Select(s => s.PortableCopy()).ToList();
            module.condition = comp.condition; module.durability = comp.durability; module.active = comp.active;
        }
        // Extension point for future combat effects. Burnout is latched and cannot be repaired by adding durability.
        public static void Damage(Hediff_CBMModule module, float amount, CBMDamageCause cause = CBMDamageCause.Other, DamageInfo? damage = null)
        {
            if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            float previous = module.durability; bool burnt = ConditionRules.Burnt(module.condition, previous), active = module.active;
            var before = ImplantService.Build(module.pawn).Boards();
            module.durability = System.Math.Max(0, module.durability - amount);
            if (module.durability <= 0) { module.condition |= CBMCondition.BurntOut; module.active = false; CBMAbilities.Interrupt(module, CBMStopReason.Unavailable); }
            CBMDamageEvents.Notify(module, previous, burnt, active, cause, damage);
            CBMCapacityComponent.Reconcile(module.pawn, SlotLossReason.Destroyed, before);
            PanelSnapshot.Invalidate();
        }
    }
}
