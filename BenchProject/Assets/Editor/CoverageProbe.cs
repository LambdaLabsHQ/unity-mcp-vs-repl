using System.Linq;
using UnityEngine;

public static class CoverageProbe
{
    public static string Dump(Vector3 point)
    {
        return string.Join("\n", Object.FindObjectsOfType<MonoBehaviour>()
            .Where(m => m.GetType().GetMethod("CanReach", new[] { typeof(Vector3) }) != null)
            .OrderBy(m => m.name)
            .Select(m => m.name + " => " + m.GetType().GetMethod("CanReach", new[] { typeof(Vector3) }).Invoke(m, new object[] { point })));
    }
}
