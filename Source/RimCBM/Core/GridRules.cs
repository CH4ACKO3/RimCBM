using System;
using System.Collections.Generic;
using System.Linq;

namespace RimCBM.Core
{
    public enum ModuleStatus { SurgeryPrepared, Installed }
    public enum LimitScope { Part, Pawn }
    public sealed class InstallationLimit
    {
        public LimitScope Scope;
        public int MaxCount = 1;
    }
    public sealed class ExclusionRule
    {
        public LimitScope Scope;
        public HashSet<string> ModuleIds = new HashSet<string>();
        public HashSet<string> Tags = new HashSet<string>();
        public bool Matches(ModuleSpec other) => ModuleIds.Contains(other.Id) || Tags.Overlaps(other.Tags);
    }
    public enum ExpansionKind { None, Row, Column, Board }

    public static class SlotTags
    {
        public const string HotSwap = "HotSwap";
        public const string CoreBody = "CoreBody";
    }

    public static class TagQuery
    {
        public static bool Matches(HashSet<string> tags, IEnumerable<string> all, IEnumerable<string> any, IEnumerable<string> forbidden)
            => all.All(tags.Contains) && (!any.Any() || any.Any(tags.Contains)) && !forbidden.Any(tags.Contains);
    }
    public sealed class ModuleSpec
    {
        public string Id;
        public string Color, Type, GrantedCellColor, GrantedCellType, IndicatorColor, InactiveIndicatorColor, GrantedIndicatorColor;
        public HashSet<string> Tags = new HashSet<string>();
        public List<ExclusionRule> Dependencies = new List<ExclusionRule>();
        public List<ExclusionRule> Exclusions = new List<ExclusionRule>();
        public List<InstallationLimit> Limits = new List<InstallationLimit>();
        public int Width = 1, Height = 1;
        public bool Rotatable = true, HotSwappable;
        public ExpansionKind Expansion;
        public int Extension = 1, BayWidth = 4, BayHeight = 4;
        public HashSet<string> GrantedCellTags = new HashSet<string>();
        public HashSet<string> RequiredAllTags = new HashSet<string>();
        public HashSet<string> RequiredAnyTags = new HashSet<string>();
        public HashSet<string> ForbiddenTags = new HashSet<string>();
    }

    public sealed class RootSpec
    {
        public string Id;
        public int Width, Height;
        public string Color, Type, IndicatorColor;
        public Dictionary<string, string> CellIndicatorColors = new Dictionary<string, string>();
        public Dictionary<string, string> CellColors = new Dictionary<string, string>();
        public Dictionary<string, string> CellTypes = new Dictionary<string, string>();
        // Host identity propagates to expansion boards; socket capabilities do not.
        public HashSet<string> HostTags = new HashSet<string>();
        public HashSet<string> DefaultCellTags = new HashSet<string>();
        public Dictionary<string, HashSet<string>> CellTags = new Dictionary<string, HashSet<string>>();
        public static string Key(int x, int y) => x + ":" + y;
    }

    public sealed class ModuleState
    {
        public string Id, SpecId, RootId, BoardId;
        public int X, Y;
        public bool Rotated;
        public ModuleStatus Status;
        public CBMCondition Condition;
        public bool AwaitingRecovery;
        public float Durability = 1f;
        public bool BurntOut => ConditionRules.Burnt(Condition, Durability);
        public ModuleState Copy() => (ModuleState)MemberwiseClone();
    }

    public sealed class Cell
    {
        public HashSet<string> Tags = new HashSet<string>();
        public bool Hot => Tags.Contains(SlotTags.HotSwap);
        public string Provider, Color, Type, IndicatorColor;
        public HashSet<string> Providers = new HashSet<string>();
    }

    public sealed class Board
    {
        public string Id, RootId;
        public int Width, Height;
        public Dictionary<string, Cell> Cells = new Dictionary<string, Cell>();
        public Cell At(int x, int y)
        {
            Cells.TryGetValue(RootSpec.Key(x, y), out var cell);
            return cell;
        }
    }

    public sealed class RuleResult
    {
        public string Error;
        public bool Allowed => Error == null;
        public static RuleResult Ok() => new RuleResult();
        public static RuleResult Fail(string error) => new RuleResult { Error = error };
    }

