using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RimCBM
{
    // Old XML and saves keep their passive power fields; all active uses share the new action pipeline.
    public static class CBMLegacyAbilities
    {
        public const string UseId = "__legacy_use";
        private static readonly ConditionalWeakTable<CBMExtension, CBMAbilitySpec> uses = new ConditionalWeakTable<CBMExtension, CBMAbilitySpec>();
        public static CBMAbilitySpec Use(Hediff_CBMModule module) => Use(module.def.GetModExtension<CBMExtension>());
        private static CBMAbilitySpec Use(CBMExtension ext) => ext.useDurationTicks <= 0 ? null : uses.GetValue(ext, e => new CBMAbilitySpec {
            id = UseId, labelKey = "RC_Use", kind = CBMAbilityKind.OneShot,
            useWd = e.useWd, durationTicks = e.useDurationTicks, cooldownTicks = e.useCooldownTicks
        });
        public static List<CBMAbilitySpec> Specs(CBMExtension ext)
        {
            var use = Use(ext);
            return use == null ? ext.abilities : ext.abilities.Concat(new[] { use }).ToList();
        }
    }
}
