# Magic Motion CodeBuddy 连机真机测试文档

## 1. 文档目的

本文档交由 CodeBuddy 执行 Magic Motion Android 真机测试，用于回答四个问题：

1. APK 能否在目标手机上安全安装并正常启动；
2. 摄像头与 MediaPipe Hand Landmarker 是否在真机上真实运行；
3. `OPEN_PALM + 掌心向前 + PUSH` 是否能稳定触发 `SK001 震山掌`；
4. 震山掌是否完成“释放 → 命中石妖 → 扣血 → 击败”的 MVP 闭环。

构建成功不等于真机合格。CodeBuddy 必须分别给出安装、启动、识别、技能、战斗、性能和稳定性证据，不得只依据 APK 存在、应用能打开或日志中出现一次触发就判定通过。

## 2. 测试对象

- 项目目录：`E:\2026\魔法手势游戏`
- APK：`E:\2026\魔法手势游戏\Builds\Android\MagicMotion-Development.apk`
- 应用包名：`com.magicmotion.game`
- Unity：`2022.3.62f1`
- Android 最低版本：Android 8.0 / API 26
- 架构：ARM64
- 脚本后端：IL2CPP
- MediaPipe 插件：`homuler/MediaPipeUnityPlugin v0.14.4`
- 手部模型：Hand Landmarker，最多两只手
- APK 基准大小：75,543,958 字节
- APK 基准 SHA-256：`81043A456F778F3D9C1AD7770DE226E4269912172D1297A55493E511317DC711`

测试开始前必须重新计算 APK 哈希。若 APK 已被重新构建，允许哈希变化，但报告中必须记录实际测试文件的大小、修改时间和 SHA-256，不能继续引用旧值。

## 3. 测试目标与合格定义

### 3.1 MVP 总目标

首次使用者能在 5 分钟内完成：

```text
打开应用
  ↓
授权摄像头
  ↓
看到系统识别手部
  ↓
完成张掌、掌心向前、快速推掌
  ↓
释放震山掌
  ↓
命中石妖并产生扣血反馈
  ↓
重复施法并击败石妖
```

### 3.2 总体合格条件

只有同时满足以下条件，才能将本轮真机测试结论写为“合格”：

- P0 测试项全部通过；
- 摄像头成功率 100%；
- 单手张掌识别准确率不低于 90%；
- 左右手分类准确率不低于 90%；
- 双手同时进入画面时能稳定输出两只手；
- 规范动作下震山掌触发成功率不低于 85%；
- 静止张掌、握拳、横向挥手等负例不得触发震山掌；
- 每次有效技能只结算一次伤害；
- 石妖能被持续攻击至战斗结束；
- 中端目标设备运行 FPS 平均不低于 30，且无持续性低于 24 FPS；
- 连续运行 15 分钟无崩溃、无 ANR、无摄像头永久失效、无明显内存持续增长；
- 无 MediaPipe 原生库、模型加载、权限或 IL2CPP 致命错误；
- 测试报告包含规定的日志、截图/录屏和次数统计。

任一 P0 项失败，总结论必须是“不合格/阻塞”，不得通过调试手段绕开后将完整 MVP 标记为通过。

## 4. 测试环境准备

### 4.1 手机要求

优先准备一台中端 Android 手机作为主验收机；如条件允许，再增加高端和低端设备。

每台设备记录：

- 品牌与型号；
- Android 版本；
- 处理器/SoC；
- RAM；
- 屏幕分辨率和刷新率；
- 前置摄像头方向是否正常；
- 测试时电量；
- 测试时是否充电；
- 室温和网络状态；
- 是否开启省电模式、游戏模式或性能模式。

主测试时关闭省电模式，保持正常系统刷新率，清理镜头，使用稳定室内光线。不要开启会修改画面、悬浮窗或摄像头行为的第三方工具。

### 4.2 电脑要求

- 已安装 ADB；可使用 Unity Android SDK 中的 `adb.exe`。
- 手机已开启开发者选项和 USB 调试。
- 使用可传输数据的 USB 线连接电脑。
- 手机屏幕保持解锁。
- 首次连接时，由用户在手机上确认 USB 调试授权。

Unity SDK 中 ADB 的参考路径：

```powershell
E:\Unity\Hub\Editor\2022.3.62f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe
```

