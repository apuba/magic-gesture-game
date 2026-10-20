# MagicMotion 项目工作详细总结

> 整理日期：2026-10-20  
> 当前有效工程：`E:\2026\MagicMotion`  
> 本文结论以当前磁盘代码、测试报告、构建日志、真机记录和手势采集数据为准。

## 1. 项目目标与当前范围

MagicMotion 是一个基于 Unity 的 Android 体感魔法游戏 MVP。玩家面对手机前置摄像头，通过真实手势释放魔法并攻击场景中的石魔。

当前核心链路：

```text
前置摄像头
→ MediaPipe 手部关键点
→ OpenPalm 张掌识别
→ Forward 掌心朝向识别
→ Push 推掌动作识别
→ SK001「震山掌」
→ 黄色魔法弹体
→ 命中石魔并扣血
```

当前只实现并验证这一条垂直链路，暂未扩展火球、护盾、聚灵、召唤狼、技能编辑器等后续内容。

技术基线：

- Unity `2022.3.62f1`
- URP
- Android ARM64 / IL2CPP
- MediaPipe Hand Landmarker
- 应用包名：`com.magicmotion.game`
- 测试设备：HONOR ALP-AN00
- 当前唯一有效工程目录：`E:\2026\MagicMotion`

## 2. 已完成的基础工程

目前已经形成可配置、可替换的识别架构：

| 模块 | 职责 |
|---|---|
| `CameraManager` | 摄像头启动、权限申请及状态管理 |
| `MediaPipeHandTrackingProvider` | 获取最多两只手的 21 个关键点 |
| `GestureRecognizer` | 识别 OpenPalm 等基础手势 |
| `PalmDirectionClassifier` | 识别掌心方向 |
| `PushMotionTracker` | 判断推掌动作 |
| `MotionAnalyzer` | 生成和发布动作事件 |
| `SkillManager` | 组合手势、掌心方向和动作，释放技能 |
| `EventBus` | 解耦视觉、动作、技能和战斗层 |
| `RuntimeMaterialFactory` | 运行时创建技能材质 |
| `DebugHud` | 显示相机、识别、动作、施法、FPS及诊断状态 |

主要配置文件：

```text
Assets/MagicMotion/Resources/Config/MotionConfig.json
Assets/MagicMotion/Resources/Config/Skills.json
```

场景链路包括 `BootScene → TrainingScene/BattleScene`。真机已经能够进入战斗场景，不再卡死在 Boot 状态。

## 3. 已解决的主要问题

### 3.1 Android 构建问题

项目必须使用纯英文路径 `E:\2026\MagicMotion` 构建。

此前主要问题：

1. Android Gradle Plugin 拒绝中文路径。
2. 本机 `android-35\android.jar` 损坏。

当前处理方式：

- 使用英文工程目录构建。
- Target SDK 暂时固定为 API 34。
- 保留本地 MediaPipe 包与所需原生依赖。
- Android Development Build 已能稳定完成。

### 3.2 启动卡死问题

早期 APK 会一直停留在 `Game: Boot`，导致摄像头、识别和战斗全部无法开始。

该问题已经修复，后续真机验证证明：

- 能正常进入战斗场景。
- 相机能启动。
- Vision 管线能持续处理帧。
- 没有对应的空引用、ANR或崩溃。

### 3.3 摄像头权限体验

已经修复：

- 首次启动能够正常弹出系统权限框。
- 点击允许后相机快速进入 Running。
- 拒绝权限后不到一秒进入明确的拒绝状态。
- 不再自动连续弹出第二次权限框。

### 3.4 技能材质和视觉效果

早期技能释放时，URP 粒子着色器会在打包时被剥离。

目前已经修复：

- 真机施法期间不再出现 `Particles/Unlit is unavailable`。
- 用户已经确认看到黄色光球飞出并击中石魔。
- 完整 TrailRenderer 拖尾效果仍缺少单独的人工确认记录。

### 3.5 画面位置适配

针对手高于、低于、偏左或偏右摄像头时识别变差的问题，已经修改推掌的横向漂移计算。

五宫格测试结果：

| 位置 | 结果 |
|---|---|
| 中心 | 通过 |
| 上方 | 通过 |
| 下方 | 通过 |
| 左侧 | 通过 |
| 右侧 | 通过 |

该项已经证明推掌不再只适用于画面中心。

### 3.6 单次动作重复施法

早期一次推掌可能连续产生多次 Cast。目前已经加入收回重置机制：

- 推掌识别后必须先回缩一定比例。
- 回缩后才能重新允许下一次推掌。
- 真机曾验证达到 5 次推掌对应 5 次施法。

