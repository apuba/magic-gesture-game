# Magic Motion 开发交接（2026-10-23，CodeBuddy 接手）

> 本文是当前“魔法教室 / 符箓跟随最小闭环”的接手说明。目标是让 CodeBuddy 能在不误回滚既有手势与坐标修复的前提下继续定位和完成符箓 UI。本文记录的是当前真实工作区状态，不代表真机验收已通过。

## 1. 工作目录与安全边界

- 唯一代码与构建目录：`E:\2026\MagicMotion`
- Unity：`2022.3.62f1`
- Android：ARM64 / IL2CPP
- 包名：`com.magicmotion.game`
- 当前分支：`main`
- 当前工作区有未提交修改；接手前必须运行 `git status --short`，不得 `git reset --hard`、不得从旧中文目录或 junction 回填文件。
- 既有坐标修复必须保留：所有手部坐标、距离、角度都要沿用 `HandTrackingFrameTransform`、`HandGeometry.SquareDistance` 和 `HandTrackingResult.WidthToHeight` 链路。

## 2. 用户当前反馈与本轮目标

用户在真机上能看到摄像头和手部骨架，但没有看到符箓。用户确认安装包与电脑构建时间匹配，因此“安装了旧包”不再是当前默认解释。

本轮目标是先把问题拆成可观测状态：

1. 让设置按钮下显示固定校验码，确认运行的是本次 APK。
2. 显示符箓组件是否收到结果、是否看到有效手、是否进入绘制状态。
3. 在骨架可见时先实现无手势门槛的基础符箓跟随，排除剑指/拇指/指尖间距等识别因素。
4. 真机确认可见后，再恢复翻掌、方向选符、元素锁定和 Push 释放。

## 3. 当前已实现代码

### 3.1 符箓跟随组件

- `Assets/MagicMotion/Scripts/UI/RuneFollowTracker.cs`
  - 纯逻辑显示状态：约 0.2 秒稳定后显示，约 0.18 秒短暂丢帧容错。
  - `CalculateTarget` 使用食指尖和中指尖中点，并沿两指根部到指尖方向向前偏移约 0.32 个掌宽。
  - 该类不负责技能、战斗、方向选择或翻掌。

- `Assets/MagicMotion/Scripts/UI/RuneFollowView.cs`
  - 订阅 `GameManager.Events` 的 `HandTrackingResult`。
  - 当前阶段只要结果中存在有效 `HandData`，就选择置信度最高的手，不再要求剑指判定。
  - 使用已有 `HandTrackingFrameTransform.LandmarkToGuiNormalized` 与 `HandTrackingOverlayLayout.Fit` 在摄像头画面上绘制。
  - 通过 `OnGUI` 绘制金色基础符箓（纸张、中心符纹、轻微呼吸/漂浮）。
  - `BuildMarker = "RF-CHECK-A2"`。
  - 暴露诊断属性：`ReceivedResultCount`、`ValidHandsInLatestResult`、`IsRuneVisible`、`HasRenderAnchor`。

- `Assets/MagicMotion/Scripts/Core/MagicMotionBootstrap.cs`
  - 已加入 `root.AddComponent<RuneFollowView>()`。

### 3.2 设置按钮下诊断码

- `Assets/MagicMotion/Scripts/UI/HandTrackingDebugView.cs`
  - 设置按钮下方显示：

    `RF-CHECK-A2`

    `events=… hands=… visible=… anchor=…`

  - 含义：
    - `events`：符箓组件收到的 `HandTrackingResult` 数量，应持续增长。
    - `hands`：最近一帧的有效手数量；骨架可见时通常应为 `1` 或 `2`。
    - `visible`：跟随状态机是否已进入显示状态。
    - `anchor`：是否已有渲染锚点。
  - 如果显示 `component=missing`，说明启动装配或脚本未进入运行时。

## 4. 最新验证证据

### 自动化测试

