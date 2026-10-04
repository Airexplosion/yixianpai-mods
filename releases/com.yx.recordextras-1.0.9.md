修正原生战绩详情双方修为显示，使用当前复盘轮次的战前修为快照，并补齐飞枭灵芝、突袭、龙行破空、额外速度、五行仙命和速度刻印等加成来源。

源码、测试和构建说明位于 [sources/com.yx.recordextras](https://github.com/Airexplosion/yixianpai-mods/tree/main/sources/com.yx.recordextras)，从本机 1.0.8 DLL 恢复后修正。发布包仅含 manifest 与 RecordExtras.dll。

验证：Release 编译 0 错误、0 警告；22 项测试通过，含 400 组构造快照与本机原生速度方法的差异比较；IL 检查 0 错误、6 项警告。真实战绩界面的实机验证尚未完成。更新后请重启游戏。