    // No Verse/Unity dependencies: the same authority is used by previews, commits and tests.
    public sealed class GridRules
    {
        public const int MaxDimension = 32;
        public readonly Dictionary<string, ModuleSpec> Specs;
        public readonly Dictionary<string, RootSpec> Roots;
        public List<ModuleState> Modules { get; private set; }
        public bool LayoutResearch;
        public readonly List<ModuleState> Reservations = new List<ModuleState>();

        public GridRules(IEnumerable<ModuleSpec> specs, IEnumerable<RootSpec> roots, IEnumerable<ModuleState> modules, bool research)
        {
            Specs = specs.ToDictionary(s => s.Id);
            Roots = roots.ToDictionary(r => r.Id);
            Modules = modules.Select(m => m.Copy()).ToList();
            LayoutResearch = research;
        }

        public static string RootBoard(string root) => "root/" + root;
        public static string BayBoard(string module) => "bay/" + module;
        public int Width(ModuleState module) => module.Rotated ? Specs[module.SpecId].Height : Specs[module.SpecId].Width;
        public int Height(ModuleState module) => module.Rotated ? Specs[module.SpecId].Width : Specs[module.SpecId].Height;

        private bool ValidSpec(ModuleSpec s) => s.Width > 0 && s.Height > 0 && s.Width <= MaxDimension && s.Height <= MaxDimension
            && s.Extension > 0 && s.Extension <= MaxDimension && s.BayWidth > 0 && s.BayHeight > 0
            && s.BayWidth <= MaxDimension && s.BayHeight <= MaxDimension
            && s.Exclusions.All(e => Enum.IsDefined(typeof(LimitScope), e.Scope) && (e.ModuleIds.Count > 0 || e.Tags.Count > 0))
            && s.Limits.All(l => l.MaxCount > 0 && Enum.IsDefined(typeof(LimitScope), l.Scope));

        public Dictionary<string, Board> Boards() => BuildBoards(Modules);

        private Dictionary<string, Board> BuildBoards(List<ModuleState> modules)
        {
            var boards = new Dictionary<string, Board>();
            foreach (var root in Roots.Values)
            {
                if (root.Width <= 0 || root.Height <= 0 || root.Width > MaxDimension || root.Height > MaxDimension) continue;
                var board = new Board { Id = RootBoard(root.Id), RootId = root.Id, Width = root.Width, Height = root.Height };
                for (int y = 0; y < root.Height; y++)
                    for (int x = 0; x < root.Width; x++)
                    {
                        var tags = new HashSet<string>(root.HostTags);
                        tags.UnionWith(root.DefaultCellTags);
                        if (root.CellTags.TryGetValue(RootSpec.Key(x, y), out var extra)) tags.UnionWith(extra);
                        board.Cells.Add(RootSpec.Key(x, y), new Cell { Tags = tags, IndicatorColor = root.CellIndicatorColors.TryGetValue(RootSpec.Key(x, y), out var dot) ? dot : root.IndicatorColor, Color = root.CellColors.TryGetValue(RootSpec.Key(x, y), out var color) ? color : root.Color,
                            Type = root.CellTypes.TryGetValue(RootSpec.Key(x, y), out var type) ? type : root.Type });
                    }
                boards.Add(board.Id, board);
            }
            foreach (var m in modules.Where(m => m.Status == ModuleStatus.Installed))
            {
                if (m.AwaitingRecovery || m.BurntOut || !Specs.TryGetValue(m.SpecId, out var spec) || !ValidSpec(spec) || spec.Expansion == ExpansionKind.None
                    || !Roots.TryGetValue(m.RootId, out var root) || m.BoardId != RootBoard(root.Id)
                    || !boards.TryGetValue(m.BoardId, out var board)) continue;
                // Providers can only occupy base cells. Never bootstrap their own capacity.
                if (m.X < 0 || m.Y < 0 || m.X + Width(m) > root.Width || m.Y + Height(m) > root.Height) continue;
                if (!FootprintTagsMatch(m, board)) continue;
                if (modules.Any(other => other.Id != m.Id && other.Status == ModuleStatus.Installed && !other.AwaitingRecovery
                    && other.BoardId == m.BoardId && Specs.ContainsKey(other.SpecId) && Overlaps(m, other))) continue;
                if (spec.Expansion == ExpansionKind.Board)
                {
                    var bay = new Board { Id = BayBoard(m.Id), RootId = root.Id, Width = spec.BayWidth, Height = spec.BayHeight };
                    for (int y = 0; y < bay.Height; y++)
                        for (int x = 0; x < bay.Width; x++)
                            bay.Cells[RootSpec.Key(x, y)] = GrantedCell(root, spec, m.Id);
                    boards[bay.Id] = bay;
                }
                else if (spec.Expansion == ExpansionKind.Row && root.Height + spec.Extension <= MaxDimension)
                {
                    board.Height = root.Height + spec.Extension;
                    for (int y = root.Height; y < board.Height; y++)
                        for (int x = 0; x < root.Width; x++)
                            board.Cells[RootSpec.Key(x, y)] = GrantedCell(root, spec, m.Id);
                }
                else if (spec.Expansion == ExpansionKind.Column && root.Width + spec.Extension <= MaxDimension)
                {
                    board.Width = root.Width + spec.Extension;
                    for (int y = 0; y < root.Height; y++)
                        for (int x = root.Width; x < board.Width; x++)
                            board.Cells[RootSpec.Key(x, y)] = GrantedCell(root, spec, m.Id);
                }
            }
            return boards;
        }

