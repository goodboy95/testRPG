# 双角色移动与动作

> 2026-09-14 后续区域调速：村庄 v2 室外默认 4.2 米/秒；本文五个梦境区域、住宅及 v1 回归保留 3.1 米/秒，Shift 慢走仍为 1.4。动画源和原始步幅参考不变，复用实际位移步态。见[赶路验收](../production/village-travel-validation.md)。

> 2026-09-12；覆盖洋房与四室五个 3D 区域。原始玩家静态候选 v2 不属于本轮。

## 当前行为

- `[已实现]` 玩家继续默认慢跑 3.1 米/秒，左右 Shift 慢走 1.4 米/秒，摇杆连续调速。沿用起步约 0.15 秒、制动约 0.10 秒及左右 90°/180° 可取消转身。
- `[已实现]` 两位角色各有独立 `CharacterAnimationDriver` 和动画资源副本；归一化步态相位由实际位移驱动，行走/慢跑根据各自资产周期计算步频。移动混合过渡为约 0.14 秒，交互混合约 0.18 秒。晓雨待机偏移 1.73 秒，避免与玩家同步呼吸。
- `[已实现]` 玩家不接收移动输入时仍推进待机/交互动画，不再每帧重置。检查、拾取、操作、交流映射 `observe/pickup/operate/talk`；机关明确提交时也触发操作姿态。动作不提交业务结果，不延迟道具结算或线索。
- `[已实现]` 晓雨在五区使用既有安全路径，按实际移动量播放步态。加速 7 米/秒²、减速 9 米/秒²，上限 3.4 米/秒；普通跟随在 0.10/0.26 米剩余距离间滞回，精确协作落点停止距离 0.004 米。正常超距不瞬移，障碍内/异常落点保留安全恢复。共享路点导航到达拐角后才切换下一段，并校验每一步，避免二维回退时提前切角碰到家具。
- `[已实现]` `CompanionMotionState` 为纯表现层规则，保存临时姿态类型、目的地、朝向、到达标记和计时。重复相同请求不重启；短动作到达 0.01 米内才计时；持续姿态维持到取消。音乐室/礼堂侧向观察、档案室录音机、礼堂按住机关分别使用观察/操作/保持动作。
- `[已实现]` 协作关闭后沿安全路径返回跟随；其他模态暂停普通跟随并取消短姿态（录音机结果对白期间允许其表演）。持久按压继续由已有临时合作状态控制。失焦、重载与新实例重置动作；所有状态均不进入 Core 或存档。
- `[已实现]` 玩家近镜头隐藏沿用 SpringArm 阈值；晓雨在相机接近身体时采用 0.50/0.65 米隐藏滞回，防止镜头进入网格。

## 本机资产与制作

`[已实现·仅本机非商业]` 玩家仍加载 `tests/mc-model-test-runtime/caelus-pbr-test.glb`；晓雨动态加载 `tests/xiaoyu-model-runtime/xiaoyu-motion-v3.glb`。两人都保留缺失/无效基础动作时的 billboard 回退，缺少可选姿态时使用待机；只有三段基础动画时沿用基础播放器。Legacy 不加载这些模型。

| 角色 | 静态高度 | 三角面 | 骨骼 | 待机/走/跑周期 |
| --- | --- | --- | --- | --- |
| 现有玩家 | 1.700 米 | 29120 | 278 | 4.8 / 0.9 / 0.6 秒 |
| 晓雨（流萤外观） | 1.600 米 | 32030 | 279 | 5.0 / 0.8 / 0.5333 秒 |

每人 12 段动画：待机、步行、慢跑、四段转身和 `observe/pickup/operate/talk/hold`。采用既有 Blender 5.2.0 LTS 与 MMD Tools 4.5.13，保留有权重的骨架与纹理，清理 MMD 辅助约束，将双骨脚部/手臂 IK 烘焙为 30 FPS 动作。晓雨使用较快步频、放松肩肘、手指弯曲，以及随腿摆让位的裙摆和轻微发梢动作。持续观察/按压重访动画后段，使呼吸和衣摆不冻结。

源 PMX 的 80 个形变已检查；本轮 GLB 使用中性脸，不输出形变动画。未导入剑，没有武器挂点、战斗动作、口型、布料刚体或运行时生成调用。模型是本机外观覆盖，不改变晓雨身份、台词、剧情或正式美术基线。手部采用离线通用伸手姿态，未增加逐机关运行时手掌吸附。

制作源、旧玩家备份、脚本、manifest、GLB 复导审计、对照图/视频位于 `outputs/character-motion-v3/`；v2 历史源继续保留。`assets/流萤建模/`、本机镜像和输出均由 `.git/info/exclude` 排除，原始模型目录有 `.gdignore`，导出排除原始目录及 `tests/*/outputs/*`。不得提交、推送、再分发或导出这些资产或含它们的画面。来源摘要进入 `assets/generated/provenance.json`，详细制作记录留在本机 manifest。

## 验证与复现

- `[已实现]` 在 145 项基线上新增 6 项跟随/到位计时/重复请求/取消/暂停规则用例；测试项目直接链接表现层数学文件，不修改 Core。
- `[已实现]` 九场景中的 `CharacterMotionProbe` 额外验证模态期间真实骨骼呼吸、交互姿态、独立实例相位及共享资源不被修改、可选动画回退，以及 30/60/120 FPS 的步频和家具拐角绕行。
- `[已实现]` `DualCharacterProbe` 使用正式五区、真实玩家碰撞和晓雨寻路，验证移动、近镜头、操作、协作到位、取消、失焦与模型回退；持有存档抑制租约。`--dual-capture --width=1280/1600/1920` 输出图形验收，`--before` 用旧玩家资产与晓雨 billboard 比较（路线使用当前驱动，不能当作历史程序录屏）。
- `[已实现]` 从 GLB 重新导入并以 60 FPS 采样鞋底、循环接缝和承重段漂移。资产采样结果不等于所有运行时混合都无滑步。

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& 'C:/Program Files/dotnet/dotnet.exe' build TownRpg.csproj --no-restore
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --audio-driver Dummy --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --audio-driver Dummy --path . --scene res://scenes/testing/DualCharacterProbe.tscn
```

本机录制使用 `outputs/character-motion-v3/run_godot.py` 直接启动同版本引擎并捕获输出，避免本次控制台包装进程启动停滞。实际验证、图像与未验收项见[双角色验收](../production/character-motion-v3-validation.md)。
