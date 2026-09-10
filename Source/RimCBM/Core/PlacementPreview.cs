using System.Collections.Generic;
using System.Linq;

namespace RimCBM.Core
{
    // One immutable GUI snapshot/drag orientation. Commits always go through fresh runtime rules.
    public sealed class PlacementPreview
    {
        public sealed class Region
        {
            public readonly HashSet<string> Cells = new HashSet<string>();
            public readonly Dictionary<string, RuleResult> Anchors = new Dictionary<string, RuleResult>();
            public RuleResult At(int x, int y) => Anchors.TryGetValue(RootSpec.Key(x, y), out var result) ? result : RuleResult.Fail("OutsideGrid");
        }
        private readonly GridRules rules;
        private readonly string id;
        private readonly bool rotated;
        private readonly ModuleState external;
        private readonly Dictionary<string, Region> cache = new Dictionary<string, Region>();
        public int Evaluations { get; private set; }
        public PlacementPreview(GridRules rules, string id, bool rotated, ModuleState external = null)
        { this.rules = rules; this.id = id; this.rotated = rotated; this.external = external; }
        public Region For(Board board)
        {
            if (cache.TryGetValue(board.Id, out var region)) return region;
            region = new Region();
            var module = external?.Copy() ?? rules.Modules.First(m => m.Id == id).Copy();
            var source = module.Copy();
            module.Rotated = rotated;
            if (external != null) { module.RootId = board.RootId; source.RootId = board.RootId; }
            for (int y = 0; y <= board.Height - rules.Height(module); y++)
                for (int x = 0; x <= board.Width - rules.Width(module); x++)
                {
                    var result = external == null ? rules.Place(id, board.Id, x, y, rotated, false)
                        : rules.InsertHot(module, board.Id, x, y, rotated, false);
                    Evaluations++;
                    region.Anchors[RootSpec.Key(x, y)] = result;
                    if (rules.EligibleFootprint(source, board, x, y, rotated, external != null))
                        for (int dy = 0; dy < rules.Height(module); dy++)
                            for (int dx = 0; dx < rules.Width(module); dx++) region.Cells.Add(RootSpec.Key(x + dx, y + dy));
                }
            cache.Add(board.Id, region);
            return region;
        }
    }
}
