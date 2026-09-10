"""Regenerate prototype Defs and translations; no installed game files are changed."""
from pathlib import Path
from xml.etree.ElementTree import Element, SubElement, indent, ElementTree, tostring

import argparse
import tempfile

REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--check", action="store_true", help="Check generated files without modifying the repository")
args = parser.parse_args()
_scratch = tempfile.TemporaryDirectory(prefix="rimcbm-defs-") if args.check else None
ROOT = Path(_scratch.name) if _scratch else REPOSITORY_ROOT

def node(parent, name, value=None, **attrs):
    result = SubElement(parent, name, attrs)
    if value is not None: result.text = str(value)
    return result

def save(root, path):
    # Display text only: keep DefNames, type names and save identifiers stable.
    import re
    for element in root.iter():
        if element.text and (element.tag in ('label', 'description', 'jobString', 'reportString') or '.' in element.tag or element.tag.startswith('RC_')):
            if 'ChineseSimplified' in path:
                element.text = re.sub(r'\bCBMs?\b', '植入体', element.text)
            else:
                element.text = re.sub(r'\bCBMs\b', 'implants', element.text)
                element.text = re.sub(r'\bCBM\b', 'implant', element.text)
    dest = ROOT / path
    dest.parent.mkdir(parents=True, exist_ok=True)
    indent(root, space="  ")
    dest.write_bytes(tostring(root, encoding="utf-8", xml_declaration=True) + b"\n")

