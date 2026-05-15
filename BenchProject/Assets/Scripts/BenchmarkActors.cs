using UnityEngine;

public sealed class Health : MonoBehaviour
{
    public int hitPoints = 100;

    public void Damage(int amount)
    {
        hitPoints = Mathf.Max(0, hitPoints - amount);
    }
}

public sealed class Turret : MonoBehaviour
{
    public float range = 15f;
    public Transform currentTarget;

    public float DistanceTo(Vector3 point)
    {
        return Vector3.Distance(transform.position, point);
    }

    public bool CanReach(Vector3 point)
    {
        return DistanceTo(point) <= range;
    }
}
