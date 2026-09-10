# 植入体受击、损坏与特殊后果

## 原版命中流程

从 `DamageWorker_AddInjury.FinalizeAndAddInjury(Pawn, Hediff_Injury, DamageInfo, DamageResult)` 的实际伤害结果接入。此时部位已确定，原版护甲、伤害修正和伤口结算已执行。不是直接使用最初可能为空的 DamageInfo.HitPart，也不会把攻击标称伤害当成穿甲后的伤害。

1. 没有实际伤害、非健康伤害、EMP 或 SurgicalCut 不走此流程；EMP 保留既有停机机制。
2. 从实际受伤部位向上寻找最近的支持槽位的身体部位；内部器官归该宿主部位。受击保留左右差异，不使用负重系统的左右合并组。
3. 在该部位仍保留的植入体中抽选一个候选，权重为 `宽 × 高 × hitWeight`。待安装和关闭者仍是体内实物；已烧毁或待掉落者不再抽选。
4. 对候选投掷自己的 `hitChance`。一次已结算的部位伤害至多直接命中一个植入体；原版传播伤害或多次伤口结算可分别命中，扩容损毁还可能引起既有槽位损失连锁。
5. `耐久损失 = 实际伤害 × damageMultiplier × 对应伤害类型倍率 / hitPoints`。耐久为 0..1，归零锁定烧毁并关闭，随后重新协调容量和能力。

命中抽选不改变原版角色已经承受的伤害。此版没有植入体拦截人体伤害或独立护甲穿透模型。部位本身被摧毁导致槽位消失时，仍以既有容量损失流程决定回收/遗失/损坏，避免已掉落的实例再受一次直接命中。

## XML 配置

位于 Hediff 的 `CBMExtension`：

```xml
<combat>
  <hitChance>0.3</hitChance>
  <hitWeight>1</hitWeight>
  <hitPoints>40</hitPoints>
  <damageMultiplier>1</damageMultiplier>
  <damageFactors>
    <li><damageDef>Burn</damageDef><multiplier>1.5</multiplier></li>
    <li><damageDef>Cut</damageDef><multiplier>0.8</multiplier></li>
  </damageFactors>
</combat>
```

这是选中该候选后的命中概率，不是每个植入体独立同时投骰。没有列出的 DamageDef 使用倍率 1。hitWeight=0 从候选中排除；hitChance=0 仍可被选中，但不会受直接伤害。

当前原型：一般植入体概率 30%、耐久强度 40；储能单元强度 50；扩容组件概率 40%、强度 30。所有原型 Burn ×1.5、Cut ×0.8、Blunt ×1。可以分别覆盖这些测试平衡值。

## 特殊后果

`damageEffects` 列表支持每项 trigger（Damaged/Burnout）、chance、requiresActive、causes、workerClass。空 causes 代表所有来源；可用来源为 Combat、Surgery、SlotRemoved、SlotDestroyed、Other、Fire。requiresActive 检查受损前开关状态，不等于设备此刻有燃料或正在输出功率；未来燃油 worker 可进一步读取燃料。

下面示例适用于未来可燃植入体，不是当前代谢发电机的默认行为：

```xml
<damageEffectCooldownTicks>600</damageEffectCooldownTicks>
<damageEffects>
  <li>
    <trigger>Damaged</trigger>
    <chance>0.25</chance>
    <requiresActive>true</requiresActive>
    <causes><li>Combat</li><li>SlotDestroyed</li></causes>
    <workerClass>RimCBM.CBMDamageEffect_Ignite</workerClass>
    <fireSize>0.3</fireSize>
  </li>
  <li>
    <trigger>Burnout</trigger>
    <chance>1</chance>
    <workerClass>RimCBM.CBMDamageEffect_Ignite</workerClass>
    <fireSize>0.5</fireSize>
  </li>
</damageEffects>
```