modules = [
    ("FuelGenerator", "fuel generator prototype", "燃料发电测试机", 1, 2, True, "None", False),
    ("TargetRig", "CBM targeting test suite", "CBM 目标能力测试套件", 2, 2, True, "None", False),
    ("AbilityRig", "CBM ability test suite", "CBM 能力测试套件", 2, 2, True, "None", False),
    ("Battery", "CBM energy storage", "CBM 储能单元", 1, 2, True, "None", False),
    ("Metabolic", "CBM metabolic generator", "CBM 代谢发电机", 1, 2, False, "None", False),
    ("Overdrive", "CBM motor overdrive", "CBM 运动强化器", 1, 2, True, "None", False),
    ("Placeholder", "CBM test placeholder", "CBM 测试占位块", 1, 2, True, "None", False),
    ("Interface", "CBM interface prototype", "CBM 接口原型", 1, 2, True, "None", False),
    ("Controller", "CBM fixed controller prototype", "CBM 固定控制器原型", 2, 2, False, "None", False),
    ("Row", "CBM row extender", "CBM 增行器", 1, 2, True, "Row", False),
    ("Column", "CBM column extender", "CBM 增列器", 2, 2, True, "Column", False),
    ("Bay", "CBM hot-swap expansion bay", "CBM 热插拔扩展板", 2, 2, True, "Board", True),
]
economy = {
    "FuelGenerator": (1.2, 1400, 0, []),
    "Placeholder": (.4, 100, 0, []), "Interface": (.3, 250, 0, []),
    "Controller": (.8, 800, 0, []), "AbilityRig": (1.5, 2400, 0, []), "TargetRig": (1.8, 2800, 0, []),
    "Battery": (1.5, 600, 5, ["Utility", "Combat"]), "Metabolic": (.8, 1400, 2, ["Utility"]),
    "Overdrive": (1.2, 1800, 2, ["Combat"]), "Row": (.6, 1000, 3, ["Utility"]),
    "Column": (.8, 1200, 2, ["Utility"]), "Bay": (1.2, 2200, 1, ["Utility"]),
}
defs = Element("Defs")
zh_hediffs, zh_items, zh_recipes = Element("LanguageData"), Element("LanguageData"), Element("LanguageData")
for short, label, zh, width, height, hot, expansion, granted_hot in modules:
    label, zh = label.removeprefix("CBM "), zh.removeprefix("CBM ")
    key = "RimCBM_" + short
    item = node(defs, "ThingDef", ParentName="BodyPartBase")
    node(item, "defName", key + "Item"); node(item, "label", label)
    desc = f"RimCBM grid prototype: {width} x {height}. " + ("Provides additional grid space." if expansion != "None" else "Tests installation and layout only; no energy or combat effect yet.")
    zh_desc = f"RimCBM 槽位原型：{width}×{height}。" + ("提供额外的槽位空间。" if expansion != "None" else "用于验证安装与布局，尚无能源或战斗效果。")
    if short == "FuelGenerator": desc, zh_desc = "Refillable chemical-fuel generator. Stores 20 units and produces up to 120 W at 80% conversion efficiency.", "可补充化合燃料的发电测试机。储存 20 单位燃料，最高输出 120 W，转换效率 80%。"
    if short == "TargetRig": desc, zh_desc = "Targeted electric attack and close-range treatment. Right-click a target to use.", "提供对敌电击与近距离治疗能力，通过右键目标使用。"
    if short == "AbilityRig": desc, zh_desc = "Four independent abilities: passive mobility, sustained assistance, a timed burst and automatic injury repair.", "包含独立的被动移动辅助、持续动力、单次爆发和受伤自动修复，用于测试四类能力。"
    if short == "Battery": desc, zh_desc = "Stores 100 Wd in the shared CBM energy pool.", "为角色共享能源池提供 100 Wd 储能容量。"
    if short == "Metabolic": desc, zh_desc = "Generates 60 W using nutrition. Stops below 20% food or when storage is full. EMP-sensitive.", "消耗营养产生 60 W 电力，饱食度低于 20% 或储能已满时停止。受 EMP 影响。"
    if short == "Overdrive": desc, zh_desc = "Consumes 100 W, costs 0.5 Wd to start. Adds movement speed; a 5 Wd burst lasts 10 seconds with a 20-second cooldown. EMP-sensitive.", "持续耗能 100 W，启动消耗 0.5 Wd。提高移动速度；单次强化消耗 5 Wd，持续 10 秒、冷却 20 秒。受 EMP 影响。"
    node(item, "description", desc); node(item, "techLevel", "Spacer")
    node(node(item, "thingCategories"), "li", "BodyPartsBionic")
    graphic = node(item, "graphicData"); node(graphic, "texPath", "Things/Item/Health/HealthItem"); node(graphic, "graphicClass", "Graphic_Single")
    mass, value, weight, pools = economy[short]
    stats = node(item, "statBases"); node(stats, "Mass", mass); node(stats, "MarketValue", value)
    # Replace inherited TechHediff: vanilla's broad generator must not bypass this stock policy.
    node(node(item, "tradeTags", Inherit="False"), "li", "RimCBM_Implant")
    trade = node(node(item, "modExtensions"), "li", Class="RimCBM.CBMTradeExtension")
    node(trade, "weight", weight)
    for pool in pools: node(node(trade, "pools") if trade.find("pools") is None else trade.find("pools"), "li", pool)
    node(node(item, "comps"), "li", Class="RimCBM.CompProperties_CBMCondition")
    h = node(defs, "HediffDef", ParentName="ImplantHediffBase")
    node(h, "defName", key); node(h, "label", label); node(h, "description", desc); node(h, "hediffClass", "RimCBM.Hediff_CBMModule")
    node(h, "everCurableByItem", "false"); node(h, "priceOffset", value)
    node(h, "keepOnBodyPartRestoration", "true")
    ext = node(node(h, "modExtensions"), "li", Class="RimCBM.CBMExtension")
    for field, value in dict(width=width, height=height, hotSwappable=str(hot).lower(), expansion=expansion,
                             item=key + "Item").items(): node(ext, field, value)
    factor = {"FuelGenerator": .9, "Placeholder": 1, "Battery": .95, "Interface": .95, "Metabolic": .9,
              "Overdrive": .85, "AbilityRig": .8, "TargetRig": .8, "Controller": .8,
              "Row": .9, "Column": .9, "Bay": .75}[short]
    for profile, multiplier in [("installationSurgery", factor), ("removalSurgery", min(1, factor + .05))]:
        profile_node = node(ext, profile)
        for field, value in dict(successFactor=multiplier, severeChance=.2, burnoutChance=.05, retainedChance=.15 if profile == "installationSurgery" else 0,
            minorDamage="0.15~0.3", severeDamage="0.45~0.7", minorPatientDamage=3,
            severePatientDamage=12, burnoutPatientDamage=25).items(): node(profile_node, field, value)
    combat = node(ext, "combat")
    node(combat, "hitChance", .4 if expansion != "None" else .3)
    node(combat, "hitPoints", 30 if expansion != "None" else 50 if short == "Battery" else 40)
    node(combat, "hitWeight", 1)
    node(combat, "damageMultiplier", 1)
    node(combat, "fireDamageMultiplier", 1)
    if short in ("Battery", "Metabolic", "FuelGenerator"):
        fault = node(node(ext, "faults"), "li")
        for field, value in dict(id="shortCircuit", labelKey="RC_FaultDischarge", maxDurability=.75, chance=.25, intervalTicks="600~1800", damageAmount=2, energyWd=.5, requiresEnergy="true", affectsBurnt="true", requiresActive="false", workerClass="RimCBM.CBMFaultWorker_Discharge").items(): node(fault, field, value)
    damage_types = node(combat, "damageFactors")
    for damage_type, factor in [("Burn", 1.5), ("Blunt", 1), ("Cut", .8)]:
        row = node(damage_types, "li"); node(row, "damageDef", damage_type); node(row, "multiplier", factor)
    if short == "Battery": node(ext, "chargeAcceptanceWatts", 200)
    if short == "FuelGenerator":
        node(ext, "defaultMode", "standard")
        modes = node(ext, "modes")
        for mode_id, label_key, generation, efficiency_cost, research_id in [("standard", "RC_ModeStandard", 1, 1, None), ("economy", "RC_ModeEconomy", .5, 1, "RimCBM_EnergyControl")]:
            mode = node(modes, "li"); node(mode, "id", mode_id); node(mode, "labelKey", label_key)
            node(mode, "generationMultiplier", generation)
            if research_id: node(node(mode, "research"), "li", research_id)
        fuel = node(ext, "fuel")
        node(fuel, "capacity", 20); node(fuel, "efficiency", .8); node(fuel, "refillTicks", 180)
        opt = node(node(fuel, "options"), "li")
        node(opt, "item", "Chemfuel"); node(opt, "unitsPerItem", 1); node(opt, "energyWdPerUnit", 10)
    power = {
        "FuelGenerator": dict(generationWatts=120),
        "Battery": dict(energyCapacityWd=100),
        "Metabolic": dict(generationWatts=60, nutritionPerWd=.004),
        "Overdrive": dict(idleWatts=100, activationWd=.5, useWd=5, useDurationTicks=600, useCooldownTicks=1200),
    }
    for field, value in power.get(short, {}).items(): node(ext, field, value)
    if short in ("Metabolic", "Overdrive", "AbilityRig", "TargetRig", "FuelGenerator"): node(node(ext, "tags"), "li", "EMPSensitive")
    if short == "Overdrive":
        node(node(ext, "statOffsets"), "MoveSpeed", .15)
        node(node(ext, "burstStatOffsets"), "MoveSpeed", .8)
    if short == "TargetRig":
        abilities = node(ext, "abilities")
        for aid, kinds, values in [
            ("shock", ["HostilePawn"], dict(range=12, useWd=2, damageAmount=8, damageDef="Burn")),
            ("treat", ["Self", "FriendlyPawn"], dict(range=1.5, useWd=1, healAmount=4)),
        ]:
            a = node(abilities, "li")
            for field, value in dict(id=aid, kind="OneShot", labelKey="RC_AbilityName_" + aid, cooldownTicks=300, **values).items(): node(a, field, value)
            targets = node(a, "targetKinds", Inherit="False")
            for kind in kinds: node(targets, "li", kind)
    if short == "AbilityRig":
        abilities = node(ext, "abilities")
        for aid, kind, values, stat in [
            ("passive", "Passive", {}, .05),
            ("assist", "Toggle", dict(defaultEnabled="false", idleWatts=40, activationWd=.2), .2),
            ("burst", "OneShot", dict(useWd=2, durationTicks=180, cooldownTicks=600), .8),
            ("repair", "Reactive", dict(useWd=1, cooldownTicks=300, reserveWd=5, trigger="DamageTaken", minimumDamage=1, healAmount=3), None),
        ]:
            a = node(abilities, "li")
            for field, value in dict(id=aid, kind=kind, labelKey="RC_AbilityName_" + aid, **values).items(): node(a, field, value)
            if stat is not None: node(node(a, "statOffsets"), "MoveSpeed", stat)
    node(ext, "shortLabelKey", "RC_Name_" + short)
    if expansion != "None" or short == "Controller":
        limit = node(node(ext, "exclusions"), "li")
        node(limit, "scope", "Pawn" if short == "Controller" else "Part")
        node(node(limit, "modules"), "li", key)
    if expansion != "None":
        node(node(ext, "tags"), "li", "Expansion")
        node(ext, "type", "Expansion")
        # Added cells select their style by tags; HotSwap takes priority over Expansion.
    granted_tags = (["Expansion"] if expansion != "None" else []) + (["HotSwap"] if granted_hot else [])
    if granted_tags:
        values = node(ext, "grantedCellTags")
        for value in granted_tags: node(values, "li", value)
    r = node(defs, "RecipeDef", ParentName="SurgeryInstallImplantBase")
    node(r, "defName", key + "Prepare"); node(r, "label", "prepare " + label); node(r, "description", "Surgically prepare the CBM, then place it using the health panel.")
    node(r, "jobString", "Preparing CBM."); node(r, "workerClass", "RimCBM.Recipe_PrepareCBM"); node(r, "addsHediff", key)
    node(r, "humanlikeOnly", "true")
    node(r, "surgeryOutcomeEffect", "RimCBM_SurgeryOutcome")
    ingredient = node(node(r, "ingredients"), "li"); node(node(node(ingredient, "filter"), "thingDefs"), "li", key + "Item"); node(ingredient, "count", 1)
    node(node(node(r, "fixedIngredientFilter"), "thingDefs"), "li", key + "Item")
    for target, prefix in [(zh_items, key + "Item"), (zh_hediffs, key)]:
        node(target, prefix + ".label", zh); node(target, prefix + ".description", zh_desc)
    node(zh_recipes, key + "Prepare.label", "植入准备：" + zh)
    node(zh_recipes, key + "Prepare.description", "手术将 CBM 加入此部位的待安装列表，随后在健康面板中拖入格子。")
    node(zh_recipes, key + "Prepare.jobString", "正在准备 CBM。")

r = node(defs, "RecipeDef", ParentName="SurgeryRemoveImplantBase")
node(r, "defName", "RimCBM_Remove"); node(r, "label", "extract selected CBM"); node(r, "description", "Extract the exact module selected in the CBM panel.")
node(r, "workerClass", "RimCBM.Recipe_RemoveCBM"); node(r, "jobString", "Extracting CBM.")
# Do not expose an unbound instance-removal recipe in the vanilla operation picker.
node(r, "surgeryOutcomeEffect", "RimCBM_SurgeryOutcome")
node(r, "recipeUsers", Inherit="False"); node(r, "isViolation", "false")
node(zh_recipes, "RimCBM_Remove.label", "取出指定 CBM"); node(zh_recipes, "RimCBM_Remove.description", "取出 CBM 面板中选定的具体植入物。"); node(zh_recipes, "RimCBM_Remove.jobString", "正在取出 CBM。")
outcome = node(defs, "SurgeryOutcomeEffectDef", ParentName="SurgeryOutcomeBase")
node(outcome, "defName", "RimCBM_SurgeryOutcome")
results = node(outcome, "outcomes", Inherit="False")
node(results, "li", Class="SurgeryOutcomeSuccess")
failed = node(results, "li", Class="SurgeryOutcome_Failure")
node(failed, "failure", "true"); node(failed, "chance", 1); node(failed, "totalDamage", 0)
save(defs, "Defs/CBM_Prototypes.xml")
save(zh_items, "Languages/ChineseSimplified/DefInjected/ThingDef/CBM.xml")
save(zh_hediffs, "Languages/ChineseSimplified/DefInjected/HediffDef/CBM.xml")
save(zh_recipes, "Languages/ChineseSimplified/DefInjected/RecipeDef/CBM.xml")

