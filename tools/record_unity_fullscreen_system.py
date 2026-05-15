#!/usr/bin/env python3
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "BenchProject"
UNITY_BIN = Path(os.environ.get(
    "UNITY_BIN",
    "/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity",
))
REPL_REPO = Path(os.environ.get("UNITY_REPL_REPO", "/tmp/lambdalabs-unity-repl"))
REPL = REPL_REPO / "repl.sh"
UNITY_LOG = ROOT / "unity-fullscreen-recording.log"
SECONDS = int(os.environ.get("UNITY_FULLSCREEN_RECORD_SECONDS", "18"))
DISPLAY = int(os.environ.get("UNITY_FULLSCREEN_RECORD_DISPLAY", "1"))
EXPERIMENT = os.environ.get("UNITY_FULLSCREEN_EXPERIMENT", "visual").strip().lower()
RECORD_WINDOW = os.environ.get("UNITY_FULLSCREEN_RECORD_WINDOW", "1") != "0"

if EXPERIMENT == "showdown":
    OUT_DIR = ROOT / "results" / "capability_showdown_recording"
    OUT_MP4 = OUT_DIR / "mcp_vs_repl_pathfinding_showdown.mp4"
    EXPERIMENT_FILE = ROOT / "experiments" / "visual_repro" / "CapabilityShowdownExperiment.cs"
    PLAYER_FILE = None
    RUN_CODE = "CapabilityShowdownExperiment.Run(1337)"
    PLAY_CODE = f"CapabilityShowdownExperiment.PlayDemo({SECONDS})"
elif EXPERIMENT == "game":
    OUT_DIR = ROOT / "results" / "game_pathfinding_recording"
    OUT_MP4 = OUT_DIR / "mcp_vs_repl_real_pathfinding.mp4"
    EXPERIMENT_FILE = ROOT / "experiments" / "visual_repro" / "GamePathfindingExperiment.cs"
    PLAYER_FILE = None
    RUN_CODE = "GamePathfindingExperiment.Run(1337)"
    PLAY_CODE = f"GamePathfindingExperiment.PlayDemo({SECONDS})"
elif EXPERIMENT == "agentic":
    OUT_DIR = ROOT / "results" / "agentic_loop_recording"
    OUT_MP4 = OUT_DIR / "mcp_vs_repl_agentic_loop.mp4"
    EXPERIMENT_FILE = ROOT / "experiments" / "visual_repro" / "AgenticLoopExperiment.cs"
    PLAYER_FILE = None
    RUN_CODE = "AgenticLoopExperiment.Run(1337)"
    PLAY_CODE = f"AgenticLoopExperiment.PlayDemo({SECONDS})"
elif EXPERIMENT == "visual":
    OUT_DIR = ROOT / "results" / "system_fullscreen_recording"
    OUT_MP4 = OUT_DIR / "repl_vs_mcp_unity_fullscreen_system.mp4"
    EXPERIMENT_FILE = ROOT / "experiments" / "visual_repro" / "VisualReproExperiment.cs"
    PLAYER_FILE = ROOT / "experiments" / "visual_repro" / "UnityEditorLiveRecording.cs"
    RUN_CODE = "VisualReproExperiment.Run(1337)"
    PLAY_CODE = f"UnityEditorLiveRecording.PlayDemo({SECONDS})"
else:
    raise ValueError("UNITY_FULLSCREEN_EXPERIMENT must be 'visual', 'showdown', 'game', or 'agentic'")

RAW_MOV = OUT_DIR / "unity_fullscreen_raw.mov"


def run(cmd, cwd=ROOT, timeout=None, check=True):
    proc = subprocess.run(
        [str(x) for x in cmd],
        cwd=cwd,
        text=True,
        capture_output=True,
        timeout=timeout,
    )
    if check and proc.returncode != 0:
        raise RuntimeError(
            "Command failed:\n"
            + " ".join(str(x) for x in cmd)
            + "\n\nSTDOUT:\n"
            + proc.stdout
            + "\n\nSTDERR:\n"
            + proc.stderr
        )
    return proc


def require_tool(name):
    path = shutil.which(name)
    if not path:
        raise FileNotFoundError(f"Required tool not found on PATH: {name}")
    return path


