# CE Accuracy and Firepower Analyzer 维护说明

本文件是这个 MOD 的专属 AI 维护约束。通用 RimWorld 工作区规则见上级 `AGENTS.md`，结构导航见 `Source/ARCHITECTURE.md`。

## 项目定位

- 这是一个 Combat Extended 环境下的命中率、火力、护甲后伤害和弹道分布分析工具。
- 目标是“近似分析和对比”，不是完全复刻 CE 内部运算，也不修改 CE 战斗、武器或护甲数据。
- 代码维护时优先保持现有功能稳定，不为小需求引入大型框架或复杂抽象。

## 当前结构重点

- `Dialog_CEHitChanceCalculator.cs` 是主窗口和报表 UI，已经很大。新增大功能时，优先考虑拆出小型 helper 或面板类，不继续无节制堆到这个文件里。
- `HitChanceModel.cs` 只放命中率、摆动、后坐力、射程误差、弹道分布等命中模型。
- `DpsModel.cs` 只放 DPS、时间轴、护甲后伤害和伤害拆分。
- `CEWeaponDataLoader.cs` 负责读取 CE 武器、弹药、炮塔、射手和伤害数据。
- `CETargetDataLoader.cs` 负责读取目标碰撞箱和护甲。
- `Source/TestProgram.cs` 是趋势测试入口，发行版不需要，但源码仓库可以保留。

## 修改功能时必须同步

新增或修改会影响计算结果的输入字段时，通常需要同步：

- `HitChanceInputs`
- `CEHitChanceCalculatorSettings.ExposeData`
- `Dialog_CEHitChanceCalculator` 的输入 UI 和 buffer
- `BuildInputSignature`
- 中英文 `Languages/*/Keyed/CEHCC.xml`
- `Source/TestProgram.cs` 中的趋势测试

新增武器、弹药、射手、炮塔或目标读取逻辑时，优先接入现有 loader，不要在 UI 绘制代码里直接读取游戏对象。

## 性能约束

- `DoWindowContents` 会逐帧调用，不能直接运行 Monte Carlo、距离曲线、反射扫描、全 Def 扫描或贴图重建。
- 计算结果必须通过输入签名缓存；新增影响结果的字段必须更新 `BuildInputSignature`。
- 弹道图使用 `Texture2D` 缓存。替换贴图或关闭窗口时必须 `Destroy`，避免显存积累。
- 不要为了显示便利在每帧重复创建大量 `List`、`Texture2D` 或临时 Thing。

## CE 机制与近似口径

- 涉及 CE 机制时，优先查 CE 源码、反编译或实际游戏数据，不凭记忆硬猜。
- 读取 CE 私有字段或跨版本字段时，可以使用反射 fallback，但要把风险集中在 loader 中。
- UI、说明和注释中避免宣称“完全复刻 CE”。统一使用“近似”“参考”“估算”等口径。
- 破片、特殊弹道、核弹、串联导弹等未完整支持内容，应提示限制，不要强行伪装成精确模拟。

## 防御性代码边界

- 外部边界可以防御：CE/其它 MOD 数据、反射、Def 缺失、用户输入、存档兼容。
- 内部模型不要堆大量空判断和沉默 fallback。内部输入应尽量在入口处规范化。
- 如果必须静默 fallback，应只用于低风险读取，并用方法名或注释说明这是有意降级。

## 构建和验证

修改 C# 后优先运行：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build 'D:\Steam\steamapps\common\RimWorld\Mods\CE_HitChanceCalculator\Source\CE_HitChanceCalculator.csproj' -c Release
& 'C:\Program Files\dotnet\dotnet.exe' build 'D:\Steam\steamapps\common\RimWorld\Mods\CE_HitChanceCalculator\Source\CE_HitChanceCalculator.Tests.csproj' -c Release
& 'D:\Steam\steamapps\common\RimWorld\Mods\CE_HitChanceCalculator\Tests\CEHitChanceCalculator.Tests.exe'
```

修改 XML 或翻译后，至少解析所有 XML，确认没有未闭合标签或乱码。

## 发行版和源码仓库

- 发行版目录应使用 `CE_HitChanceCalculator_Workshop`，不要直接上传开发目录。
- 发行版只保留 `About`、`Assemblies`、`Defs`、`Languages`、`LICENSE.md`、`NOTICE.md`、`README.md` 等运行所需文件。
- 发行版不要包含 `Source`、`Tests`、`Source/obj`、反编译目录、TODO、临时脚本或 `.git`。
- GitHub 源码仓库应保留 `LICENSE.md`、`NOTICE.md`、`README.md` 和源码；不要提交编译产物。

## About.xml 注意事项

- 本地开发副本可能会把 `About/About.xml` 改成 DEV 名称和 DEV packageId，用于和发行版区分。
- 公开源码和发行版应使用正式名称 `CE Accuracy and Firepower Analyzer`，正式 packageId `zr.ceaccuracyfirepoweranalyzer`。
- 修改 `About/About.xml` 前先检查 `git ls-files -v About/About.xml`。如果它是 `skip-worktree`，不要误把本地 DEV 元数据提交到公开仓库。

## 工作资料位置

- 反编译输出、调查记录、临时脚本和测试笔记放到 `D:\Management\RimWorld\work`，不要留在 MOD 根目录。
- MOD 根目录应尽量保持 RimWorld 可加载结构清楚：`About`、`Assemblies`、`Defs`、`Languages`、`Source`。

