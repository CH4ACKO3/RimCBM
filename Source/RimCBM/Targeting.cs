using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCBM
{
    public static class CBMTargeting
    {
        public static bool Matches(Pawn caster, CBMAbilitySpec a, Thing target)
        {
            if (target == null || target.Destroyed) return false;
            if (target == caster) return a.targetKinds.Contains(CBMTargetKind.Self);
            if (target is Pawn pawn) return !pawn.Dead && (a.targetKinds.Contains(CBMTargetKind.Pawn)
                || a.targetKinds.Contains(CBMTargetKind.HostilePawn) && pawn.HostileTo(caster)
                || a.targetKinds.Contains(CBMTargetKind.FriendlyPawn) && pawn.Faction == caster.Faction);
            return a.targetKinds.Contains(CBMTargetKind.Thing);
        }
        public static RuleResult Validate(Pawn caster, CBMAbilitySpec a, Thing target, bool planning = false)
        {
            if (!Matches(caster, a, target)) return RuleResult.Fail("InvalidCBMTarget");
            if (caster == target) return RuleResult.Ok();
            if (!caster.Spawned || !target.Spawned || caster.Map != target.Map || target.Position.Fogged(caster.Map)) return RuleResult.Fail("InvalidCBMTarget");
            if (planning) return RuleResult.Ok();
            if (!caster.Position.InHorDistOf(target.Position, a.range)) return RuleResult.Fail("CBMOutOfRange");
            if (a.requireLineOfSight && !GenSight.LineOfSight(caster.Position, target.Position, caster.Map)) return RuleResult.Fail("CBMNoSight");
            return RuleResult.Ok();
        }
        public static RuleResult CanBegin(Hediff_CBMModule module, CBMAbilitySpec ability)
        {
            if (!ImplantService.CanEdit(module.pawn) || !module.pawn.Spawned || module.pawn.Downed || !module.pawn.Awake() || module.pawn.InMentalState) return RuleResult.Fail("OperatorUnavailable");
            if (!CBMAbilities.Available(module) || !CBMModes.Allows(module, ability)) return RuleResult.Fail("NotPowered");
            var state = CBMAbilities.State(module, ability);
            if (state.cooldownUntil > CBMAbilities.Now || state.effectUntil > CBMAbilities.Now || state.lastTriggerTick == CBMAbilities.Now) return RuleResult.Fail("Cooldown");
            if (CBMEnergy.For(module.pawn).storedWd < ability.useWd * CBMModes.Consumption(module)) return RuleResult.Fail("NotEnoughEnergy");
            return RuleResult.Ok();
        }
        public static void Begin(Hediff_CBMModule module, CBMAbilitySpec ability)
        {
            Find.Targeter.BeginTargeting(new TargetingParameters { canTargetPawns = true, canTargetBuildings = true, canTargetItems = true,
                validator = info => Matches(module.pawn, ability, info.Thing) }, target => {
                var result = ImplantService.Modules(module.pawn).Contains(module)
                    ? CBMCasting.Queue(module, ability, target.Thing) : RuleResult.Fail("UnknownModule");
                Report(result);
            }, module.pawn);
        }
        public static void Report(RuleResult result)
        { if (!result.Allowed) Messages.Message(("RC_Error_" + result.Error).Translate(), MessageTypeDefOf.RejectInput, false); }
        public static List<FloatMenuOption> Options(Pawn caster, Thing target)
        {
            if (target == caster) return CBMControlMenu.Options(caster);
            var entries = new List<CBMControlEntry>();
            foreach (var m in ImplantService.Modules(caster).Where(m => m.status == ModuleStatus.Installed))
                foreach (var a in CBMAbilities.Specs(m).Where(a => a.kind == CBMAbilityKind.OneShot && Matches(caster, a, target)))
                {
                    var peers = ImplantService.Modules(caster).Where(o => o.def == m.def && o.Part == m.Part && o.status == ModuleStatus.Installed).OrderBy(o => o.loadID).ToList();
                    entries.Add(new CBMControlEntry { module = m, abilityId = a.id, operation = "Use",
                        label = ("RC_Implants".Translate() + ": ") + a.Label + " → " + target.LabelCap + " · " + m.Part.LabelCap + (peers.Count > 1 ? " #" + (peers.IndexOf(m) + 1) : ""),
                        check = () => CBMCasting.CanQueue(m, a, target), execute = () => CBMCasting.Queue(m, a, target) });
                }
            var options = CBMControlMenu.OptionsFor(entries);
            options.AddRange(CBMSupply.TargetOptions(caster, target));
            return options;
        }
    }
    public class FloatMenuOptionProvider_CBM : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => true;
        protected override bool CanSelfTarget => true;
        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context) => base.SelectedPawnValid(pawn, context) && CBMControlMenu.Visible(pawn);
        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing is Pawn) yield break; // Pawn overload is also called by vanilla.
            foreach (var option in ForTarget(clickedThing, context)) yield return option;
        }
        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context) => ForTarget(clickedPawn, context);
        private IEnumerable<FloatMenuOption> ForTarget(Thing target, FloatMenuContext context)
        {
            foreach (var caster in context.ValidSelectedPawns)
                foreach (var option in CBMTargeting.Options(caster, target))
                {
                    if (context.IsMultiselect) option.Label = caster.LabelShortCap + ": " + option.Label;
                    yield return option;
                }
        }
    }
    // Vanilla only builds order menus when a pawn is already selected.
    [HarmonyPatch(typeof(Selector), "HandleMapClicks")]
    public static class CBMUnselectedPawnMenu
    {
        static bool Prefix(Selector __instance)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1 || __instance.SelectedPawns.Any() || Find.Targeter.IsTargeting) return true;
            if (!TryOpenAt(__instance, UI.MouseMapPosition())) return true;
            Event.current.Use(); return false;
        }
        public static bool TryOpenAt(Selector selector, Vector3 position)
        {
            if (selector.SelectedPawns.Any() || Find.Targeter.IsTargeting) return false;
            var pawn = GenUI.ThingsUnderMouse(position, .8f, TargetingParameters.ForPawns()).OfType<Pawn>()
                .FirstOrDefault(p => CBMControlMenu.Visible(p) && !p.Position.Fogged(p.Map));
            if (pawn == null) return false;
            selector.ClearSelection(); selector.Select(pawn); CBMControlMenu.Open(pawn); return true;
        }
    }
    // Compatibility entry points for old callers; direct battery charging is retired.
    public static class CBMCharging
    {
        public static RuleResult CanCharge(Pawn pawn, Thing target) => RuleResult.Fail("DirectChargingDisabled");
        public static RuleResult Queue(Pawn pawn, Thing target) => CanCharge(pawn, target);
    }
}
