using UnityEngine;

// Subtle, natural light flicker: a gentle constant shimmer plus occasional
// short dips like a failing bulb. Add to any Light.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Tooltip("How much the light shimmers all the time (0.1 = +/-10%).")]
    [Range(0f, 0.5f)] public float shimmer = 0.06f;
    public float shimmerSpeed = 2.5f;

    [Tooltip("Average dips per second. 0 = never.")]
    [Range(0f, 2f)] public float dipsPerSecond = 0.08f;
    [Tooltip("Brightness during a dip (0 = fully off).")]
    [Range(0f, 1f)] public float dipBrightness = 0.25f;

    Light lightSource;
    float baseIntensity;
    float seed;
    float dipTimer;

    void Start()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * shimmerSpeed) * 2f - 1f;
        float brightness = 1f + noise * shimmer;

        if (dipTimer > 0f)
        {
            dipTimer -= Time.deltaTime;
            // Stutter a little during the dip instead of a clean on/off.
            brightness *= Random.value < 0.7f ? dipBrightness : 1f;
        }
        else if (Random.value < dipsPerSecond * Time.deltaTime)
        {
            dipTimer = Random.Range(0.05f, 0.25f);
        }

        lightSource.intensity = baseIntensity * brightness;
    }
}
