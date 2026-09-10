using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public static class CBMSurgery
    {
        public static bool BodyFits(Pawn pawn, BodyPartRecord part, HediffDef def)
        {
            if (part == null || ImplantService.Profile(pawn, part) == null) return false;
            var rules = ImplantService.Build(pawn);
            var module = new ModuleState { Id = "surgery-check", RootId = ImplantService.RootId(pawn, part), SpecId = def.defName };
            foreach (var board in rules.Boards().Values.Where(b => b.RootId == module.RootId))
                foreach (bool rotated in new[] { false, true })
                    for (int y = 0; y < board.Height; y++) for (int x = 0; x < board.Width; x++)
                        if (rules.EligibleFootprint(module, board, x, y, rotated, false)) return true;
            return false;
        }
        public static RuleResult CanPrepare(Pawn pawn, BodyPartRecord part, HediffDef def, Bill currentBill = null)
        {
            if (!ImplantService.CanEdit(pawn)) return RuleResult.Fail("ReadOnly");
            if (!BodyFits(pawn, part, def)) return RuleResult.Fail("SlotTagsMismatch");
            var root = ImplantService.RootId(pawn, part);
            var rules = ImplantService.Build(pawn);
            var queued = new List<Bill_Medical>();
            foreach (var bill in pawn.BillStack.Bills)
            {
                if (bill == currentBill) break;
                if (bill is Bill_Medical medical && medical.recipe.Worker is Recipe_PrepareCBM && !medical.suspended)
                    queued.Add(medical);
            }
            return rules.CanAdd(new ModuleState { SpecId = def.defName, RootId = root }, queued.Select((b, i) =>
                new ModuleState { Id = "queued-surgery-" + i, SpecId = b.recipe.addsHediff.defName,
                    RootId = ImplantService.RootId(pawn, b.Part), Status = ModuleStatus.SurgeryPrepared }));
        }
        public static RuleResult QueueInstall(Pawn pawn, BodyPartRecord part, HediffDef def)
        {
            var result = CanPrepare(pawn, part, def);
            if (!result.Allowed) return result;
            var recipe = DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r => r.addsHediff == def && r.Worker is Recipe_PrepareCBM);
            if (recipe == null) return RuleResult.Fail("UnknownModule");
            var bill = new Bill_Medical(recipe, null);
            pawn.BillStack.AddBill(bill); bill.Part = part;
            return RuleResult.Ok();
        }
        public static RuleResult QueueRemoval(Pawn pawn, Hediff_CBMModule module)
        {
            if (!ImplantService.CanEdit(pawn)) return RuleResult.Fail("ReadOnly");
            var result = ImplantService.Build(pawn).Detach(module.GetUniqueLoadID(), true, false);
            if (!result.Allowed) return result;
            if (pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().Any(b => b.module == module)) return RuleResult.Fail("AlreadyQueued");
            var bill = new Bill_RemoveCBM(DefDatabase<RecipeDef>.GetNamed("RimCBM_Remove"), module);
            pawn.BillStack.AddBill(bill); bill.Part = module.Part;
            return RuleResult.Ok();
        }
        public static void ReturnItem(Thing item, Pawn patient, Pawn surgeon)
        {
            if (item == null || item.Destroyed || item.Spawned || item.ParentHolder != null) return;
            var host = surgeon?.MapHeld != null ? surgeon : patient;
            if (host?.MapHeld != null && GenPlace.TryPlaceThing(item, host.PositionHeld, host.MapHeld, ThingPlaceMode.Near)) return;
            if (host?.inventory != null && host.inventory.innerContainer.TryAdd(item)) return;
            // Final fallback is an owned, saved container, not an unreferenced unspawned thing.
            Current.Game.GetComponent<CBMReturnedItems>().Hold(item, patient ?? surgeon);
        }
    }
    [HarmonyPatch(typeof(Bill_Medical), "ShouldDoNow")]
    public static class CBMSurgeryExclusionScheduling
    {
        static void Postfix(Bill_Medical __instance, ref bool __result)
        {
            if (__result && __instance.recipe.Worker is Recipe_PrepareCBM)
                __result = CBMSurgery.CanPrepare(__instance.GiverPawn, __instance.Part, __instance.recipe.addsHediff, __instance).Allowed;
        }
    }
    public class CBMReturnedItem : IExposable
    {
        public Thing item; public Pawn owner;
        public void ExposeData() { Scribe_Deep.Look(ref item, "item"); Scribe_References.Look(ref owner, "owner"); }
    }
    public class CBMReturnedItems : GameComponent
    {
        private List<CBMReturnedItem> pending = new List<CBMReturnedItem>();
        public CBMReturnedItems(Game game) { }
        public void Hold(Thing item, Pawn owner) { if (!pending.Any(p => p.item == item)) pending.Add(new CBMReturnedItem { item = item, owner = owner }); }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref pending, "cbmReturnedItems", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pending == null) pending = new List<CBMReturnedItem>();
        }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var entry in pending.ToList())
            {
                if (entry.item.DestroyedOrNull()) { pending.Remove(entry); continue; }
                var owner = entry.owner;
                if (owner?.MapHeld != null && GenPlace.TryPlaceThing(entry.item, owner.PositionHeld, owner.MapHeld, ThingPlaceMode.Near)
                    || owner?.inventory != null && owner.inventory.innerContainer.TryAdd(entry.item)) pending.Remove(entry);
            }
        }
    }
    [HarmonyPatch(typeof(Bill_Medical), "Notify_IterationCompleted")]
    public static class CBMSurgeryReturnIngredients
    {
        static void Prefix(Bill_Medical __instance, out Pawn __state) => __state = __instance.recipe.Worker is Recipe_PrepareCBM ? __instance.GiverPawn : null;
        static void Finalizer(Pawn __state, Pawn billDoer, List<Thing> ingredients)
        {
            if (__state == null || ingredients == null) return;
            foreach (var item in ingredients.Where(t => t.TryGetComp<CompCBMCondition>() != null)) CBMSurgery.ReturnItem(item, __state, billDoer);
        }
    }
}
