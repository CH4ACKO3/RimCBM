# 对外扩展契约

## 效果、上下文和配置验证

纯自定义效果设置 `<kind>Custom</kind>` 和自己的 `workerClass`，不再需要提供虚假的伤害或治疗参数。默认 Worker 不允许 Custom。效果 Worker 的 `RequiresMap(effect)`、`IsViolent(effect)` 可覆写；内建 Damage/Projectile 返回 true。既有 `MapOnly` 属性仍有效。

`CBMAbilityWorker.CanApply(CBMExecutionContext)` / `Apply(CBMExecutionContext)` 和 `CBMEffectWorker.CanApply(context, effect)` / `Apply(context, effect)` 可读取 module、ability、target、automatic、trigger。默认实现转发旧重载，现有扩展无需立即重写。验证必须无副作用，提交不能再次扣取本次能力能源。能力、效果、故障、燃料 spec 的 ConfigErrors 现为 virtual；派生类应保留 base 校验。

能力 `requiresViolence` 未设置时根据伤害字段与效果 Worker 判断，攻击默认遵守原版无法暴力限制；显式 false 可用于特殊豁免。自定义攻击 Worker 应声明效果 IsViolent 或在能力设置 requiresViolence=true。

## 持续能力生命周期

在能力 Worker 覆写：

- `OnStarted(module, ability, state)`：首次获得运行资格及供电，或从停机恢复时一次调用。
- `Tick(module, ability, state, ticks)`：每次供能结算后运行，当前周期 60 tick；不会补算休眠期间的周期。
- `OnStopped(module, ability, state, reason)`：由运行转为停止时一次调用，reason 包含禁用、不可用、缺电、中断和移除。

切换关闭、模式/EMP 中断、移除、死亡以及直接烧毁有即时停止处理；宿主场景、兼容性和其他运行资格变化在下一次 60 tick 结算协调。不要依赖毫秒级通知。连续无电不会反复 OnStopped。

Worker 是 Def 级共享对象，不能把某个 pawn 的运行状态存进 Worker 字段。能力可设置 `dataClass` 为继承 `CBMAbilityData` 的类型，通过 `state.data` 取得逐实例数据，必须实现 ExposeData 与深复制 Copy。数据随能力状态保存及物品转换；未知能力 ID 的状态不会删除。`runtimeActive` 不保存，加载后首次有效供电会重新 OnStarted，供挂接运行期资源；回调必须避免重复创建已持久化的健康状态或对象。

## 自定义事件和反应目标

能力可配置 `<trigger>Custom</trigger>`、`<eventId>Author.EventName</eventId>`，第三方调用 `CBMAbilities.Dispatch(pawn, new CBMAbilityEvent { id=..., source=..., payload=... })`。还可用 `triggerWorkerClass` 覆写纯查询 Matches 和 Target。所有事件仍共享优先级、能源储备、冷却、目标校验与按宿主的防重入机制。

内建事件为 DamageTaken 和 Periodic；受伤事件携带原始 DamageInfo、实际总伤害与 instigator。`targetEventSource=true` 将反应指向来源，例如反击攻击者，仍需配置正确 targetKinds/range，来源为空就不触发，不能退回自身。非自身反应只在同一地图且满足范围/视线时结算，不建立追击任务。事件 payload 是运行期对象，不进入存档；持续跨 tick 的资料需写入 state.data。

## 物品身份

一个物品 ThingDef 对应一个植入体 HediffDef。重复映射会产生 Def 配置错误，模糊映射不能安装或进入热插拔列表。失败残留直接使用本次手术明确的 HediffDef，不再反查首个定义。第三方若要两个方案，请提供两个物品 Def；目前不支持同一实体物品的多态植入身份。

## 补给来源

`CBMFuelOption.workerClass` 默认 CBMFuelSourceWorker：

- `AvailablePortions(source)` 返回当前可提供的整份数量，不能修改来源。
- `TryConsume(source, portions)` 原子消耗这些份数，失败必须完全不修改来源；成功后框架才增加油箱燃料。默认实现消耗物品堆叠，可覆写为从容器内部扣除内容。
- 每份对应 unitsPerItem；来源仍按 option.item 寻找并遵守地图距离、所属物品、可达和任务中断规则。
- 容器来源应设置 massPerUnit 为每单位实际燃料重量，避免把容器重量计入油箱；默认 -1 按原物品质量换算。

`CompProperties_CBMCharger.workerClass` 默认 CBMChargeSourceWorker，负责 Available(charger) 和 Tick(charger)。第三方电网可替换它，从自己的来源扣取能源并补充 charger.buffer。默认保持原版电网的输入功率和效率模型。框架仍管理站点任务、预约、邻接、受损/开关检查、接口速率和接收方容量，buffer 会限制在 0..bufferWd。自定义来源必须保证注入缓冲能量有对应来源扣费，接口无法替第三方验证其私有电网账目。

所有来源 Worker 和 HostTag Worker 均按 Def 缓存，必须保持无宿主实例状态；容器状态放在 ThingComp，充电状态放在实际充电器 Comp。
