# Turret Coverage Lab

This visual experiment is designed to demonstrate REPL's advantage over large
tool-table MCP interfaces.

The point is not that REPL can create cubes faster. The point is that a single
host-language program can:

- Discover unknown project APIs by reflection.
- Use project-specific types (`Turret`, `Health`) without any pre-registered tool.
- Compose geometry, LINQ, custom methods, scene creation, camera rendering, and file output.
- Run as a coroutine so Unity can tick before rendering.
- Crystallize the discovered workflow into project code.

An MCP tool-table solution has two options:

1. Enumerate and recall many tools, actions, and schemas.
2. Fall back to `execute_code`.

The second option is a concession: it becomes REPL inside MCP.

## What It Builds

The generated scene contains:

- A deterministic obstacle map.
- Five turret objects with custom `Turret.CanReach(Vector3)` logic.
- A line-of-sight-aware heatmap.
- A waypoint path with risk markers and risk bars.
- Side-by-side in-scene panels comparing:
  - REPL: one stable `eval C#` interface.
  - MCP: 42 observed Coplay tool names and large reference context.

## Outputs

Running the experiment writes:

- `results/visual_repro/turret_coverage_lab.png`
- `results/visual_repro/metrics.json`
- `results/visual_repro/run_summary.json`
- `results/video/repl_vs_mcp_comparison.mp4` when the video step is run
- `results/unity_editor_recording/repl_vs_mcp_unity_editor_live.mp4` when the Unity Editor recording step is run
- `BenchProject/Assets/VisualRepro/TurretCoverageLab.unity`
- `BenchProject/Assets/Editor/CoverageProbe.cs`

## Reproduce

From the repository root:

```bash
python3 tools/run_visual_repro.py
```

To generate the comparison video from the deterministic visual output:

```bash
python3 tools/make_comparison_video.py
```

To record the actual Unity Editor window while REPL drives the scene:

```bash
UNITY_RECORD_FPS=5 UNITY_RECORD_SECONDS=18 UNITY_RECORD_OUTPUT_FPS=20 \
python3 tools/record_unity_editor_video.py
```

To use macOS system recording instead, make Unity enter fullscreen first and
record the whole fullscreen Space:

```bash
UNITY_FULLSCREEN_RECORD_SECONDS=18 \
python3 tools/record_unity_fullscreen_system.py
```

To make the final two-lane MCP vs REPL comparison video from that recording:

```bash
python3 tools/make_mcp_repl_two_lane_video.py
```

Optional:

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity" \
python3 tools/run_visual_repro.py
```

The runner:

1. Clones `https://github.com/LambdaLabsHQ/unity-repl.git` into `/tmp/lambdalabs-unity-repl` if needed.
2. Starts Unity in batch mode with `BenchProject`.
3. Waits for Unity REPL to answer `Application.unityVersion`.
4. Loads `experiments/visual_repro/VisualReproExperiment.cs` into the REPL evaluator.
5. Executes `VisualReproExperiment.Run(1337)`.
6. Saves the PNG, metrics, scene, and crystallized probe.
7. Exits Unity.

The basic video generator turns the PNG plus metrics into a 23 second,
1920x1080 MP4 with reproducible slides, sampled thumbnails, and
`video_summary.json`.

The Unity Editor recorder opens the real Unity window, uses the REPL to build
the scene, animates camera and marker state inside Unity, captures the visible
Editor window via `InternalEditorUtility.ReadScreenPixel`, and encodes the
captured frames into an 18 second, 1920x1080 MP4.

The fullscreen system recorder uses AppleScript to put Unity into macOS
fullscreen, then runs `screencapture -v -D1` while REPL plays the live demo. This
requires macOS Screen Recording permission for the app running the script.

The two-lane comparison video crops the system recording into a measured MCP
tool-table lane and a live REPL lane, then overlays the measured tool/context
numbers from `results/context_measure.json` and `results/visual_repro/metrics.json`.

## Current Measured Result

The latest run produced:

```json
{
  "coverage_actor_count_discovered_by_reflection": 5,
  "obstacle_count": 7,
  "path_sample_count": 12,
  "path_zero_risk_samples": 4,
  "path_max_risk": 2,
  "path_average_risk": 0.750,
  "coplay_readme_tool_count_observed": 42,
  "coplay_tools_reference_approx_tokens_observed": 15096
}
```

The REPL execution step itself completed in roughly `0.63s` after Unity was ready.
