
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LightSensor : MonoBehaviour
{
    public bool isActive = false;

    public float checkInterval = 0.05f;

    private Collider sensorCollider;
    private float nextCheckTime;

    private readonly HashSet<BeamReflector> reflectedBy =
        new HashSet<BeamReflector>();

    void Awake()
    {
        sensorCollider = GetComponent<Collider>();
    }

    public void SetReflectedIllumination(
        BeamReflector reflector, bool illuminated)
    {
        if (reflector == null)
            return;

        if (illuminated)
            reflectedBy.Add(reflector);
        else
            reflectedBy.Remove(reflector);
    }

    void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        bool directLight = IsDirectlyLit();

        reflectedBy.RemoveWhere(r => r == null);

        isActive = directLight || reflectedBy.Count > 0;
    }

    bool IsDirectlyLit()
    {
        foreach (LightSource source in LightSource.All)
        {
            if (source == null || source.SourceLight == null)
                continue;

            Light light = source.SourceLight;

            if (!light.isActiveAndEnabled ||
                light.intensity <= 0f)
                continue;

            if (light.type != LightType.Spot &&
                light.type != LightType.Point)
                continue;

            Vector3 target = sensorCollider.bounds.center;
            Vector3 offset = target - light.transform.position;
            float distance = offset.magnitude;

            if (distance <= 0.001f || distance > light.range)
                continue;

            Vector3 direction = offset / distance;

            if (light.type == LightType.Spot)
            {
                float angle = Vector3.Angle(
                    light.transform.forward, direction);

                if (angle > light.spotAngle * 0.5f)
                    continue;
            }

            if (Physics.Raycast(
                light.transform.position,
                direction,
                out RaycastHit hit,
                distance + 0.1f,
                ~0,
                QueryTriggerInteraction.Collide))
            {
                if (hit.collider == sensorCollider)
                    return true;
            }
        }

        return false;
    }

    void OnDisable()
    {
        isActive = false;
        reflectedBy.Clear();
    }
}
