using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RimCBM;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

public class RimCBMProbe : GameComponent
{
    static int phase;
    static float deadline;
    static bool inputChecked;
    static int relocationTick;
    static Thing chargingBattery;
    static int chargingStart;
    static int supplyTick;
    static float supplyChargeBefore;
    static string output = Environment.GetEnvironmentVariable("RIMCBM_TEST_OUTPUT");
    public RimCBMProbe(Game game)
    {
    }
    static void Check(bool ok, string message)
    {
        if (!ok)
            throw new Exception(message);
        File.AppendAllText(Path.Combine(output, "checks.txt"), "PASS " + message + "\n");
    }
    public override void GameComponentUpdate()
    {
        if (string.IsNullOrEmpty(output) || phase < 0 || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting)
            return;
        Application.runInBackground = true;
        if (phase == 1 || phase == 2)
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        try
        {
            if (Environment.GetEnvironmentVariable("RIMCBM_INTERACTIVE") == "1")
            {
                if (phase >= 10 && phase <= 23)
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                if (phase == 0)
                {
                    phase = 10;
                    Demo();
                    deadline = Time.realtimeSinceStartup + 4;
                }
                else if (phase == 10 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "demo.png"));
                    phase = 11;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 11 && Time.realtimeSinceStartup > deadline)
                {
                    var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "RimCBM");
                    var pending = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.SurgeryPrepared && m.intentBoard == null && m.Part == pawn.RaceProps.body.corePart);
                    typeof(GridPanel).GetField("selected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.Installed).GetUniqueLoadID());
                    typeof(GridPanel).GetField("placing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, pending.GetUniqueLoadID());
                    typeof(GridPanel).GetField("rotated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, false);
                    phase = 12;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 12 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "boundary.png"));
                    phase = 13;
                    deadline = Time.realtimeSinceStartup + 1;
                }
                else if (phase == 13 && Time.realtimeSinceStartup > deadline)
                {
                    typeof(GridPanel).GetMethod("CancelPlacement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
                    typeof(GridPanel).GetField("gridScroll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, new Vector2(0, 220));
                    var selectedPawn = GridPanel.SelectedPawn;
                    var burnt = ImplantService.Modules(selectedPawn).FirstOrDefault(m => m.condition.HasFlag(CBMCondition.BurntOut));
                    if (burnt != null)
                        typeof(GridPanel).GetField("selected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, burnt.GetUniqueLoadID());
                    phase = 18;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 18 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "stacked.png"));
                    phase = 19;
                    deadline = Time.realtimeSinceStartup + 1;
                }
                else if (phase == 19 && Time.realtimeSinceStartup > deadline)
                {
                    CBMPanelState.SetOpen(GridPanel.SelectedPawn, false);
                    typeof(GridPanel).GetField("gridScroll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, Vector2.zero);
                    phase = 14;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 14 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "closed.png"));
                    phase = 15;
                    deadline = Time.realtimeSinceStartup + 1;
                }
                else if (phase == 15 && Time.realtimeSinceStartup > deadline)
                {
                    CBMPanelState.SetOpen(GridPanel.SelectedPawn, true);
                    Prefs.UIScale = 1.5f;
                    phase = 16;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 16 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "compact.png"));
                    phase = 17;
                    deadline = Time.realtimeSinceStartup + 1;
                }
                else if (phase == 17 && Time.realtimeSinceStartup > deadline)
                {
                    Prefs.UIScale = 1f;
                    var pawn = GridPanel.SelectedPawn;
                    var suite = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_AbilityRig");
                    GridPanel.FocusModule(pawn, suite);
                    var command = pawn.GetGizmos().OfType<Command_CBMControl>().Single();
                    Check(CBMControlMenu.Options(pawn).First().Label == CBMEnergy.Display(pawn), "quick menu begins with dedicated current capacity row");
                    command.action();
                    Find.WindowStack.Windows.OfType<FloatMenu>().Last().vanishIfMouseDistant = false;
                    Check(Find.WindowStack.Windows.OfType<FloatMenu>().Any() && !Find.WindowStack.Windows.OfType<Dialog_CBMAbilities>().Any(),
                        "CBM gizmo opens native quick menu without ability dialog");
                    phase = 20;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 20 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "energy.png"));
                    phase = 23;
                    deadline = Time.realtimeSinceStartup + 1;
                }
                else if (phase == 23 && Time.realtimeSinceStartup > deadline)
                {
                    var pawn = GridPanel.SelectedPawn;
                    Find.WindowStack.TryRemove(typeof(FloatMenu), false);
                    var suite = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_AbilityRig");
                    Find.WindowStack.Add(new Dialog_InfoCard(suite));
                    Check(suite.SpecialDisplayStats(StatRequest.For(pawn)).Any(r => r.LabelCap == "RC_Info_Install".Translate()), "installed info page includes surgical success ceiling");
                    phase = 21;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 21 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "info.png"));
                    var entries = (List<StatDrawEntry>)typeof(StatsReportUtility).GetField("cachedDrawEntries", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                    int index = entries.FindIndex(e => e.LabelCap == "RC_Info_Install".Translate());
                    Check(index >= 0, "native information window draws implant surgery entry");
                    StatsReportUtility.SelectEntry(index);
                    phase = 22;
                    deadline = Time.realtimeSinceStartup + 2;
                }
                else if (phase == 22 && Time.realtimeSinceStartup > deadline)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "surgery-info.png"));
                    GameDataSaveLoader.SaveGame("RimCBM-interactive");
                    File.WriteAllText(Path.Combine(output, "ready.txt"), "Interactive demo ready; game will stay open.");
                    phase = -1;
                }
                return;
            }
            if (phase == 0)
            {
                phase = 1;
                Run();
                deadline = Time.realtimeSinceStartup + 3;
            }
            else if (phase == 1 && Time.realtimeSinceStartup > deadline)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "health-wide.png"));
                GameDataSaveLoader.SaveGame("RimCBM-probe");
                phase = 2;
                deadline = Time.realtimeSinceStartup + 3;
            }
            else if (phase == 2 && Time.realtimeSinceStartup > deadline)
            {
                phase = 3;
                GameDataSaveLoader.LoadGame("RimCBM-probe");
            }
            else if (phase == 3)
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "RimCBM");
                Check(ImplantService.Build(pawn).Validate().Allowed, "save/load layout validation");
                Check(ImplantService.Modules(pawn).Count(m => m.status == ModuleStatus.SurgeryPrepared) == 1, "save/load surgery-only pending preserved");
                var savedItem = pawn.inventory.innerContainer.First(t => t.def.defName == "RimCBM_PlaceholderItem").TryGetComp<CompCBMCondition>();
                Check(savedItem.condition == (CBMCondition.Dirty | CBMCondition.Unsterile) && Math.Abs(savedItem.durability - .42f) < .001f && !savedItem.active,
                    "physical CBM condition durability and switch survive save/load");
                Check(Math.Abs(CBMEnergy.For(pawn).storedWd - 12.5f) < .001f
                    && ImplantService.Modules(pawn).Any(m => m.def.defName == "RimCBM_Overdrive" && m.empUntilTick > Find.TickManager.TicksGame),
                    "Wd energy and EMP expiration survive save/load");
                var savedRig = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_AbilityRig");
                var savedReactive = CBMAbilities.State(savedRig, CBMAbilities.Specs(savedRig).First(a => a.id == "repair"));
                Check(savedReactive.reserveWd == 10 && savedReactive.priority == 3 && savedReactive.cooldownUntil > Find.TickManager.TicksGame,
                    "ability cooldown priority and reserve survive save/load by stable id");
                var savedPortable = pawn.inventory.innerContainer.First(t => t.def.defName == "RimCBM_AbilityRigItem").TryGetComp<CompCBMCondition>();
                Check(savedPortable.modeId == "off" && savedRig.modeId == "off", "physical and installed mode selections survive game save/load");
                Check(savedPortable.abilityStates.Any(a => a.id == "repair" && a.cooldownUntil > Find.TickManager.TicksGame), "physical ability cooldown survives save/load");
                var bills = pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().ToList();
                Check(bills.Count == 1 && bills[0].module != null && bills[0].module.pawn == pawn, "exact surgical target cross-reference survives save/load");
                var removing = bills[0].module;
                int before = ImplantService.Modules(pawn).Count();
                bills[0].recipe.Worker.ApplyOnPawn(pawn, removing.Part, null, new List<Thing>(), bills[0]);
                Check(ImplantService.Modules(pawn).Count() == before - 1 && !ImplantService.Modules(pawn).Contains(removing), "extraction worker removes only bound instance");
                Check(pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_ControllerItem")).Count == 1, "extraction worker returns a physical CBM");
                var dynamicPawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Dynamic");
                Check(ImplantService.Modules(dynamicPawn).Count() == 2 && ImplantService.Build(dynamicPawn).Validate().Allowed,
                    "save/load retains partially supported modules");
                Check(CBMPanelState.IsOpen(pawn) && !CBMPanelState.IsOpen(dynamicPawn), "per-pawn panel visibility survives save/load");
                var savedStatus = ImplantService.Modules(dynamicPawn).First(m => m.def.defName == "RimCBM_Placeholder");
                Check(Math.Abs(savedStatus.durability - .6f) < .001f && !savedStatus.active, "durability and indicator state survive save/load");
                var dynamicArm = ImplantService.Modules(dynamicPawn).First().Part;
                dynamicPawn.health.AddHediff(HediffDefOf.MissingBodyPart, dynamicArm);
                CBMCapacityComponent.Reconcile(dynamicPawn);
                Check(!ImplantService.Modules(dynamicPawn).Any(), "missing limb drops retained modules instead of deleting them silently");
                Check(dynamicPawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_BayItem")).Sum(t => t.stackCount) == 1,
                    "missing limb materializes the retained expansion provider");
                Select(pawn);
                Prefs.UIScale = 1.5f;
                CBMPanelState.SetOpen(pawn, true);
                Check(ImplantService.Modules(pawn).Count(m => m.intentBoard != null) == 1
                    && pawn.CurJobDef == CBMRelocation.JobDef, "relocation target and running action survive save/load");
                relocationTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                phase = 4;
                deadline = Time.realtimeSinceStartup + 15;
            }
            else if (phase == 4 && (Find.TickManager.TicksGame >= relocationTick + 150 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "RimCBM");
                Check(ImplantService.Modules(pawn).Any(m => m.x == 2 && m.y == 2 && m.status == ModuleStatus.Installed)
                    && ImplantService.Modules(pawn).All(m => m.intentBoard == null), "pawn action completes relocation and releases reservation after load");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                ConditionTests(Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Dynamic"));
                SurgeryTests(pawn);
                EconomyTests(pawn);
                BurdenTests();
                CombatDamageTests(pawn);
                ReliabilityTests(pawn);
                AccessTests(pawn);
                EffectRuntimeTests(pawn);
                ReviewFixTests(pawn);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "health-compact.png"));
                chargingBattery = ThingMaker.MakeThing(ThingDef.Named("Battery"));
                chargingBattery.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(chargingBattery, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3), pawn.Map);
                chargingBattery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
                CBMEnergy.For(pawn).storedWd = 0;
                Check(!CBMCharging.Queue(pawn, chargingBattery).Allowed, "direct battery charging is rejected");
                pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RimCBM_Charge"), chargingBattery));
                chargingStart = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                phase = 50;
                deadline = Time.realtimeSinceStartup + 20;
            }
            else if (phase == 50 && (Find.TickManager.TicksGame > chargingStart + 240 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "RimCBM");
                Check(chargingBattery.TryGetComp<CompPowerBattery>().StoredEnergy > 600 - (Find.TickManager.TicksGame - chargingStart + 2) * .0001f && pawn.CurJobDef?.defName != "RimCBM_Charge",
                    "legacy charge job terminates with only native 5W battery self-discharge");
                pawn.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
                chargingBattery.Destroy();
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                SupplyTests(pawn);
                supplyTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                phase = 51;
                deadline = Time.realtimeSinceStartup + 20;
            }
            else if (phase == 51 && (Find.TickManager.TicksGame > supplyTick + 60 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Supply");
                Check(pawn.CurJobDef?.defName == "RimCBM_Refill" && ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_FuelGenerator").fuelState.units == 0,
                    "refill job reserves and waits without early material consumption");
                var fuelSource = pawn.CurJob.targetA.Thing;
                int fuelCount = fuelSource.stackCount;
                var module = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_FuelGenerator");
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Check(fuelSource.stackCount == fuelCount && module.fuelState.units == 0, "cancelled refill leaves both stock and tank unchanged");
                CBMSupply.Order(pawn, CBMSupply.RefillJob(pawn, module, fuelSource, false));
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                GameDataSaveLoader.SaveGame("RimCBM-supply");
                phase = 52;
                deadline = Time.realtimeSinceStartup + 1;
            }
            else if (phase == 52 && Time.realtimeSinceStartup > deadline)
            {
                phase = 53;
                GameDataSaveLoader.LoadGame("RimCBM-supply");
            }
            else if (phase == 53)
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Supply");
                var module = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_FuelGenerator");
                Check(pawn.CurJobDef?.defName == "RimCBM_Refill" && pawn.CurJob.count == module.loadID && module.fuelState.preferred == ThingDefOf.Chemfuel,
                    "refill job retains exact implant identity and fuel policy through save/load");
                var portable = pawn.inventory.innerContainer.First(t => t.def.defName == "RimCBM_FuelGeneratorItem").TryGetComp<CompCBMCondition>();
                Check(Math.Abs(portable.fuelState.units - 2.5f) < .001f && portable.fuelState.item == ThingDefOf.Chemfuel,
                    "fractional physical fuel survives full game save/load");
                var savedData = Find.CurrentMap.mapPawns.FreeColonists.SelectMany(p => p.inventory.innerContainer).Select(t => t.TryGetComp<CompCBMCondition>()).Where(c => c != null)
                    .SelectMany(c => c.abilityStates).FirstOrDefault(a => a.id == "reviewLife")?.data as ProbeAbilityData;
                Check(savedData?.value == 2, "custom per-instance ability data survives full game save/load");
                Check(CBMEnergy.For(pawn).chargeTarget == .9f && CBMEnergy.For(pawn).chargeThreshold == .25f, "charging policy survives save/load");
                supplyTick = Find.TickManager.TicksGame;
                phase = 54;
                deadline = Time.realtimeSinceStartup + 20;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            }
            else if (phase == 54 && (Find.TickManager.TicksGame > supplyTick + 300 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Supply");
                Check(ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_FuelGenerator").fuelState.units == 20, "resumed refill completes once to tank capacity");
                var station = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_ChargingStation")).Single();
                Check(CBMSupply.CanCharge(pawn, station).Allowed && station.TryGetComp<CompPowerTrader>().PowerNet != null, "dedicated charger connects to a real vanilla power network: " + CBMSupply.CanCharge(pawn, station).Error + ", power=" + station.TryGetComp<CompPowerTrader>().PowerOn + ", net=" + (station.TryGetComp<CompPowerTrader>().PowerNet != null) + ", awake=" + pawn.Awake() + ", net energy=" + station.TryGetComp<CompPowerTrader>().PowerNet?.CurrentStoredEnergy());
                supplyChargeBefore = CBMEnergy.For(pawn).storedWd;
                CBMSupply.Order(pawn, CBMSupply.ChargeJob(pawn, station, false));
                supplyTick = Find.TickManager.TicksGame;
                phase = 55;
                deadline = Time.realtimeSinceStartup + 20;
            }
            else if (phase == 55 && (Find.TickManager.TicksGame > supplyTick + 180 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Supply");
                Check(CBMEnergy.For(pawn).storedWd > supplyChargeBefore && pawn.CurJobDef?.defName == "RimCBM_ChargeAtStation", "station job walks to interface and transfers energy over time");
                var station = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_ChargingStation")).Single();
                var other = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(other, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3), pawn.Map);
                Check(!other.CanReserve(station), "active charging reserves station for one pawn");
                other.Destroy();
                station.TryGetComp<CompFlickable>().SwitchIsOn = false;
                supplyChargeBefore = CBMEnergy.For(pawn).storedWd;
                supplyTick = Find.TickManager.TicksGame;
                phase = 56;
                deadline = Time.realtimeSinceStartup + 10;
            }
            else if (phase == 56 && (Find.TickManager.TicksGame > supplyTick + 30 || Time.realtimeSinceStartup > deadline))
            {
                var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "Supply");
                Check(pawn.CurJobDef?.defName != "RimCBM_ChargeAtStation" && CBMEnergy.For(pawn).storedWd == supplyChargeBefore, "switching off the station disconnects job without phantom charging");
                var caster = Find.CurrentMap.mapPawns.FreeColonists.First(p => ImplantService.Modules(p).Any(m => m.def.defName == "RimCBM_TargetRig"));
                var rig = ImplantService.Modules(caster).First(m => m.def.defName == "RimCBM_TargetRig");
                var ability = CBMAbilities.Specs(rig).First(a => a.id == "treat");
                var state = CBMAbilities.State(rig, ability);
                state.cooldownUntil = state.effectUntil = 0;
                state.lastTriggerTick = -1;
                var wound = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, caster, caster.RaceProps.body.corePart);
                wound.Severity = 10;
                caster.health.AddHediff(wound);
                CBMEnergy.For(caster).storedWd = 20;
                Check(CBMCasting.Queue(rig, ability).Allowed, "real healing action starts");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                GameDataSaveLoader.SaveGame("RimCBM-cast");
                phase = 57;
                deadline = Time.realtimeSinceStartup + 1;
            }
            else if (phase == 57 && Time.realtimeSinceStartup > deadline)
            {
                phase = 58;
                GameDataSaveLoader.LoadGame("RimCBM-cast");
            }
            else if (phase == 58)
            {
                var caster = Find.CurrentMap.mapPawns.FreeColonists.First(p => ImplantService.Modules(p).Any(m => m.def.defName == "RimCBM_TargetRig"));
                Check(caster.CurJob?.source is Hediff_CBMModule && caster.CurJob.dutyTag == "treat", "cast source and stable ability id survive game save/load");
                supplyTick = Find.TickManager.TicksGame;
                phase = 59;
                deadline = Time.realtimeSinceStartup + 15;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            }
            else if (phase == 59 && (Find.TickManager.TicksGame > supplyTick + 150 || Time.realtimeSinceStartup > deadline))
            {
                var caster = Find.CurrentMap.mapPawns.FreeColonists.First(p => ImplantService.Modules(p).Any(m => m.def.defName == "RimCBM_TargetRig"));
                var rig = ImplantService.Modules(caster).First(m => m.def.defName == "RimCBM_TargetRig");
                var state = CBMAbilities.State(rig, CBMAbilities.Specs(rig).First(a => a.id == "treat"));
                Check(state.cooldownUntil > supplyTick && caster.CurJobDef?.defName != "RimCBM_CastAbility", "saved healing action completes and commits cooldown");
                Check(caster.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.def == HediffDefOf.Cut).All(h => h.Severity < 10), "completed action heals native injury");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                phase = 5;
                deadline = Time.realtimeSinceStartup + 1;
            }
            else if (phase == 5 && Time.realtimeSinceStartup > deadline)
            {
                File.WriteAllText(Path.Combine(output, "complete.txt"), "PASS");
                phase = -1;
                Application.Quit();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
            Log.Error("RimCBM probe: " + ex);
            phase = -1;
            Application.Quit();
        }
    }
    public override void GameComponentOnGUI()
    {
        if (string.IsNullOrEmpty(output) || (phase != 1 && phase != 10) || inputChecked || Event.current.type != EventType.Repaint)
            return;
        inputChecked = true;
        var original = Event.current;
        try
        {
            var pawn = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.Name.ToStringShort == "RimCBM");
            var module = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.Installed);
            var snapshot = PanelSnapshot.Get(pawn);
            int builds = PanelSnapshot.Builds;
            for (int i = 0; i < 200; i++)
                PanelSnapshot.Get(pawn);
            Check(PanelSnapshot.Builds == builds, "200 unchanged UI snapshots perform zero rule rebuilds");
            PanelSnapshot.Invalidate();
            Check(!ReferenceEquals(snapshot, PanelSnapshot.Get(pawn)), "explicit snapshot invalidation creates fresh rules");
            Text.Font = GameFont.Small;
            if (phase == 1)
                Check(CBMControlMenu.Options(pawn).Any(o => o.Disabled), "native quick menu renders disabled options in GUI context");
            var selfMenu = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, pawn.DrawPos, out var selfContext);
            Check(CBMControlMenu.Options(pawn).All(o => selfMenu.Any(n => n.Label == o.Label)), "native right-click self contains the same CBM controls as gizmo");
            var powerTest = ThingMaker.MakeThing(ThingDef.Named("Battery"));
            powerTest.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(powerTest, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3), pawn.Map);
            powerTest.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
            var batteryMenu = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, powerTest.DrawPos, out var batteryContext);
            Check(!batteryMenu.Any(o => o.Label.StartsWith("RC_Charge".Translate().ToString())), "native right-click battery has no implant charging entry");
            var currentCharge = CBMEnergy.For(pawn).storedWd;
            CBMEnergy.For(pawn).storedWd = CBMEnergy.For(pawn).capacityWd;
            Check(!CBMCharging.CanCharge(pawn, powerTest).Allowed, "right-click charging rejects full storage");
            CBMEnergy.For(pawn).storedWd = currentCharge;
            powerTest.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(0);
            Check(!CBMCharging.CanCharge(pawn, powerTest).Allowed, "right-click charging rejects empty source");
            powerTest.Destroy();
            Find.Selector.ClearSelection();
            Check(CBMUnselectedPawnMenu.TryOpenAt(Find.Selector, pawn.DrawPos) && Find.Selector.SelectedPawns.Contains(pawn)
                && Find.WindowStack.WindowOfType<FloatMenu>() != null, "right-click unselected pawn selects it and opens the same quick menu");
            Find.WindowStack.WindowOfType<FloatMenu>().Close();
            var doctor = pawn.Map.mapPawns.FreeColonists.First(p => p != pawn);
            var friendlyMenu = FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, doctor.DrawPos, out var friendlyContext);
            Check(friendlyMenu.Any(o => o.Label.StartsWith(("RC_Implants".Translate() + ": ") + "RC_AbilityName_treat".Translate()))
                && new FloatMenuOptionProvider_CBM().GetOptionsFor(doctor, friendlyContext).Count() == 1
                && !friendlyMenu.Any(o => o.Label.StartsWith(("RC_Implants".Translate() + ": ") + "RC_AbilityName_shock".Translate())), "native target menu adds friendly treatment once and filters hostile attack");
            Event.current = new Event { type = EventType.MouseDown, button = 1, mousePosition = new Vector2(145, 8) };
            var rowMethod = typeof(HealthCardUtility).GetMethod("DrawHediffRow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            rowMethod.Invoke(null, new object[] { new Rect(0, 0, 360, 300), pawn, new Hediff[] { module }, 0f, 0f });
            var menu = Find.WindowStack.WindowOfType<FloatMenu>();
            Check(menu != null && Event.current.type == EventType.Used, "native health CBM row right-click opens context menu through patched renderer");
            menu?.Close();
            var target = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.Installed && m.boardId.StartsWith("bay/"));
            Event.current = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(145, 8) };
            rowMethod.Invoke(null, new object[] { new Rect(0, 0, 360, 300), pawn, new Hediff[] { target }, 0f, 0f });
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            Check(Event.current.type == EventType.Used && Find.WindowStack.WindowOfType<FloatMenu>() == null
                && (string)typeof(GridPanel).GetField("focusBoard", flags).GetValue(null) == target.boardId
                && (BodyPartRecord)typeof(GridPanel).GetField("part", flags).GetValue(null) == target.Part,
                "native CBM left click focuses its exact body part and extension board without a menu");
            var visibleMethod = typeof(HealthCardUtility).GetMethod("VisibleHediffs", flags);
            var visible = ((IEnumerable<Hediff>)visibleMethod.Invoke(null, new object[] { pawn, true })).OfType<Hediff_CBMModule>().ToList();
            Check(visible.All(m => m.status == ModuleStatus.Installed) && visible.Count == ImplantService.Modules(pawn).Count(m => m.status == ModuleStatus.Installed),
                "open native health list contains only deployed CBMs");
            Check(ImplantService.Modules(pawn).All(m => string.IsNullOrEmpty(m.LabelInBrackets)), "native CBM labels have no prepared or deployed suffix");
            var rootBoard = PanelSnapshot.Get(pawn).Boards.Values.First(b => b.Id.StartsWith("root/"));
            Check(rootBoard.Cells.Values.All(c => CBMColors.CellIndicator(c).a == 0f), "default cells have no corner indicator");
            CBMColors.TryColor("HealthGood", out var goodColor);
            CBMColors.TryColor("HealthEnhanced", out var blueColor);
            Check(goodColor == HealthUtility.GoodConditionColor && blueColor == new Color(.5f, .5f, .9f), "palette uses native health efficiency colors");
            GridPanel.FocusModule(pawn, module);
            Event.current = new Event { type = EventType.MouseDown, button = 1, mousePosition = new Vector2(40, 285) };
            GridPanel.Draw(new Rect(0, 0, 390, 410), pawn);
            Check(Find.WindowStack.WindowOfType<FloatMenu>() == null, "empty grid right-click produces no menu");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); phase = -1; Application.Quit(); }
        finally { Event.current = original; }
    }
    static void Select(Pawn pawn)
    {
        Find.Selector.ClearSelection();
        Find.Selector.Select(pawn);
        Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
        ((MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType = typeof(ITab_Pawn_Health);
    }
    static void Demo()
    {
        Directory.CreateDirectory(output);
        var baselinePolicy = DefDatabase<CBMLossPolicyDef>.AllDefsListForReading.First();
        baselinePolicy.removedRecoveryChance = 1;
        baselinePolicy.removedDamageChance = 0;
        baselinePolicy.removedBurnChance = 0;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        var pawn = Find.CurrentMap.mapPawns.FreeColonists.First();
        pawn.Name = new NameSingle("RimCBM");
        pawn.drafter.Drafted = true;
        var head = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Head");
        var suite = Prepare(pawn, head, "AbilityRig");
        Check(ImplantService.Place(pawn, suite.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, head)), 0, 0, false).Allowed, "demo ability suite installed");
        var leg = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Leg");
        var targetSuite = Prepare(pawn, leg, "TargetRig");
        Check(ImplantService.Place(pawn, targetSuite.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, leg)), 0, 0, false).Allowed, "demo target suite installed");
        var torso = pawn.RaceProps.body.corePart;
        var arms = pawn.RaceProps.body.AllParts.Where(p => p.def.defName == "Arm").ToList();
        pawn.health.AddHediff(HediffDef.Named("BionicArm"), arms[0]);
        string poweredBoard = GridRules.RootBoard(ImplantService.RootId(pawn, arms[0]));
        int poweredX = 0;
        foreach (string name in new[] { "Battery", "Metabolic", "Overdrive" })
        {
            var module = Prepare(pawn, arms[0], name);
            Check(ImplantService.Place(pawn, module.GetUniqueLoadID(), poweredBoard, poweredX++, 0, false).Allowed, "demo power module " + name);
        }
        CBMEnergy.For(pawn).storedWd = 40;
        CBMEnergy.Component.Step(pawn);
        var battery = ThingMaker.MakeThing(ThingDef.Named("Battery"));
        battery.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(battery, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3), pawn.Map);
        battery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
        var bay = Prepare(pawn, torso, "Bay");
        string main = GridRules.RootBoard(ImplantService.RootId(pawn, torso));
        Check(ImplantService.Place(pawn, bay.GetUniqueLoadID(), main, 0, 0, false).Allowed, "demo expansion board installed");
        var installed = Prepare(pawn, torso, "Placeholder");
        Check(ImplantService.Place(pawn, installed.GetUniqueLoadID(), main, 2, 2, false).Allowed, "demo fixed-cell placeholder installed");
        installed.durability = .65f;
        var hot = Prepare(pawn, torso, "Placeholder");
        Check(ImplantService.Place(pawn, hot.GetUniqueLoadID(), GridRules.BayBoard(bay.GetUniqueLoadID()), 0, 0, false).Allowed, "demo hot-cell placeholder installed");
        hot.condition = CBMCondition.BurntOut;
        hot.durability = 0;
        hot.active = false;
        for (int i = 0; i < 4; i++)
            Prepare(pawn, torso, "Placeholder");
        ImplantService.Modules(pawn).Last().condition = CBMCondition.Dirty | CBMCondition.Unsterile;
        foreach (var arm in arms)
            for (int i = 0; i < 2; i++)
                Prepare(pawn, arm, "Placeholder");
        var row = Prepare(pawn, torso, "Row");
        var column = Prepare(pawn, torso, "Column");
        Check(ImplantService.Place(pawn, row.GetUniqueLoadID(), main, 2, 0, false).Allowed, "demo row installed");
        Check(ImplantService.Place(pawn, column.GetUniqueLoadID(), main, 0, 2, false).Allowed, "demo column installed");
        for (int i = 0; i < 4; i++)
            pawn.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(ThingDef.Named("RimCBM_PlaceholderItem")));
        for (int i = 0; i < 4; i++)
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named("RimCBM_PlaceholderItem")), pawn.Position, pawn.Map, ThingPlaceMode.Near);
        var dirtyCarried = pawn.inventory.innerContainer.First(t => t.def.defName == "RimCBM_PlaceholderItem").TryGetComp<CompCBMCondition>();
        dirtyCarried.condition = CBMCondition.Dirty | CBMCondition.Unsterile;
        dirtyCarried.durability = .45f;
        var medicine = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
        medicine.stackCount = 20;
        GenPlace.TryPlaceThing(medicine, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        var doctor = pawn.Map.mapPawns.FreeColonists.FirstOrDefault(p => p != pawn);
        if (doctor != null)
        {
            doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 12;
            if (!doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
        }
        CBMPanelState.SetOpen(pawn, true);
        Select(pawn);
        Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
        var relocating = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.SurgeryPrepared && m.Part == torso);
        Check(CBMRelocation.Queue(pawn, relocating.GetUniqueLoadID(), GridRules.BayBoard(bay.GetUniqueLoadID()), 2, 2, false).Allowed,
            "demo relocation target reserved until pawn acts");
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }
    static Hediff_CBMModule Prepare(Pawn pawn, BodyPartRecord part, string shortName)
    {
        var recipe = DefDatabase<RecipeDef>.GetNamed("RimCBM_" + shortName + "Prepare");
        recipe.Worker.ApplyOnPawn(pawn, part, null, new List<Thing>(), null);
        return ImplantService.Modules(pawn).Last();
    }
    static void Run()
    {
        Directory.CreateDirectory(output);
        var baselinePolicy = DefDatabase<CBMLossPolicyDef>.AllDefsListForReading.First();
        baselinePolicy.removedRecoveryChance = 1;
        baselinePolicy.removedDamageChance = 0;
        baselinePolicy.removedBurnChance = 0;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        var pawn = Find.CurrentMap.mapPawns.FreeColonists.First();
        pawn.Name = new NameSingle("RimCBM");
        pawn.drafter.Drafted = true;
        var torso = pawn.RaceProps.body.corePart;
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm");
        Check(ImplantService.Profile(pawn, arm).width == 2 && ImplantService.Profile(pawn, arm).height == 3, "natural arm grid 2 by 3");
        pawn.health.AddHediff(HediffDef.Named("BionicArm"), arm);
        Check(ImplantService.Profile(pawn, arm).width == 3, "bionic arm grid 3 by 6");
        var armBoard = ImplantService.Build(pawn).Boards()[GridRules.RootBoard(ImplantService.RootId(pawn, arm))];
        Check(armBoard.At(2, 0).Tags.Contains("BodyPart.Arm") && armBoard.At(2, 0).Hot, "XML cell tags coexist with host identity");
        var bay = Prepare(pawn, torso, "Bay");
        var rules = ImplantService.Build(pawn);
        Check(bay.status == ModuleStatus.SurgeryPrepared && rules.Boards().Count == ImplantService.Parts(pawn).Count, "preparation creates pending but no expansion");
        string main = GridRules.RootBoard(ImplantService.RootId(pawn, torso));
        Check(ImplantService.Place(pawn, bay.GetUniqueLoadID(), main, 0, 0, false).Allowed, "deploy expansion after surgery");
        Check(ImplantService.Build(pawn).Boards()[GridRules.BayBoard(bay.GetUniqueLoadID())].At(0, 0).Tags.Contains("BodyPart.Torso"), "XML expansion preserves body tag");
        var child = Prepare(pawn, torso, "Interface");
        Check(ImplantService.Place(pawn, child.GetUniqueLoadID(), GridRules.BayBoard(bay.GetUniqueLoadID()), 0, 0, false).Allowed, "deploy into hot extension");
        Check(!ImplantService.Build(pawn).Detach(bay.GetUniqueLoadID(), true).Allowed, "runtime dependency blocks extraction");
        Check(ImplantService.ExtractHot(pawn, child).Allowed, "hot extraction returns actual item");
        Check(ImplantService.Modules(pawn).All(m => m.status == ModuleStatus.Installed), "hot extraction never adds pending");
        var item = pawn.inventory.innerContainer.First(t => t.def.defName == "RimCBM_InterfaceItem");
        Check(ImplantService.InsertHot(pawn, torso, item, HediffDef.Named("RimCBM_Interface"), GridRules.BayBoard(bay.GetUniqueLoadID()), 1, 0, false).Allowed, "actual carried item inserts directly");
        Check(!pawn.inventory.innerContainer.Any(t => t.def.defName == "RimCBM_InterfaceItem"), "hot insertion consumes physical item");
        var controller = Prepare(pawn, torso, "Controller");
        Check(ImplantService.Place(pawn, controller.GetUniqueLoadID(), main, 1, 2, false).Allowed, "fixed controller deploys");
        Check(!ImplantService.Place(pawn, controller.GetUniqueLoadID(), main, 0, 2, false).Allowed, "unresearched rearrangement blocked");
        Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed("RimCBM_LayoutReconfiguration"), false);
        Check(ImplantService.Place(pawn, controller.GetUniqueLoadID(), main, 0, 2, false).Allowed, "research unlocks same board move");
        Check(!ImplantService.ExtractHot(pawn, controller).Allowed, "research cannot detach fixed module");
        Prepare(pawn, torso, "Interface");
        var bill = new Bill_RemoveCBM(DefDatabase<RecipeDef>.GetNamed("RimCBM_Remove"), controller);
        pawn.BillStack.AddBill(bill);
        bill.Part = torso;
        Check(bill.CompletableEver, "instance-bound extraction bill valid");
        int count = ImplantService.Modules(pawn).Count();
        var controllerRecipe = DefDatabase<RecipeDef>.GetNamed("RimCBM_ControllerPrepare");
        Check(!controllerRecipe.Worker.GetPartsToApplyOn(pawn, controllerRecipe).Any(), "pawn quota filters all duplicate surgery targets");
        controllerRecipe.Worker.ApplyOnPawn(pawn, torso, null, new List<Thing>(), null);
        Check(ImplantService.Modules(pawn).Count() == count, "surgery worker enforces quota at execution");
        Check(ImplantService.Parts(pawn).Count(p => p.def.defName == "Hand") == 1
            && ImplantService.Parts(pawn).Count(p => p.def.defName == "Foot") == 2, "remaining natural hand and both feet expose independent grids");
        var visibleMethod = typeof(HealthCardUtility).GetMethod("VisibleHediffs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var hidden = ((IEnumerable<Hediff>)visibleMethod.Invoke(null, new object[] { pawn, true })).ToList();
        Check(!hidden.OfType<Hediff_CBMModule>().Any() && hidden.Any(h => h.def.defName == "BionicArm")
            && ImplantService.Modules(pawn).Count() == count, "closed panel filters CBM display without hiding vanilla bionics or removing implants");
        var moving = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.SurgeryPrepared);
        string destination = GridRules.BayBoard(bay.GetUniqueLoadID());
        Check(CBMRelocation.Queue(pawn, moving.GetUniqueLoadID(), destination, 2, 2, false).Allowed
            && moving.status == ModuleStatus.SurgeryPrepared && moving.intentBoard == destination, "drag intent does not instantly install a prepared CBM");
        Check(ImplantService.Build(pawn).Reservations.Count == 1, "runtime intent reserves destination");
        var secondMove = ImplantService.Modules(pawn).First(m => m.status == ModuleStatus.Installed && m.boardId == destination);
        Check(CBMRelocation.Queue(pawn, secondMove.GetUniqueLoadID(), destination, 0, 0, false).Allowed
            && secondMove.x == 1 && pawn.jobs.jobQueue.Any(q => q.job.def == CBMRelocation.JobDef && q.job.count == secondMove.loadID),
            "second relocation queues independently and leaves installed module at source");
        CBMRelocation.Cancel(secondMove);
        Check(!pawn.jobs.jobQueue.Any(q => q.job.def == CBMRelocation.JobDef && q.job.count == secondMove.loadID)
            && moving.intentBoard != null, "cancel removes queued job without affecting another relocation");
        CBMRelocation.Cancel(moving);
        Check(moving.intentBoard == null && ImplantService.Build(pawn).Reservations.Count == 0, "cancel releases destination and cancels work");
        Check(CBMRelocation.Queue(pawn, moving.GetUniqueLoadID(), destination, 2, 2, false).Allowed, "cancelled relocation can be scheduled again");
        Check(armBoard.At(0, 0).Tags.Contains(SlotTags.CoreBody)
            && ImplantService.Build(pawn).Boards()[main].At(0, 0).Tags.Contains(SlotTags.CoreBody)
            && !ImplantService.Build(pawn).Boards()[destination].At(0, 0).Tags.Contains(SlotTags.CoreBody), "XML CoreBody includes bionic and natural initial slots but excludes attached bay");
        var stateItem = ThingMaker.MakeThing(ThingDef.Named("RimCBM_PlaceholderItem"));
        var stateComp = stateItem.TryGetComp<CompCBMCondition>();
        stateComp.condition = CBMCondition.Dirty | CBMCondition.Unsterile;
        stateComp.durability = .42f;
        stateComp.active = false;
        pawn.inventory.innerContainer.TryAdd(stateItem);
        DynamicCapacity();
        EnergyTests(pawn);
        AbilityTests(pawn);
        Check(new Command_CBMControl(pawn).Desc.Contains(CBMEnergy.Display(pawn)), "control gizmo tooltip includes live Wd energy");
        ControlMenuTests(pawn);
        TargetAbilityTests(pawn);
        if (moving.intentBoard == null)
            Check(CBMRelocation.Queue(pawn, moving.GetUniqueLoadID(), destination, 2, 2, false).Allowed, "restore relocation fixture after action tests");
        Check(!CBMPanelState.IsOpen(pawn), "panel defaults closed");
        CBMPanelState.SetOpen(pawn, true);
        Select(pawn);
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }
    static void DynamicCapacity()
    {
        var pawn = Find.CurrentMap.mapPawns.FreeColonists.Last();
        pawn.Name = new NameSingle("Dynamic");
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm");
        pawn.health.AddHediff(HediffDef.Named("BionicArm"), arm);
        string board = GridRules.RootBoard(ImplantService.RootId(pawn, arm));
        var partial = Prepare(pawn, arm, "Placeholder");
        var gone = Prepare(pawn, arm, "Placeholder");
        var provider = Prepare(pawn, arm, "Bay");
        Check(ImplantService.Place(pawn, partial.GetUniqueLoadID(), board, 0, 2, false).Allowed
            && ImplantService.Place(pawn, gone.GetUniqueLoadID(), board, 1, 5, true).Allowed
            && ImplantService.Place(pawn, provider.GetUniqueLoadID(), board, 1, 2, false).Allowed, "runtime capacity setup");
        var child = Prepare(pawn, arm, "Interface");
        Check(ImplantService.Place(pawn, child.GetUniqueLoadID(), GridRules.BayBoard(provider.GetUniqueLoadID()), 0, 0, false).Allowed, "runtime child board setup");
        int drops = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).Sum(t => t.stackCount);
        int childDrops = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_InterfaceItem")).Sum(t => t.stackCount);
        pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs.First(h => h.Part == arm && h.def.defName == "BionicArm"));
        partial.durability = .6f;
        partial.active = false;
        CBMCapacityComponent.Reconcile(pawn);
        Check(ImplantService.Modules(pawn).Contains(partial) && partial.x == 0 && partial.y == 2 && !partial.rotated
            && ImplantService.Modules(pawn).Contains(provider), "partially supported modules and provider retain position");
        Check(!ImplantService.Modules(pawn).Contains(gone) && !ImplantService.Modules(pawn).Contains(child), "lost support and inactive provider child are removed");
        Check(pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).Sum(t => t.stackCount) == drops + 1
            && pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_InterfaceItem")).Sum(t => t.stackCount) == childDrops + 1, "capacity loss creates actual ground items");
        CBMCapacityComponent.Reconcile(pawn);
        Check(pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).Sum(t => t.stackCount) == drops + 1, "repeated capacity reconciliation never duplicates items");
        Check(ImplantService.Build(pawn).Validate().Allowed, "runtime suspended layout valid");
        var spec = provider.def.GetModExtension<CBMExtension>().Spec(provider.def.defName);
        Check(spec.Tags.Contains("Expansion") && CBMColors.Module(spec).b > CBMColors.Module(spec).r, "XML expansion tag and blue style resolved");
    }

    static void ConditionTests(Pawn pawn)
    {
        var torso = pawn.RaceProps.body.corePart;
        var item = ThingMaker.MakeThing(ThingDef.Named("RimCBM_PlaceholderItem"));
        item.stackCount = 2;
        var comp = item.TryGetComp<CompCBMCondition>();
        comp.durability = .4f;
        comp.active = false;
        var split = item.SplitOff(1);
        Check(split.TryGetComp<CompCBMCondition>().durability == .4f && !split.TryGetComp<CompCBMCondition>().active, "splitting a CBM stack preserves exact state");
        var clean = ThingMaker.MakeThing(item.def);
        Check(!comp.AllowStackWith(clean) && comp.AllowStackWith(split), "different CBM condition cannot merge into one stack");
        var module = (Hediff_CBMModule)HediffMaker.MakeHediff(HediffDef.Named("RimCBM_Placeholder"), pawn, torso);
        CBMConditionUtility.ApplyItem(split, module);
        var recovered = CBMConditionUtility.MakeItem(module, true);
        Check(recovered.TryGetComp<CompCBMCondition>().durability == .4f && !recovered.TryGetComp<CompCBMCondition>().active
            && recovered.TryGetComp<CompCBMCondition>().condition == (CBMCondition.Dirty | CBMCondition.Unsterile), "surgical recovery preserves wear and adds contamination");
        var recipe = DefDatabase<RecipeDef>.GetNamed("RimCBM_PlaceholderPrepare");
        int modulesBefore = ImplantService.Modules(pawn).Count();
        Check(!new Bill_Medical(recipe, null).IsFixedOrAllowedIngredient(recovered), "medical bill skips dirty CBM ingredients before starting surgery");
        recipe.Worker.ApplyOnPawn(pawn, torso, null, new List<Thing> { recovered }, null);
        recipe.Worker.ConsumeIngredient(recovered, recipe, pawn.Map);
        Check(ImplantService.Modules(pawn).Count() == modulesBefore && !recovered.Destroyed, "dirty surgery ingredient rejected without consuming CBM");
        item.Destroy();
        split.Destroy();
        clean.Destroy();
        recovered.Destroy();
        var policy = DefDatabase<CBMLossPolicyDef>.AllDefsListForReading.First();
        policy.removedDamageChance = 1;
        policy.removedDamage = .4f;
        policy.removedRecoveryChance = 1;
        policy.removedBurnChance = 0;
        policy.destroyedDamageChance = 1;
        policy.destroyedDamage = .8f;
        policy.destroyedRecoveryChance = 1;
        policy.destroyedBurnChance = 1;
        var profile = ImplantService.Profile(pawn, torso);
        string board = GridRules.RootBoard(ImplantService.RootId(pawn, torso));
        var partial = Prepare(pawn, torso, "Placeholder");
        Check(ImplantService.Place(pawn, partial.GetUniqueLoadID(), board, 0, 2, false).Allowed, "partial loss test placement");
        var before = ImplantService.Build(pawn).Boards();
        int height = profile.height;
        profile.height = 3;
        CBMCapacityComponent.Reconcile(pawn, SlotLossReason.Removed, before);
        Check(ImplantService.Modules(pawn).Contains(partial) && Math.Abs(partial.durability - .8f) < .001f, "removed partial support can damage retained module");
        CBMCapacityComponent.Reconcile(pawn);
        Check(Math.Abs(partial.durability - .8f) < .001f, "periodic reconciliation does not reroll or repeat partial damage");
        profile.height = height;
        before = ImplantService.Build(pawn).Boards();
        profile.height = 3;
        CBMCapacityComponent.Reconcile(pawn, SlotLossReason.Destroyed, before);
        Check(Math.Abs(partial.durability - .4f) < .001f, "destroyed support uses separate damage policy");
        profile.height = height;
        pawn.health.RemoveHediff(partial);
        var provider = Prepare(pawn, torso, "Bay");
        ImplantService.Place(pawn, provider.GetUniqueLoadID(), board, 0, 0, false);
        var child = Prepare(pawn, torso, "Placeholder");
        ImplantService.Place(pawn, child.GetUniqueLoadID(), GridRules.BayBoard(provider.GetUniqueLoadID()), 0, 0, false);
        var oldItems = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).ToList();
        CBMConditionUtility.Damage(provider, 1);
        var burntItem = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).FirstOrDefault(t => !oldItems.Contains(t));
        Check(!provider.Operational && ImplantService.Modules(pawn).Contains(provider) && ImplantService.Modules(pawn).Contains(child)
            && child.retainedRemnant && child.condition.HasFlag(CBMCondition.BurntOut) && burntItem == null,
            "burnt expansion leaves unsupported damaged child inside without spawning an item");
        CBMCapacityComponent.Reconcile(pawn);
        Check(ImplantService.Modules(pawn).Contains(child) && child.retainedRemnant, "unsupported remnant persists across reconciliation");
        pawn.health.RemoveHediff(child);
        pawn.health.RemoveHediff(provider);
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm" && ImplantService.Profile(pawn, p) != null);
        pawn.health.AddHediff(HediffDef.Named("BionicArm"), arm);
        var replacementTest = Prepare(pawn, arm, "Placeholder");
        ImplantService.Place(pawn, replacementTest.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, arm)), 0, 4, false);
        var replacementRecipe = DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.addsHediff == HediffDef.Named("BionicArm") && r.Worker is Recipe_InstallArtificialBodyPart);
        replacementRecipe.Worker.ApplyOnPawn(pawn, arm, null, new List<Thing>(), null);
        Check(ImplantService.Modules(pawn).Contains(replacementTest) && replacementTest.durability == 1,
            "prosthesis replacement does not settle transient natural-grid shrinkage");
        pawn.health.RemoveHediff(replacementTest);
        var doomed = Prepare(pawn, arm, "Placeholder");
        ImplantService.Place(pawn, doomed.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, arm)), 0, 0, false);
        policy.destroyedRecoveryChance = 0;
        oldItems = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).ToList();
        pawn.health.AddHediff(HediffDefOf.MissingBodyPart, arm, new DamageInfo(DamageDefOf.Cut, 999));
        Check(!ImplantService.Modules(pawn).Contains(doomed)
            && pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).All(t => oldItems.Contains(t)),
            "damage-driven limb loss uses destruction chance and can yield no recovered item");
        var leg = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Leg" && ImplantService.Profile(pawn, p) != null);
        var surgical = Prepare(pawn, leg, "Placeholder");
        ImplantService.Place(pawn, surgical.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, leg)), 0, 0, false);
        oldItems = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).ToList();
        new Recipe_RemoveBodyPart().DamagePart(pawn, leg);
        var surgicalItem = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("RimCBM_PlaceholderItem")).FirstOrDefault(t => !oldItems.Contains(t));
        Check(!ImplantService.Modules(pawn).Contains(surgical) && surgicalItem != null
            && Math.Abs(surgicalItem.TryGetComp<CompCBMCondition>().durability - .6f) < .001f
            && !surgicalItem.TryGetComp<CompCBMCondition>().condition.HasFlag(CBMCondition.BurntOut),
            "vanilla surgical amputation uses removal probabilities instead of destruction probabilities");
        var remote = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var inventory = remote.inventory;
        remote.inventory = null;
        var remoteLeg = remote.RaceProps.body.AllParts.First(p => p.def.defName == "Leg");
        var awaiting = Prepare(remote, remoteLeg, "Placeholder");
        ImplantService.Place(remote, awaiting.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(remote, remoteLeg)), 0, 0, false);
        policy.destroyedRecoveryChance = 1;
        policy.destroyedBurnChance = 1;
        remote.health.AddHediff(HediffDefOf.MissingBodyPart, remoteLeg, new DamageInfo(DamageDefOf.Cut, 999));
        Check(awaiting.lossPending && awaiting.lossRecover && awaiting.lossReason == SlotLossReason.Destroyed
            && awaiting.condition.HasFlag(CBMCondition.BurntOut), "failed off-map recovery retains explicit sampled outcome");
        policy.destroyedRecoveryChance = 0;
        policy.destroyedBurnChance = 0;
        CBMCapacityComponent.Reconcile(remote, SlotLossReason.Destroyed);
        Check(ImplantService.Modules(remote).Contains(awaiting) && awaiting.lossRecover && awaiting.condition.HasFlag(CBMCondition.BurntOut),
            "retry does not reroll recovery or burnout after probabilities change");
        remote.inventory = inventory;
        CBMCapacityComponent.Reconcile(remote);
        Check(!ImplantService.Modules(remote).Contains(awaiting)
            && remote.inventory.innerContainer.Any(t => t.TryGetComp<CompCBMCondition>()?.condition.HasFlag(CBMCondition.BurntOut) == true),
            "deferred recovery materializes the original sampled state exactly once");
        int itemCount = remote.inventory.innerContainer.Count;
        CBMCapacityComponent.Reconcile(remote);
        Check(remote.inventory.innerContainer.Count == itemCount, "completed deferred recovery cannot duplicate an item");
        remote.Destroy();

    }
    static void ReviewFixTests(Pawn observer)
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 3), observer.Map);
        var leg = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Leg");
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm");
        var rig = Prepare(pawn, leg, "AbilityRig");
        var battery = Prepare(pawn, arm, "Battery");
        Check(ImplantService.Place(pawn, rig.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, leg)), 0, 0, false).Allowed
            && ImplantService.Place(pawn, battery.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, arm)), 0, 0, false).Allowed, "review fixtures occupy valid grids");
        var ext = rig.def.GetModExtension<CBMExtension>();
        var original = ext.abilities;
        var toggle = new CBMAbilitySpec { id = "reviewLife", kind = CBMAbilityKind.Toggle, idleWatts = 10, workerClass = typeof(ProbeLifecycleWorker), dataClass = typeof(ProbeAbilityData) };
        var reaction = new CBMAbilitySpec
        {
            id = "reviewEvent",
            kind = CBMAbilityKind.Reactive,
            trigger = CBMTrigger.Custom,
            eventId = "Review",
            targetEventSource = true,
            targetKinds = new List<CBMTargetKind> { CBMTargetKind.FriendlyPawn },
            range = 20,
            useWd = 1,
            workerClass = typeof(ProbeEventWorker)
        };
        try
        {
            ext.abilities = new List<CBMAbilitySpec> { toggle, reaction };
            CBMEnergy.For(pawn).storedWd = 10;
            ProbeLifecycleWorker.starts = ProbeLifecycleWorker.stops = ProbeLifecycleWorker.ticks = 0;
            CBMEnergy.Component.Step(pawn);
            CBMEnergy.Component.Step(pawn);
            Check(ProbeLifecycleWorker.starts == 1 && ProbeLifecycleWorker.ticks == 2, "continuous worker starts once across repeated supplied steps");
            var data = (ProbeAbilityData)CBMAbilities.State(rig, toggle).data;
            Check(data.value == 2, "continuous worker has per-instance data");
            var portable = CBMConditionUtility.MakeItem(rig);
            var copy = portable.TryGetComp<CompCBMCondition>().abilityStates.First(x => x.id == toggle.id);
            Check(((ProbeAbilityData)copy.data).value == 2 && !ReferenceEquals(copy.data, data), "custom ability data deep-copies to physical implant");
            observer.inventory.innerContainer.TryAdd(portable);
            CBMEnergy.For(pawn).storedWd = 0;
            CBMEnergy.Component.Step(pawn);
            Check(ProbeLifecycleWorker.stops == 1 && ProbeLifecycleWorker.reason == CBMStopReason.PowerLost, "brownout invokes one lifecycle stop");
            CBMEnergy.Component.Step(pawn);
            Check(ProbeLifecycleWorker.stops == 1, "unpowered worker does not repeatedly stop");
            CBMEnergy.For(pawn).storedWd = 10;
            CBMEnergy.Component.Step(pawn);
            Check(ProbeLifecycleWorker.starts == 2, "restored power starts worker again");
            CBMAbilities.Toggle(rig, toggle);
            Check(ProbeLifecycleWorker.stops == 2 && ProbeLifecycleWorker.reason == CBMStopReason.Disabled, "toggle off stops worker immediately");
            ProbeEventWorker.context = null;
            CBMAbilities.Dispatch(pawn, new CBMAbilityEvent { id = "Review", source = observer, payload = "payload" });
            Check(ProbeEventWorker.context?.target == observer && (string)ProbeEventWorker.context.trigger.payload == "payload", "custom reaction receives event source and payload");
            float energy = CBMEnergy.For(pawn).storedWd;
            CBMAbilities.Dispatch(pawn, new CBMAbilityEvent { id = "Review", source = observer });
            Check(CBMEnergy.For(pawn).storedWd == energy, "custom reaction shares cooldown and energy rules");
            var custom = new CBMAbilityEffect { kind = CBMEffectKind.Custom, workerClass = typeof(ProbeCustomEffect) };
            Check(!custom.ConfigErrors().Any() && !custom.Worker.RequiresMap(custom), "custom world-safe effect needs no fake damage definition");
            var customAbility = new CBMAbilitySpec { id = "worldCustom", kind = CBMAbilityKind.OneShot, effects = new List<CBMAbilityEffect> { custom } };
            ext.abilities.Add(customAbility);
            pawn.DeSpawn();
            var caravan = RimWorld.Planet.CaravanMaker.MakeCaravan(new[] { pawn }, Faction.OfPlayer, observer.Map.Tile, false);
            Check(CBMCasting.Queue(rig, customAbility).Allowed && ProbeCustomEffect.applied, "custom effect can run in caravan through the public casting entry");
            caravan.RemovePawn(pawn);
            caravan.Destroy();
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 3), observer.Map);
            CBMCapacityComponent.Reconcile(pawn);
            PanelSnapshot.Get(pawn);
            int builds = PanelSnapshot.Builds;
            CBMCapacityComponent.Reconcile(pawn);
            CBMCapacityComponent.Reconcile(observer);
            PanelSnapshot.Get(pawn);
            Check(PanelSnapshot.Builds == builds, "unchanged own and other pawn capacity checks retain panel snapshot");
            var otherDef = HediffDef.Named("RimCBM_Interface");
            var otherExt = otherDef.GetModExtension<CBMExtension>();
            var oldItem = otherExt.item;
            try
            {
                otherExt.item = ext.item;
                Check(CBMItems.Implant(ext.item) == null && otherExt.ConfigErrors().Any(x => x.Contains("exactly one")), "ambiguous physical identity is reported instead of choosing first def");
                Check(!CBMAccess.Check(pawn, leg, rig.def, true).Allowed, "ambiguous identity cannot enter installation");
                var item = ThingMaker.MakeThing(ext.item);
                CBMSurgicalOutcome.FailedInstall(item, pawn, observer, leg, new CBMSurgeryProfile { retainedChance = 1, severeChance = 0, burnoutChance = 0, severePatientDamage = 0 }, otherDef);
                Check(ImplantService.Modules(pawn).Any(m => m.def == otherDef && m.retainedRemnant), "failed installation retains the explicit recipe identity");
            }
            finally { otherExt.item = oldItem; }
            var fuelModule = Prepare(pawn, pawn.RaceProps.body.corePart, "FuelGenerator");
            Check(ImplantService.Place(pawn, fuelModule.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, pawn.RaceProps.body.corePart)), 0, 0, false).Allowed, "custom fuel source fixture installs");
            var fuelSpec = CBMSupply.Spec(fuelModule);
            var oldOptions = fuelSpec.options;
            var source = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
            source.stackCount = 5;
            pawn.inventory.innerContainer.TryAdd(source);
            try
            {
                fuelSpec.options = new List<CBMFuelOption> { new CBMFuelOption { item = ThingDefOf.Chemfuel, energyWdPerUnit = 10, massPerUnit = .25f, workerClass = typeof(ProbeFuelSource) } };
                ProbeFuelSource.portions = 2;
                ProbeFuelSource.fail = false;
                Check(CBMSupply.Refill(fuelModule, source, 1) == 2 && source.stackCount == 5 && ProbeFuelSource.portions == 0, "custom container provider consumes contents instead of source stack");
                Check(Math.Abs(CBMSupply.FuelMass(fuelModule) - .5f) < .001f, "custom fuel mass excludes container shell");
                ProbeFuelSource.portions = 1;
                ProbeFuelSource.fail = true;
                Check(CBMSupply.Refill(fuelModule, source, 1) == 0 && fuelModule.fuelState.units == 2 && ProbeFuelSource.portions == 1, "failed source commit adds no fuel");
            }
            finally { fuelSpec.options = oldOptions; source.Destroy(); pawn.health.RemoveHediff(fuelModule); }
            var station = ThingMaker.MakeThing(ThingDef.Named("RimCBM_ChargingStation"));
            GenSpawn.Spawn(station, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 4), pawn.Map);
            var charger = station.TryGetComp<CompCBMCharger>();
            charger.props = new CompProperties_CBMCharger { workerClass = typeof(ProbeChargeSource) };
            ProbeChargeSource.energy = 1;
            charger.CompTick();
            Check(charger.Ready && Math.Abs(charger.buffer - .25f) < .001f && ProbeChargeSource.energy == .75f, "custom grid provider debits source before filling charger buffer");
            station.Destroy();
            CBMEnergy.For(pawn).storedWd = 10;
            CBMAbilities.Toggle(rig, toggle);
            CBMEnergy.Component.Step(pawn);
            pawn.health.RemoveHediff(rig);
            Check(ProbeLifecycleWorker.reason == CBMStopReason.Removed && !CBMAbilities.State(rig, toggle).runtimeActive, "removal cleans up running ability lifecycle");
            var oldDrive = ImplantService.Modules(observer).First(m => m.def.defName == "RimCBM_Overdrive");
            var itemDrive = CBMConditionUtility.MakeItem(oldDrive);
            var restored = (Hediff_CBMModule)HediffMaker.MakeHediff(oldDrive.def, pawn, leg);
            CBMConditionUtility.ApplyItem(itemDrive, restored);
            Check(restored.cooldownUntilTick == oldDrive.cooldownUntilTick && CBMAbilities.State(restored, CBMLegacyAbilities.Use(restored)).cooldownUntil == CBMAbilities.State(oldDrive, CBMLegacyAbilities.Use(oldDrive)).cooldownUntil,
                "legacy cooldown survives physical conversion and new action-state lookup");
            itemDrive.Destroy();
        }
        finally { ext.abilities = original; pawn.Destroy(); }
    }
    static void EffectRuntimeTests(Pawn pawn)
    {
        var m = ImplantService.Modules(pawn).First(x => x.def.defName == "RimCBM_TargetRig");
        var target = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var map = pawn.Map;
        Check(!CBMRuntime.Running(target), "dormant world pawn pauses implant simulation");
        var battery = Prepare(target, target.RaceProps.body.corePart, "Battery");
        var generator = Prepare(target, target.RaceProps.body.corePart, "FuelGenerator");
        battery.status = generator.status = ModuleStatus.Installed;
        generator.fuelState.item = ThingDefOf.Chemfuel;
        generator.fuelState.units = 10;
        CBMEnergy.For(target).storedWd = 5;
        CBMEnergy.Component.Step(target);
        Check(CBMEnergy.For(target).storedWd == 5 && generator.fuelState.units == 10, "dormant host neither generates nor consumes fuel");
        var caravan = RimWorld.Planet.CaravanMaker.MakeCaravan(new[] { target }, Faction.OfPlayer, map.Tile, false);
        Check(CBMRuntime.Running(target), "caravan pawn runs the unified implant simulation");
        Check(!CBMRuntime.Allows(target, new CBMAbilitySpec { mapOnly = true }) && CBMRuntime.Allows(target, new CBMAbilitySpec()), "world policy distinguishes map effects and self support");
        Check(!CBMRuntime.Allows(target, new CBMAbilitySpec { effects = new List<CBMAbilityEffect> { new CBMAbilityEffect { kind = CBMEffectKind.Projectile } } }), "projectile effects are unavailable outside maps");
        CBMEnergy.Component.Step(target);
        Check(CBMEnergy.For(target).storedWd > 5 && generator.fuelState.units < 10, "caravan generator converts actual stored fuel into shared energy");
        var faultState = new CBMFaultState { id = generator.def.GetModExtension<CBMExtension>().faults.First().id, nextTick = CBMAbilities.Now };
        generator.durability = .5f;
        generator.faultStates.Add(faultState);
        var fault = generator.def.GetModExtension<CBMExtension>().faults.First();
        float oldChance = fault.chance;
        fault.chance = 1;
        try
        {
            CBMReliabilityComponent.Step(target);
            Check(target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(), "residual discharge injures caravan host through native damage");
        }
        finally { fault.chance = oldChance; }
        target.health.RemoveHediff(generator);
        target.health.RemoveHediff(battery);
        caravan.RemovePawn(target);
        caravan.Destroy();
        GenSpawn.Spawn(target, CellFinder.RandomClosewalkCellNear(pawn.Position, map, 3), map);
        var wound = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, target, target.RaceProps.body.corePart);
        wound.Severity = 8;
        target.health.AddHediff(wound);
        var heal = new CBMAbilityEffect { kind = CBMEffectKind.Heal, amount = 2 };
        heal.Worker.Apply(m, heal, target);
        Check(wound.Severity < 8 && wound.Severity >= 6, "composable healing consumes a bounded severity budget");
        var tend = new CBMAbilityEffect { kind = CBMEffectKind.Tend, quality = .8f };
        Check(tend.Worker.CanApply(m, tend, target), "tending detects untreated injuries");
        tend.Worker.Apply(m, tend, target);
        Check(!wound.TendableNow(), "tending applies native treatment without deleting injury");
        var loss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, target);
        loss.Severity = .3f;
        target.health.AddHediff(loss);
        var reduce = new CBMAbilityEffect { kind = CBMEffectKind.ReduceHediff, hediff = HediffDefOf.BloodLoss, amount = .1f };
        reduce.Worker.Apply(m, reduce, target);
        Check(loss.Severity < .3f && loss.Severity >= .199f, "status reduction has its own bounded budget");
        var projectile = new CBMAbilityEffect { kind = CBMEffectKind.Projectile, projectile = ThingDef.Named("Bullet_Autopistol") };
        projectile.Worker.Apply(m, projectile, target);
        var shot = map.listerThings.ThingsOfDef(projectile.projectile).OfType<Projectile>().First();
        Check(shot.Spawned, "projectile effect launches a real native projectile");
        shot.Destroy();
        target.Destroy();
        var a = CBMAbilities.Specs(m).First(x => x.id == "treat");
        var state = CBMAbilities.State(m, a);
        state.cooldownUntil = state.effectUntil = 0;
        state.lastTriggerTick = -1;
        var ownWound = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, pawn.RaceProps.body.corePart);
        ownWound.Severity = 10;
        pawn.health.AddHediff(ownWound);
        CBMEnergy.For(pawn).storedWd = 20;
        Check(CBMCasting.Queue(m, a).Allowed && pawn.CurJob.dutyTag == a.id, "cast job records stable ability id and exact source");
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        Check(CBMEnergy.For(pawn).storedWd == 20 && ownWound.Severity == 10 && state.cooldownUntil == 0, "interrupted cast spends nothing and has no effects or cooldown");
        pawn.health.RemoveHediff(ownWound);
    }
    static void TargetAbilityTests(Pawn pawn)
    {
        var leg = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Leg");
        var rig = Prepare(pawn, leg, "TargetRig");
        Check(ImplantService.Place(pawn, rig.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, leg)), 0, 0, false).Allowed, "target ability suite deploys");
        var shock = CBMAbilities.Specs(rig).First(a => a.id == "shock");
        var treat = CBMAbilities.Specs(rig).First(a => a.id == "treat");
        Check(!CBMTargeting.Matches(pawn, shock, pawn) && CBMTargeting.Matches(pawn, treat, pawn), "target definitions distinguish self healing from hostile attacks");
        var target = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile);
        target.apparel.DestroyAll(); // Verify ability injury independently of randomly generated armor.
        GenSpawn.Spawn(target, GenAdj.AdjacentCells.Select(c => pawn.Position + c).First(c => c.Standable(pawn.Map) && GenSight.LineOfSight(pawn.Position, c, pawn.Map)), pawn.Map);
        var energy = CBMEnergy.For(pawn);
        energy.storedWd = 20;
        Check(CBMTargeting.Matches(pawn, shock, target) && !CBMTargeting.Matches(pawn, treat, target), "enemy target only matches hostile ability");
        float severity = target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
        Check(CBMAbilities.Activate(rig, shock, target: target).Allowed && energy.storedWd == 18
            && target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity) > severity, "targeted discharge applies actual damage to enemy and spends once");
        target.SetFaction(Faction.OfPlayer);
        Check(!CBMTargeting.Matches(pawn, shock, target) && CBMTargeting.Matches(pawn, treat, target), "target faction changes invalidate attack applicability");
        // The closewalk helper may choose a diagonal neighbor, within the healing radius.
        severity = target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
        Check(CBMAbilities.Activate(rig, treat, target: target).Allowed && energy.storedWd == 17
            && target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity) < severity, "targeted healing repairs another friendly pawn rather than caster");
        var state = CBMAbilities.State(rig, treat);
        state.cooldownUntil = 0;
        state.lastTriggerTick = -1;
        var wound = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, pawn.RaceProps.body.corePart);
        wound.Severity = 6;
        pawn.health.AddHediff(wound);
        Check(CBMAbilities.Activate(rig, treat, target: pawn).Allowed && wound.Severity == 2 && energy.storedWd == 16, "same healing ability accepts self target");
        pawn.health.RemoveHediff(wound);
        state.cooldownUntil = 0;
        state.lastTriggerTick = -1;
        float oldRange = treat.range;
        treat.range = 0;
        if (target.Position == pawn.Position)
            target.Position = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 2);
        Check(!CBMAbilities.Activate(rig, treat, target: target).Allowed && energy.storedWd == 16, "out-of-range target rejects without charge");
        treat.range = oldRange;
        target.DeSpawn();
        Check(!CBMAbilities.Activate(rig, treat, target: target).Allowed && energy.storedWd == 16, "despawned target cannot receive a stale activation");
        target.Destroy();
        energy.storedWd = 12.5f;
        Check(CBMControlMenu.Entries(pawn).Any(e => e.module == rig && e.operation == "Target"), "gizmo uses native targeting for external-only ability");
    }
    static void ControlMenuTests(Pawn pawn)
    {
        var rig = ImplantService.Modules(pawn).First(m => m.def.defName == "RimCBM_AbilityRig");
        Check(pawn.GetGizmos().OfType<Command_CBMControl>().Count() == 1, "native pawn gizmos include one combined CBM control");
        var entries = CBMControlMenu.Entries(pawn);
        Check(entries.Count(e => e.module == rig) == 3 && entries.All(e => e.abilityId != "passive"), "quick menu exposes three actionable ability kinds without passive clutter");
        Check(entries.Any(e => e.module.def.defName == "RimCBM_Metabolic" && e.operation == "Toggle")
            && entries.Any(e => e.module.def.defName == "RimCBM_Overdrive" && e.operation == "Use"), "quick menu also includes legacy generator and single-use controls");
        var toggle = entries.First(e => e.module == rig && e.abilityId == "assist");
        var ability = CBMAbilities.Specs(rig).First(a => a.id == "assist");
        Check(toggle.Invoke().Allowed && CBMAbilities.State(rig, ability).enabled, "quick menu toggles exact ability instance");
        Check(toggle.Invoke().Allowed && CBMAbilities.State(rig, ability).enabled, "stale toggle preserves requested state instead of flipping twice");
        CBMAbilities.Toggle(rig, ability);
        var burst = entries.First(e => e.module == rig && e.abilityId == "burst");
        Check(!burst.Available().Allowed, "unavailable quick-menu abilities are disabled");
        var spec = CBMAbilities.Specs(rig).First(a => a.id == "burst");
        var state = CBMAbilities.State(rig, spec);
        state.cooldownUntil = 0;
        state.lastTriggerTick = -1;
        state.effectUntil = 0;
        float before = CBMEnergy.For(pawn).storedWd;
        var previousMove = ImplantService.Modules(pawn).FirstOrDefault(m => m.intentBoard != null);
        string previousBoard = previousMove?.intentBoard;
        int previousX = previousMove?.intentX ?? 0, previousY = previousMove?.intentY ?? 0;
        bool previousRotation = previousMove?.intentRotated ?? false;
        Check(burst.Invoke().Allowed && CBMEnergy.For(pawn).storedWd == before && pawn.CurJob.source == rig, "quick-menu single use queues exact implant without early charge");
        Check(!burst.Invoke().Allowed, "stale menu cannot duplicate pending cast");
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        if (previousMove != null)
        {
            CBMRelocation.Cancel(previousMove);
            Check(CBMRelocation.Queue(pawn, previousMove.GetUniqueLoadID(), previousBoard, previousX, previousY, previousRotation).Allowed, "relocation can be reordered after a cast interrupts it");
        }
        pawn.health.RemoveHediff(rig);
        Check(!toggle.Invoke().Allowed, "menu cannot act on an extracted CBM reference");
        pawn.health.AddHediff(rig, rig.Part);
        state.effectUntil = 0;
        CBMEnergy.For(pawn).storedWd = 12.5f;
        CBMStatEffects.Invalidate(pawn);
    }
    static void AbilityTests(Pawn pawn)
    {
        var totalLoad = CBMBurden.Profiles.Single(p => p.group == CBMLoadGroup.Total);
        float savedFree = totalLoad.freeKg;
        totalLoad.freeKg = 1000;
        CBMStatEffects.Invalidate(pawn);
        var head = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Head");
        float baseline = pawn.GetStatValue(StatDefOf.MoveSpeed);
        var rig = Prepare(pawn, head, "AbilityRig");
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline) < .001f, "prepared abilities have no effects");
        Check(ImplantService.Place(pawn, rig.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, head)), 0, 0, false).Allowed, "ability suite deploys");
        var specs = CBMAbilities.Specs(rig);
        var passive = specs[0];
        var toggle = specs[1];
        var burst = specs[2];
        var reactive = specs[3];
        var toggleState = CBMAbilities.State(rig, toggle);
        var burstState = CBMAbilities.State(rig, burst);
        var reactionState = CBMAbilities.State(rig, reactive);
        var energy = CBMEnergy.For(pawn);
        rig.active = false;
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline - .05f) < .001f && !CBMAbilities.Toggle(rig, passive).Allowed,
            "passive applies without global switch and rejects manual toggle");
        energy.storedWd = 10;
        Check(CBMAbilities.Toggle(rig, toggle).Allowed, "toggle ability has independent switch");
        CBMEnergy.Component.Step(pawn);
        Check(Math.Abs(energy.storedWd - 9.76f) < .001f && Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline - .25f) < .001f,
            "toggle pays startup plus upkeep and adds actual stat offset");
        Check(CBMAbilities.Activate(rig, burst).Allowed && Math.Abs(energy.storedWd - 7.76f) < .001f
            && Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline - 1.05f) < .001f, "one-shot pays once and timed effect stacks with passive and toggle");
        Check(!CBMAbilities.Activate(rig, burst).Allowed && Math.Abs(energy.storedWd - 7.76f) < .001f, "one-shot duplicate is rejected without charge");
        burstState.effectUntil = CBMAbilities.Now;
        rig.TickInterval(1);
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline - .25f) < .001f && burstState.cooldownUntil > CBMAbilities.Now, "timed expiry invalidates stat cache without resetting cooldown");
        energy.storedWd = 0;
        CBMEnergy.Component.Step(pawn);
        Check(!CBMAbilities.Effective(rig, toggle) && CBMAbilities.Effective(rig, passive), "brownout stops powered ability but keeps free passive");
        var extra = new CBMAbilitySpec { id = "priority-check", kind = CBMAbilityKind.Toggle, idleWatts = 40, activationWd = .2f, priority = 3 };
        specs.Add(extra);
        energy.storedWd = .241f;
        CBMEnergy.Component.Step(pawn);
        Check(CBMAbilities.Effective(rig, extra) && !CBMAbilities.Effective(rig, toggle), "ability-level priority allocates scarce power within one module");
        specs.Remove(extra);
        rig.abilityStates.RemoveAll(a => a.id == extra.id);
        CBMAbilities.Toggle(rig, toggle);
        var injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, pawn.RaceProps.body.corePart);
        injury.Severity = 20;
        pawn.health.AddHediff(injury);
        energy.storedWd = 5.5f;
        pawn.health.PostApplyDamage(new DamageInfo(DamageDefOf.Cut, 2), 2);
        Check(injury.Severity == 20 && energy.storedWd == 5.5f, "reaction reserve blocks healing without spending");
        energy.storedWd = 10;
        CBMAbilities.Toggle(rig, reactive);
        pawn.health.PostApplyDamage(new DamageInfo(DamageDefOf.Cut, 2), 2);
        Check(injury.Severity == 20 && energy.storedWd == 10, "disarmed reaction does not consume or heal");
        CBMAbilities.Toggle(rig, reactive);
        reactive.damageDefs.Add(DamageDefOf.Flame);
        pawn.health.PostApplyDamage(new DamageInfo(DamageDefOf.Cut, 2), 2);
        Check(injury.Severity == 20 && energy.storedWd == 10, "reaction filters unmatched damage types");
        reactive.damageDefs.Clear();
        pawn.health.PostApplyDamage(new DamageInfo(DamageDefOf.Cut, 2), 2);
        Check(injury.Severity == 17 && energy.storedWd == 9, "native post-damage reaction heals a real injury and spends once");
        pawn.health.PostApplyDamage(new DamageInfo(DamageDefOf.Cut, 2), 2);
        Check(injury.Severity == 17 && energy.storedWd == 9, "repeated damage during cooldown cannot trigger again");
        reactionState.cooldownUntil = 0;
        reactionState.lastTriggerTick = -1;
        reactive.trigger = CBMTrigger.HealthBelow;
        reactionState.threshold = 1;
        CBMAbilities.React(pawn);
        Check(injury.Severity == 14 && energy.storedWd == 8, "low-health threshold reaction works");
        reactionState.cooldownUntil = 0;
        reactionState.lastTriggerTick = -1;
        reactive.trigger = CBMTrigger.BloodLossAbove;
        reactionState.threshold = .25f;
        var blood = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
        blood.Severity = .4f;
        pawn.health.AddHediff(blood);
        CBMAbilities.React(pawn);
        Check(injury.Severity == 11 && energy.storedWd == 7, "blood-loss threshold reaction works");
        burstState.cooldownUntil = 0;
        burstState.lastTriggerTick = -1;
        burstState.enabled = false;
        var burstStats = burst.statOffsets;
        burst.statOffsets = new List<StatModifier>();
        burst.durationTicks = 0;
        burst.healAmount = 2;
        Check(CBMAbilities.Activate(rig, burst).Allowed && injury.Severity == 9 && energy.storedWd == 5, "zero-duration one-shot executes immediate worker without a toggle requirement");
        burst.statOffsets = burstStats;
        burst.durationTicks = 180;
        burst.healAmount = 0;
        pawn.health.RemoveHediff(blood);
        pawn.health.RemoveHediff(injury);
        reactive.trigger = CBMTrigger.DamageTaken;
        reactionState.threshold = reactive.threshold;
        reactionState.cooldownUntil = 0;
        reactionState.lastTriggerTick = -1;
        Check(!CBMAbilities.Activate(rig, reactive, true).Allowed && energy.storedWd == 5, "no injury means no healing charge or cooldown");
        CBMEnergy.Emp(rig);
        Check(!CBMAbilities.Effective(rig, passive) && !CBMAbilities.Activate(rig, burst).Allowed, "EMP blocks tagged passive and triggered abilities");
        rig.empUntilTick = 0;
        reactionState.cooldownUntil = CBMAbilities.Now + 1200;
        reactionState.reserveWd = 10;
        reactionState.priority = 3;
        rig.modeId = "off";
        var item = CBMConditionUtility.MakeItem(rig);
        var copy = (Hediff_CBMModule)HediffMaker.MakeHediff(rig.def, pawn, head);
        CBMConditionUtility.ApplyItem(item, copy);
        Check(copy.abilityStates.First(a => a.id == "repair").cooldownUntil == reactionState.cooldownUntil
            && copy.abilityStates.All(a => a.effectUntil == 0 && a.poweredUntil == 0), "physical transfer preserves cooldown and settings but clears transient effects");
        pawn.inventory.innerContainer.TryAdd(item);
        energy.storedWd = 12.5f;
        CBMStatEffects.Invalidate(pawn);
        totalLoad.freeKg = savedFree;
        CBMStatEffects.Invalidate(pawn);
    }
    static void EnergyTests(Pawn pawn)
    {
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm" && ImplantService.Profile(pawn, p)?.width == 3);
        string board = GridRules.RootBoard(ImplantService.RootId(pawn, arm));
        var storage = Prepare(pawn, arm, "Battery");
        var generator = Prepare(pawn, arm, "Metabolic");
        var drive = Prepare(pawn, arm, "Overdrive");
        Check(ImplantService.Place(pawn, storage.GetUniqueLoadID(), board, 0, 0, false).Allowed
            && ImplantService.Place(pawn, generator.GetUniqueLoadID(), board, 1, 0, false).Allowed
            && ImplantService.Place(pawn, drive.GetUniqueLoadID(), board, 2, 0, false).Allowed, "energy sample modules install on a shared body grid");
        var energy = CBMEnergy.For(pawn);
        Check(energy.capacityWd == 100 && energy.storedWd == 0, "new storage increases Wd capacity without free charge");
        drive.active = false;
        pawn.needs.food.CurLevelPercentage = .8f;
        float food = pawn.needs.food.CurLevel;
        CBMEnergy.Component.Step(pawn);
        Check(Math.Abs(energy.storedWd - 60f * CompPower.WattsToWattDaysPerTick * 60) < .00001f && pawn.needs.food.CurLevel < food,
            "generator uses native W-to-Wd tick conversion and consumes nutrition");
        pawn.needs.food.CurLevelPercentage = .1f;
        float frozen = energy.storedWd;
        CBMEnergy.Component.Step(pawn);
        Check(energy.storedWd == frozen, "metabolic generation stops before starving pawn");
        pawn.needs.food.CurLevelPercentage = .8f;
        generator.active = false;
        float baseline = pawn.GetStatValue(StatDefOf.MoveSpeed);
        energy.storedWd = 10;
        drive.active = true;
        CBMEnergy.Component.Step(pawn);
        Check(Math.Abs(energy.storedWd - 9.4f) < .0001f && drive.Operational
            && Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed) - baseline - .15f) < .001f,
            "continuous power pays startup and one second and applies real movement stat");
        var tagEffect = DefDatabase<CBMTagEffectDef>.GetNamed("RimCBM_EMPSensitive");
        tagEffect.consumptionMultiplier = 2;
        tagEffect.effectMultiplier = .5f;
        energy.storedWd = 10;
        drive.energyStarted = false;
        CBMEnergy.Component.Step(pawn);
        Check(Math.Abs(energy.storedWd - 8.8f) < .0001f && Math.Abs(CBMStatEffects.Offset(pawn, StatDefOf.MoveSpeed) - .075f) < .0001f,
            "tag effects modify both energy costs and real stat output");
        tagEffect.consumptionMultiplier = tagEffect.effectMultiplier = 1;
        energy.storedWd = 9.4f;
        Check(CBMEnergy.Use(drive).Allowed && Math.Abs(energy.storedWd - 9.4f) < .0001f && pawn.CurJobDef?.defName == "RimCBM_CastAbility", "legacy use queues an action without spending");
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        Check(CBMAbilities.Activate(drive, CBMLegacyAbilities.Use(drive)).Allowed && Math.Abs(energy.storedWd - 4.4f) < .0001f
            && Math.Abs(CBMStatEffects.Offset(pawn, StatDefOf.MoveSpeed) - .95f) < .001f,
            "burst spends Wd and applies its timed effect");
        Check(!CBMEnergy.Use(drive).Allowed && Math.Abs(energy.storedWd - 4.4f) < .0001f, "cooldown prevents a second spend and effect");
        int boards = ImplantService.Build(pawn).Boards().Count;
        pawn.TakeDamage(new DamageInfo(DamageDefOf.EMP, 10));
        Check(drive.empUntilTick > Find.TickManager.TicksGame && !drive.Operational && drive.effectUntilTick == 0
            && generator.empUntilTick > Find.TickManager.TicksGame && storage.empUntilTick == 0
            && ImplantService.Build(pawn).Boards().Count == boards && CBMStatEffects.Offset(pawn, StatDefOf.MoveSpeed) == 0,
            "actual EMP disables tagged modules and clears effect without destroying physical slots or untagged storage");
        drive.empUntilTick = 0;
        energy.storedWd = 0;
        CBMEnergy.Component.Step(pawn);
        Check(!drive.Operational && energy.storedWd == 0, "brownout disables effect without overdrawing energy");
        var priority = Prepare(pawn, arm, "Overdrive");
        ImplantService.Place(pawn, priority.GetUniqueLoadID(), board, 0, 2, false);
        priority.powerPriority = 3;
        energy.storedWd = .6f;
        CBMEnergy.Component.Step(pawn);
        Check(priority.Operational && !drive.Operational, "higher priority consumer gets scarce power");
        pawn.health.RemoveHediff(priority);
        energy.storedWd = 50;
        CBMConditionUtility.Damage(storage, 1);
        Check(CBMEnergy.For(pawn).storedWd == 0 && energy.capacityWd == 0, "burnt storage clamps charge immediately");
        storage.condition = CBMCondition.None;
        storage.durability = 1;
        storage.active = true;
        drive.active = false;
        generator.active = false;
        drive.empUntilTick = Find.TickManager.TicksGame + 600;
        CBMEnergy.For(pawn).storedWd = 12.5f;
        var spec = drive.def.GetModExtension<CBMExtension>().Spec(drive.def.defName);
        var empColor = CBMColors.ModuleIndicator(spec, drive);
        drive.condition = CBMCondition.BurntOut;
        var burntColor = CBMColors.ModuleIndicator(spec, drive);
        drive.condition = CBMCondition.Dirty;
        drive.empUntilTick = 0;
        var dirtyColor = CBMColors.ModuleIndicator(spec, drive);
        Check(empColor != burntColor && burntColor != dirtyColor, "indicator selects distinct EMP burnout and contamination states");
        drive.condition = CBMCondition.None;
        drive.empUntilTick = Find.TickManager.TicksGame + 600;
    }
    static void CombatDamageTests(Pawn observer)
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 5), observer.Map);
        var arms = pawn.RaceProps.body.AllParts.Where(p => p.def.defName == "Arm").ToList();
        var def = HediffDef.Named("RimCBM_Placeholder");
        var ext = def.GetModExtension<CBMExtension>();
        var oldProfile = ext.combat;
        var oldEffects = ext.damageEffects;
        var settings = DefDatabase<CBMSettingsDef>.GetNamed("RimCBM_Settings");
        float oldReference = settings.referenceMassKg;
        var modules = new List<Hediff_CBMModule>();
        try
        {
            settings.referenceMassKg = 120;
            Check(CBMBurden.Scale(60, 1) == .5f, "reference body mass is configurable through Def without recompilation");
            settings.referenceMassKg = oldReference;
            ext.combat = new CBMCombatProfile { hitChance = 1, hitPoints = 20 };
            foreach (var part in new[] { arms[0], arms[0], arms[1] })
            {
                var m = (Hediff_CBMModule)HediffMaker.MakeHediff(def, pawn, part);
                m.status = ModuleStatus.SurgeryPrepared;
                pawn.health.AddHediff(m, part);
                modules.Add(m);
            }
            var hit = new DamageInfo(DamageDefOf.Blunt, 2, 100, -1, observer, arms[0]);
            hit.SetIgnoreArmor(true);
            hit.SetAllowDamagePropagation(false);
            var result = pawn.TakeDamage(hit);
            Check(modules.Count(m => m.durability < 1) == 1 && modules[2].durability == 1,
                "actual post-armor injury hits at most one implant and preserves opposite arm");
            var damaged = modules.First(m => m.durability < 1);
            Check(Math.Abs(1 - damaged.durability - result.totalDamageDealt / 20f) < .001f, "resolved injury damage converts to fractional implant durability");
            float before = modules.Sum(m => m.durability);
            CBMCombat.ApplyResolvedHit(new CBMHitContext { pawn = pawn, hitPart = arms[0], damage = hit, actualDamage = 0 });
            Check(modules.Sum(m => m.durability) == before, "zero actual injury cannot damage implants");
            ext.combat.hitChance = 0;
            CBMCombat.ApplyResolvedHit(new CBMHitContext { pawn = pawn, hitPart = arms[0], damage = hit, actualDamage = 2 });
            Check(modules.Sum(m => m.durability) == before, "zero configured hit chance prevents implant hits");
            ext.combat.damageFactors.Add(new CBMDamageMultiplier { damageDef = DamageDefOf.Blunt, multiplier = 2 });
            Check(Math.Abs(CBMCombat.Resolver.DurabilityDamage(new CBMHitContext { damage = hit, actualDamage = 2 }, damaged) -  .2f) < .001f,
                "per-damage-type multiplier modifies durability loss");
            ProbeDamageEffect.calls = 0;
            ext.damageEffects = new List<CBMDamageEffect> {
                new CBMDamageEffect { workerClass = typeof(ProbeDamageEffect), trigger = CBMDamageTrigger.Damaged },
                new CBMDamageEffect { workerClass = typeof(ProbeDamageEffect), trigger = CBMDamageTrigger.Burnout }
            };
            CBMConditionUtility.Damage(damaged, .1f, CBMDamageCause.Combat, hit);
            Check(ProbeDamageEffect.calls == 1 && damaged.damageEffectUntilTick > Find.TickManager.TicksGame, "damage effect executes with a persistent cooldown");
            CBMConditionUtility.Damage(damaged, .1f, CBMDamageCause.Combat, hit);
            Check(ProbeDamageEffect.calls == 1, "damage effect cooldown suppresses repeated effects");
            var physical = CBMConditionUtility.MakeItem(damaged);
            Check(physical.TryGetComp<CompCBMCondition>().damageEffectUntilTick == damaged.damageEffectUntilTick, "extraction preserves damage effect cooldown");
            physical.Destroy();
            CBMConditionUtility.Damage(damaged, 1, CBMDamageCause.Combat, hit);
            Check(ProbeDamageEffect.calls == 2 && damaged.condition.HasFlag(CBMCondition.BurntOut) && !damaged.active,
                "new burnout triggers once despite damage cooldown and disables module");
            CBMConditionUtility.Damage(damaged, 1, CBMDamageCause.Combat, hit);
            Check(ProbeDamageEffect.calls == 2, "burnt wreck cannot retrigger burnout effects");
            var fireModule = modules[2];
            ext.damageEffects = new List<CBMDamageEffect> { new CBMDamageEffect { chance = 1, causes = new List<CBMDamageCause> { CBMDamageCause.Combat } } };
            CBMConditionUtility.Damage(fireModule, .1f, CBMDamageCause.Surgery);
            Check(!pawn.IsBurning(), "damage cause filter excludes surgery from combat-only ignition");
            CBMConditionUtility.Damage(fireModule, .1f, CBMDamageCause.Combat, hit);
            Check(pawn.IsBurning(), "configured ignition consequence attaches a real vanilla fire");
        }
        finally
        {
            settings.referenceMassKg = oldReference;
            ext.combat = oldProfile;
            ext.damageEffects = oldEffects;
            var fire = pawn.GetAttachment(ThingDefOf.Fire);
            if (fire != null)
                fire.Destroy();
            pawn.Destroy();
        }
    }

    static void AccessTests(Pawn observer)
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 5), observer.Map);
        var part = pawn.RaceProps.body.corePart;
        var arm = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Arm");
        var def = HediffDef.Named("RimCBM_Placeholder");
        var ext = def.GetModExtension<CBMExtension>();
        var oldResearch = ext.installResearch;
        var oldCompatibility = ext.compatibility;
        var oldDeps = ext.dependencies;
        var oldModes = ext.modes;
        var oldAbilities = ext.abilities;
        var oldDefault = ext.defaultMode;
        try
        {
            var research = DefDatabase<ResearchProjectDef>.GetNamed("RimCBM_Integration");
            ext.installResearch = new List<ResearchProjectDef> { research };
            Check(!CBMSurgery.CanPrepare(pawn, part, def).Allowed && !CBMSurgery.QueueInstall(pawn, part, def).Allowed, "installation research gates both validation and surgery queue");
            Find.ResearchManager.FinishProject(research, false);
            Check(CBMSurgery.CanPrepare(pawn, part, def).Allowed, "finishing configured research unlocks installation");
            ext.compatibility = new List<CBMCompatibilityRule> { new CBMCompatibilityRule { requiredAllTags = new List<string> { "Race.Human", "Part.Natural" }, forbiddenTags = new List<string> { "Apparel.Apparel_Parka" } } };
            Check(CBMSurgery.CanPrepare(pawn, part, def).Allowed && !CBMAccess.HostTags(pawn, part).Contains("HotSwap"), "host rules recognize natural human anatomy without inheriting socket capabilities");
            ext.compatibility[0].maxBodySize = .01f;
            Check(!CBMSurgery.CanPrepare(pawn, part, def).Allowed, "body-size compatibility blocks preparation");
            ext.compatibility[0].maxBodySize = float.MaxValue;
            var storage = Prepare(pawn, arm, "Battery");
            Check(ImplantService.Place(pawn, storage.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, arm)), 0, 0, false).Allowed, "dependency fixture deploys storage on another body part");
            ext.dependencies = new List<CBMExclusionEntry> { new CBMExclusionEntry { scope = LimitScope.Part, modules = new List<HediffDef> { storage.def } } };
            Check(!CBMSurgery.CanPrepare(pawn, part, def).Allowed, "part dependency rejects storage on a different body part");
            ext.dependencies[0].scope = LimitScope.Pawn;
            Check(CBMSurgery.CanPrepare(pawn, part, def).Allowed, "global dependency accepts deployed storage");
            Check(CBMSurgery.QueueInstall(pawn, part, def).Allowed && !CBMSurgery.QueueRemoval(pawn, storage).Allowed, "queued installation reserves prerequisite against extraction");
            pawn.BillStack.Clear();
            var module = Prepare(pawn, part, "Placeholder");
            Check(ImplantService.Place(pawn, module.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, part)), 0, 0, false).Allowed, "dependent module deploys through shared admission rules");
            Check(!CBMSurgery.QueueRemoval(pawn, storage).Allowed && CBMAccess.Runtime(module), "sole prerequisite cannot be scheduled for extraction");
            var coat = (Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_Parka"), ThingDefOf.Cloth);
            pawn.apparel.Wear(coat);
            Check(!CBMAccess.Runtime(module) && !module.Operational && ImplantService.Modules(pawn).Contains(module), "incompatible apparel suspends operation without removing physical implant");
            ext.compatibility[0].installationOnly = true;
            Check(CBMAccess.Runtime(module), "installation-only compatibility does not disable existing implants");
            ext.compatibility[0].installationOnly = false;
            pawn.apparel.Remove(coat);
            coat.Destroy();
            Check(module.Operational, "removing incompatible apparel restores operation");
            CBMConditionUtility.Damage(storage, 1);
            Check(!CBMAccess.Runtime(module) && ImplantService.Modules(pawn).Contains(module), "burnt prerequisite disables dependent effects without deleting dependent");
            storage.condition = CBMCondition.None;
            storage.durability = 1;
            ext.dependencies = new List<CBMExclusionEntry>();
            ext.abilities = new List<CBMAbilitySpec> { new CBMAbilitySpec { id = "passive", kind = CBMAbilityKind.Passive }, new CBMAbilitySpec { id = "pulse", kind = CBMAbilityKind.OneShot } };
            var locked = DefDatabase<ResearchProjectDef>.GetNamed("RimCBM_AdvancedControl");
            ext.modes = new List<CBMModeSpec> {
                new CBMModeSpec { id = "low", abilities = new List<string> { "passive" }, exclusiveGroup = "probe", scope = LimitScope.Pawn, consumptionMultiplier = .5f },
                new CBMModeSpec { id = "high", abilities = new List<string> { "pulse" }, research = new List<ResearchProjectDef> { locked }, exclusiveGroup = "probe", scope = LimitScope.Pawn, effectMultiplier = 2 } };
            ext.defaultMode = CBMModes.Off;
            Check(!CBMAbilities.Available(module) && !CBMModes.Select(module, "high").Allowed, "off mode suppresses abilities and research gates a locked mode");
            Check(CBMModes.Select(module, "low").Allowed && CBMModes.Allows(module, ext.abilities[0]) && !CBMModes.Allows(module, ext.abilities[1]) && CBMModes.Consumption(module) == .5f,
                "mode selects ability membership and consumption multiplier");
            var other = Prepare(pawn, part, "Placeholder");
            Check(ImplantService.Place(pawn, other.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, part)), 1, 0, false).Allowed, "second mode fixture deploys");
            Check(CBMModes.Select(other, "low").Allowed && CBMModes.Id(module) == CBMModes.Off, "global running group shuts down conflicting peer without installation exclusion");
            Find.ResearchManager.FinishProject(locked, false);
            CBMAbilities.State(other, ext.abilities[1]).cooldownUntil = Find.TickManager.TicksGame + 100;
            Check(CBMModes.Select(other, "high").Allowed && CBMModes.Effect(other) == 2 && CBMAbilities.State(other, ext.abilities[1]).cooldownUntil > Find.TickManager.TicksGame,
                "researched mode applies multiplier without resetting ability cooldown");
            var item = CBMConditionUtility.MakeItem(other);
            var copy = (Hediff_CBMModule)HediffMaker.MakeHediff(def, pawn, part);
            CBMConditionUtility.ApplyItem(item, copy);
            Check(copy.modeId == "high", "mode choice transfers through extracted physical item");
            item.Destroy();
            foreach (var mode in ext.modes)
                mode.scope = LimitScope.Part;
            var local = Prepare(pawn, arm, "Placeholder");
            Check(ImplantService.Place(pawn, local.GetUniqueLoadID(), GridRules.RootBoard(ImplantService.RootId(pawn, arm)), 1, 0, false).Allowed,
                "part-scoped mode fixture deploys on another region");
            CBMModes.Select(other, "low");
            CBMModes.Select(local, "low");
            Check(CBMModes.Running(other) && CBMModes.Running(local), "part-scoped mode group permits independent operation in different regions");
            ext.modes[0].scope = LimitScope.Pawn;
            Check(new[] { other, local }.Count(CBMModes.Running) == 1, "conflicting restored mode choices cannot both operate before a tick");
            CBMModes.Reconcile(pawn);
            Check(new[] { other, local }.Count(m => CBMModes.Id(m) != CBMModes.Off) == 1, "mode reconciliation resolves restored group conflict deterministically");
        }
        finally { ext.installResearch = oldResearch; ext.compatibility = oldCompatibility; ext.dependencies = oldDeps; ext.modes = oldModes; ext.abilities = oldAbilities; ext.defaultMode = oldDefault; pawn.Destroy(); }
    }

    static void SupplyTests(Pawn observer)
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        pawn.Name = new NameSingle("Supply");
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 7), observer.Map);
        pawn.needs.food.CurLevelPercentage = 1;
        pawn.needs.rest.CurLevelPercentage = 1;
        var part = pawn.RaceProps.body.corePart;
        string board = GridRules.RootBoard(ImplantService.RootId(pawn, part));
        var storage = Prepare(pawn, part, "Battery");
        var generator = Prepare(pawn, part, "FuelGenerator");
        Check(ImplantService.Place(pawn, storage.GetUniqueLoadID(), board, 0, 0, false).Allowed && ImplantService.Place(pawn, generator.GetUniqueLoadID(), board, 1, 0, false).Allowed, "fuel generator and charging interface deploy");
        var energy = CBMEnergy.For(pawn);
        energy.storedWd = 0;
        CBMEnergy.Component.Step(pawn);
        Check(energy.storedWd == 0, "empty fuel generator cannot create power");
        var fuel = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
        fuel.stackCount = 50;
        var cell = GenAdj.AdjacentCells.Select(c => pawn.Position + c).First(c => c.Standable(pawn.Map));
        GenSpawn.Spawn(fuel, cell, pawn.Map);
        float mass = StatPart_CBMInstalledMass.Mass(pawn);
        Check(CBMSupply.Refill(generator, fuel, .5f) == 10 && fuel.stackCount == 40 && generator.fuelState.units == 10, "refill consumes whole items up to target without overfill");
        Check(Math.Abs(StatPart_CBMInstalledMass.Mass(pawn) - mass - 10 * ThingDefOf.Chemfuel.GetStatValueAbstract(StatDefOf.Mass)) < .001f, "tank fuel contributes to physical pawn mass and burden");
        var spec = CBMSupply.Spec(generator);
        float before = generator.fuelState.units;
        CBMEnergy.Component.Step(pawn);
        Check(energy.storedWd > 0 && Math.Abs((before - generator.fuelState.units) * 10 * .8f - energy.storedWd) < .00001f, "fuel conversion obeys efficiency and conserves stored energy");
        energy.storedWd = energy.capacityWd;
        before = generator.fuelState.units;
        CBMEnergy.Component.Step(pawn);
        Check(generator.fuelState.units == before, "full shared storage does not waste generator fuel");
        generator.active = false;
        energy.storedWd = 0;
        CBMEnergy.Component.Step(pawn);
        Check(generator.fuelState.units == before && energy.storedWd == 0, "switched-off generator neither burns fuel nor produces power");
        var wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        GenSpawn.Spawn(wood, cell, pawn.Map);
        Check(!CBMSupply.CanRefill(pawn, generator, wood).Allowed, "incompatible fuel is rejected");
        spec.options.Add(new CBMFuelOption { item = ThingDefOf.WoodLog });
        Check(!CBMSupply.CanRefill(pawn, generator, wood).Allowed, "compatible alternate fuel cannot mix with occupied tank");
        spec.options.RemoveAt(spec.options.Count - 1);
        wood.Destroy();
        generator.fuelState.units = 2.5f;
        generator.fuelState.preferred = ThingDefOf.Chemfuel;
        var physical = CBMConditionUtility.MakeItem(generator);
        var comp = physical.TryGetComp<CompCBMCondition>();
        Check(comp.fuelState.units == 2.5f && comp.fuelState != generator.fuelState && physical.GetStatValue(StatDefOf.Mass, true, 0) > physical.def.GetStatValueAbstract(StatDefOf.Mass), "extraction copies fractional fuel and includes physical item fuel mass");
        pawn.inventory.innerContainer.TryAdd(physical);
        generator.fuelState.units = 0;
        generator.fuelState.item = null;
        var carriedFuel = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
        carriedFuel.stackCount = 3;
        pawn.inventory.innerContainer.TryAdd(carriedFuel);
        Check(CBMSupply.CanRefill(pawn, generator, carriedFuel).Allowed && CBMSupply.Refill(generator, carriedFuel, 1) == 3 && carriedFuel.Destroyed,
            "owned inventory fuel refills directly without remote-map access");
        generator.fuelState.units = 0;
        generator.fuelState.item = null;
        Check(CBMSupply.AutomaticJob(pawn) == null, "automatic supplies default off");
        generator.fuelState.automatic = true;
        Check(CBMSupply.AutomaticJob(pawn)?.def.defName == "RimCBM_Refill", "low tank generates an automatic refill job");
        generator.fuelState.units = 12;
        generator.fuelState.item = ThingDefOf.Chemfuel;
        Check(CBMSupply.AutomaticJob(pawn)?.def.defName == "RimCBM_Refill", "automatic refill remembers target above its starting threshold");
        generator.fuelState.units = 20;
        Check(CBMSupply.AutomaticJob(pawn) == null && !generator.fuelState.requested, "reaching refill target clears replenishment request");
        generator.fuelState.units = 0;
        generator.fuelState.item = null;
        generator.fuelState.automatic = false;
        var station = ThingMaker.MakeThing(ThingDef.Named("RimCBM_ChargingStation"));
        station.SetFaction(Faction.OfPlayer);
        var stationCell = GenAdj.AdjacentCells.Select(c => pawn.Position + c).First(c => c.Standable(pawn.Map) && c != cell);
        GenSpawn.Spawn(station, stationCell, pawn.Map);
        var charger = station.TryGetComp<CompCBMCharger>();
        var power = station.TryGetComp<CompPowerTrader>();
        power.PowerOn = true;
        power.PowerOutput = -505;
        charger.buffer = 0;
        charger.CompTick();
        Check(Math.Abs(charger.buffer - 500 * CompPower.WattsToWattDaysPerTick * .9f) < .000001f, "station converts declared grid draw at configured efficiency");
        charger.buffer = 2;
        energy.storedWd = 0;
        float delivered = charger.Transfer(pawn, 1);
        Check(delivered > 0 && Math.Abs(delivered - 200 * CompPower.WattsToWattDaysPerTick) < .000001f && Math.Abs(charger.buffer + energy.storedWd - 2) < .000001f,
            "charge transfer respects implant acceptance rate and conserves buffer energy");
        energy.autoCharge = true;
        energy.storedWd = 20;
        Check(CBMSupply.AutomaticJob(pawn)?.def.defName == "RimCBM_ChargeAtStation", "low shared energy requests automatic station charging");
        energy.storedWd = 50;
        Check(CBMSupply.AutomaticJob(pawn)?.def.defName == "RimCBM_ChargeAtStation", "automatic charge resumes toward target above trigger threshold");
        energy.storedWd = 90;
        Check(CBMSupply.AutomaticJob(pawn) == null && !energy.chargeRequested, "automatic charging stops at target");
        energy.autoCharge = false;
        energy.storedWd = 0;
        energy.chargeRequested = true;
        Check(CBMSupply.AutomaticJob(pawn) == null && !energy.chargeRequested, "disabled charging policy clears stale demand");
        storage.empUntilTick = Find.TickManager.TicksGame + 600;
        Check(charger.Transfer(pawn, 1) == 0, "EMP-disabled interfaces cannot accept a charge");
        storage.empUntilTick = 0;
        power.PowerOn = false;
        Check(charger.Transfer(pawn, 1) == 0, "unpowered station cannot deliver its buffer");
        var conduit = ThingMaker.MakeThing(ThingDef.Named("PowerConduit"));
        conduit.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(conduit, stationCell, pawn.Map);
        var battery = ThingMaker.MakeThing(ThingDef.Named("Battery"));
        battery.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(battery, CellFinder.RandomClosewalkCellNear(stationCell, pawn.Map, 3), pawn.Map);
        battery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
        var wireCell = stationCell;
        while (wireCell != battery.Position)
        {
            wireCell += wireCell.x != battery.Position.x ? new IntVec3(Math.Sign(battery.Position.x - wireCell.x), 0, 0) : new IntVec3(0, 0, Math.Sign(battery.Position.z - wireCell.z));
            if (!battery.OccupiedRect().Contains(wireCell) && !wireCell.GetThingList(pawn.Map).Any(t => t.def.defName == "PowerConduit"))
            {
                var wire = ThingMaker.MakeThing(ThingDef.Named("PowerConduit"));
                wire.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(wire, wireCell, pawn.Map);
            }
        }
        foreach (var roofCell in battery.OccupiedRect())
            pawn.Map.roofGrid.SetRoof(roofCell, RoofDefOf.RoofConstructed);
        pawn.Map.powerNetManager.UpdatePowerNetsAndConnections_First();
        Check(power.PowerNet == battery.TryGetComp<CompPowerBattery>().PowerNet && power.PowerNet.CurrentStoredEnergy() > 100,
            "fixture battery and charger share the same powered network");
        Check(!CBMSupply.CanCharge(pawn, battery).Allowed, "ordinary battery still cannot act as a charging interface");
        energy.storedWd = 0;
        pawn.drafter.Drafted = true;
        CBMSupply.Order(pawn, CBMSupply.RefillJob(pawn, generator, fuel, false));
        var other = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(other, CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3), pawn.Map);
        Check(!other.CanReserve(fuel), "running refill reserves source against a second pawn");
        other.Destroy();
    }

    static void ReliabilityTests(Pawn observer)
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(observer.Position, observer.Map, 5), observer.Map);
        var part = pawn.RaceProps.body.corePart;
        var module = Prepare(pawn, part, "Placeholder");
        var ext = module.def.GetModExtension<CBMExtension>();
        var oldFaults = ext.faults;
        try
        {
            ext.faults = new List<CBMFaultSpec>();
            pawn.TryAttachFire(1, observer);
            float before = module.durability;
            CBMReliabilityComponent.Step(pawn);
            Check(module.durability < before, "attached fire continuously wears implants without a direct hit");
            pawn.GetAttachment(ThingDefOf.Fire).Destroy();
            before = module.durability;
            CBMReliabilityComponent.Step(pawn);
            Check(module.durability == before, "extinguishing stops continuous thermal wear");
            var fault = new CBMFaultSpec { id = "probe", chance = 1, intervalTicks = new IntRange(60, 60), workerClass = typeof(ProbeFaultWorker) };
            ext.faults.Add(fault);
            module.durability = .5f;
            module.active = false;
            ProbeFaultWorker.calls = 0;
            CBMReliabilityComponent.Step(pawn);
            Check(ProbeFaultWorker.calls == 0 && module.faultStates.Single().nextTick > Find.TickManager.TicksGame, "damaged disabled implant schedules its own fault timer");
            module.faultStates[0].nextTick = Find.TickManager.TicksGame;
            CBMReliabilityComponent.Step(pawn);
            CBMReliabilityComponent.Step(pawn);
            Check(ProbeFaultWorker.calls == 1, "fault executes once when due without duplicate attempts on the same tick");
            var item = CBMConditionUtility.MakeItem(module);
            var copy = (Hediff_CBMModule)HediffMaker.MakeHediff(module.def, pawn, part);
            CBMConditionUtility.ApplyItem(item, copy);
            Check(copy.faultStates.Single().nextTick == module.faultStates.Single().nextTick && copy.faultStates[0] != module.faultStates[0], "physical extraction and implantation preserve independent fault timer state");
            item.Destroy();
            module.durability = 1;
            CBMReliabilityComponent.Step(pawn);
            Check(module.faultStates[0].nextTick == 0, "healthy implant clears its fault schedule");
            module.durability = .5f;
            CBMEnergy.For(pawn).storedWd = 1;
            float injury = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
            new CBMFaultWorker_Discharge().Apply(module, new CBMFaultSpec { energyWd = 0, requiresEnergy = false, damageAmount = 1 });
            Check(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity) > injury && module.durability == .5f,
                "internal discharge injures pawn without recursively damaging implants");
            CBMConditionUtility.Damage(module, 1);
            Check(ImplantService.Modules(pawn).Contains(module) && module.durability == 0, "burnout keeps physical implant inside pawn");
            module.faultStates[0].nextTick = Find.TickManager.TicksGame;
            CBMReliabilityComponent.Step(pawn);
            Check(ProbeFaultWorker.calls == 2, "burnt inactive implant can retain configured periodic faults");
            pawn.health.RemoveHediff(module);
            CBMReliabilityComponent.Step(pawn);
            Check(ProbeFaultWorker.calls == 2, "removed implant no longer faults on former host");
            var physical = ThingMaker.MakeThing(ext.item);
            CBMSurgicalOutcome.FailedInstall(physical, pawn, observer, part, new CBMSurgeryProfile
            {
                retainedChance = 1,
                severeChance = 0,
                burnoutChance = 0,
                severeDamage = new FloatRange(.4f, .4f),
                severePatientDamage = 0
            });
            var remnant = ImplantService.Modules(pawn).Single();
            Check(physical.Destroyed && remnant.retainedRemnant && remnant.status == ModuleStatus.Installed && Math.Abs(remnant.durability - .6f) < .001f,
                "retained surgical failure consumes exactly one item and leaves damaged installed remnant");
            CBMCapacityComponent.Reconcile(pawn);
            CBMCapacityComponent.Reconcile(pawn);
            Check(ImplantService.Modules(pawn).Contains(remnant) && !remnant.HardwareReady && remnant.State().AwaitingRecovery,
                "failed installation remnant remains inert and cannot enter placement workflow");
            Check(CBMSurgery.QueueRemoval(pawn, remnant).Allowed, "retained failure can schedule exact extraction without a grid location");
            var bill = pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().Single();
            bill.recipe.Worker.ApplyOnPawn(pawn, part, null, new List<Thing>(), bill);
            Check(!ImplantService.Modules(pawn).Any(), "successful surgery removes retained failure");
            string board = GridRules.RootBoard(ImplantService.RootId(pawn, part));
            var battery = Prepare(pawn, part, "Battery");
            Check(ImplantService.Place(pawn, battery.GetUniqueLoadID(), board, 2, 0, false).Allowed, "fault energy test installs real storage");
            var energy = CBMEnergy.For(pawn);
            energy.storedWd = 2;
            new CBMFaultWorker_Discharge().Apply(battery, new CBMFaultSpec { energyWd = .5f, damageAmount = 0 });
            Check(Math.Abs(energy.storedWd - 1.5f) < .001f, "discharge draws configured energy from shared storage");
            energy.storedWd = 0;
            injury = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
            new CBMFaultWorker_Discharge().Apply(battery, new CBMFaultSpec { energyWd = .5f, damageAmount = 2 });
            Check(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity) == injury, "energy-dependent discharge cannot injure from an empty pool");
            var bay = Prepare(pawn, part, "Bay");
            Check(ImplantService.Place(pawn, bay.GetUniqueLoadID(), board, 0, 0, false).Allowed, "damaged hot-removal fixture installs a live socket");
            var hot = Prepare(pawn, part, "Interface");
            Check(ImplantService.Place(pawn, hot.GetUniqueLoadID(), GridRules.BayBoard(bay.GetUniqueLoadID()), 0, 0, false).Allowed, "damaged hot-removal fixture connects module");
            CBMConditionUtility.Damage(hot, 1);
            Check(ImplantService.ExtractHot(pawn, hot).Allowed && !ImplantService.Modules(pawn).Contains(hot)
                && pawn.inventory.innerContainer.Any(t => t.TryGetComp<CompCBMCondition>()?.condition.HasFlag(CBMCondition.BurntOut) == true),
                "burnt implant on compatible live socket remains hot removable with state intact");
        }
        finally { ext.faults = oldFaults; var fire = pawn.GetAttachment(ThingDefOf.Fire); if (fire != null) fire.Destroy(); pawn.Destroy(); }
    }

    static void BurdenTests()
    {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var hands = pawn.RaceProps.body.AllParts.Where(p => p.def.defName == "Hand").ToList();
        var legs = pawn.RaceProps.body.AllParts.Where(p => p.def.defName == "Leg").ToList();
        Check(CBMBurden.Profiles.Count() == 7 && DefDatabase<StatDef>.AllDefsListForReading.OfType<CBMLoadStatDef>().Count() == 7, "seven regional load profiles and native pawn stats are registered");
        float baseline = pawn.GetStatValue(StatDefOf.MoveSpeed, true, 0);
        float baseWork = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal, true, 0);
        float bodyScale = CBMBurden.Scale(pawn);
        Check(Math.Abs(bodyScale - CBMBurden.BaseMass(pawn) * pawn.BodySize / 60f) < .001f, "burden scale uses raw base Mass and actual BodySize exactly once");
        var legProfile = CBMBurden.Profiles.Single(p => p.group == CBMLoadGroup.Legs);
        Check(CBMBurden.Scale(60, 1) == 1 && CBMBurden.Scale(120, 1) == 2 && CBMBurden.Scale(60, .5f) == .5f && CBMBurden.Scale(120, .5f) == 1,
            "race base mass and life-stage body size both adjust burden scale");
        Check(CBMBurden.Penalty(legProfile, 3, .5f) > CBMBurden.Penalty(legProfile, 3, 1) && CBMBurden.Penalty(legProfile, 3, 2) == 0,
            "same implant weight penalizes smaller bodies more and larger bodies less");
        Check(Math.Abs(CBMBurden.Penalty(legProfile, 6, 2) - CBMBurden.Penalty(legProfile, 3, 1)) < .001f,
            "proportional load has the same penalty after scaling the full curve");
        Check(CBMBurden.Penalty(legProfile, legProfile.freeKg * .5f, .5f) == 0 && CBMBurden.Scale(0, 0) > 0,
            "scaled allowance boundary is exact and zero-size input cannot divide by zero");
        var cargoItem = ThingMaker.MakeThing(ThingDefOf.Steel);
        cargoItem.stackCount = 30;
        pawn.inventory.innerContainer.TryAdd(cargoItem);
        Check(CBMBurden.Scale(pawn) == bodyScale, "inventory mass cannot inflate implant allowance");
        pawn.inventory.innerContainer.Remove(cargoItem);
        cargoItem.Destroy();
        Check(CBMBurden.Loads(pawn).All(v => v == 0) && CBMBurden.Factor(pawn, StatDefOf.MoveSpeed) == 1, "unimplanted pawn receives no encumbrance penalty");
        var added = new List<Hediff_CBMModule>();
        Action<BodyPartRecord> add = part =>
        {
            var module = (Hediff_CBMModule)HediffMaker.MakeHediff(HediffDef.Named("RimCBM_Battery"), pawn, part);
            module.status = ModuleStatus.SurgeryPrepared;
            pawn.health.AddHediff(module, part);
            added.Add(module);
        };
        add(hands[0]);
        add(hands[1]);
        var loads = CBMBurden.Loads(pawn);
        Check(CBMBurden.Scale(pawn) == bodyScale, "implanted mass cannot inflate its own allowance");
        Check(Math.Abs(loads[(int)CBMLoadGroup.Hands] - 3) < .001f && loads[(int)CBMLoadGroup.Arms] == 0 && loads[6] == 3,
            "left and right hands combine once without also charging arm region");
        Check(CBMBurden.Factor(pawn, StatDefOf.WorkSpeedGlobal) < 1 && CBMBurden.Factor(pawn, StatDefOf.AimingDelayFactor) > 1
            && CBMBurden.Factor(pawn, StatDefOf.MoveSpeed) == 1, "hand load slows work and aiming but not movement below total allowance");
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.WorkSpeedGlobal, true, 0) / baseWork - CBMBurden.Factor(pawn, StatDefOf.WorkSpeedGlobal)) < .001f,
            "hand penalty applies through real native work speed stat");
        add(legs[0]);
        add(legs[1]);
        Check(CBMBurden.Loads(pawn)[(int)CBMLoadGroup.Legs] == 3 && CBMBurden.Factor(pawn, StatDefOf.MoveSpeed) < 1,
            "combined leg load slows movement");
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed, true, 0) / baseline - CBMBurden.Factor(pawn, StatDefOf.MoveSpeed)) < .001f,
            "movement penalty modifies actual pawn speed");
        float loaded = CBMBurden.Loads(pawn)[6];
        added[0].active = false;
        added[0].condition = CBMCondition.BurntOut;
        added[0].durability = 0;
        Check(CBMBurden.Loads(pawn)[6] == loaded, "disabled burnt and prepared implants retain burden");
        add(pawn.RaceProps.body.corePart);
        add(pawn.RaceProps.body.corePart);
        Check(CBMBurden.Loads(pawn)[6] == 9 && CBMBurden.Penalty(CBMBurden.Profiles.Single(p => p.group == CBMLoadGroup.Total), 9) > 0,
            "total implant load has its own overload effect");
        var profile = CBMBurden.Profiles.Single(p => p.group == CBMLoadGroup.Feet);
        Check(CBMBurden.Penalty(profile, profile.freeKg) == 0 && CBMBurden.Penalty(profile, profile.freeKg + 3) > CBMBurden.Penalty(profile, profile.freeKg + 1),
            "allowance boundary is free and heavier excess increases penalty");
        var moveParts = StatDefOf.MoveSpeed.parts;
        Check(moveParts.FindIndex(p => p is StatPart_CBM) < moveParts.FindIndex(p => p is StatPart_CBMBurden), "burden applies after implant movement bonuses");
        foreach (var module in added)
            pawn.health.RemoveHediff(module);
        Check(CBMBurden.Loads(pawn).All(v => v == 0) && Math.Abs(pawn.GetStatValue(StatDefOf.MoveSpeed, true, 0) - baseline) < .001f,
            "removal immediately restores base load and speed");
        pawn.Destroy();
    }

    static void EconomyTests(Pawn pawn)
    {
        var battery = ThingDef.Named("RimCBM_BatteryItem");
        Check(Math.Abs(battery.BaseMass - 1.5f) < .001f && battery.BaseMarketValue == 600, "implant item has configured native mass and price");
        var def = HediffDef.Named("RimCBM_Battery");
        Check(def.priceOffset == battery.BaseMarketValue, "installed value uses the same base item price");
        float before = pawn.GetStatValue(StatDefOf.Mass, true, 0);
        float carrying = MassUtility.Capacity(pawn), cargo = MassUtility.GearAndInventoryMass(pawn);
        var module = (Hediff_CBMModule)HediffMaker.MakeHediff(def, pawn, pawn.RaceProps.body.corePart);
        module.status = ModuleStatus.SurgeryPrepared;
        pawn.health.AddHediff(module, module.Part);
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.Mass, true, 0) - before - 1.5f) < .01f, "prepared implant adds physical pawn mass once");
        module.condition = CBMCondition.BurntOut;
        module.durability = 0;
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.Mass, true, 0) - before - 1.5f) < .01f, "burnt implant retains physical mass");
        Check(MassUtility.Capacity(pawn) == carrying && MassUtility.GearAndInventoryMass(pawn) == cargo, "body mass does not change vanilla carrying capacity or caravan cargo mass");
        pawn.health.RemoveHediff(module);
        Check(Math.Abs(pawn.GetStatValue(StatDefOf.Mass, true, 0) - before) < .01f, "extracted implant releases body mass");
        var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.GetModExtension<CBMTradeExtension>() != null).ToList();
        Check(all.Count == 12 && all.All(d => !d.tradeTags.Contains("TechHediff")), "dedicated trade pool prevents vanilla generic implant stock bypass");
        var traders = DefDatabase<TraderKindDef>.AllDefsListForReading.Where(d => d.stockGenerators.OfType<StockGenerator_CBM>().Any()).ToList();
        Check(traders.Count == 9 && traders.All(t => t.stockGenerators.OfType<StockGenerator_CBM>().Count() == 1), "caravans ships and settlement receive one stock generator each");
        Check(traders.All(t => t.WillTrade(battery)), "eligible traders buy implants even when their generated stock is empty");
        var generator = new StockGenerator_CBM { stockChance = 0, kinds = new IntRange(2, 4), countRange = new IntRange(1, 2) };
        Check(!generator.GenerateThings(pawn.Map.Tile).Any(), "zero stock probability produces no implants");
        generator.stockChance = 1;
        Rand.PushState(70123);
        try
        {
            var goods = generator.GenerateThings(pawn.Map.Tile).ToList();
            Check(goods.Select(t => t.def).Distinct().Count() >= 2 && goods.Select(t => t.def).Distinct().Count() <= 4 && goods.All(t => t.stackCount <= 2), "stock respects distinct type and unit count bounds");
            Check(goods.All(t => t.TryGetComp<CompCBMCondition>().condition == CBMCondition.None && t.TryGetComp<CompCBMCondition>().durability == 1), "normal trader stock is clean sterile and intact");
            foreach (var thing in goods)
                thing.Destroy();
            Check(!generator.HandlesThingDef(ThingDef.Named("RimCBM_PlaceholderItem")) && !generator.HandlesThingDef(ThingDef.Named("RimCBM_AbilityRigItem")), "debug prototypes are excluded from normal commerce");
            generator.pool = "Combat";
            Check(generator.HandlesThingDef(ThingDef.Named("RimCBM_OverdriveItem")) && !generator.HandlesThingDef(ThingDef.Named("RimCBM_BayItem")), "combat stock filters the configured pool");
            generator.pool = null;
            generator.stockChance = .5f;
            generator.kinds = IntRange.One;
            generator.countRange = IntRange.One;
            int nonempty = 0, storage = 0, bays = 0;
            for (int i = 0; i < 400; i++)
            {
                var sample = generator.GenerateThings(pawn.Map.Tile).ToList();
                if (sample.Count > 0)
                    nonempty++;
                storage += sample.Count(t => t.def == battery);
                bays += sample.Count(t => t.def.defName == "RimCBM_BayItem");
                foreach (var thing in sample)
                    thing.Destroy();
            }
            Check(nonempty > 150 && nonempty < 250, "seeded stock simulation respects per-refresh appearance probability");
            Check(storage > bays * 2, "weighted rarity makes batteries more common than expansion bays");
        }
        finally { Rand.PopState(); }
    }

    static void SurgeryTests(Pawn pawn)
    {
        var doctor = pawn.Map.mapPawns.FreeColonists.First(p => p != pawn && p.Name.ToStringShort != "Dynamic");
        var head = pawn.RaceProps.body.AllParts.First(p => p.def.defName == "Head");
        var alpha = HediffDef.Named("RimCBM_Row");
        var beta = HediffDef.Named("RimCBM_AbilityRig");
        var ext = alpha.GetModExtension<CBMExtension>();
        var exclusion = new CBMExclusionEntry { scope = LimitScope.Pawn, modules = new List<HediffDef> { beta } };
        ext.exclusions.Add(exclusion);
        Check(!CBMSurgery.CanPrepare(pawn, head, alpha).Allowed, "runtime asymmetric exclusion detects existing other module globally");
        exclusion.modules.Clear();
        exclusion.modules.Add(HediffDef.Named("RimCBM_Controller"));
        Check(CBMSurgery.QueueInstall(pawn, head, alpha).Allowed, "exclusive test surgery can reserve first module");
        var first = pawn.BillStack.Bills.Last();
        Check(!CBMSurgery.QueueInstall(pawn, head, exclusion.modules[0]).Allowed, "reverse declaration blocks conflicting queued surgery");
        var forced = new Bill_Medical(DefDatabase<RecipeDef>.GetNamed("RimCBM_ControllerPrepare"), null);
        pawn.BillStack.AddBill(forced);
        forced.Part = head;
        Check(first.ShouldDoNow() && !forced.ShouldDoNow(), "native medical scheduling skips later conflicting bill without consuming medicine");
        pawn.BillStack.Delete(first);
        Check(forced.ShouldDoNow(), "cancelling earlier medical reservation permits later bill");
        pawn.BillStack.Delete(forced);
        ext.exclusions.Remove(exclusion);
        Check(HediffDef.Named("RimCBM_Controller").GetModExtension<CBMExtension>().installationLimits.Count == 0
            && HediffDef.Named("RimCBM_Controller").GetModExtension<CBMExtension>().exclusions.Any(e => e.scope == LimitScope.Pawn && e.modules.Contains(HediffDef.Named("RimCBM_Controller"))), "generated unique controller migrated to global self-exclusion");
        var def = HediffDef.Named("RimCBM_Controller");
        Check(CBMSurgery.QueueInstall(pawn, head, def).Allowed && !CBMSurgery.QueueInstall(pawn, head, def).Allowed,
            "queued surgery reserves unique-module quota");
        var queued = pawn.BillStack.Bills.OfType<Bill_Medical>().First(b => b.recipe.addsHediff == def);
        pawn.BillStack.Delete(queued);
        var recipe = DefDatabase<RecipeDef>.GetNamed("RimCBM_PlaceholderPrepare");
        Check(recipe.ingredients.Count > 1 && recipe.anesthetize && recipe.workAmount > 0 && recipe.skillRequirements.Any(),
            "installation keeps vanilla medicine anesthesia work and skill requirements");
        var surgicalExt = recipe.addsHediff.GetModExtension<CBMExtension>();
        var savedInstall = surgicalExt.installationSurgery;
        var savedRemove = surgicalExt.removalSurgery;
        surgicalExt.installationSurgery = surgicalExt.removalSurgery = new CBMSurgeryProfile { severeChance = 0, burnoutChance = 0, minorDamage = new FloatRange(.25f, .25f), minorPatientDamage = 0 };
        var profile = new CBMSurgeryProfile();
        Check(profile.Result(.01f) == CBMSurgeryFailureKind.Burnout && profile.Result(.1f) == CBMSurgeryFailureKind.Severe && profile.Result(.9f) == CBMSurgeryFailureKind.Minor, "surgery severity roll covers burnout severe and minor branches");
        var nativeBill = new Bill_Medical(recipe, null);
        nativeBill.consumedMedicine[ThingDefOf.MedicineIndustrial] = 2;
        var qualityItem = ThingMaker.MakeThing(surgicalExt.item);
        float healthy = recipe.surgeryOutcomeEffect.GetQuality(recipe, doctor, pawn, new List<Thing> { qualityItem }, head, nativeBill);
        qualityItem.TryGetComp<CompCBMCondition>().durability = .5f;
        float damaged = recipe.surgeryOutcomeEffect.GetQuality(recipe, doctor, pawn, new List<Thing> { qualityItem }, head, nativeBill);
        Check(healthy > 0 && Math.Abs(damaged / healthy - .85f) < .001f, "native surgery quality applies durability exactly once");
        surgicalExt.installationSurgery.successFactor = .8f;
        float harder = recipe.surgeryOutcomeEffect.GetQuality(recipe, doctor, pawn, new List<Thing> { qualityItem }, head, nativeBill);
        Check(Math.Abs(harder / damaged - .8f) < .001f, "native surgery quality applies per-implant success multiplier");
        surgicalExt.installationSurgery.successFactor = 1;
        Check(recipe.surgeryOutcomeEffect.outcomes.Count == 2 && recipe.surgeryOutcomeEffect.outcomes.Last().totalDamage == 0, "custom outcome preserves native quality but avoids duplicate patient injury");
        var rows = qualityItem.def.SpecialDisplayStats(StatRequest.For(qualityItem)).ToList();
        Check(rows.Any(r => r.LabelCap == "RC_Info_Condition".Translate() && r.ValueString.Contains("50%"))
            && rows.Any(r => r.LabelCap == "RC_Info_Install".Translate()), "physical info card contains live durability and surgical rate");
        Check(DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.GetModExtension<CBMExtension>() != null).All(d => !d.label.Contains("CBM")), "implant names no longer contain CBM");
        qualityItem.Destroy();
        var originalOutcome = recipe.surgeryOutcomeEffect;
        recipe.surgeryOutcomeEffect = null;
        var item = ThingMaker.MakeThing(ThingDef.Named("RimCBM_PlaceholderItem"));
        item.TryGetComp<CompCBMCondition>().durability = .6f;
        var bill = new Bill_Medical(recipe, null);
        pawn.BillStack.AddBill(bill);
        bill.Part = head;
        recipe.Worker.ConsumeIngredient(item, recipe, pawn.Map);
        Check(!item.Destroyed, "CBM survives native pre-outcome ingredient consumption");
        bill.Notify_IterationCompleted(doctor, new List<Thing> { item });
        var installed = ImplantService.Modules(pawn).Last(m => m.Part == head);
        Check(item.Destroyed && installed.status == ModuleStatus.SurgeryPrepared && Math.Abs(installed.durability - .6f) < .001f,
            "native medical completion consumes one real item and preserves condition in pending list");
        var failure = new SurgeryOutcomeEffectDef { outcomes = new List<SurgeryOutcome> { new ProbeFailureOutcome { failure = true } } };
        recipe.surgeryOutcomeEffect = failure;
        var failedItem = ThingMaker.MakeThing(item.def);
        int count = ImplantService.Modules(pawn).Count();
        var failedBill = new Bill_Medical(recipe, null);
        pawn.BillStack.AddBill(failedBill);
        failedBill.Part = head;
        recipe.Worker.ConsumeIngredient(failedItem, recipe, pawn.Map);
        failedBill.Notify_IterationCompleted(doctor, new List<Thing> { failedItem });
        Check(!failedItem.Destroyed && failedItem.Spawned && ImplantService.Modules(pawn).Count() == count
            && Math.Abs(failedItem.TryGetComp<CompCBMCondition>().durability - .75f) < .001f
            && failedItem.TryGetComp<CompCBMCondition>().condition.HasFlag(CBMCondition.Dirty),
            "failed surgery returns damaged contaminated item without phantom implant");
        recipe.surgeryOutcomeEffect = originalOutcome;
        var comp = failedItem.TryGetComp<CompCBMCondition>();
        foreach (string service in new[] { "Clean", "Repair", "Sterilize" })
        {
            var serviceRecipe = DefDatabase<RecipeDef>.GetNamed("RimCBM_" + service);
            var worker = (Recipe_ServiceCBM)serviceRecipe.Worker;
            Check(worker.Accepts(failedItem), "maintenance accepts correct state: " + service);
            worker.ConsumeIngredient(failedItem, serviceRecipe, pawn.Map);
            worker.Notify_IterationCompleted(doctor, new List<Thing> { failedItem });
            if (service == "Clean")
                Check(!comp.condition.HasFlag(CBMCondition.Dirty) && comp.condition.HasFlag(CBMCondition.Unsterile) && !CBMConditionUtility.CanInstall(failedItem).Allowed, "cleaning removes dirt without sterilizing or permitting installation");
            if (service == "Repair")
                Check(comp.condition.HasFlag(CBMCondition.Unsterile), "repair leaves sterility invalid");
        }
        Check(comp.condition == CBMCondition.None && comp.durability == 1 && CBMConditionUtility.CanInstall(failedItem).Allowed,
            "clean repair sterilize closes recovery-to-reinstallation loop");
        comp.condition = CBMCondition.BurntOut;
        Check(!((Recipe_ServiceCBM)DefDatabase<RecipeDef>.GetNamed("RimCBM_Repair").Worker).Accepts(failedItem), "ordinary repair cannot revive burnout");
        failedItem.Destroy();
        Check(CBMSurgery.QueueRemoval(pawn, installed).Allowed, "prepared instance can schedule exact extraction");
        var removal = pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().First(b => b.module == installed);
        var removeOutcome = removal.recipe.surgeryOutcomeEffect;
        removal.recipe.surgeryOutcomeEffect = failure;
        removal.Notify_IterationCompleted(doctor, new List<Thing>());
        Check(ImplantService.Modules(pawn).Contains(installed) && Math.Abs(installed.durability - .35f) < .001f,
            "failed exact extraction retains target and applies damage");
        removal.recipe.surgeryOutcomeEffect = null;
        Check(CBMSurgery.QueueRemoval(pawn, installed).Allowed, "failed extraction can be rescheduled");
        var retry = pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().First(b => b.module == installed);
        retry.Notify_IterationCompleted(doctor, new List<Thing>());
        Check(!ImplantService.Modules(pawn).Contains(installed), "successful rescheduled extraction removes exact instance");
        retry.recipe.surgeryOutcomeEffect = removeOutcome;
        var burntItem = ThingMaker.MakeThing(surgicalExt.item);
        CBMSurgicalOutcome.FailedInstall(burntItem, pawn, doctor, head, new CBMSurgeryProfile { burnoutChance = 1, severeChance = 0, burnoutPatientDamage = 0 });
        Check(burntItem.Spawned && burntItem.TryGetComp<CompCBMCondition>().condition.HasFlag(CBMCondition.BurntOut) && burntItem.TryGetComp<CompCBMCondition>().durability == 0, "burnout failure returns persistent contaminated wreck");
        burntItem.Destroy();
        surgicalExt.installationSurgery = savedInstall;
        surgicalExt.removalSurgery = savedRemove;
    }
}

