#!/usr/bin/env python3
import json
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "results" / "system_fullscreen_recording" / "repl_vs_mcp_unity_fullscreen_system.mp4"
METRICS = ROOT / "results" / "visual_repro" / "metrics.json"
CONTEXT = ROOT / "results" / "context_measure.json"
OUT_DIR = ROOT / "results" / "two_lane_comparison"
OVERLAY = OUT_DIR / "two_lane_overlay.png"
OUT_MP4 = OUT_DIR / "mcp_vs_repl_two_lane.mp4"
FONT = Path("/System/Library/Fonts/SFNS.ttf")


def run(cmd, cwd=ROOT):
    proc = subprocess.run(
        [str(x) for x in cmd],
        cwd=cwd,
        text=True,
        capture_output=True,
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
    if not shutil.which(name):
        raise FileNotFoundError(f"Missing required tool: {name}")


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


def load_json(path):
    return json.loads(path.read_text())


def metric_value(context, label, key):
    for item in context["files"]:
        if item["label"] == label:
            return item[key]
    raise KeyError(label)


def create_overlay(metrics, context):
    tools = metrics["coplay_readme_tool_count_observed"]
    tool_tokens = metrics["coplay_tools_reference_approx_tokens_observed"]
    workflow_tokens = metric_value(context, "coplay workflows reference", "approx_tokens_chars_div_4")
    discovered = metrics["coverage_actor_count_discovered_by_reflection"]

    left = (
        f"MCP TOOL TABLE PATH\n"
        f"{tools} registered tools; tools ref ~{tool_tokens:,} tokens\n"
        f"workflow ref ~{workflow_tokens:,}; no CanReach endpoint"
    )
    right = (
        "REPL PATH\n"
        "1 stable eval C# surface\n"
        f"discovers {discovered} actors; coroutine + screenshot + code"
    )
    footer = "Same Unity experiment, two views. MCP lane visualizes measured Coplay tool-table burden; REPL lane is the live Unity REPL run."

    run(
        [
            "magick",
            "-size", "1920x1080",
            "xc:none",
            "-fill", "rgba(5,8,13,0.78)",
            "-draw", "roundrectangle 18,18 942,150 18,18",
            "-draw", "roundrectangle 978,18 1902,150 18,18",
            "-draw", "roundrectangle 18,1006 1902,1064 16,16",
            "-fill", "#ffb247",
            "-font", str(FONT),
            "-pointsize", "34",
            "-interline-spacing", "8",
            "-gravity", "NorthWest",
            "-annotate", "+46+38",
            left,
            "-fill", "#36f58b",
            "-font", str(FONT),
            "-pointsize", "34",
            "-interline-spacing", "8",
            "-gravity", "NorthWest",
            "-annotate", "+1006+38",
            right,
            "-fill", "#d7e7ff",
            "-font", str(FONT),
            "-pointsize", "27",
            "-gravity", "NorthWest",
            "-annotate", "+46+1024",
            footer,
            OVERLAY,
        ]
    )


def make_video():
    # The source recording pans through REPL first, then MCP. Offset the same
    # Unity recording into two synchronized panes so the viewer can compare the
    # two paths at once.
    run(
        [
            "ffmpeg",
            "-y",
            "-ss", "8.4",
            "-t", "5.2",
            "-i", SOURCE,
            "-ss", "4.4",
            "-t", "5.2",
            "-i", SOURCE,
            "-i", OVERLAY,
            "-filter_complex",
            (
                "[0:v]crop=760:660:700:145,"
                "scale=960:1080:force_original_aspect_ratio=increase,"
                "crop=960:1080,setpts=2*(PTS-STARTPTS)[mcp];"
                "[1:v]crop=760:660:480:145,"
                "scale=960:1080:force_original_aspect_ratio=increase,"
                "crop=960:1080,setpts=2*(PTS-STARTPTS)[repl];"
                "[mcp][repl]hstack=inputs=2[stack];"
                "[stack][2:v]overlay=0:0,format=yuv420p[v]"
            ),
            "-map", "[v]",
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-movflags", "+faststart",
            OUT_MP4,
        ]
    )


def extract_thumbnails():
    for old in OUT_DIR.glob("thumb_*.jpg"):
        old.unlink()
    thumbs = []
    for second in (1, 5, 9):
        out = OUT_DIR / f"thumb_{second:02d}s.jpg"
        run(["ffmpeg", "-y", "-ss", str(second), "-i", OUT_MP4, "-frames:v", "1", out])
        thumbs.append(out)
    return thumbs


def main():
    require_tool("ffmpeg")
    require_tool("ffprobe")
    require_tool("magick")
    if not SOURCE.exists():
        raise FileNotFoundError(f"Missing source Unity recording: {SOURCE}")
    if not METRICS.exists():
        raise FileNotFoundError(f"Missing metrics: {METRICS}")
    if not CONTEXT.exists():
        raise FileNotFoundError(f"Missing context metrics: {CONTEXT}")
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    metrics = load_json(METRICS)
    context = load_json(CONTEXT)
    create_overlay(metrics, context)
    make_video()
    thumbs = extract_thumbnails()
    summary = {
        "video": str(OUT_MP4),
        "source": str(SOURCE),
        "overlay": str(OVERLAY),
        "thumbnails": [str(x) for x in thumbs],
        "note": "MCP lane visualizes the measured Coplay tool-table path; REPL lane uses the live Unity REPL recording.",
        "ffprobe": ffprobe_json(OUT_MP4),
    }
    (OUT_DIR / "two_lane_summary.json").write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
