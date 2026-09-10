using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using Verse;

namespace RimCBM
{
    public class RimCBMMod : Mod
    {
        public RimCBMMod(ModContentPack content) : base(content) { new Harmony("ch4acko3.rimcbm").PatchAll(); }
    }

    public class CBMExclusionEntry
    {
        public LimitScope scope;
        public List<HediffDef> modules = new List<HediffDef>();
        public List<string> tags = new List<string>();
    }
    public class CBMLimitEntry
    {
        public LimitScope scope;
        public int maxCount = 1;
    }

    public class CBMExtension : DefModExtension
    {
        public List<ResearchProjectDef> installResearch = new List<ResearchProjectDef>();
        public List<CBMCompatibilityRule> compatibility = new List<CBMCompatibilityRule>();
        public List<CBMExclusionEntry> dependencies = new List<CBMExclusionEntry>();
        public List<CBMModeSpec> modes = new List<CBMModeSpec>();
        public string defaultMode = CBMModes.Off;
        public CBMFuelSpec fuel;
        public float chargeAcceptanceWatts;
        public List<CBMFaultSpec> faults = new List<CBMFaultSpec>();
        public CBMCombatProfile combat = new CBMCombatProfile();
        public List<CBMDamageEffect> damageEffects = new List<CBMDamageEffect>();
        public int damageEffectCooldownTicks = 600;
        public CBMSurgeryProfile installationSurgery = new CBMSurgeryProfile(), removalSurgery = new CBMSurgeryProfile();
        public List<CBMAbilitySpec> abilities = new List<CBMAbilitySpec>();
        public float energyCapacityWd, generationWatts, nutritionPerWd, idleWatts, activationWd, useWd;
        public int useDurationTicks, useCooldownTicks = 600;
        public List<StatModifier> statOffsets = new List<StatModifier>(), burstStatOffsets = new List<StatModifier>();
        public string color, type, grantedCellColor, grantedCellType, shortLabelKey;
        public string indicatorColor, inactiveIndicatorColor, grantedIndicatorColor;
        public string dirtyIndicatorColor, damagedIndicatorColor, burntIndicatorColor, empIndicatorColor, unpoweredIndicatorColor;
        public List<string> tags = new List<string>();
        public List<CBMExclusionEntry> exclusions = new List<CBMExclusionEntry>();
        public List<CBMLimitEntry> installationLimits = new List<CBMLimitEntry>();
        public int width = 1, height = 1;
        public bool rotatable = true, hotSwappable;
        public ExpansionKind expansion;
        public int extension = 1, bayWidth = 4, bayHeight = 4;
        public bool grantedCellsHot;
        public List<string> grantedCellTags = new List<string>();
        public List<string> requiredAllTags = new List<string>();
        public List<string> requiredAnyTags = new List<string>();
        public List<string> forbiddenTags = new List<string>();
        public ThingDef item;
        public ModuleSpec Spec(string id)
        {
            var spec = new ModuleSpec { Id = id, IndicatorColor = indicatorColor, InactiveIndicatorColor = inactiveIndicatorColor, GrantedIndicatorColor = grantedIndicatorColor, Color = color, Type = type, GrantedCellColor = grantedCellColor, GrantedCellType = grantedCellType,
                Dependencies = dependencies.Select(e => new ExclusionRule { Scope = e.scope, ModuleIds = new HashSet<string>(e.modules.Where(d => d != null).Select(d => d.defName)), Tags = new HashSet<string>(e.tags) }).ToList(),
                Exclusions = exclusions.Select(e => new ExclusionRule { Scope = e.scope, ModuleIds = new HashSet<string>(e.modules.Where(d => d != null).Select(d => d.defName)), Tags = new HashSet<string>(e.tags) })
                    .Concat((installationLimits ?? new List<CBMLimitEntry>()).Where(l => l.maxCount == 1).Select(l => new ExclusionRule { Scope = l.scope, ModuleIds = new HashSet<string> { id } })).ToList(),
                Limits = (installationLimits ?? new List<CBMLimitEntry>()).Where(l => l.maxCount > 1).Select(l => new InstallationLimit { Scope = l.scope, MaxCount = l.maxCount }).ToList(),
                Tags = new HashSet<string>(tags ?? new List<string>()), Width = width, Height = height, Rotatable = rotatable,
                HotSwappable = hotSwappable, Expansion = expansion, Extension = extension, BayWidth = bayWidth, BayHeight = bayHeight,
                GrantedCellTags = new HashSet<string>(grantedCellTags ?? new List<string>()),
                RequiredAllTags = new HashSet<string>(requiredAllTags ?? new List<string>()),
                RequiredAnyTags = new HashSet<string>(requiredAnyTags ?? new List<string>()),
                ForbiddenTags = new HashSet<string>(forbiddenTags ?? new List<string>()) };
            // Legacy XML compatibility only; runtime authority is the tag set.
            if (grantedCellsHot) spec.GrantedCellTags.Add(SlotTags.HotSwap);
            return spec;
        }
        public override IEnumerable<string> ConfigErrors()
        {
            if (abilities.Any(a => a.id == CBMLegacyAbilities.UseId)) yield return "Reserved legacy ability id.";
            if ((installationLimits ?? new List<CBMLimitEntry>()).Any(l => l.maxCount < 1 || !Enum.IsDefined(typeof(LimitScope), l.scope))) yield return "Invalid installation limit.";
            foreach (float value in new[] { chargeAcceptanceWatts, energyCapacityWd, generationWatts, nutritionPerWd, idleWatts, activationWd, useWd })
                if (value < 0 || float.IsNaN(value) || float.IsInfinity(value)) yield return "Energy values must be finite and nonnegative.";
            if (useDurationTicks < 0 || useCooldownTicks < 0 || (generationWatts > 0 && idleWatts > 0)) yield return "Invalid CBM power timing or mixed generator/consumer definition.";
            foreach (var e in exclusions.Concat(dependencies))
                if (!Enum.IsDefined(typeof(LimitScope), e.scope) || e.modules.Count + e.tags.Count == 0 || e.modules.Any(d => d == null || d.GetModExtension<CBMExtension>() == null) || e.tags.Any(string.IsNullOrEmpty)) yield return "Invalid CBM exclusion scope or target.";
            if (installResearch.Any(r => r == null)) yield return "Unknown installation research.";
            foreach (var c in compatibility)
                if (c.minBodySize < 0 || c.maxBodySize < c.minBodySize || float.IsNaN(c.minBodySize) || float.IsNaN(c.maxBodySize)
                    || c.requiredAllTags.Concat(c.requiredAnyTags).Concat(c.forbiddenTags).Any(string.IsNullOrEmpty)) yield return "Invalid host compatibility rule.";
            if (modes.GroupBy(m => m.id).Any(g => g.Count() > 1) || modes.Any(m => m.id.NullOrEmpty() || m.id == CBMModes.Off || !Enum.IsDefined(typeof(LimitScope), m.scope)
                || m.abilities.Any(id => !abilities.Any(a => a.id == id)) || m.research.Any(p => p == null)
                || new[] { m.generationMultiplier, m.consumptionMultiplier, m.effectMultiplier }.Any(n => n < 0 || float.IsNaN(n) || float.IsInfinity(n)))
                || defaultMode != CBMModes.Off && !modes.Any(m => m.id == defaultMode)) yield return "Invalid CBM mode definition.";
            if (fuel != null) foreach (var error in fuel.ConfigErrors()) yield return error;
            if (faults.GroupBy(f => f.id).Any(g => g.Count() > 1)) yield return "Duplicate fault ids.";
            foreach (var fault in faults) foreach (var error in fault.ConfigErrors()) yield return error;
            foreach (var error in combat.ConfigErrors().Concat(damageEffects.SelectMany(e => e.ConfigErrors()))) yield return error;
            if (damageEffectCooldownTicks < 0) yield return "Invalid damage effect cooldown.";
            foreach (var error in installationSurgery.ConfigErrors().Concat(removalSurgery.ConfigErrors())) yield return error;
            if (abilities.GroupBy(a => a.id).Any(g => g.Count() > 1)) yield return "Duplicate ability ids.";
            foreach (var a in abilities) foreach (var error in a.ConfigErrors()) yield return a.id + ": " + error;
            if (abilities.Count > 0 && (idleWatts > 0 || activationWd > 0 || useWd > 0 || useDurationTicks > 0 || statOffsets.Count > 0 || burstStatOffsets.Count > 0)) yield return "Use ability-local effects and consumption when defining abilities; legacy fields are for legacy modules only.";
            if (item == null) yield return "CBM item is required.";
            else if (DefDatabase<HediffDef>.AllDefsListForReading.Count(d => d.GetModExtension<CBMExtension>()?.item == item) > 1) yield return "CBM item must map to exactly one implant HediffDef: " + item.defName;
            else if (!(item.comps?.Any(c => c.compClass == typeof(CompCBMCondition)) ?? false)) yield return "CBM item requires CompProperties_CBMCondition to preserve instance state.";
            if (width < 1 || height < 1 || width > GridRules.MaxDimension || height > GridRules.MaxDimension) yield return "Invalid CBM footprint.";
        }
    }

