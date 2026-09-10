# CBM 能力配置

一个 CBMExtension 可以包含多个 abilities。每个能力以不变的 id 保存状态，顺序可以调整。能力的开关、冷却、供电期限、效果期限、优先级、自动触发保留电量与阈值独立记录。旧版 statOffsets / burstStatOffsets 与耗能字段仍兼容；使用 abilities 时不要同时设置旧版消费与属性效果字段，避免重复生效。模块储能／发电字段仍可使用。

## 四种能力

| kind | 生效条件 | 支付方式 | 玩家操作 |
|---|---|---|---|
| Passive | 部署且硬件可运行，始终启用 | 可免费，也可配置启动和维持费用 | 不提供关闭按钮 |
| Toggle | 玩家启用且获得供电 | activationWd + idleWatts | 独立开关、优先级 |
| OneShot | 玩家主动使用，目标有效、未冷却 | useWd，一次扣清 | 使用按钮 |
| Reactive | 自动待命且触发条件满足 | useWd，一次扣清，并保留 reserveWd | 自动开关、优先级、保留电量、阈值 |

准备件、烧毁件、待掉落件和已死亡／冻结的宿主不生效。匹配 EMP tag 的模块在 EMP 期间全部能力停用；主动效果中止但冷却保留。非 EMP 敏感模块不受该事件停用。新能力不使用旧的模块总开关，Passive / OneShot 也不受 defaultEnabled 影响；Toggle / Reactive 使用该默认值初始化。

持续能力与旧版持续耗能在同一个优先级分配器里结算，每 60 tick 一次。优先级为模块 powerPriority 加能力 priority；数值大者优先，相同值按实例 ID／能力 ID 稳定排序。缺电不产生效果、也不部分扣费，恢复供电需要重新支付启动费用。切换后最迟下一次结算获得付费效果。

单次与反应式支持 durationTicks 的临时属性效果，以及零持续时间的即时 Worker 效果。付费前检查可用性和作用对象，失败不扣费、不进入冷却。冷却从使用时开始；即使冷却较短，也不能在上次效果结束前重叠激活。同一 tick 最多触发一次，防止零冷却重复触发。手动使用需要宿主清醒、未倒地、无精神状态；自动反应无需手动操作能力。

## XML 示例

放在 CBMExtension 内：

```xml
<abilities>
  <li>
    <id>mobility</id>
    <kind>Passive</kind>
    <labelKey>MyMod_Mobility</labelKey>
    <statOffsets><MoveSpeed>0.05</MoveSpeed></statOffsets>
  </li>
  <li>
    <id>motor</id>
    <kind>Toggle</kind>
    <defaultEnabled>false</defaultEnabled>
    <activationWd>0.2</activationWd>
    <idleWatts>40</idleWatts>
    <priority>1</priority>
    <statOffsets><MoveSpeed>0.2</MoveSpeed></statOffsets>
  </li>
  <li>
    <id>burst</id>
    <kind>OneShot</kind>
    <useWd>2</useWd>
    <durationTicks>180</durationTicks>
    <cooldownTicks>600</cooldownTicks>
    <statOffsets><MoveSpeed>0.8</MoveSpeed></statOffsets>
  </li>
  <li>
    <id>repair</id>
    <kind>Reactive</kind>
    <trigger>DamageTaken</trigger>
    <minimumDamage>1</minimumDamage>
    <useWd>1</useWd>
    <reserveWd>5</reserveWd>
    <cooldownTicks>300</cooldownTicks>
    <healAmount>3</healAmount>
  </li>
</abilities>
```

labelKey 可省略，回退显示 id。持续和冷却以游戏 tick 表示；费用为原版 W / Wd。示例作为 RimCBM_AbilityRig（2×2 能力测试套件）提供，带 EMPSensitive tag。

## 反应条件与策略

- DamageTaken：原版伤害结算结束后触发，minimumDamage 使用实际伤害量；可用 damageDefs 列表限制伤害种类（省略即全部）。这不是伤害前护盾，不能撤销致死结果。
- HealthBelow：每 60 tick 检查原版 summaryHealth.SummaryHealthPercent 是否低于 threshold，范围 0–1。
- BloodLossAbove：每 60 tick 检查原版 BloodLoss 严重度是否达到 threshold，范围 0–1。

持续满足阈值时，冷却结束后可再次触发。没有有效作用对象则不消耗。reserveWd 是“扣费后必须剩余”的电量，不是预留给此能力的独占电池，也不限制其他手动能力的消耗。同一次自动检查按优先级执行，每个能力都重新验证余额和伤势，前一个治疗使后一个无效时不会再扣费。受伤事件与阈值检查是不同触发类型，不重复交叉匹配。

界面在选中 CBM 后，通过底栏“能力”打开滚动列表。每项显示类型、运行／冷却状态及实际 tag 与模式修正后的费用；反应式可以设置保留电量和阈值。当前快捷选项提供优先级 0–3、保留电量 0/5/10/25/50 Wd、阈值 10/25/50/75/90%；XML 可提供其他初始数值。DamageTaken 的种类过滤与最低伤害由定义控制。

## 效果扩展和保存

基础 Worker 支持 statOffsets 和 healAmount。属性效果乘以耐久与 CBM tag 的 effectMultiplier；耗能乘以 consumptionMultiplier。healAmount 优先流血伤口，再按伤口严重度分配，排除永久伤，不能治疗疾病、再生缺失部位或直接清除失血。没有普通伤口时治疗能力拒绝执行。

