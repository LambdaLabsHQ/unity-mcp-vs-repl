# Agentic Loop Unity Control Experiment

这份文档说明 `AgenticLoopExperiment` 对比实验的设计、复现方式和结果解读。

PPT 介绍稿见：

- `experiments/visual_repro/AGENTIC_LOOP_PPT_NARRATIVE.md`

它替代早期“工具表 vs REPL”式 demo。新的重点不是证明 MCP 不能写代码，而是证明 Unity REPL 把 Unity Editor / Game 变成了一个可编程、可验证、可跨帧的 live control surface，从而支撑 agentic loop：

```text
Observe -> Action -> Evaluation -> Adapt
```

## 结论

Unity REPL 的核心优势不是“多一个 Unity 工具”，而是给 Unity 增加了一层可执行验证层：

- agent 可以读取任意 live object 的状态。
- agent 可以调用任意 Unity API、Editor API、项目代码和对象成员函数。
- agent 可以现场编写 C#，立即在 Unity 主线程求值。
- agent 可以跨帧等待、验证、继续行动。
- agent 可以在同一个运行态里把端到端目标跑到 `PASS`。

MCP 不阻止 agent 写代码。MCP + code-writing 也可以完成很多 Unity 任务。但如果它走的是“编辑 `.cs` 文件 -> Unity 编译/domain reload -> 运行 -> 看结果 -> 再编辑”的路径，它本质上是外部批处理循环。Unity REPL 的区别是把 action 和 evaluation 放进同一个 live Unity loop。

如果 MCP 暴露一个足够强的 `execute_code` / `eval C#` / coroutine 工具，那么它的有效控制面已经变成 REPL，只是外层包了一层 MCP 协议。

## 实验文件

- Unity 实验源码：`experiments/visual_repro/AgenticLoopExperiment.cs`
- 录制脚本：`tools/record_unity_fullscreen_system.py`
- 生成场景：`BenchProject/Assets/VisualRepro/AgenticLoopShowdown.unity`
- 输出截图：`results/agentic_loop/agentic_loop_showdown.png`
- 输出视频：`results/agentic_loop_recording/mcp_vs_repl_agentic_loop.mp4`
- 输出指标：`results/agentic_loop/metrics.json`

## 任务设计

两侧是同一个 Unity 关卡目标：

```text
open door -> survive laser -> extend bridge -> reach exit
```

关卡里有四类 live object：

| 对象 | 项目代码能力 | 实验含义 |
|---|---|---|
| `AgenticDoor` | `UnlockForAgent()` / `Lock()` | 门不是普通 transform，必须调用项目成员函数改变语义状态 |
| `AgenticLaser` | `SuppressForSeconds()` / `CanHit(Vector3)` | 危险系统有运行态状态和检测逻辑 |
| `AgenticBridge` | `Extend()` / `Retract()` | 关卡拓扑可以在运行中改变 |
| `AgenticBot` | `Damage()` / `MarkExit()` / runtime flags | 目标是否达成由运行态状态验证 |

这些对象不是为了展示“某个工具更多”，而是为了展示任意游戏里都会出现的真实控制面：门、机关、伤害、路径、胜利条件、运行态状态。

## 对比双方

### 左侧：MCP + Code Batch Loop

左侧标为 `MCP + code batch loop`。

这里没有声称 MCP 不能写代码。它表示一种常见 Unity MCP 工作流：

1. agent 根据观察写或修改 C# 文件。
2. Unity 编译，可能触发 domain reload。
3. agent 运行菜单项、测试、Play Mode 或脚本。
4. agent 读取日志、场景状态或测试结果。
5. 如果失败，回到第 1 步。

视频里左侧完成两轮 batch loop：

1. 第一轮修门：门打开了，但运行后发现激光仍然杀死 bot。
2. 第二轮修激光：激光被处理了，但运行后发现桥仍然缺失。
3. 视频结束时仍然 `NO PASS`，下一轮 patch 还在队列里。

它展示的是外部批处理循环的摩擦：每轮 action 和 evaluation 被 Unity 编译、domain reload、测试触发和日志观察隔开。

### 右侧：REPL Live Control Loop

