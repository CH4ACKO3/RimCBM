using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    public class CBMPanelState : GameComponent
    {
        private List<Pawn> openPawns = new List<Pawn>();
        public CBMPanelState(Game game) { }
        public static bool IsOpen(Pawn pawn) => pawn != null && Current.Game?.GetComponent<CBMPanelState>()?.openPawns.Contains(pawn) == true;
        public static void SetOpen(Pawn pawn, bool open)
        {
            var state = Current.Game?.GetComponent<CBMPanelState>();
            if (state == null || pawn == null) return;
            state.openPawns.Remove(pawn);
            if (open) state.openPawns.Add(pawn);
            GridPanel.ClearInteraction();
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref openPawns, "rimCBMOpenPanels", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { if (openPawns == null) openPawns = new List<Pawn>(); openPawns.RemoveAll(p => p == null); }
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "VisibleHediffs")]
    public static class CBMHealthVisibility
    {
        public static void Postfix(Pawn pawn, ref IEnumerable<Hediff> __result)
        {
            // Only filter the display enumeration. Never touch the pawn's actual health list.
            bool open = CBMPanelState.IsOpen(pawn);
            __result = __result.Where(h => !(h is Hediff_CBMModule module) || (open && module.status == RimCBM.Core.ModuleStatus.Installed));
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffRow")]
    public static class CBMNativeContextMenu
    {
        private static IEnumerable<Hediff> entry;
        private static Pawn entryPawn;
        private static readonly Func<IEnumerable<Hediff>, Pawn, bool> NativeCanClick =
            (Func<IEnumerable<Hediff>, Pawn, bool>)Delegate.CreateDelegate(typeof(Func<IEnumerable<Hediff>, Pawn, bool>),
                AccessTools.Method(typeof(HealthCardUtility), "CanEntryBeClicked"));
        public static bool CaptureEntry(IEnumerable<Hediff> diffs, Pawn pawn)
        { entry = diffs; entryPawn = pawn; return NativeCanClick(diffs, pawn); }
        public static bool EntryButton(Rect rect, bool sound)
        {
            if (Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1) && Mouse.IsOver(rect))
            {
                var modules = entry?.OfType<Hediff_CBMModule>().ToList();
                if (modules != null && modules.Count > 0)
                {
                    if (Event.current.button == 0) GridPanel.FocusModule(entryPawn, modules[0]);
                    else GridPanel.ShowModuleGroupMenu(entryPawn, modules);
                    Event.current.Use(); return false;
                }
            }
            return Widgets.ButtonInvisible(rect, sound);
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var capture = AccessTools.Method(typeof(HealthCardUtility), "CanEntryBeClicked");
            var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) });
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(capture)) instruction.operand = AccessTools.Method(typeof(CBMNativeContextMenu), nameof(CaptureEntry));
                else if (instruction.Calls(button)) instruction.operand = AccessTools.Method(typeof(CBMNativeContextMenu), nameof(EntryButton));
                yield return instruction;
            }
        }
    }

    public static class GridDrawing
    {
        public static float Pixel => 1f / Prefs.UIScale;
        public static float Snap(float value) => Mathf.Round(value * Prefs.UIScale) / Prefs.UIScale;
        public static void Solid(Rect rect, Color color)
        {
            var previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, BaseContent.WhiteTex); GUI.color = previous;
        }
        public static void Border(Rect rect, Color color)
        {
            float p = Pixel;
            Solid(new Rect(rect.x, rect.y, rect.width, p), color);
            Solid(new Rect(rect.x, rect.yMax - p, rect.width, p), color);
            Solid(new Rect(rect.x, rect.y + p, p, rect.height - 2 * p), color);
            Solid(new Rect(rect.xMax - p, rect.y + p, p, rect.height - 2 * p), color);
        }
    }
}
