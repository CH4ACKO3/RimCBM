# 研究、依赖、模式与宿主兼容性

## 研究占位

原版研究界面新增“植入体”页签：植入体整合→能源控制 / 模式控制→宿主适配；布局重构保留原有作用。新节点暂为 500 研究量，未普遍锁住已有安装与能力。燃料发电测试机保留标准输出，能源控制研究解锁其低功率模式（输出一半、转换效率不变）。

CBMExtension.installResearch 是安装研究列表，全部完成才允许手术和热插入。CBMAbilitySpec.research 是能力研究列表，CBMModeSpec.research 是模式研究列表。旧存档中已部署模块不会因为新增安装研究而被取出或关闭；研究门槛不阻止拆卸。配方仍可使用原版 researchPrerequisites。

```xml
<installResearch><li>RimCBM_Integration</li></installResearch>
```

## 模块依赖

```xml
<dependencies>
  <li>
    <scope>Pawn</scope>
    <modules><li>RimCBM_Battery</li></modules>
    <tags><li>PowerInterface</li></tags>
  </li>
</dependencies>
```

每个 li 必须满足；一个 li 中的 modules 与 tags 是可替代候选，匹配任意一件即可。tag 只查 CBM 自身 tags。Part 指同一个具体身体部位（左右不同），含其扩展板；Pawn 指全身。不能依赖自己，循环依赖不能成立。

前置件必须已部署、未烧毁、非残留；关闭电源不改变其物理依赖资格，待安装件和排队中的手术不能充当前置件。待安装模块及安装手术队列会阻止移除唯一的有效前置件；有其他有效替代者则允许。取消安装计划可释放队列依赖。

前置件烧毁或宿主条件不再满足时，依赖功能停止，但不撤掉物理槽位、不删除模块。已经失效的依赖不会阻止取出坏件修理。依赖和安装互斥仍是两类规则，原有 exclusions 保持兼容。

## 工作模式

```xml
<defaultMode>off</defaultMode>
<modes>
  <li>
    <id>low</id><labelKey>MyMod_LowMode</labelKey>
    <abilities><li>sensor</li></abilities>
    <generationMultiplier>0.5</generationMultiplier>
    <consumptionMultiplier>0.5</consumptionMultiplier>
    <effectMultiplier>0.8</effectMultiplier>
    <exclusiveGroup>SensorBus</exclusiveGroup><scope>Part</scope>
  </li>
  <li>
    <id>high</id><labelKey>MyMod_HighMode</labelKey>
    <abilities><li>sensor</li><li>pulse</li></abilities>
    <research><li>RimCBM_AdvancedControl</li></research>
    <exclusiveGroup>SensorBus</exclusiveGroup><scope>Part</scope>
  </li>
</modes>
```

一个实例一次只选一种模式。off 是内建保留值，停用受模式控制的功能，但保留物理占位和储能容量。没有 modes 的旧定义保持原有行为；有 modes 时 abilities 是该模式允许的能力 ID 清单，空列表不允许任何能力。旧版发电器可通过 generationMultiplier 控制输出，不需要伪造能力。

选择相同 exclusiveGroup 的新模式，会把范围内冲突模块切到 off；Part 与 Pawn 混合声明时采用更广的 Pawn 范围。它不禁止这些模块同时安装。读档或默认配置造成冲突时按实例 ID 稳定保留一个。

切换清除供电期限和临时效果，重新支付启动费用，但保留冷却与各能力的设置。模式 ID 随实物、拆装及存档保存。被删除的模式 ID 安全停用。Gizmo / 右键自身菜单可选择模式，信息页显示模式倍率、研究门槛与互斥组。

## 三类 tag 的分工

| 上下文 | 来源 | 用途 |
|---|---|---|
| 格子 tag | 网格定义、部位身份、扩容组件 | requiredAllTags / requiredAnyTags / forbiddenTags 判断每格能否安装；HotSwap、CoreBody 保持原义 |
| CBM tag | CBMExtension.tags | 类型配色、tag 效果、模块依赖、模块互斥 |
| 宿主 tag | 当前 pawn、具体宿主部位、可扩展 provider | 安装及运行兼容性 |

三者不自动混合。宿主存在某个 tag 不会让格子获得热插拔能力，模块带某个 tag 也不会自动授予宿主同名特性。格子和宿主查询共用 Core.TagQuery 的“全部 / 任一 / 禁止”语义。

```xml
<compatibility>
  <li>
    <installationOnly>false</installationOnly>
    <minBodySize>0.7</minBodySize><maxBodySize>2</maxBodySize>
    <requiredAllTags><li>Part.Artificial</li></requiredAllTags>
    <forbiddenTags><li>Gene.MyIncompatibleGene</li></forbiddenTags>
  </li>
</compatibility>
```

多个 li 均须成立。installationOnly=true 只限制植入；默认 false 还会实时限制运行。运行不兼容时停用功能，移除路径始终保留。衣物规则不会禁止玩家穿衣，而是在穿上后暂停不兼容的植入功能。

自动标签：Race.DefName、BodyPart.DefName、Gender.Male/Female/None、Part.Natural/Artificial、Gene.DefName（只含激活基因）、Trait.DefName、Trait.DefName.degree、Apparel.DefName、Hediff.DefName（全身）、PartHediff.DefName（精确部位）。祖先部位被仿生替换也视为 Artificial。网格 profile 的 hostTags 会加入宿主上下文，但 defaultCellTags 和单格标签不会。

## 第三方扩展

```xml
<RimCBM.CBMHostTagDef>
  <defName>MyMod_InsulatedHost</defName>
  <apparel><li>MyMod_InsulatedSuit</li></apparel>
  <tags><li>Host.Insulated</li></tags>
</RimCBM.CBMHostTagDef>
```

provider 支持 races、parts、genes、traits、hediffs、apparel；不同类别同时满足，每类内部任一匹配，空列表不限制。也可指定 workerClass，继承 CBMHostTagWorker 重写 Matches / Tags，接入其他模组的身体或装备系统，无须把其状态硬塞进格子 tag。DLC 未开启时基因 tracker 为空，查询安全；扩展 XML 引用 DLC / 第三方 Def 时仍应使用原版 MayRequire 或条件补丁。

这是宿主条件兼容框架，不代表已经实现 CE 护甲、异种身体模型适配、升级替换或多部位联合占位。这些可以继续使用现有接口扩展。