- 最新 EditMode：`Logs/editmode-rune-marker.xml`
- 结果：84/84 通过。
- 符箓相关用例位于 `Assets/MagicMotion/Tests/EditMode/RuneFollowTrackerTests.cs`。

### 最新 Android 构建

- 日志：`Logs/android-rune-marker.log`
- 构建结果：`Build Finished, Result: Success.`
- APK：`Builds/Android/MagicMotion-Development.apk`
- 构建时间：2026-10-23 14:27:46（本地文件时间）
- SHA-256：`2A37C5755ACED2268A6BAD9BBAB1C2092E752DD2747E0A8B1AF64E191880678E`

### 重要验收边界

- 自动化测试通过 ≠ APK 已安装 ≠ 真机视觉验收通过。
- 当前用户仍反馈“骨架可见但没有符箓”；因此本功能必须继续定位，不能在交接时写成已完成。
- 尚无符箓真机截图、状态码截图、安装后 logcat 或 15 分钟稳定性证据。

## 5. CodeBuddy 接手后的第一步

1. 确认工作区：

   ```powershell
   Set-Location E:\2026\MagicMotion
   git status --short
   git rev-parse --show-toplevel
   ```

2. 使用上节 SHA-256 校验 APK，然后覆盖安装到目标设备；不要只根据文件名判断。
3. 启动后截取设置按钮下方的 `RF-CHECK-A2` 状态码和骨架画面。
4. 按状态码分流：

   | 状态 | 判断 | 下一步 |
   |---|---|---|
   | 没有 `RF-CHECK-A2` | 运行包不是本轮构建或调试叠加未显示 | 先确认 APK/开发叠加层，不改识别代码 |
   | `component=missing` | `RuneFollowView` 未装配或未启动 | 查 `MagicMotionBootstrap`、脚本编译与启动日志 |
   | `events=0` | 没订阅到结果或 VisionPipeline 未发布 | 查 `GameManager.Events` 初始化顺序和 `VisionPipeline` 发布 |
   | `events>0, hands=0` | 结果到了但 `HandData` 无效 | 查 MediaPipe 结果、`HandShapeValidator`、帧宽高比链路 |
   | `hands=1, visible=0` | 跟随状态机没有稳定进入 | 查时间戳、主线程更新、`RuneFollowTracker` |
   | `visible=1, anchor=1` 但无符箓 | 事件和状态都正常，问题在 `OnGUI`/坐标/深度/遮挡 | 临时改为屏幕固定位置绘制金色测试块，再逐层恢复坐标 |

5. 只有确认 `visible=1, anchor=1` 且屏幕能看到基础符后，才继续 P0/P1：剑指/翻掌序列、四向选择、元素符、Push 互斥路由。

## 6. 预计需要优先检查的代码点

1. `RuneFollowView.OnGUI` 是否真的被调用，以及 `cameraManager.Texture` 是否非空。
2. `GUI.depth`、`GUI.color`、`GUI.matrix` 是否被其他 `OnGUI` 组件覆盖；可先在固定屏幕位置绘制一个高对比色矩形作为绘制层探针。
3. `HandTrackingFrameTransform.LandmarkToGuiNormalized` 返回的坐标是否与 `HandTrackingDebugView.MapPoint` 一致；不要重新发明一套镜像/旋转公式。
4. `RuneFollowView` 是否因为 `lastResultAt` 使用 `Time.realtimeSinceStartupAsDouble` 与事件线程时序导致频繁 `Reset`。
5. 符箓当前尺寸按 `content` 短边比例计算；若实际尺寸太小或落在黑边外，先把固定位置测试块显示出来再调整。

## 7. 尚未实现的功能（不要误认为已完成）

- 手背剑指起手与同手翻掌序列。
- 空白符出现前后的正式状态机和单次会话 ID。
- 上/下/左/右映射为雷/火/水/风。
- 半透明四向选择法阵 UI。
- 元素符待命、切掌过渡、Push 释放路由。
- `SK001` 与元素技能互斥、元素命中表现。
- 左右手、横竖屏、长时间稳定性与真机性能验收。

