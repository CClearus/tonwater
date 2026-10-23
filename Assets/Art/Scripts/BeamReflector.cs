
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
    public Color beamColor = new Color(1f, 0.9f, 0.65f, 1f);

    [Header("Reflected Light")]
    public bool castShadows = true;
    public float reflectedLightAngleMultiplier = 1f;

    private Camera playerCamera;
    private Transform playerTransform;
    private Rigidbody rb;
    private Collider reflectorCollider;

    private bool isHeld;
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

        CreateBeamLine();
        CreateReflectedLight();
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            FindPlayerCamera();
        }

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

            // Rotate around the vertical axis only.
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
            if (cam.isActiveAndEnabled &&
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
        if (playerCamera == null)
            return;

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
            {
                PickupReflector();
            }
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

        if (playerTransform == null && playerCamera != null)
            playerTransform = playerCamera.transform.root;

        if (reflectorCollider != null)
            reflectorCollider.enabled = false;
    }

    private void DropReflector()
    {
        isHeld = false;

        if (reflectorCollider != null)
            reflectorCollider.enabled = true;

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

        // Keep the held reflector upright.
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
        beamLine.startColor = beamColor;
        beamLine.endColor = beamColor;
        beamLine.enabled = false;

        beamMaterial = new Material(Shader.Find("Sprites/Default"));
        beamLine.material = beamMaterial;
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
            if (source == null)
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

            // Cast along the actual flashlight direction.
            Vector3 incoming = sourceLight.transform.forward.normalized;

            if (!Physics.Raycast(
                sourceLight.transform.position,
                incoming,
                out RaycastHit mirrorHit,
                sourceLight.range,
                beamMask,
                QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            // The first surface hit must be this reflector.
            if (mirrorHit.collider != reflectorCollider)
                continue;

            // Calculate the physical reflection direction.
            Vector3 reflectedDirection = Vector3.Reflect(
                incoming,
                mirrorHit.normal
            );

            // Constrain the reflected beam to the horizontal plane.
            reflectedDirection.y = 0f;

            if (reflectedDirection.sqrMagnitude < 0.001f)
                continue;

            reflectedDirection.Normalize();

            Vector3 beamOrigin =
                mirrorHit.point + reflectedDirection * surfaceOffset;

            // Position and aim the real reflected light.
            reflectedLight.transform.position = beamOrigin;
            reflectedLight.transform.rotation = Quaternion.LookRotation(
                reflectedDirection,
                Vector3.up
            );

            // Match the original flashlight settings.
            reflectedLight.range = sourceLight.range;
            reflectedLight.intensity = sourceLight.intensity;
            reflectedLight.color = sourceLight.color;
            reflectedLight.spotAngle = Mathf.Clamp(
                sourceLight.spotAngle * reflectedLightAngleMultiplier,
                1f,
                179f
            );

            reflectedLight.shadows = castShadows
                ? LightShadows.Soft
                : LightShadows.None;

            reflectedLight.enabled = true;

            // Draw the visible reflected beam.
            Vector3 beamEnd =
                beamOrigin + reflectedDirection * reflectedLight.range;

            if (Physics.Raycast(
                beamOrigin,
                reflectedDirection,
                out RaycastHit targetHit,
                reflectedLight.range,
                beamMask,
                QueryTriggerInteraction.Ignore))
            {
                beamEnd = targetHit.point;
            }

            beamLine.SetPosition(0, beamOrigin);
            beamLine.SetPosition(1, beamEnd);
            beamLine.startColor = sourceLight.color;
            beamLine.endColor = sourceLight.color;
            beamLine.enabled = true;

            UpdateReflectedSensors(
                beamOrigin,
                reflectedDirection,
                reflectedLight.range
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

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider == reflectorCollider ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            LightSensor sensor =
                hit.collider.GetComponentInParent<LightSensor>();

            if (sensor != null)
                newlyLitSensors.Add(sensor);

            // Stop at the first solid object.
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