        private bool Overlaps(ModuleState a, ModuleState b) => a.X < b.X + Width(b) && b.X < a.X + Width(a)
            && a.Y < b.Y + Height(b) && b.Y < a.Y + Height(a);

        private static Cell GrantedCell(RootSpec root, ModuleSpec spec, string provider)
        {
            var cell = new Cell { Provider = provider, Providers = new HashSet<string> { provider },
                IndicatorColor = spec.GrantedIndicatorColor, Color = spec.GrantedCellColor, Type = spec.GrantedCellType, Tags = new HashSet<string>(root.HostTags) };
            cell.Tags.UnionWith(spec.GrantedCellTags);
            return cell;
        }

        private bool FootprintTagsMatch(ModuleState module, Board board, bool allowMissing = false)
        {
            var spec = Specs[module.SpecId];
            for (int y = module.Y; y < module.Y + Height(module); y++)
                for (int x = module.X; x < module.X + Width(module); x++)
                {
                    var cell = board.At(x, y);
                    if (cell == null && allowMissing) continue;
                    if (cell == null || !TagQuery.Matches(cell.Tags, spec.RequiredAllTags, spec.RequiredAnyTags, spec.ForbiddenTags)) return false;
                }
            return true;
        }

        public RuleResult Validate() => Validate(Modules);

        private RuleResult Validate(List<ModuleState> modules, string strictId = null)
        {
            if (modules.Select(m => m.Id).Distinct().Count() != modules.Count) return RuleResult.Fail("DuplicateInstance");
            var boards = BuildBoards(modules);
            foreach (var m in modules)
            {
                if (!Specs.TryGetValue(m.SpecId, out var spec) || !ValidSpec(spec)) return RuleResult.Fail("UnknownModule");
                // Unsupported remnants remain physical implants, but have no grid occupancy.
                if (m.AwaitingRecovery && m.Id != strictId) continue;
                if (!Roots.ContainsKey(m.RootId)) return RuleResult.Fail("MissingBodyPart");
                if (m.Rotated && !spec.Rotatable) return RuleResult.Fail("RotationForbidden");
                if (m.Status != ModuleStatus.Installed) continue;
                if (!boards.TryGetValue(m.BoardId ?? "", out var board) || board.RootId != m.RootId) return RuleResult.Fail("MissingBoard");
                if (m.X < 0 || m.Y < 0 || m.X > MaxDimension || m.Y > MaxDimension) return RuleResult.Fail("OutsideGrid");
                bool complete = SupportedCells(m, board) == Width(m) * Height(m);
                if (!complete && (m.Id == strictId || SupportedCells(m, board) == 0)) return RuleResult.Fail("OutsideGrid");
                if (!FootprintTagsMatch(m, board, !complete)) return RuleResult.Fail("SlotTagsMismatch");
                if (modules.Any(other => other.Id != m.Id && other.Status == ModuleStatus.Installed && !other.AwaitingRecovery
                    && other.BoardId == m.BoardId && Specs.ContainsKey(other.SpecId) && Overlaps(m, other))) return RuleResult.Fail("Occupied");
                if (spec.Expansion != ExpansionKind.None && complete)
                {
                    var root = Roots[m.RootId];
                    if (m.BoardId != RootBoard(root.Id) || m.X + Width(m) > root.Width || m.Y + Height(m) > root.Height)
                        return RuleResult.Fail("ExpansionNeedsBaseCells");
                    if ((spec.Expansion == ExpansionKind.Row && root.Height + spec.Extension > MaxDimension)
                        || (spec.Expansion == ExpansionKind.Column && root.Width + spec.Extension > MaxDimension)) return RuleResult.Fail("OutsideGrid");
                }
            }
            return RuleResult.Ok();
        }

