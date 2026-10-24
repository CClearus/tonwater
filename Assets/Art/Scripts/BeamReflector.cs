
using System;
using System.Collections.Generic;
using UnityEngine;

public class BeamReflector : MonoBehaviour
{
    [Header("Pickup")]
    public float pickupRange = 3f;
    public float holdDistance = 1.5f;
    public KeyCode pickupKey = KeyCode.E;

    [Header("Rotation")]
    public float rotateSpeed = 100f;

    [Header("Reflection")]
    public float surfaceOffset = 0.03f;
    public LayerMask beamMask = ~0;

    [Header("Visible Beam")]
    public float beamWidth = 0.035f;

    [Header("Reflected Light")]
    public bool castShadows = true;
    public float reflectedLightAngleMultiplier = 1f;

    private Camera playerCamera;
    private Transform playerTransform;
    private Rigidbody rb;
    private Collider reflectorCollider;

    private bool isHeld;
    private bool originalIsTrigger;

    private LineRenderer beamLine;
    private Light reflectedLight;
    private LightSource reflectedLightSource;
    private Material beamMaterial;

    private readonly HashSet<LightSensor> activeSensors =
        new HashSet<LightSensor>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        reflectorCollider = GetComponent<Collider>();

        if (reflectorCollider != null)
            originalIsTrigger = reflectorCollider.isTrigger;

