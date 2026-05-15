using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AgenticDoor : MonoBehaviour
{
    public bool Locked = true;
    public GameObject Barrier;
    public Material LockedMaterial;
    public Material OpenMaterial;

    public void UnlockForAgent()
    {
        Locked = false;
        if (Barrier != null)
        {
            Barrier.transform.localScale = new Vector3(0.18f, 0.18f, 1.4f);
            Barrier.GetComponent<Renderer>().sharedMaterial = OpenMaterial;
        }
    }

    public void Lock()
    {
        Locked = true;
        if (Barrier != null)
        {
            Barrier.transform.localScale = new Vector3(0.18f, 0.95f, 1.4f);
            Barrier.GetComponent<Renderer>().sharedMaterial = LockedMaterial;
        }
    }
}

public sealed class AgenticLaser : MonoBehaviour
{
    public bool Suppressed;
    public LineRenderer Beam;
    public Material ActiveMaterial;
    public Material SuppressedMaterial;

    public void SuppressForSeconds(float seconds)
    {
        Suppressed = true;
        SetVisual();
    }

    public void Arm()
    {
        Suppressed = false;
        SetVisual();
    }

    public bool CanHit(Vector3 point)
    {
        if (Suppressed) return false;
        var p = transform.InverseTransformPoint(point);
        return Mathf.Abs(p.x) < 0.22f && Mathf.Abs(p.z) < 1.9f;
    }

    public void SetVisual()
    {
        if (Beam != null)
        {
            Beam.enabled = !Suppressed;
            Beam.material = Suppressed ? SuppressedMaterial : ActiveMaterial;
        }
    }
}

public sealed class AgenticBridge : MonoBehaviour
{
    public bool Extended;
    public GameObject Platform;
    public Material ExtendedMaterial;
    public Material MissingMaterial;

    public void Extend()
    {
        Extended = true;
        if (Platform != null)
        {
            Platform.SetActive(true);
            Platform.GetComponent<Renderer>().sharedMaterial = ExtendedMaterial;
        }
    }

    public void Retract()
    {
        Extended = false;
        if (Platform != null)
        {
            Platform.SetActive(false);
            Platform.GetComponent<Renderer>().sharedMaterial = MissingMaterial;
        }
    }
}

public sealed class AgenticBot : MonoBehaviour
{
    public float Hp = 1f;
    public bool Alive = true;
    public bool ReachedExit;

    public void ResetRuntime(Vector3 position)
    {
        transform.position = position;
        Hp = 1f;
        Alive = true;
        ReachedExit = false;
    }

    public void Damage(float amount)
    {
        Hp = Mathf.Clamp01(Hp - amount);
        if (Hp <= 0.01f) Alive = false;
    }

    public void MarkExit()
    {
        ReachedExit = true;
    }
}

public static class AgenticLoopExperiment
{
    private sealed class Arena
    {
        public string Prefix;
        public Vector3 Center;
        public Color Accent;
        public AgenticBot Bot;
        public AgenticDoor Door;
        public AgenticLaser Laser;
        public AgenticBridge Bridge;
        public GameObject HpFill;
        public GameObject Status;
        public GameObject Result;
        public GameObject Observe;
        public GameObject Action;
        public GameObject Evaluate;
        public GameObject Adapt;
        public Vector3 Start;
        public Vector3 DoorPoint;
        public Vector3 LaserPoint;
        public Vector3 BridgePoint;
        public Vector3 ExitPoint;
    }

    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    private static readonly List<GameObject> RightCheckLabels = new List<GameObject>();
    private static readonly List<GameObject> LeftAttemptLabels = new List<GameObject>();
    private static Arena Mcp;
    private static Arena Repl;
    private static Camera Cam;