## 4. 手势数据采集系统

为了停止反复人工猜测参数，项目已经实现专用数据采集器。

主要文件：

```text
Assets/MagicMotion/Scripts/Calibration/GestureDataRecorder.cs
Assets/MagicMotion/Scripts/Calibration/GestureRecordingPlan.cs
Assets/MagicMotion/Tests/EditMode/GestureRecordingPlanTests.cs
Tools/GestureCalibration/analyze_push_samples.py
docs/手势数据采集与分析指南.md
```

### 4.1 样本计划

正样本共 30 次：

- 距离：0.5、1.0、1.5 米。
- 位置：中、上、下、左、右。
- 每种“距离 × 位置”组合采集两次。

负样本共 15 次：

- 张掌静止。
- 张掌左右移动。
- 张掌上下移动。
- 张掌向身体方向收回。
- 侧掌或握拳向前伸出。

### 4.2 采集器能力

- 3 秒准备倒计时。
- 2 秒连续数据采集。
- 支持接受、重录和中途停止。
- 保存每帧 21 个关键点、掌心中心、掌宽、深度、法线及算法事件。
- 不保存照片或视频。
- 提供中文大字提示、圆形操作按钮和中文语音提示。
- 重录数据不会被分析脚本误当成有效样本。

## 5. 正式采集结果

CodeBuddy 已完成第一轮右手采集，会话为：

```text
push-20261015-130043
```

采集完整性：

| 项目 | 结果 |
|---|---:|
| 已接受样本 | 45/45 |
| 正样本 | 30 |
| 负样本 | 15 |
| 总尝试 | 46 |
| 重录 | 1 |
| 总有效帧 | 999 |
| 算法事件 | 61 |
| 单次最低帧数 | 18 |
| 单次平均帧数 | 21.6 |
| 距离/位置覆盖缺口 | 0 |

数据目录：

```text
Logs/GestureRecordings/push-20261015-130043/
```

第二轮左手 45 次采集尚未执行。

## 6. 采集数据分析结论

### 6.1 不能只靠掌宽增长阈值

正样本的掌宽增长通常明显强于负样本，但尾部存在重叠：

- 正样本 `max_growth` 中位数：0.799。
- 负样本 `max_growth` 中位数：0.165。
- 部分负样本也会产生较大的增长值。

因此，单独提高 `minimumScaleGrowth` 无法同时获得高召回和低误触。

### 6.2 分析脚本和运行时口径差异

分析脚本最初得到较大的 `lateral_residual`，曾建议将横移阈值放宽到约 5.9。

进一步核对后确认：

- 分析脚本和运行时的数学公式相同。
- 脚本取整个采集窗口内增长最大的一对帧。
- 运行时逐帧检测，只要某一时刻同时满足条件就触发。
- 脚本 lateral 数值因此系统性偏大，不能直接写入运行配置。

随后建立了运行时回放脚本，回放得到的 Motion 次数与真机事件基本一致。

### 6.3 稳健优先参数

运行时口径扫描结果：

| 方案 | 正样本召回 | 负样本误触 |
|---|---:|---:|
| 旧配置：growth 0.12 / lateral 0.8 | 33.3% | 6.7% |
| 稳健：growth 0.40 / lateral 3.0 | 63.3% | 0% |
| 召回优先：growth 0.40 / lateral 4.0 | 76.7% | 6.7% |

项目选择稳健方案：

```json
"minimumScaleGrowth": 0.40,
"maximumLateralDrift": 3.0
```

这些结果仍来自同一批标定数据，不属于独立保留集验证。

## 7. 参数真机调试经过

### 7.1 第一轮

参数：

- `minimumScaleGrowth=0.40`
- `maximumLateralDrift=3.0`
- 技能置信度仍为 0.85

结果：

- 20 次推掌只施法 6 次，成功率 30%。
- 5 次干扰动作误触发 0 次。

发现两个主要拦截点：

- 技能置信度不足 0.85。
- OpenPalm 被误判为 Fist。

### 7.2 第二轮

修复内容：

- 推掌置信度改用固定参考点计算。
- 不再让置信度随识别门槛变化。
- HUD 增加更完整的手势与掌心诊断。

结果：

- 20 次施法 9 次，成功率 45%。
- 干扰误触仍为 0。
- Fist 误判从 6 次下降到 1 次。

### 7.3 第三轮

将技能最低置信度从 0.85 调整到 0.78。

结果：

- 约 15/20 次成功，约 75%。
- 误触发 0。
- 主要失败原因转为 `gesture=Unknown`。

