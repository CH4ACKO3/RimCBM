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
    public enum CBMSurgeryFailureKind { Minor, Severe, Burnout, Retained }
    public class CBMSurgeryProfile
    {
        public float successFactor = 1f, severeChance = .2f, burnoutChance = .05f, retainedChance;
        public FloatRange minorDamage = new FloatRange(.15f, .3f), severeDamage = new FloatRange(.45f, .7f);
        public int minorPatientDamage = 3, severePatientDamage = 12, burnoutPatientDamage = 25;
        public CBMSurgeryFailureKind Result(float roll) => roll < burnoutChance ? CBMSurgeryFailureKind.Burnout
            : roll < burnoutChance + severeChance ? CBMSurgeryFailureKind.Severe
            : roll < burnoutChance + severeChance + retainedChance ? CBMSurgeryFailureKind.Retained : CBMSurgeryFailureKind.Minor;
        public IEnumerable<string> ConfigErrors()
        {
            foreach (float n in new[] { successFactor, severeChance, burnoutChance, retainedChance, minorDamage.min, minorDamage.max, severeDamage.min, severeDamage.max })
                if (float.IsNaN(n) || n < 0 || n > 1) yield return "Surgical probability/durability damage must be in 0..1.";
            if (severeChance + burnoutChance + retainedChance > 1 || minorDamage.min > minorDamage.max || severeDamage.min > severeDamage.max
                || minorPatientDamage < 0 || severePatientDamage < 0 || burnoutPatientDamage < 0) yield return "Invalid surgical failure profile.";
        }
    }
    public static class CBMSurgicalOutcome
    {
        public static float ConditionFactor(float durability) => .7f + .3f * Mathf.Clamp01(durability);
        public static CBMSurgeryProfile Profile(RecipeDef recipe, Bill bill, out float durability)
        {
            var module = (bill as Bill_RemoveCBM)?.module;
            durability = module?.durability ?? 1;
            return module != null ? module.def.GetModExtension<CBMExtension>().removalSurgery
                : recipe.Worker is Recipe_PrepareCBM ? recipe.addsHediff?.GetModExtension<CBMExtension>()?.installationSurgery : null;
        }
        public static float Estimate(HediffDef def, float durability, Pawn patient, BodyPartRecord part, Pawn doctor)
        {
            var recipe = DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.addsHediff == def && r.Worker is Recipe_PrepareCBM);
            var bill = new Bill_Medical(recipe, null);
            bill.consumedMedicine[ThingDefOf.MedicineIndustrial] = 2;
            // GetQuality only reads conditions; it neither rolls nor consumes inspirations/materials.
            return (recipe.surgeryOutcomeEffect?.GetQuality(recipe, doctor, patient, new List<Thing>(), part, bill) ?? 1) * ConditionFactor(durability);
        }
        public static void FailedInstall(Thing item, Pawn patient, Pawn doctor, BodyPartRecord part, CBMSurgeryProfile profile, HediffDef implantDef = null)
        {
            if (item == null) return;
            var physical = item.stackCount > 1 ? item.SplitOff(1) : item;
            var comp = physical.TryGetComp<CompCBMCondition>();
            var kind = profile.Result(Rand.Value);
            float before = comp?.durability ?? 1; bool wasActive = comp?.active ?? true;
            bool wasBurnt = comp != null && ConditionRules.Burnt(comp.condition, comp.durability);
            if (comp != null)
            {
                comp.condition |= CBMCondition.Dirty | CBMCondition.Unsterile;
                comp.durability = kind == CBMSurgeryFailureKind.Burnout ? 0 : Mathf.Max(0, comp.durability - Damage(profile, kind));
                if (comp.durability == 0) { comp.condition |= CBMCondition.BurntOut; comp.active = false; }
            }
            bool retained = false;
            if (kind == CBMSurgeryFailureKind.Retained)
            {
                var def = implantDef ?? CBMItems.Implant(physical.def);
                if (def?.GetModExtension<CBMExtension>()?.item == physical.def)
                {
                    var module = (Hediff_CBMModule)HediffMaker.MakeHediff(def, patient, part);
                    CBMConditionUtility.ApplyItem(physical, module);
                    module.retainedRemnant = true; module.status = ModuleStatus.Installed; module.active = false;
                    patient.health.AddHediff(module, part);
                    retained = ImplantService.Modules(patient).Contains(module);
                    if (retained)
                    {
                        physical.Destroy();
                        CBMDamageEvents.Notify(module, before, wasBurnt, wasActive, CBMDamageCause.Surgery);
                    }
                }
            }
            if (!retained) CBMSurgery.ReturnItem(physical, patient, doctor);
            Injure(patient, doctor, part, profile, kind, physical.LabelCap);
        }
        public static void FailedRemoval(Hediff_CBMModule module, Pawn doctor)
        {
            var profile = module.def.GetModExtension<CBMExtension>().removalSurgery;
            var kind = profile.Result(Rand.Value);
            module.condition |= CBMCondition.Dirty | CBMCondition.Unsterile;
            CBMConditionUtility.Damage(module, kind == CBMSurgeryFailureKind.Burnout ? 1 : Damage(profile, kind), CBMDamageCause.Surgery);
            Injure(module.pawn, doctor, module.Part, profile, kind, module.LabelCap);
        }
        private static float Damage(CBMSurgeryProfile p, CBMSurgeryFailureKind kind) => (kind == CBMSurgeryFailureKind.Severe || kind == CBMSurgeryFailureKind.Retained ? p.severeDamage : p.minorDamage).RandomInRange;
        private static void Injure(Pawn patient, Pawn doctor, BodyPartRecord part, CBMSurgeryProfile profile, CBMSurgeryFailureKind kind, string label)
        {
            int damage = kind == CBMSurgeryFailureKind.Burnout ? profile.burnoutPatientDamage
                : kind == CBMSurgeryFailureKind.Severe || kind == CBMSurgeryFailureKind.Retained ? profile.severePatientDamage : profile.minorPatientDamage;
            if (!patient.Dead && damage > 0)
            {
                CBMCombat.Suppressed++;
                try { HealthUtility.GiveRandomSurgeryInjuries(patient, damage, part); }
                finally { CBMCombat.Suppressed--; }
            }
            Messages.Message("RC_SurgeryFailureMessage".Translate(patient.LabelShortCap, label, ("RC_Surgery_" + kind).Translate()), patient, MessageTypeDefOf.NegativeHealthEvent, true);
        }
    }
    [HarmonyPatch(typeof(SurgeryOutcomeEffectDef), "GetQuality")]
    public static class CBMSurgicalQuality
    {
        static void Postfix(RecipeDef recipe, List<Thing> ingredients, Bill bill, ref float __result)
        {
            var profile = CBMSurgicalOutcome.Profile(recipe, bill, out var durability);
            if (profile == null) return;
            if (recipe.Worker is Recipe_PrepareCBM)
                durability = ingredients?.FirstOrDefault(t => t.def == recipe.addsHediff.GetModExtension<CBMExtension>().item)?.TryGetComp<CompCBMCondition>()?.durability ?? 1;
            __result = Mathf.Clamp01(__result) * profile.successFactor * CBMSurgicalOutcome.ConditionFactor(durability);
        }
    }
}