## 8. 当前未提交改动概览

接手时不要覆盖这些文件中的用户现有修改：

- 用户已有改动：`GestureDataRecorder.cs`、`DebugHud.cs`、`HandTrackingDebugView.cs`、`TimedHandTrail.cs` 及对应测试。
- 本轮新增/修改：`MagicMotionBootstrap.cs`、`SwordFingerDetector.cs`、`SwordFingerDetectorTests.cs`、`RuneFollowTracker.cs`、`RuneFollowView.cs`、`RuneFollowTrackerTests.cs`。
- 规范文档：`docs/Magic Motion 符文施法系统与四系符文 UI  特效规范 V1.0.md`。

在真机符箓可见之前，不要提交、推送或整理成“已完成”版本；先保留诊断状态和截图证据。

## 9. 参考文档

- `docs/工作交接-2026-10-23-Codex.md`
- `docs/工作交接-2026-10-22-CodeBuddy.md`
- `docs/项目现状报告-2026-10-23.md`
- `docs/手势识别基线-v0.1.0.md`
- `docs/Magic Motion 符文施法系统与四系符文 UI  特效规范 V1.0.md`
- `docs/MagicMotion_项目工作详细总结_2026-10-20.md`

## 10. 2026-10-23 下午进展（CodeBuddy）：符箓真机可见，根因已修复

### 10.1 探针轮（RF-CHECK-A3）

按第 5 节分流表加入绘制层探针后真机确认：

- 状态码：`events=980 hands=1 visible=1 anchor=1`，`draw=drew 1266/545 s1.50`。
- 屏幕左侧中部洋红探针可见，指尖上方金色符纸可见。
- 结论：事件链、状态机、坐标映射全部正常，问题只在 IMGUI 排序。

### 10.2 根因

`RuneFollowView.OnGUI` 原先在 `DrawBlankRune` 内部中途设置 `GUI.depth = -8`，结尾又恢复为 0。IMGUI 对 depth 的处理是：中途修改只在下一轮 OnGUI pass 生效，且排序取的是该组件整轮 pass 结束时的值。因此符箓组件实际始终以 depth 0 参与排序，被后画的全屏相机覆盖层（`HandTrackingDebugView.OnGUI`）整个盖住——这就是「状态码全对但符箓不显示」的唯一原因。

### 10.3 修复与当前状态

- `GUI.depth = -99` 移到 `OnGUI` 开头一次性设置、不再恢复。
- 临时探针（洋红固定块、红色坐标点）已移除；`DrawState` 诊断行（`draw=…`）保留，便于后续“看不到符箓”类反馈一张截图定位。
- 构建标记升级为 `RF-CHECK-B1`。
- 验证：EditMode 84/84 通过（`Logs/editmode-rune-b1.xml`）；Android 构建成功（`Logs/android-rune-b1.log`）；APK `Builds/Android/MagicMotion-Development.apk`，2026-10-23 15:19:56，SHA-256 `D81CF000B3DD6BDD3F75780475DD2742FFC741B18F3F3B1DB879226538E719EF`。

### 10.4 下一步（按第 7 节顺序）

基础符箓已真机确认可见，可以开始 P0/P1：剑指/翻掌起手序列、四向状态机、元素符与 Push 释放路由。第 3 节“无手势门槛跟随”的临时策略在四向功能落地后应替换为正式状态机。

## 11. 2026-10-23（第二轮）：手势门禁，只有动作正确才激活符箓【已实现，待真机标定】

依据 `docs/02_手势与技能系统/MagicMotion_项目完整阶段总结与MVP0.2交接文档_2026-10-23.md` 第 3.1 节（① 剑指起手、② 翻掌召符）与第 8 节 P0。

### 11.1 本轮新增

