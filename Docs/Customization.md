# 槽位、CBM 标签与配色

颜色支持 HTML 格式 `#RRGGBB` 或 `#RRGGBBAA`，以及 `HealthEnhanced`（原版健康增强蓝）、`HealthGood`（原版健康良好绿）、`None`（透明/不显示）。颜色只影响管理面板，不改变安装资格或热插拔能力。标签是区分大小写的非互斥字符串集合。

## CBM

在 HediffDef 的 `RimCBM.CBMExtension` 中可配置：

```xml
<tags><li>Expansion</li><li>Utility</li></tags>
<type>Expansion</type>
<color>#285D91</color>
<grantedCellType>Expansion</grantedCellType>
<grantedCellColor>#193B61</grantedCellColor>
<grantedCellTags><li>Expansion</li><li>HotSwap</li></grantedCellTags>
<shortLabelKey>RC_Name_Bay</shortLabelKey>
```

`tags` 描述 CBM 自身的身份，独立于 `requiredAllTags` / `requiredAnyTags` / `forbiddenTags` 对目标格子的要求。`hotSwappable` 仍是模块侧的热插拔能力。`type` 是可选的外观类型；需要同时具备多种分类时使用 tags。`shortLabelKey` 是可选的本地化 Keyed 键，用于格内名称；未提供时使用 HediffDef 的 label，悬停始终保留完整名称与描述。

`grantedCell*` 只控制此扩容器提供的格子，不会把 CBM 自身所有 tag 隐式复制到格子上。可省略直接颜色，让类型或 tag 配色规则统一管理。

## 身体槽位

`RimCBM.CBMGridProfileDef` 支持 `color` 和 `type` 作为原始格的默认值；`cellTags` 条目可以同时覆盖颜色、类型并添加 tag：

```xml
<type>Standard</type>
<cellTags>
  <li>
    <x>2</x><y>0</y>
    <type>MedicalSocket</type>
    <color>#204858</color>
    <tags><li>HotSwap</li><li>Medical</li></tags>
  </li>
</cellTags>
```

格子的主板坐标从左上角 `(0,0)` 开始。宿主身份由 `hostTags` 传播到全部扩展区域。原始格的颜色不隐式传播到扩展格，扩展格使用提供者的配置。行、列扩展不补交角，交角处没有格子。

## 按类型或 tag 定义色表

新增一个 XML Def，无需修改 UI 代码：

```xml
<Defs>
  <RimCBM.CBMVisualStyleDef>
    <defName>MyMod_MedicalStyle</defName>
    <type>MedicalSocket</type>
    <tag>Medical</tag>
    <priority>20</priority>
    <cellColor>#204858</cellColor>
    <moduleColor>#326D81</moduleColor>
  </RimCBM.CBMVisualStyleDef>
</Defs>
```

解析优先级：实例定义的直接颜色（格子使用单格覆盖或主板默认）→ 匹配 type → 匹配 tag → 中性默认色。同类规则选择 priority 较高者；同优先级按 defName 稳定排序。`cellColor` 与 `moduleColor` 可以只声明一个，缺省的目标不会拦截其他配色规则。

初版 `Defs/CBM_VisualStyles.xml` 提供 Expansion 蓝色色表，示例扩容器使用 Expansion 类型，对应新增格按 Expansion tag 配色；HotSwap tag 的格子优先使用浅绿色。格子默认没有角标。选中只轻微提亮本来的颜色，不覆盖其分类色。

生成的示例 Def 由 `Tools/GenerateDefs.py` 管理，修改示例时同步修改生成器；其他模组直接提供自己的 Def 或 XML Patch 即可。


## 互斥

在 CBMExtension 中配置 exclusions，每条规则指定范围及目标 CBM／CBM tag：

```xml
<exclusions>
  <li>
    <scope>Part</scope>
    <modules>
      <li>RimCBM_ExampleOther</li>
    </modules>
  </li>
  <li>
    <scope>Pawn</scope>
    <tags><li>Exclusive.NeuralCore</li></tags>
  </li>
</exclusions>
```

Part 表示同一个具体身体部位：主板与独立扩展板属于同一范围，左右臂分开。Pawn 表示整个角色，跨所有身体部位。modules 使用 HediffDef 名称，tags 匹配 CBM 自己的 tags，不匹配槽位 tag。modules 与 tags 任意一项命中即互斥，可同时配置多个范围和目标。

