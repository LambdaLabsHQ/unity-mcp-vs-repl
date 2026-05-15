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

public static class VisualReproExperiment
{
    private struct Obstacle
    {
        public string Name;
        public Vector3 Center;
        public Vector3 Size;
    }

    private struct RiskSample
    {
        public Vector3 Position;
        public int Risk;
    }

    private static readonly List<Obstacle> Obstacles = new List<Obstacle>();
    private static readonly List<RiskSample> PathSamples = new List<RiskSample>();
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    public static IEnumerator Run(int seed)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var repoRoot = Directory.GetParent(projectRoot).FullName;
        var outDir = Path.Combine(repoRoot, "results", "visual_repro");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory("Assets/VisualRepro");
        Directory.CreateDirectory("Assets/Editor");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PathSamples.Clear();
        Obstacles.Clear();
        Mats.Clear();
        CreateMaterials();

        BuildWorld(seed);
        var actors = DiscoverCoverageActors();
        BuildHeatmap(actors);
        BuildPathAndRisk(actors);
        BuildRiskBars();
        BuildComparisonPanels(actors.Count);
        BuildCamera();

        var scenePath = "Assets/VisualRepro/TurretCoverageLab.unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

        var metricsPath = Path.Combine(outDir, "metrics.json");
        WriteMetrics(metricsPath, seed, actors.Count);

        yield return null;
        yield return new WaitForSeconds(0.25f);

        var pngPath = Path.Combine(outDir, "turret_coverage_lab.png");
        CapturePng(pngPath, 1800, 1100);
        WriteCrystallizedProbe();