| 文件 | 作用 |
|---|---|
| `Scripts/Gesture/HandFacing.cs` | 朝向枚举：`Unknown / BackToCamera / SideToCamera / PalmToCamera` |
| `Scripts/Gesture/HandFacingDetector.cs` | 把 `HandData.PalmNormal` 归约为三种朝向；上下朝向一律判为 `Unknown`，不计入翻掌阶段 |
| `Scripts/Gesture/FlipSequenceRecognizer.cs` | 状态机 `Idle → BackHeld → SideSeen → Ready`，要求有序 `Back → Side → Palm` 才算召符 |
| `Tests/EditMode/FlipSequenceRecognizerTests.cs` | 7 条用例覆盖顺序、窗口、起手时长与待命丢失 |

### 11.2 门禁规则（对应文档 §3.1 ①②）

1. **起手**：必须同时满足剑指与 `BackToCamera`（手背朝摄像头），并稳定 `startHoldSeconds = 0.3s`（文档建议 0.25–0.4s）才进入 `BackHeld`。
2. **翻掌**：必须依次出现 `SideToCamera` 再 `PalmToCamera`，且自起手完成起总计不得超过 `flipWindowSeconds = 1.2s`（文档建议 0.2–1.2s）。
3. **单帧手心不可召符**：从 `BackHeld` 直接读到 `PalmToCamera` 视为误判，序列立即作废（文档明确要求）。
4. **末端稳定**：手心朝向需持续 `endHoldSeconds = 0.2s`（文档建议 0.15–0.25s）才 `Ready`，符箓才出现。
5. **全程保持剑指**：丢失超过 `lostGraceSeconds = 0.12s` 即取消，容忍单帧抖动。

### 11.3 接入方式

`RuneFollowView.HandleResult` 由原先「存在有效手即显示」改为：先用 `SwordFingerDetector.IsRuneFinger` 判定手型，再用 `HandFacingDetector.Detect` 判定朝向，两者喂给 `FlipSequenceRecognizer`，仅当 `Ready` 时才把目标点交给 `RuneFollowTracker`。识别坐标与显示坐标仍分开管理（文档 §3.3）。

手型判定**故意不看拇指**：起手正是手背朝镜头，拇指被遮挡后会被推断为伸直并逐帧跳变（工程内已有实测结论）。约束由朝向承担。

### 11.4 真机验收方法

状态码第三行现为 `draw=sword=1 face=back flip=Ready drew x/y s缩放`：

| 显示 | 含义与处理 |
|---|---|
| `face=?` | 法线未落入前后左右四区，多为倾角过大，需真机标定阈值 |
| `flip=Idle` | 未满足起手，检查是否手背朝镜头 |
| `flip=BackHeld` | 起手已成立，等待翻到侧面 |
| `flip=SideSeen` | 侧面已过，等待手心并稳住 0.2s |
| `flip=Ready` | 已召符，符箓应出现在指尖前方 |

### 11.5 验证

- EditMode **91/91 通过**（`Logs/editmode-flip-gate.xml`，新增 7 条）
- Android 构建成功（`Logs/android-flip-gate.log`），APK 2026-10-23 15:36 左右生成

### 11.6 参数放宽（2026-10-23 同轮调整）

上一版取的是文档建议区间的严格端，真机上手偏难，已统一放宽。下表为当前代码默认值：

| 参数 | 原值 | 现值 | 方向 |
|---|---|---|---|
| 起手稳定 `startHoldSeconds` | 0.3s | **0.2s** | 缩短 |
| 翻掌窗口 `flipWindowSeconds` | 1.2s | **2.0s** | 放宽 |
| 末端手心稳定 `endHoldSeconds` | 0.2s | **0.12s** | 缩短 |
| 丢手型宽限 `lostGraceSeconds` | 0.12s | **0.25s** | 加长 |
| 强制过侧面 `requireSideStage` | 恒 true | **默认 false** | 降为可选项 |