下文以 `$adb` 表示该路径。CodeBuddy 可以按实际环境替换，但报告中要记录最终使用的 ADB 路径与版本。

## 5. 连接手机

### 5.1 检查 ADB 版本

```powershell
$adb = 'E:\Unity\Hub\Editor\2022.3.62f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
& $adb version
```

### 5.2 启动服务并查看设备

```powershell
& $adb start-server
& $adb devices -l
```

合格结果必须满足：

- 只选择报告中明确记录的目标设备；
- 设备状态为 `device`；
- 不能是 `unauthorized`、`offline` 或空列表；
- 多台设备连接时，后续命令必须使用 `-s <serial>` 指定设备。

示例：

```powershell
$serial = '<从 adb devices -l 复制的设备序列号>'
& $adb -s $serial shell getprop ro.product.manufacturer
& $adb -s $serial shell getprop ro.product.model
& $adb -s $serial shell getprop ro.build.version.release
& $adb -s $serial shell getprop ro.build.version.sdk
& $adb -s $serial shell wm size
& $adb -s $serial shell dumpsys meminfo | Select-String 'Total RAM'
```

若设备未授权：让用户在手机上确认授权，然后重新执行 `adb devices -l`。不得通过绕过系统安全提示的方式继续。

## 6. APK 安装

### 6.1 记录待测 APK

```powershell
$apk = 'E:\2026\魔法手势游戏\Builds\Android\MagicMotion-Development.apk'
Get-Item -LiteralPath $apk | Select-Object FullName, Length, LastWriteTime
Get-FileHash -LiteralPath $apk -Algorithm SHA256
```

### 6.2 检查已有安装

```powershell
& $adb -s $serial shell pm list packages com.magicmotion.game
& $adb -s $serial shell dumpsys package com.magicmotion.game
```

如果应用已存在，优先保留应用数据并覆盖安装：

```powershell
& $adb -s $serial install -r $apk
```

如果出现 `INSTALL_FAILED_UPDATE_INCOMPATIBLE`、签名不一致或降级错误：

1. 立即停止；
2. 记录完整错误；
3. 不得擅自卸载旧应用；
4. 询问用户是否允许清除旧版本及其数据后再处理。

对于确认无重要数据的全新测试设备，可以在获得明确许可后卸载重装：

```powershell
& $adb -s $serial uninstall com.magicmotion.game
& $adb -s $serial install $apk
```

安装合格结果：命令返回 `Success`，且以下命令能查到包：

```powershell
& $adb -s $serial shell pm path com.magicmotion.game
```

## 7. 日志与证据采集

### 7.1 清理旧日志

在首次启动前执行：

```powershell
& $adb -s $serial logcat -c
```

### 7.2 启动应用

```powershell
& $adb -s $serial shell monkey -p com.magicmotion.game -c android.intent.category.LAUNCHER 1
```

### 7.3 实时观察关键日志

```powershell
& $adb -s $serial logcat -v time Unity:I AndroidRuntime:E ActivityManager:I libc:F '*:S'
```

建议同时保存完整日志：

```powershell
& $adb -s $serial logcat -v threadtime | Tee-Object -FilePath 'E:\2026\魔法手势游戏\Logs\device-logcat.txt'
```

测试结束后再保存一次静态快照：

```powershell
& $adb -s $serial logcat -d -v threadtime > 'E:\2026\魔法手势游戏\Logs\device-logcat-final.txt'
```

必须重点搜索：

```powershell
rg -n -i "FATAL EXCEPTION|ANR|SIGSEGV|DllNotFoundException|mediapipe|hand_landmarker|Camera|Permission|OutOfMemory|MissingMethod|EntryPointNotFound|MagicMotion" 'E:\2026\魔法手势游戏\Logs\device-logcat-final.txt'
```

### 7.4 截图、录屏与文件命名

证据建议存放在：

```text
docs/log/device-test/<日期>-<设备型号>/
```

至少提供：

- `01-install-success.png`：安装成功或包信息；
- `02-permission.png`：摄像头授权结果；
- `03-camera-running.png`：Camera/Vision Running；
- `04-one-hand.png`：单手及左右手识别；
- `05-two-hands.png`：双手同时识别；
- `06-open-palm.png`：`OPEN_PALM`；
- `07-push.png`：`PUSH`；
- `08-sk001.png`：`SK001`；
- `09-enemy-hit.png`：敌人扣血；
- `10-battle-complete.png`：战斗结束；
- 一段从摆手到技能命中过程的连续录屏；
- 完整 `device-logcat-final.txt`；
- 最终测试报告 Markdown。

