using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Exit door for a level. Reads the level's GameObjectDataSO (found by scene
// name, or assigned), shows one cell per required sensor on its StatusScreen,
// and opens when all of them are on. Every player computes this from the
// synced sensor states, so the sign and door match for everyone.
//   Locked:   dim white door icon, red edge glow.
//   Unlocked: (all sensors on) icon and edge turn blue and pulse.
//   Open:     the door parts slide away and light spills out from behind.
public class ExitDoor : MonoBehaviour
{
    [Serializable]
    public class MovingPart
    {
        public Transform part;
        [Tooltip("How far this part moves when the door opens (local space).")]
        public Vector3 openOffset;
        [NonSerialized] public Vector3 closedPosition;
    }

    [Tooltip("Leave empty to find the data for the current scene in Resources/LevelData.")]
    [SerializeField] GameObjectDataSO levelData;

    [Header("Sign")]
    [Tooltip("The screen showing one cell per sensor (in the level data's order).")]
    [SerializeField] StatusScreen screen;

    [Header("Door")]
    [SerializeField] List<MovingPart> parts = new();
    [SerializeField] float openSeconds = 3f;
    [SerializeField] AudioClip unlockSound;
    [SerializeField] AudioClip openSound;
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    [Header("Look")]
    [Tooltip("Door panels using Dacha/DoorPanel (their icon changes color).")]
    [SerializeField] Renderer[] iconPanels;
    [SerializeField, ColorUsage(false, true)] Color lockedIconColor = new Color(1.4f, 1.45f, 1.5f);
    [SerializeField, ColorUsage(false, true)] Color unlockedIconColor = new Color(0.15f, 0.7f, 2.4f);
    [Tooltip("Glow around the door opening (uses Dacha/Glow).")]
    [SerializeField] Renderer edgeGlow;
    [SerializeField, ColorUsage(false, true)] Color lockedEdgeColor = new Color(1.6f, 0.03f, 0.03f);
    [SerializeField, ColorUsage(false, true)] Color unlockedEdgeColor = new Color(0.15f, 0.7f, 2.4f);
    [Tooltip("Wide soft glow behind the edge line (uses Dacha/GlowSoft).")]
    [SerializeField] Renderer edgeHalo;
    [SerializeField] float edgeHaloStrength = 0.3f;
    [Tooltip("Light at the door shape that glows onto the floor; follows the icon color.")]
    [SerializeField] Light shapeLight;
    [SerializeField] float shapeLightIntensity = 25f;
    [Tooltip("Light behind the door that spills out as it opens (keep it at 0 intensity in the scene).")]
    [SerializeField] Light spillLight;
    [SerializeField] float spillLightIntensity = 250f;
    [Tooltip("Glowing backdrop behind the door, revealed as it opens.")]
    [SerializeField] Renderer beyondGlow;

    static readonly int IconColorId = Shader.PropertyToID("_IconColor");
    static readonly int GlowColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    readonly List<SensorController> sensors = new();
    readonly List<Material> iconMaterials = new();
    Material edgeMaterial, haloMaterial, beyondMaterial;
    float openAmount;
    float unlockBlend;   // 0 locked look -> 1 unlocked look
    float unlockTime = -99f;
    bool isUnlocked, isOpen;
    float allOnTimer;

    public bool IsOpen => isOpen;
    public bool IsUnlocked => isUnlocked;

    void Start()
    {
        if (levelData == null) levelData = GameObjectDataSO.ForScene(SceneManager.GetActiveScene().name);
        if (levelData == null)
        {
            Debug.LogWarning($"ExitDoor: no level data for scene '{SceneManager.GetActiveScene().name}'. " +
                             "Create one in Resources/LevelData named after the scene.", this);
            enabled = false;
            return;
        }

        foreach (var p in parts)
            if (p.part != null) p.closedPosition = p.part.localPosition;

        foreach (var r in iconPanels)
            if (r != null) { var m = new Material(r.sharedMaterial); r.sharedMaterial = m; iconMaterials.Add(m); }
        if (edgeGlow != null) { edgeMaterial = new Material(edgeGlow.sharedMaterial); edgeGlow.sharedMaterial = edgeMaterial; }
        if (edgeHalo != null) { haloMaterial = new Material(edgeHalo.sharedMaterial); edgeHalo.sharedMaterial = haloMaterial; }
        if (beyondGlow != null) { beyondMaterial = new Material(beyondGlow.sharedMaterial); beyondGlow.sharedMaterial = beyondMaterial; }
        if (spillLight != null) spillLight.intensity = 0f;

        foreach (var id in levelData.requiredSensorIds)
        {
            var sensor = SensorController.Find(id);
            if (sensor == null) Debug.LogWarning($"ExitDoor: no sensor with ID '{id}' in this scene.", this);
            sensors.Add(sensor);
        }

        if (screen != null)
        {
            screen.SetCellCount(sensors.Count);
            for (int i = 0; i < sensors.Count; i++) screen.SetDone(i, IsSensorOn(i), instant: true);
        }
        ApplyLook();
    }

