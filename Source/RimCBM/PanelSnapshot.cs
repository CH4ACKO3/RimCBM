using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public sealed class PanelSnapshot
    {
        private static Pawn owner;
        private static string signature;
        private static PanelSnapshot snapshot;
        public GridRules Rules;
        public List<BodyPartRecord> Parts;
        public Dictionary<string, Board> Boards;
        public Dictionary<string, Hediff_CBMModule> Modules;
        public static int Builds { get; private set; }
        public static PanelSnapshot Get(Pawn pawn)
        {
            var key = new StringBuilder();
            key.Append(DefDatabase<ResearchProjectDef>.GetNamedSilentFail("RimCBM_LayoutReconfiguration")?.IsFinished == true);
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                key.Append('|').Append(h.GetUniqueLoadID()).Append(':').Append(h.Part?.GetHashCode());
                if (h is Hediff_CBMModule m)
                    key.Append(':').Append(m.boardId).Append(':').Append(m.x).Append(':').Append(m.y).Append(':').Append(m.rotated).Append(':').Append(m.lossPending).Append(':').Append(m.condition).Append(':').Append(m.durability).Append(':').Append(m.status).Append(':').Append(m.intentBoard).Append(':').Append(m.intentX).Append(':').Append(m.intentY).Append(':').Append(m.intentRotated);
            }
            // Definitions normally stay immutable after loading, but profile size changes are cheap to detect.
            foreach (var profile in DefDatabase<CBMGridProfileDef>.AllDefsListForReading)
                key.Append('|').Append(profile.width).Append(':').Append(profile.height);
            string next = key.ToString();
            if (owner == pawn && signature == next && snapshot != null) return snapshot;
            owner = pawn; signature = next;
            var rules = ImplantService.Build(pawn);
            snapshot = new PanelSnapshot { Rules = rules, Parts = ImplantService.Parts(pawn), Boards = rules.Boards(),
                Modules = ImplantService.Modules(pawn).ToDictionary(m => m.GetUniqueLoadID()) };
            Builds++;
            return snapshot;
        }
        public static void Invalidate() { snapshot = null; }
        public static void Invalidate(Pawn pawn) { if (owner == pawn) snapshot = null; }
    }
}
