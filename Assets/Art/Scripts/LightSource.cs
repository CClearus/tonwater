
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> All =
        new List<LightSource>();

    public Light SourceLight { get; private set; }

    void Awake()
    {
        SourceLight = GetComponent<Light>();
    }

    void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }
}
