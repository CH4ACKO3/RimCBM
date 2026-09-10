using System.Collections.Generic;
using Verse.AI;
using Verse;

namespace RimCBM
{
    // Retain the type and JobDef so old saves load; resumed jobs cannot transfer power.
    public class JobDriver_CBMCharge : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => false;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var stop = ToilMaker.MakeToil("RetiredCBMCharge");
            stop.initAction = () => EndJobWith(JobCondition.Incompletable);
            stop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return stop;
        }
    }
}
