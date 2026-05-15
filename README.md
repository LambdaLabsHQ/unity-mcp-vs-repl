# Unity MCP vs REPL: Agentic Loop Experiment

This repository contains a reproducible Unity experiment comparing a typical
`MCP + code-writing` workflow with `Unity REPL` as a live control surface.

The point is not that MCP agents cannot write code. They can. The point is that
Unity REPL lets an agent close the loop *inside Unity*: observe arbitrary live
object state, act by calling Unity/project code directly, evaluate the result
across frames, and adapt until the end-to-end objective is true.

## Demo Video

<!-- VIDEO_EMBED_START -->
The comparison video is generated at:

```text
results/agentic_loop_recording/mcp_vs_repl_agentic_loop.mp4
```
<!-- VIDEO_EMBED_END -->

## Thesis

MCP is a protocol layer. Unity REPL is an execution layer.

For Unity, the hard part is not just triggering actions. The hard part is
running an agentic loop over a live game/editor state:

```text
Observe -> Action -> Evaluation -> Adapt
```

Unity REPL gives the agent a C# evaluator on the Unity Editor main thread. That
means the agent can:

- read arbitrary live GameObject, Component, Editor, asset, and runtime state;
- call arbitrary Unity API, Editor API, project code, and object member methods;
- write temporary C# and use it immediately;
- run `IEnumerator` workflows across frames;
- verify gameplay outcomes in the same runtime where the actions happened.

## The Experiment

The visual experiment uses the same live Unity game objective on both sides:

```text
open door -> survive laser -> extend bridge -> reach exit
```

The scene contains project-specific gameplay objects:

| Object | Project API |
|---|---|
| `AgenticDoor` | `UnlockForAgent()` / `Lock()` |
| `AgenticLaser` | `SuppressForSeconds()` / `CanHit(Vector3)` |
| `AgenticBridge` | `Extend()` / `Retract()` |
| `AgenticBot` | `Damage()` / `MarkExit()` / runtime flags |

Left side:

- `MCP + code batch loop`
- represents an external edit/compile/run/evaluate cycle;
- completes two patch attempts in the video;
- still ends at `NO PASS`.

Right side:

- `REPL live control loop`
- reads live object state;
- calls project member functions directly;
- evaluates outcomes across frames;
- reaches `PASS` in one live control loop.

Current metrics:

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

## Reproduce

Requirements:

- Unity 6000.3.12f1 or compatible Unity 6 editor
- macOS for the included system window recording script
- `ffmpeg`, `ffprobe`, `gh` optional for publishing

Run from the repository root:

```bash
UNITY_FULLSCREEN_EXPERIMENT=agentic \
UNITY_FULLSCREEN_RECORD_SECONDS=22 \
UNITY_FULLSCREEN_MAXIMIZE_GAME_VIEW=0 \
UNITY_FULLSCREEN_RECORD_WINDOW=1 \
python3 tools/record_unity_fullscreen_system.py
```

The script:

1. starts `BenchProject` in Unity;
2. waits for Unity REPL to answer `Application.unityVersion`;
3. loads `experiments/visual_repro/AgenticLoopExperiment.cs`;
4. runs `AgenticLoopExperiment.Run(1337)`;
5. records the Unity window with macOS `screencapture -l<UnityWindowID>`;
6. drives `AgenticLoopExperiment.PlayDemo(22)` through Unity REPL;
7. writes the MP4 and thumbnails under `results/agentic_loop_recording/`.

## Documentation

- [Experiment design](experiments/visual_repro/AGENTIC_LOOP_EXPERIMENT.md)
- [PPT narrative](experiments/visual_repro/AGENTIC_LOOP_PPT_NARRATIVE.md)
- [Unity experiment source](experiments/visual_repro/AgenticLoopExperiment.cs)
- [Recording script](tools/record_unity_fullscreen_system.py)

## What This Does Not Claim

This demo does not claim that MCP agents cannot write code. It does not claim
that MCP can never complete the same task. It isolates a different point:

```text
MCP + code-writing can eventually solve Unity tasks through external batch loops.
Unity REPL gives the agent a live, programmable Action + Evaluation loop inside Unity.
```

If an MCP server exposes a strong `execute_code` / `eval C#` endpoint with
coroutine support, session state, live object inspection, and runtime
verification, then its effective control surface has become REPL-shaped.
