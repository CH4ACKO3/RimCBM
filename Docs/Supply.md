# 燃料、充电与补给

## 玩家操作

- 选中角色右键地图上的兼容燃料，为指定植入体安排补给。角色走到物品旁，等待补给时间后一次性消耗整件物品；途中取消、燃料失效、植入体取出或烧毁不会提前扣除物品。
- CBM Gizmo 或右键自身的控制菜单可选择燃料模块、指定补给目标、使用随身燃料，并配置自动补给。储罐以实例区分，显示部位、当前量与容量。同种模块不会共享燃料。
- “植入体充电座”在杂项建筑中，需要微电子科技、60 钢铁和 3 普通零部件。连接原版电网并开启，角色右键充电座或在控制菜单选择充电后走近连接。一次一人，移动离开、断电、开关关闭、损坏或失去接口会中断。
- 原版电池仍不能直接充电。储能单元暂时内置 200 W 接收接口；其他模块可独立提供接口。总接收功率是所有可用接口经耐久修正后的和，EMP 停用的接口不接收。
- 手动充电补到满；自动充电默认关闭，提供 25%→90%、50%→100% 策略。自动燃料补给默认关闭，提供 25%→100%、50%→100% 策略，并可指定允许的燃料种类。低位触发后记住目标，单堆不足或工作被打断不会因为超过低位而遗忘补给需求。
- 自动补给接入普通工作决策，不插入紧急工作；征召、饥饿、疲劳、倒地或精神状态时不会自动安排。优先随身燃料，其次最近的安全可达库存；遵守禁用和预留。当前自动补给由宿主自行执行，不是护理员为昏迷患者补给。

## 燃料定义

在 Hediff 的 CBMExtension 中：

```xml
<generationWatts>120</generationWatts>
<fuel>
  <capacity>20</capacity>
  <efficiency>0.8</efficiency>
  <refillTicks>180</refillTicks>
  <options>
    <li>
      <item>Chemfuel</item>
      <unitsPerItem>1</unitsPerItem>
      <energyWdPerUnit>10</energyWdPerUnit>
    </li>
  </options>
</fuel>
<chargeAcceptanceWatts>0</chargeAcceptanceWatts>
```

capacity 与 unitsPerItem 使用储罐容量单位；每件物品占用多少单位可配置。energyWdPerUnit 是每单位原料所含能量，实际转化量再乘 efficiency。可配置多个物品，罐未空时不能混装另一种燃料。补给只接受完整物品，剩余空间不足一件就不消耗；发电可以消耗分数单位。

每秒发电量受输出功率、耐久、剩余燃料和共享储能空余共同限制。满电、关闭、烧毁或被 EMP 停机时不烧燃料。旧版营养发电机制保留；同时配置 fuel 与 nutritionPerWd 会同时需要两种资源。

`CBMSupply.AvailableEnergy` 与 `ConsumeEnergy` 提供燃料能量查询和消耗接口。自定义效果应先检查可用量，避免超额消耗。fuelState 保存燃料类型、分数量、自动策略及选择；实物与 Hediff 之间深复制，带燃料的物品不堆叠。燃料按照对应实物的 Mass 计入植入负重、Pawn Mass 和取出物品的 Mass；发电后相应减重。

燃料发电测试机是 1×2 原型，储量 20、输出 120 W、效率 80%，每单位化合燃料原始能量 10 Wd。纯测试品不进入商人货池，开发者模式可生成；本轮未设计正式 CBM 产品线或制造配方。

## 充电座

ThingDef 添加 CompPowerTrader、CompFlickable、CompBreakdownable 以及：

```xml
<li Class="RimCBM.CompProperties_CBMCharger">
  <watts>500</watts>
  <efficiency>0.9</efficiency>
  <bufferWd>5</bufferWd>
</li>
```

建筑使用 Normal tick。500 W 是充电输入功率，另有 5 W 待机消耗；内部缓冲通过原版电网供电填充，满后降至待机负载，缓冲保存进存档。实际转移受输入×效率、植入接口接收功率、缓冲剩余和充电目标共同限制。消耗缓冲多少就向角色增加多少 Wd，不直接修改任何原版电池。

燃料发电继续遵循现有世界 pawn 能源结算，因此商队可以使用已装入的燃料；充电和自动补给任务仅在地图中运行。没有模拟离图电网连接，也没有便携充电器、护理员补给或燃料排放/热量模块；这些可分别从充电接口、补给任务及已有故障 worker 扩展。
