using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimCBM
{
    public class Dialog_CBMAbilities : Window
    {
        private readonly Hediff_CBMModule module;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(560, 560);
        public Dialog_CBMAbilities(Hediff_CBMModule module)
        { this.module = module; doCloseX = true; closeOnClickedOutside = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect)
        {
            if (!ImplantService.Modules(module.pawn).Contains(module)) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 28, 32), module.def.LabelCap);
            Text.Font = GameFont.Small;
            var energy = CBMEnergy.For(module.pawn);
            Widgets.Label(new Rect(0, 36, rect.width, 25), energy.storedWd.ToString("0.##") + " / " + energy.capacityWd.ToString("0.##") + " Wd");
            var specs = CBMAbilities.Specs(module);
            Rect outer = new Rect(0, 68, rect.width, rect.height - 68), inner = new Rect(0, 0, outer.width - 18, specs.Count * 108);
            Widgets.BeginScrollView(outer, ref scroll, inner);
            float y = 0;
            foreach (var a in specs)
            {
                var state = CBMAbilities.State(module, a);
                float scale = CBMModes.Consumption(module);
                Widgets.DrawMenuSection(new Rect(0, y, inner.width, 102));
                Widgets.Label(new Rect(8, y + 5, inner.width - 105, 25), a.Label + " · " + ("RC_Ability_" + a.kind).Translate());
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(8, y + 31, inner.width - 18, 23), CBMAbilities.Status(module, a) + " · " +
                    (a.Continuous ? (a.idleWatts * scale).ToString("0.##") + " W / " + (a.activationWd * scale).ToString("0.##") + " Wd" : (a.useWd * scale).ToString("0.##") + " Wd"));
                Text.Font = GameFont.Small;
                if (a.kind == CBMAbilityKind.Toggle || a.kind == CBMAbilityKind.Reactive)
                {
                    if (Widgets.ButtonText(new Rect(inner.width - 92, y + 5, 84, 25), (state.enabled ? "RC_TurnOff" : "RC_TurnOn").Translate())) Report(CBMAbilities.Toggle(module, a));
                }
                else if (a.kind == CBMAbilityKind.OneShot)
                {
                    bool targeted = !a.targetKinds.Contains(CBMTargetKind.Self);
                    var result = targeted ? CBMTargeting.CanBegin(module, a) : CBMCasting.CanQueue(module, a);
                    if (Widgets.ButtonText(new Rect(inner.width - 92, y + 5, 84, 25), (targeted ? "RC_CBMSelectTarget" : "RC_Use").Translate(), active: result.Allowed)) { if (targeted) { Close(); CBMTargeting.Begin(module, a); } else Report(CBMCasting.Queue(module, a)); }
                    if (!result.Allowed) TooltipHandler.TipRegion(new Rect(inner.width - 92, y + 5, 84, 25), ("RC_Error_" + result.Error).Translate());
                }
                bool editable = ImplantService.CanEdit(module.pawn);
                Text.Font = GameFont.Tiny;
                TooltipHandler.TipRegion(new Rect(8, y + 69, 110, 24), "RC_PriorityHint".Translate());
                if (a.kind != CBMAbilityKind.OneShot && Widgets.ButtonText(new Rect(8, y + 69, 110, 24), "RC_Priority".Translate() + " " + state.priority, active: editable))
                    Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, 4).Select(n => new FloatMenuOption(n.ToString(), () => state.priority = n)).ToList()));
                if (a.kind == CBMAbilityKind.Reactive)
                {
                    if (Widgets.ButtonText(new Rect(126, y + 69, 152, 24), "RC_Reserve".Translate() + " " + state.reserveWd + " Wd", active: editable))
                        Find.WindowStack.Add(new FloatMenu(new[] { 0f, 5f, 10f, 25f, 50f }.Select(n => new FloatMenuOption(n + " Wd", () => state.reserveWd = n)).ToList()));
                    if (a.trigger != CBMTrigger.DamageTaken && Widgets.ButtonText(new Rect(286, y + 69, 130, 24), "RC_Threshold".Translate() + " " + state.threshold.ToStringPercent(), active: editable))
                        Find.WindowStack.Add(new FloatMenu(new[] { .1f, .25f, .5f, .75f, .9f }.Select(n => new FloatMenuOption(n.ToStringPercent(), () => state.threshold = n)).ToList()));
                    Widgets.Label(new Rect(8, y + 49, inner.width - 16, 18), ("RC_Trigger_" + a.trigger).Translate());
                }
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(new Rect(0, y, inner.width - 98, 48), "RC_AbilityTiming".Translate(a.durationTicks.ToStringTicksToPeriod(), a.cooldownTicks.ToStringTicksToPeriod()));
                y += 108;
            }
            Widgets.EndScrollView();
            Text.Font = GameFont.Small;
        }
        private static void Report(Core.RuleResult result)
        { if (!result.Allowed) Messages.Message(("RC_Error_" + result.Error).Translate(), MessageTypeDefOf.RejectInput, false); }
    }
}
