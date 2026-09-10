using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public enum CBMServiceKind { Clean, Sterilize, Repair, Recycle, Recover }
    public class CBMServiceExtension : DefModExtension
    {
        public CBMServiceKind service;
        public FloatRange recoveryDamage = new FloatRange(.1f, .4f);
        public float recoveryBurnoutChance = .15f;
        public int recycledSteel = 3;
        public override IEnumerable<string> ConfigErrors()
        {
            if (float.IsNaN(recoveryDamage.min) || float.IsNaN(recoveryDamage.max) || recoveryDamage.min < 0 || recoveryDamage.max > 1 || recoveryDamage.min > recoveryDamage.max
                || float.IsNaN(recoveryBurnoutChance) || recoveryBurnoutChance < 0 || recoveryBurnoutChance > 1 || recycledSteel < 0)
                yield return "Invalid implant recovery or recycling profile.";
        }
    }
    public class Recipe_ServiceCBM : RecipeWorker
    {
        private CBMServiceExtension Profile => recipe.GetModExtension<CBMServiceExtension>();
        public bool Accepts(Thing thing)
        {
            if (thing.DestroyedOrNull()) return false;
            if (Profile.service == CBMServiceKind.Recover)
                return thing is Corpse corpse && corpse.InnerPawn != null && ImplantService.Modules(corpse.InnerPawn).Any();
            var comp = thing.TryGetComp<CompCBMCondition>();
            if (comp == null) return true; // Ordinary supplies are filtered by the recipe's ingredient filters.
            bool burnt = ConditionRules.Burnt(comp.condition, comp.durability);
            if (Profile.service == CBMServiceKind.Recycle) return burnt;
            if (burnt) return false;
            switch (Profile.service)
            {
                case CBMServiceKind.Clean: return comp.condition.HasFlag(CBMCondition.Dirty);
                case CBMServiceKind.Sterilize: return !comp.condition.HasFlag(CBMCondition.Dirty) && comp.condition.HasFlag(CBMCondition.Unsterile);
                default: return !comp.condition.HasFlag(CBMCondition.Dirty) && comp.durability < 1;
            }
        }
        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
        {
            // The same physical implant/corpse survives processing. Supplies use vanilla consumption.
            if (!(ingredient is Corpse) && ingredient.TryGetComp<CompCBMCondition>() == null)
                base.ConsumeIngredient(ingredient, recipe, map);
        }
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            foreach (var item in ingredients.Where(t => !t.Destroyed && (t is Corpse || t.TryGetComp<CompCBMCondition>() != null)))
            {
                if (Accepts(item))
                {
                    var comp = item.TryGetComp<CompCBMCondition>();
                    switch (Profile.service)
                    {
                        case CBMServiceKind.Recover: Recover((Corpse)item, billDoer); break;
                        case CBMServiceKind.Clean: comp.condition = (comp.condition & ~CBMCondition.Dirty) | CBMCondition.Unsterile; break;
                        case CBMServiceKind.Repair: comp.durability = 1; comp.condition |= CBMCondition.Unsterile; break;
                        case CBMServiceKind.Sterilize:
                            // No instant sterilization fallback: a real chamber owns the item until completion.
                            (billDoer?.CurJob?.targetA.Thing as Building_CBMAutoclave)?.TryLoad(item, billDoer);
                            break;
                        case CBMServiceKind.Recycle:
                            if (Profile.recycledSteel > 0)
                            {
                                var steel = ThingMaker.MakeThing(ThingDefOf.Steel); steel.stackCount = Profile.recycledSteel;
                                CBMSurgery.ReturnItem(steel, billDoer, billDoer);
                            }
                            item.Destroy(); break;
                    }
                }
                CBMSurgery.ReturnItem(item, billDoer, billDoer);
            }
            PanelSnapshot.Invalidate();
        }
        private void Recover(Corpse corpse, Pawn worker)
        {
            // Batch removal prevents capacity-provider removal from recovering its children a second time.
            bool resolving = CapacityEvents.Resolving;
            CapacityEvents.Resolving = true;
            try
            {
                foreach (var module in ImplantService.Modules(corpse.InnerPawn).ToList())
                {
                    var recovered = CBMConditionUtility.MakeItem(module, true);
                    var comp = recovered.TryGetComp<CompCBMCondition>();
                    float skill = worker?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                    comp.durability = Math.Max(0, comp.durability - Profile.recoveryDamage.RandomInRange * (1 - skill / 40f));
                    if (Rand.Chance(Profile.recoveryBurnoutChance * (1 - skill / 40f)) || comp.durability <= 0)
                    { comp.condition |= CBMCondition.BurntOut; comp.durability = 0; comp.active = false; }
                    corpse.InnerPawn.health.RemoveHediff(module);
                    CBMSurgery.ReturnItem(recovered, worker, worker);
                }
            }
            finally { CapacityEvents.Resolving = resolving; }
        }
    }
    public class CBMAutoclaveExtension : DefModExtension
    {
        public int cycleTicks = 15000;
        public override IEnumerable<string> ConfigErrors()
        { if (cycleTicks <= 0) yield return "Autoclave cycleTicks must be positive."; }
    }
    public class Building_CBMAutoclave : Building_WorkTable, IThingHolder
    {
        private ThingOwner<Thing> contents;
        private int remainingTicks;
        private bool completed;
        private Pawn operatorPawn;
        public Building_CBMAutoclave() { contents = new ThingOwner<Thing>(this, true); }
        public bool Occupied => contents.Count > 0;
        public int RemainingTicks => remainingTicks;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, contents);
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "cbmChamber", this);
            Scribe_Values.Look(ref remainingTicks, "cbmCycleTicks");
            Scribe_Values.Look(ref completed, "cbmCycleComplete");
            Scribe_References.Look(ref operatorPawn, "cbmOperator");
        }
        public bool TryLoad(Thing item, Pawn worker = null)
        {
            var comp = item?.TryGetComp<CompCBMCondition>();
            if (comp == null || item.Destroyed || Occupied || !Spawned || !CurrentlyUsableForBills() || item.stackCount != 1
                || ConditionRules.Burnt(comp.condition, comp.durability) || comp.condition.HasFlag(CBMCondition.Dirty)
                || !comp.condition.HasFlag(CBMCondition.Unsterile)) return false;
            if (item.Spawned) item.DeSpawn();
            if (!contents.TryAddOrTransfer(item)) return false;
            remainingTicks = def.GetModExtension<CBMAutoclaveExtension>().cycleTicks; completed = false; operatorPawn = worker;
            return true;
        }
        public override void TickRare()
        {
            base.TickRare(); AdvanceCycle(250);
        }
        public void AdvanceCycle(int ticks)
        {
            if (!Spawned || !Occupied || ticks <= 0) return;
            if (!completed)
            {
                if (GetComp<CompPowerTrader>()?.PowerOn != true || GetComp<CompBreakdownable>()?.BrokenDown == true) return;
                remainingTicks = Math.Max(0, remainingTicks - ticks);
                if (remainingTicks > 0) return;
                var comp = contents[0].TryGetComp<CompCBMCondition>();
                if (!comp.condition.HasFlag(CBMCondition.Dirty) && !ConditionRules.Burnt(comp.condition, comp.durability))
                    comp.condition &= ~CBMCondition.Unsterile;
                completed = true;
            }
            contents.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Occupied)
            {
                foreach (var item in contents.ToList())
                {
                    var comp = item.TryGetComp<CompCBMCondition>();
                    if (!completed) comp.condition |= CBMCondition.Unsterile;
                    if (mode == DestroyMode.KillFinalize)
                    { comp.condition |= CBMCondition.Dirty | CBMCondition.Unsterile; comp.durability = Math.Max(0, comp.durability - .25f);
                        if (comp.durability == 0) { comp.condition |= CBMCondition.BurntOut; comp.active = false; } }
                    contents.Remove(item);
                    if (MapHeld == null || !GenPlace.TryPlaceThing(item, PositionHeld, MapHeld, ThingPlaceMode.Near))
                        Current.Game.GetComponent<CBMReturnedItems>().Hold(item, operatorPawn ?? MapHeld?.mapPawns.FreeColonists.FirstOrDefault());
                }
            }
            base.Destroy(mode);
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            if (Occupied && Faction == Faction.OfPlayer)
                yield return new Command_Action { defaultLabel = "RC_AutoclaveEject".Translate(), defaultDesc = "RC_AutoclaveEjectHelp".Translate(),
                    action = () => { if (Spawned) contents.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near); } };
        }
        public override string GetInspectString() => base.GetInspectString() + "\n" + (Occupied
            ? "RC_AutoclaveCycle".Translate(contents[0].LabelCap, remainingTicks.ToStringTicksToPeriod())
            : "RC_AutoclaveEmpty".Translate());
    }
    [HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.UsableForBillsAfterFueling))]
    public static class CBMAutoclaveBusy
    {
        static void Postfix(Building_WorkTable __instance, ref bool __result)
        { if (__instance is Building_CBMAutoclave chamber && chamber.Occupied) __result = false; }
    }
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ShouldDoNow))]
    public static class CBMServiceBenchScheduling
    {
        static void Postfix(Bill_Production __instance, ref bool __result)
        {
            // Old saves can retain the former prototype bills on vanilla crafting tables.
            if (__result && __instance.recipe.Worker is Recipe_ServiceCBM && __instance.billStack?.billGiver is Thing giver
                && !__instance.recipe.AllRecipeUsers.Contains(giver.def)) __result = false;
        }
    }
}
