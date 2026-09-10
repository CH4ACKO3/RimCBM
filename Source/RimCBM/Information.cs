using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimCBM
{
    public static class CBMInformation
    {
        private static string T(string key) => ("RC_Info_" + key).Translate();
        private static string Percent(float n) => n.ToString("0.##%");
        private static string Stats(List<StatModifier> stats) => string.Join("\n", stats.Select(s => s.stat.LabelCap + ": " + s.value.ToString("+0.##;-0.##;0")));
        public static IEnumerable<StatDrawEntry> Rows(HediffDef def, float durability = 1, RimCBM.Core.CBMCondition condition = RimCBM.Core.CBMCondition.None, Hediff_CBMModule module = null, CBMFuelState fuelState = null)
        {
            var e = def.GetModExtension<CBMExtension>();
            if (e == null) yield break;
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_Compatibility".Translate(), "", CBMAccess.Description(e), 4961);
            foreach (var mode in e.modes) yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_Mode".Translate(), mode.Label,
                "RC_ModeInfo".Translate(string.Join(", ", mode.abilities), mode.generationMultiplier, mode.consumptionMultiplier, mode.effectMultiplier, mode.exclusiveGroup ?? "—") + "\n" + string.Join(", ", mode.research.Select(r => r.LabelCap.ToString())), 4959);
            if (e.chargeAcceptanceWatts > 0) yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_ChargeStation".Translate(), e.chargeAcceptanceWatts + " W", "RC_ChargeHelp".Translate(), 4976);
            if (e.fuel != null) yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_Refill".Translate(), (module?.fuelState.units ?? fuelState?.units ?? 0).ToString("0.##") + "/" + e.fuel.capacity,
                "RC_FuelHelp".Translate() + "\n" + string.Join("\n", e.fuel.options.Select(o => o.item.LabelCap + ": " + o.unitsPerItem + " × " + o.energyWdPerUnit + " Wd × " + e.fuel.efficiency.ToStringPercent())), 4975);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, StatDefOf.Mass.LabelCap.ToString(), e.item.GetStatValueAbstract(StatDefOf.Mass).ToString("0.##") + " kg", "RC_Info_ImplantMass".Translate(), 5020);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_Info_BasePrice".Translate(), e.item.BaseMarketValue.ToStringMoney(), "RC_Info_BasePriceHelp".Translate(), 5010);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Condition"), CBMConditionUtility.Label(condition, durability) + " · " + Percent(durability), T("ConditionHelp"), 5000);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Cleanliness"), T(condition.HasFlag(Core.CBMCondition.Dirty) ? "Dirty" : "Clean"), T("HygieneHelp"), 4990);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Sterility"), T(condition.HasFlag(Core.CBMCondition.Unsterile) ? "Unsterile" : "Sterile"), T("HygieneHelp"), 4980);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Combat"), Percent(e.combat.hitChance),
                "RC_Info_CombatText".Translate(Percent(e.combat.hitChance), e.combat.hitPoints, e.combat.damageMultiplier, e.combat.hitWeight).ToString()
                + "\n" + string.Join("\n", e.combat.damageFactors.Select(f => f.damageDef.LabelCap + " ×" + f.multiplier)), 4970);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, "RC_FireWear".Translate(), "", "RC_Info_FireWear".Translate(100 * DefDatabase<CBMSettingsDef>.GetNamed("RimCBM_Settings").burningDurabilityPerSecond * e.combat.fireDamageMultiplier), 4965);
            foreach (var fault in e.faults)
                yield return new StatDrawEntry(StatCategoryDefOf.Implant, fault.labelKey.Translate(), Percent(fault.maxDurability), "RC_Info_Fault".Translate(Percent(fault.maxDurability), fault.intervalTicks.min / 60f, fault.intervalTicks.max / 60f, Percent(fault.chance), fault.damageAmount, fault.energyWd, T(fault.requiresEnergy ? "Yes" : "No"), T(fault.requiresActive ? "Yes" : "No"), T(fault.affectsBurnt ? "Yes" : "No")), 4962);
            foreach (var effect in e.damageEffects)
                yield return new StatDrawEntry(StatCategoryDefOf.Implant,
                    ("RC_Damage_" + (effect.workerClass == typeof(CBMDamageEffect_Ignite) ? "Ignite" : "Effect")).Translate(),
                    ("RC_Damage_" + effect.trigger).Translate(), "RC_Info_DamageEffect".Translate(("RC_Damage_" + effect.trigger).Translate(), Percent(effect.chance), T(effect.requiresActive ? "Yes" : "No"), e.damageEffectCooldownTicks / 60f), 4960);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Layout"), e.width + " × " + e.height,
                "RC_Info_LayoutText".Translate(e.width, e.height, T(e.rotatable ? "Yes" : "No"), T(e.hotSwappable ? "Yes" : "No"),
                    ("RC_Expansion_" + e.expansion).Translate().ToString() + (e.expansion == Core.ExpansionKind.None ? "" : e.expansion == Core.ExpansionKind.Board ? " " + e.bayWidth + " × " + e.bayHeight : " " + e.extension)), 4900);
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Power"), e.energyCapacityWd + " Wd",
                "RC_Info_PowerText".Translate(e.energyCapacityWd, e.generationWatts, e.idleWatts, e.activationWd, e.useWd).ToString()
                + "\n" + Stats(e.statOffsets) + "\n" + Stats(e.burstStatOffsets)
                + (e.useWd > 0 ? "\n" + "RC_Info_AbilityText".Translate(e.idleWatts, e.activationWd, e.useWd, e.useDurationTicks / 60f, e.useCooldownTicks / 60f, 0, 0, "RC_Target_Self".Translate(), 0).ToString() : ""), 4800);
            foreach (var a in e.abilities)
            {
                var description = "RC_Info_AbilityText".Translate(a.idleWatts, a.activationWd, a.useWd, a.durationTicks / 60f, a.cooldownTicks / 60f,
                    a.healAmount, a.damageAmount + " " + (a.damageDef?.label ?? ""), string.Join(", ", a.targetKinds.Select(k => ("RC_Target_" + k).Translate().ToString())), a.range).ToString();
                description += "\n" + Stats(a.statOffsets);
                var worldRule = (a.mapOnly || a.damageAmount > 0 || a.effects.Any(x => x.Worker.RequiresMap(x)) ? "RC_WorldMapOnly" : "RC_WorldSelf").Translate();
                description += "\n" + (a.kind == CBMAbilityKind.OneShot ? "RC_Info_CastRules".Translate(a.warmupTicks / 60f, worldRule) : "RC_Info_WorldRules".Translate(worldRule));
                foreach (var effect in a.effects) description += "\n" + ("RC_Effect_" + effect.kind).Translate() + ": "
                    + (effect.kind == CBMEffectKind.Projectile ? effect.projectile?.LabelCap.ToString() : effect.kind == CBMEffectKind.Tend ? effect.quality.ToStringPercent() : effect.amount.ToString("0.##"))
                    + " " + (effect.hediff?.label ?? effect.damageDef?.label ?? "");
                if (a.research.Count > 0) description += "\n" + "RC_CBMResearch".Translate() + ": " + string.Join(", ", a.research.Select(r => r.LabelCap.ToString()));
                if (a.kind == CBMAbilityKind.Reactive) description += "\n" + ("RC_Trigger_" + a.trigger).Translate() + "\n" + "RC_Info_Reactive".Translate(a.threshold.ToStringPercent(), a.minimumDamage, a.reserveWd);
                if (module != null)
                {
                    var state = module.abilityStates.FirstOrDefault(s => s.id == a.id);
                    description += "\n" + ("RC_" + (CBMAbilities.Effective(module, a) ? "On" : "Off")).Translate();
                    if (state != null) description += "\n" + "RC_Info_Cooldown".Translate(System.Math.Max(0, state.cooldownUntil - CBMAbilities.Now) / 60f);
                }
                yield return new StatDrawEntry(StatCategoryDefOf.Implant, a.Label, ("RC_Ability_" + a.kind).Translate().ToString(), description, 4700);
            }
            var restrictions = string.Join(", ", e.tags) + "\n" + "RC_Info_Restrictions".Translate(string.Join(", ", e.requiredAllTags), string.Join(", ", e.requiredAnyTags), string.Join(", ", e.forbiddenTags));
            foreach (var exclusion in e.exclusions) restrictions += "\n" + ("RC_Scope_" + exclusion.scope).Translate() + ": " + string.Join(", ", exclusion.modules.Select(d => d.LabelCap.ToString()).Concat(exclusion.tags));
            foreach (var effect in CBMTagEffects.For(def))
                restrictions += "\n" + effect.tag + ": " + "RC_Info_TagEffect".Translate(effect.consumptionMultiplier, effect.effectMultiplier)
                    + (effect.empSensitive ? "\n" + "RC_Info_EMP".Translate().ToString() + " " + effect.empDurationTicks / 60f + "s" : "");
            yield return new StatDrawEntry(StatCategoryDefOf.Implant, T("Tags"), string.Join(", ", e.tags), restrictions, 4600);
            foreach (var installing in new[] { true, false })
            {
                var p = installing ? e.installationSurgery : e.removalSurgery;
                float conditionFactor = CBMSurgicalOutcome.ConditionFactor(durability);
                var details = "RC_Info_ChanceText".Translate(Percent(p.successFactor), Percent(conditionFactor), Percent(durability));
                yield return new StatDrawEntry(StatCategoryDefOf.Surgery, T(installing ? "Install" : "Remove"), Percent(.98f * p.successFactor * conditionFactor), details, installing ? 4000 : 3900);
                var risk = "RC_Info_RiskText".Translate(Percent(1 - p.severeChance - p.burnoutChance - p.retainedChance), Percent(p.severeChance), Percent(p.burnoutChance),
                    Percent(p.minorDamage.min) + "–" + Percent(p.minorDamage.max), Percent(p.severeDamage.min) + "–" + Percent(p.severeDamage.max),
                    p.minorPatientDamage, p.severePatientDamage, p.burnoutPatientDamage).ToString();
                if (installing && p.retainedChance > 0) risk += "\n\n" + "RC_Info_RetainedRisk".Translate(Percent(p.retainedChance));
                yield return new StatDrawEntry(StatCategoryDefOf.Surgery, T(installing ? "InstallRisk" : "RemoveRisk"), "", risk, installing ? 3800 : 3700);
            }
        }
    }
    [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")]
    public static class CBMItemInformation
    {
        static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance, StatRequest req)
        {
            foreach (var row in __result) yield return row;
            var def = CBMItems.Implant(__instance);
            if (def == null) yield break;
            var comp = req.Thing?.TryGetComp<CompCBMCondition>();
            foreach (var row in CBMInformation.Rows(def, comp?.durability ?? 1, comp?.condition ?? Core.CBMCondition.None, fuelState: comp?.fuelState)) yield return row;
        }
    }
}
