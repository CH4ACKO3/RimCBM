using System;

namespace RimCBM.Core
{
    [Flags]
    public enum CBMCondition { None = 0, Dirty = 1, Unsterile = 2, BurntOut = 4 }
    public enum SlotLossReason { Removed, Destroyed }
    public static class ConditionRules
    {
        public static bool Burnt(CBMCondition condition, float durability) => (condition & CBMCondition.BurntOut) != 0 || durability <= 0;
        public static string InstallError(CBMCondition condition, float durability)
        {
            if (Burnt(condition, durability)) return "BurntOut";
            if ((condition & CBMCondition.Dirty) != 0) return "Dirty";
            if ((condition & CBMCondition.Unsterile) != 0) return "Unsterile";
            return null;
        }
    }
}