        public int SupportedCells(ModuleState module, Board board)
        {
            if (board == null || !Specs.ContainsKey(module.SpecId)) return 0;
            int count = 0;
            for (int y = module.Y; y < module.Y + Height(module); y++)
                for (int x = module.X; x < module.X + Width(module); x++)
                    if (board.At(x, y) != null) count++;
            return count;
        }

        // No mutation: the adapter must materialize an item successfully before removing its hediff.
        public IReadOnlyList<string> UnsupportedModules()
        {
            var boards = Boards();
            return Modules.Where(m => m.Status == ModuleStatus.Installed && Specs.ContainsKey(m.SpecId)
                && (!boards.TryGetValue(m.BoardId ?? "", out var board) || SupportedCells(m, board) == 0))
                .Select(m => m.Id).ToList();
        }

        private bool FullyHot(ModuleState m, Dictionary<string, Board> boards)
        {
            if (!Specs.TryGetValue(m.SpecId, out var spec) || !spec.HotSwappable || !boards.TryGetValue(m.BoardId ?? "", out var board)) return false;
            if (m.X < 0 || m.Y < 0 || m.X > MaxDimension || m.Y > MaxDimension) return false;
            for (int y = m.Y; y < m.Y + Height(m); y++)
                for (int x = m.X; x < m.X + Width(m); x++)
                    if (board.At(x, y)?.Hot != true) return false;
            return true;
        }

        // Prepared modules reserve a slot in the quota. Repositioning never consumes another slot.
        public Func<ModuleState, RuleResult> AdmissionCheck;
        public List<ModuleState> DependencyReservations = new List<ModuleState>();
        public bool DependenciesMet(ModuleState module, IEnumerable<ModuleState> pool, HashSet<string> path = null)
        {
            path = path ?? new HashSet<string>();
            if (!path.Add(module.Id ?? "candidate")) return false;
            bool okay = Specs[module.SpecId].Dependencies.All(dep => pool.Any(other => other.Id != module.Id
                && other.Status == ModuleStatus.Installed && !other.BurntOut && !other.AwaitingRecovery
                && (dep.Scope == LimitScope.Pawn || other.RootId == module.RootId) && dep.Matches(Specs[other.SpecId])
                && DependenciesMet(other, pool, path)));
            path.Remove(module.Id ?? "candidate"); return okay;
        }
        public RuleResult CanAdd(ModuleState candidate, IEnumerable<ModuleState> additional = null)
        {
            if (candidate == null || !Specs.TryGetValue(candidate.SpecId, out var spec) || !ValidSpec(spec)) return RuleResult.Fail("UnknownModule");
            var admission = AdmissionCheck?.Invoke(candidate); if (admission != null && !admission.Allowed) return admission;
            if (!DependenciesMet(candidate, Modules)) return RuleResult.Fail("MissingDependency");
            var existing = Modules.Concat(additional ?? Enumerable.Empty<ModuleState>()).Where(m => candidate.Id == null || m.Id != candidate.Id).ToList();
            foreach (var other in existing)
            {
                if (!Specs.TryGetValue(other.SpecId, out var otherSpec)) continue;
                foreach (var exclusion in spec.Exclusions.Where(e => e.Matches(otherSpec)).Concat(otherSpec.Exclusions.Where(e => e.Matches(spec))))
                    if (exclusion.Scope == LimitScope.Pawn || other.RootId == candidate.RootId)
                        return RuleResult.Fail(exclusion.Scope == LimitScope.Pawn ? "PawnExclusion" : "PartExclusion");
            }
            foreach (var limit in spec.Limits)
                if (existing.Count(m => m.Id != candidate.Id && m.SpecId == candidate.SpecId
                    && (limit.Scope == LimitScope.Pawn || m.RootId == candidate.RootId)) >= limit.MaxCount)
                    return RuleResult.Fail(limit.Scope == LimitScope.Pawn ? "PawnLimit" : "PartLimit");
            return RuleResult.Ok();
        }