profiles = Element("Defs")
for name, part, width, height, replacement in [
    ("Torso", "Torso", 3, 4, None), ("Head", "Head", 2, 2, None), ("Arm", "Arm", 2, 3, None),
    ("Leg", "Leg", 2, 3, None), ("Hand", "Hand", 2, 2, None), ("Foot", "Foot", 2, 2, None), ("BionicArm", "Arm", 3, 6, "BionicArm"), ("BionicLeg", "Leg", 3, 6, "BionicLeg")]:
    p = node(profiles, "RimCBM.CBMGridProfileDef")
    node(p, "defName", "RimCBM_Grid" + name); node(p, "bodyPart", part); node(p, "width", width); node(p, "height", height)
    node(node(p, "hostTags"), "li", "BodyPart." + part)
    node(node(p, "defaultCellTags"), "li", "CoreBody")
    if replacement:
        node(p, "replacement", replacement)
        entries = node(p, "cellTags")
        for y in range(height):
            entry = node(entries, "li"); node(entry, "x", width - 1); node(entry, "y", y)
            node(node(entry, "tags"), "li", "HotSwap")
save(profiles, "Defs/CBM_GridProfiles.xml")

research_defs = Element("Defs")
research = node(research_defs, "ResearchProjectDef")
for k, v in dict(defName="RimCBM_LayoutReconfiguration", label="implant layout reconfiguration",
    description="Reposition deployed CBMs within the same grid board. Does not allow extraction or return to the surgery-only pending list.",
    baseCost=2200, techLevel="Industrial", researchViewX=0, researchViewY=2).items(): node(research, k, v)
node(node(research, "prerequisites"), "li", "MicroelectronicsBasics")
tab = node(research_defs, "ResearchTabDef"); node(tab, "defName", "RimCBM"); node(tab, "label", "Implants")
node(research, "tab", "RimCBM")
for suffix, title, prerequisite, x, y in [("Integration", "implant integration", "MicroelectronicsBasics", 0, 0), ("EnergyControl", "implant energy control", "RimCBM_Integration", 1, 0), ("AdvancedControl", "implant mode control", "RimCBM_Integration", 1, 2), ("HostAdaptation", "implant host adaptation", "RimCBM_AdvancedControl", 2, 2)]:
    project = node(research_defs, "ResearchProjectDef")
    for field, value in dict(defName="RimCBM_" + suffix, label=title, description="Research framework placeholder. Specific implant unlocks will be assigned as the content progresses.", baseCost=500, techLevel="Spacer", researchViewX=x, researchViewY=y, tab="RimCBM").items(): node(project, field, value)
    node(node(project, "prerequisites"), "li", prerequisite)
save(research_defs, "Defs/CBM_Research.xml")
zh_research = Element("LanguageData")
node(zh_research, "RimCBM_LayoutReconfiguration.label", "植入体布局重构")
node(zh_research, "RimCBM_LayoutReconfiguration.description", "允许在同一块槽位板内重新排列已部署 CBM。不会解锁拆卸，也不能退回仅由手术加入的待安装列表。")
for suffix, title in [("Integration", "植入体整合"), ("EnergyControl", "植入体能源控制"), ("AdvancedControl", "植入体模式控制"), ("HostAdaptation", "植入体宿主适配")]:
    node(zh_research, "RimCBM_" + suffix + ".label", title)
    node(zh_research, "RimCBM_" + suffix + ".description", "研究框架占位。后续随正式植入体内容分配安装、能力与模式解锁；当前不限制已有基础功能。")
save(zh_research, "Languages/ChineseSimplified/DefInjected/ResearchProjectDef/CBM.xml")
zh_tab = Element("LanguageData"); node(zh_tab, "RimCBM.label", "植入体")
save(zh_tab, "Languages/ChineseSimplified/DefInjected/ResearchTabDef/CBM.xml")