截图或单条日志只能证明某一时刻状态，不能代替连续动作和完整闭环录屏。

## 8. 测试门禁与测试项

## TC-00：设备连接

目标：确保所有后续命令作用于正确设备。

步骤：

1. 执行 `adb devices -l`；
2. 记录 serial、型号和 Android 版本；
3. 多设备时显式使用 `-s`。

合格：目标设备状态为 `device`，设备身份与报告一致。

失败处理：停止测试，修复连接后重新开始。

## TC-01：APK 完整性与安装

目标：确认实际安装的是指定构建产物。

步骤：

1. 记录 APK 文件大小、时间和 SHA-256；
2. 执行保留数据覆盖安装；
3. 查询包路径与版本信息。

合格：安装返回 `Success`，包名为 `com.magicmotion.game`，设备能查询到应用。

## TC-02：冷启动与场景入口（P0）

目标：确认用户从启动图标进入后能到达可测试的训练或战斗体验，而不是停留在空白 BootScene。

步骤：

1. 强制停止应用；
2. 清理日志；
3. 从桌面图标或 monkey 冷启动；
4. 等待 10 秒；
5. 观察界面和 Debug HUD。

命令：

```powershell
& $adb -s $serial shell am force-stop com.magicmotion.game
& $adb -s $serial logcat -c
& $adb -s $serial shell monkey -p com.magicmotion.game -c android.intent.category.LAUNCHER 1
```

合格：

- 10 秒内进入可交互场景；
- 画面不全黑、不空白、不永久停留在 BootScene；
- Debug HUD 可见；
- 无崩溃或 ANR。

若应用只显示空场景，Camera 为 `Stopped`，且没有进入训练/战斗入口，则判定 P0 阻塞。必须记录画面和日志，不能手动通过 Unity Editor 切场景后将 APK 启动体验判为通过。

## TC-03：摄像头权限（P0）

目标：确认首次授权、拒绝和再次启动行为正确。

首次允许流程：

1. 在系统设置中确认应用尚未获得摄像头权限，或在允许清除测试数据的前提下重置权限；
2. 启动应用；
3. 用户在手机上点击“允许”；
4. 观察 Debug HUD 和日志。

合格：

- 系统权限框明确请求摄像头；
- 允许后 10 秒内 `Camera: Running`；
- 应用不崩溃；
- 实际使用前置摄像头。

拒绝流程：

1. 用户拒绝权限；
2. 确认应用不崩溃；
3. Debug HUD 显示权限相关提示。

拒绝权限属于预期负例，不纳入摄像头成功率；但应用崩溃或无限等待属于失败。

## TC-04：MediaPipe 初始化（P0）

目标：确认模型和 Android 原生库真实加载成功。

步骤：

1. 摄像头进入 Running；
2. 检查 Debug HUD 的 Vision 状态；
3. 检查日志中的初始化信息和异常。

合格：

- `Vision: Running`；
- 日志出现 MediaPipe Hand Landmarker 初始化成功信息；
- 不出现 `DllNotFoundException`、`EntryPointNotFoundException`、模型缺失、AAR/so 加载失败、SIGSEGV；
- 手进入画面后 `Hands` 从 0 变为 1 或 2。

## TC-05：画面旋转与镜像（P0）

目标：确认前摄像头输入方向、左右移动和关键点坐标一致。

步骤：

1. 竖屏正持手机；
2. 分别将手移向画面左、右、上、下；
3. 将手机旋转到系统允许的其他方向；
4. 观察手部识别、左右手标签和操作方向。

合格：

- 画面不倒置、不横向拉伸；
- 手向画面左移动时识别数据不会向右漂移；
- 左右手标签与镜像自拍语义一致；
- 旋转后识别仍能恢复；
- 不发生关键点持续错位或 Z 方向反转。

如项目当前没有关键点可视化，至少用左右手标签、手势变化和连续录屏验证；无法充分判断时结论写“待补可视化验证”，不能直接判通过。

## TC-06：单手 21 点与左右手识别

目标：验证单手输入的稳定性和左右手分类。