右侧标为 `REPL live control loop`。

REPL 侧在同一个 live Unity 运行态里执行：

1. `Observe`: 读取 door、laser、bridge、bot、exit 的当前状态。
2. `Action`: 调用 `AgenticDoor.UnlockForAgent()`。
3. `Evaluation`: bot 跨帧前进，验证门已打开。
4. `Adapt`: 调用 `AgenticLaser.SuppressForSeconds(...)`。
5. `Evaluation`: 验证 HP 没有下降。
6. `Action`: 调用 `AgenticBridge.Extend()`。
7. `Evaluation`: 跨帧等待 bot 到达出口。
8. 调用 `AgenticBot.MarkExit()`，最终 `PASS`。

REPL 侧不是靠预置 `open_door`、`disable_laser`、`extend_bridge` 工具完成任务，而是直接进入宿主语言和项目运行态，调用对象成员函数并立即验证结果。

## 为什么这个实验能展示 REPL 优势

### 1. 控制粒度更细

MCP + code-writing 的自然单位通常是“改文件 / 编译 / 运行一次”。REPL 的自然单位是“求值一个 C# 表达式或 coroutine”。后者能直接落到 live object 和当前帧。

### 2. Action 和 Evaluation 在同一个循环里

Unity 自动化的难点不只是“做动作”，而是做完动作后马上判断是否达成目标：

- bot 是否仍然活着？
- door 是否真的打开？
- laser 是否还能命中？
- bridge 是否已经改变了可通行拓扑？
- exit flag 是否成立？

REPL 把这些 evaluation 直接变成 C# 查询和 coroutine 等待，而不是把它们拆成外部日志轮询或多轮工具调用。

### 3. 任意项目 API 都是控制面

Unity 项目的真实能力通常藏在项目自己的 MonoBehaviour、ScriptableObject、Editor 工具、manager、service 和状态机里。REPL 不需要先把这些能力包装成 endpoint。只要 C# 能调用，agent 就能现场调用。

### 4. 跨帧是语言级能力

`unity-repl` 的 `CoroutinePump` 会把返回的 `IEnumerator` drive 到完成。也就是说，agent 可以写 Unity coroutine：

```csharp
yield return new WaitForSeconds(1.0f);
yield return new WaitUntil(() => bot.ReachedExit);
```

这对游戏测试、动画等待、异步加载、Play Mode 验证非常关键。传统请求/响应工具如果没有类似机制，就需要外部轮询或 server 侧状态机。

## 实验实现细节

`AgenticLoopExperiment.cs` 里定义了四个项目对象：

```csharp
public sealed class AgenticDoor : MonoBehaviour
public sealed class AgenticLaser : MonoBehaviour
public sealed class AgenticBridge : MonoBehaviour
public sealed class AgenticBot : MonoBehaviour
```

这些类模拟真实 Unity 项目里的 gameplay API。实验场景由 `AgenticLoopExperiment.Run(1337)` 生成，并保存为：

```text
BenchProject/Assets/VisualRepro/AgenticLoopShowdown.unity
```

动画由：

```csharp
AgenticLoopExperiment.PlayDemo(seconds)
```

驱动。录制脚本通过 Unity REPL 先加载源码，再调用 `Run` 和 `PlayDemo`。

## 可复现命令

从仓库根目录运行：

```bash
UNITY_FULLSCREEN_EXPERIMENT=agentic \
UNITY_FULLSCREEN_RECORD_SECONDS=22 \
UNITY_FULLSCREEN_MAXIMIZE_GAME_VIEW=0 \
UNITY_FULLSCREEN_RECORD_WINDOW=1 \
python3 tools/record_unity_fullscreen_system.py
```

脚本会执行以下步骤：

