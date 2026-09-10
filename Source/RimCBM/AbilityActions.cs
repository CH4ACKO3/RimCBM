using System;
using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RimCBM
{
    public static class CBMRuntime
    {
        // Only simulated hosts run. Containers and dormant world pawns never catch up missed ticks.
        public static bool Running(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Dead && !pawn.Suspended
            && (pawn.Spawned || pawn.IsCaravanMember());
        public static bool Allows(Pawn pawn, CBMAbilitySpec ability) => Running(pawn) && (pawn.Spawned
            || !ability.mapOnly && ability.damageAmount <= 0 && ability.effects.All(e => !e.Worker.RequiresMap(e)));
    }

    public static class CBMCasting
    {
        public static RuleResult CanQueue(Hediff_CBMModule module, CBMAbilitySpec ability, Thing target = null)
        {
            target = target ?? module.pawn;
            if (module.pawn.CurJobDef?.defName == "RimCBM_CastAbility" && module.pawn.CurJob.source == module && module.pawn.CurJob.dutyTag == ability.id) return RuleResult.Fail("Cooldown");
            var check = CBMAbilities.CanActivate(module, ability, target: target, planning: true);
            if (!check.Allowed || !module.pawn.Spawned) return check;
            return Position(module.pawn, ability, target, out _) ? RuleResult.Ok() : RuleResult.Fail("CBMOutOfRange");
        }
        public static bool Position(Pawn pawn, CBMAbilitySpec ability, Thing target, out IntVec3 cell)
        {
            cell = pawn.Position;
            if (CBMTargeting.Validate(pawn, ability, target).Allowed) return true;
            if (!CBMTargeting.Validate(pawn, ability, target, true).Allowed) return false;
            cell = GenRadial.RadialCellsAround(target.Position, Math.Min(ability.range, 56), true)
                .Where(c => c.InBounds(pawn.Map) && c.Standable(pawn.Map) && !c.Fogged(pawn.Map)
                    && (!ability.requireLineOfSight || GenSight.LineOfSight(c, target.Position, pawn.Map)))
                .OrderBy(c => c.DistanceToSquared(pawn.Position))
                .Where(c => pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)).DefaultIfEmpty(IntVec3.Invalid).First();
            return cell.IsValid;
        }
        public static RuleResult Queue(Hediff_CBMModule module, CBMAbilitySpec ability, Thing target = null)
        {
            target = target ?? module.pawn;
            var check = CanQueue(module, ability, target); if (!check.Allowed) return check;
            if (!module.pawn.Spawned) return CBMAbilities.Activate(module, ability, target: target);
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RimCBM_CastAbility"), target);
            job.source = module; job.dutyTag = ability.id;
            // Vanilla compares target and jobDef, but two implants may share both.
            if (module.pawn.CurJobDef == job.def) module.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            module.pawn.jobs.TryTakeOrderedJob(job);
            return module.pawn.CurJob == job ? RuleResult.Ok() : RuleResult.Fail("OperatorUnavailable");
        }
    }

    public class JobDriver_CBMCastAbility : JobDriver
    {
        private Hediff_CBMModule Module => job.source as Hediff_CBMModule;
        private CBMAbilitySpec Ability => Module == null ? null : CBMAbilities.Specs(Module).FirstOrDefault(a => a.id == job.dutyTag);
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Module == null || Module.pawn != pawn || Ability == null || !job.targetA.HasThing || job.targetA.Thing.Destroyed || !CBMAbilities.CanActivate(Module, Ability, target: job.targetA.Thing, planning: true).Allowed);
            yield return Toils_General.Do(() => {
                if (!CBMCasting.Position(pawn, Ability, job.targetA.Thing, out var cell)) EndJobWith(JobCondition.Incompletable);
                else job.targetC = cell;
            });
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            var warmup = Toils_General.Wait(1);
            warmup.initAction = () => ticksLeftThisToil = Ability.warmupTicks;
            warmup.FailOn(() => !CBMTargeting.Validate(pawn, Ability, job.targetA.Thing).Allowed);
            warmup.WithProgressBarToilDelay(TargetIndex.A);
            yield return warmup;
            yield return Toils_General.Do(() => {
                var result = CBMAbilities.Activate(Module, Ability, target: job.targetA.Thing);
                if (!result.Allowed) EndJobWith(JobCondition.Incompletable);
            });
        }
    }
}