互斥是双向关系，只需一边声明：A 排斥 B，先装 A 或先装 B 都不允许共存。规则不传递：A 排斥 B、B 排斥 C，不自动推导 A 排斥 C。自身不默认互斥，要“只能装一个”时把自己的 HediffDef 填入 modules；或让模块携带其排斥的共同组 tag。

例如 RimCBM_Controller 内：

```xml
<exclusions>
  <li><scope>Pawn</scope><modules><li>RimCBM_Controller</li></modules></li>
</exclusions>
```

这表示控制器与自身全局互斥。现有增行器、增列器、扩展板均声明 Part 范围的自身互斥。原先按扩容方式硬编码的“一种一个”校验已移除，不再叠加另一套数量规则；新增扩容器应显式声明相应互斥（尤其是不能叠加到相同行／列坐标的扩容定义）。

已部署、待安装均占用互斥关系；未暂停的安装手术队列也预占。新增手术与热插入检查全部队列；执行已排队手术时只检查更早的手术，避免后排反过来阻挡前排。原版医疗调度会跳过冲突的后排手术，取消／暂停前排后可恢复；手术完成前再次检查。普通随身实物不占名额。

部署时排除同一实例，不会与自己冲突。已安装者重排不重复入场；修改定义导致旧存档已有冲突时，不删除或停用已有件，但不能加入冲突者；冲突待安装件需通过手术取出后处理。关闭能力不解除物理 CBM 互斥，取出才释放。旧 installationLimits/maxCount=1 自动转换为自互斥，以兼容旧第三方定义；旧 maxCount>1 保留兼容性，但新互斥配置无需数量字段。

## 角标与耐久条

CBMExtension 新增 `indicatorColor`（开启角标）、`inactiveIndicatorColor`（关闭角标）和 `grantedIndicatorColor`（扩展格角标）。身体配置及其 `cellTags` 单格条目均可使用 `indicatorColor`。

```xml
<indicatorColor>#96D2A3</indicatorColor>
<inactiveIndicatorColor>#6B737B</inactiveIndicatorColor>
<grantedIndicatorColor>#DCEAF5</grantedIndicatorColor>
```

CBMVisualStyleDef 对应字段为 `cellIndicatorColor`、`moduleIndicatorColor`、`inactiveIndicatorColor`，同样遵守显式值、类型、tag 的优先级。透明色可隐藏角标。缺省格子角标为 None，现有所有示例格子都不显示角标；模块开启为浅绿点、关闭为灰色点。

Hediff_CBMModule 保存 `active`（默认 true）、`durability`（默认 1）及可叠加的 `condition`。损坏会使耐久条变为暗黄，烧毁有暗红边框并熄灭工作角标。`Operational` 和 `ConditionFactor` 供后续功能效果查询；烧毁的扩容器已实际停止提供槽位。

扩展格和扩容器使用 `#6060AC`，HotSwap 格使用 `#73997C`，约为原版蓝／绿数值配色的 75% 亮度。仍支持 `HealthEnhanced` 和 `HealthGood` 原版颜色参数。示例新增格不强制指定 type，使 HotSwap tag 色表优先于 Expansion tag；自定义显式颜色或 type 仍可覆盖该选择。

UI 缓存以身体、研究、模块布局变化失效；运行时修改 Def 的 tag、颜色等配置后，调用 `PanelSnapshot.Invalidate()` 刷新面板快照。提交安装始终使用新建规则验证，不信任显示缓存作为最终权限。


## 本体标签

`CoreBody` 表示主体，不表示有机组织。当前自然部位和仿生臂／腿的初始格通过 profile 的 `defaultCellTags` 显式赋予此 tag。它不放在 `hostTags`，因此不会自动传播到新增行、列或独立扩展板；新定义的 profile 也不会无条件获得此 tag。扩展器需要时可通过 `grantedCellTags` 显式授予。CBM 可用 `requiredAllTags` 中的 `CoreBody` 限制为仅主体格安装，或用 `forbiddenTags` 排除主体格。


## 状态与容量损失