环境：手掌距手机约 40～100 cm，均匀室内光线，纯色或普通背景。

步骤：

1. 左手张开放入画面，保持 2 秒，重复 10 次；
2. 右手张开放入画面，保持 2 秒，重复 10 次；
3. 每次手完全离开画面后再开始下一次；
4. 记录是否检测到手、左右手标签、置信度和是否存在明显跳变。

合格：

- 20 次中至少 18 次在 1 秒内检测到手；
- 左右手标签至少 18/20 正确；
- 每次有效手数据包含 21 个关键点；
- 手离开画面后 `Hands` 能回到 0；
- 无旧手数据长期残留。

## TC-07：双手识别

目标：验证两只手同时输出。

步骤：

1. 双手同时张开放入画面，保持 2 秒；
2. 交换双手在画面中的左右位置；
3. 一只手遮挡另一只手后再分开；
4. 共执行 10 轮。

合格：

- 至少 9/10 轮能稳定显示 `Hands: 2`；
- 左右手不会在无交叉时频繁互换；
- 遮挡解除后 1 秒内恢复双手；
- 无崩溃和严重卡顿。

## TC-08：OPEN_PALM 识别准确率

目标：确认张掌手型可稳定识别。

步骤：

1. 左手完成 10 次标准张掌；
2. 右手完成 10 次标准张掌；
3. 每次保持 1 秒；
4. 每次之间完全放松或离开画面；
5. 记录 Debug HUD 是否显示 `Gesture=OpenPalm`。

合格：20 次中至少 18 次识别成功，准确率不低于 90%。

## TC-09：PUSH 动作识别

目标：确认向镜头快速推掌能被识别，普通移动不会误判。

正例步骤：

1. 手掌张开并朝向摄像头；
2. 从约 80 cm 处在 0.12～0.80 秒内推至约 40 cm；
3. 左右手各执行 10 次；
4. 每次间隔至少 1 秒。

负例步骤：

- 静止张掌 10 次；
- 仅横向挥手 10 次；
- 缓慢靠近 10 次；
- 握拳快速靠近 10 次。

合格：

- 正例至少 17/20 显示 `Motion=Push`；
- 每个负例集合误触不超过 1/10；
- 单次动作不得连续产生多个 Push；
- 0.60 秒动作冷却期间不得重复触发动作事件。

## TC-10：SK001 震山掌触发

目标：验证完整输入组合和技能冷却。

步骤：

1. 先保持张掌；
2. 掌心正对摄像头；
3. 快速向摄像头推掌；
4. 左右手各执行 10 次；
5. 记录手势、动作、技能状态和实际视觉效果。

合格：

- 20 次规范动作中至少 17 次触发 `Skill=SK001`，成功率不低于 85%；
- 技能触发后出现震山掌弹体/拖尾；
- 一次动作只创建一次技能效果；
- 1 秒技能冷却期间重复动作不能再次释放；
- 冷却结束后可以再次释放；
- 从推掌完成到效果出现的可感知延迟目标不超过 500 ms。

如果只能看到日志中的 `SK001`，但没有技能视觉效果，不算技能完整通过。

## TC-11：负例与防误触

目标：确认非规范输入不会释放震山掌。

每项执行 10 次：

- 无手进入画面；
- 静止张掌；
- 握拳推近；
- 张掌但掌心侧向；
- 张掌横向移动；
- 张掌缓慢靠近；
- 手在画面边缘快速经过；
- 双手互相遮挡。

合格：

- 任一负例集合误触不得超过 1/10；
- 无手和静止张掌必须 0 次触发；
- 不得因识别丢失恢复而自动释放技能。

## TC-12：命中、伤害与战斗闭环（P0）

目标：确认技能不是只有动画，而是真正进入战斗结算。

步骤：

1. 进入 BattleScene；
2. 记录石妖初始 HP；
3. 对准石妖释放一次震山掌；
4. 记录命中前后 HP；
5. 重复施法直至石妖 HP 归零；
6. 观察战斗结束状态。

合格：

- 初始显示有效敌人 HP；
- 弹体命中石妖后 HP 减少；
- 未命中时不得扣血；
- 一次命中只结算一次伤害；
- HP 不得降为负数；
- HP 归零后出现死亡/战斗结束状态；
- 死亡后继续碰撞不得重复结算。

