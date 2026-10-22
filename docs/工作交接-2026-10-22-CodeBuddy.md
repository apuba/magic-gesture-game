# Magic Motion 工作交接（2026-10-22）

## 1. 唯一工作目录

后续开发、测试、构建和安装只使用：

`E:\2026\MagicMotion`

不要再使用或引用：

`E:\2026\魔法手势游戏`

该目录曾被其他 AI 继续修改，已迁移其中确认需要的代码和资源，但不再作为构建源。

## 2. 当前代码状态

当前分支：`main`

最近提交：

- `64d4c43`：叠加层设置面板与双色轨迹调试渲染
- `bd2fbdd`：剑指判定与符文轨迹采样优化
- `c3d68ee`：手形过滤与帧变换修复
- `2e75423`：手部轨迹与剑指画符开发总结

当前存在未提交修改，这是本次低延迟修复的一部分，禁止直接丢弃：

- `VisionPipeline.cs`
- `MediaPipeHandTrackingProvider.cs`
- `HandTrackingDebugView.cs`
- `DebugHud.cs`
- `OverlaySettings.cs`
- `GameConfig.cs`
- `GameConfig.json`
- `HandTrackingFrameTransformTests.cs`
- `SwordFingerDetectorTests.cs`

## 3. 本轮已完成的修复

### 重复画中画摄像头

`DebugHud` 原来额外绘制了一路摄像头预览，而 `HandTrackingDebugView` 已绘制全屏摄像头，导致竖屏出现画中画。

已删除 `DebugHud` 的重复摄像头绘制，只保留全屏摄像头和诊断文字。

### 调试信息开关

“显示信息文字”现在同时控制：

- `HandTrackingDebugView` 的状态文字
- `DebugHud` 的诊断面板

### 设置按钮文字

设置按钮使用 `Resources/Fonts/SimHei.ttf`，并显式预热“设置”字形，避免初始化阶段文字为空。

### 骨架与手掌跟踪延迟

本轮确认延迟来自多层叠加：

- 视觉处理原先固定为 15 FPS
- 设置里的跟踪频率没有真正接入 `VisionPipeline`
- 新手部结果需要连续 3 次命中才发布
- `LandmarkSmoother` 使用了 0.65 强平滑
- 显示层还追加了 50 ms 平滑

当前调整为：

- `VisionPipeline` 支持运行时 `TrackingRate`
- 默认跟踪频率 30 FPS
- `OverlaySettings` 的跟踪频率真正作用到 `VisionPipeline`
- `RequiredSightings` 从 3 调整为 1，仍保留 `HandShapeValidator`
- `landmarkSmoothing` 从 0.65 调整为 0.2
- 显示层平滑从 50 ms 调整为 25 ms
- 轨迹跟随平滑从 50 ms 调整为 25 ms
- 旧设备上的跟踪频率 PlayerPrefs 做一次性迁移到 30 FPS

配置文件当前为：

- 摄像头：1280×720，30 FPS
- 视觉跟踪：30 FPS
- 默认前置摄像头：开启

## 4. 测试与构建证据

EditMode 测试已通过：

- 总数：73
- 通过：73
- 失败：0
- 报告：`E:\2026\MagicMotion\Logs\editmode-low-latency-20261022.xml`

Android 构建已成功：

- 输出：`E:\2026\MagicMotion\Builds\Android\MagicMotion-Development.apk`
- Unity：2022.3.62f1
- 架构：ARM64
- 包名：`com.magicmotion.game`

Unity 构建日志中有许可证过期提示，但构建本身返回成功；后续如需正式发布，应先处理 Unity 许可证状态。

## 5. 真机安装状态

USB 设备：

`AXYP6R4A30002818`（HONOR ALP-AN00）

最新 APK 已执行覆盖安装并返回 `Success`，随后已启动应用，进程可见。

自动化安装成功不等于视觉验收完成。需要在手机上重点确认：

1. 快速左右移动手掌时，骨架是否连续跟随。
2. 手掌快速靠近或离开时，是否仍有明显滞后。
3. 单指和剑指切换时，轨迹是否出现跳跃。
4. 竖屏下骨架、掌心、指尖是否仍与真实位置重合。
5. 设置按钮文字是否稳定显示。
6. 关闭“显示信息文字”后，两个诊断信息层是否都消失。

## 6. 当前已知风险

- 当前低延迟参数已通过自动化测试，但真实体感延迟必须在目标手机上验收。
- `MediaPipeHandTrackingProvider` 中 `gpuActive` 当前只有赋值，存在未使用字段警告，不影响构建。
- 构建日志仍有 Unity Licensing Client 的 token/ULF 过期提示。
- 当前工作区存在未提交修改，CodeBuddy 接手时应先执行 `git status --short`，不要重置或覆盖这些修改。
- 不要从旧目录复制回文件，除非先进行明确的逐文件对比。

## 7. 建议 CodeBuddy 的下一步

1. 先阅读本文件、`docs/MagicMotion_项目工作详细总结_2026-10-20.md` 和当前 `git diff`。
2. 在不改变低延迟参数的前提下重新检查竖屏坐标链：摄像头旋转、镜像、MediaPipe 输入和 GUI 映射必须使用同一套定义。
3. 使用同一台 HONOR ALP-AN00 做冷启动和连续快速动作测试。
4. 记录 `Tracking FPS`、`Display FPS`、结果年龄和掉帧现象，再决定是否继续调参。
5. 只有在真机视觉验收通过后，才整理提交；不要自动推送远程仓库。