        yield return "VISUAL_REPRO_DONE png=" + pngPath + " metrics=" + metricsPath + " scene=" + scenePath;
    }

    private static void CreateMaterials()
    {
        Mat("floor", new Color(0.13f, 0.15f, 0.16f, 1f));
        Mat("grid0", new Color(0.08f, 0.22f, 0.34f, 1f));
        Mat("grid1", new Color(0.72f, 0.62f, 0.15f, 1f));
        Mat("grid2", new Color(0.88f, 0.38f, 0.12f, 1f));
        Mat("grid3", new Color(0.88f, 0.06f, 0.08f, 1f));
        Mat("obstacle", new Color(0.21f, 0.21f, 0.24f, 1f));
        Mat("turret", new Color(0.16f, 0.70f, 1.00f, 1f));
        Mat("enemy", new Color(0.95f, 0.95f, 0.95f, 1f));
        Mat("safe", new Color(0.05f, 0.80f, 0.36f, 1f));
        Mat("warn", new Color(1.00f, 0.72f, 0.08f, 1f));
        Mat("danger", new Color(1.00f, 0.08f, 0.12f, 1f));
        Mat("panel", new Color(0.02f, 0.025f, 0.03f, 1f));
        Mat("line", new Color(0.95f, 0.95f, 0.95f, 1f));
    }

    private static Material Mat(string name, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        Mats[name] = mat;
        return mat;
    }

    private static void BuildWorld(int seed)
    {
        Cube("Floor", new Vector3(0, -0.08f, 0), new Vector3(34f, 0.12f, 24f), Mats["floor"]);

        AddObstacle("Block_A", -7.5f, -4.5f, 2.6f, 6.2f, 1.3f);
        AddObstacle("Block_B", -1.8f, -1.3f, 4.0f, 2.0f, 1.9f);
        AddObstacle("Block_C", 4.2f, -5.2f, 2.8f, 4.6f, 1.5f);
        AddObstacle("Block_D", 8.6f, 1.3f, 2.3f, 6.0f, 2.2f);
        AddObstacle("Block_E", -8.4f, 5.4f, 4.8f, 2.0f, 1.4f);
        AddObstacle("Block_F", 0.2f, 5.8f, 2.2f, 4.2f, 2.0f);
        AddObstacle("Block_G", 5.2f, 5.2f, 3.6f, 1.8f, 1.6f);

        AddTurret("T_A_Laser", new Vector3(-13f, 0.25f, -8f), 8.2f);
        AddTurret("T_B_Grenade", new Vector3(-3.2f, 0.25f, -8.8f), 6.4f);
        AddTurret("T_C_Rail", new Vector3(12.2f, 0.25f, -4.8f), 9.4f);
        AddTurret("T_D_Beam", new Vector3(-12.8f, 0.25f, 7.4f), 7.8f);
        AddTurret("T_E_Sniper", new Vector3(11.8f, 0.25f, 8.2f), 8.8f);

        var start = Sphere("Enemy_Start", new Vector3(-15.2f, 0.55f, -9.7f), 0.35f, Mats["enemy"]);
        start.AddComponent<Health>().hitPoints = 100;
    }

    private static void AddObstacle(string name, float x, float z, float sx, float sz, float h)
    {
        Obstacles.Add(new Obstacle
        {
            Name = name,
            Center = new Vector3(x, h * 0.5f, z),
            Size = new Vector3(sx, h, sz)
        });
        Cube(name, new Vector3(x, h * 0.5f, z), new Vector3(sx, h, sz), Mats["obstacle"]);
    }

    private static void AddTurret(string name, Vector3 pos, float range)
    {
        var baseGo = Cylinder(name, pos, new Vector3(0.55f, 0.18f, 0.55f), Mats["turret"]);
        var barrel = Cube(name + "_Barrel", pos + new Vector3(0, 0.28f, 0.55f), new Vector3(0.18f, 0.18f, 1.0f), Mats["turret"]);
        barrel.transform.SetParent(baseGo.transform);
        var turret = baseGo.AddComponent<Turret>();
        turret.range = range;
        DrawCircle(name + "_Range", new Vector3(pos.x, 0.06f, pos.z), range, Color.cyan);
        Label(name + " r=" + range.ToString("F1"), pos + new Vector3(-1.4f, 0.9f, 0), 0.28f, Color.cyan);
    }

    private static List<MonoBehaviour> DiscoverCoverageActors()
    {
        var result = new List<MonoBehaviour>();
        var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in behaviours)
        {
            if (mb == null) continue;
            var method = mb.GetType().GetMethod("CanReach", new[] { typeof(Vector3) });
            if (method != null && method.ReturnType == typeof(bool))
                result.Add(mb);
        }
        return result.OrderBy(x => x.name).ToList();
    }

    private static void BuildHeatmap(List<MonoBehaviour> actors)
    {
        int nx = 32;
        int nz = 22;
        float minX = -16f;
        float minZ = -10.5f;
        float step = 1f;

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                var p = new Vector3(minX + ix * step + 0.5f, 0.02f, minZ + iz * step + 0.5f);
                int risk = CoverageAt(actors, p);
                var mat = Mats["grid" + Mathf.Min(3, risk)];
                Cube("Heat_" + ix + "_" + iz, p, new Vector3(0.92f, 0.035f, 0.92f), mat);
            }
        }
    }

    private static void BuildPathAndRisk(List<MonoBehaviour> actors)
    {
        Vector3[] waypoints = new[]
        {
            new Vector3(-15.2f, 0.22f, -9.7f),
            new Vector3(-11.5f, 0.22f, -7.2f),
            new Vector3(-7.0f, 0.22f, -8.7f),
            new Vector3(-2.0f, 0.22f, -4.7f),
            new Vector3(2.3f, 0.22f, -6.8f),
            new Vector3(6.8f, 0.22f, -2.5f),
            new Vector3(3.0f, 0.22f, 1.3f),
            new Vector3(-1.2f, 0.22f, 2.7f),
            new Vector3(-5.8f, 0.22f, 5.0f),
            new Vector3(-1.0f, 0.22f, 8.7f),
            new Vector3(5.8f, 0.22f, 7.7f),
            new Vector3(14.8f, 0.22f, 9.6f),
        };

        var line = new GameObject("Enemy_Path_Risk_Line");
        var lr = line.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.18f;
        lr.positionCount = waypoints.Length;
        lr.material = Mats["line"];
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        lr.SetPositions(waypoints);

        for (int i = 0; i < waypoints.Length; i++)
        {
            int risk = CoverageAt(actors, waypoints[i]);
            PathSamples.Add(new RiskSample { Position = waypoints[i], Risk = risk });
            var mat = risk == 0 ? Mats["safe"] : (risk == 1 ? Mats["warn"] : Mats["danger"]);
            Sphere("Path_Waypoint_" + i + "_risk_" + risk, waypoints[i] + Vector3.up * 0.28f, 0.25f + risk * 0.08f, mat);
        }

        var worst = PathSamples.OrderByDescending(s => s.Risk).First();
        Cylinder("Worst_Risk_Beacon", worst.Position + Vector3.up * 1.5f, new Vector3(0.18f, 1.5f, 0.18f), Mats["danger"]);
        Label("worst risk=" + worst.Risk, worst.Position + new Vector3(-1.2f, 2.8f, 0), 0.36f, Color.red);
    }

    private static void BuildRiskBars()
    {
        float baseX = 18.6f;
        float baseZ = -9.5f;
        Label("Path risk bars", new Vector3(baseX - 1.1f, 3.2f, baseZ), 0.32f, Color.white);
        for (int i = 0; i < PathSamples.Count; i++)
        {
            int risk = PathSamples[i].Risk;
            var mat = risk == 0 ? Mats["safe"] : (risk == 1 ? Mats["warn"] : Mats["danger"]);
            Cube("RiskBar_" + i, new Vector3(baseX + i * 0.42f, 0.18f + risk * 0.28f, baseZ), new Vector3(0.28f, 0.25f + risk * 0.55f, 0.7f), mat);
        }
    }

    private static void BuildComparisonPanels(int actorCount)
    {
        Cube("Panel_REPL_Back", new Vector3(-10.5f, 5.2f, -13.4f), new Vector3(11.8f, 6.2f, 0.25f), Mats["panel"]);
        Cube("Panel_MCP_Back", new Vector3(9.8f, 5.2f, -13.4f), new Vector3(13.4f, 6.2f, 0.25f), Mats["panel"]);

        Label("REPL ABSOLUTE ADVANTAGE", new Vector3(-15.7f, 8.3f, -13.65f), 0.48f, Color.green);
        Label(
            "one stable interface: eval C#\n" +
            "discovers project API by reflection\n" +
            "found CanReach(Vector3) actors: " + actorCount + "\n" +
            "LINQ + custom types + geometry in one program\n" +
            "coroutine controls wait/render/screenshot\n" +
            "crystallizes CoverageProbe.cs as project code",
            new Vector3(-15.7f, 7.35f, -13.65f),
            0.30f,
            new Color(0.65f, 1f, 0.72f, 1f));

        Label("MCP TOOL TABLE PATH", new Vector3(3.55f, 8.3f, -13.65f), 0.48f, new Color(1f, 0.62f, 0.12f, 1f));
        Label(
            "Coplay README exposes 42 tool names\n" +
            "tools reference ~= 15k coarse tokens\n" +
            "workflow reference ~= 18.6k coarse tokens\n" +
            "needs scene/object/material/physics/camera/UI tools\n" +
            "long-tail custom Turret API has no endpoint\n" +
            "if it falls back to execute_code, it became REPL",
            new Vector3(3.55f, 7.35f, -13.65f),
            0.30f,
            new Color(1f, 0.82f, 0.55f, 1f));

        Label("Turret Coverage Lab: deterministic seed, custom project API, line-of-sight, path risk, visual output", new Vector3(-13.8f, 0.25f, 12.2f), 0.34f, Color.white);
    }

    private static void BuildCamera()
    {
        var camGo = new GameObject("Visual_Repro_Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 16.2f;
        cam.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.transform.position = new Vector3(0f, 24f, -20f);
        camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        Camera.SetupCurrent(cam);
        Selection.activeGameObject = camGo;
    }

    private static int CoverageAt(List<MonoBehaviour> actors, Vector3 point)
    {
        int score = 0;
        foreach (var actor in actors)
        {
            var method = actor.GetType().GetMethod("CanReach", new[] { typeof(Vector3) });
            if (method == null) continue;
            bool canReach = false;
            try { canReach = (bool)method.Invoke(actor, new object[] { point }); }
            catch { canReach = false; }
            if (!canReach) continue;
            if (Blocked2D(actor.transform.position, point)) continue;
            score++;
        }
        return score;
    }

    private static bool Blocked2D(Vector3 a, Vector3 b)
    {
        for (int step = 1; step < 20; step++)
        {
            float t = step / 20f;
            var p = Vector3.Lerp(a, b, t);
            for (int i = 0; i < Obstacles.Count; i++)
            {
                var o = Obstacles[i];
                if (Mathf.Abs(p.x - o.Center.x) <= o.Size.x * 0.5f &&
                    Mathf.Abs(p.z - o.Center.z) <= o.Size.z * 0.5f)
                    return true;
            }
        }
        return false;
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

    private static GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void DrawCircle(string name, Vector3 center, float radius, Color color)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.widthMultiplier = 0.05f;
        lr.material = Mats["line"];
        lr.startColor = color;
        lr.endColor = color;
        int count = 96;
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float a = (float)i / count * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
        }
    }

    private static void Label(string text, Vector3 pos, float size, Color color)
    {
        var go = new GameObject("Label_" + text.Split('\n')[0].Replace(" ", "_"));
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = size * 0.18f;
        tm.anchor = TextAnchor.UpperLeft;
        tm.alignment = TextAlignment.Left;
        tm.color = color;
    }

    private static void CapturePng(string path, int width, int height)
    {
        var cam = GameObject.Find("Visual_Repro_Camera").GetComponent<Camera>();
        var rt = new RenderTexture(width, height, 24);
        var oldTarget = cam.targetTexture;
        var oldActive = RenderTexture.active;
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = oldTarget;
        RenderTexture.active = oldActive;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
    }

    private static void WriteMetrics(string path, int seed, int actorCount)
    {
        int zero = PathSamples.Count(x => x.Risk == 0);
        int max = PathSamples.Count == 0 ? 0 : PathSamples.Max(x => x.Risk);
        double avg = PathSamples.Count == 0 ? 0 : PathSamples.Average(x => x.Risk);
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"experiment\": \"Turret Coverage Lab\",");
        sb.AppendLine("  \"seed\": " + seed + ",");
        sb.AppendLine("  \"unity_version\": \"" + Application.unityVersion + "\",");
        sb.AppendLine("  \"repl_interface_count\": 1,");
        sb.AppendLine("  \"coverage_actor_count_discovered_by_reflection\": " + actorCount + ",");
        sb.AppendLine("  \"obstacle_count\": " + Obstacles.Count + ",");
        sb.AppendLine("  \"path_sample_count\": " + PathSamples.Count + ",");
        sb.AppendLine("  \"path_zero_risk_samples\": " + zero + ",");
        sb.AppendLine("  \"path_max_risk\": " + max + ",");
        sb.AppendLine("  \"path_average_risk\": " + avg.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ",");
        sb.AppendLine("  \"coplay_readme_tool_count_observed\": 42,");
        sb.AppendLine("  \"coplay_tools_reference_approx_tokens_observed\": 15096,");
        sb.AppendLine("  \"claim\": \"A general MCP solution either enumerates many tools/schemas or falls back to execute_code, which is REPL inside MCP.\"");
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteCrystallizedProbe()
    {
        File.WriteAllText("Assets/Editor/CoverageProbe.cs",
@"using System.Linq;
using UnityEngine;

public static class CoverageProbe
{
    public static string Dump(Vector3 point)
    {
        return string.Join(""\n"", Object.FindObjectsOfType<MonoBehaviour>()
            .Where(m => m.GetType().GetMethod(""CanReach"", new[] { typeof(Vector3) }) != null)
            .OrderBy(m => m.name)
            .Select(m => m.name + "" => "" + m.GetType().GetMethod(""CanReach"", new[] { typeof(Vector3) }).Invoke(m, new object[] { point })));
    }
}
");
    }
}