如 APK 没有可达的 BattleScene 入口，本项判定 P0 阻塞，不能只在 Unity Editor 中手动打开 BattleScene 代替 APK 用户流程验收。

## TC-13：性能

目标：确认中端 Android 设备上达到 MVP 可玩水平。

步骤：

1. 冷启动后等待 1 分钟；
2. 连续识别单手 3 分钟；
3. 连续双手识别 3 分钟；
4. 连续释放震山掌 3 分钟；
5. 记录 HUD FPS、发热、明显卡顿和输入延迟；
6. 保存内存和帧耗时数据。

可使用：

```powershell
& $adb -s $serial shell dumpsys meminfo com.magicmotion.game
& $adb -s $serial shell dumpsys gfxinfo com.magicmotion.game reset
# 执行一段测试后
& $adb -s $serial shell dumpsys gfxinfo com.magicmotion.game
```

合格：

- 平均 FPS 不低于 30；
- 不得持续 5 秒以上低于 24 FPS；
- 推掌到技能表现目标不超过 500 ms；
- 无周期性长卡顿；
- 内存达到稳定区间后不持续单向增长；
- 手机可以升温，但不得出现系统过热警告、应用被杀或明显热降频导致不可玩。

仅凭主观“看起来流畅”不足以判定性能通过，至少同时提供 HUD FPS 录屏和一次 `dumpsys meminfo/gfxinfo`。

## TC-14：15 分钟稳定性

目标：验证摄像头、MediaPipe 和技能链长时间运行不会失效。

步骤：

1. 应用连续运行 15 分钟；
2. 每分钟至少执行一次张掌和推掌；
3. 测试期间进行一次退到后台再返回；
4. 熄屏再亮屏一次；
5. 检查摄像头和识别是否恢复；
6. 测试结束保存完整日志和内存信息。

合格：

- 无崩溃、ANR、Native Crash；
- 摄像头不会永久黑屏；
- MediaPipe 不会停止输出；
- 返回前台后 3 秒内恢复运行；
- 技能仍可触发；
- 无明显内存持续增长；
- 无重复初始化导致的多路摄像头或重复技能事件。

## 9. 测试统计方法

每次尝试只能记录为以下三种之一：

- 成功：在规定时间内产生正确结果；
- 失败：未产生结果或结果错误；
- 无效：手完全出画、动作未按规范完成、测试被外部事件打断。

无效尝试不进入成功率分母，但必须记录无效原因，且同一项无效次数超过 20% 时，应重新布置环境后重测。

计算公式：

```text
成功率 = 成功次数 ÷（成功次数 + 失败次数）× 100%
误触率 = 误触次数 ÷ 有效负例次数 × 100%
```

禁止只挑选成功片段。原始记录必须包含全部有效尝试。

## 10. P0 阻塞条件

出现以下任一情况，立即停止完整闭环验收并提交阻塞报告：

- ADB 无法稳定识别设备；
- APK 无法安装；
- 应用冷启动崩溃或 ANR；
- 启动后无法进入训练/战斗场景；
- 摄像头授权后仍无法启动；
- MediaPipe 原生库或模型加载失败；
- 手进入画面后始终 `Hands: 0`；
- 无法进入 BattleScene；
- 技能无法产生任何视觉效果；
- 技能命中后敌人 HP 不变化；
- 出现稳定复现的 Native Crash、SIGSEGV 或严重数据错乱。

P0 阻塞报告必须包含：设备信息、APK 哈希、复现步骤、预期结果、实际结果、发生时间、截图/录屏和相应时间段日志。

## 11. CodeBuddy 输出要求


CodeBuddy 完成测试后，必须在 `docs/log/device-test/<日期>-<设备型号>/` 下提交：

1. `真机测试结果.md`；
2. `device-logcat-final.txt`；
3. 关键截图；
4. 完整闭环录屏或录屏路径说明；
5. APK 信息和 SHA-256；
6. 每个测试项的原始次数记录；
7. 所有失败项和复现步骤；
8. 最终结论：合格、不合格或阻塞。

不能只回复“测试正常”“可以运行”或“手势成功”。

## 12. 测试结果模板

将下面模板复制为 `真机测试结果.md`：

