#!/usr/bin/env python3
import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VISUAL_RESULTS = ROOT / "results" / "visual_repro"
SOURCE_IMAGE = VISUAL_RESULTS / "turret_coverage_lab.png"
METRICS_JSON = VISUAL_RESULTS / "metrics.json"
VIDEO_DIR = ROOT / "results" / "video"
SLIDE_DIR = VIDEO_DIR / "slides"
OUT_MP4 = VIDEO_DIR / "repl_vs_mcp_comparison.mp4"
FONT = Path(os.environ.get("VIDEO_FONT", "/System/Library/Fonts/SFNS.ttf"))
WIDTH = 1920
HEIGHT = 1080
FPS = 30


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
    path = shutil.which(name)
    if not path:
        raise FileNotFoundError(f"Required tool not found on PATH: {name}")
    return path


def text_lines(text):
    return [line.rstrip() for line in text.rstrip().splitlines()]


def add_text_block(args, x, y, width, lines, font_size, line_height, fill, box_fill, text_y_offset):
    box_height = len(lines) * line_height + 32
    args.extend(
        [
            "-fill",
            box_fill,
            "-draw",
            f"roundrectangle {x},{y} {x + width},{y + box_height} 12,12",
            "-fill",
            fill,
            "-font",
            str(FONT),
            "-pointsize",
            str(font_size),
            "-interline-spacing",
            str(max(0, line_height - font_size)),
            "-gravity",
            "NorthWest",
            "-annotate",
            f"+{x + 18}+{y + text_y_offset}",
            "\n".join(lines),
        ]
    )


def render_slide(image, out_png, seg):
    title = text_lines(seg["title"])
    body = text_lines(seg["body"])
    footer = text_lines(seg["footer"])
    footer_y = HEIGHT - (len(footer) * 38 + 72)
    args = ["magick", image]
    if seg.get("crop"):
        x, y, w, h = seg["crop"]
        args.extend(["-crop", f"{w}x{h}+{x}+{y}", "+repage"])
    args.extend(
        [
            "-resize",
            f"{WIDTH}x{HEIGHT}^",
            "-gravity",
            "center",
            "-extent",
            f"{WIDTH}x{HEIGHT}",
            "-alpha",
            "set",
            "-fill",
            "rgba(0,0,0,0.16)",
            "-draw",
            f"rectangle 0,0 {WIDTH},{HEIGHT}",
            "-fill",
            "#4bd0ff",
            "-draw",
            f"rectangle 0,0 {WIDTH},6",
        ]
    )
    add_text_block(args, 48, 38, 1680, title, 54, 68, "white", "rgba(5,8,13,0.72)", 20)
    add_text_block(args, 48, 146, 1320, body, 34, 48, "white", "rgba(5,8,13,0.58)", 20)
    add_text_block(args, 48, footer_y, 1620, footer, 27, 38, "#d7e7ff", "rgba(5,8,13,0.62)", 18)
    args.append(out_png)
    run(args)


def make_segment(slide_image, out_path, seg):
    fade_out_start = max(0, seg["duration"] - 0.35)
    vf = f"fade=t=in:st=0:d=0.35,fade=t=out:st={fade_out_start:.2f}:d=0.35,format=yuv420p"
    run(
        [
            "ffmpeg",
            "-y",
            "-loop",
            "1",
            "-framerate",
            str(FPS),
            "-t",
            str(seg["duration"]),
            "-i",
            slide_image,
            "-f",
            "lavfi",
            "-t",
            str(seg["duration"]),
            "-i",
            "anullsrc=channel_layout=stereo:sample_rate=48000",
            "-vf",
            vf,
            "-map",
            "0:v:0",
            "-map",
            "1:a:0",
            "-c:v",
            "libx264",
            "-preset",
            "veryfast",
            "-crf",
            "18",
            "-c:a",
            "aac",
            "-shortest",
            out_path,
        ]
    )


def concat_segments(segment_paths, out_path):
    with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False) as handle:
        list_path = Path(handle.name)
        for path in segment_paths:
            safe = str(path).replace("'", "'\\''")
            handle.write(f"file '{safe}'\n")
    try:
        run(["ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", list_path, "-c", "copy", out_path])
    finally:
        list_path.unlink(missing_ok=True)


def ffprobe_json(path):
    proc = run(
        [
            "ffprobe",
            "-v",
            "error",
            "-print_format",
            "json",
            "-show_streams",
            "-show_format",
            path,
        ]
    )
    return json.loads(proc.stdout)


def extract_thumbnails(path):
    for old in VIDEO_DIR.glob("thumb_*.png"):
        old.unlink()
    thumbs = []
    for second in (2, 7, 11, 16, 21):
        out = VIDEO_DIR / f"thumb_{second:02d}s.png"
        run(["ffmpeg", "-y", "-ss", str(second), "-i", path, "-frames:v", "1", out])
        thumbs.append(out)
    return thumbs