## 8. 远距离问题结论

用户反馈约 70 厘米效果最好，1 米以外明显变差。采集和真机调试已经给出几何原因：

```text
growth = 推进距离 / 推进后的摄像头距离
```

同样推进约 18～20 厘米：

- 在 0.7 米处增长明显。
- 在 1 米处增长明显变小。
- 在 1.5 米处更难与日常移动、抖动区分。

项目尝试过根据初始掌宽动态降低远距离门槛，但真机测试失败：

- 远距离推掌与干扰动作的 growth 重叠。
- lateral 也存在重叠。
- 降低门槛后产生误触和重复触发。
- depth 噪声过大，不能可靠补充判断。

因此已经回退远距离宽松门槛：

```json
"minimumScaleGrowth": 0.40,
"minimumScaleGrowthFar": 0.40
```

当前产品策略：

- 主要保证约 0.5～0.8 米。
- 手太远时提示用户靠近。
- 不硬性屏蔽远距离识别，只提供提示。

## 9. 当前生效参数

动作参数：

```json
{
  "minimumDuration": 0.10,
  "maximumDuration": 1.20,
  "minimumScaleGrowth": 0.40,
  "minimumScaleGrowthFar": 0.40,
  "nearPalmWidth": 0.105,
  "nearEnoughPalmWidth": 0.08,
  "farPalmWidthExit": 0.095,
  "minimumScaleVelocity": 0.10,
  "minimumDepthChange": 0.015,
  "maximumLateralDrift": 3.0,
  "cooldownSeconds": 0.45,
  "assumedAxialShare": 0.5,
  "minimumRetractRatio": 0.03
}
```

技能参数：

| 参数 | 当前值 |
|---|---|
| 技能 | SK001 震山掌 |
| 手势 | OpenPalm |
| 掌心 | Forward |
| 动作 | Push |
| 最低置信度 | 0.78 |
| 输入最大年龄 | 0.25 秒 |
| 技能冷却 | 1.0 秒 |

## 10. 2026-10-16 最新代码改动

在现有操作日志结束后，磁盘上还有一批更新：

1. HUD 将关键提示从普通小字中独立出来，显示成更大的黄色提示。
2. 增加中文语音“把手靠近一些”。
3. 增加距离提示迟滞：
   - 掌宽低于 `0.08` 后提示太远。
   - 提示出现后，掌宽必须超过 `0.095` 才清除。
   - 新状态需要稳定 0.5 秒才切换。
4. 无手帧保持上一状态，避免提示闪烁。
5. 采集器工作时暂停普通 HUD 语音，避免两套语音重叠。

相关 EditMode 测试均为 48/48 通过：

- `editmode-hint2.xml`
- `editmode-tts.xml`
- `editmode-hyst.xml`
- `editmode-left.xml`

相应 Android 构建全部成功。最后一次构建：

| 项目 | 值 |
|---|---|
| 构建时间 | 2026-10-16 16:55 |
| APK | `Builds/Android/MagicMotion-Development.apk` |
| 大小 | 98,697,727 字节 |
| SHA-256 | `C864084FBFAB38ECC3A24857A6A37E90877B4DDB5AABD044978C74FFE7AD78D3` |
| 构建结果 | 0 错误、0 警告 |

当前没有找到这份最终 APK 已完成真机安装及提示、语音、迟滞、布局验收的书面证据。

## 11. 当前验证状态

| 范围 | 状态 |
|---|---|
| Unity 工程与核心架构 | 已完成 |
| Android APK 构建 | 已通过 |
| Boot 到 Battle 启动链路 | 已真机通过 |
| 摄像头权限允许/拒绝 | 已真机通过 |
| MediaPipe 初始化与手部帧处理 | 已真机通过 |
| 黄色技能弹体与石魔命中 | 已真机确认 |
| 五宫格位置适配 | 已通过 |
| 单次推掌重复施法修复 | 已通过 |
| 右手 45 次正式采集 | 已完成 |
| 采集数据导出和离线分析 | 已完成 |
| 稳健参数真机调试 | 已完成三轮 |
| 近距离约 0.5～0.8m 推掌 | 阶段性约 75% |
| 1m 及以上距离 | 当前不作为可靠范围 |
| 左手完整 45 次采集 | 未完成 |
| 独立 held-out 数据验证 | 未完成 |
| 双手识别 | 未完整验收 |
| 旋转与镜像 | 未完整验收 |
| TC-05～TC-14 全量验收 | 未完成 |
| 15 分钟稳定性 | 未完成 |
| 性能量化 | 未完成 |
| 最新大字提示/TTS/迟滞真机验收 | 未确认 |