```markdown
# Magic Motion 真机测试结果

## 基本信息

- 测试日期：
- 测试人员/执行代理：CodeBuddy
- 设备序列号（可部分脱敏）：
- 品牌与型号：
- Android 版本 / API：
- SoC / RAM：
- 屏幕分辨率：
- 测试时电量/充电状态：
- APK 路径：
- APK 大小：
- APK SHA-256：
- 安装方式：覆盖安装 / 全新安装

## 总结论

- 结论：合格 / 不合格 / 阻塞
- P0 是否全部通过：是 / 否
- 主要问题：
- 未验证边界：

## 测试结果

| 编号 | 测试项 | 结果 | 数据 | 证据 | 备注 |
|---|---|---|---|---|---|
| TC-00 | 设备连接 | 通过/失败 |  |  |  |
| TC-01 | APK 安装 | 通过/失败 |  |  |  |
| TC-02 | 冷启动与入口 | 通过/失败 |  |  |  |
| TC-03 | 摄像头权限 | 通过/失败 |  |  |  |
| TC-04 | MediaPipe 初始化 | 通过/失败 |  |  |  |
| TC-05 | 旋转与镜像 | 通过/失败/待补 |  |  |  |
| TC-06 | 单手与左右手 | 通过/失败 | 成功 __/20 |  |  |
| TC-07 | 双手识别 | 通过/失败 | 成功 __/10 |  |  |
| TC-08 | OPEN_PALM | 通过/失败 | 成功 __/20，__% |  |  |
| TC-09 | PUSH | 通过/失败 | 正例 __/20；负例误触 __/40 |  |  |
| TC-10 | SK001 | 通过/失败 | 成功 __/20，__%；延迟 __ms |  |  |
| TC-11 | 防误触 | 通过/失败 |  |  |  |
| TC-12 | 战斗闭环 | 通过/失败 | 伤害 __；施法 __ 次击败 |  |  |
| TC-13 | 性能 | 通过/失败 | 平均 FPS __；最低持续 FPS __；内存 __MB |  |  |
| TC-14 | 15 分钟稳定性 | 通过/失败 | 崩溃 __；ANR __；恢复 __s |  |  |

## 原始次数记录

### 单手与左右手

| 次数 | 手 | 检测耗时 | Handedness | 21 点完整 | 结果 | 备注 |
|---|---|---:|---|---|---|---|

### OPEN_PALM / PUSH / SK001

| 次数 | 手 | Gesture | Motion | Skill | 延迟 | 敌人 HP 变化 | 结果 |
|---|---|---|---|---|---:|---|---|

### 负例

| 负例类型 | 有效次数 | 误触次数 | 误触率 | 结果 |
|---|---:|---:|---:|---|

## 性能数据

- 冷启动耗时：
- 摄像头启动耗时：
- MediaPipe 首次识别耗时：
- 平均 FPS：
- 是否持续低于 24 FPS：
- 初始内存：
- 15 分钟内存：
- 是否出现过热警告：

## 问题清单

### 问题 1

- 严重级别：P0 / P1 / P2
- 复现概率：
- 前置条件：
- 复现步骤：
- 预期结果：
- 实际结果：
- 首次发生时间：
- 相关日志时间段：
- 截图/录屏：
- 是否稳定复现：

## 日志检查

- FATAL EXCEPTION：无 / 有
- ANR：无 / 有
- SIGSEGV：无 / 有
- DllNotFoundException：无 / 有
- MediaPipe 模型错误：无 / 有
- Camera 错误：无 / 有
- 其他异常：

## 最终建议

- 是否允许进入下一阶段：
- 必须先修复的问题：
- 建议优化项：
```

## 13. 最终判定规则

### 合格

- 所有 P0 项通过；
- 所有量化阈值达到要求；
- 日志无致命错误；
- 证据完整；
- 未通过人工绕过场景、手改数据或只选成功样本得出结论。

### 不合格

- 功能可以执行，但一个或多个合格阈值未达到；
- 存在稳定误触、漏识别、性能或恢复问题；
- 战斗闭环不完整。

### 阻塞

- 安装、启动、权限、MediaPipe 初始化、场景入口等 P0 问题导致后续测试无法执行；
- 测试设备或 ADB 环境不可用；
- 缺少必要测试入口或可观测数据，无法形成可信结论。

CodeBuddy 必须忠实报告“未验证”和“阻塞”，不得用编译成功、APK 构建成功或单次日志触发替代真机验收。
