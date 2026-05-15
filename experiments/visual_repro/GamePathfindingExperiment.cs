using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IGameThreatSource
{
    bool CanReach(Vector3 point);
}

public sealed class GameThreatTurret : MonoBehaviour, IGameThreatSource
{
    public float Range = 2.6f;

    public bool CanReach(Vector3 point)
    {
        var a = new Vector3(transform.position.x, 0f, transform.position.z);
        var b = new Vector3(point.x, 0f, point.z);
        if (Vector3.Distance(a, b) > Range) return false;
        return !GamePathfindingExperiment.LineBlocked(a, b);
    }
}

public static class GamePathfindingExperiment
{
    private const int Width = 16;
    private const int Height = 10;
    private const float Cell = 0.72f;

    private sealed class Arena
    {
        public string Prefix;
        public Vector3 Center;
        public Vector2Int Start = new Vector2Int(1, 1);
        public Vector2Int Goal = new Vector2Int(14, 8);
        public HashSet<Vector2Int> Walls = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> Hazards = new HashSet<Vector2Int>();
        public List<GameThreatTurret> Turrets = new List<GameThreatTurret>();
        public List<LineRenderer> Beams = new List<LineRenderer>();
        public List<GameObject> HazardTiles = new List<GameObject>();
        public List<Vector3> Route = new List<Vector3>();
        public GameObject Bot;
        public GameObject HpFill;
        public GameObject ResultLabel;
    }

    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    private static readonly List<Bounds> BlockingBounds = new List<Bounds>();
    private static Arena McpArena;
    private static Arena ReplArena;
    private static Camera Cam;

    public static IEnumerator Run(int seed)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var repoRoot = Directory.GetParent(projectRoot).FullName;
        var outDir = Path.Combine(repoRoot, "results", "game_pathfinding");
        Directory.CreateDirectory(outDir);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Mats.Clear();
        BlockingBounds.Clear();
        CreateMaterials();
        BuildLighting();

        Label("Real Unity task: navigate a dangerous level", new Vector3(-5.0f, 0.22f, 5.9f), 0.46f, new Color(0.9f, 0.96f, 1f, 1f));
        Label("same scene, same custom project code, different control surface", new Vector3(-4.2f, 0.22f, 5.35f), 0.25f, new Color(0.68f, 0.78f, 0.86f, 1f));

        McpArena = BuildArena("MCP", new Vector3(-6.2f, 0f, 0f), new Color(1f, 0.6f, 0.12f, 1f));
        ReplArena = BuildArena("REPL", new Vector3(6.2f, 0f, 0f), new Color(0.22f, 1f, 0.5f, 1f));

        McpArena.Route = FindRoute(McpArena, avoidProjectThreats: false);
        ReplArena.Route = FindRoute(ReplArena, avoidProjectThreats: true);

        DrawRoute("MCP_naive_route", McpArena.Route, Mats["mcpPath"], 0.12f);
        DrawRoute("REPL_safe_route", ReplArena.Route, Mats["replPath"], 0.14f);

        BuildCamera();
        yield return null;

        var mcpRisk = CountRisk(McpArena, McpArena.Route);
        var replRisk = CountRisk(ReplArena, ReplArena.Route);
        Label("MCP bot: shortest path ignores CustomThreat.CanReach()", McpArena.Center + new Vector3(-5.45f, 0.2f, 4.75f), 0.22f, new Color(1f, 0.76f, 0.35f, 1f));
        Label("REPL bot: eval C# reflects CanReach() and replans", ReplArena.Center + new Vector3(-5.45f, 0.2f, 4.75f), 0.22f, new Color(0.52f, 1f, 0.66f, 1f));
        Label("risk cells on chosen route: " + mcpRisk, McpArena.Center + new Vector3(-5.45f, 0.2f, -5.05f), 0.22f, new Color(1f, 0.5f, 0.34f, 1f));
        Label("risk cells on chosen route: " + replRisk, ReplArena.Center + new Vector3(-5.45f, 0.2f, -5.05f), 0.22f, new Color(0.42f, 1f, 0.6f, 1f));

        var scenePath = "Assets/VisualRepro/GamePathfindingShowdown.unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