当前总体状态：

> 核心玩法链路已经打通，推掌采集、分析和三轮近距离调参已经完成；当前可用距离限定为约 0.5～0.8 米，但左手、双手、独立验证集、完整性能稳定性测试，以及最新提示语音版本的真机验收仍未完成。因此整个 MVP 目前不能标记为“完整验收通过”。

## 12. 当前工程管理状态

Git 当前不是干净状态。

已修改：

```text
Assets/MagicMotion/Resources/Config/MotionConfig.json
Assets/MagicMotion/Scripts/Calibration/GestureDataRecorder.cs
Assets/MagicMotion/Scripts/Config/MotionConfig.cs
Assets/MagicMotion/Scripts/UI/DebugHud.cs
```

未跟踪：

```text
Assets/MagicMotion/Scripts/UI/SpeechFeedback.cs
Assets/MagicMotion/Scripts/UI/SpeechFeedback.cs.meta
```

仓库当前只有一个初始化提交：

```text
1cfd277 初始化 MagicMotion Unity 项目仓库
```

后续接手前应先检查当前未提交差异，完成最终 APK 真机验收，再形成明确的阶段提交。

## 13. 遗留风险

1. 当前约 75% 的近距离成功率来自同一位用户和同一台设备，泛化能力未知。
2. 当前正式采集只有右手，左右手是否存在系统性差异尚不清楚。
3. 当前参数主要在已参与分析的数据上得到，缺少独立保留数据验证。
4. 1 米以上距离的推掌与干扰动作在现有特征上重叠，短期内不宜继续降低门槛。
5. 最终 HUD、语音和迟滞版本只有测试与构建证据，缺少正式真机验收记录。
6. MediaPipe 检测到手后 FPS 曾下降到约 19.3，主线程同步推理仍是性能瓶颈。
7. 完整 TC-05～TC-14 测试尚未结束，不能把局部通过写成整个 MVP 通过。
8. APK 体积约 98.7 MB，仍可继续分析模型、原生库和符号文件占用。

## 14. 建议的下一步

### P0：验证最后 APK

1. 安装 2026-10-16 16:55 的 APK。
2. 验证太远提示、中文语音、提示迟滞和布局。
3. 确认采集器工作时不会与普通语音重叠。
4. 保存截图、logcat和测试结论。

### P1：近距离独立复测

1. 使用未参与标定的新动作。
2. 在 0.5～0.8 米完成至少 20 次推掌。
3. 完成至少 20～40 次干扰动作。
4. 分别统计 Motion、Cast、重复触发和误触发。

### P1：补充左手验证

1. 先完成左手快速正例与负例验证。
2. 如发现明显左右手差异，再补完整左手 45 次采集。

### P1：完成正式验收

- 左右手识别。
- 双手识别。
- 镜像与旋转。
- SK001 延迟与成功率。
- 战斗闭环。
- 性能数据。
- 15 分钟稳定性。

### P2：文档与版本管理

1. 将后续三轮调参、远距离方案失败与回退、HUD/TTS改动合入主交接文档。
2. 检查未提交代码差异。
3. 真机验收通过后创建阶段提交。
4. 明确记录代码完成、测试通过、构建成功、安装成功和真机验收五种不同证据。

## 15. 关键文件索引

```text
docs/工作交接-2026-10-15.md
docs/手势数据采集与分析指南.md
docs/MagicMotion_CodeBuddy真机测试文档.md
docs/log/gesture-calibration/2026-10-15-HONOR-ALP-AN00/操作日志.md
docs/log/gesture-calibration/2026-10-15-HONOR-ALP-AN00/分析报告-push-20261015-130043.md
docs/log/device-test/2026-10-15-HONOR-ALP-AN00-round5/
Logs/GestureRecordings/push-20261015-130043/
Assets/MagicMotion/Scripts/Motion/PushMotionTracker.cs
Assets/MagicMotion/Scripts/Vision/MediaPipeHandTrackingProvider.cs
Assets/MagicMotion/Scripts/Vision/PalmDirectionClassifier.cs
Assets/MagicMotion/Scripts/Skill/SkillManager.cs
Assets/MagicMotion/Scripts/Calibration/GestureDataRecorder.cs
Assets/MagicMotion/Scripts/UI/DebugHud.cs
Assets/MagicMotion/Scripts/UI/SpeechFeedback.cs
Assets/MagicMotion/Resources/Config/MotionConfig.json
Assets/MagicMotion/Resources/Config/Skills.json
Tools/GestureCalibration/analyze_push_samples.py
```