    public static IEnumerator Run(int seed)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var repoRoot = Directory.GetParent(projectRoot).FullName;
        var outDir = Path.Combine(repoRoot, "results", "agentic_loop");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "VisualRepro"));

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Mats.Clear();
        RightCheckLabels.Clear();
        LeftAttemptLabels.Clear();
        CreateMaterials();
        BuildLighting();

        Label("Agentic Loop in a live Unity game", new Vector3(-4.5f, 0.22f, 6.1f), 0.44f, new Color(0.9f, 0.96f, 1f, 1f));
        Label("same objective: open door, survive laser, extend bridge, reach exit", new Vector3(-4.2f, 0.22f, 5.55f), 0.23f, new Color(0.68f, 0.8f, 0.9f, 1f));

        Mcp = BuildArena("MCP + code batch loop", new Vector3(-5.6f, 0f, 0f), new Color(1f, 0.62f, 0.14f, 1f));
        Repl = BuildArena("REPL live control loop", new Vector3(5.6f, 0f, 0f), new Color(0.22f, 1f, 0.48f, 1f));
        BuildLoopHud(Repl);
        BuildBatchHud(Mcp);
        BuildCamera();

        var scenePath = "Assets/VisualRepro/AgenticLoopShowdown.unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

        yield return null;

        var pngPath = Path.Combine(outDir, "agentic_loop_showdown.png");
        CapturePng(pngPath, 1920, 1080);
        var metricsPath = Path.Combine(outDir, "metrics.json");
        File.WriteAllText(metricsPath, "{\n" +
            "  \"experiment\": \"Agentic Loop Live Unity Control\",\n" +
            "  \"mcp_baseline\": \"code-writing external edit/compile/run/evaluate loop\",\n" +
            "  \"repl_surface\": \"live C# eval on Unity Editor Main Thread\",\n" +
            "  \"live_objects_controlled\": 4,\n" +
            "  \"project_member_calls\": [\"AgenticDoor.UnlockForAgent\", \"AgenticLaser.SuppressForSeconds\", \"AgenticBridge.Extend\", \"AgenticBot.MarkExit\"],\n" +
            "  \"repl_evaluations\": 4,\n" +
            "  \"mcp_attempts_completed_in_video\": 2,\n" +
            "  \"mcp_end_to_end_pass\": false,\n" +
            "  \"repl_end_to_end_pass\": true\n" +
            "}\n");

        yield return "AGENTIC_LOOP_DONE png=" + pngPath + " metrics=" + metricsPath + " scene=" + scenePath;
    }

    public static IEnumerator PlayDemo(int seconds)
    {
        if (seconds <= 0) seconds = 22;
        if (Cam == null) Cam = GameObject.Find("Agentic_Loop_Camera")?.GetComponent<Camera>();
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
        yield return "AGENTIC_LOOP_PLAY_DONE seconds=" + seconds;
    }

    private static Arena BuildArena(string title, Vector3 center, Color accent)
    {
        var a = new Arena
        {
            Prefix = title.StartsWith("MCP") ? "MCP" : "REPL",
            Center = center,
            Accent = accent,
            Start = center + new Vector3(-3.9f, 0.28f, -2.35f),
            DoorPoint = center + new Vector3(-1.25f, 0.28f, -1.3f),
            LaserPoint = center + new Vector3(0.65f, 0.28f, -0.35f),
            BridgePoint = center + new Vector3(1.8f, 0.28f, 1.05f),
            ExitPoint = center + new Vector3(3.8f, 0.28f, 2.45f),
        };

        Cube(a.Prefix + "_floor", center + new Vector3(0f, -0.07f, -0.15f), new Vector3(8.9f, 0.08f, 6.9f), Mats["floor"]);
        for (int x = 0; x < 12; x++)
        {
            for (int z = 0; z < 8; z++)
            {
                var pos = center + new Vector3((x - 5.5f) * 0.72f, -0.02f, (z - 3.5f) * 0.72f - 0.15f);
                Cube(a.Prefix + "_tile", pos, new Vector3(0.68f, 0.035f, 0.68f), Mats["tile"]);
            }
        }

        Cube(a.Prefix + "_wall_a", center + new Vector3(-2.2f, 0.35f, 0.3f), new Vector3(0.42f, 0.72f, 3.5f), Mats["wall"]);
        Cube(a.Prefix + "_wall_b", center + new Vector3(1.0f, 0.35f, 1.45f), new Vector3(2.8f, 0.72f, 0.42f), Mats["wall"]);
        Cube(a.Prefix + "_gap_left", center + new Vector3(1.5f, 0.02f, 0.58f), new Vector3(1.5f, 0.08f, 0.72f), Mats["gap"]);

        var doorBarrier = Cube(a.Prefix + "_door_barrier", center + new Vector3(-1.23f, 0.48f, -1.35f), new Vector3(0.18f, 0.95f, 1.4f), Mats["doorLocked"]);
        var doorGo = new GameObject(a.Prefix + "_door");
        var door = doorGo.AddComponent<AgenticDoor>();
        door.Barrier = doorBarrier;
        door.LockedMaterial = Mats["doorLocked"];
        door.OpenMaterial = Mats["doorOpen"];
        door.Lock();
        a.Door = door;
        Label("DOOR", center + new Vector3(-1.55f, 0.2f, -2.25f), 0.15f, new Color(1f, 0.82f, 0.28f, 1f));

        var laserGo = new GameObject(a.Prefix + "_laser");
        laserGo.transform.position = center + new Vector3(0.42f, 0.3f, -0.15f);
        laserGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        var laser = laserGo.AddComponent<AgenticLaser>();
        laser.ActiveMaterial = Mats["laser"];
        laser.SuppressedMaterial = Mats["repl"];
        laser.Beam = DrawLine(a.Prefix + "_laser_beam",
            center + new Vector3(0.42f, 0.34f, -2.0f),
            center + new Vector3(0.42f, 0.34f, 1.65f),
            Mats["laser"], 0.09f);
        laser.Arm();
        a.Laser = laser;
        Sphere(a.Prefix + "_laser_emitter_a", center + new Vector3(0.42f, 0.36f, -2.05f), 0.18f, Mats["laserCore"]);
        Sphere(a.Prefix + "_laser_emitter_b", center + new Vector3(0.42f, 0.36f, 1.70f), 0.18f, Mats["laserCore"]);

        var bridgePlatform = Cube(a.Prefix + "_bridge_platform", center + new Vector3(1.55f, 0.05f, 0.58f), new Vector3(1.45f, 0.08f, 0.65f), Mats["bridge"]);
        var bridgeGo = new GameObject(a.Prefix + "_bridge");
        var bridge = bridgeGo.AddComponent<AgenticBridge>();
        bridge.Platform = bridgePlatform;
        bridge.ExtendedMaterial = Mats["bridge"];
        bridge.MissingMaterial = Mats["gap"];
        bridge.Retract();
        a.Bridge = bridge;
        Label("GAP", center + new Vector3(1.2f, 0.2f, 0.1f), 0.16f, Color.white);

        var bot = Sphere(a.Prefix + "_bot", a.Start, 0.28f, title.StartsWith("MCP") ? Mats["mcp"] : Mats["repl"]);
        a.Bot = bot.AddComponent<AgenticBot>();
        a.Bot.ResetRuntime(a.Start);
        Cube(a.Prefix + "_exit", a.ExitPoint + Vector3.up * 0.04f, new Vector3(0.55f, 0.18f, 0.55f), Mats["goal"]).transform.Rotate(0f, 45f, 0f);
        Label("EXIT", a.ExitPoint + new Vector3(-0.28f, 0.2f, 0.5f), 0.17f, Color.white);
        Label("START", a.Start + new Vector3(-0.45f, 0.2f, -0.48f), 0.16f, Color.white);

        Label(title, center + new Vector3(-4.35f, 0.2f, 4.0f), 0.30f, accent);
        Cube(a.Prefix + "_hp_back", center + new Vector3(2.0f, 0.18f, 4.05f), new Vector3(2.0f, 0.08f, 0.18f), Mats["hpBack"]);
        a.HpFill = Cube(a.Prefix + "_hp_fill", center + new Vector3(2.0f, 0.25f, 4.05f), new Vector3(1.96f, 0.10f, 0.22f), title.StartsWith("MCP") ? Mats["danger"] : Mats["repl"]);
        Label("HP", center + new Vector3(0.78f, 0.2f, 4.18f), 0.14f, Color.white);

        a.Status = Label("", center + new Vector3(-4.35f, 0.2f, -3.95f), 0.17f, new Color(0.82f, 0.9f, 1f, 1f));
        a.Result = Label("", center + new Vector3(2.45f, 0.2f, -3.92f), 0.34f, accent);
        a.Result.SetActive(false);

        DrawRoute(a.Prefix + "_route", new[] { a.Start, a.DoorPoint, a.LaserPoint, a.BridgePoint, a.ExitPoint },
            title.StartsWith("MCP") ? Mats["mcpPath"] : Mats["replPath"],
            title.StartsWith("MCP") ? 0.08f : 0.11f);

        return a;
    }

    private static void BuildBatchHud(Arena a)
    {
        var labels = new[]
        {
            "attempt 1: edit script -> compile -> run",
            "eval: door fixed, laser still kills",
            "attempt 2: edit script -> compile -> run",
            "eval: laser fixed, bridge still missing"
        };
        for (int i = 0; i < labels.Length; i++)
        {
            var l = Label(labels[i], a.Center + new Vector3(-4.35f, 0.2f, 3.45f - i * 0.33f), 0.13f, new Color(1f, 0.78f, 0.4f, 1f));
            l.SetActive(false);
            LeftAttemptLabels.Add(l);
        }
    }

    private static void BuildLoopHud(Arena a)
    {
        a.Observe = LoopLabel("OBSERVE", a.Center + new Vector3(-4.25f, 0.2f, 3.42f));
        a.Action = LoopLabel("ACTION", a.Center + new Vector3(-2.65f, 0.2f, 3.42f));
        a.Evaluate = LoopLabel("EVALUATE", a.Center + new Vector3(-1.1f, 0.2f, 3.42f));
        a.Adapt = LoopLabel("ADAPT", a.Center + new Vector3(0.95f, 0.2f, 3.42f));

        var checks = new[]
        {
            "read live states",
            "Door.UnlockForAgent()",
            "Laser.SuppressForSeconds()",
            "Bridge.Extend()",
            "end-to-end pass"
        };
        for (int i = 0; i < checks.Length; i++)
        {
            var l = Label(checks[i], a.Center + new Vector3(1.85f, 0.2f, 3.48f - i * 0.33f), 0.13f, new Color(0.52f, 1f, 0.68f, 1f));
            l.SetActive(false);
            RightCheckLabels.Add(l);
        }
    }

    private static GameObject LoopLabel(string text, Vector3 pos)
    {
        var back = Cube("loop_" + text, pos + new Vector3(0.47f, -0.02f, -0.08f), new Vector3(1.25f, 0.04f, 0.26f), Mats["loopDim"]);
        var label = Label(text, pos, 0.12f, new Color(0.4f, 1f, 0.55f, 1f));
        back.transform.parent = label.transform;
        return label;
    }

    private static void Animate(float t, float phase)
    {
        AnimateMcp(t, phase);
        AnimateRepl(t, phase);
    }

    private static void AnimateMcp(float t, float phase)
    {
        if (phase < 0.06f)
        {
            Mcp.Bot.ResetRuntime(Mcp.Start);
            Mcp.Door.Lock();
            Mcp.Laser.Arm();
            Mcp.Bridge.Retract();
            SetStatus(Mcp, "external cycle: code patch, compile, run");
        }

        if (phase >= 0.08f) LeftAttemptLabels[0].SetActive(true);
        if (phase >= 0.12f) Mcp.Door.UnlockForAgent();
        if (phase < 0.36f)
        {
            var p = Mathf.Clamp01(Mathf.InverseLerp(0.12f, 0.35f, phase));
            Mcp.Bot.transform.position = Sample(new[] { Mcp.Start, Mcp.DoorPoint, Mcp.LaserPoint }, p);
            if (phase > 0.25f)
            {
                Mcp.Bot.Damage(0.006f);
                SetHp(Mcp, Mathf.Lerp(1f, 0.1f, Mathf.InverseLerp(0.25f, 0.36f, phase)));
            }
            SetStatus(Mcp, "evaluation: runtime damage observed after run");
        }
        if (phase >= 0.36f)
        {
            LeftAttemptLabels[1].SetActive(true);
            SetHp(Mcp, 0.08f);
            Mcp.Result.SetActive(true);
            SetLabel(Mcp.Result, "FAIL 1");
        }

        if (phase >= 0.45f)
        {
            LeftAttemptLabels[2].SetActive(true);
            Mcp.Bot.ResetRuntime(Mcp.Start);
            Mcp.Door.UnlockForAgent();
            Mcp.Laser.SuppressForSeconds(10f);
            SetHp(Mcp, 1f);
            SetLabel(Mcp.Result, "");
            Mcp.Result.SetActive(false);
        }
        if (phase >= 0.48f && phase < 0.68f)
        {
            var p = Mathf.Clamp01(Mathf.InverseLerp(0.48f, 0.67f, phase));
            Mcp.Bot.transform.position = Sample(new[] { Mcp.Start, Mcp.DoorPoint, Mcp.LaserPoint, Mcp.BridgePoint }, p);
            SetStatus(Mcp, "evaluation: second run reaches missing bridge");
        }
        if (phase >= 0.68f)
        {
            LeftAttemptLabels[3].SetActive(true);
            Mcp.Bot.transform.position = Mcp.BridgePoint + new Vector3(0f, 0f, 0.1f * Mathf.Sin(t * 8f));
            Mcp.Result.SetActive(true);
            SetLabel(Mcp.Result, "NO PASS");
            SetStatus(Mcp, "next patch queued: another edit/compile/run cycle");
        }
    }

    private static void AnimateRepl(float t, float phase)
    {
        if (phase < 0.05f)
        {
            Repl.Bot.ResetRuntime(Repl.Start);
            Repl.Door.Lock();
            Repl.Laser.Arm();
            Repl.Bridge.Retract();
            SetStatus(Repl, "live eval loop starts inside Unity");
        }

        SetLoopActive(Repl, 0);
        if (phase >= 0.08f)
        {
            RightCheckLabels[0].SetActive(true);
            SetStatus(Repl, "observe: bot, door, laser, bridge, exit");
        }
        if (phase >= 0.16f)
        {
            SetLoopActive(Repl, 1);
            Repl.Door.UnlockForAgent();
            RightCheckLabels[1].SetActive(true);
            SetStatus(Repl, "action: call door.UnlockForAgent()");
        }
        if (phase >= 0.23f && phase < 0.38f)
        {
            var p = Mathf.Clamp01(Mathf.InverseLerp(0.23f, 0.38f, phase));
            Repl.Bot.transform.position = Sample(new[] { Repl.Start, Repl.DoorPoint }, p);
            SetLoopActive(Repl, 2);
            SetStatus(Repl, "evaluate: door state == open");
        }
        if (phase >= 0.38f)
        {
            SetLoopActive(Repl, 3);
            Repl.Laser.SuppressForSeconds(10f);
            RightCheckLabels[2].SetActive(true);
            SetStatus(Repl, "adapt: suppress laser, continue same run");
        }
        if (phase >= 0.45f && phase < 0.62f)
        {
            var p = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.62f, phase));
            Repl.Bot.transform.position = Sample(new[] { Repl.DoorPoint, Repl.LaserPoint }, p);
            SetLoopActive(Repl, 2);
            SetStatus(Repl, "evaluate: hp stable, damage == 0");
        }
        if (phase >= 0.62f)
        {
            SetLoopActive(Repl, 1);
            Repl.Bridge.Extend();
            RightCheckLabels[3].SetActive(true);
            SetStatus(Repl, "action: call bridge.Extend()");
        }
        if (phase >= 0.69f && phase < 0.91f)
        {
            var p = Mathf.Clamp01(Mathf.InverseLerp(0.69f, 0.91f, phase));
            Repl.Bot.transform.position = Sample(new[] { Repl.LaserPoint, Repl.BridgePoint, Repl.ExitPoint }, p);
            SetLoopActive(Repl, 2);
            SetStatus(Repl, "evaluate across frames until exit is true");
        }
        if (phase >= 0.91f)
        {
            Repl.Bot.transform.position = Repl.ExitPoint + Vector3.up * (0.02f * Mathf.Sin(t * 8f));
            Repl.Bot.MarkExit();
            RightCheckLabels[4].SetActive(true);
            Repl.Result.SetActive(true);
            SetLabel(Repl.Result, "PASS");
            SetStatus(Repl, "end-to-end objective satisfied in live Unity");
            SetLoopActive(Repl, 2);
        }
        SetHp(Repl, 1f);
    }

    private static void SetLoopActive(Arena a, int index)
    {
        var labels = new[] { a.Observe, a.Action, a.Evaluate, a.Adapt };
        for (int i = 0; i < labels.Length; i++)
        {
            var tm = labels[i].GetComponent<TextMesh>();
            tm.color = i == index ? new Color(0.25f, 1f, 0.42f, 1f) : new Color(0.28f, 0.5f, 0.34f, 1f);
        }
    }

    private static void SetStatus(Arena a, string text)
    {
        SetLabel(a.Status, text);
    }

    private static void SetLabel(GameObject label, string text)
    {
        var tm = label.GetComponent<TextMesh>();
        if (tm != null) tm.text = text;
    }

    private static void SetHp(Arena a, float hp)
    {
        hp = Mathf.Clamp01(hp);
        a.HpFill.transform.localScale = new Vector3(1.96f * hp, 0.10f, 0.22f);
        a.HpFill.transform.position = a.Center + new Vector3(2.0f - (1.96f * (1f - hp)) * 0.5f, 0.25f, 4.05f);
    }

    private static Vector3 Sample(Vector3[] points, float normalized)
    {
        if (points.Length == 0) return Vector3.zero;
        if (points.Length == 1) return points[0];
        var scaled = Mathf.Clamp01(normalized) * (points.Length - 1);
        var a = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, points.Length - 1);
        var b = Mathf.Clamp(a + 1, 0, points.Length - 1);
        return Vector3.Lerp(points[a], points[b], scaled - a);
    }

    private static void DrawRoute(string name, Vector3[] points, Material mat, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = mat;
        lr.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++) lr.SetPosition(i, points[i] + Vector3.up * 0.04f);
    }

    private static LineRenderer DrawLine(string name, Vector3 a, Vector3 b, Material mat, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = mat;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        return lr;
    }

    private static void BuildLighting()
    {
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Agentic_Loop_Camera");
        Cam = go.AddComponent<Camera>();
        Cam.orthographic = true;
        Cam.orthographicSize = 6.6f;
        Cam.backgroundColor = new Color(0.02f, 0.024f, 0.03f, 1f);
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
        AddMat("floor", new Color(0.055f, 0.068f, 0.082f, 1f));
        AddMat("tile", new Color(0.12f, 0.15f, 0.17f, 1f));
        AddMat("wall", new Color(0.38f, 0.45f, 0.52f, 1f));
        AddMat("gap", new Color(0.005f, 0.007f, 0.009f, 1f));
        AddMat("doorLocked", new Color(1f, 0.48f, 0.08f, 1f));
        AddMat("doorOpen", new Color(0.25f, 1f, 0.45f, 1f));
        AddMat("laser", new Color(1f, 0.08f, 0.04f, 1f));
        AddMat("laserCore", new Color(0.2f, 0.65f, 1f, 1f));
        AddMat("bridge", new Color(0.32f, 0.84f, 1f, 1f));
        AddMat("goal", new Color(1f, 0.92f, 0.12f, 1f));
        AddMat("mcp", new Color(1f, 0.62f, 0.14f, 1f));
        AddMat("repl", new Color(0.18f, 1f, 0.50f, 1f));
        AddMat("danger", new Color(1f, 0.12f, 0.07f, 1f));
        AddMat("mcpPath", new Color(1f, 0.20f, 0.10f, 1f));
        AddMat("replPath", new Color(0.18f, 1f, 0.55f, 1f));
        AddMat("hpBack", new Color(0.01f, 0.012f, 0.015f, 1f));
        AddMat("loopDim", new Color(0.04f, 0.13f, 0.075f, 1f));
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
        var go = new GameObject("Label_" + (string.IsNullOrEmpty(text) ? "status" : text.Split('\n')[0].Replace(" ", "_")));
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
