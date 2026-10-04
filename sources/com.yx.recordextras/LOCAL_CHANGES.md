# 战绩详情补充信息 1.0.9 本地修复

旧版 1.0.8 只读取当轮 `permanentBuffTempDatas[10012]`，所以其他速度来源没有并入修为数字。修为基数还误用了战后 `publicData.exp`，与原生战绩详情采用的 `lastRoundData.exp` 不一致。

本次按本机游戏的 `BattleCharacterUI.CalExtraSpeed` 补齐：

- 当轮快照中的 `10012`（飞枭灵芝）、`10045`（突袭）、`10056`（龙行破空）、`369`（额外速度）。
- 仙命 128 家族：按当轮已选仙命、上阵牌的五行名称、仙命 278 的覆盖条件，读取实际配置参数。
- 当轮已使用的速度刻印 42 家族，读取实际刻印配置参数。
- 修为与加成都使用当轮战前快照；保留原来的 `修为：基础+加成（合计）` 显示和天衍仙命图标。

叶冥冥偶数轮、黎承云八方来风等条件加成采用战绩中已经结算的速度值，与原生战斗界面保持一致，不再根据当前轮次、战后仙命或留手牌数重复追加。修改作用于双方，不改战绩数据或对局逻辑。

源码从本地 1.0.8 发布 DLL 恢复，原包保留在 `../downloads/com.yx.recordextras-1.0.8.zip`。工程只在本地引用游戏/Unity/SDK DLL，发布包仅含 manifest 和 RecordExtras.dll。

## 验证

- Release 编译：0 错误、0 编译警告。
- 22 项测试通过，包括四类速度 Buff、合并计算、五行条件、刻印、当轮修为、双方和轮次切换、重复刷新、空数据和本地化标签。
- 测试中另有 400 组混合快照，使用本机游戏原始方法做差异比较，全部一致。测试配置和快照是构造样本，不是从真实战绩导出的案例。
- SDK IL 检查：0 error；6 个 YX014 warning 都是游戏已有的 `List<int>` / `Dictionary<int,int>`，原生代码也使用这些集合类型。记录见 `build/il-check.json`。
- 尚未进入游戏验证实际战绩界面；替换插件后需要重新加载插件或重启游戏。

## 构建和测试

```powershell
dotnet build source/RecordExtras.csproj -c Release
# 需要本机游戏反编译文件 downloads/recordextras-game/BattleCharacterUI.cs
./build/prepare-native-oracle.ps1
dotnet test tests/RecordExtras.Tests.csproj -c Release
dotnet ../yixianpai-mod-sdk/tools/yx-patch.dll pack com.yx.recordextras --out dist
```
