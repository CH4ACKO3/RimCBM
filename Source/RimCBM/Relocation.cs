using System.Collections.Generic;
using System.Linq;
using RimCBM.Core;
using Verse;
using Verse.AI;

namespace RimCBM
{
    public static class CBMRelocation
    {
        public static JobDef JobDef => DefDatabase<JobDef>.GetNamed("RimCBM_Relocate");
        public static void Cancel(Hediff_CBMModule module, bool stopJob = true)
        {
            module.intentBoard = null;
            var pawn = module.pawn;
            if (stopJob && pawn?.jobs != null)
            {
                foreach (var queued in pawn.jobs.jobQueue.Where(q => q.job.def == JobDef && q.job.count == module.loadID).ToList())
                    pawn.jobs.jobQueue.Extract(queued.job)?.Cleanup(pawn, true);
                if (pawn.CurJobDef == JobDef && pawn.CurJob.count == module.loadID)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            PanelSnapshot.Invalidate();
        }
        public static RuleResult Queue(Pawn pawn, string id, string board, int x, int y, bool rotated)
        {
            if (!ImplantService.CanHotSwap(pawn) || pawn.InMentalState) return RuleResult.Fail("OperatorUnavailable");
            var module = ImplantService.Modules(pawn).FirstOrDefault(m => m.GetUniqueLoadID() == id);
            if (module == null) return RuleResult.Fail("UnknownModule");
            if (module.intentBoard != null) return RuleResult.Fail("Reserved");
            var result = ImplantService.Build(pawn).Place(id, board, x, y, rotated, false);
            if (!result.Allowed) return result;
            if (module.status == ModuleStatus.Installed && module.boardId == board && module.x == x && module.y == y && module.rotated == rotated) return RuleResult.Ok();
            module.intentBoard = board; module.intentX = x; module.intentY = y; module.intentRotated = rotated;
            var job = JobMaker.MakeJob(JobDef, pawn);
            job.count = module.loadID;
            job.playerForced = true;
            // Appending directly avoids vanilla's same-job comparison merging distinct modules.
            if (pawn.CurJobDef == JobDef || pawn.jobs.jobQueue.Any(q => q.job.def == JobDef))
                pawn.jobs.jobQueue.EnqueueLast(job);
            else if (!pawn.jobs.TryTakeOrderedJob(job)) { Cancel(module); return RuleResult.Fail("OperatorUnavailable"); }
            PanelSnapshot.Invalidate();
            return RuleResult.Ok();
        }
    }

    public class JobDriver_CBMRelocate : JobDriver
    {
        private Hediff_CBMModule Module => ImplantService.Modules(pawn).FirstOrDefault(m => m.loadID == job.count);
        public override bool TryMakePreToilReservations(bool errorOnFailed) => Module?.intentBoard != null;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var module = Module;
            this.FailOn(() => module == null || module.intentBoard == null || !ImplantService.Modules(pawn).Contains(module)
                || !ImplantService.CanHotSwap(pawn) || pawn.InMentalState);
            AddFinishAction(condition => { if (module != null) CBMRelocation.Cancel(module, false); });
            var work = Toils_General.Wait(120).WithProgressBarToilDelay(TargetIndex.A);
            work.initAction = () => pawn.pather.StopDead();
            yield return work;
            yield return Toils_General.Do(() => {
                var result = ImplantService.Place(pawn, module.GetUniqueLoadID(), module.intentBoard,
                    module.intentX, module.intentY, module.intentRotated);
                if (!result.Allowed) Messages.Message(("RC_Error_" + result.Error).Translate(), pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                CBMRelocation.Cancel(module, false);
            });
        }
    }

    // Queued jobs can be cleared without ever creating a driver; release their reservations too.
    public class CBMRelocationCleanup : GameComponent
    {
        public CBMRelocationCleanup(Game game) { }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var pawn in RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList())
                foreach (var module in ImplantService.Modules(pawn).Where(m => m.intentBoard != null).ToList())
                {
                    bool Scheduled(Job j) => j != null && j.def == CBMRelocation.JobDef && j.count == module.loadID;
                    if (pawn.Dead || (!Scheduled(pawn.CurJob) && !(pawn.jobs?.jobQueue.Any(q => Scheduled(q.job)) ?? false)))
                        CBMRelocation.Cancel(module);
                }
        }
    }
}
