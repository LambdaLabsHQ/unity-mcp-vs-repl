# Unity REPL vs MCP: Agentic Loop PPT Narrative

这份文档是给 PPT 使用的叙事稿。它不是复现实验说明，而是用于介绍“为什么 Unity REPL 在 Unity agentic workflow 中有结构性优势”。

配套实验说明见：

- `experiments/visual_repro/AGENTIC_LOOP_EXPERIMENT.md`

配套视频见：

- `results/agentic_loop_recording/mcp_vs_repl_agentic_loop.mp4`

## 核心标题

Unity REPL vs MCP：不是“工具更多”，而是“控制面不同”

## 一句话 Thesis

MCP 可以让 agent 写代码；Unity REPL 的真正优势是把 Unity Editor / Game 变成一个实时、可编程、可验证的控制面，让 agent 在同一个 loop 里完成 Action + Evaluation，直到端到端目标成立。

## Slide 1: 问题不是“能不能写代码”

传统误解：

> MCP 只能调用工具，REPL 才能写代码。

这个说法不准确。MCP 不阻止 agent 写代码。一个 MCP agent 完全可以修改 `.cs` 文件、触发 Unity 编译、运行测试、读取日志。

真正的问题是：

> agent 的 Action 和 Evaluation 发生在哪里？

对比：

| 形态 | Action 和 Evaluation 的位置 |
|---|---|
| MCP + code-writing | 通常发生在 Unity 外部，是 edit / compile / run / observe 的批处理循环 |
| Unity REPL | 发生在 Unity 内部，是 live object state / member call / coroutine / verification 的实时循环 |

讲解重点：

不要把对比讲成“MCP 不能写代码”。正确说法是：MCP 可以写代码，但默认工作流更像外部批处理；Unity REPL 把行动和验证都放进同一个运行中的 Unity 环境。

## Slide 2: 对比对象的准确定义

### MCP + Code Batch Loop

这里的 MCP baseline 不是“不会写代码”，而是：

1. agent 观察场景或日志。
2. agent 写或修改 C# 文件。
3. Unity 编译，可能 domain reload。
4. agent 运行菜单项、测试、Play Mode 或脚本。
5. agent 读取结果。
6. 失败后再进入下一轮。

这是有效的，但它是外部批处理循环。

### Unity REPL Live Control Loop

1. agent 在 Unity 主线程 eval C#。
2. 直接读取 live object 状态。
3. 直接调用对象成员函数和系统 API。
4. 通过 coroutine 跨帧等待。
5. 立即验证结果。
6. 根据验证结果继续行动。

这是实时控制循环。

## Slide 3: 本质差异

| 维度 | MCP + code-writing | Unity REPL |
|---|---|---|
| 控制边界 | Unity 外部 | Unity 内部 |
| 行动单位 | 文件修改 / 工具调用 / 测试运行 | C# 表达式 / statement / coroutine |
| 验证方式 | 日志、测试结果、再次查询 | 直接读取 live runtime state |
| 反馈周期 | edit -> compile -> run -> observe | eval -> observe -> adapt |
| 状态连续性 | 容易被 domain reload / run boundary 打断 | session 内状态持续到 domain reload |
| 任意项目 API | 可通过写代码访问，但通常要经过编译循环 | 直接调用 |
| 跨帧能力 | 需要工具协议或轮询设计 | `IEnumerator` 原生 drive |
| 抽象本质 | 协议 + 工具集合 | evaluator + Unity 主线程控制面 |

关键句：

> MCP 是协议层抽象；Unity REPL 是执行层抽象。

## Slide 4: 为什么 Unity 特别适合 REPL

Unity 的真实工作不是简单 RPC：

- scene graph 深度嵌套。
- GameObject / Component 状态实时变化。
- Editor Mode 和 Play Mode 行为不同。
- Prefab、AssetDatabase、SerializedObject、SceneManager 互相交织。
- 游戏测试需要等待动画、AI、物理、加载、输入。
- 项目能力往往藏在自定义 MonoBehaviour 里。

也就是说，Unity 不是一个适合枚举 endpoint 的系统。它更像一个需要 live programming 的运行环境。

Unity 本来已经有完整宿主语言：C#。REPL 的思路是：不要再把 C# API 包成一层工具目录，直接 eval C#。

## Slide 5: REPL 的真正能力

Unity REPL 给 agent 的不是一批工具，而是一个控制面：

- 访问任意对象状态。
- 调用任意成员函数。
- 调用 Unity Editor API。
- 调用 Runtime API。
- 调用项目自定义代码。
- 使用 LINQ、反射、泛型、闭包、临时类型。
- 现场编写一次性代码。
- 现场固化成项目代码。
- 通过 coroutine 跨帧执行。
- 用真实运行结果验证目标是否成立。

核心表达：

> Action + Evaluation in the same runtime.

## Slide 6: Agentic Loop 视角

普通工具调用更像：

```text
Agent -> Tool -> Result
```

Unity REPL 支撑的是：

```text
Observe -> Action -> Evaluation -> Adapt
```

更具体：

```text
读取对象状态
-> 调用项目函数
-> 等待一帧 / 等待动画 / 等待加载
-> 验证 HP、位置、flag、scene state
-> 如果失败，继续 patch
-> 如果成功，输出端到端结果
```

这才是 game development、level design、game testing 需要的工作模式。

## Slide 7: 实验目标

我们设计了一个 Unity live game control 对比实验。

同一个目标：

```text
open door -> survive laser -> extend bridge -> reach exit
```

场景对象：

