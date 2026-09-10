# CI、发行与创意工坊

参考 Search and Rescue 的构建约定，本仓库将校验、构建、打包与发布阶段分开。

- main 推送、PR 和手动触发：验证数据，锁定依赖构建，规则测试，打包并上传 CI artifact。
- `v<modVersion>` 标签：相同检查通过后，创建 GitHub 预发行版并附上 ZIP、manifest、中英文文案和更新说明。标签必须与 About.xml 版本一致。
- 当前没有 Steam 自动上传任务，也不需要 Steam secret；普通提交不会发布工坊物品。

## 本地发行包

```powershell
./Tools/CI/Build.ps1
```

输出在 artifacts/ci。ZIP 仅含 RimCBM 的运行目录、自己的 DLL、玩家介绍与许可；不包含 Source、Tests、开发文档、游戏 DLL、NuGet 依赖或用户配置。manifest 记录文件及 ZIP 的 SHA-256。可重复运行独立校验：

```powershell
python Tools/Validate.py --package artifacts/ci
```

## Workshop 准备文件

- [英文描述](workshop/Description.en.bbcode)
- [简体中文描述](workshop/Description.zh-CN.bbcode)
- [元数据](workshop/metadata.json)
- [SteamCMD 模板](workshop/workshop.vdf.example)
- [中英文更新说明](releases/0.1.0.md)

工坊描述使用 BBCode，CI 检查中英文非空及长度。元数据不填虚构 PublishedFileId。首次发布时在本机 RimWorld 上传界面选取构建后的模组，添加封面和截图、核对描述/依赖/可见性，然后上传；语言描述可在 Steam 后台分别设置。

上传产生的 About/PublishedFileId.txt 留在本机且被 Git 忽略。本轮仓库初始化不会上传 Steam、创建工坊条目或创建版本标签。未来自动发布需单独绑定确切的工坊 ID 和受保护的 Steam 发布凭据，不能复用其他模组的 ID。

## 发布前核对

确认原型状态和兼容范围说明准确，完成本机引擎/界面体验，检查包内容与 manifest，准备专属封面及截图，核对游戏和 Harmony 版本。更新 About.xml、CHANGELOG 与该版本中英文 notes 后再打标签。
