using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IShowdownCoverageActor
{
    bool CanReach(Vector3 point);
}

public sealed class ShowdownTurret : MonoBehaviour, IShowdownCoverageActor
{
    public float Range = 4f;

    public bool CanReach(Vector3 point)
    {
        var flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        var flatPoint = new Vector3(point.x, 0f, point.z);
        return Vector3.Distance(flatSelf, flatPoint) <= Range;
    }
}

public static class CapabilityShowdownExperiment
{
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    private static readonly List<GameObject> McpTokens = new List<GameObject>();
    private static readonly List<GameObject> ReplTokens = new List<GameObject>();
    private static readonly List<GameObject> ReplChecks = new List<GameObject>();
    private static readonly List<Vector3> Route = new List<Vector3>();
    private static readonly List<Bounds> Obstacles = new List<Bounds>();
    private static readonly List<ShowdownTurret> Turrets = new List<ShowdownTurret>();
    private static LineRenderer ReplBeam;
    private static GameObject ReplRunner;
    private static GameObject McpRunner;
    private static GameObject McpGate;
    private static Camera Cam;

    public static IEnumerator Run(int seed)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var repoRoot = Directory.GetParent(projectRoot).FullName;
        var outDir = Path.Combine(repoRoot, "results", "capability_showdown");
        Directory.CreateDirectory(outDir);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Mats.Clear();
        McpTokens.Clear();
        ReplTokens.Clear();
        ReplChecks.Clear();
        Route.Clear();
        Obstacles.Clear();
        Turrets.Clear();

        CreateMaterials();
        BuildLighting();
        Label("SAME TASK: reach GOAL through custom project logic", new Vector3(-4.0f, 0.32f, 9.25f), 0.44f, new Color(0.88f, 0.95f, 1f, 1f));
        Label("left: tool registry recall   |   right: one C# eval surface", new Vector3(-3.1f, 0.32f, 8.65f), 0.26f, new Color(0.75f, 0.86f, 0.95f, 1f));
        BuildMcpLane();
        BuildReplLane();
        BuildCamera();

        yield return null;

        var actors = DiscoverCanReachActors();
        var routeRisk = Route.Select(p => CoverageAt(actors, p)).ToArray();
        var maxRisk = routeRisk.Length == 0 ? 0 : routeRisk.Max();
        Label("validated by reflection: " + actors.Count + " CanReach(Vector3) actors, max route risk=" + maxRisk,
            new Vector3(2.4f, 0.25f, -11.4f), 0.34f, new Color(0.4f, 1f, 0.72f, 1f));

        var scenePath = "Assets/VisualRepro/CapabilityShowdown.unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

        var pngPath = Path.Combine(outDir, "capability_showdown.png");
        CapturePng(pngPath, 1920, 1080);
        var metricsPath = Path.Combine(outDir, "metrics.json");
        File.WriteAllText(metricsPath, "{\n" +
            "  \"experiment\": \"Capability Showdown\",\n" +
            "  \"custom_can_reach_actors\": " + actors.Count + ",\n" +
            "  \"mcp_registered_tools_visualized\": 42,\n" +
            "  \"coplay_tools_reference_approx_tokens\": 15096,\n" +
            "  \"coplay_workflows_reference_approx_tokens\": 18646,\n" +
            "  \"repl_interfaces\": 1,\n" +
            "  \"route_waypoints\": " + Route.Count + ",\n" +
            "  \"max_route_risk_after_repl_plan\": " + maxRisk + "\n" +
            "}\n");