所有 CBM 物品的 comps 必须包含 `<li Class="RimCBM.CompProperties_CBMCondition" />`。肮脏 `Dirty`、未灭菌 `Unsterile`、烧毁 `BurntOut` 为独立位标志；损坏由 `0 < durability < 1` 派生。耐久为 0 也视为烧毁；已经设置 BurntOut 后，单独抬高耐久不会恢复工作。已提供清洁、维修、灭菌配方，开发者模式另保留状态测试操作。

损坏但清洁、灭菌的组件允许安装，后续功能可按 `ConditionFactor`（开启且未烧毁时等于耐久比例）计算。污染阻止首次部署及实物安装，不强制删除现有组件；已安装件重新排列不等于重新植入。烧毁件禁止部署／移动，但仍可按连接权限热拔出或手术回收。手术回收及槽位丢失回收会附加 Dirty、Unsterile；热拔出保留原状态。

状态、耐久、开关状态在手术植入／取出、热插拔和掉落之间完整复制，并随物品和 Hediff 保存。仅相同状态的物品允许堆叠，拆分堆叠复制状态。原版手术选材排除不适合安装的 CBM；执行时再次验证。开发者恢复“完好”是测试工具，不是玩家维修功能。

`Defs/CBM_LossPolicy.xml` 的 CBMLossPolicyDef 配置初版概率（均可 XML Patch）：

| 参数 | 手术／移除 Removed | 摧毁 Destroyed |
|---|---:|---:|
| DamageChance：失去支撑时受损概率 | 0.35 | 0.85 |
| Damage：触发后的耐久扣减基数 | 0.25 | 0.65 |
| RecoveryChance：完全失去支撑时回收概率 | 0.95 | 0.35 |
| BurnChance：完全失去支撑时烧毁概率 | 0.02 | 0.70 |

实际字段名为 `removedDamageChance` / `destroyedDamageChance`，其余同样以前缀区分。受损量 = 扣减基数 × 本次丢失格数 / CBM 总占位格数。先抽受损；完全无支撑时额外抽回收及烧毁。回收到的物品保留结算后的状态，未回收则遗失。烧毁也可能由累计耐久耗尽触发，因此最终烧毁率不必等于配置的额外 BurnChance。

自定义槽位提供者应在改变容量前调用 `CapacityEvents.Begin(pawn, reason)`，并在 finally 中调用 `CapacityEvents.End(change)`；或记录 `ImplantService.Build(pawn).Boards()`，改变容量后传给 `CBMCapacityComponent.Reconcile(pawn, reason, before)`。Removed 表示受控移除／手术，Destroyed 表示破坏。无明确来源的配置变更回退到 Removed。原版损伤导致的失肢识别为 Destroyed，SurgicalCut 识别为 Removed；义肢更换按整次手术的最终容量结算。

回收结果记录在 Hediff 的 `lossPending / lossRecover / lossReason`，耐久与状态在事件发生时即结算。生成物品失败时保留该结果再试，不重新抽取；此结果也参与存档。扩容器烧毁导致下游槽消失时沿用 Destroyed 原因，逐层处理。


## 能源与功能

CBMExtension 支持 energyCapacityWd、generationWatts、nutritionPerWd、idleWatts、activationWd、useWd、useDurationTicks、useCooldownTicks。W 表示功率，Wd 表示原版储能；每 tick 使用原版常量换算，不使用现实秒换算。供能与持续耗能不能配置在同一模块。statOffsets 为通电时属性偏移，burstStatOffsets 为主动效果期间额外偏移，均使用原版 StatModifier XML 写法。耐久按比例影响储能容量、发电与属性效果，不降低持续耗电。储能容量不依赖开关，烧毁／失去槽位则移除容量并裁剪溢出储能。

共享储能与各组件的开关、启动状态、供电截止 tick、EMP 截止 tick、效果／冷却截止 tick、powerPriority 均保存。优先级数值较大者优先；同优先级按实例 ID 稳定排序。启动与一个结算间隔的费用必须全部付清才能供电。缺电和 EMP 使效果停用，恢复时重新支付启动费用。

```xml
<RimCBM.CBMTagEffectDef>
  <defName>Example_EMPSensitive</defName>
  <tag>EMPSensitive</tag>
  <empSensitive>true</empSensitive>
  <empDurationTicks>600</empDurationTicks>
  <consumptionMultiplier>1</consumptionMultiplier>
  <effectMultiplier>1</effectMultiplier>
</RimCBM.CBMTagEffectDef>
```

