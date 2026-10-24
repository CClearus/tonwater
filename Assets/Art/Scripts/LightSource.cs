
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> All =
        new List<LightSource>();

    [Header("Light Identification")]
    [Tooltip("Enable this only for the player's flashlight.")]
    public bool isPlayerFlashlight = false;

    [Header("Visible Beam Line")]
    public bool drawLine = true;

    [Min(0.1f)]
    public float lineLength = 20f;

    [Min(0.001f)]
    public float lineWidth = 0.025f;

    public Color lineColor = Color.white;

    [Header("Intersection Debug")]
    public bool showDebugLine = true;

    public Light SourceLight { get; private set; }

    private LineRenderer lineRenderer;
    private static Material sharedLineMaterial;

    private void Awake()
    {
        SourceLight = GetComponent<Light>();

        CreateLineRenderer();
        UpdateLine();
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private void LateUpdate()
    {
        if (SourceLight == null)
            SourceLight = GetComponent<Light>();

        UpdateLine();
    }

    private void CreateLineRenderer()
    {
        GameObject lineObject = new GameObject("Visible Light Beam Line");
        lineObject.transform.SetParent(transform, false);

        lineRenderer = lineObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;

        if (sharedLineMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
                sharedLineMaterial = new Material(shader);
        }

        if (sharedLineMaterial != null)
            lineRenderer.sharedMaterial = sharedLineMaterial;

        lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        lineRenderer.receiveShadows = false;
    }

    private void UpdateLine()
    {
        if (lineRenderer == null || SourceLight == null)
            return;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        Color beamColor =
            lineColor == Color.white && SourceLight.color != Color.white
                ? SourceLight.color
                : lineColor;

        lineRenderer.startColor = beamColor;
        lineRenderer.endColor = beamColor;

        bool visible =
            drawLine &&
            SourceLight.enabled &&
            SourceLight.gameObject.activeInHierarchy &&
            SourceLight.type == LightType.Spot;

        lineRenderer.enabled = visible;

        if (!visible)
            return;

        Vector3 origin = SourceLight.transform.position;
        Vector3 direction = SourceLight.transform.forward.normalized;

        float length = Mathf.Min(
            lineLength,
            SourceLight.range
        );

        Vector3 end = origin + direction * length;

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, end);

        if (showDebugLine)
            Debug.DrawLine(origin, end, beamColor);
    }

    private void Start()
    {
        Debug.Log(
            "[LightSource] " + name +
            " | Player flashlight: " + isPlayerFlashlight +
            " | Light found: " + (SourceLight != null) +
            " | Registered: " + All.Contains(this),
            this
        );
    }
}