- Damaged 按实际耐久下降触发；首次从未烧毁转为烧毁触发 Burnout。首次烧毁可能同时满足两者。
- 同模块的受损后果共享冷却，投骰失败也消耗本次触发窗口，避免高频微伤重复抽奖。首次烧毁绕过受损冷却，但残骸不会重复触发烧毁事件。
- 冷却随 Hediff 保存，也随取出的物品、拆分、重新植入传递。
- 起火使用原版 Fire；仅在地图中附着于 pawn 或其尸体，不模拟商队/离图火灾。当前普通植入体 damageEffects 默认为空，不会普遍随机自燃。
- 接入战斗损伤、手术取出失败和槽位损失造成的在体损伤。手术失败对患者施加的随机伤害不会再重复投掷本模组的直接命中。安装失败尚未植入的实物仍使用原有回收规则。
- 自定义 worker 继承 `CBMDamageEffectWorker`，接收实例、损伤来源、DamageInfo、损失耐久、首次烧毁和受损前开关状态。同步嵌套效果受到递归保护，避免特殊后果无限触发自身。

## CE 扩展接口

`CBMCombat.Resolver` 可替换为 `CBMHitResolver` 子类，重写 `Select` 与 `DurabilityDamage`。`CBMHitContext` 携带 pawn、真实部位、DamageInfo、实际伤害，可用于后续详细穿透、植入体护甲与命中覆盖计算。

CE 专属伤害入口可以设置 `CBMCombat.UseVanillaHook=false`，再调用 `ApplyResolvedHit`，以免原版与 CE 适配重复结算。无论入口如何替换，耐久、烧毁、特殊后果和容量损失链路仍共用。当前未声明已经兼容或实现 CE 护甲模型。


## 持续火伤与体内残留

`CBMSettingsDef.burningDurabilityPerSecond` 默认为 0.01；每 60 tick 检查地图上存活且未暂停生理活动的 pawn。附着原版 Fire 时，每件未烧毁植入体扣除 `全局值 × combat.fireDamageMultiplier × Clamp(火焰大小, 0.25, 2)` 耐久。熄灭后停止，倍率 0 可免疫这项热损伤。原版火焰造成的人体伤口仍独立走上面的部位命中流程。

损坏或烧毁不会自动取出植入体。完全失去槽位支撑、但宿主身体部位仍存在的损坏件成为 `retainedRemnant`：停用、不能移动或重新放置，显示在对应部位右侧“待装/残留”栏，右键可安排手术。仍有支撑者保留格上位置；原本已连接到兼容热插拔格的烧毁件仍可热拔。没有槽位连接的手术残留不能凭自身 HotSwap 标记跳过手术。身体部位本身被切除或摧毁时，继续使用原有回收概率。

安装手术新增 `installationSurgery.retainedChance`，与 severeChance、burnoutChance 一起占用失败分支概率，剩余为轻度失败。当前值 0.15 是**手术已经失败之后的 15%**。该分支使用 severeDamage / severePatientDamage，消耗实物并留下受污染的损坏植入体，不进入可拖动待安装状态。取出手术成功前仍计入质量和数量互斥。

## 可注册的周期故障

在 CBMExtension 下添加 faults，每项 id 在该定义中唯一：

```xml
<faults>
  <li>
    <id>shortCircuit</id>
    <labelKey>RC_FaultDischarge</labelKey>
    <maxDurability>0.75</maxDurability>
    <intervalTicks>600~1800</intervalTicks>
    <chance>0.25</chance>
    <affectsBurnt>true</affectsBurnt>
    <requiresActive>false</requiresActive>
    <requiresEnergy>true</requiresEnergy>
    <energyWd>0.5</energyWd>
    <damageAmount>2</damageAmount>
    <workerClass>RimCBM.CBMFaultWorker_Discharge</workerClass>
  </li>
</faults>
```

当前储能器和代谢发电器已配置此示例：耐久不高于 75% 时，每 10～30 秒检查一次，25% 概率短路；有足够能量才消耗 0.5 Wd 并造成 2 点宿主部位内部烧伤。关闭设备不能规避该示例的硬件故障；烧毁后仍可从其他正常储能模块取电，完全无电时不造成放电伤害。使用本体 Burn 表达电灼伤，不依赖 DLC。内部伤害跳过护甲且禁止传播，不再递归投掷植入体命中。

扩展 worker 继承 `CBMFaultWorker`，实现 `Apply(Hediff_CBMModule, CBMFaultSpec)`，通过 XML workerClass 注册；需要燃料、自定义状态或其他效果时可继承 spec 添加字段。故障不依赖正常能力调度，待安装件、关闭件、烧毁件及手术残留均按自身条件评估。每项独立计时，投骰失败也重排下一次；计时随存档、取出的物品和再次植入传递，离图不补算错过的次数。取出后不再伤害原宿主。