def ensure_repl_repo():
    if REPL.exists():
        return
    run(["git", "clone", "--depth", "1", "https://github.com/LambdaLabsHQ/unity-repl.git", REPL_REPO])


def repl_eval(code, timeout_s=30, check=True):
    proc = run([REPL, "--timeout", str(timeout_s), "-e", code], cwd=PROJECT, timeout=timeout_s + 15, check=False)
    if check and proc.returncode != 0:
        raise RuntimeError(proc.stderr + "\n" + proc.stdout)
    return proc


def repl_file(path, timeout_s=30):
    return run([REPL, "--timeout", str(timeout_s), "-f", path], cwd=PROJECT, timeout=timeout_s + 15)


def unity_ready():
    proc = repl_eval("Application.unityVersion", timeout_s=3, check=False)
    return proc.returncode == 0 and bool(proc.stdout.strip())


def start_unity_if_needed():
    if unity_ready():
        return None
    if not UNITY_BIN.exists():
        raise FileNotFoundError(f"UNITY_BIN not found: {UNITY_BIN}")
    UNITY_LOG.unlink(missing_ok=True)
    proc = subprocess.Popen(
        [str(UNITY_BIN), "-projectPath", str(PROJECT), "-logFile", str(UNITY_LOG)],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    deadline = time.time() + 180
    while time.time() < deadline:
        if unity_ready():
            return proc
        time.sleep(1)
    raise TimeoutError("Unity REPL did not become ready.")


def applescript(script):
    return run(["osascript", "-e", script]).stdout.strip()


def enter_unity_fullscreen():
    script = r'''
tell application "Unity" to activate
delay 0.5
tell application "System Events"
  tell process "Unity"
    set frontmost to true
    if (count of windows) is 0 then error "Unity has no visible window"
    try
      set value of attribute "AXFullScreen" of window 1 to true
    on error
      keystroke "f" using {control down, command down}
    end try
  end tell
end tell
delay 2.0
tell application "System Events"
  tell process "Unity"
    set frontmost to true
    set windowName to name of window 1
    set isFull to false
    try
      set isFull to value of attribute "AXFullScreen" of window 1
    end try
    return windowName & "|" & (isFull as text)
  end tell
end tell
'''
    return applescript(script)


def unity_window_id():
    script = r'''
import CoreGraphics

let windows = CGWindowListCopyWindowInfo(.optionOnScreenOnly, kCGNullWindowID) as! [[String: Any]]
var bestId: Int = 0
var bestArea: Int = 0
for window in windows {
    let owner = window[kCGWindowOwnerName as String] as? String ?? ""
    let layer = window[kCGWindowLayer as String] as? Int ?? 9999
    if !owner.contains("Unity") || layer != 0 { continue }
    guard let bounds = window[kCGWindowBounds as String] as? [String: Any] else { continue }
    let width = bounds["Width"] as? Int ?? 0
    let height = bounds["Height"] as? Int ?? 0
    let area = width * height
    if area > bestArea {
        bestArea = area
        bestId = window[kCGWindowNumber as String] as? Int ?? 0
    }
}
if bestId != 0 {
    print(bestId)
}
'''
    proc = run(["swift", "-e", script], check=False)
    value = proc.stdout.strip().splitlines()
    if not value:
        return None
    try:
        return int(value[-1])
    except ValueError:
        return None


def maximize_game_view():
    code = r'''
var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
gameView.Show();
gameView.Focus();
gameView.maximized = true;
UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
"game_view_maximized";
'''
    return repl_eval(code, timeout_s=10)


def ffprobe_json(path):
    proc = run(
        [
            "ffprobe",
            "-v", "error",
            "-print_format", "json",
            "-show_streams",
            "-show_format",
            path,
        ]
    )
    return json.loads(proc.stdout)


def convert_to_mp4():
    run(
        [
            "ffmpeg",
            "-y",
            "-i", RAW_MOV,
            "-vf", "scale=1920:1080:force_original_aspect_ratio=decrease,"
                   "pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x0b0d10,"
                   "format=yuv420p",
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-c:a", "aac",
            "-movflags", "+faststart",
            OUT_MP4,
        ]
    )


def extract_thumbnails():
    for old in OUT_DIR.glob("thumb_*.jpg"):
        old.unlink()
    thumbs = []
    for second in (2, 7, 12, 17):
        if second >= SECONDS:
            continue
        out = OUT_DIR / f"thumb_{second:02d}s.jpg"
        run(["ffmpeg", "-y", "-ss", str(second), "-i", OUT_MP4, "-frames:v", "1", out])
        thumbs.append(out)
    return thumbs


def screen_recording_help():
    return (
        "System screen recording failed. Grant Screen Recording / Screen & System Audio Recording "
        "permission to the app running this script (Codex, Terminal, iTerm2, or VS Code), then fully quit "
        "and reopen that app. You can reset with: tccutil reset ScreenCapture"
    )


def main():
    require_tool("screencapture")
    require_tool("ffmpeg")
    require_tool("ffprobe")
    require_tool("osascript")
    ensure_repl_repo()
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    unity_proc = start_unity_if_needed()
    summary = {
        "experiment": EXPERIMENT,
        "unity_started_by_script": unity_proc is not None,
        "unity_bin": str(UNITY_BIN),
        "seconds": SECONDS,
        "display": DISPLAY,
        "steps": [],
    }

    try:
        define_visual = repl_file(EXPERIMENT_FILE, timeout_s=30)
        summary["steps"].append({"label": f"define_{EXPERIMENT}_experiment", "stdout": define_visual.stdout.strip()})

        run_visual = repl_eval(RUN_CODE, timeout_s=90)
        summary["steps"].append({"label": f"run_{EXPERIMENT}_experiment", "stdout": run_visual.stdout.strip()})

        if PLAYER_FILE is not None:
            define_player = repl_file(PLAYER_FILE, timeout_s=30)
            summary["steps"].append({"label": "define_live_demo", "stdout": define_player.stdout.strip()})

        if os.environ.get("UNITY_FULLSCREEN_MAXIMIZE_GAME_VIEW", "1") != "0":
            try:
                game_view = maximize_game_view()
                summary["steps"].append({"label": "maximize_game_view", "stdout": game_view.stdout.strip()})
            except Exception as exc:
                summary["steps"].append({"label": "maximize_game_view_failed", "error": str(exc)})

        full = enter_unity_fullscreen()
        summary["fullscreen"] = full
        window_id = unity_window_id() if RECORD_WINDOW else None
        summary["record_window"] = bool(window_id)
        summary["unity_window_id"] = window_id

        RAW_MOV.unlink(missing_ok=True)
        OUT_MP4.unlink(missing_ok=True)
        capture_cmd = [
            "screencapture",
            "-x",
            "-v",
            "-V", str(SECONDS),
        ]
        if window_id:
            capture_cmd.append(f"-l{window_id}")
        else:
            capture_cmd.append(f"-D{DISPLAY}")
        capture_cmd.append(str(RAW_MOV))
        recorder = subprocess.Popen(
            capture_cmd,
            cwd=ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        time.sleep(1.0)
        if recorder.poll() is not None and not RAW_MOV.exists():
            stdout, stderr = recorder.communicate()
            raise RuntimeError(screen_recording_help() + "\n\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr)

        play = repl_eval(PLAY_CODE, timeout_s=max(60, SECONDS * 5))
        summary["steps"].append({"label": f"play_{EXPERIMENT}_demo", "stdout": play.stdout.strip()})

        stdout, stderr = recorder.communicate(timeout=SECONDS + 20)
        summary["screencapture"] = {"exit_code": recorder.returncode, "stdout": stdout, "stderr": stderr}
        if recorder.returncode != 0 or not RAW_MOV.exists() or RAW_MOV.stat().st_size == 0:
            raise RuntimeError(screen_recording_help() + "\n\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr)

        convert_to_mp4()
        thumbs = extract_thumbnails()
        summary["raw_mov"] = str(RAW_MOV)
        summary["video"] = str(OUT_MP4)
        summary["thumbnails"] = [str(x) for x in thumbs]
        summary["ffprobe"] = ffprobe_json(OUT_MP4)
        (OUT_DIR / "fullscreen_recording_summary.json").write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2))
    finally:
        if unity_proc is not None:
            repl_eval('EditorApplication.Exit(0); "exiting"', timeout_s=3, check=False)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        if UNITY_LOG.exists():
            print(UNITY_LOG.read_text(errors="ignore")[-8000:], file=sys.stderr)
        raise
