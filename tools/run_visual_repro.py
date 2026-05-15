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
REPL_REPO = Path("/tmp/lambdalabs-unity-repl")
REPL = REPL_REPO / "repl.sh"
UNITY_BIN = Path(os.environ.get(
    "UNITY_BIN",
    "/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity",
))
RESULTS = ROOT / "results" / "visual_repro"
LOG = ROOT / "visual-repro-unity.log"


def run(cmd, cwd=ROOT, timeout=None):
    return subprocess.run(
        [str(x) for x in cmd],
        cwd=cwd,
        text=True,
        capture_output=True,
        timeout=timeout,
    )


def ensure_repl_repo():
    if REPL.exists():
        return
    if REPL_REPO.exists():
        shutil.rmtree(REPL_REPO)
    subprocess.run(
        ["git", "clone", "--depth", "1", "https://github.com/LambdaLabsHQ/unity-repl.git", str(REPL_REPO)],
        check=True,
    )


def start_unity():
    if not UNITY_BIN.exists():
        raise FileNotFoundError(f"UNITY_BIN not found: {UNITY_BIN}")
    LOG.unlink(missing_ok=True)
    return subprocess.Popen(
        [
            str(UNITY_BIN),
            "-batchmode",
            "-projectPath", str(PROJECT),
            "-logFile", str(LOG),
        ],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def repl_eval(code, timeout_s=30):
    start = time.perf_counter()
    proc = run([REPL, "--timeout", str(timeout_s), "-e", code], cwd=PROJECT, timeout=timeout_s + 10)
    elapsed = time.perf_counter() - start
    return {
        "elapsed_s": elapsed,
        "exit_code": proc.returncode,
        "stdout": proc.stdout.strip(),
        "stderr": proc.stderr.strip(),
        "code": code,
    }


def repl_file(path, timeout_s=30):
    start = time.perf_counter()
    proc = run([REPL, "--timeout", str(timeout_s), "-f", path], cwd=PROJECT, timeout=timeout_s + 10)
    elapsed = time.perf_counter() - start
    return {
        "elapsed_s": elapsed,
        "exit_code": proc.returncode,
        "stdout": proc.stdout.strip(),
        "stderr": proc.stderr.strip(),
        "file": str(path),
    }


def wait_for_repl(timeout_s=240):
    deadline = time.time() + timeout_s
    attempts = []
    while time.time() < deadline:
        result = repl_eval("Application.unityVersion", timeout_s=5)
        attempts.append(result)
        if result["exit_code"] == 0 and result["stdout"]:
            return attempts
        time.sleep(1)
    raise TimeoutError("Unity REPL did not become ready")


def main():
    RESULTS.mkdir(parents=True, exist_ok=True)
    ensure_repl_repo()

    proc = start_unity()
    summary = {
        "unity_bin": str(UNITY_BIN),
        "project": str(PROJECT),
        "repl_repo_head": run(["git", "-C", REPL_REPO, "log", "-1", "--format=%H %ci %s"]).stdout.strip(),
        "steps": [],
    }
    try:
        attempts = wait_for_repl()
        summary["steps"].append({"label": "ready", "attempts": attempts})

        define = repl_file(ROOT / "experiments/visual_repro/VisualReproExperiment.cs", timeout_s=30)
        summary["steps"].append({"label": "define_visual_experiment", "result": define})
        if define["exit_code"] != 0:
            raise RuntimeError(define)

        execute = repl_eval("VisualReproExperiment.Run(1337)", timeout_s=90)
        summary["steps"].append({"label": "execute_visual_experiment", "result": execute})
        if execute["exit_code"] != 0:
            raise RuntimeError(execute)

        summary["outputs"] = {
            "png": str(RESULTS / "turret_coverage_lab.png"),
            "metrics": str(RESULTS / "metrics.json"),
            "scene": str(PROJECT / "Assets/VisualRepro/TurretCoverageLab.unity"),
            "crystallized_probe": str(PROJECT / "Assets/Editor/CoverageProbe.cs"),
        }
    finally:
        try:
            repl_eval('EditorApplication.Exit(0); "exiting"', timeout_s=3)
        except Exception:
            pass
        try:
            proc.wait(timeout=30)
        except subprocess.TimeoutExpired:
            proc.terminate()
            proc.wait(timeout=10)

    out = RESULTS / "run_summary.json"
    out.write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        if LOG.exists():
            print(LOG.read_text(errors="ignore")[-8000:], file=sys.stderr)
        raise