        CreateBeamLine();
        CreateReflectedLight();
    }

    private void Update()
    {
        if (playerCamera == null)
            FindPlayerCamera();

        if (playerCamera != null && Input.GetKeyDown(pickupKey))
        {
            if (isHeld)
                DropReflector();
            else
                TryPickupReflector();
        }

        if (isHeld)
        {
            HoldReflector();

            float rotateInput = 0f;

            if (Input.GetKey(KeyCode.Q))
                rotateInput = -1f;

            if (Input.GetKey(KeyCode.R))
                rotateInput = 1f;

            transform.Rotate(
                Vector3.up,
                rotateInput * rotateSpeed * Time.deltaTime,
                Space.World
            );
        }

        ReflectBeam();
    }

    private void FindPlayerCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsSortMode.None
        );

        foreach (Camera cam in cameras)
        {
            if (cam != null &&
                cam.isActiveAndEnabled &&
                cam.CompareTag("MainCamera"))
            {
                playerCamera = cam;
                playerTransform = cam.transform.root;
                return;
            }
        }

        if (Camera.main != null)
        {
            playerCamera = Camera.main;
            playerTransform = playerCamera.transform.root;
        }
    }

    private void TryPickupReflector()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            pickupRange,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            BeamReflector reflector =
                hit.collider.GetComponentInParent<BeamReflector>();

            if (reflector == this)
                PickupReflector();
        }
    }

    private void PickupReflector()
    {
        isHeld = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Keep the collider available for flashlight raycasts.
        if (reflectorCollider != null)
        {
            reflectorCollider.enabled = true;
            reflectorCollider.isTrigger = true;
        }
    }

    private void DropReflector()
    {
        isHeld = false;

        if (reflectorCollider != null)
        {
            reflectorCollider.isTrigger = originalIsTrigger;
            reflectorCollider.enabled = true;
        }

        if (rb != null)
            rb.isKinematic = false;
    }

    private void HoldReflector()
    {
        if (playerCamera == null)
            return;

        transform.position =
            playerCamera.transform.position +
            playerCamera.transform.forward * holdDistance;

        Vector3 flatForward = Vector3.ProjectOnPlane(
            playerCamera.transform.forward,
            Vector3.up
        );

        if (flatForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                flatForward.normalized,
                Vector3.up
            );
        }
    }

    private void CreateBeamLine()
    {
        GameObject lineObject = new GameObject("Reflected Beam Line");
        lineObject.transform.SetParent(transform);
        lineObject.transform.localPosition = Vector3.zero;

        beamLine = lineObject.AddComponent<LineRenderer>();
        beamLine.useWorldSpace = true;
        beamLine.positionCount = 2;
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

    private void CreateReflectedLight()
    {
        GameObject lightObject = new GameObject("Reflected Light");

        reflectedLight = lightObject.AddComponent<Light>();
        reflectedLight.type = LightType.Spot;
        reflectedLight.enabled = false;

        reflectedLightSource = lightObject.AddComponent<LightSource>();
    }

    private void ReflectBeam()
    {
        if (beamLine == null || reflectedLight == null)
            return;

        bool foundReflection = false;

        foreach (LightSource source in LightSource.All)
        {
            if (source == null || source == reflectedLightSource)
                continue;

            Light sourceLight = source.SourceLight;

            if (sourceLight == null ||
                sourceLight == reflectedLight ||
                !sourceLight.enabled ||
                sourceLight.type != LightType.Spot ||
                sourceLight.intensity <= 0f)
            {
                continue;
            }

            Vector3 incoming =
                sourceLight.transform.forward.normalized;

            RaycastHit mirrorHit = default(RaycastHit);
            bool hitReflector = false;

            RaycastHit[] mirrorHits = Physics.RaycastAll(
                sourceLight.transform.position,
                incoming,
                sourceLight.range,
                beamMask,
                QueryTriggerInteraction.Collide
            );

            Array.Sort(
                mirrorHits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit hit in mirrorHits)
            {
                if (hit.collider == null)
                    continue;

                // Ignore the player carrying the reflector.
                if (playerTransform != null &&
                    hit.collider.transform.IsChildOf(playerTransform))
                {
                    continue;
                }

                if (hit.collider == reflectorCollider)
                {
                    mirrorHit = hit;
                    hitReflector = true;
                    break;
                }

                if (!hit.collider.isTrigger)
                    break;
            }

            if (!hitReflector)
                continue;

            Vector3 reflectedDirection = Vector3.Reflect(
                incoming,
                mirrorHit.normal
            );

            // Keep the reflected beam horizontal.
            reflectedDirection.y = 0f;

            if (reflectedDirection.sqrMagnitude < 0.001f)
                continue;

            reflectedDirection.Normalize();

            Vector3 beamOrigin =
                mirrorHit.point +
                reflectedDirection * surfaceOffset;

            float remainingRange = Mathf.Max(
                0.1f,
                sourceLight.range - mirrorHit.distance
            );

            reflectedLight.transform.position = beamOrigin;
            reflectedLight.transform.rotation = Quaternion.LookRotation(
                reflectedDirection,
                Vector3.up
            );

            // Copy the incident flashlight's actual light properties.
            reflectedLight.color = sourceLight.color;
            reflectedLight.intensity = sourceLight.intensity;
            reflectedLight.range = remainingRange;

            reflectedLight.spotAngle = Mathf.Clamp(
                sourceLight.spotAngle * reflectedLightAngleMultiplier,
                1f,
                179f
            );

            reflectedLight.shadows = castShadows
                ? LightShadows.Soft
                : LightShadows.None;

            reflectedLight.enabled = true;

            Vector3 beamEnd =
                beamOrigin + reflectedDirection * remainingRange;

            RaycastHit[] targetHits = Physics.RaycastAll(
                beamOrigin,
                reflectedDirection,
                remainingRange,
                beamMask,
                QueryTriggerInteraction.Collide
            );

            Array.Sort(
                targetHits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit targetHit in targetHits)
            {
                if (targetHit.collider == null)
                    continue;

                if (targetHit.collider == reflectorCollider ||
                    targetHit.collider.transform.IsChildOf(transform))
                    continue;

                if (playerTransform != null &&
                    targetHit.collider.transform.IsChildOf(playerTransform))
                    continue;

                beamEnd = targetHit.point;

                if (!targetHit.collider.isTrigger)
                    break;
            }

            // Match the visible line to the incident flashlight color.
            beamLine.startColor = sourceLight.color;
            beamLine.endColor = sourceLight.color;
            beamLine.startWidth = beamWidth;
            beamLine.endWidth = beamWidth;

            beamLine.SetPosition(0, beamOrigin);
            beamLine.SetPosition(1, beamEnd);
            beamLine.enabled = true;

            UpdateReflectedSensors(
                beamOrigin,
                reflectedDirection,
                remainingRange
            );

            foundReflection = true;
            break;
        }

        if (!foundReflection)
        {
            beamLine.enabled = false;
            reflectedLight.enabled = false;
            ClearReflectedSensors();
        }
    }

    private void UpdateReflectedSensors(
        Vector3 origin,
        Vector3 direction,
        float range)
    {
        HashSet<LightSensor> newlyLitSensors =
            new HashSet<LightSensor>();

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            range,
            beamMask,
            QueryTriggerInteraction.Collide
        );

        Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider == reflectorCollider ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            if (playerTransform != null &&
                hit.collider.transform.IsChildOf(playerTransform))
                continue;

            LightSensor sensor =
                hit.collider.GetComponentInParent<LightSensor>();

            if (sensor != null)
                newlyLitSensors.Add(sensor);

            if (!hit.collider.isTrigger)
                break;
        }

        foreach (LightSensor sensor in activeSensors)
        {
            if (sensor != null && !newlyLitSensors.Contains(sensor))
                sensor.SetReflectedIllumination(this, false);
        }

        foreach (LightSensor sensor in newlyLitSensors)
        {
            if (sensor != null)
                sensor.SetReflectedIllumination(this, true);
        }

        activeSensors.Clear();

        foreach (LightSensor sensor in newlyLitSensors)
            activeSensors.Add(sensor);
    }

    private void ClearReflectedSensors()
    {
        foreach (LightSensor sensor in activeSensors)
        {
            if (sensor != null)
                sensor.SetReflectedIllumination(this, false);
        }

        activeSensors.Clear();
    }

    private void OnDisable()
    {
        if (beamLine != null)
            beamLine.enabled = false;

        if (reflectedLight != null)
            reflectedLight.enabled = false;

        ClearReflectedSensors();
    }

    private void OnDestroy()
    {
        if (beamMaterial != null)
            Destroy(beamMaterial);

        if (reflectedLightSource != null)
            Destroy(reflectedLightSource.gameObject);
    }
}
