using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public class CBMFuelSourceWorker
    {
        // A portion maps to option.unitsPerItem. Queries are pure; failed consumption must change nothing.
        public virtual int AvailablePortions(Thing source) => source == null || source.Destroyed ? 0 : source.stackCount;
        public virtual bool TryConsume(Thing source, int portions)
        {
            if (portions <= 0 || AvailablePortions(source) < portions) return false;
            source.SplitOff(portions).Destroy(); return true;
        }
    }
    public class CBMChargeSourceWorker
    {
        public virtual bool Available(CompCBMCharger charger) => charger.parent.TryGetComp<CompPowerTrader>()?.PowerOn == true;
        // Called once per map tick. A custom provider must debit its source for all energy put in the buffer.
        public virtual void Tick(CompCBMCharger charger)
        {
            var power = charger.parent.TryGetComp<CompPowerTrader>(); if (power == null) return;
            if (charger.Ready) charger.buffer = Mathf.Min(charger.Settings.bufferWd, charger.buffer
                + Mathf.Max(0, -power.PowerOutput - 5) * CompPower.WattsToWattDaysPerTick * charger.Settings.efficiency);
            power.PowerOutput = -(5 + (charger.buffer < charger.Settings.bufferWd - .00001f ? charger.Settings.watts : 0));
        }
    }
}