自定义 workerClass 继承 RimCBM.CBMAbilityWorker，提供无参数构造函数。CanApply 只验证，不修改世界或扣费；Apply 在支付与冷却登记之后执行自定义效果。基础版本支持自身与 Thing 目标。单次能力可从右键菜单对指定目标释放，或从 Gizmo 进入原版 Targeter。支持实际投射物及寻找可达位置后准备释放；范围选区尚未提供。界面应调用 CBMCasting.Queue；CBMAbilities.Activate 仅供最终结算和反应触发。详见 [AbilityActions.md](AbilityActions.md)。

反应回调使用 Pawn_HealthTracker.PostApplyDamage 的后置补丁，避免伤口移除干扰原版遍历；每个 pawn 的反应执行有重入保护。手术取出、热拔出、格子掉落时，实物保留设置和绝对冷却截止 tick，但不携带仍生效的临时效果／供电租期。重新安装不会重置冷却。带个体能力状态的物品不堆叠，避免合并不同设置；拆分复制状态。新增 XML 能力会从默认值初始化，旧存档缺失状态列表可安全加载。


## 统一 Gizmo 快捷操作

Pawn.GetGizmos 提供一个 CBM 按钮，枚举已部署组件的单次、切换与反应式能力，以及旧版发电／耗能组件。按钮直接打开原版 FloatMenu，点击某项即操作并关闭菜单，不创建 Dialog_CBMAbilities。详细策略配置入口仍在健康面板。

冷却、缺电、宿主状态或烧毁导致不可用的选项置灰并显示原因；执行时再次校验，防止菜单打开后组件被取出、余额变化或能力刚进入冷却。开关记录期望状态，旧菜单不会把已经关闭的能力重新开启。同部位同种组件以实例顺序编号，操作绑定具体 Hediff。多选角色的 Gizmo 不合并，避免误触发其他角色。


## 目标能力与地图右键

CBMAbilitySpec 新增 targetKinds、range、requireLineOfSight、damageAmount 与 damageDef。targetKinds 默认为 Self；支持 Self（自己）、Pawn（任意其他活 pawn，含动物）、FriendlyPawn（同阵营其他活 pawn）、HostilePawn（敌对活 pawn）、Thing（其他物品／建筑）。多个类型取并集，未指定类型不会出现在相应目标菜单中。

```xml
<abilities>
  <li>
    <id>treatment</id>
    <kind>OneShot</kind>
    <targetKinds><li>Self</li><li>FriendlyPawn</li></targetKinds>
    <range>1.5</range>
    <useWd>1</useWd>
    <cooldownTicks>300</cooldownTicks>
    <healAmount>4</healAmount>
  </li>
  <li>
    <id>discharge</id>
    <kind>OneShot</kind>
    <targetKinds><li>HostilePawn</li></targetKinds>
    <range>12</range>
    <requireLineOfSight>true</requireLineOfSight>
    <useWd>2</useWd>
    <cooldownTicks>300</cooldownTicks>
    <damageAmount>8</damageAmount>
    <damageDef>Burn</damageDef>
  </li>
</abilities>
```

上述配置作为 2×2 TargetRig 测试套件提供。对敌电击样例用基础游戏 Burn 伤害模拟灼伤，不依赖 DLC ElectricalBurn 定义。实际调用 Thing.TakeDamage，包含原版伤害与防护处理，施术者作为 instigator；healAmount 治疗指定 pawn 的普通伤口。攻击被护甲吸收仍属于已释放，会正常消耗。地图内主动释放通过统一动作执行，包括走近、准备和最终校验；不无限追击移动目标。直接电池充电停用，改用专用充电站，详见 [Supply.md](Supply.md)。

非自身目标必须已生成在同一地图、未被战争迷雾覆盖；下达时寻找可达位置，准备和结算时通过 range 与视线检查。目标失效、阵营变化、距离／视线改变、冷却或余额不足时，点击旧选项也会重新检查，拒绝者不付费。外部目标支持 OneShot 和 Reactive Worker 效果，不能使用原本属于施术者的 statOffsets；自定义 Worker 可覆盖 CanApply(module, spec, target) 与 Apply(module, spec, target)，旧的自身重载保留兼容。

RimWorld 1.6 原生 FloatMenuOptionProvider_CBM 同时支持征召／未征召及多选。对 pawn 的选项只在 Pawn 重载生成，避免被 Thing 重载重复添加；多选为每个施术者加名字前缀。右键自己时直接追加自己的所有快捷操作，右键他人时只追加所选角色对该目标的能力，不切换为操纵目标的 CBM。无已选 pawn 的右键入口选择己方 CBM 宿主并打开同样选单，不处理敌方 pawn 或普通空地。

补给与充电选项使用专用接口，原版电池入口保持禁用。最新工作流与配置见 [Supply.md](Supply.md)。


## 扩展契约

最新生命周期、事件上下文、Custom 效果、实例数据与资源来源 Worker 见 [ExtensionContracts.md](ExtensionContracts.md)。旧 useDurationTicks 能力通过保留 ID `__legacy_use` 适配统一动作；旧存档仍保留原有能源和冷却字段，新的定义请使用 abilities。