    public class CBMCellTagEntry
    {
        public int x, y;
        public string color, type, indicatorColor;
        public List<string> tags = new List<string>();
    }

    public class CBMGridProfileDef : Def
    {
        public BodyPartDef bodyPart;
        public HediffDef replacement;
        public int width, height;
        public string color, type, indicatorColor;
        public List<string> hotCells = new List<string>();
        public List<string> hostTags = new List<string>();
        public List<string> defaultCellTags = new List<string>();
        public List<CBMCellTagEntry> cellTags = new List<CBMCellTagEntry>();
        public RootSpec Root(string id)
        {
            var root = new RootSpec { Id = id, Width = width, Height = height, Color = color, Type = type, IndicatorColor = indicatorColor,
                HostTags = new HashSet<string>(hostTags ?? new List<string>()),
                DefaultCellTags = new HashSet<string>(defaultCellTags ?? new List<string>()) };
            foreach (var entry in cellTags ?? new List<CBMCellTagEntry>())
            {
                string key = RootSpec.Key(entry.x, entry.y);
                if (entry.indicatorColor != null) root.CellIndicatorColors[key] = entry.indicatorColor;
                if (entry.color != null) root.CellColors[key] = entry.color;
                if (entry.type != null) root.CellTypes[key] = entry.type;
                if (!root.CellTags.TryGetValue(key, out var tags)) root.CellTags[key] = tags = new HashSet<string>();
                tags.UnionWith(entry.tags ?? new List<string>());
            }
            foreach (string key in hotCells ?? new List<string>())
            {
                if (!root.CellTags.TryGetValue(key, out var tags)) root.CellTags[key] = tags = new HashSet<string>();
                tags.Add(SlotTags.HotSwap);
            }
            return root;
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors()) yield return error;
            if (bodyPart == null || width < 1 || height < 1 || width > GridRules.MaxDimension || height > GridRules.MaxDimension) yield return "Invalid CBM body grid.";
        }
    }

    public class Hediff_CBMModule : Hediff_Implant
    {
        public string modeId;
        public CBMFuelState fuelState = new CBMFuelState();
        public bool retainedRemnant;
        public List<CBMFaultState> faultStates = new List<CBMFaultState>();
        public int damageEffectUntilTick;
        public List<CBMAbilityState> abilityStates = new List<CBMAbilityState>();
        public ModuleStatus status;
        public bool active = true;
        public float durability = 1f;
        public CBMCondition condition;
        public bool lossPending, lossRecover;
        public SlotLossReason lossReason;
        public int poweredUntilTick, empUntilTick, effectUntilTick, cooldownUntilTick, powerPriority;
        public bool energyStarted;
        public bool HardwareReady => status == ModuleStatus.Installed && !retainedRemnant && !lossPending && !ConditionRules.Burnt(condition, durability) && !pawn.Dead;
        public bool CanRun => HardwareReady && CBMAccess.Runtime(this) && CBMModes.Running(this) && CBMRuntime.Running(pawn) && active && empUntilTick <= (Find.TickManager?.TicksGame ?? 0);
        public bool Operational
        {
            get
            {
                if (def.GetModExtension<CBMExtension>().abilities.Count > 0) return CBMAbilities.Available(this) && CBMAbilities.Specs(this).Any(a =>
                    CBMAbilities.Effective(this, a) || !a.Continuous && CBMAbilities.Enabled(this, a));
                if (!CanRun) return false;
                var ext = def.GetModExtension<CBMExtension>();
                return (ext.idleWatts == 0 && ext.activationWd == 0 && ext.generationWatts == 0)
                    || poweredUntilTick > (Find.TickManager?.TicksGame ?? 0);
            }
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            CBMAbilities.Interrupt(this, CBMStopReason.Death);
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (dinfo.Def == DamageDefOf.EMP) CBMEnergy.Emp(this);
        }
        public float ConditionFactor => Operational ? UnityEngine.Mathf.Clamp01(durability) : 0f;
        public string boardId;
        public string intentBoard;
        public int intentX, intentY;
        public bool intentRotated;
        public ModuleState Intent() { var target = State(); target.BoardId = intentBoard; target.X = intentX; target.Y = intentY; target.Rotated = intentRotated; target.Status = ModuleStatus.Installed; return target; }
        public int x, y;
        public bool rotated;
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (var row in base.SpecialDisplayStats(req)) yield return row;
            foreach (var row in CBMInformation.Rows(def, durability, condition, this)) yield return row;
        }
        public override bool TryMergeWith(Hediff other) => false;
        public override string LabelInBrackets => null;
        public ModuleState State() => new ModuleState { Id = GetUniqueLoadID(), SpecId = def.defName,
            RootId = ImplantService.RootId(pawn, Part), Status = status, Condition = condition, Durability = durability, AwaitingRecovery = lossPending || retainedRemnant, BoardId = boardId, X = x, Y = y, Rotated = rotated };
        public void Apply(ModuleState state) { status = state.Status; boardId = state.BoardId; x = state.X; y = state.Y; rotated = state.Rotated; }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (abilityStates.Any(s => s.effectUntil > 0 && s.effectUntil <= CBMAbilities.Now))
            {
                foreach (var s in abilityStates.Where(s => s.effectUntil <= CBMAbilities.Now)) s.effectUntil = 0;
                CBMStatEffects.Invalidate(pawn);
            }
        }
        public override void PostRemoved() { CBMAbilities.Interrupt(this, CBMStopReason.Removed); base.PostRemoved(); CBMStatEffects.Invalidate(pawn); }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref abilityStates, "cbmAbilities", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && abilityStates == null) abilityStates = new List<CBMAbilityState>();
            Scribe_Values.Look(ref modeId, "cbmMode");
            Scribe_Deep.Look(ref fuelState, "cbmFuel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && fuelState == null) fuelState = new CBMFuelState();
            Scribe_Values.Look(ref retainedRemnant, "cbmRetainedRemnant");
            Scribe_Collections.Look(ref faultStates, "cbmFaultStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && faultStates == null) faultStates = new List<CBMFaultState>();
            Scribe_Values.Look(ref damageEffectUntilTick, "cbmDamageEffectUntil");
            Scribe_Values.Look(ref poweredUntilTick, "cbmPoweredUntil"); Scribe_Values.Look(ref empUntilTick, "cbmEmpUntil");
            Scribe_Values.Look(ref effectUntilTick, "cbmEffectUntil"); Scribe_Values.Look(ref cooldownUntilTick, "cbmCooldownUntil");
            Scribe_Values.Look(ref powerPriority, "cbmPowerPriority"); Scribe_Values.Look(ref energyStarted, "cbmEnergyStarted");
            Scribe_Values.Look(ref intentBoard, "cbmIntentBoard");
            Scribe_Values.Look(ref intentX, "cbmIntentX"); Scribe_Values.Look(ref intentY, "cbmIntentY");
            Scribe_Values.Look(ref intentRotated, "cbmIntentRotated");
            Scribe_Values.Look(ref lossPending, "cbmLossPending");
            Scribe_Values.Look(ref lossRecover, "cbmLossRecover");
            Scribe_Values.Look(ref lossReason, "cbmLossReason");
            Scribe_Values.Look(ref condition, "cbmCondition");
            Scribe_Values.Look(ref active, "cbmActive", true);
            Scribe_Values.Look(ref durability, "cbmDurability", 1f);
            Scribe_Values.Look(ref status, "cbmStatus", ModuleStatus.SurgeryPrepared);
            Scribe_Values.Look(ref boardId, "cbmBoard");
            Scribe_Values.Look(ref x, "cbmX"); Scribe_Values.Look(ref y, "cbmY"); Scribe_Values.Look(ref rotated, "cbmRotated");
        }
    }

    public static class ImplantService
    {
        public static IEnumerable<Hediff_CBMModule> Modules(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_CBMModule>();
        public static string RootId(Pawn pawn, BodyPartRecord part) => "part-" + pawn.RaceProps.body.AllParts.IndexOf(part);
        public static CBMGridProfileDef Profile(Pawn pawn, BodyPartRecord part)
        {
            if (part == null || !pawn.health.hediffSet.GetNotMissingParts().Contains(part)) return null;
            var defs = DefDatabase<CBMGridProfileDef>.AllDefsListForReading.Where(p => p.bodyPart == part.def);
            var replacement = pawn.health.hediffSet.hediffs.OfType<Hediff_AddedPart>().FirstOrDefault(h => h.Part == part);
            return defs.FirstOrDefault(p => p.replacement != null && p.replacement == replacement?.def) ?? defs.FirstOrDefault(p => p.replacement == null);
        }
        public static List<BodyPartRecord> Parts(Pawn pawn) => pawn.RaceProps.body.AllParts.Where(p => Profile(pawn, p) != null).ToList();
        public static bool CanEdit(Pawn pawn) => !pawn.Dead && pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Humanlike;
        public static bool CanHotSwap(Pawn pawn) => CanEdit(pawn) && pawn.Spawned && !pawn.Downed && pawn.Awake()
            && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && pawn.inventory != null;
        public static GridRules Build(Pawn pawn)
        {
            var roots = Parts(pawn).Select(part => {
                var profile = Profile(pawn, part);
                return profile.Root(RootId(pawn, part));
            });
            var specs = DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.GetModExtension<CBMExtension>() != null)
                .Select(d => d.GetModExtension<CBMExtension>().Spec(d.defName));
            bool research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("RimCBM_LayoutReconfiguration")?.IsFinished == true;
            var rules = new GridRules(specs, roots, Modules(pawn).Select(h => h.State()), research);
            rules.AdmissionCheck = state => CBMAccess.Check(pawn, Parts(pawn).FirstOrDefault(p => RootId(pawn, p) == state.RootId), HediffDef.Named(state.SpecId), state.Status != ModuleStatus.Installed);
            if (pawn.BillStack != null)
                rules.DependencyReservations.AddRange(pawn.BillStack.Bills.OfType<Bill_Medical>().Where(b => !b.suspended && b.recipe.Worker is Recipe_PrepareCBM && b.Part != null)
                    .Select(b => new ModuleState { Id = "bill-" + b.GetUniqueLoadID(), SpecId = b.recipe.addsHediff.defName, RootId = RootId(pawn, b.Part), Status = ModuleStatus.SurgeryPrepared }));
            rules.Reservations.AddRange(Modules(pawn).Where(h => h.intentBoard != null).Select(h => h.Intent()));
            return rules;
        }
        public static RuleResult Place(Pawn pawn, string id, string board, int x, int y, bool rotated)
        {
            if (!CanEdit(pawn)) return RuleResult.Fail("ReadOnly");
            var rules = Build(pawn);
            var old = rules.Modules.FirstOrDefault(m => m.Id == id);
            if (old == null) return RuleResult.Fail("UnknownModule");
            // A move using only hot-swap permission needs a capable, present operator.
            if (old.Status == ModuleStatus.Installed && !(rules.LayoutResearch && old.BoardId == board) && !CanHotSwap(pawn))
                return RuleResult.Fail("OperatorUnavailable");
            var result = rules.Place(id, board, x, y, rotated);
            if (result.Allowed) Modules(pawn).First(h => h.GetUniqueLoadID() == id).Apply(rules.Modules.First(m => m.Id == id));
            return result;
        }
        public static RuleResult ExtractHot(Pawn pawn, Hediff_CBMModule module)
        {
            if (!CanHotSwap(pawn) || !Modules(pawn).Contains(module)) return RuleResult.Fail("OperatorUnavailable");
            var result = Build(pawn).Detach(module.GetUniqueLoadID(), commit: false);
            if (!result.Allowed) return result;
            var item = CBMConditionUtility.MakeItem(module);
            if (!pawn.inventory.innerContainer.TryAdd(item)) { item.Destroy(); return RuleResult.Fail("InventoryFull"); }
            pawn.health.RemoveHediff(module);
            return RuleResult.Ok();
        }
        public static RuleResult InsertHot(Pawn pawn, BodyPartRecord part, Thing item, HediffDef def, string board, int x, int y, bool rotated)
        {
            if (!CanHotSwap(pawn) || !pawn.inventory.innerContainer.Contains(item) || item.stackCount < 1
                || def.GetModExtension<CBMExtension>()?.item != item.def) return RuleResult.Fail("ItemUnavailable");
            var quota = CBMSurgery.CanPrepare(pawn, part, def);
            if (!quota.Allowed) return quota;
            var installable = CBMConditionUtility.CanInstall(item);
            if (!installable.Allowed) return installable;
            var module = (Hediff_CBMModule)HediffMaker.MakeHediff(def, pawn, part);
            CBMConditionUtility.ApplyItem(item, module);
            var rules = Build(pawn);
            var result = rules.InsertHot(module.State(), board, x, y, rotated);
            if (!result.Allowed) return result;
            module.Apply(rules.Modules.First(m => m.Id == module.GetUniqueLoadID()));
            // The item is already held by this pawn. No remote inventory lookup or teleport.
            var unit = item.SplitOff(1);
            pawn.health.AddHediff(module, part);
            unit.Destroy();
            return RuleResult.Ok();
        }
    }

    public class Recipe_PrepareCBM : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            var rules = ImplantService.Build(pawn);
            return ImplantService.Parts(pawn).Where(p => CBMSurgery.BodyFits(pawn, p, recipe.addsHediff) && rules.CanAdd(new ModuleState { SpecId = recipe.addsHediff.defName,
                RootId = ImplantService.RootId(pawn, p) }).Allowed);
        }
        // Native medical jobs consume ingredients before ApplyOnPawn; retain the actual CBM until outcome is known.
        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
        {
            if (ingredient.TryGetComp<CompCBMCondition>() == null) base.ConsumeIngredient(ingredient, recipe, map);
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            var item = ingredients?.FirstOrDefault(t => t.def == recipe.addsHediff.GetModExtension<CBMExtension>().item);
            if (item == null && (bill != null || surgeon != null)) return;
            var allowed = CBMSurgery.CanPrepare(pawn, part, recipe.addsHediff, bill);
            if (!allowed.Allowed || !CBMConditionUtility.CanInstall(item).Allowed)
            { CBMSurgery.ReturnItem(item, pawn, surgeon); return; }
            if (surgeon != null && CheckSurgeryFail(surgeon, pawn, ingredients, part, bill))
            {
                CBMSurgicalOutcome.FailedInstall(item, pawn, surgeon, part, recipe.addsHediff.GetModExtension<CBMExtension>().installationSurgery, recipe.addsHediff);
                return;
            }
            var module = (Hediff_CBMModule)HediffMaker.MakeHediff(recipe.addsHediff, pawn, part);
            CBMConditionUtility.ApplyItem(item, module);
            pawn.health.AddHediff(module, part);
            if (!ImplantService.Modules(pawn).Contains(module)) { CBMSurgery.ReturnItem(item, pawn, surgeon); return; }
            if (item != null) item.SplitOff(1).Destroy();
            if (surgeon != null) TaleRecorder.RecordTale(TaleDefOf.DidSurgery, surgeon, pawn);
        }
    }

    public class Bill_RemoveCBM : Bill_Medical
    {
        public Hediff_CBMModule module;
        public Bill_RemoveCBM() { }
        public Bill_RemoveCBM(RecipeDef recipe, Hediff_CBMModule target) : base(recipe, null) { module = target; }
        public override string Label => base.Label + (module == null ? "" : " · " + module.LabelCap);
        public override bool CompletableEver => base.CompletableEver && module != null && ImplantService.Modules(GiverPawn).Contains(module)
            && ImplantService.Build(GiverPawn).Detach(module.GetUniqueLoadID(), true, false).Allowed;
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && module != null && !ImplantService.Modules(module.pawn).Contains(module)) module = null;
            Scribe_References.Look(ref module, "cbmTarget");
        }
    }

    public class Recipe_RemoveCBM : Recipe_Surgery
    {
        // Created only from the CBM panel, with an exact instance reference.
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe) => ImplantService.Modules(pawn).Select(m => m.Part).Distinct();
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            var module = (bill as Bill_RemoveCBM)?.module;
            if (module == null || module.Part != part || !ImplantService.Modules(pawn).Contains(module) || !pawn.Spawned) return;
            if (!ImplantService.Build(pawn).Detach(module.GetUniqueLoadID(), true, false).Allowed) return;
            if (surgeon != null && CheckSurgeryFail(surgeon, pawn, ingredients, part, bill))
            { CBMSurgicalOutcome.FailedRemoval(module, surgeon); return; }
            var item = CBMConditionUtility.MakeItem(module, true);
            if (!GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near)) { item.Destroy(); return; }
            pawn.health.RemoveHediff(module);
            if (surgeon != null) TaleRecorder.RecordTale(TaleDefOf.DidSurgery, surgeon, pawn);
        }
    }
}