def load_metrics():
    if not METRICS_JSON.exists():
        raise FileNotFoundError(f"Missing metrics: {METRICS_JSON}")
    return json.loads(METRICS_JSON.read_text())


def build_segments(metrics):
    repl_tools = metrics["repl_interface_count"]
    discovered = metrics["coverage_actor_count_discovered_by_reflection"]
    tool_count = metrics["coplay_readme_tool_count_observed"]
    tool_tokens = metrics["coplay_tools_reference_approx_tokens_observed"]
    workflow_tokens = 18646
    zero_risk = metrics["path_zero_risk_samples"]
    path_samples = metrics["path_sample_count"]
    max_risk = metrics["path_max_risk"]

    return [
        {
            "duration": 4.0,
            "title": "REPL vs MCP: Tool Table vs Language Evaluation",
            "body": (
                "Recorded from a deterministic Unity experiment.\n"
                "Same scene, same seed, reproducible output.\n\n"
                "The measured question is not who has more buttons.\n"
                "The question is who avoids the context and recall tax."
            ),
            "footer": "Generated from results/visual_repro/turret_coverage_lab.png",
        },
        {
            "duration": 5.0,
            "crop": (260, 100, 760, 850),
            "title": "REPL Path: one stable interface",
            "body": (
                f"{repl_tools} interface: eval C# in the Unity Editor.\n"
                "No endpoint list for scene graph, assets, LINQ, reflection, or coroutines.\n"
                "The agent writes the host language directly:\n\n"
                "VisualReproExperiment.Run(1337)"
            ),
            "footer": "The tool surface is the Unity C# API plus project code.",
        },
        {
            "duration": 5.0,
            "crop": (830, 95, 760, 855),
            "title": "MCP Tool Table Path: context expands",
            "body": (
                f"Observed Coplay README tool count: {tool_count}\n"
                f"Tools reference: about {tool_tokens:,} tokens\n"
                f"Workflow reference: about {workflow_tokens:,} tokens\n\n"
                "More registered tools increase prompt load, memory burden,\n"
                "and long-tail recall failures."
            ),
            "footer": "When the generic escape hatch is execute_code, MCP has reintroduced REPL.",
        },
        {
            "duration": 5.0,
            "crop": (250, 200, 1250, 700),
            "title": "Long-tail project logic: no endpoint required",
            "body": (
                f"Reflection discovered {discovered} actors with CanReach(Vector3).\n"
                "The experiment then composed geometry, custom methods,\n"
                "line-of-sight checks, heatmap rendering, screenshot capture,\n"
                "and an Editor script crystallization step."
            ),
            "footer": f"Path samples: {path_samples}; zero-risk samples: {zero_risk}; max risk: {max_risk}.",
        },
        {
            "duration": 4.0,
            "title": "Conclusion",
            "body": (
                "MCP wins when the task fits the registered tools.\n\n"
                "REPL wins decisively when the task is novel, project-specific,\n"
                "stateful, asynchronous, or compositional.\n\n"
                "That is the Unity case by default."
            ),
            "footer": "Reproduce: python3 tools/run_visual_repro.py && python3 tools/make_comparison_video.py",
        },
    ]


def main():
    require_tool("ffmpeg")
    require_tool("ffprobe")
    require_tool("magick")
    if not SOURCE_IMAGE.exists():
        raise FileNotFoundError(
            f"Missing source image: {SOURCE_IMAGE}\n"
            "Run `python3 tools/run_visual_repro.py` first."
        )
    if not FONT.exists():
        raise FileNotFoundError(f"Missing font: {FONT}")

    VIDEO_DIR.mkdir(parents=True, exist_ok=True)
    SLIDE_DIR.mkdir(parents=True, exist_ok=True)

    metrics = load_metrics()
    segments = build_segments(metrics)

    with tempfile.TemporaryDirectory(prefix="repl_vs_mcp_video_") as tmp:
        tmpdir = Path(tmp)
        segment_paths = []
        for index, seg in enumerate(segments, start=1):
            slide_png = SLIDE_DIR / f"slide_{index:02d}.png"
            render_slide(SOURCE_IMAGE, slide_png, seg)
            out = tmpdir / f"segment_{index:02d}.mp4"
            make_segment(slide_png, out, seg)
            segment_paths.append(out)
        concat_segments(segment_paths, OUT_MP4)

    thumbs = extract_thumbnails(OUT_MP4)
    probe = ffprobe_json(OUT_MP4)
    summary = {
        "video": str(OUT_MP4),
        "source_image": str(SOURCE_IMAGE),
        "metrics": str(METRICS_JSON),
        "font": str(FONT),
        "slides": [str(SLIDE_DIR / f"slide_{index:02d}.png") for index in range(1, len(segments) + 1)],
        "segments": [
            {"duration": seg["duration"], "title": seg["title"], "crop": seg.get("crop")}
            for seg in segments
        ],
        "thumbnails": [str(x) for x in thumbs],
        "ffprobe": probe,
    }
    summary_path = VIDEO_DIR / "video_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
