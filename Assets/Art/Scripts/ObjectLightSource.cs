
using UnityEngine;

[RequireComponent(typeof(LightSource))]
public class ObjectLightSource : MonoBehaviour
{
    [Header("Light Settings")]
    public LightType lightType = LightType.Spot;
    public Color lightColor = Color.white;
    [Min(0f)] public float intensity = 5f;
    [Min(0.1f)] public float range = 20f;

    [Range(1f, 179f)]
    public float spotAngle = 30f;

    [Header("Direction")]
    [Tooltip("Optional transform that determines where the light points.")]
    public Transform lightDirection;

    [Tooltip("Local direction used when no direction transform is assigned.")]
    public Vector3 localDirection = Vector3.forward;

    [Header("Options")]
    public bool lightOn = true;
    public bool useShadows = true;

    private Light objectLight;
    private LightSource lightSource;

    public Light SourceLight => objectLight;

    private void Awake()
    {
        lightSource = GetComponent<LightSource>();

        // Create a child object to hold the actual Light component.
        GameObject lightObject = new GameObject("Object Light");

        lightObject.transform.SetParent(transform, false);

        objectLight = lightObject.AddComponent<Light>();

        ApplySettings();
    }

    private void LateUpdate()
    {
        if (objectLight == null)
            return;

        ApplySettings();
    }

    private void ApplySettings()
    {
        Transform directionTransform =
            lightDirection != null ? lightDirection : transform;

        objectLight.type = lightType;
        objectLight.color = lightColor;
        objectLight.intensity = intensity;
        objectLight.range = range;
        objectLight.spotAngle = spotAngle;
        objectLight.shadows = useShadows
            ? LightShadows.Soft
            : LightShadows.None;

        objectLight.enabled = lightOn;

        // Keep the light at the object's position.
        objectLight.transform.position = transform.position;

        // Point the light in the selected direction.
        Vector3 direction = lightDirection != null
            ? directionTransform.forward
            : transform.TransformDirection(localDirection.normalized);

        if (direction.sqrMagnitude > 0.001f)
        {
            objectLight.transform.rotation =
                Quaternion.LookRotation(direction, transform.up);
        }
    }

    public void TurnOn()
    {
        lightOn = true;

        if (objectLight != null)
            objectLight.enabled = true;
    }

    public void TurnOff()
    {
        lightOn = false;

        if (objectLight != null)
            objectLight.enabled = false;
    }

    public void SetColor(Color newColor)
    {
        lightColor = newColor;

        if (objectLight != null)
            objectLight.color = newColor;
    }
}
