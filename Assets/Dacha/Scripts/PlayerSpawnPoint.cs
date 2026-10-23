using System.Collections.Generic;
using UnityEngine;

// Place in the scene where players should appear. Players are given points
// in order (host -> 1st, next player -> 2nd, ...); the blue arrow is the
// direction they face.
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Lower numbers are used first.")]
    [SerializeField] int order;

    static readonly List<PlayerSpawnPoint> points = new();

    void OnEnable() => points.Add(this);
    void OnDisable() => points.Remove(this);

    // Returns the spawn point for the given player index, or null if none exist.
    public static PlayerSpawnPoint Get(int index)
    {
        if (points.Count == 0) return null;
        points.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));
        return points[index % points.Count];
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        Gizmos.color = Color.cyan;
        Vector3 start = transform.position + Vector3.up;
        Vector3 end = start + transform.forward * 1.5f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(end, end - Quaternion.Euler(0, 30, 0) * transform.forward * 0.4f);
        Gizmos.DrawLine(end, end - Quaternion.Euler(0, -30, 0) * transform.forward * 0.4f);
    }
}
