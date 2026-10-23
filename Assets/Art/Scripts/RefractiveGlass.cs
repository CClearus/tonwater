
using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RefractiveGlass : MonoBehaviour
{
    [Header("Glass Settings")]
    [Range(1f, 2.5f)]
    public float refractiveIndex = 1.5f;

    [Header("Refracted Light")]
    public float lightIntensityMultiplier = 1f;
    public bool castShadows = true;
    public LayerMask beamMask = ~0;

    [Header("Visible Beam")]
    public float beamWidth = 0.025f;
    public float surfaceOffset = 0.02f;

    private Collider glassCollider;
    private LineRenderer beamLine;
    private Light refractedLight;
    private LightSource refractedLightSource;
    private Material beamMaterial;

    private const float AIR_IOR = 1f;
    private const float MIN_DIRECTION = 0.0001f;

    private void Awake()
    {
        glassCollider = GetComponent<Collider>();

        CreateBeamLine();
        CreateRefractedLight();
    }

    private void Update()
    {
        UpdateRefraction();
    }

    private void CreateBeamLine()
    {
        GameObject lineObject = new GameObject("Glass Refracted Beam");
        lineObject.transform.SetParent(transform);
        lineObject.transform.localPosition = Vector3.zero;

        beamLine = lineObject.AddComponent<LineRenderer>();
        beamLine.useWorldSpace = true;
        beamLine.positionCount = 4;
        beamLine.startWidth = beamWidth;
        beamLine.endWidth = beamWidth;
        beamLine.numCapVertices = 4;
        beamLine.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            beamMaterial = new Material(shader);
            beamLine.material = beamMaterial;
        }
    }

    private void CreateRefractedLight()
    {
        GameObject lightObject = new GameObject("Refracted Light");

        refractedLight = lightObject.AddComponent<Light>();
        refractedLight.type = LightType.Spot;
        refractedLight.enabled = false;

        refractedLightSource = lightObject.AddComponent<LightSource>();
    }

    private void UpdateRefraction()
    {
        beamLine.enabled = false;
        refractedLight.enabled = false;

        foreach (LightSource source in LightSource.All)
        {
            if (source == null || source == refractedLightSource)
                continue;

            Light sourceLight = source.SourceLight;

            if (sourceLight == null ||
                sourceLight == refractedLight ||
                !sourceLight.enabled ||
                sourceLight.type != LightType.Spot ||
                sourceLight.intensity <= 0f)
            {
                continue;
            }

            Vector3 origin = sourceLight.transform.position;
            Vector3 incoming =
                sourceLight.transform.forward.normalized;

            if (!Physics.Raycast(
                origin,
                incoming,
                out RaycastHit entryHit,
                sourceLight.range,
                beamMask,
                QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (entryHit.collider != glassCollider)
                continue;

            // Air -> glass.
            if (!TryRefract(
                incoming,
                entryHit.normal,
                AIR_IOR,
                refractiveIndex,
                out Vector3 insideDirection))
            {
                continue;
            }

            // Find the opposite glass surface.
            float searchDistance =
                glassCollider.bounds.extents.magnitude * 2f + 0.1f;

            Vector3 searchOrigin =
                entryHit.point + insideDirection * searchDistance;

            if (!Physics.Raycast(
                searchOrigin,
                -insideDirection,
                out RaycastHit reverseExitHit,
                searchDistance * 2f,
                beamMask,
                QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (reverseExitHit.collider != glassCollider)
                continue;

            Vector3 exitPoint = reverseExitHit.point;
            Vector3 exitNormal = reverseExitHit.normal;

            // Glass -> air.
            if (!TryRefract(
                insideDirection,
                exitNormal,
                refractiveIndex,
                AIR_IOR,
                out Vector3 outgoingDirection))
            {
                // Total internal reflection.
                continue;
            }

            Vector3 lightOrigin =
                exitPoint + outgoingDirection * surfaceOffset;

            float remainingRange = Mathf.Max(
                0.1f,
                sourceLight.range - entryHit.distance
            );

            Vector3 beamEnd =
                lightOrigin + outgoingDirection * remainingRange;

            RaycastHit[] targetHits = Physics.RaycastAll(
                lightOrigin,
                outgoingDirection,
                remainingRange,
                beamMask,
                QueryTriggerInteraction.Ignore
            );

            Array.Sort(
                targetHits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit targetHit in targetHits)
            {
                if (targetHit.collider == null)
                    continue;

                if (targetHit.collider == glassCollider)
                    continue;

                beamEnd = targetHit.point;
                break;
            }

            refractedLight.transform.position = lightOrigin;
            refractedLight.transform.rotation = Quaternion.LookRotation(
                outgoingDirection,
                Vector3.up
            );

            // Match the incident flashlight's real light properties.
            refractedLight.color = sourceLight.color;
            refractedLight.intensity =
                sourceLight.intensity * lightIntensityMultiplier;
            refractedLight.range = remainingRange;
            refractedLight.spotAngle = sourceLight.spotAngle;

            refractedLight.shadows = castShadows
                ? LightShadows.Soft
                : LightShadows.None;

            refractedLight.enabled = true;

            // Match the visible beam line color too.
            beamLine.startColor = sourceLight.color;
            beamLine.endColor = sourceLight.color;
            beamLine.startWidth = beamWidth;
            beamLine.endWidth = beamWidth;

            beamLine.SetPosition(0, origin);
            beamLine.SetPosition(1, entryHit.point);
            beamLine.SetPosition(2, exitPoint);
            beamLine.SetPosition(3, beamEnd);
            beamLine.enabled = true;

            break;
        }
    }

    private bool TryRefract(
        Vector3 incident,
        Vector3 surfaceNormal,
        float fromIndex,
        float toIndex,
        out Vector3 refracted)
    {
        incident.Normalize();
        surfaceNormal.Normalize();

        float cosIncident = -Vector3.Dot(
            incident,
            surfaceNormal
        );

        if (cosIncident < 0f)
        {
            surfaceNormal = -surfaceNormal;
            cosIncident = -Vector3.Dot(
                incident,
                surfaceNormal
            );
        }

        float eta = fromIndex / toIndex;

        float discriminant =
            1f - eta * eta *
            (1f - cosIncident * cosIncident);

        if (discriminant < 0f)
        {
            refracted = Vector3.zero;
            return false;
        }

        refracted =
            eta * incident +
            (eta * cosIncident - Mathf.Sqrt(discriminant)) *
            surfaceNormal;

        if (refracted.sqrMagnitude < MIN_DIRECTION)
            return false;

        refracted.Normalize();
        return true;
    }

    private void OnDisable()
    {
        if (beamLine != null)
            beamLine.enabled = false;

        if (refractedLight != null)
            refractedLight.enabled = false;
    }

    private void OnDestroy()
    {
        if (beamMaterial != null)
            Destroy(beamMaterial);

        if (refractedLightSource != null)
            Destroy(refractedLightSource.gameObject);
    }
}
