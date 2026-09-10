# 开发与测试

在仓库根目录执行命令。常规构建需 .NET 8 SDK、Python 3.11+ 和 PowerShell 7，默认使用 NuGet 引用包，不依赖某台电脑的 Steam 路径。

```powershell
./Tools/CI/Build.ps1
```

脚本按锁文件还原依赖，检查生成数据与 XML/翻译/文档，编译主程序集和引擎探针，运行规则测试，再打包。引用版本统一放在 `Build/RimWorld.References.props`；更改版本后重新 restore 并提交相关 packages.lock.json。源 DLL、NuGet 缓存、bin/obj、测试产物不提交。

## 单独执行

```powershell
python Tools/GenerateDefs.py --check
python Tools/Validate.py

dotnet build Source/RimCBM/RimCBM.csproj -c Release -p:UseReferencePackages=true
dotnet run --project Tests/RimCBM.RuleTests.csproj -c Release
```

`Tools/GenerateDefs.py` 是样例 Def 与翻译的生成源。改生成源后运行不带 `--check` 的命令更新输出；检查模式只在临时目录生成，不修改工作区。

## 使用本机 API

```powershell
dotnet build Tests/Engine/Probe.csproj -c Release `
  -p:UseReferencePackages=false `
  -p:RimWorldManagedDir="C:/Games/RimWorld/RimWorldWin64_Data/Managed" `
  -p:HarmonyAssemblyPath="C:/Games/Harmony/Current/Assemblies/0Harmony.dll"
```

也可通过 RIMWORLD_DIR 和 HARMONY_DLL 提供引用路径。完整 CI 总是显式选择引用包，避免本机环境影响远端结果。主程序集会复制到 `Assemblies/RimCBM.dll`。

## 真实引擎回归（Windows）

```powershell
./Tests/Engine/Run.ps1 -GamePath "C:/Games/RimWorld" -HarmonyPath "C:/Games/Harmony" -Graphics
```

先构建 Probe。运行器建立全新的隔离运行目录、配置和存档，输出进程 ID 与路径；等待 `complete.txt` 为 PASS，并检查 `failure.txt` 与 Player.log。默认输出至忽略的 `artifacts/engine-*`，不会覆盖已有目录。`-Interactive` 留下可见的演示游戏，普通检查会退出。

云 CI 编译探针但不运行商业游戏。独立规则测试不等于战斗、画面或全部模组组合验收。最近功能回归基线为 117 项规则检查及 329 项本机引擎检查，原始机器流水位于历史归档，后续次数以实际输出为准。

## 代码边界

`Source/RimCBM/Core` 不依赖 Verse，供独立规则测试直接复用。其余文件按槽位、健康整合、能力、手术、供应等职责组织。扩展开发遵循 [ExtensionContracts](ExtensionContracts.md)；保留 DefName、能力 ID、存档键，不以代码整理为由破坏现有存档。
