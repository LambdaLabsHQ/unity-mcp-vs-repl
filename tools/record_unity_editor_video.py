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
OUT_DIR = ROOT / "results" / "unity_editor_recording"
FRAME_DIR = OUT_DIR / "frames"
OUT_MP4 = OUT_DIR / "repl_vs_mcp_unity_editor_live.mp4"
FPS = int(os.environ.get("UNITY_RECORD_FPS", "12"))
SECONDS = int(os.environ.get("UNITY_RECORD_SECONDS", "24"))
OUTPUT_FPS = int(os.environ.get("UNITY_RECORD_OUTPUT_FPS", "20"))
UNITY_LOG = ROOT / "unity-editor-recording.log"


def run(cmd, cwd=ROOT, timeout=None):
    proc = subprocess.run(
        [str(x) for x in cmd],
        cwd=cwd,
        text=True,
        capture_output=True,
        timeout=timeout,
    )
    if proc.returncode != 0:
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


def repl_eval(code, timeout_s=30, check=True):
    proc = subprocess.run(
        [str(REPL), "--timeout", str(timeout_s), "-e", code],
        cwd=PROJECT,
        text=True,
        capture_output=True,
        timeout=timeout_s + 15,
    )
    if check and proc.returncode != 0:
        raise RuntimeError(proc.stderr + "\n" + proc.stdout)
    return proc


def repl_file(path, timeout_s=30):
    return run([REPL, "--timeout", str(timeout_s), "-f", path], cwd=PROJECT, timeout=timeout_s + 15)


def ensure_repl_repo():
    if REPL.exists():
        return
    run(["git", "clone", "--depth", "1", "https://github.com/LambdaLabsHQ/unity-repl.git", REPL_REPO])


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
        [
            str(UNITY_BIN),
            "-projectPath", str(PROJECT),
            "-logFile", str(UNITY_LOG),
        ],
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


def unity_window_geometry():
    script = r'''
tell application "Unity" to activate
delay 0.5
tell application "System Events"
  tell process "Unity"
    set frontmost to true
    if (count of windows) is 0 then error "Unity has no visible window"
    set position of window 1 to {0, 34}
    set size of window 1 to {1710, 986}
    delay 0.5
    set p to position of window 1
    set s to size of window 1
    return ((item 1 of p) as text) & "," & ((item 2 of p) as text) & "," & ((item 1 of s) as text) & "," & ((item 2 of s) as text) & "," & (name of window 1)
  end tell
end tell
'''
    raw = applescript(script)
    parts = raw.split(",", 4)
    if len(parts) != 5:
        raise RuntimeError(f"Unexpected Unity window geometry: {raw}")
    x, y, w, h = [int(float(v.strip())) for v in parts[:4]]
    title = parts[4].strip()
    return {"x": x, "y": y, "width": w, "height": h, "title": title}


def csharp_string(value):
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


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


def encode_video(frame_count):
    duration = frame_count / FPS
    fps_filter = f"framerate=fps={OUTPUT_FPS}," if OUTPUT_FPS > FPS else ""
    run(
        [
            "ffmpeg",
            "-y",
            "-framerate", str(FPS),
            "-i", FRAME_DIR / "frame_%04d.jpg",
            "-f", "lavfi",
            "-t", f"{duration:.3f}",
            "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
            "-vf",
            "scale=1920:1080:force_original_aspect_ratio=decrease,"
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x0b0d10,"
            + fps_filter +
            "format=yuv420p",
            "-map", "0:v:0",
            "-map", "1:a:0",
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-c:a", "aac",
            "-shortest",
            "-movflags", "+faststart",
            OUT_MP4,
        ]
    )


def extract_thumbnails():
    for old in OUT_DIR.glob("thumb_*.jpg"):
        old.unlink()
    thumbs = []
    for second in (2, 7, 12, 17, 22):
        if second >= SECONDS:
            continue
        out = OUT_DIR / f"thumb_{second:02d}s.jpg"
        run(["ffmpeg", "-y", "-ss", str(second), "-i", OUT_MP4, "-frames:v", "1", out])
        thumbs.append(out)
    return thumbs


def main():
    require_tool("ffmpeg")
    require_tool("ffprobe")
    ensure_repl_repo()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    FRAME_DIR.mkdir(parents=True, exist_ok=True)

    unity_proc = start_unity_if_needed()
    summary = {
        "unity_started_by_script": unity_proc is not None,
        "unity_bin": str(UNITY_BIN),
        "fps": FPS,
        "output_fps": OUTPUT_FPS,
        "seconds": SECONDS,
        "steps": [],
    }

    try:
        geometry = unity_window_geometry()
        summary["window"] = geometry

        define_visual = repl_file(ROOT / "experiments/visual_repro/VisualReproExperiment.cs", timeout_s=30)
        summary["steps"].append({"label": "define_visual_experiment", "stdout": define_visual.stdout.strip()})

        run_visual = repl_eval("VisualReproExperiment.Run(1337)", timeout_s=90)
        summary["steps"].append({"label": "run_visual_experiment", "stdout": run_visual.stdout.strip()})

        define_recorder = repl_file(ROOT / "experiments/visual_repro/UnityEditorLiveRecording.cs", timeout_s=30)
        summary["steps"].append({"label": "define_live_recorder", "stdout": define_recorder.stdout.strip()})

        record_call = (
            "UnityEditorLiveRecording.Record("
            + csharp_string(str(FRAME_DIR))
            + f", {geometry['x']}, {geometry['y']}, {geometry['width']}, {geometry['height']}, {FPS}, {SECONDS})"
        )
        recorded = repl_eval(record_call, timeout_s=max(180, SECONDS * 20))
        summary["steps"].append({"label": "record_unity_editor_frames", "stdout": recorded.stdout.strip()})

        frames = sorted(FRAME_DIR.glob("frame_*.jpg"))
        if len(frames) < FPS * SECONDS:
            raise RuntimeError(f"Expected {FPS * SECONDS} frames, got {len(frames)}")
        summary["frame_count"] = len(frames)

        encode_video(len(frames))
        thumbs = extract_thumbnails()
        summary["video"] = str(OUT_MP4)
        summary["thumbnails"] = [str(x) for x in thumbs]
        summary["ffprobe"] = ffprobe_json(OUT_MP4)
        summary_path = OUT_DIR / "recording_summary.json"
        summary_path.write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2))
    finally:
        if unity_proc is not None:
            try:
                repl_eval('EditorApplication.Exit(0); "exiting"', timeout_s=3, check=False)
            except Exception:
                pass
            try:
                unity_proc.wait(timeout=30)
            except Exception:
                unity_proc.terminate()


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        if UNITY_LOG.exists():
            print(UNITY_LOG.read_text(errors="ignore")[-8000:], file=sys.stderr)
        raise