1. 确认 `screencapture`、`ffmpeg`、`ffprobe`、`osascript` 可用。
2. 确认 `/tmp/lambdalabs-unity-repl/repl.sh` 存在，不存在则 clone `https://github.com/LambdaLabsHQ/unity-repl.git`。
3. 启动 `BenchProject` 的 Unity Editor。
4. 等待 Unity REPL 响应 `Application.unityVersion`。
5. 通过 REPL 加载 `experiments/visual_repro/AgenticLoopExperiment.cs`。
6. 执行 `AgenticLoopExperiment.Run(1337)` 生成场景、截图和 metrics。
7. 获取 Unity 窗口的 CGWindowID。
8. 使用 macOS 系统工具直接录 Unity 窗口：

   ```bash
   screencapture -x -v -V 22 -l<UnityWindowID> results/agentic_loop_recording/unity_fullscreen_raw.mov
   ```

9. 同时通过 REPL 执行 `AgenticLoopExperiment.PlayDemo(22)`。
10. 用 `ffmpeg` 转为 1920x1080 H.264 MP4，并生成缩略图。

注意：这里不是坐标裁剪录屏。`-l<UnityWindowID>` 是 macOS `screencapture` 的指定窗口录制模式。

## 当前结果

当前本机输出：

```json
{
  "experiment": "Agentic Loop Live Unity Control",
  "mcp_baseline": "code-writing external edit/compile/run/evaluate loop",
  "repl_surface": "live C# eval on Unity Editor Main Thread",
  "live_objects_controlled": 4,
  "project_member_calls": [
    "AgenticDoor.UnlockForAgent",
    "AgenticLaser.SuppressForSeconds",
    "AgenticBridge.Extend",
    "AgenticBot.MarkExit"
  ],
  "repl_evaluations": 4,
  "mcp_attempts_completed_in_video": 2,
  "mcp_end_to_end_pass": false,
  "repl_end_to_end_pass": true
}
```

视频参数：

```text
path: results/agentic_loop_recording/mcp_vs_repl_agentic_loop.mp4
resolution: 1920x1080
duration: 22.0s
frames: 1237
```

## 如何解读视频

### 0-4 秒

两边载入同一个目标：开门、避开激光、搭桥、到达出口。

左侧进入第一轮外部 batch loop。右侧开始 live observe，直接读取 Unity 对象状态。

### 4-9 秒

左侧第一轮 patch 打开门，但运行时 evaluation 发现激光仍然会伤害 bot。

右侧调用 `Door.UnlockForAgent()`，然后继续在同一运行态里验证门已打开。

### 9-15 秒

左侧进入第二轮 patch，修掉激光，但仍然没有解决断桥。

右侧调用 `Laser.SuppressForSeconds()`，跨帧验证 bot HP 稳定，没有受到伤害。

### 15-22 秒

左侧停在断桥前，显示 `NO PASS`，下一轮 patch 仍需继续。

右侧调用 `Bridge.Extend()`，bot 跨桥到达出口，最终显示 `PASS`。

## 这个实验没有声称什么

为了避免过度解读，明确列出非目标：

- 它不是 LLM 智能水平 benchmark。两边行为是 deterministic visualization，用于隔离控制面的差异。
- 它不是说 MCP 不能写代码。
- 它不是当前 Coplay MCP server 的完整真实运行 trace。
- 它不是说 MCP 永远不能做到同样结果。

它真正展示的是：

```text
MCP + code-writing can eventually solve Unity tasks through an external batch loop.
Unity REPL gives the agent a live, programmable Action + Evaluation loop inside Unity.
```

如果 MCP 暴露了一个强 `execute_code` endpoint，并支持跨帧 coroutine、session 状态和 live object evaluation，那么它已经在协议内部提供了 REPL 形态。

## 下一步可增强项

要把这个实验升级成更严格的 benchmark，可以继续补：

1. 真实 Coplay MCP client transcript：记录每轮工具调用、文件修改、编译等待、日志读取和失败原因。
2. 真实 REPL transcript：记录每个 eval input、stdout、耗时和最终状态。
3. 定量指标：agent turns、elapsed seconds、Unity domain reload 次数、compile 次数、token 输入量、observation 字节数。
4. 多关卡随机种子：同一套 object API，在不同布局里重复验证。
5. Play Mode 版：让 bot、laser、bridge 全部用真实 MonoBehaviour Update / physics / coroutine 运行，而不只是 editor animation。

这些增强会把当前“可视化实验”扩展为完整的可重复评测。
