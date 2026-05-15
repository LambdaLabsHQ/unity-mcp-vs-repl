using System.Linq;
using UnityEngine;

public static class TurretRangeProbe
{
    public static string RunAt(Vector3 pos)
    {
        return string.Join("\n", GameObject.FindObjectsOfType<Turret>()
            .OrderBy(t => t.name)
            .Select(t => $"{t.name}: distance={t.DistanceTo(pos):F2}, canReach={t.CanReach(pos)}"));
    }
}