text = {
"CBMResearch": ("Required research", "研究门槛"), "Dependencies": ("Implant dependencies", "植入体依赖"),
"Compatibility": ("Host compatibility", "宿主兼容条件"), "CompatibilityBlocked": ("Dependency or host incompatible", "依赖或宿主条件不满足"),
"InstallOnly": ("Installation only", "仅安装时"), "InstallAndRun": ("Installation and operation", "安装与运行时"),
"HostRule": ("All: {0}; any: {1}; none: {2}; body size: {3}–{4}", "全部满足：{0}；任一满足：{1}；禁止：{2}；体型：{3}–{4}"),
"ModeStandard": ("Standard output", "标准输出"), "ModeEconomy": ("Low output", "低功率输出"),
"Mode": ("Mode", "运行模式"), "ModeInfo": ("Abilities: {0}\nGeneration ×{1}; consumption ×{2}; effect ×{3}\nExclusive group: {4}", "能力：{0}\n发电 ×{1}；耗能 ×{2}；效果 ×{3}\n运行互斥组：{4}"),
"Error_CBMResearchRequired": ("Required implant research is unfinished", "所需植入体研究尚未完成"),
"Error_HostIncompatible": ("Host compatibility requirements are not met", "宿主兼容条件不满足"),
"Error_MissingDependency": ("Requires an installed, intact prerequisite implant", "需要已部署且未烧毁的前置植入体"),
"Error_DependencyInUse": ("Another implant still depends on this implant", "仍有其他植入体依赖此组件"),
"Error_UnknownMode": ("Unknown implant mode", "未知运行模式"), "Error_ModeUnavailable": ("Ability unavailable in the current mode or research state", "当前模式或研究状态不允许使用该能力"),
"FireWear": ("Fire damage over time", "持续燃烧损伤"),
"Remnant": ("Retained fault", "故障残留"), "PendingRemnants": ("Pending / remnants", "待装/残留"),
"Surgery_Retained": ("damaged implant left inside", "受损植入体残留体内"),
"FaultDischarge": ("Intermittent discharge", "间歇放电"),
"Info_RetainedRisk": ("Additional failure branch: {0} leaves a severely damaged implant inside the body. It cannot operate or be placed from the pending list; extraction surgery is required.", "另有 {0} 的失败结果会将重度损坏的植入体留在体内；它无法运行，也不能作为待安装件拖放，需要手术取出。"),
"Info_Fault": ("At durability ≤ {0}: attempt every {1}–{2}s, chance {3}. Damage {4}, energy {5} Wd; needs energy {6}, needs switch on {7}, affects burnout {8}. Removal stops this fault.", "耐久 ≤ {0} 时：每 {1}–{2} 秒尝试一次，概率 {3}。伤害 {4}、耗能 {5} Wd；要求能量 {6}，要求开关开启 {7}，烧毁后仍生效 {8}。取出后停止。"),
"Info_FireWear": ("While attached fire burns: loses {0}% durability per second × fire intensity (0.25–2). Damaged and burnt implants stay inside until extracted or hot-unplugged; physical limb loss is handled separately.", "身体着火时：每秒损失 {0}% 耐久 × 火势（0.25–2）。损坏和烧毁植入体保留体内，需手术或满足条件的热拔出；部位本身丢失另行处理。"),

"Info_Combat": ("Implant damage", "受击与损坏"),
"Info_CombatText": ("Chance after this implant is selected: {0}. Structural durability: {1}. Damage multiplier: {2}.\nOnly actual post-armor injury can hit implants on that body region; at most one direct implant hit per resolved injury. Candidate weight = footprint × hitWeight ({3}).\nDurability loss = actual injury × type factor × damage multiplier / structural durability. EMP keeps its separate disruption rules.", "选中此植入体后的命中概率：{0}。耐久强度：{1}。伤害倍率：{2}。\n实际穿过护甲的部位伤害才可能命中同部位植入体，每次结算的部位伤害至多直接命中一个；候选权重＝占格面积 × hitWeight（{3}）。\n耐久损失＝实际伤害 × 类型倍率 × 伤害倍率 ÷ 耐久强度。EMP 仍使用独立停机规则。"),
"Damage_Damaged": ("On damage", "受损时"), "Damage_Burnout": ("On burnout", "首次烧毁时"),
"Damage_Ignite": ("Ignition", "起火"), "Damage_Effect": ("Special consequence", "特殊后果"),
"Info_DamageEffect": ("Trigger: {0}; chance {1}; requires switch on: {2}. Shared damage-event cooldown: {3}s. A new burnout transition bypasses this cooldown. Conditions may be limited to specific damage causes.", "触发：{0}；概率 {1}；要求开关开启：{2}。受损事件共享冷却：{3} 秒；首次烧毁不受冷却阻止。具体效果可限制损坏来源。"),

"LoadBodyScale": ("Base Mass {0} kg × BodySize {1}; burden scale ×{2} (reference {3} kg). Allowances and curves scale together; gear and implants do not increase the allowance.", "基础 Mass {0} kg × BodySize {1}；承重倍率 ×{2}（基准 {3} kg）。免罚额度与曲线同步缩放，装备和植入体不扩大额度。"),

"Load_Head": ("Head", "头部"), "Load_Hands": ("Hands", "手部"), "Load_Arms": ("Arms", "手臂"), "Load_Torso": ("Torso", "躯干"), "Load_Legs": ("Legs", "腿部"), "Load_Feet": ("Feet", "脚部"), "Load_Total": ("Total", "全身"),
"LoadPenalty": ("Implant encumbrance", "植入体累赘"),
"LoadRow": ("{0}: {1} kg (no-penalty allowance {2} kg)", "{0}：{1} kg（免罚额度 {2} kg）"),
"LoadHelp": ("Left and right share one allowance. Only implants inside the body count, including prepared or burnt units. Excess mass follows configurable curves; this does not change grid capacity or vanilla inventory load.", "左右合并共用额度。只统计体内植入体，包括待安装和烧毁者。超额重量按曲线产生惩罚，不改变格子容量和原版背包负重。"),
"LoadShort": ("Implant load {0} kg", "植入负重 {0} kg"),

"Info_ImplantMass": ("Added to body mass while surgically prepared or installed, including disabled or burnt-out units. Counts for transport loading and corpse mass. Vanilla caravan carrying capacity is separate.", "手术待安装和已部署的植入体均计入身体重量，停机或烧毁不会减重。影响运输装载和尸体重量；原版商队携带负重另行计算。"),
"Info_BasePrice": ("Base market value", "基础市场价值"),
"Info_BasePriceHelp": ("Base value of the physical implant, also used as the installed pawn value offset. Actual trade prices use vanilla negotiation, trader margins and other modifiers.", "植入体实物的基础市场价值，同时作为植入后角色价值增量。实际买卖价格仍受原版议价、商人差价等修正。"),

"Error_DirectChargingDisabled": ("Direct battery charging is unavailable.", "尚不支持直接从电池充电。"),
"PriorityHint": ("Power allocation priority: higher numbers are served first when energy is scarce. Does not change power cost or strength. Ability priority is added to its implant priority. Manual one-shot activation checks energy immediately.", "供能优先级：数字越大，能源不足时越先尝试分配。不改变耗能或效果强度。能力优先级与所属植入体优先级相加；手动单次能力立即检查并消耗能量。"),
"Info_Cleanliness": ("Cleanliness", "清洁状态"), "Info_Sterility": ("Sterility", "灭菌状态"),
"Info_Clean": ("Clean", "清洁"), "Info_Dirty": ("Dirty", "肮脏"), "Info_Sterile": ("Sterile", "已灭菌"), "Info_Unsterile": ("Unsterile", "未灭菌"),
"Info_HygieneHelp": ("Cleanliness and sterility are separate states. Cleaning removes dirt but leaves the implant unsterile. Sterilization requires a clean implant. Repair breaks sterility. Installation requires both clean and sterile. Sterile packaging is not simulated yet.", "清洁与灭菌分别记录。清洁去除污物，但仍为未灭菌；灭菌要求先清洁。维修会破坏无菌状态。安装必须同时满足清洁、已灭菌。目前尚未模拟无菌包装。"),

"Info_InstallRisk": ("Installation failure", "安装失败后果"), "Info_RemoveRisk": ("Extraction failure", "取出失败后果"),

"Expansion_None": ("None", "无"), "Expansion_Row": ("Rows", "行"), "Expansion_Column": ("Columns", "列"), "Expansion_Board": ("Separate bay", "独立槽板"),
"Info_TagEffect": ("Consumption multiplier {0}; effect multiplier {1}. Values above are base values.", "耗能倍率 {0}；效果倍率 {1}。上面的功能数值为基础值。"),

"Target_Self": ("Self", "自身"), "Target_Pawn": ("Pawn", "角色"), "Target_FriendlyPawn": ("Friendly pawn", "友方角色"), "Target_HostilePawn": ("Hostile pawn", "敌对角色"), "Target_Thing": ("Thing", "物品"),
"Ability_Passive": ("Passive", "被动"), "Ability_Toggle": ("Toggle", "持续开关"), "Ability_OneShot": ("One-shot", "单次"), "Ability_Reactive": ("Reactive", "反应式"),
"Scope_Part": ("Body part exclusion", "部位互斥"), "Scope_Pawn": ("Global exclusion", "全局互斥"),
"Info_Reactive": ("Threshold {0}; minimum damage {1}; energy reserve {2} Wd", "阈值 {0}；最低触发伤害 {1}；保留能量 {2} Wd"),
"Info_Cooldown": ("Remaining cooldown: {0}s", "剩余冷却：{0} 秒"),
"Info_Restrictions": ("Requires all: {0}\nRequires any: {1}\nForbidden: {2}", "必须全部满足：{0}\n至少满足一项：{1}\n禁止标签：{2}"),
"Info_EMP": ("EMP temporarily disables this implant.", "EMP 会使此植入体暂时停机。"),

"Implants": ("Implants", "植入体"),
"Surgery_Minor": ("minor damage", "轻度损坏"), "Surgery_Severe": ("severe damage", "重度损坏"), "Surgery_Burnout": ("burnout", "烧毁"),
"SurgeryFailureMessage": ("{0}: surgery on {1} failed ({2}).", "{0} 的 {1} 手术失败：{2}。"),
"Info_Condition": ("Condition", "当前状态"), "Info_Layout": ("Installation layout", "安装布局"),
"Info_Power": ("Energy", "能源"), "Info_Install": ("Installation success ceiling", "安装成功率上限"),
"Info_Remove": ("Extraction success ceiling", "取出成功率上限"),
"Info_Risk": ("Failure consequences", "失败后果"), "Info_Tags": ("Tags and restrictions", "标签与限制"),
"Info_ConditionHelp": ("Damage reduces effects and surgical success. Burnout prevents activation and ordinary repair. Dirty or unsterilized items must be serviced before installation.", "损坏会降低效果和手术成功率。烧毁后无法启动，普通维修无法恢复。肮脏或未灭菌的物品必须处理后才能手术安装。"),
"Info_LayoutText": ("Footprint: {0} × {1}\nRotatable: {2}\nHot-plug capable: {3}\nExpansion: {4}", "占用：{0} × {1}\n允许旋转：{2}\n支持热插拔：{3}\n扩容：{4}"),
"Info_PowerText": ("Storage: {0} Wd\nGeneration: {1} W\nUpkeep: {2} W\nStartup: {3} Wd\nActivation: {4} Wd", "储能：{0} Wd\n发电：{1} W\n持续耗能：{2} W\n启动耗能：{3} Wd\n单次耗能：{4} Wd"),
"Info_ChanceText": ("This is a ceiling, not a guaranteed rate. Actual chance = vanilla surgery quality (doctor, medicine, bed/environment, inspiration; capped at 98%) × implant factor {0} × condition factor {1}. Current durability: {2}.", "这是上限，不是固定成功率。实际成功率＝原版手术质量（医生、药品、床位/环境、灵感，上限 98%）× 植入体系数 {0} × 状态系数 {1}。当前耐久：{2}。"),
"Info_RiskText": ("Conditional on failure: minor {0}, severe {1}, burnout {2}.\nDurability loss: minor {3}, severe {4}, burnout 100%.\nPatient injury budget: {5} / {6} / {7}; injury location can cause permanent harm or death.\nInstallation failures without retention return the contaminated item. Failed extraction leaves the damaged implant installed; loss of expansion support follows slot-loss rules.", "以下概率均以手术失败为前提：轻度 {0}、重度 {1}、烧毁 {2}。\n耐久损失：轻度 {3}、重度 {4}、烧毁 100%。\n患者伤害预算依次为 {5} / {6} / {7}；伤害部位可能导致永久伤残或死亡。\n安装失败的非残留分支退回受污染的物品；取出失败保留受损植入体，扩容失效按槽位损失规则处理。"),
"Info_AbilityText": ("Upkeep {0} W; startup {1} Wd; use {2} Wd\nDuration {3}s; cooldown {4}s\nHealing {5}; damage {6}\nTargets: {7}; range {8}", "持续 {0} W；启动 {1} Wd；单次 {2} Wd\n持续 {3} 秒；冷却 {4} 秒\n治疗量 {5}；伤害 {6}\n目标：{7}；距离 {8}"),
"Info_Yes": ("Yes", "是"), "Info_No": ("No", "否"),

"Name_Battery": ("Storage", "储能"), "Name_Metabolic": ("Metabolic", "代谢发电"), "Name_Overdrive": ("Overdrive", "运动强化"),
"On": ("running", "运行"), "Off": ("off", "关闭"), "EMP": ("EMP disabled", "EMP 停机"), "NotPowered": ("waiting for power", "等待供能"),
"TurnOn": ("On", "开启"), "TurnOff": ("Off", "关闭"), "Use": ("Activate", "激活"), "Activation": ("Startup", "启动"), "Priority": ("Priority", "优先级"),
"ChargeHint": ("Click to charge from a colony battery.", "点击选择殖民地电池进行充电。"), "Charge": ("Recharge", "充电"), "NoBattery": ("No charged colony battery.", "没有可用的殖民地电池。"),
"CancelSurgery": ("Cancel extraction surgery", "取消取出手术"),
"Error_PawnExclusion": ("Conflicts with another CBM or scheduled implant on this pawn.", "与此角色已有或排队植入的 CBM 互斥。"),
"Error_PartExclusion": ("Conflicts with another CBM or scheduled implant on this body part.", "与此部位已有或排队植入的 CBM 互斥。"),
"Error_PawnLimit": ("This pawn has reached this CBM's quantity limit.", "此角色已达到该 CBM 的数量上限。"),
"Error_PartLimit": ("This body part has reached this CBM's quantity limit.", "此部位已达到该 CBM 的数量上限。"),
"Carried": ("Carried", "随身 CBM"), "HotInsertShort": ("Hot-plug", "热插入"),
"PanelToggle": ("Show or hide this pawn's CBM panel.", "开启或关闭此角色的 CBM 面板。"),
"Name_Placeholder": ("Test CBM", "测试块"), "Name_Interface": ("Interface", "接口"),
"Name_Controller": ("Controller", "控制器"), "Name_Row": ("Row extender", "增行器"),
"Name_Column": ("Column extender", "增列器"), "Name_Bay": ("Expansion bay", "扩展板"),
"PendingShort": ("Pending", "待安装"), "UnplugShort": ("Unplug", "拔出"), "SurgeryShort": ("Surgery", "手术"),
"Pending": ("Prepared CBMs", "待安装 CBM"), "Deployed": ("deployed", "已部署"),
"ShowHealth": ("Health", "健康状态"), "ShowCBM": ("CBM grids", "CBM 槽位"),
"NoCompatibleParts": ("No compatible body grid.", "没有兼容的身体槽位板。"),
"MainBoard": ("Main", "主板"), "ExtensionBoard": ("Expansion", "扩展板"),
"ResearchComplete": ("Layout research complete · same-board moves unlocked", "布局重构已研究 · 可在同板内重排"),
"ResearchLocked": ("Layout locked · research implant layout reconfiguration", "布局已锁定 · 需研究植入体布局重构"),
"GridLegend": ("H: hot-swap socket · other cells: fixed", "H：热插拔格 · 其余为固定连接格"),
"CellTags": ("Cell tags", "格子标签"),
"HotExtract": ("Hot-extract to inventory", "热拔出到随身库存"), "QueueSurgery": ("Schedule extraction", "安排取出手术"),
"ChooseHint": ("Drag or select then place. R rotates; Esc cancels. Right-click an empty cell to insert a carried CBM.", "拖动或选中后点击放置；R 旋转，Esc 取消。右击空格接入随身 CBM。"),
"OnlySurgeryIn": ("surgery adds; placement removes", "仅手术加入，部署后不能退回"),
"PendingEmpty": ("Surgery-prepared CBMs appear here.", "手术准备完成的 CBM 会出现在这里。"),
"HotCell": ("Hot-swap cell: the CBM must also support hot swapping.", "热插拔格：CBM 本身也必须支持热插拔。"),
"FixedCell": ("Fixed connection: surgery required for extraction.", "固定连接格：取出需要手术。"),
"PlaceHint": ("Release or click to place. R rotates.", "松开鼠标或点击放置；R 旋转。"),
"NoCarriedCBM": ("No CBM in this pawn's inventory.", "此角色随身库存中没有 CBM。"),
"SurgeryQueued": ("CBM extraction surgery scheduled.", "已安排指定 CBM 的取出手术。"),
}
text["CancelMove"] = ("Cancel relocation", "取消移动")
text["MoveTarget"] = ("Reserved relocation target", "移动目标（已预留）")
text.update({
    "LossRecovered": ("Recovered {0} after loss of its supporting slots.", "槽位丢失后回收到 {0}。"),
    "LossLost": ("{0} was lost when its supporting slots disappeared.", "{0} 随支撑槽位丢失，未能回收。"),
    "State_Dirty": ("dirty", "肮脏"), "State_Unsterile": ("unsterile", "未灭菌"),
    "State_Damaged": ("damaged", "损坏"), "State_BurntOut": ("burnt out", "烧毁"), "State_Intact": ("intact", "完好"),
})
errors = {
"NotPowered": ("This CBM is not operational.", "此 CBM 尚未运行。"), "NotEnoughEnergy": ("Not enough stored energy.", "储能不足。"), "Cooldown": ("This CBM is cooling down.", "此 CBM 正在冷却。"),
"BurntOut": ("This CBM is burnt out and cannot be installed or relocated.", "此 CBM 已烧毁，不能安装或移动。"),
"Dirty": ("Clean this CBM before installation.", "此 CBM 肮脏，清洁后才能安装。"),
"Unsterile": ("Sterilize this CBM before installation.", "此 CBM 未灭菌，灭菌后才能安装。"),
"Reserved": ("These cells or this module are reserved by a relocation.", "这些格子或组件已被移动意图预留。"),
"SlotTagsMismatch": ("Every occupied cell must meet this CBM's tag requirements.", "占用的每一格都必须满足此 CBM 的标签限制。"),
"DuplicateInstance": ("Duplicate module instance.", "植入物实例重复。"), "UnknownModule": ("Unknown CBM definition.", "未知 CBM 定义。"),
"MissingBodyPart": ("Host body part is missing.", "宿主身体部位已缺失。"), "RotationForbidden": ("This CBM cannot rotate.", "此 CBM 不允许旋转。"),
"MissingBoard": ("The supporting board is unavailable.", "所依赖的槽位板不可用。"), "OutsideGrid": ("Footprint extends outside available cells.", "模块形状超出可用格子。"),
"Occupied": ("Another CBM occupies these cells.", "目标格子被其他 CBM 占用。"),
"ExpansionNeedsBaseCells": ("Expanders must fit entirely in original main-board cells.", "扩容器必须完全放在主板原始格子中。"),
"ExpansionLimit": ("Only one expander of each type per main board.", "每块主板同类扩容器只能部署一个。"),
"HotDestinationRequired": ("Both the CBM and every target cell must support hot swapping.", "CBM 和所有目标格都必须支持热插拔。"),
"WrongBodyPart": ("This CBM is prepared for a different body region.", "此 CBM 已绑定其他身体区域。"),
"CrossBoardRequiresHotSwap": ("Research permits same-board moves only; cross-board movement requires both hot-swap endpoints.", "研究只允许同板移动；跨板需要两端满足热插拔条件。"),
"ResearchRequired": ("Research layout reconfiguration before moving fixed connections.", "移动固定连接模块前需研究植入体布局重构。"),
"ExpansionInUse": ("Remove dependent modules before extracting this expander.", "此扩容器仍被使用，先处理依赖它的模块。"),
"SurgeryRequired": ("Extraction requires surgery; this CBM or some occupied cells are not hot-swappable.", "需要手术取出：此 CBM 或部分占用格不支持热插拔。"),
"ReadOnly": ("This pawn cannot be edited.", "当前角色仅可查看。"),
"OperatorUnavailable": ("CBM adjustment requires an awake pawn on the map capable of manipulation and not in a mental state.", "调整 CBM 需要角色在地图中清醒、未倒地、非精神状态且具备操作能力。"),
"InventoryFull": ("Could not store the extracted item.", "无法存放拆出的物品。"), "ItemUnavailable": ("The actual carried CBM is unavailable.", "随身 CBM 实物已不可用。"),
"AlreadyQueued": ("Extraction for this module is already queued.", "此模块已有取出手术。"),
}
text.update({"Error_" + key: value for key, value in errors.items()})
text.update({
    "Name_TargetRig": ("Target suite", "目标套件"),
    "AbilityName_shock": ("Electric discharge", "电击"), "AbilityName_treat": ("Field treatment", "现场治疗"),
    "CBMSelectTarget": ("Select target", "选择目标"),
    "Error_InvalidCBMTarget": ("Invalid CBM target.", "目标不适用于此 CBM。"),
    "Error_CBMOutOfRange": ("Target is out of range.", "目标超出作用距离。"),
    "Error_CBMNoSight": ("Line of sight is blocked.", "视线被阻挡。"),
    "Error_CBMStorageFull": ("CBM storage is full.", "CBM 储能已满。"),
    "Error_CBMUnreachable": ("Cannot reach or reserve this target.", "无法到达或预留此目标。"),
    "Name_AbilityRig": ("Ability suite", "能力套件"),
    "CBMControlDesc": ("Control installed CBMs on {0}.", "控制 {0} 已安装的 CBM。"),
    "NoCBMControls": ("No manually controllable CBM abilities.", "没有可手动控制的 CBM 能力。"),
    "Abilities": ("Abilities", "能力"), "Ability_Passive": ("Passive", "被动"),
    "Ability_Toggle": ("Toggle", "切换"), "Ability_OneShot": ("One-shot", "单次"), "Ability_Reactive": ("Reactive", "反应式"),
    "AbilityName_passive": ("Mobility calibration", "运动校准"), "AbilityName_assist": ("Motor assistance", "动力辅助"),
    "AbilityName_burst": ("Speed burst", "速度爆发"), "AbilityName_repair": ("Emergency repair", "应急修复"),
    "Armed": ("Armed", "自动待命"), "Ready": ("Ready", "可使用"),
    "Reserve": ("Reserve", "保留电量"), "Threshold": ("Threshold", "触发阈值"),
    "AbilityCooldown": ("Cooldown: {0}", "冷却：{0}"),
    "AbilityTiming": ("Duration: {0} · cooldown: {1}", "持续：{0} · 冷却：{1}"),
    "Trigger_DamageTaken": ("Trigger: damage taken", "触发：受到伤害"),
    "Trigger_HealthBelow": ("Trigger: health below threshold", "触发：生命值低于阈值"),
    "Trigger_BloodLossAbove": ("Trigger: blood loss reaches threshold", "触发：失血达到阈值"),
    "Error_AbilityMode": ("This ability does not support that operation.", "此类能力不支持该操作。"),
    "Error_NoAbilityEffect": ("No valid effect target; no energy spent.", "当前没有有效作用对象，不消耗电量。"),
})
text.update({
"CarriedFuel": ("Use carried fuel", "使用随身燃料"),
"Refill": ("Refill fuel", "补充燃料"), "FuelEmpty": ("Empty", "空"),
"ChargeStation": ("Charge implants", "为植入体充电"), "ChargePolicy": ("Automatic charging", "自动充电"),
"AutoFuelOff": ("Disable automatic refilling", "关闭自动补给"), "AutoFuel": ("Automatic refilling", "自动补给"),
"AnyFuel": ("Allow any compatible fuel", "允许任意兼容燃料"), "FuelPrefer": ("Automatic fuel selection", "自动补给燃料选择"),
"Error_FuelMismatch": ("Incompatible fuel or tank contains another fuel", "燃料不兼容或罐内仍有其他燃料"),
"Error_FuelFull": ("Full or insufficient space for one item", "已满或不足以容纳一件补给物"),
"Error_ChargerUnavailable": ("No active charging interface or powered charger", "没有可用的充电接口或充电座未供电"),
"ChargeHelp": ("Requires a dedicated powered charging station. Approach and remain connected; rate is limited by the station and functional implant interfaces. Native batteries are not charging stations.", "需要专用电网充电座。走近并停留连接，速率由充电座与正常植入体接口共同限制。不能直接从原版电池充电。"),
"FuelHelp": ("Each implant has its own tank. Refilling takes time and consumes whole items without overfilling or mixing fuel. Fuel weight and state survive extraction. Automatic refill is off by default and configured in the implant control menu.", "每件植入体拥有独立燃料罐。补给耗时，按整件消耗，不溢出、不混装。燃料重量与状态随取出物品保留。自动补给默认关闭，可在植入体控制菜单配置。")
})
for index, language in enumerate(["English", "ChineseSimplified"]):
    data = Element("LanguageData")
    for key, values in text.items(): node(data, "RC_" + key, values[index])
    save(data, f"Languages/{language}/Keyed/CBM.xml")

