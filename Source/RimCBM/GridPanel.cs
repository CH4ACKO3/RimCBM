using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimCBM.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCBM
{
    [HarmonyPatch(typeof(ITab_Pawn_Health), "FillTab")]
    public static class CompactHealthPanel
    {
        public static bool Prefix()
        {
            var pawn = GridPanel.SelectedPawn;
            if (pawn == null || !pawn.RaceProps.Humanlike) return true;
            var button = new Rect(246f, 3f, 64f, 24f);
            if (Widgets.ButtonText(button, "RC_Implants".Translate())) CBMPanelState.SetOpen(pawn, !CBMPanelState.IsOpen(pawn));
            TooltipHandler.TipRegion(button, "RC_PanelToggle".Translate());
            if (!CBMPanelState.IsOpen(pawn) || UI.screenWidth >= 1052) return true;
            GridPanel.Draw(new Rect(8f, 32f, 614f, AttachedHealthPanel.HealthHeight - 40f), pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(InspectTabBase), nameof(InspectTabBase.DoTabGUI))]
    public static class AttachedHealthPanel
    {
        public static float HealthHeight = 430f;
        private static readonly System.Reflection.FieldInfo SizeField = AccessTools.Field(typeof(InspectTabBase), "size");
        private static readonly System.Reflection.PropertyInfo PaneTop = AccessTools.Property(typeof(InspectTabBase), "PaneTopY");
        public static void Prefix(InspectTabBase __instance)
        {
            if (!(__instance is ITab_Pawn_Health)) return;
            HealthHeight = Mathf.Min(430f, Mathf.Max(160f, (float)PaneTop.GetValue(__instance, null) - 34f));
            SizeField.SetValue(__instance, new Vector2(630f, HealthHeight));
        }
        private static readonly System.Reflection.PropertyInfo TabRect = AccessTools.Property(typeof(InspectTabBase), "TabRect");
        public static void Postfix(InspectTabBase __instance)
        {
            if (!(__instance is ITab_Pawn_Health) || UI.screenWidth < 1052) return;
            var pawn = GridPanel.SelectedPawn;
            if (pawn == null || !pawn.RaceProps.Humanlike || !CBMPanelState.IsOpen(pawn)) return;
            var native = (Rect)TabRect.GetValue(__instance, null);
            var rect = new Rect(native.xMax + 4f, native.y, 410f, native.height);
            Find.WindowStack.ImmediateWindow(194752836, rect, WindowLayer.GameUI,
                () => GridPanel.Draw(new Rect(10f, 10f, rect.width - 20f, rect.height - 20f), pawn), doBackground: true,
                absorbInputAroundWindow: false);
        }
    }

    public static class GridPanel
    {
        private static Pawn current;
        private static BodyPartRecord part;
        private static string boardId, selected, placing, focusBoard;
        private static bool rotated;
        private static Thing externalItem;
        private static HediffDef externalDef;
        private static int dragOffsetX, dragOffsetY, lastFrame;
        private static Vector2 gridScroll, pendingScroll;
        private static PlacementPreview previewCache;
        private static GridRules previewRules;
        private static string previewId;
        private static bool previewRotation;
        private static Dictionary<ThingDef, HediffDef> carriedDefs;
        private static readonly Dictionary<string, string> fittedLabels = new Dictionary<string, string>();
        public static Pawn SelectedPawn => Find.Selector.SingleSelectedThing as Pawn ?? (Find.Selector.SingleSelectedThing as Corpse)?.InnerPawn;
        private static string T(string key) => key.Translate();
        private static string ModuleLabel(HediffDef def)
        {
            var key = def.GetModExtension<CBMExtension>().shortLabelKey;
            return string.IsNullOrEmpty(key) ? def.LabelCap.ToString() : T(key);
        }
        private static string FitLabel(string label, Rect rect)
        {
            string key = label + "|" + rect.width + "|" + rect.height + "|" + Prefs.UIScale;
            if (fittedLabels.TryGetValue(key, out var fitted)) return fitted;
            if (Text.CalcHeight(label, rect.width) <= rect.height) return fittedLabels[key] = label;
            while (label.Length > 1 && Text.CalcHeight(label + "…", rect.width) > rect.height) label = label.Substring(0, label.Length - 1);
            return fittedLabels[key] = label.TrimEnd() + "…";
        }
        private static string Error(RuleResult result) => result.Allowed ? "" : T("RC_Error_" + result.Error);
        private static void CancelPlacement() { previewCache = null; placing = null; externalItem = null; externalDef = null; dragOffsetX = dragOffsetY = 0; }
        public static void ClearInteraction() { CancelPlacement(); selected = null; focusBoard = null; }
        public static void FocusModule(Pawn pawn, Hediff_CBMModule module)
        {
            CBMPanelState.SetOpen(pawn, true);
            current = pawn; part = module.Part; selected = module.GetUniqueLoadID();
            boardId = module.boardId; focusBoard = module.boardId;
            gridScroll = pendingScroll = Vector2.zero;
        }
        private static void Report(RuleResult result)
        {
            CancelPlacement();
            if (!result.Allowed) Messages.Message(Error(result), MessageTypeDefOf.RejectInput, false);
        }

        public static void Draw(Rect rect, Pawn pawn)
        {
            var oldFont = Text.Font; var oldAnchor = Text.Anchor; var oldColor = GUI.color;
            bool released = Event.current.rawType == EventType.MouseUp;
            Widgets.BeginGroup(rect);
            try
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; GUI.color = Color.white;
                if (current != pawn)
                {
                    current = pawn; part = null; boardId = selected = null; CancelPlacement();
                    gridScroll = pendingScroll = Vector2.zero;
                }
                if (Time.frameCount - lastFrame > 1 && Event.current.type == EventType.MouseDown) CancelPlacement();
                lastFrame = Time.frameCount;
                var snapshot = PanelSnapshot.Get(pawn);
                var parts = snapshot.Parts;
                if (parts.Count == 0) return;
                if (part == null || !parts.Contains(part)) { part = parts[0]; boardId = selected = null; CancelPlacement(); }
                var rules = snapshot.Rules;
                string root = ImplantService.RootId(pawn, part);
                var boards = snapshot.Boards.Values.Where(b => b.RootId == root)
                    .OrderBy(b => b.Id.StartsWith("root/") ? 0 : 1).ThenBy(b => b.Id).ToList();
                if (boards.Count == 0) return;
                if (selected != null && !rules.Modules.Any(m => m.Id == selected)) selected = null;
                if (placing != null && (!ImplantService.CanEdit(pawn) || !rules.CanStartPlacement(placing, ImplantService.CanHotSwap(pawn)).Allowed))
                    CancelPlacement();

                if (externalItem != null && (!ImplantService.CanHotSwap(pawn) || !pawn.inventory.innerContainer.Contains(externalItem))) CancelPlacement();
                float sidebarWidth = Mathf.Clamp(rect.width * .30f, 112f, 148f);
                float leftWidth = rect.width - sidebarWidth - 12f;
                if (Widgets.ButtonText(new Rect(0, 0, Mathf.Min(132f, leftWidth), 28), part.LabelCap))
                    Find.WindowStack.Add(new FloatMenu(parts.Select(p => new FloatMenuOption(p.LabelCap, () => {
                        part = p; selected = null; CancelPlacement(); gridScroll = pendingScroll = Vector2.zero;
                    })).ToList()));
                var energy = CBMEnergy.For(pawn);
                Text.Font = GameFont.Tiny;
                var energyRect = new Rect(138, 0, Mathf.Max(40, leftWidth - 138), 32);
                Widgets.Label(energyRect, energy.storedWd.ToString("0.0") + "/" + energy.capacityWd.ToString("0.#") + " Wd\n" + "RC_LoadShort".Translate(CBMBurden.Loads(pawn)[6].ToString("0.#")));
                TooltipHandler.TipRegion(energyRect, "+" + (energy.generatedWd * 1000f).ToString("0.#") + " W / −" + (energy.consumedWd * 1000f).ToString("0.#") + " W\n" + CBMBurden.Description(pawn));
                Text.Font = GameFont.Small;
                DrawGrids(new Rect(0, 36f, leftWidth, rect.height - 132f), pawn, rules, boards);
                DrawInformation(new Rect(0, rect.height - 88f, rect.width, 88f), pawn);
                DrawPending(new Rect(leftWidth + 12f, 0, sidebarWidth, rect.height - 96f), pawn, rules);
                if (Event.current.type == EventType.MouseDrag && (placing != null || externalItem != null)) Event.current.Use();
                if (Event.current.type == EventType.KeyDown && (placing != null || externalItem != null))
                {
                    if (Event.current.keyCode == KeyCode.Escape) { CancelPlacement(); Event.current.Use(); }
                    else if (Event.current.keyCode == KeyCode.R && (externalDef != null ? externalDef.GetModExtension<CBMExtension>().rotatable : rules.Specs[rules.Modules.First(m => m.Id == placing).SpecId].Rotatable))
                    { rotated = !rotated; dragOffsetX = dragOffsetY = 0; Event.current.Use(); }
                }
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && (placing != null || externalItem != null))
                { CancelPlacement(); Event.current.Use(); }
            }
            finally
            {
                // Release anywhere, including outside the grid or an invalid drop, ends the ghost.
                if (released && externalItem == null) CancelPlacement();
                Widgets.EndGroup(); Text.Font = oldFont; Text.Anchor = oldAnchor; GUI.color = oldColor;
            }
        }

        private static void DrawInformation(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);
            var module = ImplantService.Modules(pawn).FirstOrDefault(m => m.GetUniqueLoadID() == selected);
            if (module == null) return;
            Widgets.InfoCardButton(new Rect(rect.x + 12f, rect.y + 54f, 24f, 24f), module);
            Widgets.ThingIcon(new Rect(rect.x + 8f, rect.y + 12f, 32f, 32f), module.def.GetModExtension<CBMExtension>().item);
            Widgets.Label(new Rect(rect.x + 48f, rect.y + 6f, rect.width - 56f, 24f), module.def.LabelCap.ToString().Truncate(rect.width - 132f));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 48f, rect.y + 32f, rect.width - 128f, 24f), (module.Part.LabelCap + " · " + CBMConditionUtility.Label(module.condition, module.durability) + " · " + module.durability.ToStringPercent()).Truncate(rect.width - 128f));
            var ext = module.def.GetModExtension<CBMExtension>();
            Widgets.Label(new Rect(rect.x + 48, rect.y + 54, rect.width - 126, 25), CBMEnergy.Status(module) + " · " + ext.idleWatts.ToString("0.#") + " W");
            Text.Font = GameFont.Small;
            if (ext.abilities.Count > 0)
            {
                if (Widgets.ButtonText(new Rect(rect.xMax - 86, rect.y + 31, 78, 24), T("RC_Abilities"))) Find.WindowStack.Add(new Dialog_CBMAbilities(module));
                return;
            }
            if (Widgets.ButtonText(new Rect(rect.xMax - 72, rect.y + 5, 64, 24), T(module.active ? "RC_TurnOff" : "RC_TurnOn"))) Report(CBMEnergy.Toggle(module));
            if (ext.useDurationTicks > 0 && Widgets.ButtonText(new Rect(rect.xMax - 72, rect.y + 31, 64, 24), T("RC_Use"))) Report(CBMEnergy.Use(module));
            Text.Font = GameFont.Tiny;
            TooltipHandler.TipRegion(new Rect(rect.xMax - 88, rect.y + 59, 80, 22), T("RC_PriorityHint"));
            if (Widgets.ButtonText(new Rect(rect.xMax - 88, rect.y + 59, 80, 22), T("RC_Priority") + " " + module.powerPriority))
                Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, 4).Select(n => new FloatMenuOption(T("RC_Priority") + " " + n, () => { if (ImplantService.CanEdit(pawn)) module.powerPriority = n; })).ToList()));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, module.def.description + "\n" + T("RC_Activation") + ": " + ext.activationWd + " Wd · " + T("RC_Use") + ": " + ext.useWd + " Wd");
        }

        public static List<FloatMenuOption> ModuleOptions(Pawn pawn, Hediff_CBMModule module)
        {
            var options = new List<FloatMenuOption>();
            if (module.intentBoard != null && ImplantService.CanEdit(pawn))
                options.Add(new FloatMenuOption(T("RC_CancelMove"), () => CBMRelocation.Cancel(module)));
            if (Prefs.DevMode)
            {
                options.Add(new FloatMenuOption("DEV: " + T("RC_State_Intact"), () => { module.condition = CBMCondition.None; module.durability = 1; module.active = true; PanelSnapshot.Invalidate(); }));
                options.Add(new FloatMenuOption("DEV: " + T("RC_State_Dirty"), () => { module.condition |= CBMCondition.Dirty | CBMCondition.Unsterile; PanelSnapshot.Invalidate(); }));
                options.Add(new FloatMenuOption("DEV: " + T("RC_State_Damaged"), () => CBMConditionUtility.Damage(module, .35f)));
                options.Add(new FloatMenuOption("DEV: " + T("RC_State_BurntOut"), () => CBMConditionUtility.Damage(module, 1f)));
            }
            var rules = ImplantService.Build(pawn);
            var hot = rules.Detach(module.GetUniqueLoadID(), commit: false);
            bool editable = ImplantService.CanEdit(pawn);
            if (module.status == ModuleStatus.Installed && module.def.GetModExtension<CBMExtension>().hotSwappable)
            {
                bool allowed = editable && hot.Allowed && ImplantService.CanHotSwap(pawn);
                options.Add(new FloatMenuOption(T("RC_UnplugShort"), allowed ? (Action)(() => Report(ImplantService.ExtractHot(pawn, module))) : null)
                    { tooltip = !hot.Allowed ? Error(hot) : (!allowed ? T("RC_Error_OperatorUnavailable") : T("RC_HotExtract")) });
            }
            var surgical = rules.Detach(module.GetUniqueLoadID(), true, false);
            var removalBill = pawn.BillStack.Bills.OfType<Bill_RemoveCBM>().FirstOrDefault(b => b.module == module);
            bool queued = removalBill != null;
            if (queued && editable) options.Add(new FloatMenuOption(T("RC_CancelSurgery"), () => pawn.BillStack.Delete(removalBill)));
            options.Add(new FloatMenuOption(T("RC_SurgeryShort"), editable && surgical.Allowed && !queued ? (Action)(() => QueueSurgery(pawn, module)) : null)
                { tooltip = queued ? T("RC_Error_AlreadyQueued") : (!surgical.Allowed ? Error(surgical) : T("RC_QueueSurgery")) });
            return options;
        }
        public static void ShowModuleGroupMenu(Pawn pawn, List<Hediff_CBMModule> modules)
        {
            CancelPlacement();
            if (modules.Count == 1) { ShowModuleMenu(pawn, modules[0]); return; }
            Find.WindowStack.Add(new FloatMenu(modules.Select((m, i) => new FloatMenuOption(
                ModuleLabel(m.def) + " #" + (i + 1)
                + (m.status == ModuleStatus.Installed ? " (" + m.x + "," + m.y + ")" : ""), () => ShowModuleMenu(pawn, m))).ToList()));
        }
        private static void ShowModuleMenu(Pawn pawn, Hediff_CBMModule module)
        {
            CancelPlacement();
            if (current != pawn) { current = pawn; gridScroll = pendingScroll = Vector2.zero; }
            part = module.Part; boardId = module.boardId; selected = module.GetUniqueLoadID();
            Find.WindowStack.Add(new FloatMenu(ModuleOptions(pawn, module)));
        }

        private static void DrawPending(Rect rect, Pawn pawn, GridRules rules)
        {
            var pending = ImplantService.Modules(pawn).Where(m => m.Part == part && (m.status == ModuleStatus.SurgeryPrepared || m.retainedRemnant)).ToList();
            if (Widgets.ButtonText(new Rect(rect.xMax - 23, rect.y, 23, 25), "+")) ShowInstallMenu(pawn, part);
            Widgets.Label(new Rect(rect.x, rect.y + 3f, rect.width - 25, 25f), T(pending.Any(m => m.retainedRemnant) ? "RC_PendingRemnants" : "RC_PendingShort") + " " + pending.Count);
            var viewport = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 36f);
            bool overViewport = viewport.Contains(Event.current.mousePosition);
            float width = viewport.width - 18f;
            var carried = new List<KeyValuePair<Thing, HediffDef>>();
            if (carriedDefs == null) carriedDefs = DefDatabase<HediffDef>.AllDefsListForReading
                .Where(d => d.GetModExtension<CBMExtension>()?.hotSwappable == true && d.GetModExtension<CBMExtension>().item != null)
                .Where(d => CBMItems.Implant(d.GetModExtension<CBMExtension>().item) == d).ToDictionary(d => d.GetModExtension<CBMExtension>().item);
            if (pawn.inventory != null)
                foreach (var item in pawn.inventory.innerContainer)
                {
                    if (carriedDefs.TryGetValue(item.def, out var def)) carried.Add(new KeyValuePair<Thing, HediffDef>(item, def));
                }
            float contentHeight = pending.Count * 84f + (carried.Count > 0 ? 28f + carried.Count * 64f : 0f);
            Widgets.BeginScrollView(viewport, ref pendingScroll, new Rect(0, 0, width, Mathf.Max(viewport.height, contentHeight)));
            for (int i = 0; i < pending.Count; i++)
            {
                var h = pending[i];
                var row = new Rect(0, i * 84f, width, 78f);
                Widgets.DrawMenuSection(row);
                Widgets.DrawHighlightIfMouseover(row);
                float shapeWidth = Mathf.Min(width - 12f, rules.Width(h.State()) * 18f);
                float shapeHeight = Mathf.Min(40f, rules.Height(h.State()) * 18f);
                var shape = new Rect((width - shapeWidth) * .5f, row.y + 6f, shapeWidth, shapeHeight);
                Widgets.DrawBoxSolid(shape, CBMColors.Module(rules.Specs[h.def.defName]));
                Widgets.DrawBox(shape);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0, row.y + 48f, width, 14f), h.retainedRemnant ? T("RC_Remnant") : rules.Width(h.State()) + "×" + rules.Height(h.State()));
                Widgets.Label(new Rect(3f, row.y + 62f, width - 6f, 16f), ModuleLabel(h.def).Truncate(width - 6f));
                Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small;
                GridDrawing.Solid(new Rect(row.x + 4, row.y + 4, 4, 4), CBMColors.ModuleIndicator(rules.Specs[h.def.defName], h));
                TooltipHandler.TipRegion(row, h.def.LabelCap + "\n" + h.def.description + "\n" + CBMConditionUtility.Label(h.condition, h.durability));
                if (overViewport && row.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
                { ShowModuleMenu(pawn, h); Event.current.Use(); }
                if (overViewport && row.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    selected = h.GetUniqueLoadID(); CancelPlacement();
                    if (ImplantService.CanEdit(pawn) && rules.CanStartPlacement(h.GetUniqueLoadID()).Allowed)
                    { placing = h.GetUniqueLoadID(); rotated = h.rotated; dragOffsetX = dragOffsetY = 0; }
                    Event.current.Use();
                }
            }
            if (carried.Count > 0) Widgets.Label(new Rect(0, pending.Count * 84f, width, 26f), T("RC_Carried"));
            for (int i = 0; i < carried.Count; i++)
            {
                var item = carried[i].Key; var def = carried[i].Value;
                var row = new Rect(0, pending.Count * 84f + 28f + i * 64f, width, 58f);
                Widgets.DrawMenuSection(row);
                Widgets.ThingIcon(new Rect(row.center.x - 12f, row.y + 3f, 24f, 24f), item);
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(2, row.y + 32f, width - 4f, 22f), (ModuleLabel(def) + " ×" + item.stackCount).Truncate(width - 4f));
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(row, item.LabelCap + "\n" + (item.TryGetComp<CompCBMCondition>()?.CompInspectStringExtra() ?? ""));
                if (overViewport && Mouse.IsOver(row) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
                {
                    var check = rules.CanAdd(new ModuleState { SpecId = def.defName, RootId = ImplantService.RootId(pawn, part) });
                    var conditionCheck = CBMConditionUtility.CanInstall(item);
                    if (!conditionCheck.Allowed) check = conditionCheck;
                    bool allowed = ImplantService.CanHotSwap(pawn) && check.Allowed;
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                        new FloatMenuOption(T("RC_HotInsertShort"), allowed ? (Action)(() => {
                            CancelPlacement(); externalItem = item; externalDef = def; rotated = false;
                        }) : null) { tooltip = !check.Allowed ? Error(check) : (allowed ? "" : T("RC_Error_OperatorUnavailable")) }
                    }));
                    Event.current.Use();
                }
            }
            Widgets.EndScrollView();
        }

        private static void DrawGrids(Rect viewport, Pawn pawn, GridRules rules, List<Board> boards)
        {
            int maxWidth = boards.Max(b => Math.Max(b.Width, rules.Modules.Where(m => m.Status == ModuleStatus.Installed && m.BoardId == b.Id)
                .Select(m => m.X + rules.Width(m)).DefaultIfEmpty(0).Max()));
            float size = Mathf.Floor(Mathf.Clamp((viewport.width - 18f) / maxWidth, 32f, 52f) * Prefs.UIScale) / Prefs.UIScale;
            var heights = boards.Select(b => Math.Max(b.Height, rules.Modules.Where(m => m.Status == ModuleStatus.Installed && m.BoardId == b.Id)
                .Select(m => m.Y + rules.Height(m)).DefaultIfEmpty(0).Max())).ToList();
            float contentHeight = heights.Sum() * size + Math.Max(0, boards.Count - 1) * 24f;
            var view = new Rect(0, 0, Mathf.Max(viewport.width - 18f, maxWidth * size), Mathf.Max(viewport.height, contentHeight));
            if (focusBoard != null)
            {
                int index = boards.FindIndex(b => b.Id == focusBoard);
                if (index >= 0)
                {
                    float target = heights.Take(index).Sum() * size + index * 24f;
                    var module = rules.Modules.FirstOrDefault(m => m.Id == selected);
                    if (module != null) target += Math.Max(0, module.Y) * size;
                    gridScroll.y = Mathf.Clamp(target, 0f, Mathf.Max(0f, contentHeight - viewport.height));
                }
                focusBoard = null;
            }
            bool over = Mouse.IsOver(viewport);
            gridScroll = new Vector2(GridDrawing.Snap(gridScroll.x), GridDrawing.Snap(gridScroll.y));
            Widgets.BeginScrollView(viewport, ref gridScroll, view);
            float top = 0f;
            for (int i = 0; i < boards.Count; i++)
            {
                var board = boards[i];
                float left = GridDrawing.Snap(Mathf.Max(0, (view.width - board.Width * size) * .5f));
                var bounds = new Rect(0, top, view.width, heights[i] * size);
                // Draw only boards intersecting the viewport, but keep one stable content height.
                if (top + heights[i] * size >= gridScroll.y && top <= gridScroll.y + viewport.height)
                    DrawBoard(pawn, rules, board, left, GridDrawing.Snap(top), size, over && bounds.Contains(Event.current.mousePosition));
                top += heights[i] * size + 24f;
            }
            Widgets.EndScrollView();
        }

        private static void DrawBoard(Pawn pawn, GridRules rules, Board board, float ox, float oy, float cellSize, bool overViewport)
        {
            bool dragging = placing != null || externalItem != null;
            var installed = rules.Modules.Where(m => m.Status == ModuleStatus.Installed && m.BoardId == board.Id).ToList();
            foreach (var pair in board.Cells)
            {
                var xy = pair.Key.Split(':'); int x = int.Parse(xy[0]), y = int.Parse(xy[1]);
                var cell = new Rect(ox + x * cellSize, oy + y * cellSize, cellSize, cellSize).ContractedBy(GridDrawing.Pixel);
                GridDrawing.Solid(cell, CBMColors.Cell(pair.Value));
                var dotColor = CBMColors.CellIndicator(pair.Value);
                if (dotColor.a > 0f) GridDrawing.Solid(new Rect(cell.x + 3, cell.y + 3, 4, 4), dotColor);
                if (!dragging) TooltipHandler.TipRegion(cell, T(pair.Value.Hot ? "RC_HotCell" : "RC_FixedCell") + "\n"
                    + T("RC_CellTags") + ": " + string.Join(", ", pair.Value.Tags.OrderBy(t => t)));
            }
            ModuleState hovered = null;
            foreach (var m in installed)
            {
                var block = new Rect(ox + m.X * cellSize, oy + m.Y * cellSize, rules.Width(m) * cellSize, rules.Height(m) * cellSize).ContractedBy(GridDrawing.Pixel);
                var fill = CBMColors.Module(rules.Specs[m.SpecId]);
                GridDrawing.Solid(block, m.Id == selected ? Color.Lerp(fill, Color.white, .12f) : fill);
                GridDrawing.Border(block, m.Id == selected ? Color.white : new Color(.70f, .74f, .78f));
                var def = DefDatabase<HediffDef>.GetNamed(m.SpecId);
                float iconSize = Mathf.Max(8f, Mathf.Min(24f, Mathf.Min(block.width - 8f, block.height - 24f)));
                Widgets.ThingIcon(new Rect(block.center.x - iconSize * .5f, block.y + 3f, iconSize, iconSize), def.GetModExtension<CBMExtension>().item);
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter;
                var labelRect = new Rect(block.x + 2, block.y + iconSize + 5, block.width - 4, block.height - iconSize - 15);
                // Short footprints still carry a name; full name remains available on hover.
                if (labelRect.height < 15) labelRect = new Rect(block.x + 2, block.yMax - 24, block.width - 4, 15);
                Widgets.Label(labelRect, FitLabel(ModuleLabel(def), labelRect));
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
                var runtime = ImplantService.Modules(pawn).First(h => h.GetUniqueLoadID() == m.Id);
                GridDrawing.Solid(new Rect(block.x + 3, block.y + 3, 4, 4), CBMColors.ModuleIndicator(rules.Specs[m.SpecId], runtime));
                var durabilityRect = new Rect(block.x + 3, block.yMax - 6, block.width - 6, 3);
                GridDrawing.Solid(durabilityRect, new Color(.12f, .14f, .15f));
                GridDrawing.Solid(new Rect(durabilityRect.x, durabilityRect.y, durabilityRect.width * Mathf.Clamp01(runtime.durability), durabilityRect.height), runtime.durability < 1f ? new Color(.70f, .56f, .32f) : new Color(.66f, .82f, .70f));
                if (!dragging) TooltipHandler.TipRegion(block, def.LabelCap + "\n" + def.description + "\n" + CBMConditionUtility.Label(runtime.condition, runtime.durability) + " · " + runtime.durability.ToStringPercent());
                if (m.BurntOut) GridDrawing.Border(block.ContractedBy(GridDrawing.Pixel * 2), new Color(.55f, .27f, .23f));
                if (overViewport && block.Contains(Event.current.mousePosition)) hovered = m;
            }
            foreach (var target in rules.Reservations.Where(r => r.BoardId == board.Id))
            {
                var reservation = new Rect(ox + target.X * cellSize, oy + target.Y * cellSize,
                    rules.Width(target) * cellSize, rules.Height(target) * cellSize).ContractedBy(GridDrawing.Pixel * 2);
                GridDrawing.Border(reservation, new Color(.7f, .6f, .35f));
                if (!dragging) TooltipHandler.TipRegion(reservation, T("RC_MoveTarget"));
            }
            int gx = Mathf.FloorToInt((Event.current.mousePosition.x - ox) / cellSize);
            int gy = Mathf.FloorToInt((Event.current.mousePosition.y - oy) / cellSize);
            bool overGrid = overViewport && board.At(gx, gy) != null;
            if (placing != null || externalItem != null)
            {
                var region = PreviewRegion(rules, board);
                DrawBoundary(region.Cells, ox, oy, cellSize);
            }
            if ((placing != null || externalItem != null) && overViewport)
            {
                var preview = PlacementState(rules, board);
                int tx = gx - dragOffsetX, ty = gy - dragOffsetY;
                var result = PreviewPlacement(rules, board, tx, ty);
                var ghost = new Rect(ox + tx * cellSize, oy + ty * cellSize, rules.Width(preview) * cellSize, rules.Height(preview) * cellSize).ContractedBy(GridDrawing.Pixel);
                GridDrawing.Solid(ghost, result.Allowed ? new Color(.3f, .75f, .5f, .4f) : new Color(.85f, .3f, .25f, .4f));
                if (Event.current.button == 0 && Event.current.type == (externalItem != null ? EventType.MouseDown : EventType.MouseUp))
                {
                    Report(externalItem != null ? ImplantService.InsertHot(pawn, part, externalItem, externalDef, board.Id, tx, ty, rotated)
                        : CBMRelocation.Queue(pawn, placing, board.Id, tx, ty, rotated));
                    Event.current.Use();
                }
            }
            if (overViewport && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hovered != null)
            {
                selected = hovered.Id; CancelPlacement();
                if (ImplantService.CanEdit(pawn) && rules.CanStartPlacement(hovered.Id, ImplantService.CanHotSwap(pawn)).Allowed)
                { placing = hovered.Id; rotated = hovered.Rotated; dragOffsetX = gx - hovered.X; dragOffsetY = gy - hovered.Y; }
                Event.current.Use();
            }
            if (overViewport && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                if (placing != null || externalItem != null) CancelPlacement();
                else if (hovered != null) ShowModuleMenu(pawn, ImplantService.Modules(pawn).First(m => m.GetUniqueLoadID() == hovered.Id));
                Event.current.Use();
            }
        }

        private static ModuleState PlacementState(GridRules rules, Board board)
        {
            var state = externalItem != null ? new ModuleState { Id = "external-preview", SpecId = externalDef.defName, RootId = board.RootId }
                : rules.Modules.First(m => m.Id == placing).Copy();
            if (externalItem != null)
            {
                var comp = externalItem.TryGetComp<CompCBMCondition>();
                if (comp != null) { state.Condition = comp.condition; state.Durability = comp.durability; }
            }
            state.Rotated = rotated; return state;
        }
        private static PlacementPreview.Region PreviewRegion(GridRules rules, Board board)
        {
            string key = externalItem != null ? externalItem.GetUniqueLoadID() + externalDef.defName : placing;
            if (previewCache == null || previewRules != rules || previewId != key || previewRotation != rotated)
            {
                previewRules = rules; previewId = key; previewRotation = rotated;
                previewCache = new PlacementPreview(rules, placing, rotated, externalItem != null ? PlacementState(rules, board) : null);
            }
            return previewCache.For(board);
        }
        private static RuleResult PreviewPlacement(GridRules rules, Board board, int x, int y) => PreviewRegion(rules, board).At(x, y);

        private static void DrawBoundary(HashSet<string> cells, float ox, float oy, float size)
        {
            foreach (var key in cells)
            {
                var xy = key.Split(':'); int x = int.Parse(xy[0]), y = int.Parse(xy[1]);
                float left = ox + x * size, top = oy + y * size, pixel = GridDrawing.Pixel;
                if (!cells.Contains(RootSpec.Key(x - 1, y))) GridDrawing.Solid(new Rect(left, top, pixel, size), Color.white);
                if (!cells.Contains(RootSpec.Key(x + 1, y))) GridDrawing.Solid(new Rect(left + size - pixel, top, pixel, size), Color.white);
                if (!cells.Contains(RootSpec.Key(x, y - 1))) GridDrawing.Solid(new Rect(left, top, size, pixel), Color.white);
                if (!cells.Contains(RootSpec.Key(x, y + 1))) GridDrawing.Solid(new Rect(left, top + size - pixel, size, pixel), Color.white);
            }
        }

        private static void ShowInstallMenu(Pawn pawn, BodyPartRecord part)
        {
            var options = DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.GetModExtension<CBMExtension>() != null)
                .Select(d => { var allowed = CBMSurgery.CanPrepare(pawn, part, d); return new FloatMenuOption(d.LabelCap,
                    allowed.Allowed ? (Action)(() => Report(CBMSurgery.QueueInstall(pawn, part, d))) : null) { tooltip = Error(allowed) }; }).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private static void QueueSurgery(Pawn pawn, Hediff_CBMModule module) => Report(CBMSurgery.QueueRemoval(pawn, module));
    }
}