    void Update()
    {
        int onCount = 0;
        for (int i = 0; i < sensors.Count; i++)
        {
            bool on = IsSensorOn(i);
            if (on) onCount++;
            if (screen != null && screen.IsDone(i) != on) screen.SetDone(i, on);
        }

        bool allOn = sensors.Count > 0 && onCount == sensors.Count;
        allOnTimer = allOn ? allOnTimer + Time.deltaTime : 0f;

        if (allOn && !isUnlocked) SetUnlocked(true);
        else if (!allOn && isUnlocked && !(isOpen && levelData.doorStaysOpen)) SetUnlocked(false);

        if (!isOpen && allOn && allOnTimer >= levelData.doorOpenDelay) SetOpen(true);
        else if (isOpen && !allOn && !levelData.doorStaysOpen) SetOpen(false);

        openAmount = Mathf.MoveTowards(openAmount, isOpen ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, openSeconds));
        float eased = Mathf.SmoothStep(0f, 1f, openAmount);
        foreach (var p in parts)
            if (p.part != null) p.part.localPosition = p.closedPosition + p.openOffset * eased;

        unlockBlend = Mathf.MoveTowards(unlockBlend, isUnlocked ? 1f : 0f, Time.deltaTime * 2f);
        ApplyLook();
    }

    bool IsSensorOn(int i) => sensors[i] != null && sensors[i].IsOn;

    void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        if (unlocked)
        {
            unlockTime = Time.time;
            if (unlockSound != null) AudioSource.PlayClipAtPoint(unlockSound, transform.position);
        }
    }

    void SetOpen(bool open)
    {
        isOpen = open;
        if (open && openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        (open ? onOpened : onClosed).Invoke();
    }

    void ApplyLook()
    {
        // Locked: slow faint breathing. Unlocked: one bright flash, then a gentle pulse.
        float sinceUnlock = Time.time - unlockTime;
        float flash = isUnlocked ? Mathf.Exp(-sinceUnlock * 3f) * 1.5f : 0f;
        float pulse = isUnlocked
            ? 1f + 0.25f * Mathf.Sin(Time.time * 3f) + flash
            : 0.85f + 0.15f * Mathf.Sin(Time.time * 1.2f);

        Color icon = Color.Lerp(lockedIconColor, unlockedIconColor, unlockBlend) * pulse;
        foreach (var m in iconMaterials) m.SetColor(IconColorId, icon);

        Color edge = Color.Lerp(lockedEdgeColor, unlockedEdgeColor, unlockBlend) * pulse;
        if (edgeMaterial != null) edgeMaterial.SetColor(GlowColorId, edge);
        if (haloMaterial != null) haloMaterial.SetColor(GlowColorId, edge * edgeHaloStrength);
        if (shapeLight != null)
        {
            Color c = icon / Mathf.Max(0.001f, icon.maxColorComponent);
            shapeLight.color = c;
            shapeLight.intensity = shapeLightIntensity * pulse * (1f - Mathf.SmoothStep(0f, 1f, openAmount));
        }

        float eased = Mathf.SmoothStep(0f, 1f, openAmount);
        if (spillLight != null) spillLight.intensity = spillLightIntensity * eased;
        if (beyondMaterial != null) beyondMaterial.SetFloat(IntensityId, eased);
    }

    void OnDestroy()
    {
        foreach (var m in iconMaterials) Destroy(m);
        if (edgeMaterial != null) Destroy(edgeMaterial);
        if (haloMaterial != null) Destroy(haloMaterial);
        if (beyondMaterial != null) Destroy(beyondMaterial);
    }
}