        public RuleResult CanStartPlacement(string id, bool canHotSwap = true)
        {
            if (Reservations.Any(r => r.Id == id)) return RuleResult.Fail("Reserved");
            var module = Modules.FirstOrDefault(m => m.Id == id);
            if (module == null || !Specs.ContainsKey(module.SpecId)) return RuleResult.Fail("UnknownModule");
            if (module.AwaitingRecovery) return RuleResult.Fail("MissingBoard");
            if (module.BurntOut) return RuleResult.Fail("BurntOut");
            if (module.Status == ModuleStatus.SurgeryPrepared)
            {
                var error = ConditionRules.InstallError(module.Condition, module.Durability);
                return error == null ? CanAdd(module) : RuleResult.Fail(error);
            }
            if (LayoutResearch) return RuleResult.Ok();
            return canHotSwap && FullyHot(module, Boards()) ? RuleResult.Ok() : RuleResult.Fail("ResearchRequired");
        }

        public RuleResult Place(string id, string boardId, int x, int y, bool rotated, bool commit = true)
        {
            var source = Modules.FirstOrDefault(m => m.Id == id);
            if (source == null || !Specs.ContainsKey(source.SpecId)) return RuleResult.Fail("UnknownModule");
            if (source.AwaitingRecovery) return RuleResult.Fail("MissingBoard");
            if (source.BurntOut) return RuleResult.Fail("BurntOut");
            if (source.Status == ModuleStatus.SurgeryPrepared)
            {
                var error = ConditionRules.InstallError(source.Condition, source.Durability);
                if (error != null) return RuleResult.Fail(error);
                var quota = CanAdd(source);
                if (!quota.Allowed) return quota;
            }
            var boards = Boards();
            if (!boards.TryGetValue(boardId, out var board) || board.RootId != source.RootId) return RuleResult.Fail("WrongBodyPart");
            if (rotated && !Specs[source.SpecId].Rotatable) return RuleResult.Fail("RotationForbidden");
            var candidate = source.Copy();
            candidate.BoardId = boardId; candidate.X = x; candidate.Y = y; candidate.Rotated = rotated; candidate.Status = ModuleStatus.Installed;
            bool hotDestination = FullyHot(candidate, boards);
            if (source.Status == ModuleStatus.Installed)
            {
                bool unchanged = source.BoardId == boardId && source.X == x && source.Y == y && source.Rotated == rotated;
                bool researchedMove = LayoutResearch && source.BoardId == boardId;
                bool hotMove = FullyHot(source, boards) && hotDestination;
                if (!unchanged && !researchedMove && !hotMove)
                    return RuleResult.Fail(source.BoardId != boardId ? "CrossBoardRequiresHotSwap" : "ResearchRequired");
            }
            var next = Modules.Select(m => m.Id == id ? candidate : m.Copy()).ToList();
            var result = ReservationCheck(candidate);
            if (result.Allowed) result = Validate(next, candidate.Id);
            if (result.Allowed && commit) Modules = next;
            return result;
        }

        public RuleResult ReservationCheck(ModuleState candidate)
        {
            return Reservations.Any(r => r.Id != candidate.Id && r.BoardId == candidate.BoardId
                && Specs.ContainsKey(r.SpecId) && Overlaps(candidate, r)) ? RuleResult.Fail("Reserved") : RuleResult.Ok();
        }