| 对象 | 能力 |
|---|---|
| `AgenticDoor` | `UnlockForAgent()` / `Lock()` |
| `AgenticLaser` | `SuppressForSeconds()` / `CanHit(Vector3)` |
| `AgenticBridge` | `Extend()` / `Retract()` |
| `AgenticBot` | `Damage()` / `MarkExit()` / runtime flags |

这些对象代表真实游戏里的门、机关、伤害系统、桥、胜利条件。

## Slide 8: 左侧 MCP Baseline 做了什么

左侧不是“不能写代码”。

左侧表示 MCP + code-writing batch loop：

1. 第一轮：写代码修门。
2. 编译 / 运行 / 验证。
3. 发现激光仍然杀死 bot。
4. 第二轮：写代码修激光。
5. 编译 / 运行 / 验证。
6. 发现桥仍然缺失。
7. 视频结束：`NO PASS`。

这展示的是：

> 每次修正都要走外部 edit / compile / run / evaluate 周期。

它最终也许能成功，但反馈周期更重。

## Slide 9: 右侧 REPL 做了什么

右侧在同一个 live Unity 运行态里：

1. Observe：读取 door、laser、bridge、bot、exit 状态。
2. Action：调用 `AgenticDoor.UnlockForAgent()`。
3. Evaluation：bot 前进，验证门打开。
4. Adapt：调用 `AgenticLaser.SuppressForSeconds()`。
5. Evaluation：跨帧验证 HP 不下降。
6. Action：调用 `AgenticBridge.Extend()`。
7. Evaluation：等待 bot 到达出口。
8. 调用 `AgenticBot.MarkExit()`。
9. 最终 `PASS`。

这展示的是：

> REPL 不是多一个工具，而是允许 agent 直接控制游戏本身并验证结果。

## Slide 10: 实验结果

当前实验 metrics：

```json
{
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

配套视频：

```text
results/agentic_loop_recording/mcp_vs_repl_agentic_loop.mp4
```

讲解重点：

不要把 `mcp_end_to_end_pass=false` 解释成“MCP 永远做不到”。它表达的是：在视频窗口里的两轮外部 batch loop 还没有闭环；REPL 在同一 live loop 中闭环了。

## Slide 11: 这个实验真正证明什么

它证明：

> Unity REPL 更适合作为 agent 的 Unity 控制面，因为它把 action 和 evaluation 放在同一个运行环境里。

它不证明：

- MCP 不能写代码。
- MCP 永远不能完成这个任务。
- 当前 demo 是完整 Coplay MCP runtime benchmark。
- REPL 自动让模型更聪明。

它证明的是控制面差异：

```text
MCP + code-writing:
eventually solve through external batch loops

Unity REPL:
close the loop inside Unity
```

## Slide 12: 如果 MCP 加 execute_code 呢？

这是关键讨论点。

如果 MCP server 暴露：

- `execute_code`
- `eval_csharp`
- coroutine support
- session state
- live object inspection
- runtime verification

那它当然也能获得类似能力。

但这说明什么？

> 它的有效部分已经变成 REPL，只是外面包了一层 MCP。

所以问题不是 MCP 协议有没有价值，而是：

> 对 Unity 这种复杂运行环境，最终需要的核心原语是 eval，而不是 endpoint table。

## Slide 13: 为什么这对 Level Design 重要

Level design 不是一次性生成内容。

真实 workflow 是：

1. 生成场景。
2. 检查可通行性。
3. 放置敌人。
4. 运行玩家路径。
5. 发现卡点。
6. 调整门、桥、障碍、触发器。
7. 再运行。
8. 直到体验目标成立。

REPL 让 agent 可以像 designer 一样 live iterate：

```text
修改 -> 运行 -> 观察 -> 修正 -> 再验证
```

而不是每次都回到外部编译循环。

## Slide 14: 为什么这对游戏测试重要

游戏测试的关键不是“调用测试工具”，而是：

- 能否控制玩家？
- 能否等待动画？
- 能否读取内部状态？
- 能否判断失败原因？
- 能否现场插桩？
- 能否修复后立即重测？

Unity REPL 允许 agent 直接写：

```csharp
bot.transform.position
door.Locked
laser.CanHit(bot.transform.position)
bridge.Extended
bot.ReachedExit
```

并且可以跨帧等待：

```csharp
yield return new WaitForSeconds(1.0f);
yield return new WaitUntil(() => bot.ReachedExit);
```

这是 agentic testing 的基础。

## Slide 15: 最终表述

英文版：

> Unity REPL does not merely let an agent operate Unity. It lets the agent close the loop inside Unity: act on arbitrary engine and project state, evaluate the result in the running game, and adapt until the end-to-end objective is true.

中文版：

> Unity REPL 不是给 agent 增加一批 Unity 工具，而是给 Unity 增加一个可编程验证层。Agent 可以直接作用于任意对象和系统，并在运行中的 Editor / Game 内验证结果，从而真正以 agentic loop 的方式完成端到端目标。

## 推荐 PPT 结构

如果要压缩成 8 页，可以这样合并：

1. Thesis：不是工具更多，是控制面不同。
2. 修正误解：MCP 可以写代码，但默认是外部 batch loop。
3. Unity 为什么需要 live control surface。
4. Unity REPL 提供什么能力。
5. Agentic Loop：Observe -> Action -> Evaluation -> Adapt。
6. 实验设计：door / laser / bridge / bot。
7. 视频结果：左侧两轮未闭环，右侧 live loop 达成 PASS。
8. 结论：eval 是 Unity agent 的核心原语。

如果要展开成 15 页，就直接使用本文件的 slide 顺序。
