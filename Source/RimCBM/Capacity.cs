using System;
using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public class CBMLossPolicyDef : Def
    {
        public float removedDamageChance = .35f, destroyedDamageChance = .85f;
        public float removedDamage = .25f, destroyedDamage = .65f;
        public float removedRecoveryChance = .95f, destroyedRecoveryChance = .35f;
        public float removedBurnChance = .02f, destroyedBurnChance = .70f;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (float value in new[] { removedDamageChance, destroyedDamageChance, removedDamage, destroyedDamage,
                removedRecoveryChance, destroyedRecoveryChance, removedBurnChance, destroyedBurnChance })
                if (float.IsNaN(value) || value < 0 || value > 1) yield return "CBM loss policy values must be between 0 and 1.";
        }
    }
    public class CBMCapacityComponent : GameComponent
    {
        private float nextCheck;
        private sealed class Observed { public Dictionary<string, Board> Boards; public string State; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, Observed> observed = new System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, Observed>();
        public CBMCapacityComponent(Game game) { }
        public override void GameComponentUpdate()
        {
            if (UnityEngine.Time.realtimeSinceStartup < nextCheck || LongEventHandler.AnyEventNowOrWaiting) return;
            nextCheck = UnityEngine.Time.realtimeSinceStartup + 1f;
            foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList())
                if (ImplantService.Modules(pawn).Any()) Reconcile(pawn);
        }
        private static bool SameBoards(Dictionary<string, Board> a, Dictionary<string, Board> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            foreach (var pair in a)
            {
                if (!b.TryGetValue(pair.Key, out var next) || pair.Value.Width != next.Width || pair.Value.Height != next.Height || pair.Value.Cells.Count != next.Cells.Count) return false;
                foreach (var cell in pair.Value.Cells)
                {
                    if (!next.Cells.TryGetValue(cell.Key, out var other) || !cell.Value.Tags.SetEquals(other.Tags) || !cell.Value.Providers.SetEquals(other.Providers)
                        || cell.Value.Color != other.Color || cell.Value.Type != other.Type || cell.Value.IndicatorColor != other.IndicatorColor) return false;
                }
            }
            return true;
        }
        public static void Reconcile(Pawn pawn, SlotLossReason reason = SlotLossReason.Removed, Dictionary<string, Board> before = null)
        {
            // Keep health hooks out of this transaction; disappearing provider boards inherit its cause.
            if (CapacityEvents.Resolving) return;
            var remembered = observed.GetValue(pawn, _ => new Observed());
            before = before ?? remembered.Boards;
            CapacityEvents.Resolving = true;
            try
            {
                var policy = DefDatabase<CBMLossPolicyDef>.AllDefsListForReading.FirstOrDefault() ?? new CBMLossPolicyDef();
                GridRules rules = null; Dictionary<string, Board> finalBoards = null;
                while (true)
                {
                    rules = ImplantService.Build(pawn);
                    var boards = rules.Boards(); finalBoards = boards;
                    bool changed = false;
                    foreach (var module in ImplantService.Modules(pawn).ToList())
                    {
                        var state = module.State();
                        if (!rules.Specs.ContainsKey(state.SpecId)) continue;
                        boards.TryGetValue(state.BoardId ?? "", out var board);
                        int supported = rules.SupportedCells(state, board);
                        bool prepared = state.Status == ModuleStatus.SurgeryPrepared;
                        if (prepared && rules.Roots.ContainsKey(state.RootId) && !module.lossPending) continue;
                        if (module.retainedRemnant && rules.Roots.ContainsKey(state.RootId)) continue;
                        int lost = 0;
                        if (!prepared && before != null && before.TryGetValue(state.BoardId ?? "", out var prior))
                            for (int y = state.Y; y < state.Y + rules.Height(state); y++)
                                for (int x = state.X; x < state.X + rules.Width(state); x++)
                                    if (prior.At(x, y) != null && board?.At(x, y) == null) lost++;
                        // Legacy unsupported saves have no event history: settle once under removal policy.
                        if (supported == 0 && !module.lossPending && lost == 0) lost = rules.Width(state) * rules.Height(state);
                        if (lost > 0 && !module.lossPending)
                        {
                            float previous = module.durability; bool wasBurnt = ConditionRules.Burnt(module.condition, previous), wasActive = module.active;
                            bool destroyed = reason == SlotLossReason.Destroyed;
                            if (Rand.Chance(destroyed ? policy.destroyedDamageChance : policy.removedDamageChance))
                            {
                                float damage = (destroyed ? policy.destroyedDamage : policy.removedDamage) * lost / (rules.Width(state) * rules.Height(state));
                                module.durability = Math.Max(0, module.durability - damage);
                                if (module.durability <= 0) { module.condition |= CBMCondition.BurntOut; module.active = false; }
                            }
                            if (supported == 0)
                            {
                                module.lossPending = true; module.lossReason = reason;
                                module.lossRecover = Rand.Chance(destroyed ? policy.destroyedRecoveryChance : policy.removedRecoveryChance);
                                module.condition |= CBMCondition.Dirty | CBMCondition.Unsterile;
                                if (Rand.Chance(destroyed ? policy.destroyedBurnChance : policy.removedBurnChance))
                                { module.condition |= CBMCondition.BurntOut; module.durability = 0; module.active = false; }
                                if (rules.Roots.ContainsKey(state.RootId) && (module.durability < 1 || ConditionRules.Burnt(module.condition, module.durability)))
                                { module.lossPending = false; module.lossRecover = false; module.retainedRemnant = true; module.active = false; }
                                if (module.intentBoard != null) CBMRelocation.Cancel(module);
                            }
                            CBMDamageEvents.Notify(module, previous, wasBurnt, wasActive, destroyed ? CBMDamageCause.SlotDestroyed : CBMDamageCause.SlotRemoved);
                            changed = true;
                        }
                        if (!module.lossPending) continue;
                        // A queued recovery outcome survives save/load and failed placement without rerolls.
                        if (module.lossRecover)
                        {
                            var item = CBMConditionUtility.MakeItem(module);
                            bool placed = pawn.MapHeld != null
                                ? GenPlace.TryPlaceThing(item, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near)
                                : pawn.inventory != null && pawn.inventory.innerContainer.TryAdd(item);
                            if (!placed) { item.Destroy(); continue; }
                        }
                        if (pawn.Faction == Faction.OfPlayer)
                            Messages.Message((module.lossRecover ? "RC_LossRecovered" : "RC_LossLost").Translate(module.LabelCap), pawn,
                                module.lossRecover ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.NegativeEvent, false);
                        pawn.health.RemoveHediff(module);
                        changed = true;
                    }
                    before = boards;
                    if (!changed) break;
                }
                foreach (var module in ImplantService.Modules(pawn).Where(m => m.intentBoard != null).ToList())
                    if (!rules.Place(module.GetUniqueLoadID(), module.intentBoard,
                        module.intentX, module.intentY, module.intentRotated, false).Allowed) CBMRelocation.Cancel(module);
                string signature = string.Join("|", ImplantService.Modules(pawn).Select(m => m.GetUniqueLoadID() + ":" + m.status + ":" + m.boardId + ":" + m.x + ":" + m.y + ":" + m.rotated + ":" + m.durability + ":" + m.condition + ":" + m.lossPending + ":" + m.retainedRemnant + ":" + m.intentBoard));
                if (remembered.State != signature || !SameBoards(remembered.Boards, finalBoards))
                {
                    CBMEnergy.For(pawn);
                    CBMStatEffects.Invalidate(pawn);
                    PanelSnapshot.Invalidate(pawn);
                }
                remembered.State = signature; remembered.Boards = finalBoards;
            }
            finally { CapacityEvents.Resolving = false; }
        }
    }
}
