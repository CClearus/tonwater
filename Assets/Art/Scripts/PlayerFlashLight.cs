
using UnityEngine;

public class PlayerFlashlight : MonoBehaviour
{
    public Camera playerCamera;
    public float range = 50f;
    public float angle = 10f;
    public float intensity = 100f;
    public Color lightColor = new Color(1f, 0.85f, 0.6f);

    private Light lighter;

    void Start()
    {
        // Only players tagged Lighter get a light
        if (!CompareTag("Lighter"))
            return;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogWarning("Player camera not found!");
            return;
        }

        GameObject lightObject = new GameObject("Lighter Light");

        lighter = lightObject.AddComponent<Light>();
        lighter.type = LightType.Spot;
        lighter.range = range;
        lighter.spotAngle = angle;
        lighter.intensity = intensity;
        lighter.color = lightColor;
        lighter.shadows = LightShadows.Soft;

        lightObject.AddComponent<LightSource>();

        lightObject.transform.SetParent(playerCamera.transform);
        lightObject.transform.localPosition = Vector3.zero;
        lightObject.transform.localRotation = Quaternion.identity;
    }
}