print("Generated prototype Defs, profiles, research and translations.")

styles = Element("Defs")
style = node(styles, "RimCBM.CBMVisualStyleDef")
for key, value in dict(defName="RimCBM_ExpansionBlue", type="Expansion", tag="Expansion", priority=10,
                       cellColor="#6060AC", moduleColor="#6060AC", cellIndicatorColor="None").items(): node(style, key, value)
hot_style = node(styles, "RimCBM.CBMVisualStyleDef")
for key, value in dict(defName="RimCBM_HotSwapGreen", tag="HotSwap", priority=100,
                       cellColor="#73997C", cellIndicatorColor="None").items(): node(hot_style, key, value)
save(styles, "Defs/CBM_VisualStyles.xml")

# Pawn action used by drag-and-drop relocation.
jobs = Element("Defs")
job = node(jobs, "JobDef")
for key, value in dict(defName="RimCBM_Relocate", driverClass="RimCBM.JobDriver_CBMRelocate",
                       reportString="reconfiguring CBM.", casualInterruptible="false").items(): node(job, key, value)
save(jobs, "Defs/CBM_Jobs.xml")
for language, report in [("English", "reconfiguring CBM."), ("ChineseSimplified", "正在调整 CBM。")]:
    data = Element("LanguageData")
    node(data, "RimCBM_Relocate.reportString", report)
    save(data, f"Languages/{language}/DefInjected/JobDef/CBM.xml")

