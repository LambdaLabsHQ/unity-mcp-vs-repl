using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static class UnityEditorLiveRecording
{
    private static Camera _camera;
    private static GameObject _marker;
    private static TextMesh _status;
    private static LineRenderer _beam;
    private static readonly List<GameObject> _toolTokens = new List<GameObject>();
    private static readonly List<Transform> _waypoints = new List<Transform>();
    private static readonly List<Transform> _riskBars = new List<Transform>();
    private static readonly List<Vector3> _riskBarBaseScales = new List<Vector3>();
    private static readonly List<GameObject> _turrets = new List<GameObject>();
    private static Material _markerMaterial;
    private static Material _toolTokenMaterial;
    private static Material _beamMaterial;

    public static IEnumerator PlayDemo(int seconds)
    {
        if (seconds <= 0) seconds = 18;

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        yield return null;
        yield return null;

        PrepareScene();

        double start = EditorApplication.timeSinceStartup;
        while (true)
        {
            float elapsed = (float)(EditorApplication.timeSinceStartup - start);
            if (elapsed >= seconds) break;

            AnimateScene(elapsed, seconds <= 0 ? 1f : elapsed / seconds, seconds);
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
            yield return null;
        }

        AnimateScene(seconds, 1f, seconds);
        yield return "UNITY_EDITOR_LIVE_DEMO_DONE seconds=" + seconds;
    }

    public static IEnumerator Record(string frameDir, int windowX, int windowY, int windowWidth, int windowHeight, int fps, int seconds)
    {
        if (fps <= 0) fps = 12;
        if (seconds <= 0) seconds = 26;
        if (windowWidth <= 0 || windowHeight <= 0) throw new ArgumentException("Invalid capture window size.");

        Directory.CreateDirectory(frameDir);
        foreach (var old in Directory.GetFiles(frameDir, "frame_*.jpg"))
            File.Delete(old);

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        yield return null;
        yield return null;

        PrepareScene();

        int totalFrames = fps * seconds;
        var capturePos = new Vector2(windowX, windowY);
        var texture = new Texture2D(windowWidth, windowHeight, TextureFormat.RGB24, false);

        for (int frame = 0; frame < totalFrames; frame++)
        {
            float time = frame / (float)fps;
            float normalized = totalFrames <= 1 ? 1f : frame / (float)(totalFrames - 1);
            AnimateScene(time, normalized, seconds);

            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
            SceneView.RepaintAll();
            yield return null;

            var pixels = InternalEditorUtility.ReadScreenPixel(capturePos, windowWidth, windowHeight);
            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(Path.Combine(frameDir, "frame_" + frame.ToString("0000") + ".jpg"), texture.EncodeToJPG(88));

            if (frame % Math.Max(1, fps) == 0)
                Debug.Log("UnityEditorLiveRecording frame " + frame + "/" + totalFrames);
        }

        UnityEngine.Object.DestroyImmediate(texture);
        yield return "UNITY_EDITOR_LIVE_RECORDING_DONE frames=" + totalFrames + " dir=" + frameDir;
    }

    private static void PrepareScene()
    {
        _camera = GameObject.Find("Visual_Repro_Camera")?.GetComponent<Camera>() ?? Camera.main;
        if (_camera == null) throw new InvalidOperationException("Visual_Repro_Camera not found. Run VisualReproExperiment.Run(1337) first.");

        _waypoints.Clear();
        _waypoints.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("Path_Waypoint_", StringComparison.Ordinal))
            .OrderBy(t => ExtractIndex(t.name))
            .ToArray());

        _riskBars.Clear();
        _riskBars.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("RiskBar_", StringComparison.Ordinal))
            .OrderBy(t => ExtractIndex(t.name))
            .ToArray());
        _riskBarBaseScales.Clear();
        _riskBarBaseScales.AddRange(_riskBars.Select(t => t.localScale));

        _turrets.Clear();
        _turrets.AddRange(UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.name.StartsWith("T_", StringComparison.Ordinal))
            .OrderBy(go => go.name)
            .ToArray());

        _markerMaterial = MakeMaterial("Recording_LiveMarker_Mat", new Color(0.1f, 1f, 0.78f, 1f));
        _toolTokenMaterial = MakeMaterial("Recording_ToolToken_Mat", new Color(1f, 0.55f, 0.12f, 1f));
        _beamMaterial = MakeMaterial("Recording_Beam_Mat", new Color(0.4f, 0.95f, 1f, 1f));

        _marker = GameObject.Find("Recording_REPL_Live_Marker");
        if (_marker == null)
        {
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "Recording_REPL_Live_Marker";
            _marker.transform.localScale = Vector3.one * 0.95f;
        }
        _marker.GetComponent<Renderer>().sharedMaterial = _markerMaterial;

        var statusGo = GameObject.Find("Recording_Status_Label");
        if (statusGo == null)
        {
            statusGo = new GameObject("Recording_Status_Label");
            _status = statusGo.AddComponent<TextMesh>();
            _status.fontSize = 64;
            _status.characterSize = 0.09f;
            _status.anchor = TextAnchor.UpperLeft;
            _status.alignment = TextAlignment.Left;
        }
        else
        {
            _status = statusGo.GetComponent<TextMesh>() ?? statusGo.AddComponent<TextMesh>();
        }
        statusGo.transform.position = new Vector3(-16.2f, 1.1f, 13.3f);
        statusGo.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        _status.color = new Color(0.86f, 0.96f, 1f, 1f);

        var beamGo = GameObject.Find("Recording_REPL_Beam");
        if (beamGo == null)
        {
            beamGo = new GameObject("Recording_REPL_Beam");
            _beam = beamGo.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.widthMultiplier = 0.11f;
        }
        else
        {
            _beam = beamGo.GetComponent<LineRenderer>() ?? beamGo.AddComponent<LineRenderer>();
        }
        _beam.material = _beamMaterial;

        BuildToolTokens();
        Selection.activeGameObject = _camera.gameObject;
        EditorGUIUtility.PingObject(_camera.gameObject);
    }

    private static void AnimateScene(float time, float normalized, int seconds)
    {
        float phase = time / seconds;
        SetCamera(phase);
        AnimateMarker(time, phase);
        AnimateRiskBars(time);
        AnimateToolTokens(time, phase);
        UpdateStatusAndSelection(time, phase);
    }

    private static void SetCamera(float phase)
    {
        var full = new CamPose(new Vector3(0f, 24f, -20f), 16.2f);
        var repl = new CamPose(new Vector3(-9.6f, 18f, -17f), 8.0f);
        var mcp = new CamPose(new Vector3(8.6f, 18f, -17f), 8.2f);
        var path = new CamPose(new Vector3(2.2f, 19f, -17.5f), 10.5f);

        CamPose pose;
        if (phase < 0.20f) pose = Ease(full, full, phase / 0.20f);
        else if (phase < 0.40f) pose = Ease(full, repl, (phase - 0.20f) / 0.20f);
        else if (phase < 0.60f) pose = Ease(repl, mcp, (phase - 0.40f) / 0.20f);
        else if (phase < 0.82f) pose = Ease(mcp, path, (phase - 0.60f) / 0.22f);
        else pose = Ease(path, full, (phase - 0.82f) / 0.18f);

        _camera.transform.position = pose.Position;
        _camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        _camera.orthographic = true;
        _camera.orthographicSize = pose.Size;
    }

    private static void AnimateMarker(float time, float phase)
    {
        if (_waypoints.Count == 0) return;
        float loop = Mathf.Repeat(time * 0.45f, 1f);
        float scaled = loop * (_waypoints.Count - 1);
        int a = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, _waypoints.Count - 1);
        int b = Mathf.Clamp(a + 1, 0, _waypoints.Count - 1);
        Vector3 pos = Vector3.Lerp(_waypoints[a].position, _waypoints[b].position, scaled - a);
        _marker.transform.position = pos + Vector3.up * (0.65f + Mathf.Sin(time * 7f) * 0.16f);

        var start = new Vector3(-14.5f, 1.1f, -12.2f);
        _beam.SetPosition(0, start);
        _beam.SetPosition(1, _marker.transform.position);
        var alpha = 0.45f + 0.35f * Mathf.Sin(time * 5f);
        _beam.startColor = new Color(0.35f, 1f, 0.9f, alpha);
        _beam.endColor = new Color(0.35f, 1f, 0.9f, alpha);
    }

    private static void AnimateRiskBars(float time)
    {
        for (int i = 0; i < _riskBars.Count; i++)
        {
            var tr = _riskBars[i];
            var baseScale = i < _riskBarBaseScales.Count ? _riskBarBaseScales[i] : tr.localScale;
            float pulse = 1f + 0.18f * Mathf.Max(0f, Mathf.Sin(time * 5f - i * 0.55f));
            tr.localScale = new Vector3(baseScale.x, Mathf.Max(0.2f, baseScale.y) * pulse, baseScale.z);
        }
    }

    private static void AnimateToolTokens(float time, float phase)
    {
        float local = Mathf.InverseLerp(0.42f, 0.62f, phase);
        int visible = Mathf.Clamp(Mathf.CeilToInt(local * _toolTokens.Count), 0, _toolTokens.Count);
        for (int i = 0; i < _toolTokens.Count; i++)
        {
            var token = _toolTokens[i];
            token.SetActive(i < visible);
            if (!token.activeSelf) continue;
            float scale = 0.75f + 0.25f * Mathf.Sin(time * 8f + i * 0.4f);
            token.transform.localScale = new Vector3(0.24f, 0.18f + scale * 0.14f, 0.24f);
        }
    }

    private static void UpdateStatusAndSelection(float time, float phase)
    {
        if (phase < 0.20f)
        {
            _status.text = "Unity Editor capture: real window, not slide playback\nREPL eval builds the scene and records every frame";
            Selection.activeGameObject = _camera.gameObject;
        }
        else if (phase < 0.40f)
        {
            _status.text = "REPL: one stable eval interface\nReflection discovered project API: CanReach(Vector3)";
            Selection.activeGameObject = _marker;
        }
        else if (phase < 0.60f)
        {
            _status.text = "MCP tool table: 42 observed tools\nReference context grows before the task even starts";
            Selection.activeGameObject = _toolTokens.FirstOrDefault(x => x.activeSelf) ?? _marker;
        }
        else if (phase < 0.82f)
        {
            _status.text = "Long-tail Unity task\nLINQ + geometry + custom methods + camera + screenshot in one C# program";
            Selection.activeGameObject = _waypoints.Count > 0 ? _waypoints[(int)(time * 2f) % _waypoints.Count].gameObject : _marker;
        }
        else
        {
            _status.text = "If MCP falls back to execute_code, it became REPL inside MCP\nThe decisive advantage is lower context and recall burden";
            Selection.activeGameObject = _camera.gameObject;
        }
    }

    private static void BuildToolTokens()
    {
        for (int i = 0; i < _toolTokens.Count; i++)
            if (_toolTokens[i] != null) UnityEngine.Object.DestroyImmediate(_toolTokens[i]);
        _toolTokens.Clear();

        for (int i = 0; i < 42; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Recording_MCP_Tool_Token_" + i.ToString("00");
            int col = i % 14;
            int row = i / 14;
            go.transform.position = new Vector3(4.2f + col * 0.42f, 0.45f + row * 0.35f, -11.8f - row * 0.42f);
            go.transform.localScale = new Vector3(0.24f, 0.28f, 0.24f);
            go.GetComponent<Renderer>().sharedMaterial = _toolTokenMaterial;
            go.SetActive(false);
            _toolTokens.Add(go);
        }
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        mat.color = color;
        return mat;
    }

    private static int ExtractIndex(string name)
    {
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private static CamPose Ease(CamPose a, CamPose b, float t)
    {
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t);
        return new CamPose(Vector3.Lerp(a.Position, b.Position, t), Mathf.Lerp(a.Size, b.Size, t));
    }

    private struct CamPose
    {
        public Vector3 Position;
        public float Size;

        public CamPose(Vector3 position, float size)
        {
            Position = position;
            Size = size;
        }
    }
}