CBM 的 tags 包含该 tag 即应用效果；多个匹配项的倍率相乘，EMP 时长取最大值。消耗倍率影响启动、维持与主动使用，不影响发电营养效率。原有格子安装条件 tag 和颜色规则继续独立运行。

组件角标优先级：烧毁 > EMP > 肮脏／未灭菌 > 损坏 > 缺电 > 开关。CBMExtension 与 CBMVisualStyleDef 均可配置 burntIndicatorColor、empIndicatorColor、dirtyIndicatorColor、damagedIndicatorColor、unpoweredIndicatorColor；沿用显式模块颜色优先于类型／tag 样式的规则。

清洁清除 Dirty 并保留 Unsterile；维修恢复耐久并设置 Unsterile；灭菌只接受不肮脏的组件并清除 Unsterile。状态作用于原实物，普通配方不复制组件，不复活 BurntOut。相应定义由 Tools/GenerateDefs.py 生成。


新版多能力配置使用 CBMExtension.abilities，见 [能力配置与扩展](Abilities.md)。旧版模块耗能与属性字段保留兼容，新定义避免与能力费用、效果重复使用。

## 植入体手术参数与原版信息页

每个 `CBMExtension` 可分别定义 `installationSurgery` 和 `removalSurgery`：

```xml
<installationSurgery>
  <successFactor>0.85</successFactor>
  <severeChance>0.2</severeChance>
  <burnoutChance>0.05</burnoutChance>
  <minorDamage>0.15~0.3</minorDamage>
  <severeDamage>0.45~0.7</severeDamage>
  <minorPatientDamage>3</minorPatientDamage>
  <severePatientDamage>12</severePatientDamage>
  <burnoutPatientDamage>25</burnoutPatientDamage>
</installationSurgery>
```

实际成功率 = 原版手术质量（包括医生、药品、床位和灵感，原版封顶 98%）× `successFactor` × `(0.7 + 0.3 × 当前耐久)`。上述概率和耐久损失范围均为 0..1；重度与烧毁概率之和不得大于 1。两种概率以**已经手术失败**为前提，余下概率为轻度。患者伤害使用原版随机手术伤害预算，实际伤害大小与部位由原版算法决定，并非固定等量扣血。

安装失败返回一件受损、肮脏、未灭菌的实物；取出失败保留原实例并损伤。烧毁或耐久耗尽不能用普通维修复活。失败后的扩容失效继续使用槽位损失策略。专用 `RimCBM_SurgeryOutcome` 继承原版成功率组件和失败社交记忆，替换失败伤害，避免重复施伤。

原型安装系数：占位块 1；储能、接口 .95；代谢、增行、增列 .9；运动强化 .85；两个能力套件和控制器 .8；扩展板 .75。取出系数为安装系数 + .05，最多 1。这些是当前测试平衡值。

物品及体内实例沿用原版信息页，显示状态、耐久、占用、能力、能源、标签限制与安装/取出成功率上限，并解释实际计算条件。健康扩展面板底部的 i 按钮打开选中实例的信息页。Gizmo 的横条、提示与快捷菜单专用行读取同一共享能源池。显示名称移除 CBM；DefName、存档字段及模组名 RimCBM 不变。

## 2026-09-11 调整：清洁/灭菌与暂停直接充电

- `Dirty` 与 `Unsterile` 是两个独立标志，介绍页增加「清洁状态」与「灭菌状态」。清洗后是清洁、未灭菌；灭菌必须先清洁；维修会变回未灭菌。当前工厂新品默认为清洁且已灭菌。尚不模拟无菌包装。
- 参考 CDDA `JSON_FLAGS.md` 的 FILTHY、NO_STERILE 和 NO_PACKED：https://github.com/CleverRaven/Cataclysm-DDA/blob/master/doc/JSON/JSON_FLAGS.md 。这里保留前两维，不照搬包装层。
- 移除健康面板点击能源条充电、地图右键电池充电。旧 `CBMCharging` API 统一拒绝，旧 JobDef/JobDriver 为读档兼容保留，但不执行供能并终止。详细外部充能机制待设计。
- Priority 表示供能分配顺序，数值大者先尝试。能力优先级与植入体优先级相加；不改变成本/强度，也不是原版工作优先级。手动单次释放即时检查电量。界面增加悬浮说明。