loss_defs = Element("Defs")
policy = node(loss_defs, "RimCBM.CBMLossPolicyDef")
for key, value in dict(defName="RimCBM_DefaultLossPolicy", removedDamageChance=.35, destroyedDamageChance=.85,
                      removedDamage=.25, destroyedDamage=.65, removedRecoveryChance=.95, destroyedRecoveryChance=.35,
                      removedBurnChance=.02, destroyedBurnChance=.70).items(): node(policy, key, value)
save(loss_defs, "Defs/CBM_LossPolicy.xml")

tag_defs = Element("Defs")
tag = node(tag_defs, "RimCBM.CBMTagEffectDef")
for key, value in dict(defName="RimCBM_EMPSensitive", tag="EMPSensitive", empSensitive="true", empDurationTicks=600).items(): node(tag, key, value)
save(tag_defs, "Defs/CBM_TagEffects.xml")
job = node(jobs, "JobDef")
for key, value in dict(defName="RimCBM_Charge", driverClass="RimCBM.JobDriver_CBMCharge", reportString="recharging CBM.", casualInterruptible="false").items(): node(job, key, value)
save(jobs, "Defs/CBM_Jobs.xml")
for language, reports in [("English", ["reconfiguring CBM.", "recharging CBM."]), ("ChineseSimplified", ["正在调整 CBM。", "正在为 CBM 充电。"])]:
    data = Element("LanguageData")
    node(data, "RimCBM_Relocate.reportString", reports[0]); node(data, "RimCBM_Charge.reportString", reports[1])
    save(data, f"Languages/{language}/DefInjected/JobDef/CBM.xml")

