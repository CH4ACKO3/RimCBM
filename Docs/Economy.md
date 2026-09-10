# 重量、价值与交易库存

## 当前数值

重量单位 kg；价格为原版基础市场价值（银币），实际成交价仍受买卖差价、议价、难度等原版规则影响。体内植入体用同一数值设置 `HediffDef.priceOffset`，进入原版角色价值计算。

| 植入体 | 重量 | 基础价值 | 抽取权重 | 货池 |
|---|---:|---:|---:|---|
| 储能单元 | 1.5 | 600 | 5 | 通用、战斗 |
| 代谢发电机 | 0.8 | 1400 | 2 | 通用 |
| 运动强化器 | 1.2 | 1800 | 2 | 战斗 |
| 增行器 | 0.6 | 1000 | 3 | 通用 |
| 增列器 | 0.8 | 1200 | 2 | 通用 |
| 热插拔扩展板 | 1.2 | 2200 | 1 | 通用 |
| 测试占位块 | 0.4 | 100 | 0 | 不售卖 |
| 接口原型 | 0.3 | 250 | 0 | 不售卖 |
| 固定控制器原型 | 0.8 | 800 | 0 | 不售卖 |
| 能力测试套件 | 1.5 | 2400 | 0 | 不售卖 |
| 目标能力测试套件 | 1.8 | 2800 | 0 | 不售卖 |

重量/价格在物品的 `statBases/Mass`、`statBases/MarketValue` 中定义。当前由 `Tools/GenerateDefs.py` 的 economy 表生成；修改该表后重新生成。正常交易以完整、清洁、已灭菌的新品供货。

## 库存概率

| 原版商人 | 本次有货概率 | 不同种类数 | 每种数量 | 货池 |
|---|---:|---:|---:|---|
| 外来者大宗商队 | 15% | 1 | 1 | 通用 |
| 外来者战斗商队 | 25% | 1 | 1 | 战斗 |
| 外来者珍奇商队 | 65% | 1–2 | 1 | 全部 |
| 外来者海盗商队 | 35% | 1–2 | 1 | 全部 |
| 大宗商船 | 30% | 1–2 | 1 | 通用 |
| 战斗商船 | 50% | 1–2 | 1 | 战斗 |
| 珍奇商船 | 90% | 2–4 | 1–2 | 全部 |
| 海盗商船 | 50% | 1–2 | 1 | 全部 |
| 外来者据点 | 80% | 2–3 | 1 | 全部 |

先按 `stockChance` 决定这一轮是否供货，再从可用货池按 `weight` 不放回抽取 `kinds` 种，每种按 `countRange` 决定数量。权重不是独立出现概率；全部货池第一抽中，储能为 5/15，扩展板为 1/15，后续抽取概率随剩余种类变化。没有本批货物也仍可向商人出售其货池接受的植入体。最终库存交付继续受原版库存生成/运输规则约束。

库存随原版商队/商船生成、据点刷新产生，不修改商人到达频率或刷新周期，不补发已经生成的库存。部落商人与未列出的模组商人默认不扩展。新植入体加入 `CBMTradeExtension` 并设置 weight/pools 即可入池；新商人可在 stockGenerators 中添加 `RimCBM.StockGenerator_CBM`。

`TechHediff` 是 BodyPartBase 继承来的原版通用交易标签。本模组用 `RimCBM_Implant` 替换，避免原版通用植入体生成器绕过概率或重复生成测试品。不要重新加回 TechHediff，除非有意允许另一套来源。

## Pawn Mass 的原版作用与本模组处理

- 原版 Mass 由身体基础重量、BodySize、缺失自然部位、装备/背包以及部分仿生替换部位共同计算。原版 `StatPart_AddedBodyPartsMass` 只对 Hediff_AddedPart 计入取出物品质量的 90%，不会自动覆盖我们的 Hediff_Implant。
- 本模组新增独立 StatPart，对角色与尸体累计所有仍在体内的植入体实物质量。手术待安装、关闭、烧毁以及暂时悬空但未掉落的实例均有重量；取出后不再累计。背包里的植入体由原版物品负重计算，避免重复加算。扩展格子本身不凭空产生额外质量。
- 运输舱 `Dialog_LoadTransporters` 通常使用 `includePawnsMass=true`，因此增加身体重量会增加装载量占用；尸体也带着体内植入体质量。
- 原版 `MassUtility.Capacity` 是可携带角色的 `BodySize × 35 kg`，与 Pawn Mass 不同。普通商队 `Caravan.MassUsage` 使用 `includePawnsMass=false`，只统计角色的装备和背包。因此增加身体重量不会直接减少商队剩余负重，也不会增加携带上限。
- Pawn Mass 不直接修改 MoveSpeed。此版本没有新增身体重量惩罚，也没有把植入体质量额外塞进背包负重。若要让身体强化存在行动或负重代价，需要单独设计。

核查依据为当前安装的 RimWorld 1.6 反编译源码：`RimWorld/MassUtility.cs`、`CollectionsMassCalculator.cs`、`Planet/Caravan.cs`、`Dialog_LoadTransporters.cs`、`StatPart_AddedBodyPartsMass.cs`、`PawnOrCorpseStatUtility.cs`、`StatWorker_MarketValue.cs`、`PriceUtility.cs`；以及原版 Stats_Basics_General.xml 与 TraderKindDefs。

后续更新：本模组已另外实现重量驱动的累赘惩罚，详见 [Burden.md](Burden.md)。上面的 Pawn Mass 说明仍描述原版行为；新增速度/操作惩罚由独立 StatPart 产生。