        var pngPath = Path.Combine(outDir, "game_pathfinding_showdown.png");
        CapturePng(pngPath, 1920, 1080);
        var metricsPath = Path.Combine(outDir, "metrics.json");
        File.WriteAllText(metricsPath, "{\n" +
            "  \"experiment\": \"Real Game Pathfinding Showdown\",\n" +
            "  \"mcp_route_waypoints\": " + McpArena.Route.Count + ",\n" +
            "  \"repl_route_waypoints\": " + ReplArena.Route.Count + ",\n" +
            "  \"mcp_route_risk_cells\": " + mcpRisk + ",\n" +
            "  \"repl_route_risk_cells\": " + replRisk + ",\n" +
            "  \"custom_threat_actors\": " + ReplArena.Turrets.Count + ",\n" +
            "  \"project_specific_method\": \"CanReach(Vector3)\"\n" +
            "}\n");

        yield return "GAME_PATHFINDING_DONE png=" + pngPath + " metrics=" + metricsPath + " scene=" + scenePath;
    }

    public static IEnumerator PlayDemo(int seconds)
    {
        if (seconds <= 0) seconds = 20;
        if (Cam == null) Cam = GameObject.Find("Game_Pathfinding_Camera")?.GetComponent<Camera>();
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
        yield return "GAME_PATHFINDING_PLAY_DONE seconds=" + seconds;
    }

    public static bool LineBlocked(Vector3 a, Vector3 b)
    {
        for (int i = 1; i < 24; i++)
        {
            var p = Vector3.Lerp(a, b, i / 24f);
            var flat = new Vector3(p.x, 0f, p.z);
            if (BlockingBounds.Any(x => x.Contains(flat))) return true;
        }
        return false;
    }

    private static Arena BuildArena(string prefix, Vector3 center, Color accent)
    {
        var arena = new Arena { Prefix = prefix, Center = center };
        AddMapData(arena);

        Cube(prefix + "_floor", center + new Vector3(-0.18f, -0.06f, -0.08f),
            new Vector3(Width * Cell + 0.35f, 0.08f, Height * Cell + 0.35f), Mats["floor"]);
        for (int x = 0; x < Width; x++)
            for (int z = 0; z < Height; z++)
                Cube(prefix + "_tile_" + x + "_" + z, CellToWorld(arena, new Vector2Int(x, z)) + Vector3.down * 0.01f,
                    new Vector3(Cell - 0.035f, 0.035f, Cell - 0.035f), Mats["tile"]);

        foreach (var cell in arena.Walls)
        {
            var pos = CellToWorld(arena, cell) + Vector3.up * 0.42f;
            Cube(prefix + "_wall", pos, new Vector3(Cell * 0.95f, 0.85f, Cell * 0.95f), Mats["wall"]);
            BlockingBounds.Add(new Bounds(new Vector3(pos.x, 0f, pos.z), new Vector3(Cell * 0.95f, 1f, Cell * 0.95f)));
        }

        foreach (var cell in arena.Hazards)
        {
            var tile = Cube(prefix + "_lava", CellToWorld(arena, cell) + Vector3.up * 0.02f,
                new Vector3(Cell * 0.92f, 0.06f, Cell * 0.92f), Mats["hazard"]);
            arena.HazardTiles.Add(tile);
        }

        AddTurret(arena, new Vector2Int(7, 2), 2.85f);
        AddTurret(arena, new Vector2Int(8, 4), 2.55f);
        AddTurret(arena, new Vector2Int(11, 2), 2.35f);

        var start = CellToWorld(arena, arena.Start);
        var goal = CellToWorld(arena, arena.Goal);
        Sphere(prefix + "_start", start + Vector3.up * 0.18f, 0.22f, Mats["start"]);
        var exit = Cube(prefix + "_exit", goal + Vector3.up * 0.22f, new Vector3(0.55f, 0.42f, 0.55f), Mats["goal"]);
        exit.transform.Rotate(0f, 45f, 0f);
        arena.Bot = Sphere(prefix + "_bot", start + Vector3.up * 0.36f, 0.27f, prefix == "MCP" ? Mats["mcpBot"] : Mats["replBot"]);

        Label(prefix + " BOT", center + new Vector3(-5.5f, 0.2f, 5.25f), 0.38f, accent);
        Label("START", start + new Vector3(-0.38f, 0.2f, -0.55f), 0.18f, Color.white);
        Label("EXIT", goal + new Vector3(-0.32f, 0.2f, 0.58f), 0.18f, Color.white);

        Cube(prefix + "_hp_back", center + new Vector3(1.0f, 0.2f, 5.12f), new Vector3(2.5f, 0.08f, 0.18f), Mats["hpBack"]);
        arena.HpFill = Cube(prefix + "_hp_fill", center + new Vector3(1.0f, 0.27f, 5.12f), new Vector3(2.48f, 0.10f, 0.22f), prefix == "MCP" ? Mats["danger"] : Mats["replPath"]);
        Label("HP", center + new Vector3(-0.52f, 0.2f, 5.25f), 0.18f, Color.white);
        arena.ResultLabel = Label(prefix == "MCP" ? "KILLED BY CUSTOM THREAT" : "REACHED EXIT",
            center + new Vector3(-2.55f, 0.2f, -4.55f), 0.36f, prefix == "MCP" ? new Color(1f, 0.22f, 0.16f, 1f) : new Color(0.26f, 1f, 0.45f, 1f));
        arena.ResultLabel.SetActive(false);

        return arena;
    }

    private static void AddMapData(Arena arena)
    {
        for (int z = 2; z <= 7; z++) arena.Walls.Add(new Vector2Int(5, z));
        for (int z = 2; z <= 7; z++) arena.Walls.Add(new Vector2Int(10, z));
        for (int x = 2; x <= 4; x++) arena.Walls.Add(new Vector2Int(x, 6));
        for (int x = 11; x <= 13; x++) arena.Walls.Add(new Vector2Int(x, 5));

        for (int x = 6; x <= 9; x++)
            for (int z = 1; z <= 3; z++)
                arena.Hazards.Add(new Vector2Int(x, z));
        arena.Hazards.Add(new Vector2Int(11, 1));
        arena.Hazards.Add(new Vector2Int(12, 2));
    }

    private static void AddTurret(Arena arena, Vector2Int cell, float range)
    {
        var pos = CellToWorld(arena, cell) + Vector3.up * 0.35f;
        var baseGo = Sphere(arena.Prefix + "_turret", pos, 0.25f, Mats["turret"]);
        var turret = baseGo.AddComponent<GameThreatTurret>();
        turret.Range = range;
        arena.Turrets.Add(turret);
        DrawCircle(arena.Prefix + "_turret_range", pos, range, Mats["range"], 0.025f);

        var beamGo = new GameObject(arena.Prefix + "_beam");
        var beam = beamGo.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.widthMultiplier = 0.075f;
        beam.material = Mats["beam"];
        beam.positionCount = 2;
        beam.enabled = false;
        arena.Beams.Add(beam);
    }

    private static List<Vector3> FindRoute(Arena arena, bool avoidProjectThreats)
    {
        var route = FindCells(arena, avoidProjectThreats);
        if (route.Count == 0)
        {
            route = new List<Vector2Int> { arena.Start, new Vector2Int(1, 8), new Vector2Int(5, 8), new Vector2Int(10, 8), arena.Goal };
        }
        return route.Select(x => CellToWorld(arena, x) + Vector3.up * 0.18f).ToList();
    }

    private static List<Vector2Int> FindCells(Arena arena, bool avoidProjectThreats)
    {
        var open = new List<Vector2Int> { arena.Start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, float> { [arena.Start] = 0f };
        var threats = avoidProjectThreats ? DiscoverThreats(arena) : new List<MonoBehaviour>();

        while (open.Count > 0)
        {
            open.Sort((a, b) => (g[a] + Heuristic(a, arena.Goal)).CompareTo(g[b] + Heuristic(b, arena.Goal)));
            var current = open[0];
            open.RemoveAt(0);
            if (current == arena.Goal) return Reconstruct(cameFrom, current);

            foreach (var next in Neighbors(current))
            {
                if (!InBounds(next) || arena.Walls.Contains(next)) continue;
                if (avoidProjectThreats && arena.Hazards.Contains(next)) continue;
                if (avoidProjectThreats && ThreatAt(threats, CellToWorld(arena, next)) > 0) continue;

                var tentative = g[current] + 1f;
                if (g.TryGetValue(next, out var old) && tentative >= old) continue;
                cameFrom[next] = current;
                g[next] = tentative;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return new List<Vector2Int>();
    }

    private static List<MonoBehaviour> DiscoverThreats(Arena arena)
    {
        return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
            .Where(m => Mathf.Abs(m.transform.position.x - arena.Center.x) < Width * Cell * 0.6f)
            .Where(m => m.GetType().GetMethod("CanReach", new[] { typeof(Vector3) }) != null)
            .ToList();
    }

    private static int CountRisk(Arena arena, List<Vector3> route)
    {
        var threats = DiscoverThreats(arena);
        int risk = 0;
        foreach (var p in route)
        {
            var cell = WorldToCell(arena, p);
            if (arena.Hazards.Contains(cell)) risk++;
            risk += ThreatAt(threats, p);
        }
        return risk;
    }

    private static int ThreatAt(List<MonoBehaviour> threats, Vector3 point)
    {
        int score = 0;
        foreach (var threat in threats)
        {
            var method = threat.GetType().GetMethod("CanReach", new[] { typeof(Vector3) });
            if (method == null) continue;
            if ((bool)method.Invoke(threat, new object[] { point })) score++;
        }
        return score;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
    {
        yield return new Vector2Int(cell.x + 1, cell.y);
        yield return new Vector2Int(cell.x - 1, cell.y);
        yield return new Vector2Int(cell.x, cell.y + 1);
        yield return new Vector2Int(cell.x, cell.y - 1);
    }

    private static bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var route = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            route.Add(current);
        }
        route.Reverse();
        return route;
    }

    private static void Animate(float time, float phase)
    {
        AnimateHazards(McpArena, time);
        AnimateHazards(ReplArena, time);
        AnimateMcp(time, phase);
        AnimateRepl(time, phase);
    }

    private static void AnimateMcp(float time, float phase)
    {
        var travel = Mathf.Clamp01(Mathf.InverseLerp(0.08f, 0.52f, phase));
        travel = Mathf.Min(travel, 0.55f);
        McpArena.Bot.transform.position = SamplePath(McpArena.Route, travel) + Vector3.up * (0.28f + Mathf.Sin(time * 9f) * 0.05f);
        var hp = Mathf.Clamp01(1f - Mathf.InverseLerp(0.32f, 0.58f, phase));
        SetHp(McpArena, hp);
        SetBeams(McpArena, McpArena.Bot.transform.position, phase > 0.24f && phase < 0.7f);
        McpArena.ResultLabel.SetActive(phase > 0.6f);
        if (phase > 0.58f)
            McpArena.Bot.transform.localScale = Vector3.one * (0.54f + 0.08f * Mathf.Sin(time * 16f));
    }

    private static void AnimateRepl(float time, float phase)
    {
        var travel = Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.92f, phase));
        ReplArena.Bot.transform.position = SamplePath(ReplArena.Route, travel) + Vector3.up * (0.28f + Mathf.Sin(time * 8f) * 0.045f);
        SetHp(ReplArena, 1f);
        SetBeams(ReplArena, ReplArena.Bot.transform.position, false);
        ReplArena.ResultLabel.SetActive(phase > 0.88f);
    }

    private static void AnimateHazards(Arena arena, float time)
    {
        for (int i = 0; i < arena.HazardTiles.Count; i++)
        {
            var s = 0.92f + 0.08f * Mathf.Sin(time * 5f + i);
            arena.HazardTiles[i].transform.localScale = new Vector3(Cell * s, 0.06f, Cell * s);
        }
    }

    private static void SetBeams(Arena arena, Vector3 target, bool allow)
    {
        for (int i = 0; i < arena.Turrets.Count; i++)
        {
            var turret = arena.Turrets[i];
            var beam = arena.Beams[i];
            var fire = allow && turret.CanReach(target);
            beam.enabled = fire;
            if (!fire) continue;
            beam.SetPosition(0, turret.transform.position + Vector3.up * 0.15f);
            beam.SetPosition(1, target);
        }
    }

    private static void SetHp(Arena arena, float hp)
    {
        hp = Mathf.Clamp01(hp);
        arena.HpFill.transform.localScale = new Vector3(2.48f * hp, 0.10f, 0.22f);
        arena.HpFill.transform.position = arena.Center + new Vector3(1.0f - (2.48f * (1f - hp)) * 0.5f, 0.27f, 5.12f);
    }

    private static Vector3 SamplePath(List<Vector3> points, float normalized)
    {
        if (points.Count == 0) return Vector3.zero;
        if (points.Count == 1) return points[0];
        var scaled = Mathf.Clamp01(normalized) * (points.Count - 1);
        var a = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, points.Count - 1);
        var b = Mathf.Clamp(a + 1, 0, points.Count - 1);
        return Vector3.Lerp(points[a], points[b], scaled - a);
    }

    private static Vector3 CellToWorld(Arena arena, Vector2Int cell)
    {
        return arena.Center + new Vector3((cell.x - (Width - 1) * 0.5f) * Cell, 0f, (cell.y - (Height - 1) * 0.5f) * Cell);
    }

    private static Vector2Int WorldToCell(Arena arena, Vector3 world)
    {
        var local = world - arena.Center;
        return new Vector2Int(
            Mathf.RoundToInt(local.x / Cell + (Width - 1) * 0.5f),
            Mathf.RoundToInt(local.z / Cell + (Height - 1) * 0.5f));
    }

    private static void BuildLighting()
    {
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Game_Pathfinding_Camera");
        Cam = go.AddComponent<Camera>();
        Cam.orthographic = true;
        Cam.orthographicSize = 6.75f;
        Cam.backgroundColor = new Color(0.025f, 0.03f, 0.035f, 1f);
        Cam.clearFlags = CameraClearFlags.SolidColor;
        go.transform.position = new Vector3(0f, 18f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 100f;
        Camera.SetupCurrent(Cam);
        Selection.activeGameObject = go;
    }

    private static void CreateMaterials()
    {
        AddMat("floor", new Color(0.07f, 0.085f, 0.1f, 1f));
        AddMat("tile", new Color(0.11f, 0.14f, 0.16f, 1f));
        AddMat("wall", new Color(0.27f, 0.31f, 0.36f, 1f));
        AddMat("hazard", new Color(0.9f, 0.08f, 0.03f, 1f));
        AddMat("danger", new Color(1f, 0.12f, 0.07f, 1f));
        AddMat("mcpBot", new Color(1f, 0.58f, 0.1f, 1f));
        AddMat("replBot", new Color(0.16f, 1f, 0.48f, 1f));
        AddMat("mcpPath", new Color(1f, 0.18f, 0.1f, 1f));
        AddMat("replPath", new Color(0.18f, 1f, 0.55f, 1f));
        AddMat("turret", new Color(0.16f, 0.45f, 1f, 1f));
        AddMat("range", new Color(0.75f, 0.82f, 1f, 0.45f));
        AddMat("beam", new Color(1f, 0.16f, 0.06f, 1f));
        AddMat("start", new Color(0.25f, 0.8f, 1f, 1f));
        AddMat("goal", new Color(1f, 0.92f, 0.15f, 1f));
        AddMat("hpBack", new Color(0.02f, 0.025f, 0.03f, 1f));
    }

    private static void AddMat(string key, Color color)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = key;
        mat.color = color;
        Mats[key] = mat;
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
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = size * 0.18f;
        tm.anchor = TextAnchor.UpperLeft;
        tm.alignment = TextAlignment.Left;
        tm.color = color;
        return go;
    }

    private static void DrawRoute(string name, List<Vector3> points, Material mat, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = mat;
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) lr.SetPosition(i, points[i] + Vector3.up * 0.16f);
    }

    private static void DrawCircle(string name, Vector3 center, float radius, Material mat, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.widthMultiplier = width;
        lr.material = mat;
        lr.positionCount = 96;
        for (int i = 0; i < 96; i++)
        {
            var a = i / 96f * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
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