maintenance = Element("Defs")
zh_service = Element("LanguageData")
for kind, label, zh, work, bench in [("Clean", "clean CBM", "清洁 CBM", 1200, "CraftingSpot"),
                                  ("Repair", "repair CBM", "维修 CBM", 6000, "FabricationBench"),
                                  ("Sterilize", "sterilize CBM", "灭菌 CBM", 2400, "FabricationBench")]:
    recipe = node(maintenance, "RecipeDef")
    for key, value in dict(defName="RimCBM_" + kind, label=label, description=label, jobString=label + ".",
                           workerClass="RimCBM.Recipe_ServiceCBM", workAmount=work, workSpeedStat="GeneralLaborSpeed", workSkill="Crafting").items(): node(recipe, key, value)
    node(node(recipe, "recipeUsers"), "li", bench)
    extension = node(node(recipe, "modExtensions"), "li", Class="RimCBM.CBMServiceExtension"); node(extension, "service", kind)
    ingredients = node(recipe, "ingredients")
    cbm = node(ingredients, "li"); cbm_defs = node(node(cbm, "filter"), "thingDefs")
    fixed_defs = node(node(recipe, "fixedIngredientFilter"), "thingDefs")
    for short, *_ in modules:
        node(cbm_defs, "li", "RimCBM_" + short + "Item"); node(fixed_defs, "li", "RimCBM_" + short + "Item")
    node(cbm, "count", 1)
    if kind != "Clean":
        material = "ComponentIndustrial" if kind == "Repair" else "MedicineIndustrial"
        ingredient = node(ingredients, "li"); node(node(node(ingredient, "filter"), "thingDefs"), "li", material); node(ingredient, "count", 1)
        node(fixed_defs, "li", material)
        node(node(recipe, "skillRequirements"), "Crafting", 6 if kind == "Repair" else 3)
    node(zh_service, "RimCBM_" + kind + ".label", zh); node(zh_service, "RimCBM_" + kind + ".description", zh)
    node(zh_service, "RimCBM_" + kind + ".jobString", "正在" + zh + "。")
save(maintenance, "Defs/CBM_Maintenance.xml")
save(zh_service, "Languages/ChineseSimplified/DefInjected/RecipeDef/CBM_Maintenance.xml")

# Add stock to existing vanilla traders; refresh timing and actual buy/sell prices remain vanilla.
patch = Element("Patch")
op = node(patch, "Operation", Class="PatchOperationAdd")
node(op, "xpath", "Defs/StatDef[defName='Mass']/parts")
node(node(op, "value"), "li", Class="RimCBM.StatPart_CBMInstalledMass")
for trader, chance, kinds, count, pool in [
    ("Caravan_Outlander_BulkGoods", .15, "1~1", "1~1", "Utility"),
    ("Caravan_Outlander_CombatSupplier", .25, "1~1", "1~1", "Combat"),
    ("Caravan_Outlander_Exotic", .65, "1~2", "1~1", None),
    ("Caravan_Outlander_PirateMerchant", .35, "1~2", "1~1", None),
    ("Orbital_BulkGoods", .30, "1~2", "1~1", "Utility"),
    ("Orbital_CombatSupplier", .50, "1~2", "1~1", "Combat"),
    ("Orbital_Exotic", .90, "2~4", "1~2", None),
    ("Orbital_PirateMerchant", .50, "1~2", "1~1", None),
    ("Base_Outlander_Standard", .80, "2~3", "1~1", None),
]:
    op = node(patch, "Operation", Class="PatchOperationAdd")
    node(op, "xpath", "Defs/TraderKindDef[defName='" + trader + "']/stockGenerators")
    gen = node(node(op, "value"), "li", Class="RimCBM.StockGenerator_CBM")
    for key, value in dict(stockChance=chance, kinds=kinds, countRange=count).items(): node(gen, key, value)
    if pool: node(gen, "pool", pool)
save(patch, "Patches/CBM_Economy.xml")

# Seven shared body-region load parameters, independent from left/right grid roots.
burden = Element("Defs")
category = node(burden, "StatCategoryDef")
node(category, "defName", "RimCBM_Burden"); node(category, "label", "implant load"); node(category, "displayOrder", 1050)
load_zh, stat_zh = Element("LanguageData"), Element("LanguageData")
node(load_zh, "RimCBM_Burden.label", "植入体负重")
settings = [
    ("Head", "头部", "Head", .5, [("MeleeDodgeChance", 1, False)]),
    ("Hands", "手部", "Hand", .5, [("WorkSpeedGlobal", 1, False), ("AimingDelayFactor", 1, True)]),
    ("Arms", "手臂", "Arm", 2, [("MeleeCooldownFactor", 1, True), ("AimingDelayFactor", .5, True)]),
    ("Torso", "躯干", "Torso", 4, [("MeleeDodgeChance", 1, False), ("MeleeHitChance", .75, False)]),
    ("Legs", "腿部", "Leg", 2, [("MoveSpeed", 1, False), ("MeleeDodgeChance", .5, False)]),
    ("Feet", "脚部", "Foot", .5, [("MoveSpeed", 1, False)]),
    ("Total", "全身", None, 8, [("MoveSpeed", .5, False), ("WorkSpeedGlobal", .3, False)]),
]
affected = set()
for group, zh, part, free, effects in settings:
    profile = node(burden, "RimCBM.CBMLoadProfileDef")
    node(profile, "defName", "RimCBM_LoadProfile" + group); node(profile, "group", group); node(profile, "freeKg", free)
    if part: node(node(profile, "bodyParts"), "li", part)
    curve = node(node(profile, "penaltyCurve"), "points")
    for point in ["(0, 0)", "(1, 0.05)", "(3, 0.18)", "(6, 0.45)", "(12, 1)"]: node(curve, "li", point)
    entries = node(profile, "effects")
    for stat, strength, increases in effects:
        affected.add(stat)
        e = node(entries, "li"); node(e, "stat", stat); node(e, "strength", strength); node(e, "increases", str(increases).lower())
    stat = node(burden, "RimCBM.CBMLoadStatDef")
    for key, val in dict(defName="RimCBM_Load" + group, label=group.lower() + " implant load", description="Implant mass in this region. Left and right are combined. Excess mass applies encumbrance effects.", group=group,
        workerClass="RimCBM.StatWorker_CBMLoad", category="RimCBM_Burden", defaultBaseValue=0, minValue=0, toStringStyle="FloatTwo", formatString="{0} kg", showOnPawns="true").items(): node(stat, key, val)
    node(stat_zh, "RimCBM_Load" + group + ".label", zh + "植入负重")
    node(stat_zh, "RimCBM_Load" + group + ".description", "体内植入体重量，左右同类部位合并。超过免罚额度后影响相应属性。")
