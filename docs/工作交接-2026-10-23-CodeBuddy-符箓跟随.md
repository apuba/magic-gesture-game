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
