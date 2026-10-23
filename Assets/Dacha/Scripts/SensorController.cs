using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// The objective sensor. Off = red, LEDs blink slowly (standby).
// On = rings rotate in sequence (outer first), everything shifts to blue, LEDs
// stay lit. Turning off plays it back. Synced to all players: only the host
// changes the state (call SetOn), clients follow.
[RequireComponent(typeof(AudioSource))]
public class SensorController : NetworkBehaviour
{
    [Serializable]
    public class RingGroup
    {
        public string label = "Ring";
        [Tooltip("Parts that rotate together (and change color together).")]
        public Transform[] parts;
        [Tooltip("Seconds after switching when this ring starts moving.")]
        public float startTime;
        [Tooltip("Seconds after switching when this ring stops.")]
        public float endTime = 5f;
        [Tooltip("Degrees turned when switching on (turns back when off).")]
        public float angle = 180f;

        [NonSerialized] public float from, to, current;
        [NonSerialized] public readonly List<(Material mat, Color baseOff, Color emisOff)> colors = new();
    }

    [Header("State")]
    [Tooltip("Unique name the level data uses to find this sensor (e.g. Sensor_A).")]
    [SerializeField] string sensorId;
    [Tooltip("Tick in Play mode to test (host/offline only).")]
    [SerializeField] bool isOn;

    [Header("Rings (outer first)")]
    [SerializeField] RingGroup[] rings;
    [Tooltip("Center the rings rotate around (the crystal).")]
    [SerializeField] Transform pivot;

    [Header("Status LEDs")]
    [SerializeField] Renderer[] leds;
    [SerializeField] Light statusLight;
    [Tooltip("Off/standby blink: seconds per blink cycle.")]
    [SerializeField] float blinkPeriod = 1.6f;

    [Header("Colors")]
    [Tooltip("How far red is shifted toward blue when on (hue 0.6 = blue).")]
    [SerializeField] float onHue = 0.6f;

    [Header("Sounds")]
    [SerializeField] AudioClip turnOnSound;
    [SerializeField] AudioClip turnOffSound;
    [Tooltip("Optional loop that plays while the sensor is on.")]
    [SerializeField] AudioClip onLoop;

    readonly NetworkVariable<bool> netOn = new(false);

    public bool IsOn => IsSpawned ? netOn.Value : isOn;
    public string SensorId => sensorId;
    public event Action<bool> Switched;

    static readonly Dictionary<string, SensorController> registry = new();

    // The sensor in the loaded scene with this ID, or null.
    public static SensorController Find(string id) =>
        id != null && registry.TryGetValue(id, out var sensor) ? sensor : null;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    AudioSource audioSource;
    Vector3 pivotLocal;
    Vector3 axisLocal;
    readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> rest = new();
    readonly List<(Material mat, Color emisOff)> ledColors = new();
    Color statusLightOff;
    float statusIntensity;
    float timer;
    bool appliedOn;
    bool lastInspectorOn;

    void Awake()
    {
        if (!string.IsNullOrEmpty(sensorId))
        {
            if (registry.TryGetValue(sensorId, out var other) && other != null && other != this)
                Debug.LogWarning($"Two sensors use the ID '{sensorId}'.", this);
            registry[sensorId] = this;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        pivotLocal = pivot != null ? transform.InverseTransformPoint(GetCenter(pivot)) : Vector3.zero;
        axisLocal = Vector3.forward;

        foreach (var ring in rings)
            foreach (var part in ring.parts)
            {
                if (part == null) continue;
                rest[part] = (part.localPosition, part.localRotation);
                CollectRedMaterials(part, ring.colors);
            }

        foreach (var led in leds)
            if (led != null)
                foreach (var m in led.materials)
                    if (m.HasProperty(EmissionId)) ledColors.Add((m, m.GetColor(EmissionId)));

        if (statusLight != null)
        {
            statusLightOff = statusLight.color;
            statusIntensity = statusLight.intensity;
        }

        lastInspectorOn = isOn;
        appliedOn = isOn;
        foreach (var ring in rings) ring.current = ring.from = ring.to = isOn ? 1f : 0f;
        timer = 999f;
        ApplyVisuals();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) netOn.Value = isOn;
        netOn.OnValueChanged += OnNetChanged;
        if (netOn.Value != appliedOn) StartSwitch(netOn.Value, playSound: false);
    }

    public override void OnNetworkDespawn() => netOn.OnValueChanged -= OnNetChanged;

