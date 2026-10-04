# 战绩详情补充信息 1.0.9

补齐额外速度来源，并使用当前复盘轮次的战前修为快照。源码从本机 1.0.8 的 RecordExtras.dll 恢复后修正；具体变更见 LOCAL_CHANGES.md。

源码与测试归档于 `Airexplosion/yixianpai-mods/sources/com.yx.recordextras/`。发布包只包含 manifest 和 RecordExtras.dll，不包含游戏、Unity 或 SDK DLL。

## 构建

本地源码目录与 `yixianpai-mod-sdk` 目录保持同级。若直接在清单仓库的归档目录构建，将 SDK 放在 `sources/yixianpai-mod-sdk/`。游戏引用位于 SDK 的 `refs/`，SDK 引用位于 `sdk/net40/`。

```powershell
dotnet build source/RecordExtras.csproj -c Release
```

## 测试

差异测试需要从本机游戏提取 `BattleCharacterUI.cs`，然后生成原生速度计算对照方法。生成文件 `build/NativeSpeedOracle.cs` 被忽略，不上传。

```powershell
./build/prepare-native-oracle.ps1 -GameSource '<本机 BattleCharacterUI.cs 的绝对路径>'
dotnet test tests/RecordExtras.Tests.csproj -c Release
```

2026-10-04 复验：编译 0 错误、0 警告，22 项测试通过（含 400 组构造快照的原生方法差异比较）；IL 检查 0 错误、6 项警告。尚未完成真实战绩界面的实机验证。
