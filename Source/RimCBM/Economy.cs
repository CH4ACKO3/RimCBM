using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimCBM
{
    public class CBMTradeExtension : DefModExtension
    {
        public float weight = 1;
        public List<string> pools = new List<string>();
        public override IEnumerable<string> ConfigErrors()
        {
            if (weight < 0 || float.IsNaN(weight) || float.IsInfinity(weight)) yield return "Trade weight must be finite and nonnegative.";
        }
    }
    public class StockGenerator_CBM : StockGenerator
    {
        public float stockChance = .5f;
        public IntRange kinds = new IntRange(1, 2);
        public string pool;
        public override bool HandlesThingDef(ThingDef def)
        {
            var e = def.GetModExtension<CBMTradeExtension>();
            return e != null && e.weight > 0 && def.tradeability != Tradeability.None && def.techLevel <= maxTechLevelBuy
                && (pool.NullOrEmpty() || e.pools.Contains(pool));
        }
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            if (!Rand.Chance(stockChance)) yield break;
            var candidates = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => HandlesThingDef(d) && d.tradeability.TraderCanSell()
                && d.PlayerAcquirable && d.techLevel <= maxTechLevelGenerate).ToList();
            int amount = kinds.RandomInRange;
            for (int i = 0; i < amount && candidates.Count > 0; i++)
            {
                var chosen = candidates.RandomElementByWeight(d => d.GetModExtension<CBMTradeExtension>().weight);
                candidates.Remove(chosen);
                foreach (var thing in StockGeneratorUtility.TryMakeForStock(chosen, RandomCountOf(chosen), faction)) yield return thing;
            }
        }
        public override IEnumerable<string> ConfigErrors(TraderKindDef parentDef)
        {
            if (float.IsNaN(stockChance) || stockChance < 0 || stockChance > 1 || kinds.min < 0 || kinds.max < kinds.min || countRange.min < 1 || countRange.max < countRange.min)
                yield return "Invalid implant stock probability or count range.";
        }
    }
    public class StatPart_CBMInstalledMass : StatPart
    {
        public static float Mass(Pawn pawn) => ImplantService.Modules(pawn).Sum(m => m.def.GetModExtension<CBMExtension>().item.GetStatValueAbstract(StatDefOf.Mass) + CBMSupply.FuelMass(m));
        private static float Offset(StatRequest req)
        {
            if (req.Thing?.TryGetComp<CompCBMCondition>() is CompCBMCondition comp)
                return comp.fuelState.Mass(CBMItems.Implant(req.Thing.def)?.GetModExtension<CBMExtension>().fuel);
            PawnOrCorpseStatUtility.TryGetPawnOrCorpseStat(req, Mass, d => 0, out float mass);
            return mass;
        }
        public override void TransformValue(StatRequest req, ref float val) => val += Offset(req);
        public override string ExplanationPart(StatRequest req)
        {
            float mass = Offset(req);
            return mass == 0 ? null : "RC_Implants".Translate() + ": +" + mass.ToString("0.##") + " kg";
        }
    }
}
