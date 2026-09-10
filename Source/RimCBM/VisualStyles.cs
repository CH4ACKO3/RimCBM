using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using RimCBM.Core;

namespace RimCBM
{
    public class CBMVisualStyleDef : Def
    {
        public string type, tag, cellColor, moduleColor, cellIndicatorColor, moduleIndicatorColor, inactiveIndicatorColor;
        public string dirtyIndicatorColor, damagedIndicatorColor, burntIndicatorColor, empIndicatorColor, unpoweredIndicatorColor;
        public int priority;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors()) yield return error;
            foreach (var color in new[] { cellColor, moduleColor, cellIndicatorColor, moduleIndicatorColor, inactiveIndicatorColor, dirtyIndicatorColor, damagedIndicatorColor, burntIndicatorColor, empIndicatorColor, unpoweredIndicatorColor })
                if (color != null && !CBMColors.TryColor(color, out _)) yield return "Invalid color (use #RRGGBB or #RRGGBBAA).";
        }
    }
    public static class CBMColors
    {
        private sealed class Palette { public Color Fill, Indicator, Inactive; }
        private static readonly ConditionalWeakTable<object, Palette> Cache = new ConditionalWeakTable<object, Palette>();
        private static CBMVisualStyleDef[] styles;
        public static bool TryColor(string value, out Color color)
        {
            if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)) { color = Color.clear; return true; }
            if (value == "HealthGood") { color = HealthUtility.GoodConditionColor; return true; }
            // HealthCardUtility.efficiencyToColor[EfficiencyEstimate.Enhanced], RimWorld 1.6.
            if (value == "HealthEnhanced") { color = new Color(.5f, .5f, .9f); return true; }
            return ColorUtility.TryParseHtmlString(value ?? "", out color);
        }
        private static Color Resolve(string value, string type, HashSet<string> tags, Func<CBMVisualStyleDef, string> field, string fallback)
        {
            if (TryColor(value, out var color)) return color;
            if (styles == null) styles = DefDatabase<CBMVisualStyleDef>.AllDefsListForReading.OrderByDescending(s => s.priority).ThenBy(s => s.defName).ToArray();
            var style = styles.FirstOrDefault(s => field(s) != null && type != null && s.type == type)
                ?? styles.FirstOrDefault(s => field(s) != null && s.tag != null && tags.Contains(s.tag));
            TryColor(style == null ? fallback : field(style), out color);
            return color;
        }
        private static Palette CellPalette(object value)
        {
            var cell = (Cell)value;
            return new Palette {
                Fill = Resolve(cell.Color, cell.Type, cell.Tags, s => s.cellColor, "#292C2E"),
                Indicator = Resolve(cell.IndicatorColor, cell.Type, cell.Tags, s => s.cellIndicatorColor, "None") };
        }
        private static Palette ModulePalette(object value)
        {
            var module = (ModuleSpec)value;
            return new Palette {
                Fill = Resolve(module.Color, module.Type, module.Tags, s => s.moduleColor, "#525761"),
                Indicator = Resolve(module.IndicatorColor, module.Type, module.Tags, s => s.moduleIndicatorColor, "#96D2A3"),
                Inactive = Resolve(module.InactiveIndicatorColor, module.Type, module.Tags, s => s.inactiveIndicatorColor, "#6B737B") };
        }
        public static Color ModuleIndicator(ModuleSpec spec, Hediff_CBMModule module)
        {
            var ext = module.def.GetModExtension<CBMExtension>();
            if (ConditionRules.Burnt(module.condition, module.durability)) return Resolve(ext.burntIndicatorColor, spec.Type, spec.Tags, s => s.burntIndicatorColor, "#963F37");
            if (module.empUntilTick > Find.TickManager.TicksGame) return Resolve(ext.empIndicatorColor, spec.Type, spec.Tags, s => s.empIndicatorColor, "#7169AB");
            if ((module.condition & (CBMCondition.Dirty | CBMCondition.Unsterile)) != 0) return Resolve(ext.dirtyIndicatorColor, spec.Type, spec.Tags, s => s.dirtyIndicatorColor, "#998654");
            if (module.durability < 1) return Resolve(ext.damagedIndicatorColor, spec.Type, spec.Tags, s => s.damagedIndicatorColor, "#AE793E");
            if (module.HardwareReady && module.active && !module.Operational) return Resolve(ext.unpoweredIndicatorColor, spec.Type, spec.Tags, s => s.unpoweredIndicatorColor, "#667786");
            return ModuleIndicator(spec, module.Operational);
        }
        public static Color Cell(Cell cell) => Cache.GetValue(cell, CellPalette).Fill;
        public static Color CellIndicator(Cell cell) => Cache.GetValue(cell, CellPalette).Indicator;
        public static Color Module(ModuleSpec module) => Cache.GetValue(module, ModulePalette).Fill;
        public static Color ModuleIndicator(ModuleSpec module, bool active) => active ? Cache.GetValue(module, ModulePalette).Indicator : Cache.GetValue(module, ModulePalette).Inactive;
    }
}