        // Socket eligibility only: preserve provider geometry while ignoring all occupancy.
        public bool EligibleFootprint(ModuleState source, Board board, int x, int y, bool rotated, bool external)
        {
            if (!Specs.TryGetValue(source.SpecId, out var spec) || source.RootId != board.RootId
                || (rotated && !spec.Rotatable)) return false;
            var target = source.Copy();
            target.BoardId = board.Id; target.X = x; target.Y = y; target.Rotated = rotated;
            if (x < 0 || y < 0 || SupportedCells(target, board) != Width(target) * Height(target)
                || !FootprintTagsMatch(target, board)) return false;
            if (spec.Expansion != ExpansionKind.None && (!Roots.TryGetValue(board.RootId, out var root)
                || board.Id != RootBoard(root.Id) || x + Width(target) > root.Width || y + Height(target) > root.Height)) return false;
            var boards = Boards();
            if (external) return FullyHot(target, boards);
            return source.Status == ModuleStatus.SurgeryPrepared || (LayoutResearch && source.BoardId == board.Id)
                || (source.BoardId == board.Id && source.X == x && source.Y == y && source.Rotated == rotated)
                || (FullyHot(source, boards) && FullyHot(target, boards));
        }

        public IReadOnlyList<string> Dependents(string id)
        {
            var boards = Boards();
            return Modules.Where(m => m.Id != id && m.Status == ModuleStatus.Installed && !m.AwaitingRecovery && Specs.ContainsKey(m.SpecId))
                .Where(m => m.BoardId == BayBoard(id) || (boards.TryGetValue(m.BoardId ?? "", out var board) && DependsOn(m, board, id)))
                .Select(m => m.Id).ToList();
        }

        private bool DependsOn(ModuleState m, Board board, string provider)
        {
            for (int y = m.Y; y < m.Y + Height(m); y++)
                for (int x = m.X; x < m.X + Width(m); x++)
                    if (board.At(x, y)?.Providers.Contains(provider) == true) return true;
            return false;
        }

        public RuleResult Detach(string id, bool surgicalExtraction = false, bool commit = true)
        {
            var source = Modules.FirstOrDefault(m => m.Id == id);
            if (source == null) return RuleResult.Fail("UnknownModule");
            if (Reservations.Any(r => r.Id == id || r.BoardId == BayBoard(id)
                || (Boards().TryGetValue(r.BoardId ?? "", out var reservedBoard) && DependsOn(r, reservedBoard, id)))) return RuleResult.Fail("Reserved");
            if (Dependents(id).Count > 0) return RuleResult.Fail("ExpansionInUse");
            if (!surgicalExtraction && (source.Status != ModuleStatus.Installed || !FullyHot(source, Boards()))) return RuleResult.Fail("SurgeryRequired");
            var next = Modules.Select(m => m.Copy()).ToList();
            if (surgicalExtraction) next.RemoveAll(m => m.Id == id);
            else next.RemoveAll(m => m.Id == id);
            if (next.Concat(DependencyReservations).Any(m => DependenciesMet(m, Modules) && !DependenciesMet(m, next))) return RuleResult.Fail("DependencyInUse");
            // Surgical extraction is the recovery path for a malformed/downgraded layout.
            var result = surgicalExtraction ? RuleResult.Ok() : Validate(next);
            if (result.Allowed && commit) Modules = next;
            return result;
        }

        // Called only by an adapter that owns a real external item. Never creates a pending entry.
        public RuleResult InsertHot(ModuleState external, string boardId, int x, int y, bool rotated, bool commit = true)
        {
            if (external == null || string.IsNullOrEmpty(external.Id) || Modules.Any(m => m.Id == external.Id)) return RuleResult.Fail("DuplicateInstance");
            if (!Specs.ContainsKey(external.SpecId)) return RuleResult.Fail("UnknownModule");
            var error = ConditionRules.InstallError(external.Condition, external.Durability);
            if (error != null) return RuleResult.Fail(error);
            var quota = CanAdd(external);
            if (!quota.Allowed) return quota;
            var candidate = external.Copy();
            candidate.Status = ModuleStatus.Installed; candidate.BoardId = boardId;
            candidate.X = x; candidate.Y = y; candidate.Rotated = rotated;
            if (!FullyHot(candidate, Boards())) return RuleResult.Fail("HotDestinationRequired");
            var next = Modules.Select(m => m.Copy()).ToList();
            next.Add(candidate);
            var result = ReservationCheck(candidate);
            if (result.Allowed) result = Validate(next, candidate.Id);
            if (result.Allowed && commit) Modules = next;
            return result;
        }
    }
}
