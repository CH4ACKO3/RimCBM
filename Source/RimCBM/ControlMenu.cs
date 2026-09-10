using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using Verse;
using UnityEngine;

namespace RimCBM
{
    public class CBMControlEntry
    {
        public Hediff_CBMModule module;
        public string abilityId, operation, label;
        public Func<RuleResult> check, execute;
        public RuleResult Available() => ImplantService.CanEdit(module.pawn) && ImplantService.Modules(module.pawn).Contains(module)
            ? check() : RuleResult.Fail("ReadOnly");
        public RuleResult Invoke() { var result = Available(); return result.Allowed ? execute() : result; }
    }
    public static class CBMControlMenu
    {
        public static bool Visible(Pawn pawn) => ImplantService.CanEdit(pawn) && ImplantService.Modules(pawn).Any(m => m.status == ModuleStatus.Installed);
        public static List<CBMControlEntry> Entries(Pawn pawn)
        {
            var result = new List<CBMControlEntry>();
            var modules = ImplantService.Modules(pawn).Where(m => m.status == ModuleStatus.Installed)
                .OrderBy(m => pawn.RaceProps.body.AllParts.IndexOf(m.Part)).ThenBy(m => m.def.defName).ThenBy(m => m.loadID).ToList();
            foreach (var m in modules)
            {
                var peers = modules.Where(o => o.Part == m.Part && o.def == m.def).ToList();
                string identity = m.Part.LabelCap + (peers.Count > 1 ? " #" + (peers.IndexOf(m) + 1) : "");
                var ext = m.def.GetModExtension<CBMExtension>();
                foreach (var a in ext.abilities)
                {
                    if (a.kind == CBMAbilityKind.Passive) continue;
                    string prefix = a.Label + " · " + identity;
                    if (a.kind == CBMAbilityKind.OneShot && !a.targetKinds.Contains(CBMTargetKind.Self))
                        result.Add(new CBMControlEntry { module = m, abilityId = a.id, operation = "Target",
                            label = "RC_CBMSelectTarget".Translate() + ": " + prefix,
                            check = () => CBMTargeting.CanBegin(m, a), execute = () => { CBMTargeting.Begin(m, a); return RuleResult.Ok(); } });
                    else if (a.kind == CBMAbilityKind.OneShot)
                        result.Add(new CBMControlEntry { module = m, abilityId = a.id, operation = "Use",
                            label = "RC_Use".Translate() + ": " + prefix + " · " + (a.useWd * CBMModes.Consumption(m)).ToString("0.##") + " Wd",
                            check = () => CBMCasting.CanQueue(m, a), execute = () => CBMCasting.Queue(m, a) });
                    else
                    {
                        bool desired = !CBMAbilities.State(m, a).enabled;
                        result.Add(new CBMControlEntry { module = m, abilityId = a.id, operation = "Toggle",
                            label = (desired ? "RC_TurnOn" : "RC_TurnOff").Translate() + ": " + prefix
                                + (a.kind == CBMAbilityKind.Reactive ? " · " + "RC_Ability_Reactive".Translate().ToString() : ""),
                            check = () => m.HardwareReady ? RuleResult.Ok() : RuleResult.Fail("NotPowered"),
                            execute = () => CBMAbilities.State(m, a).enabled == desired ? RuleResult.Ok() : CBMAbilities.Toggle(m, a) });
                    }
                }
                // Legacy generators and overdrive remain controllable from the same menu.
                if (ext.abilities.Count != 0) continue;
                if (ext.generationWatts > 0 || ext.idleWatts > 0 || ext.activationWd > 0 || ext.statOffsets.Count > 0 || ext.useDurationTicks > 0)
                {
                    bool desired = !m.active;
                    result.Add(new CBMControlEntry { module = m, operation = "Toggle",
                        label = (desired ? "RC_TurnOn" : "RC_TurnOff").Translate() + ": " + m.def.LabelCap + " · " + identity,
                        check = () => m.HardwareReady ? RuleResult.Ok() : RuleResult.Fail("NotPowered"),
                        execute = () => m.active == desired ? RuleResult.Ok() : CBMEnergy.Toggle(m) });
                }
                if (ext.useDurationTicks > 0)
                    result.Add(new CBMControlEntry { module = m, operation = "Use",
                        label = "RC_Use".Translate() + ": " + m.def.LabelCap + " · " + identity,
                        check = () => CBMEnergy.CanUse(m), execute = () => CBMEnergy.Use(m) });
            }
            return result;
        }
        public static List<FloatMenuOption> Options(Pawn pawn)
        {
            var options = OptionsFor(Entries(pawn));
            options.AddRange(CBMSupply.PolicyOptions(pawn));
            options.AddRange(CBMModes.Options(pawn));
            if (options.Count == 0) options.Add(new FloatMenuOption("RC_NoCBMControls".Translate(), null));
            options.Insert(0, new FloatMenuOption(CBMEnergy.Display(pawn), null));
            return options;
        }
        public static List<FloatMenuOption> OptionsFor(IEnumerable<CBMControlEntry> entries)
        {
            return entries.Select(entry => {
                var allowed = entry.Available();
                string reason = allowed.Allowed ? null : ("RC_Error_" + allowed.Error).Translate().ToString();
                var option = new FloatMenuOption(entry.label + (reason == null ? "" : " (" + reason + ")"), allowed.Allowed ? (Action)(() => {
                    var result = entry.Invoke();
                    if (!result.Allowed) Messages.Message(("RC_Error_" + result.Error).Translate(), MessageTypeDefOf.RejectInput, false);
                }) : null);
                option.tooltip = entry.module.def.LabelCap + "\n" + entry.module.Part.LabelCap + (reason == null ? "" : "\n" + reason);
                return option;
            }).ToList();
        }
        public static void Open(Pawn pawn) => Find.WindowStack.Add(new FloatMenu(Options(pawn)));
    }
    public class Command_CBMControl : Command_Action
    {
        private readonly Pawn pawn;
        public Command_CBMControl(Pawn pawn)
        {
            this.pawn = pawn;
            defaultLabel = "RC_Implants".Translate();
            defaultDesc = "RC_CBMControlDesc".Translate(pawn.LabelShortCap);
            icon = DefDatabase<ThingDef>.GetNamed("RimCBM_BatteryItem").uiIcon;
            action = () => CBMControlMenu.Open(pawn);
        }
        public override string Desc => base.Desc + "\n\n" + CBMEnergy.Display(pawn);
        protected override GizmoResult GizmoOnGUIInt(Rect rect, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUIInt(rect, parms);
            var energy = CBMEnergy.For(pawn);
            var bar = new Rect(rect.x + 5, rect.yMax - 8, rect.width - 10, 5);
            Widgets.DrawBoxSolid(bar, new Color(.12f, .14f, .15f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * (energy.capacityWd > 0 ? Mathf.Clamp01(energy.storedWd / energy.capacityWd) : 0), bar.height), new Color(.45f, .60f, .49f));
            return result;
        }
        public override bool GroupsWith(Gizmo other) => false;
    }
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class CBMControlGizmoPatch
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var gizmo in __result) yield return gizmo;
            if (CBMControlMenu.Visible(__instance)) yield return new Command_CBMControl(__instance);
        }
    }
}