        yield return "CAPABILITY_SHOWDOWN_DONE png=" + pngPath + " metrics=" + metricsPath + " scene=" + scenePath;
    }

    public static IEnumerator PlayDemo(int seconds)
    {
        if (seconds <= 0) seconds = 18;
        if (Cam == null) Cam = GameObject.Find("Capability_Showdown_Camera")?.GetComponent<Camera>();
        if (Cam == null) yield break;

        double start = EditorApplication.timeSinceStartup;
        while (true)
        {
            float elapsed = (float)(EditorApplication.timeSinceStartup - start);
            if (elapsed >= seconds) break;
            Animate(elapsed, Mathf.Clamp01(elapsed / seconds));
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
            yield return null;
        }
        Animate(seconds, 1f);
        yield return "CAPABILITY_SHOWDOWN_PLAY_DONE seconds=" + seconds;
    }

    private static void BuildMcpLane()
    {
        Panel(new Vector3(-10.7f, 0f, 0f), new Vector3(10.2f, 0.18f, 18.2f), new Color(0.14f, 0.12f, 0.09f, 1f));
        Label("MCP TOOL TABLE", new Vector3(-15.45f, 0.32f, 8.4f), 0.58f, new Color(1f, 0.68f, 0.22f, 1f));
        Label("registered endpoints become the search space", new Vector3(-15.45f, 0.32f, 7.55f), 0.28f, new Color(1f, 0.86f, 0.58f, 1f));

        var names = new[]
        {
            "manage_scene", "manage_gameobject", "manage_components", "manage_asset", "manage_material", "manage_physics",
            "manage_camera", "manage_ui", "manage_prefabs", "manage_script", "unity_reflect", "read_console", "run_tests",
            "execute_menu_item", "manage_build", "manage_packages", "manage_texture", "manage_shader", "manage_vfx",
            "manage_profiler", "manage_animation", "manage_graphics", "manage_probuilder", "batch_execute", "validate_script",
            "find_gameobjects", "refresh_unity", "unity_docs", "create_script", "delete_script", "find_in_file", "apply_text_edits",
            "script_apply_edits", "execute_custom_tool", "manage_tools", "manage_scriptable_object", "debug_request_context",
            "get_sha", "get_test_job", "set_active_instance", "manage_editor", "execute_code"
        };

        for (int i = 0; i < names.Length; i++)
        {
            int col = i % 7;
            int row = i / 7;
            var token = Cube("MCP_tool_" + names[i], new Vector3(-15.4f + col * 1.35f, 0.32f, 5.8f - row * 0.86f),
                new Vector3(1.18f, 0.16f, 0.42f), Mats["mcp"]);
            token.SetActive(false);
            McpTokens.Add(token);
            Label(names[i], token.transform.position + new Vector3(-0.53f, 0.18f, 0.11f), 0.105f, Color.black);
        }

        Cube("MCP_Context_Bar_Back", new Vector3(-10.8f, 0.28f, -0.7f), new Vector3(8.7f, 0.12f, 0.42f), Mats["dark"]);
        Cube("MCP_Context_Bar_Fill", new Vector3(-11.5f, 0.39f, -0.7f), new Vector3(7.3f, 0.16f, 0.5f), Mats["danger"]);
        Label("context load: tools ~15k tokens + workflows ~18.6k", new Vector3(-15.1f, 0.45f, -1.2f), 0.22f, new Color(1f, 0.72f, 0.35f, 1f));

        McpGate = Cube("MCP_Missing_Endpoint_Gate", new Vector3(-10.8f, 0.7f, -3.55f), new Vector3(8.1f, 1.0f, 0.32f), Mats["danger"]);
        Label("NO ENDPOINT: CanReach(Vector3)", new Vector3(-14.2f, 1.35f, -3.36f), 0.36f, Color.white);
        Label("fallback = execute_code => REPL inside MCP", new Vector3(-14.2f, 0.48f, -4.3f), 0.26f, new Color(1f, 0.84f, 0.6f, 1f));
        Label("PATH FAILED", new Vector3(-14.95f, 1.18f, -5.15f), 0.72f, new Color(1f, 0.16f, 0.1f, 1f));
        DrawPolyline("MCP_Fail_X_A", new[]
        {
            new Vector3(-9.1f, 0.95f, -2.85f),
            new Vector3(-8.25f, 0.95f, -4.15f)
        }, Mats["dangerLine"], 0.16f);
        DrawPolyline("MCP_Fail_X_B", new[]
        {
            new Vector3(-8.25f, 0.95f, -2.85f),
            new Vector3(-9.1f, 0.95f, -4.15f)
        }, Mats["dangerLine"], 0.16f);

        McpRunner = Sphere("MCP_Agent_Stuck", new Vector3(-15.2f, 0.62f, -6.5f), 0.42f, Mats["mcp"]);
        DrawPolyline("MCP_Failed_Path", new[]
        {
            new Vector3(-15.2f, 0.75f, -6.5f),
            new Vector3(-13.8f, 0.75f, -5.4f),
            new Vector3(-12.4f, 0.75f, -4.6f),
            new Vector3(-11.2f, 0.75f, -3.7f)
        }, Mats["dangerLine"], 0.10f);
        Label("agent recalls and composes tools\nbut project-specific API is outside the table",
            new Vector3(-15.0f, 0.32f, -7.3f), 0.26f, new Color(1f, 0.86f, 0.58f, 1f));
    }

    private static void BuildReplLane()
    {
        Panel(new Vector3(10.7f, 0f, 0f), new Vector3(10.2f, 0.18f, 18.2f), new Color(0.06f, 0.13f, 0.11f, 1f));
        Label("REPL: LANGUAGE EVALUATION", new Vector3(5.65f, 0.32f, 8.4f), 0.58f, new Color(0.3f, 1f, 0.48f, 1f));
        Label("one eval(C#) surface reaches Unity API + project code", new Vector3(5.65f, 0.32f, 7.55f), 0.28f, new Color(0.7f, 1f, 0.78f, 1f));

        Cube("REPL_Console", new Vector3(7.25f, 0.32f, 5.75f), new Vector3(3.2f, 0.18f, 1.1f), Mats["dark"]);
        Label("eval C#\nreflection + LINQ + coroutine", new Vector3(5.85f, 0.5f, 6.06f), 0.23f, new Color(0.38f, 1f, 0.56f, 1f));

        BuildMap(new Vector3(11.0f, 0f, 0.1f));

        var actors = DiscoverCanReachActors();
        Label("unknown API discovered at runtime", new Vector3(6.0f, 0.32f, -6.95f), 0.31f, new Color(0.55f, 1f, 0.75f, 1f));
        Label("typeof(MonoBehaviour).GetMethod(\"CanReach\")", new Vector3(6.0f, 0.32f, -7.62f), 0.22f, new Color(0.55f, 1f, 0.75f, 1f));
        Label("PATH FOUND", new Vector3(12.2f, 1.18f, 4.25f), 0.70f, new Color(0.25f, 1f, 0.45f, 1f));

        var checkNames = new[] { "reflect", "compute risk", "place shields", "validate" };
        for (int i = 0; i < checkNames.Length; i++)
        {
            var check = Label("✓ " + checkNames[i], new Vector3(12.5f, 0.32f, -6.85f - i * 0.62f), 0.28f, new Color(0.35f, 1f, 0.45f, 1f));
            check.SetActive(false);
            ReplChecks.Add(check);
        }

        ReplRunner = Sphere("REPL_Route_Runner", Route[0] + Vector3.up * 0.55f, 0.35f, Mats["repl"]);
        var beamGo = new GameObject("REPL_Code_Beam");
        ReplBeam = beamGo.AddComponent<LineRenderer>();
        ReplBeam.positionCount = 2;
        ReplBeam.useWorldSpace = true;
        ReplBeam.widthMultiplier = 0.08f;
        ReplBeam.material = Mats["replLine"];

        for (int i = 0; i < 8; i++)
        {
            var token = Cube("REPL_code_token_" + i, new Vector3(6.0f + i * 0.33f, 0.68f, 4.95f), new Vector3(0.22f, 0.14f, 0.26f), Mats["repl"]);
            token.SetActive(false);
            ReplTokens.Add(token);
        }
    }

    private static void BuildMap(Vector3 center)
    {
        int w = 9;
        int h = 8;
        for (int ix = 0; ix < w; ix++)
        {
            for (int iz = 0; iz < h; iz++)
            {
                var pos = center + new Vector3((ix - 4) * 0.78f, 0.04f, (iz - 3.5f) * 0.78f);
                Cube("REPL_grid_" + ix + "_" + iz, pos, new Vector3(0.72f, 0.06f, 0.72f), Mats["grid"]);
            }
        }

        AddObstacle(center + new Vector3(-1.9f, 0.38f, 1.2f), new Vector3(1.5f, 0.7f, 1.3f));
        AddObstacle(center + new Vector3(1.6f, 0.38f, 0.3f), new Vector3(1.2f, 0.7f, 2.0f));
        AddObstacle(center + new Vector3(-0.2f, 0.38f, -1.8f), new Vector3(2.2f, 0.7f, 0.9f));
        AddObstacle(center + new Vector3(3.0f, 0.38f, -1.2f), new Vector3(1.1f, 0.7f, 1.8f));

        AddShield(center + new Vector3(-1.0f, 0.55f, -0.1f));
        AddShield(center + new Vector3(0.8f, 0.55f, 1.55f));
        AddShield(center + new Vector3(2.4f, 0.55f, -2.4f));
        AddShield(center + new Vector3(-2.1f, 0.55f, 1.15f));
        AddShield(center + new Vector3(3.35f, 0.55f, 2.65f));

        AddTurret("T_A_Custom", center + new Vector3(-3.1f, 0.35f, 2.55f), 3.8f);
        AddTurret("T_B_Custom", center + new Vector3(3.2f, 0.35f, 2.45f), 4.4f);
        AddTurret("T_C_Custom", center + new Vector3(3.25f, 0.35f, -2.5f), 3.7f);
        AddTurret("T_D_Custom", center + new Vector3(-3.15f, 0.35f, -2.65f), 4.0f);
        AddTurret("T_E_Custom", center + new Vector3(0.2f, 0.35f, 3.25f), 3.6f);

        Route.Add(center + new Vector3(-4.05f, 0.45f, -3.25f));
        Route.Add(center + new Vector3(-2.8f, 0.45f, -2.1f));
        Route.Add(center + new Vector3(-1.2f, 0.45f, -0.8f));
        Route.Add(center + new Vector3(0.3f, 0.45f, -0.1f));
        Route.Add(center + new Vector3(1.6f, 0.45f, 0.8f));
        Route.Add(center + new Vector3(3.8f, 0.45f, 3.0f));
        DrawPolyline("REPL_Safe_Route", Route.ToArray(), Mats["replLine"], 0.13f);

        Sphere("Start", Route.First(), 0.27f, Mats["repl"]);
        Sphere("Goal", Route.Last(), 0.32f, Mats["goal"]);
        Label("START", Route.First() + new Vector3(-0.52f, 0.35f, -0.35f), 0.18f, Color.white);
        Label("GOAL", Route.Last() + new Vector3(-0.48f, 0.35f, 0.6f), 0.2f, Color.white);
    }

    private static void AddObstacle(Vector3 pos, Vector3 scale)
    {
        Cube("Obstacle", pos, scale, Mats["obstacle"]);
        Obstacles.Add(new Bounds(new Vector3(pos.x, 0f, pos.z), new Vector3(scale.x, 1f, scale.z)));
    }

    private static void AddShield(Vector3 pos)
    {
        Cube("REPL_Placed_Shield", pos, new Vector3(0.24f, 1.1f, 1.45f), Mats["shield"]);
        Obstacles.Add(new Bounds(new Vector3(pos.x, 0f, pos.z), new Vector3(0.9f, 1f, 1.6f)));
    }

    private static void AddTurret(string name, Vector3 pos, float range)
    {
        var go = Sphere(name, pos, 0.28f, Mats["turret"]);
        var turret = go.AddComponent<ShowdownTurret>();
        turret.Range = range;
        Turrets.Add(turret);
        DrawCircle(name + "_range", pos, range, new Color(1f, 1f, 1f, 0.38f));
        Label(name.Replace("_", " ") + " r=" + range.ToString("F1"), pos + new Vector3(-0.85f, 0.35f, 0.5f), 0.16f, Color.cyan);
    }

    private static List<MonoBehaviour> DiscoverCanReachActors()
    {
        return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
            .Where(m => m.GetType().GetMethod("CanReach", new[] { typeof(Vector3) }) != null)
            .ToList();
    }

    private static int CoverageAt(List<MonoBehaviour> actors, Vector3 point)
    {
        int score = 0;
        foreach (var actor in actors)
        {
            var method = actor.GetType().GetMethod("CanReach", new[] { typeof(Vector3) });
            if (method == null) continue;
            var canReach = (bool)method.Invoke(actor, new object[] { point });
            if (!canReach) continue;
            if (Blocked(actor.transform.position, point)) continue;
            score++;
        }
        return score;
    }

    private static bool Blocked(Vector3 a, Vector3 b)
    {
        for (int i = 1; i < 24; i++)
        {
            var p = Vector3.Lerp(a, b, i / 24f);
            var flat = new Vector3(p.x, 0f, p.z);
            if (Obstacles.Any(o => o.Contains(flat))) return true;
        }
        return false;
    }

    private static void Animate(float t, float phase)
    {
        int visibleTools = Mathf.Clamp(Mathf.CeilToInt(Mathf.InverseLerp(0.05f, 0.48f, phase) * McpTokens.Count), 0, McpTokens.Count);
        for (int i = 0; i < McpTokens.Count; i++)
        {
            McpTokens[i].SetActive(i < visibleTools);
            if (i < visibleTools)
                McpTokens[i].transform.localScale = new Vector3(1.18f, 0.16f + 0.04f * Mathf.Sin(t * 6f + i), 0.42f);
        }

        if (McpRunner != null)
        {
            var stuckPath = new[]
            {
                new Vector3(-15.2f, 0.62f, -6.5f),
                new Vector3(-13.8f, 0.62f, -5.4f),
                new Vector3(-12.4f, 0.62f, -4.6f),
                new Vector3(-11.2f, 0.62f, -3.75f)
            };
            McpRunner.transform.position = SamplePath(stuckPath, Mathf.Clamp01(phase * 2.0f));
        }
        if (McpGate != null)
        {
            var pulse = 1f + 0.08f * Mathf.Sin(t * 9f);
            McpGate.transform.localScale = new Vector3(8.1f, pulse, 0.32f);
        }

        int codeVisible = Mathf.Clamp(Mathf.CeilToInt(Mathf.InverseLerp(0.10f, 0.28f, phase) * ReplTokens.Count), 0, ReplTokens.Count);
        for (int i = 0; i < ReplTokens.Count; i++)
            ReplTokens[i].SetActive(i < codeVisible);

        if (Route.Count > 1 && ReplRunner != null)
            ReplRunner.transform.position = SamplePath(Route.ToArray(), Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0.9f, phase))) + Vector3.up * (0.18f + Mathf.Sin(t * 7f) * 0.08f);

        if (ReplBeam != null && ReplRunner != null)
        {
            ReplBeam.SetPosition(0, new Vector3(7.25f, 0.9f, 5.75f));
            ReplBeam.SetPosition(1, ReplRunner.transform.position);
            var color = new Color(0.25f, 1f, 0.58f, 0.55f + 0.25f * Mathf.Sin(t * 5f));
            ReplBeam.startColor = color;
            ReplBeam.endColor = color;
        }

        for (int i = 0; i < ReplChecks.Count; i++)
            ReplChecks[i].SetActive(phase > 0.35f + i * 0.13f);
    }

    private static Vector3 SamplePath(Vector3[] points, float normalized)
    {
        if (points.Length == 0) return Vector3.zero;
        if (points.Length == 1) return points[0];
        float scaled = Mathf.Clamp01(normalized) * (points.Length - 1);
        int a = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, points.Length - 1);
        int b = Mathf.Clamp(a + 1, 0, points.Length - 1);
        return Vector3.Lerp(points[a], points[b], scaled - a);
    }

    private static void BuildLighting()
    {
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Capability_Showdown_Camera");
        Cam = go.AddComponent<Camera>();
        Cam.orthographic = true;
        Cam.orthographicSize = 12.7f;
        Cam.backgroundColor = new Color(0.035f, 0.04f, 0.045f, 1f);
        Cam.clearFlags = CameraClearFlags.SolidColor;
        go.transform.position = new Vector3(0f, 25f, -18f);
        go.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 200f;
        Camera.SetupCurrent(Cam);
        Selection.activeGameObject = go;
    }

    private static void CreateMaterials()
    {
        AddMat("mcp", new Color(1f, 0.62f, 0.12f, 1f));
        AddMat("danger", new Color(0.9f, 0.08f, 0.05f, 1f));
        AddMat("dangerLine", new Color(1f, 0.15f, 0.08f, 1f));
        AddMat("repl", new Color(0.15f, 1f, 0.55f, 1f));
        AddMat("replLine", new Color(0.35f, 1f, 0.72f, 1f));
        AddMat("grid", new Color(0.28f, 0.35f, 0.18f, 1f));
        AddMat("obstacle", new Color(0.12f, 0.14f, 0.18f, 1f));
        AddMat("shield", new Color(0.0f, 0.72f, 0.9f, 1f));
        AddMat("turret", new Color(0.18f, 0.42f, 1f, 1f));
        AddMat("goal", new Color(1f, 0.9f, 0.18f, 1f));
        AddMat("dark", new Color(0.04f, 0.055f, 0.065f, 1f));
    }

    private static void AddMat(string key, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = key;
        mat.color = color;
        Mats[key] = mat;
    }

    private static GameObject Panel(Vector3 pos, Vector3 scale, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return Cube("Panel", pos, scale, mat);
    }

    private static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject Sphere(string name, Vector3 pos, float radius, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject Label(string text, Vector3 pos, float size, Color color)
    {
        var go = new GameObject("Label_" + text.Split('\n')[0].Replace(" ", "_"));
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(64f, 0f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = size * 0.18f;
        tm.anchor = TextAnchor.UpperLeft;
        tm.alignment = TextAlignment.Left;
        tm.color = color;
        return go;
    }

    private static void DrawPolyline(string name, Vector3[] points, Material mat, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = mat;
        lr.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++) lr.SetPosition(i, points[i]);
    }

    private static void DrawCircle(string name, Vector3 center, float radius, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.widthMultiplier = 0.035f;
        lr.material = mat;
        lr.positionCount = 96;
        for (int i = 0; i < 96; i++)
        {
            float a = i / 96f * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius));
        }
    }

    private static void CapturePng(string path, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24);
        var oldTarget = Cam.targetTexture;
        var oldActive = RenderTexture.active;
        Cam.targetTexture = rt;
        RenderTexture.active = rt;
        Cam.Render();
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Cam.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