save(burden, "Defs/CBM_Burden.xml")
save(load_zh, "Languages/ChineseSimplified/DefInjected/StatCategoryDef/CBM_Burden.xml")
save(stat_zh, "Languages/ChineseSimplified/DefInjected/RimCBM.CBMLoadStatDef/CBM_Burden.xml")
load_patch = Element("Patch")
for stat in sorted(affected):
    # Some native stats do not yet have a parts list.
    op = node(load_patch, "Operation", Class="PatchOperationConditional")
    node(op, "xpath", "Defs/StatDef[defName='" + stat + "']/parts")
    yes = node(op, "match", Class="PatchOperationAdd"); node(yes, "xpath", "Defs/StatDef[defName='" + stat + "']/parts")
    node(node(yes, "value"), "li", Class="RimCBM.StatPart_CBMBurden")
    no = node(op, "nomatch", Class="PatchOperationAdd"); node(no, "xpath", "Defs/StatDef[defName='" + stat + "']")
    node(node(node(no, "value"), "parts"), "li", Class="RimCBM.StatPart_CBMBurden")
save(load_patch, "Patches/CBM_Burden.xml")

settings_defs = Element("Defs")
settings_def = node(settings_defs, "RimCBM.CBMSettingsDef")
node(settings_def, "defName", "RimCBM_Settings"); node(settings_def, "referenceMassKg", 60); node(settings_def, "burningDurabilityPerSecond", .01)
save(settings_defs, "Defs/CBM_Settings.xml")

# Dedicated charging infrastructure and resumable supply jobs.
supply_defs = Element("Defs")
for suffix, worker, report in [("Refill", "JobDriver_CBMRefill", "refilling implant fuel."), ("ChargeAtStation", "JobDriver_CBMChargeAtStation", "charging implants.")]:
    job = node(supply_defs, "JobDef")
    node(job, "defName", "RimCBM_" + suffix); node(job, "driverClass", "RimCBM." + worker)
    node(job, "reportString", report); node(job, "casualInterruptible", "true")
b = node(supply_defs, "ThingDef", ParentName="BuildingBase")
for field, value in dict(defName="RimCBM_ChargingStation", label="implant charging station", description="Dedicated grid interface for implant charging. Draws up to 505 W, with 90% conversion efficiency and a 5 Wd internal buffer. One user at a time.", thingClass="Building", category="Building", tickerType="Normal", altitudeLayer="Building", passability="PassThroughOnly", pathCost=50, fillPercent=.5, designationCategory="Misc", size="(1,1)", rotatable="false", useHitPoints="true").items(): node(b, field, value)
g = node(b, "graphicData"); node(g, "texPath", "Things/Building/Power/Battery"); node(g, "graphicClass", "Graphic_Multi"); node(g, "drawSize", "(1,1)")
stats = node(b, "statBases")
for field, value in dict(MaxHitPoints=180, WorkToBuild=2000, Flammability=.5, Mass=15).items(): node(stats, field, value)
cost = node(b, "costList"); node(cost, "Steel", 60); node(cost, "ComponentIndustrial", 3)
node(node(b, "researchPrerequisites"), "li", "MicroelectronicsBasics")
comps = node(b, "comps")
power = node(comps, "li", Class="CompProperties_Power"); node(power, "compClass", "CompPowerTrader"); node(power, "basePowerConsumption", 505)
node(comps, "li", Class="CompProperties_Flickable"); node(comps, "li", Class="CompProperties_Breakdownable")
node(comps, "li", Class="RimCBM.CompProperties_CBMCharger")
save(supply_defs, "Defs/CBM_Supply.xml")
for language, name, description, refill, charge in [("English", "implant charging station", "Dedicated powered charging station; 500 W charging input, 5 W idle draw, 90% efficiency, one user at a time.", "refilling implant fuel.", "charging implants."), ("ChineseSimplified", "植入体充电座", "专用电网充电接口，充电输入 500 W，待机消耗 5 W，转换效率 90%，一次供一人使用。", "正在补充植入体燃料。", "正在为植入体充电。")]:
    data = Element("LanguageData"); node(data, "RimCBM_ChargingStation.label", name); node(data, "RimCBM_ChargingStation.description", description)
    save(data, "Languages/" + language + "/DefInjected/ThingDef/CBM_Supply.xml")
    data = Element("LanguageData"); node(data, "RimCBM_Refill.reportString", refill); node(data, "RimCBM_ChargeAtStation.reportString", charge)
    save(data, "Languages/" + language + "/DefInjected/JobDef/CBM_Supply.xml")

# Stable action job; source stores the exact implant, dutyTag its ability id.
jobs = ElementTree(file=ROOT / "Defs/CBM_Jobs.xml").getroot()
cast = node(jobs, "JobDef")
for key, value in dict(defName="RimCBM_CastAbility", driverClass="RimCBM.JobDriver_CBMCastAbility", reportString="using implant ability.", casualInterruptible="false").items(): node(cast, key, value)
save(jobs, "Defs/CBM_Jobs.xml")


for language, values in {
    "ChineseSimplified": {"RC_Error_ViolenceDisabled": "无法执行暴力行为", "RC_Effect_Custom": "自定义效果", "RC_Trigger_Custom": "自定义事件", "RC_Info_WorldRules": "地图外：{0}。", "RC_Info_CastRules": "准备时间：{0} 秒。地图内需要完成释放动作，取消不扣能。地图外：{1}。", "RC_WorldSelf": "仅自身支持效果", "RC_WorldMapOnly": "必须在地图内", "RC_RuntimePaused": "宿主未活动，运行暂停", "RC_Effect_Damage": "伤害", "RC_Effect_Heal": "治疗伤口", "RC_Effect_Tend": "包扎", "RC_Effect_AddHediff": "增加状态", "RC_Effect_ReduceHediff": "减轻状态", "RC_Effect_Projectile": "投射物"},
    "English": {"RC_Error_ViolenceDisabled": "Incapable of violence", "RC_Effect_Custom": "Custom effect", "RC_Trigger_Custom": "Custom event", "RC_Info_WorldRules": "Outside maps: {0}.", "RC_Info_CastRules": "Warmup: {0} seconds. On-map casts require an action; cancellation costs no energy. Outside maps: {1}.", "RC_WorldSelf": "self support only", "RC_WorldMapOnly": "map required", "RC_RuntimePaused": "Inactive host: operation paused", "RC_Effect_Damage": "Damage", "RC_Effect_Heal": "Heal injuries", "RC_Effect_Tend": "Tend", "RC_Effect_AddHediff": "Add condition", "RC_Effect_ReduceHediff": "Reduce condition", "RC_Effect_Projectile": "Projectile"}
}.items():
    data = Element("LanguageData")
    for key, value in values.items(): node(data, key, value)
    save(data, f"Languages/{language}/Keyed/AbilityActions.xml")
    data = Element("LanguageData")
    node(data, "RimCBM_CastAbility.reportString", "正在使用植入体能力。" if language == "ChineseSimplified" else "using implant ability.")
    save(data, f"Languages/{language}/DefInjected/JobDef/AbilityActions.xml")

from MaintenanceDefs import build as build_maintenance
build_maintenance(ROOT)

if args.check:
    differences = [str(p.relative_to(ROOT)) for p in ROOT.rglob("*") if p.is_file()
                   and (not (REPOSITORY_ROOT / p.relative_to(ROOT)).exists()
                        or p.read_text(encoding="utf-8") != (REPOSITORY_ROOT / p.relative_to(ROOT)).read_text(encoding="utf-8"))]
    _scratch.cleanup()
    if differences:
        raise SystemExit("Generated files differ; run python Tools/GenerateDefs.py:\n" + "\n".join(differences))
    print("Generated definitions and translations are current.")