**顺序规则的取舍**：文档 §3.1 ② 要求 `Back → Side → Palm` 有序。现在默认**不再强制侧面阶段**——追踪帧率下手的侧面常常正好落在两帧之间被漏采，拒绝这类施法给玩家的感觉是"游戏坏了"。防误触改由**末端必须稳住 0.12s** 承担，单独一帧手心仍然召不出符。若后续误触发率实测偏高，用 `new FlipSequenceRecognizer(requireSideStage: true)` 即可回到严格模式。

放宽后 EditMode **92/92 通过**（新增 1 条严格模式用例），构建成功（16:00:31，SHA-256 `A17A4F04...774D`）。

## 12. 2026-10-23（第三轮）：正式符箓贴图替换占位图形【已实现，待真机确认观感】

素材来源：`E:\2026\MagicMotion\ui\MagicMotion_Rune_UI_V1_PNG`

| 文件 | 用途 | 状态 |
|---|---|---|
| `Rune_Blank.png` | 基础符（当前绘制） | **已接入** |
| `Rune_Thunder.png` | 雷符（↑） | 待四向选符接入 |
| `Rune_Fire.png` | 火符（↓） | 待四向选符接入 |
| `Rune_Water.png` | 水符（←） | 待四向选符接入 |
| `Rune_Wind.png` | 风符（→） | 待四向选符接入 |
| `古寺五行符箓幻境.png` | 练习室背景概念图 | 未接入，待 P2 页面 |

### 12.1 改动

- 贴图复制到 `Assets/MagicMotion/Resources/Runes/`（Resources 目录，便于运行时按路径兜底加载，同时避免将来被裁剪）。
- `ProjectBootstrap` 新增 `EnsureRuneTextures()`：统一设为 **Default 类型、无 mipmap、不强制 POT（nPOTScale=0）、最大 2048、透明度保留、Clamp**，保证符纸边缘不被拉成硬矩形。
- `RuneFollowView`：新增 `[SerializeField] private Texture2D runeTexture`，`Start` 中未指定时按 `Runes/Rune_Blank` 兜底加载；`DrawBlankRune` 的矩形+朱砂笔画占位图形**改为绘制贴图**，保留悬浮摆动与呼吸透明度；贴图**按原始宽高比**换算宽度（沿用占位图形 116 单位的屏上高度），不会被拉伸。
- 贴图缺失时 `DrawState` 显示 `no-rune-texture`，不再静默失败。
- 构建标记：**`RF-CHECK-B2`**。

### 12.2 验证

- EditMode **92/92 通过**（`Logs/editmode-rune-art.xml`）
- Android 构建成功（`Logs/android-rune-art.log`），APK 2026-10-23 16:05:19，SHA-256 `5D32DC99...19B7`

### 12.3 待办（不要当作已完成）

- 四元素符贴图**已入库但未接线**，需等四向选符（P1）完成后按方向切换
- 背景概念图未使用
- 符纸在指尖前方的**偏移量、贴图实际屏上大小、旋转角度**未经真机观感确认，可能需要按手感调整
- 释放时的过渡（符纸聚拢到掌前）未实现

### 12.4 上一节遗留待办（11.7）

- 翻掌时间窗口下界（文档建议 0.2s）**未实现**，避免误伤快速施法，待真机数据决定
- `RuneReady` 内剑指→张掌的**释放准备过渡**未实现，当前丢剑指即取消；按文档 §3.5 该过渡应与 Push 路由一起做
- 四向选符（上雷/下火/左水/右风）、元素符锁定、Push 释放与 `SK001` 互斥路由均未开始
- `activeHand` 锁定（文档 §7.1 规则 3）尚未实现，当前仍按置信度选手
- 所有识别参数均为文档建议初值，**未经真机标定**

## 13. 2026-10-23（第四轮）：完整施法链 + 符箓按手掌比例缩放【已实现，待真机标定】

### 13.1 目标

翻掌召符 → 四方向选符 → 符文待命 → 推掌释放全链路打通；符箓大小不再固定，改为按手掌大小成比例，并考虑 z 轴。

### 13.2 改动

