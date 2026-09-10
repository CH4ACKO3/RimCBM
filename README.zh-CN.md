# RimCBM

RimWorld 1.6 · Alpha · [English](README.md) · [MIT](LICENSE)

以《Cataclysm: Dark Days Ahead》的 CBM 为灵感，在 RimWorld 中用**空间、供能和维护**共同约束植入体配置。

- **身体网格**：按形状安排组件，利用仿生部位、增行/增列和独立扩展板增加容量。
- **手术与热插拔**：手术先加入待安装列表，部署和重排由角色执行动作；热插拔需要组件与所有占用格同时支持。
- **共享能源**：沿用 W / Wd，管理储能、发电、燃料、专用充电座及自动补给。
- **能力和损坏**：被动、切换、单次、反应式能力，配合耐久、污染、灭菌、EMP 和可配置故障。
- **原版操作方式**：健康页侧栏、统一 Gizmo、目标右键菜单，科技放在独立的“植入体”研究页签。

## 安装与使用

需要 **RimWorld 1.6** 和 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)，核心原型不要求 DLC。支持英文和简体中文。

可用发行版会放在 [GitHub Releases](https://github.com/CH4ACKO3/RimCBM/releases)，也可以自行构建。将发行包中的 `RimCBM` 文件夹放入 `RimWorld/Mods`，加载顺序为 Harmony 在前、RimCBM 在后。源码仓库不附带编译后的 DLL。

打开角色健康页，点击详细健康列表上方的 **CBM**。用 **+** 安排安装手术，再将待装组件拖到合法位置；角色完成动作后才落位。右键组件安排拆卸，Gizmo 中控制能力和补给策略。

## 当前状态

目前是可运行的框架原型，正式组件内容、科技进程、美术和平衡仍在开发。部分组件只用于开发测试，不进入普通商人货池。尚未移植 CDDA 的完整组件目录，也未宣称兼容 CE 或所有种族、电网模组。

[玩法与扩展文档](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/README.md) · [更新记录](https://github.com/CH4ACKO3/RimCBM/blob/main/CHANGELOG.md) · [反馈问题](https://github.com/CH4ACKO3/RimCBM/issues)

## 构建

安装 .NET 8 SDK、Python 3.11+ 和 PowerShell 7 后，在仓库根目录执行：

```powershell
./Tools/CI/Build.ps1
```

流程会验证生成文件与翻译、使用锁定的引用包编译、运行独立规则测试并生成 ZIP。不需要安装游戏即可执行这些检查；真实引擎测试另需本机正版游戏。

[开发与测试](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/Development.md) · [发行与工坊准备](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/Release.md) · [第三方说明](THIRD_PARTY_NOTICES.md)
