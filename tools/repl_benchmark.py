#!/usr/bin/env python3
import json
import subprocess
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "BenchProject"
RESULTS = ROOT / "results"
REPL = Path("/tmp/lambdalabs-unity-repl/repl.sh")
RESULTS.mkdir(exist_ok=True)


def run_repl(label, code, timeout=60):
    start = time.perf_counter()
    proc = subprocess.run(
        [str(REPL), "--timeout", str(timeout), "-e", code],
        cwd=PROJECT,
        text=True,
        capture_output=True,
        timeout=timeout + 10,
    )
    elapsed = time.perf_counter() - start
    return {
        "label": label,
        "elapsed_s": elapsed,
        "exit_code": proc.returncode,
        "stdout": proc.stdout.strip(),
        "stderr": proc.stderr.strip(),
        "code_chars": len(code),
        "code": code,
    }


def wait_for_repl(max_wait_s=180):
    deadline = time.time() + max_wait_s
    attempts = []
    while time.time() < deadline:
        result = run_repl("wait_probe", "Application.unityVersion", timeout=5)
        attempts.append({
            "elapsed_s": result["elapsed_s"],
            "exit_code": result["exit_code"],
            "stdout": result["stdout"],
            "stderr": result["stderr"],
        })
        if result["exit_code"] == 0 and result["stdout"]:
            return result, attempts
        time.sleep(1)
    raise RuntimeError(f"REPL did not become ready after {max_wait_s}s. Attempts: {attempts[-5:]}")


def main():
    results = []
    ready, attempts = wait_for_repl()
    results.append({
        "label": "ready",
        "result": ready,
        "attempt_count": len(attempts),
    })

    setup_code = r'''
EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
var enemy = new GameObject("BenchEnemy");
enemy.transform.position = new Vector3(10, 0, 5);
enemy.AddComponent<Health>().hitPoints = 100;
var turretData = new [] {
    new { name = "LaserTurret_1", pos = new Vector3(0, 0, 0), range = 20f },
    new { name = "GrenadeTurret_2", pos = new Vector3(5, 0, 3), range = 8f },
    new { name = "SniperTurret_3", pos = new Vector3(-20, 0, -20), range = 12f },
};
foreach (var item in turretData) {
    var go = new GameObject(item.name);
    go.transform.position = item.pos;
    var turret = go.AddComponent<Turret>();
    turret.range = item.range;
    turret.currentTarget = enemy.transform;
}
"setup turrets=" + GameObject.FindObjectsOfType<Turret>().Length + " hp=" + enemy.GetComponent<Health>().hitPoints
'''.strip()
    results.append(run_repl("repl_setup_custom_scene", setup_code))

    query_code = r'''
var pos = GameObject.Find("BenchEnemy").transform.position;
string.Join("\n", GameObject.FindObjectsOfType<Turret>()
    .OrderBy(t => t.name)
    .Select(t => $"{t.name}: distance={t.DistanceTo(pos):F2}, canReach={t.CanReach(pos)}"))
'''.strip()
    results.append(run_repl("repl_long_tail_custom_query", query_code))

    define_coroutine = r'''
public static class BenchAsync {
    public static System.Collections.IEnumerator WaitDamage() {
        var health = GameObject.Find("BenchEnemy").GetComponent<Health>();
        yield return new WaitForSeconds(0.5f);
        health.Damage(7);
        yield return "hp=" + health.hitPoints;
    }
}
'''.strip()
    results.append(run_repl("repl_define_coroutine", define_coroutine))
    results.append(run_repl("repl_run_coroutine", "BenchAsync.WaitDamage()", timeout=10))

    crystallize_code = r'''
Directory.CreateDirectory("Assets/Editor");
File.WriteAllText("Assets/Editor/TurretRangeProbe.cs", @"using System.Linq;
using UnityEngine;

public static class TurretRangeProbe
{
    public static string RunAt(Vector3 pos)
    {
        return string.Join(""\n"", GameObject.FindObjectsOfType<Turret>()
            .OrderBy(t => t.name)
            .Select(t => $""{t.name}: distance={t.DistanceTo(pos):F2}, canReach={t.CanReach(pos)}""));
    }
}");
AssetDatabase.Refresh();
"crystallized"
'''.strip()
    results.append(run_repl("repl_crystallize_editor_script", crystallize_code, timeout=20))

    # AssetDatabase.Refresh may trigger a domain reload. Wait for the REPL server to come back.
    post_reload, post_attempts = wait_for_repl(max_wait_s=120)
    results.append({
        "label": "post_crystallize_ready",
        "result": post_reload,
        "attempt_count": len(post_attempts),
    })
    results.append(run_repl(
        "repl_call_crystallized_tool",
        'TurretRangeProbe.RunAt(GameObject.Find("BenchEnemy").transform.position)',
        timeout=20,
    ))

    payload = {
        "repl_git_head": subprocess.run(
            ["git", "-C", "/tmp/lambdalabs-unity-repl", "log", "-1", "--format=%H %ci %s"],
            text=True,
            capture_output=True,
            check=False,
        ).stdout.strip(),
        "project": str(PROJECT),
        "results": results,
    }
    (RESULTS / "repl_benchmark.json").write_text(json.dumps(payload, indent=2))
    print(json.dumps(payload, indent=2))


if __name__ == "__main__":
    main()