- `Gesture/RuneElement.cs`：元素枚举 `None/Thunder/Fire/Water/Wind`。
- `Gesture/DirectionSelectionRecognizer.cs`：四向选符（上雷/下火/左水/右风）。位移 0.15（短边为单位）、主轴优势 1.25（**纯对角不判定**）、确认 0.12s、期限 1.2s 超时取消整次施法。
- `Gesture/RuneCastStage.cs` + `RuneCastStateMachine.cs`：`Idle → SwordReady → Flipping → BaseRuneReady → RuneReady → Releasing → Cooldown → Idle`；待命超时 4s、释放窗口 1.5s、冷却 0.6s。
- `Gesture/RuneCastController.cs`：按帧驱动各识别器与状态机，锁定 `activeHand`，识别坐标与显示坐标分开管理。
- `Gesture/FlipSequenceRecognizer.cs`：新增 `Hold(bool)`，待命→释放过渡期间不因丢剑指而取消。

**识别与显示坐标分离**

- 方向判定取**掌心**位置（抗指节噪声）；符箓绘制取**指尖前方**锚点，释放准备期切到**掌前**锚点。
- 方向判定在**屏幕坐标**下做（`-y` 为向上），所以手机怎么转、图像是否镜像，"上"始终是玩家眼中的上。
- 距离统一折算为"画面短边为单位"，16:9 与竖屏下同一段手部移动的数值一致。

**z 轴与尺寸**

- `HandGeometry.CalculateFingerSpan(hand, wth, depthWeight)`：指尖到手心的跨度，方形单位下取 3D 长度，深度按权重计入。权重 0 = 只按屏幕投影大小，1 = 按真实空间长度，默认 **0.5**——手指向镜头时长度主要在 z 上，忽略会让符箓缩成一点；但 landmark 的 z 噪声最大，取满会让符纸随手腕倾斜而"呼吸"。
- 高度 = 跨度 × 屏幕像素单位 × `runeHeightScale`；宽度 = 高度 × 贴图原始宽高比（换图无需改代码）。
- 钳制 36–420px 仅用于挡住"手贴镜头/远到看不清"两种极端；尺寸平滑 0.09s，比位置跟随 0.035s 慢，避免逐帧抖动。

**互斥路由**

- `SkillManager.HandleMotionDetected` 开头：`HasPendingRune` 为真直接返回。事件总线会把 Push 广播给所有订阅者，震山掌只能在匹配前让位，否则一次推掌放两个法术。
- `SkillExecutor` 订阅 `RuneReleased`：**复用震山掌特效占位**（四元素弹道/命中反馈待美术资源），日志 `[MagicMotion.Rune] released element=... hand=...` 便于真机确认。

**视图**

- `RuneFollowView` 改为纯渲染层（仅平滑 + 按元素选贴图）；四元素贴图已接线：`BaseRuneReady` 用 `Rune_Blank`，`RuneReady`/`Releasing` 用对应元素符。
- 诊断行：`stage=<阶段> element=<元素> drew x/y h<高度>`；构建标记 **`RF-CHECK-B3`**。

### 13.3 验证

- EditMode **111/111 通过**（`Logs/editmode-rune-size2.xml`）；新增 `DirectionSelectionRecognizerTests`（8）、`RuneCastStateMachineTests`（8）、`HandGeometryTests` 跨度用例（3）
- Android 构建成功（`Logs/android-rune-size.log`），APK 2026-10-23 17:39:20，SHA-256 `F57087CA...4561`

### 13.4 待办（不要当作已完成）

- 全部阈值（含尺寸比例 1.0、深度权重 0.5）**均为文档建议初值，未经真机标定**
- 翻掌时间窗口下界（文档建议 0.2s）仍未实现
- 四元素法术无各自弹道/命中效果，当前复用震山掌占位
- 释放时的符纸聚拢动画未实现（仅锚点从指尖前切到掌前）
- 需真机确认"张开手掌"是否会被 `MotionAnalyzer` 误判为一次 Push，进而影响释放节奏