public class ProbeFailureOutcome : SurgeryOutcome
{
    public override bool Apply(float quality, RecipeDef recipe, Pawn surgeon, Pawn patient, BodyPartRecord part) => true;
}





public class ProbeDamageEffect : CBMDamageEffectWorker
{
    public static int calls;
    public override void Apply(CBMDamageContext context, CBMDamageEffect effect)
    {
        calls++;
    }
}

public class ProbeFaultWorker : CBMFaultWorker
{
    public static int calls;
    public override void Apply(Hediff_CBMModule module, CBMFaultSpec spec)
    {
        calls++;
    }
}

public class ProbeAbilityData : CBMAbilityData
{
    public int value;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref value, "value");
    }
    public override CBMAbilityData Copy() => new ProbeAbilityData { value = value };
}
public class ProbeLifecycleWorker : CBMAbilityWorker
{
    public static int starts, stops, ticks;
    public static CBMStopReason reason;
    public override void OnStarted(Hediff_CBMModule m, CBMAbilitySpec a, CBMAbilityState s)
    {
        starts++;
    }
    public override void OnStopped(Hediff_CBMModule m, CBMAbilitySpec a, CBMAbilityState s, CBMStopReason r)
    {
        stops++;
        reason = r;
    }
    public override void Tick(Hediff_CBMModule m, CBMAbilitySpec a, CBMAbilityState s, int delta)
    {
        ticks++;
        ((ProbeAbilityData)s.data).value++;
    }
}
public class ProbeEventWorker : CBMAbilityWorker
{
    public static CBMExecutionContext context;
    public override void Apply(CBMExecutionContext c)
    {
        context = c;
    }
}
public class ProbeCustomEffect : CBMEffectWorker
{
    public static bool applied;
    public override void Apply(CBMExecutionContext c, CBMAbilityEffect effect)
    {
        applied = true;
    }
}

public class ProbeFuelSource : CBMFuelSourceWorker
{
    public static int portions;
    public static bool fail;
    public override int AvailablePortions(Thing source) => portions;
    public override bool TryConsume(Thing source, int count)
    {
        if (fail || count > portions)
            return false;
        portions -= count;
        return true;
    }
}
public class ProbeChargeSource : CBMChargeSourceWorker
{
    public static float energy;
    public override bool Available(CompCBMCharger charger) => energy > 0;
    public override void Tick(CompCBMCharger charger)
    {
        if (energy >= .25f && charger.Ready)
        {
            energy -= .25f;
            charger.buffer += .25f;
        }
    }
}
