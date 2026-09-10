using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCBM
{
    public class CBMFuelOption
    {
        public ThingDef item;
        public float unitsPerItem = 1, energyWdPerUnit = 1;
        public float massPerUnit = -1;
        public Type workerClass = typeof(CBMFuelSourceWorker);
        private CBMFuelSourceWorker worker;
        public CBMFuelSourceWorker Worker => worker ?? (worker = (CBMFuelSourceWorker)Activator.CreateInstance(workerClass));
    }
    public class CBMFuelSpec
    {
        public float capacity = 20, efficiency = 1;
        public int refillTicks = 180;
        public List<CBMFuelOption> options = new List<CBMFuelOption>();
        public virtual IEnumerable<string> ConfigErrors()
        {
            foreach (var o in options)
                if (o.workerClass == null || o.workerClass.IsAbstract || !typeof(CBMFuelSourceWorker).IsAssignableFrom(o.workerClass) || o.workerClass.GetConstructor(Type.EmptyTypes) == null
                    || float.IsNaN(o.massPerUnit) || float.IsInfinity(o.massPerUnit) || o.massPerUnit < -1) yield return "Invalid fuel source worker or mass.";
            if (!CBMSupply.Positive(capacity) || !CBMSupply.Positive(efficiency) || efficiency > 1 || refillTicks < 1 || options.Count == 0
                || options.Any(o => o.item == null || !CBMSupply.Positive(o.unitsPerItem) || !CBMSupply.Positive(o.energyWdPerUnit) || o.unitsPerItem > capacity)
                || options.GroupBy(o => o.item).Any(g => g.Count() > 1)) yield return "Invalid implant fuel specification.";
        }
    }
    public class CBMFuelState : IExposable
    {
        public ThingDef item, preferred;
        public float units, threshold = .25f, target = 1;
        public bool automatic, requested;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref item, "item"); Scribe_Defs.Look(ref preferred, "preferred");
            Scribe_Values.Look(ref units, "units"); Scribe_Values.Look(ref automatic, "automatic"); Scribe_Values.Look(ref requested, "requested");
            Scribe_Values.Look(ref threshold, "threshold", .25f); Scribe_Values.Look(ref target, "target", 1f);
        }
        public CBMFuelState Copy() => new CBMFuelState { item = item, preferred = preferred, units = units, automatic = automatic, requested = requested, threshold = threshold, target = target };
        public float Mass(CBMFuelSpec spec)
        {
            var option = spec?.options.FirstOrDefault(o => o.item == item);
            return option == null || item == null ? 0 : units * (option.massPerUnit >= 0 ? option.massPerUnit : item.GetStatValueAbstract(StatDefOf.Mass) / option.unitsPerItem);
        }
    }
    public static class CBMSupply
    {
        public static bool Positive(float n) => n > 0 && !float.IsInfinity(n) && !float.IsNaN(n);
        public static CBMFuelSpec Spec(Hediff_CBMModule m) => m?.def.GetModExtension<CBMExtension>().fuel;
        public static float FuelMass(Hediff_CBMModule m) => m.fuelState.Mass(Spec(m));
        public static float AvailableEnergy(Hediff_CBMModule m)
        {
            var spec = Spec(m); if (spec == null) return float.MaxValue;
            var opt = spec.options.FirstOrDefault(o => o.item == m.fuelState.item);
            return opt == null ? 0 : m.fuelState.units * opt.energyWdPerUnit * spec.efficiency;
        }
        public static void ConsumeEnergy(Hediff_CBMModule m, float energy)
        {
            var spec = Spec(m); if (spec == null || energy <= 0) return;
            var opt = spec.options.FirstOrDefault(o => o.item == m.fuelState.item);
            if (opt == null) return;
            m.fuelState.units = Mathf.Max(0, m.fuelState.units - energy / (opt.energyWdPerUnit * spec.efficiency));
            if (m.fuelState.units < .000001f) { m.fuelState.units = 0; m.fuelState.item = null; }
            CBMBurden.Invalidate(m.pawn);
        }
        public static bool Carried(Pawn p, Thing t) => t != null && p.inventory?.innerContainer.Contains(t) == true;
        public static bool Operator(Pawn p) => ImplantService.CanHotSwap(p) && p.Spawned && !p.InMentalState;
        public static RuleResult CanRefill(Pawn p, Hediff_CBMModule m, Thing source)
        {
            if (!Operator(p) || !ImplantService.Modules(p).Contains(m) || !m.HardwareReady) return RuleResult.Fail("OperatorUnavailable");
            var spec = Spec(m); var opt = spec?.options.FirstOrDefault(o => o.item == source?.def);
            if (opt == null || source.Destroyed || opt.Worker.AvailablePortions(source) < 1 || m.fuelState.units > .000001f && m.fuelState.item != source.def) return RuleResult.Fail("FuelMismatch");
            if (spec.capacity - m.fuelState.units + .000001f < opt.unitsPerItem) return RuleResult.Fail("FuelFull");
            if (Carried(p, source)) return RuleResult.Ok();
            if (!source.Spawned || source.Map != p.Map || source.IsForbidden(p) || source.IsBurning() || !p.CanReserveAndReach(source, PathEndMode.Touch, Danger.Some)) return RuleResult.Fail("ItemUnavailable");
            return RuleResult.Ok();
        }
        public static int Refill(Hediff_CBMModule m, Thing source, float target)
        {
            if (!CanRefill(m.pawn, m, source).Allowed || !Carried(m.pawn, source) && !m.pawn.Position.AdjacentTo8WayOrInside(source)) return 0;
            var spec = Spec(m); var option = spec.options.First(o => o.item == source.def);
            int count = Math.Min(option.Worker.AvailablePortions(source), Mathf.FloorToInt((spec.capacity * Mathf.Clamp01(target) - m.fuelState.units + .000001f) / option.unitsPerItem));
            if (count <= 0) return 0;
            if (!option.Worker.TryConsume(source, count)) return 0;
            m.fuelState.item = source.def; m.fuelState.units += count * option.unitsPerItem;
            CBMBurden.Invalidate(m.pawn); PanelSnapshot.Invalidate(); return count;
        }
        public static Job RefillJob(Pawn p, Hediff_CBMModule m, Thing source, bool automatic)
        {
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RimCBM_Refill"), source);
            job.count = m.loadID; job.playerForced = !automatic; return job;
        }
        public static void Order(Pawn p, Job job)
        {
            // Vanilla compares job/targets but not our implant instance id.
            if (p.CurJobDef == job.def && p.CurJob.count != job.count) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (!p.jobs.TryTakeOrderedJob(job)) CBMTargeting.Report(RuleResult.Fail("OperatorUnavailable"));
        }
        public static float ChargeRate(Pawn p) => ImplantService.Modules(p).Where(m => m.HardwareReady && CBMAccess.Runtime(m) && m.empUntilTick <= Find.TickManager.TicksGame)
            .Sum(m => m.def.GetModExtension<CBMExtension>().chargeAcceptanceWatts * Mathf.Clamp01(m.durability));
        public static RuleResult CanCharge(Pawn p, Thing station, bool allowFull = false)
        {
            if (!Operator(p) || station == null || !station.Spawned || station.Map != p.Map || station.Faction != p.Faction || station.IsForbidden(p)
                || !p.CanReserveAndReach(station, PathEndMode.Touch, Danger.Some)) return RuleResult.Fail("OperatorUnavailable");
            var charger = station.TryGetComp<CompCBMCharger>();
            if (charger == null || !charger.Ready || ChargeRate(p) <= 0) return RuleResult.Fail("ChargerUnavailable");
            var e = CBMEnergy.For(p);
            return e.capacityWd > 0 && (allowFull || e.capacityWd > e.storedWd + .00001f) ? RuleResult.Ok() : RuleResult.Fail("FuelFull");
        }
        public static Job ChargeJob(Pawn p, Thing station, bool automatic)
        {
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RimCBM_ChargeAtStation"), station);
            job.playerForced = !automatic; return job;
        }
        public static Job AutomaticJob(Pawn p)
        {
            if (!Operator(p) || !p.IsColonistPlayerControlled || p.Drafted || p.needs?.food?.CurLevelPercentage < .3f || p.needs?.rest?.CurLevelPercentage < .25f) return null;
            foreach (var m in ImplantService.Modules(p).Where(m => m.HardwareReady && m.fuelState.automatic && Spec(m) != null))
            {
                var f = m.fuelState; var spec = Spec(m);
                if (f.units / spec.capacity <= f.threshold) f.requested = true;
                if (f.units / spec.capacity >= f.target - .00001f) f.requested = false;
                if (!f.requested) continue;
                var source = spec.options.SelectMany(o => p.Map.listerThings.ThingsOfDef(o.item)).Concat(p.inventory?.innerContainer.ToList() ?? new List<Thing>()).Distinct().Where(t => spec.options.Any(o => o.item == t.def && o.unitsPerItem <= spec.capacity * f.target - f.units + .000001f)
                    && (f.preferred == null || t.def == f.preferred) && CanRefill(p, m, t).Allowed).OrderBy(t => Carried(p, t) ? -1 : t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (source != null) return RefillJob(p, m, source, true);
            }
            var energy = CBMEnergy.For(p);
            if (!energy.autoCharge) energy.chargeRequested = false;
            else if (energy.capacityWd > 0 && energy.storedWd / energy.capacityWd <= energy.chargeThreshold) energy.chargeRequested = true;
            if (energy.capacityWd <= 0 || energy.storedWd / energy.capacityWd >= energy.chargeTarget - .00001f) energy.chargeRequested = false;
            if (energy.autoCharge && energy.chargeRequested)
            {
                var station = p.Map.listerBuildings.allBuildingsColonist.Where(t => t.TryGetComp<CompCBMCharger>() != null && CanCharge(p, t).Allowed)
                    .OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (station != null) return ChargeJob(p, station, true);
            }
            return null;
        }
        public static string FuelLabel(Hediff_CBMModule m) => m.def.LabelCap + " · " + m.Part.LabelCap + ": " + m.fuelState.units.ToString("0.##") + "/" + Spec(m).capacity + " · " + (m.fuelState.item?.LabelCap.ToString() ?? "RC_FuelEmpty".Translate().ToString());
        public static IEnumerable<FloatMenuOption> TargetOptions(Pawn p, Thing target)
        {
            foreach (var m in ImplantService.Modules(p).Where(m => Spec(m)?.options.Any(o => o.item == target.def) == true))
            {
                var result = CanRefill(p, m, target);
                yield return new FloatMenuOption("RC_Refill".Translate() + ": " + FuelLabel(m) + (result.Allowed ? "" : " (" + ("RC_Error_" + result.Error).Translate().ToString() + ")"),
                    result.Allowed ? (Action)(() => { if (CanRefill(p, m, target).Allowed) Order(p, RefillJob(p, m, target, false)); }) : null);
            }
            if (target.TryGetComp<CompCBMCharger>() != null)
            {
                var result = CanCharge(p, target);
                yield return new FloatMenuOption("RC_ChargeStation".Translate() + (result.Allowed ? "" : " (" + ("RC_Error_" + result.Error).Translate().ToString() + ")"),
                    result.Allowed ? (Action)(() => { if (CanCharge(p, target).Allowed) Order(p, ChargeJob(p, target, false)); }) : null);
            }
        }
        public static IEnumerable<FloatMenuOption> PolicyOptions(Pawn p)
        {
            var energy = CBMEnergy.For(p);
            if (ChargeRate(p) > 0)
            {
                yield return new FloatMenuOption("RC_ChargeStation".Translate(), () => Find.Targeter.BeginTargeting(new TargetingParameters { canTargetBuildings = true }, t => {
                    var result = CanCharge(p, t.Thing); if (result.Allowed) Order(p, ChargeJob(p, t.Thing, false)); else CBMTargeting.Report(result);
                }));
                yield return new FloatMenuOption("RC_ChargePolicy".Translate() + ": " + (energy.autoCharge ? energy.chargeThreshold.ToStringPercent() + " → " + energy.chargeTarget.ToStringPercent() : "RC_Off".Translate().ToString()), () => {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                        new FloatMenuOption("RC_Off".Translate(), () => { energy.autoCharge = false; energy.chargeRequested = false; }),
                        new FloatMenuOption("25% → 90%", () => { energy.autoCharge = true; energy.chargeThreshold = .25f; energy.chargeTarget = .9f; }),
                        new FloatMenuOption("50% → 100%", () => { energy.autoCharge = true; energy.chargeThreshold = .5f; energy.chargeTarget = 1; }) }));
                });
            }
            foreach (var m in ImplantService.Modules(p).Where(m => Spec(m) != null))
            {
                yield return new FloatMenuOption(FuelLabel(m), () => {
                    if (!ImplantService.Modules(p).Contains(m)) return;
                    var f = m.fuelState;
                    var list = new List<FloatMenuOption> {
                        new FloatMenuOption("RC_Refill".Translate(), () => Find.Targeter.BeginTargeting(new TargetingParameters { canTargetItems = true }, t => {
                            var result = CanRefill(p, m, t.Thing); if (result.Allowed) Order(p, RefillJob(p, m, t.Thing, false)); else CBMTargeting.Report(result);
                        })),
                        new FloatMenuOption("RC_AutoFuelOff".Translate(), () => { f.automatic = false; f.requested = false; }),
                        new FloatMenuOption("RC_AutoFuel".Translate() + " 25% → 100%", () => { f.automatic = true; f.threshold = .25f; f.target = 1; }),
                        new FloatMenuOption("RC_AutoFuel".Translate() + " 50% → 100%", () => { f.automatic = true; f.threshold = .5f; f.target = 1; }),
                        new FloatMenuOption("RC_AnyFuel".Translate(), () => f.preferred = null) };
                    foreach (var carried in p.inventory?.innerContainer.ToList() ?? new List<Thing>())
                        if (CanRefill(p, m, carried).Allowed) list.Add(new FloatMenuOption("RC_CarriedFuel".Translate() + ": " + carried.LabelCap,
                            () => { if (CanRefill(p, m, carried).Allowed) Order(p, RefillJob(p, m, carried, false)); }));
                    foreach (var option in Spec(m).options) list.Add(new FloatMenuOption("RC_FuelPrefer".Translate() + ": " + option.item.LabelCap, () => f.preferred = option.item));
                    Find.WindowStack.Add(new FloatMenu(list));
                });
            }
        }
    }
    public class JobDriver_CBMRefill : JobDriver
    {
        private Hediff_CBMModule Module => ImplantService.Modules(pawn).FirstOrDefault(m => m.loadID == job.count);
        public override bool TryMakePreToilReservations(bool errorOnFailed) => Module != null && (CBMSupply.Carried(pawn, job.targetA.Thing) || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed));
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Module == null || !CBMSupply.CanRefill(pawn, Module, job.targetA.Thing).Allowed || !job.playerForced && !Module.fuelState.automatic);
            if (!CBMSupply.Carried(pawn, job.targetA.Thing)) yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(CBMSupply.Spec(Module)?.refillTicks ?? 180).WithProgressBarToilDelay(TargetIndex.A).FailOn(() => !CBMSupply.Carried(pawn, job.targetA.Thing) && !pawn.Position.AdjacentTo8WayOrInside(job.targetA.Thing));
            yield return Toils_General.Do(() => CBMSupply.Refill(Module, job.targetA.Thing, job.playerForced ? 1 : Module.fuelState.target));
        }
    }
    public class JobDriver_CBMChargeAtStation : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !CBMSupply.CanCharge(pawn, job.targetA.Thing, true).Allowed || !job.playerForced && !CBMEnergy.For(pawn).autoCharge);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var charge = ToilMaker.MakeToil("CBMChargeStation"); charge.defaultCompleteMode = ToilCompleteMode.Never;
            charge.tickAction = () => {
                if (!pawn.Position.AdjacentTo8WayOrInside(job.targetA.Thing)) { EndJobWith(JobCondition.Incompletable); return; }
                if (!job.playerForced && (pawn.needs?.food?.CurLevelPercentage < .3f || pawn.needs?.rest?.CurLevelPercentage < .25f)) { EndJobWith(JobCondition.InterruptOptional); return; }
                var record = CBMEnergy.For(pawn); float target = job.playerForced ? 1 : record.chargeTarget;
                if (record.storedWd >= record.capacityWd * target - .00001f) { ReadyForNextToil(); return; }
                job.targetA.Thing.TryGetComp<CompCBMCharger>().Transfer(pawn, target);
            };
            yield return charge;
        }
    }
    [HarmonyPatch(typeof(JobGiver_Work), "TryIssueJobPackage")]
    public static class CBMAutomaticSupply
    {
        static bool Prefix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (__instance.emergency) return true;
            var job = CBMSupply.AutomaticJob(pawn); if (job == null) return true;
            __result = new ThinkResult(job, __instance); return false;
        }
    }
    public class CompProperties_CBMCharger : CompProperties
    {
        public float watts = 500, efficiency = .9f, bufferWd = 5;
        public Type workerClass = typeof(CBMChargeSourceWorker);
        private CBMChargeSourceWorker worker;
        public CBMChargeSourceWorker Worker => worker ?? (worker = (CBMChargeSourceWorker)Activator.CreateInstance(workerClass));
        public CompProperties_CBMCharger() { compClass = typeof(CompCBMCharger); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            if (workerClass == null || workerClass.IsAbstract || !typeof(CBMChargeSourceWorker).IsAssignableFrom(workerClass) || workerClass.GetConstructor(Type.EmptyTypes) == null) yield return "Invalid charging source worker.";
            if (!CBMSupply.Positive(watts) || !CBMSupply.Positive(bufferWd) || !CBMSupply.Positive(efficiency) || efficiency > 1) yield return "Invalid implant charger settings.";
        }
    }
    public class CompCBMCharger : ThingComp
    {
        public float buffer;
        public CompProperties_CBMCharger Settings => (CompProperties_CBMCharger)props;
        public bool Ready => parent.Spawned && !parent.IsBurning() && Settings.Worker.Available(this)
            && parent.TryGetComp<CompBreakdownable>()?.BrokenDown != true && FlickUtility.WantsToBeOn(parent);
        public override void PostExposeData() { Scribe_Values.Look(ref buffer, "cbmChargeBuffer"); }
        public override void CompTick()
        {
            Settings.Worker.Tick(this);
            buffer = float.IsNaN(buffer) || float.IsInfinity(buffer) ? 0 : Mathf.Clamp(buffer, 0, Settings.bufferWd);
        }
        public float Transfer(Pawn p, float target)
        {
            if (!Ready || !p.Spawned || p.Map != parent.Map || !p.Position.AdjacentTo8WayOrInside(parent)) return 0;
            var record = CBMEnergy.For(p);
            float amount = Mathf.Min(buffer, Mathf.Min(Mathf.Min(Settings.watts * Settings.efficiency, CBMSupply.ChargeRate(p)) * CompPower.WattsToWattDaysPerTick,
                Mathf.Max(0, record.capacityWd * Mathf.Clamp01(target) - record.storedWd)));
            buffer -= amount; record.storedWd += amount; return amount;
        }
        public override string CompInspectStringExtra() => "RC_ChargeStation".Translate() + ": " + buffer.ToString("0.##") + "/" + Settings.bufferWd + " Wd · " + Settings.watts + " W · " + Settings.efficiency.ToStringPercent();
    }
}
