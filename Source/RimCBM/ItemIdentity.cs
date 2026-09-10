using System.Linq;
using Verse;

namespace RimCBM
{
    public static class CBMItems
    {
        // A physical item has one implant identity. Ambiguous definitions must never silently choose one.
        public static HediffDef Implant(ThingDef item)
        {
            if (item == null) return null;
            var candidates = DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.GetModExtension<CBMExtension>()?.item == item).Take(2).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }
    }
}