    public override void OnDestroy()
    {
        if (!string.IsNullOrEmpty(sensorId) && registry.TryGetValue(sensorId, out var s) && s == this)
            registry.Remove(sensorId);
        base.OnDestroy();
    }

    void OnNetChanged(bool previous, bool current) => StartSwitch(current, playSound: true);

    // Turn the sensor on/off. In a network session only the host can do this.
    public void SetOn(bool on)
    {
        isOn = lastInspectorOn = on;
        if (IsSpawned)
        {
            if (IsServer) netOn.Value = on;
            else Debug.LogWarning("Only the host can switch the sensor.", this);
        }
        else if (on != appliedOn)
        {
            StartSwitch(on, playSound: true);
        }
    }

    public void Toggle() => SetOn(!IsOn);

    void StartSwitch(bool on, bool playSound)
    {
        appliedOn = on;
        timer = 0f;
        foreach (var ring in rings)
        {
            ring.from = ring.current;
            ring.to = on ? 1f : 0f;
        }

        if (playSound)
        {
            var clip = on ? turnOnSound : turnOffSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
        if (onLoop != null)
        {
            audioSource.clip = onLoop;
            audioSource.loop = true;
            if (on) audioSource.PlayDelayed(turnOnSound != null ? turnOnSound.length * 0.5f : 0f);
            else audioSource.Stop();
        }
        Switched?.Invoke(on);
    }

    void Update()
    {
        // Inspector checkbox for testing.
        if (isOn != lastInspectorOn)
        {
            lastInspectorOn = isOn;
            SetOn(isOn);
        }

        timer += Time.deltaTime;
        foreach (var ring in rings)
        {
            float t = Mathf.InverseLerp(ring.startTime, ring.endTime, timer);
            ring.current = Mathf.Lerp(ring.from, ring.to, Mathf.SmoothStep(0f, 1f, t));
        }
        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        foreach (var ring in rings)
        {
            var spin = Quaternion.AngleAxis(ring.current * ring.angle, axisLocal);
            foreach (var part in ring.parts)
            {
                if (part == null || !rest.TryGetValue(part, out var r)) continue;
                part.localPosition = spin * (r.pos - pivotLocal) + pivotLocal;
                part.localRotation = spin * r.rot;
            }
            foreach (var (mat, baseOff, emisOff) in ring.colors)
            {
                if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, Shift(baseOff, ring.current));
                mat.SetColor(EmissionId, Shift(emisOff, ring.current));
            }
        }

        // LEDs follow the outer ring's color; blink while off.
        float onAmount = rings.Length > 0 ? rings[0].current : (appliedOn ? 1f : 0f);
        float blink = Mathf.Lerp(StandbyBlink(), 1f, onAmount);
        foreach (var (mat, emisOff) in ledColors)
            mat.SetColor(EmissionId, Shift(emisOff, onAmount) * blink);

        if (statusLight != null)
        {
            statusLight.color = Shift(statusLightOff, onAmount);
            statusLight.intensity = statusIntensity * blink;
        }
    }

    // Short bright blink once per period, dark the rest of the time.
    float StandbyBlink()
    {
        float phase = Mathf.Repeat(Time.time / blinkPeriod, 1f);
        return phase < 0.12f ? 1f : 0.08f;
    }

    // Shifts a red/orange color toward blue, keeping its brightness and the
    // differences between colors (orange becomes a lighter azure, etc.).
    Color Shift(Color c, float amount)
    {
        if (amount <= 0f) return c;
        float max = c.maxColorComponent;
        if (max <= 0f) return c;
        Color.RGBToHSV(c / max, out float h, out float s, out float v);
        float blueHue = onHue - (h > 0.5f ? h - 1f : h);
        Color blue = Color.HSVToRGB(Mathf.Repeat(blueHue, 1f), s, v) * max;
        blue.a = c.a;
        return Color.Lerp(c, blue, amount);
    }

    static void CollectRedMaterials(Transform part, List<(Material, Color, Color)> into)
    {
        var r = part.GetComponent<Renderer>();
        if (r == null) return;
        foreach (var m in r.materials) // instances, so the shared assets stay red
        {
            if (!m.HasProperty(EmissionId) || !m.IsKeywordEnabled("_EMISSION")) continue;
            Color baseColor = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            into.Add((m, baseColor, m.GetColor(EmissionId)));
        }
    }

    static Vector3 GetCenter(Transform t)
    {
        var r = t.GetComponent<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }
}
