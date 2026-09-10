using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimCBM.Core;
using Verse;

namespace RimCBM
{
    // Explicit before/after event API for future exoskeletons and other slot providers.
    public static class CapacityEvents
    {
        [ThreadStatic] public static bool Resolving;
        [ThreadStatic] private static Dictionary<Pawn, Change> changes;
        public sealed class Change
        {
            public Pawn Pawn;
            public Dictionary<string, Board> Before;
            public SlotLossReason Reason;
            internal int Depth;
        }
        public static Change Begin(Pawn pawn, SlotLossReason reason)
        {
            if (Resolving || pawn == null || !ImplantService.Modules(pawn).Any()) return null;
            if (changes == null) changes = new Dictionary<Pawn, Change>();
            if (!changes.TryGetValue(pawn, out var current)) changes[pawn] = current = new Change { Pawn = pawn, Before = ImplantService.Build(pawn).Boards(), Reason = reason };
            if (reason == SlotLossReason.Destroyed) current.Reason = reason;
            current.Depth++;
            return current;
        }
        public static void End(Change change)
        {
            if (change == null || --change.Depth > 0) return;
            changes.Remove(change.Pawn);
            CBMCapacityComponent.Reconcile(change.Pawn, change.Reason, change.Before);
        }
    }
    [HarmonyPatch]
    public static class CBMHealthCapacityEvents
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Pawn_HealthTracker), "AddHediff", new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) });
            yield return AccessTools.Method(typeof(Pawn_HealthTracker), "RemoveHediff");
            yield return AccessTools.Method(typeof(Pawn_HealthTracker), "RestorePart");
        }
        static void Prefix(Pawn ___pawn, object[] __args, out CapacityEvents.Change __state)
        {
            var damage = __args.OfType<DamageInfo>().FirstOrDefault();
            __state = CapacityEvents.Begin(___pawn, damage.Def != null && damage.Def != RimWorld.DamageDefOf.SurgicalCut ? SlotLossReason.Destroyed : SlotLossReason.Removed);
        }
        static Exception Finalizer(Exception __exception, CapacityEvents.Change __state)
        {
            CapacityEvents.End(__state);
            return __exception;
        }
    }
}

namespace RimCBM
{
    // A replacement surgery may briefly restore the natural part before adding the new prosthesis.
    // Only its final capacity is authoritative.
    [HarmonyPatch(typeof(RimWorld.Recipe_InstallArtificialBodyPart), "ApplyOnPawn")]
    public static class CBMReplacementCapacityEvent
    {
        static void Prefix(Pawn pawn, out CapacityEvents.Change __state) => __state = CapacityEvents.Begin(pawn, SlotLossReason.Removed);
        static Exception Finalizer(Exception __exception, CapacityEvents.Change __state)
        { CapacityEvents.End(__state); return __exception; }
    }
}

namespace RimCBM
{
    [HarmonyPatch(typeof(RimWorld.Bill), "IsFixedOrAllowedIngredient", new[] { typeof(Thing) })]
    public static class CBMIngredientConditionFilter
    {
        static void Postfix(RimWorld.Bill __instance, Thing thing, ref bool __result)
        {
            if (__result && __instance.recipe.Worker is Recipe_PrepareCBM)
                __result = CBMConditionUtility.CanInstall(thing).Allowed;
            if (__result && __instance.recipe.Worker is Recipe_ServiceCBM service) __result = service.Accepts(thing);
        }
    }
}
