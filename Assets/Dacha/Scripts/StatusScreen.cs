using UnityEngine;

// Drives the Dacha/StatusScreen material: one cell per sensor, each showing a
// red cross (not done) or blue tick (done), switching with a power flicker.
[RequireComponent(typeof(Renderer))]
public class StatusScreen : MonoBehaviour
{
    public const int MaxCells = 8;

    [SerializeField] float flickerTime = 0.45f;
    [SerializeField] AudioClip doneSound;
    [SerializeField] AudioClip undoneSound;

    static readonly int CellCountId = Shader.PropertyToID("_CellCount");
    static readonly int Done0Id = Shader.PropertyToID("_Done0");
    static readonly int Done1Id = Shader.PropertyToID("_Done1");
    static readonly int Power0Id = Shader.PropertyToID("_Power0");
    static readonly int Power1Id = Shader.PropertyToID("_Power1");

    readonly bool[] done = new bool[MaxCells];
    readonly float[] timers = new float[MaxCells];
    readonly float[] power = new float[MaxCells];
    Material material;
    int cellCount = 1;

    public int CellCount => cellCount;

    void Awake()
    {
        var rend = GetComponent<Renderer>();
        material = new Material(rend.sharedMaterial); // per-door copy
        rend.sharedMaterial = material;
        for (int i = 0; i < MaxCells; i++) { timers[i] = -1f; power[i] = 1f; }
        cellCount = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(CellCountId)), 1, MaxCells);
        Push();
    }

    public void SetCellCount(int count)
    {
        cellCount = Mathf.Clamp(count, 1, MaxCells);
        if (count > MaxCells) Debug.LogWarning($"StatusScreen shows at most {MaxCells} cells.", this);
        material.SetFloat(CellCountId, cellCount);
    }

    public bool IsDone(int cell) => cell >= 0 && cell < MaxCells && done[cell];

    public void SetDone(int cell, bool value, bool instant = false)
    {
        if (cell < 0 || cell >= MaxCells || done[cell] == value) return;
        done[cell] = value;
        timers[cell] = instant ? -1f : 0f;
        power[cell] = 1f;
        if (!instant)
        {
            var clip = value ? doneSound : undoneSound;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
        }
        Push();
    }

    void Update()
    {
        bool changed = false;
        for (int i = 0; i < cellCount; i++)
        {
            if (timers[i] < 0f) continue;
            timers[i] += Time.deltaTime;
            float t = Mathf.Clamp01(timers[i] / flickerTime);
            // Stuttering power-up, settling to fully on.
            power[i] = t >= 1f ? 1f : (Random.value < t ? 1f : Random.Range(0f, 0.25f));
            if (t >= 1f) timers[i] = -1f;
            changed = true;
        }
        if (changed) Push();
    }

    void Push()
    {
        material.SetVector(Done0Id, new Vector4(D(0), D(1), D(2), D(3)));
        material.SetVector(Done1Id, new Vector4(D(4), D(5), D(6), D(7)));
        material.SetVector(Power0Id, new Vector4(power[0], power[1], power[2], power[3]));
        material.SetVector(Power1Id, new Vector4(power[4], power[5], power[6], power[7]));
    }

    float D(int i) => done[i] ? 1f : 0f;

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
