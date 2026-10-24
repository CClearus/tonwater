
using UnityEngine;

public class SteamLiftSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject steamLiftPrefab;

    [Header("Intersection")]
    [Min(0.01f)]
    public float intersectionTolerance = 0.5f;

    public float spawnOffset = 0f;
    public bool spawnOnlyOnce = true;

    [Header("Debug")]
    public bool showDebug = true;
    public bool showStatusOnScreen = true;

    private GameObject spawnedLift;
    private string status = "Waiting for Start...";
    private float nextDiagnosticTime;

    private void Awake()
    {
        status = "Spawner Awake called";

        Debug.Log(
            "[SteamLiftSpawner] AWAKE: Script is running.",
            this
        );
    }

    private void Start()
    {
        Debug.Log(
            "[SteamLiftSpawner] START. Prefab assigned: " +
            (steamLiftPrefab != null) +
            ". Registered lights: " + LightSource.All.Count,
            this
        );

        if (steamLiftPrefab == null)
        {
            status = "ERROR: Assign Steam Lift Prefab in Inspector.";
            Debug.LogError("[SteamLiftSpawner] " + status, this);
        }
        else
        {
            status = "Running. Searching for flashlight...";
        }
    }

    private void Update()
    {
        if (steamLiftPrefab == null)
        {
            status = "ERROR: Steam Lift Prefab is empty.";
            return;
        }

        if (spawnOnlyOnce && spawnedLift != null)
        {
            status = "Lift already spawned.";
            return;
        }

        LightSource flashlightSource = null;
        int registeredCount = 0;
        int usableSpotlights = 0;

        foreach (LightSource source in LightSource.All)
        {
            if (source == null || source.SourceLight == null)
                continue;

            registeredCount++;

            Light light = source.SourceLight;

            if (light.enabled &&
                light.gameObject.activeInHierarchy &&
                light.type == LightType.Spot &&
                light.intensity > 0f)
            {
                usableSpotlights++;
            }

            if (source.isPlayerFlashlight &&
                light.enabled &&
                light.gameObject.activeInHierarchy &&
                light.type == LightType.Spot)
            {
                flashlightSource = source;
            }
        }

        if (flashlightSource == null)
        {
            status = "No active registered player flashlight.";

            LogStatusPeriodically(
                registeredCount,
                usableSpotlights
            );

            return;
        }

        Light flashlight = flashlightSource.SourceLight;

        bool foundIntersection = false;
        Vector3 intersection = Vector3.zero;
        string failureReason = "No other usable spotlight found.";

        foreach (LightSource source in LightSource.All)
        {
            if (source == null ||
                source == flashlightSource ||
                source.SourceLight == null)
                continue;

            Light other = source.SourceLight;

            if (!other.enabled ||
                !other.gameObject.activeInHierarchy ||
                other.type != LightType.Spot ||
                other.intensity <= 0f)
                continue;

            if (!ClosestPointsOnRays(
                flashlight.transform.position,
                flashlight.transform.forward.normalized,
                other.transform.position,
                other.transform.forward.normalized,
                out Vector3 p1,
                out Vector3 p2))
            {
                failureReason =
                    "Lines are parallel or nearly parallel.";
                continue;
            }

            float separation = Vector3.Distance(p1, p2);

            if (showDebug)
            {
                Debug.DrawLine(
                    flashlight.transform.position,
                    flashlight.transform.position +
                    flashlight.transform.forward * flashlight.range,
                    Color.white
                );

                Debug.DrawLine(
                    other.transform.position,
                    other.transform.position +
                    other.transform.forward * other.range,
                    other.color
                );

                Debug.DrawLine(p1, p2, Color.yellow);
            }

            if (separation > intersectionTolerance)
            {
                failureReason =
                    "Lines miss each other. Separation: " +
                    separation.ToString("F2");
                continue;
            }

            float d1 = Vector3.Distance(
                flashlight.transform.position, p1);

            float d2 = Vector3.Distance(
                other.transform.position, p2);

            if (d1 > flashlight.range || d2 > other.range)
            {
                failureReason =
                    "Intersection is outside a light's range.";
                continue;
            }

            intersection = (p1 + p2) * 0.5f;
            foundIntersection = true;
            failureReason = "Intersection found!";

            break;
        }

        if (foundIntersection)
        {
            status = "INTERSECTION FOUND! Spawning prefab.";

            Debug.Log(
                "[SteamLiftSpawner] INTERSECTION FOUND at " +
                intersection,
                this
            );

            Vector3 position =
                intersection + Vector3.up * spawnOffset;

            spawnedLift = Instantiate(
                steamLiftPrefab,
                position,
                steamLiftPrefab.transform.rotation
            );

            Debug.Log(
                "[SteamLiftSpawner] PREFAB SPAWNED at " +
                position + ": " + spawnedLift.name,
                spawnedLift
            );

            return;
        }

        status = failureReason;

        LogStatusPeriodically(
            registeredCount,
            usableSpotlights
        );
    }

    private void LogStatusPeriodically(
        int registeredCount,
        int usableSpotlights)
    {
        if (Time.time < nextDiagnosticTime)
            return;

        nextDiagnosticTime = Time.time + 2f;

        Debug.Log(
            "[SteamLiftSpawner] " + status +
            " | Registered lights: " + registeredCount +
            " | Usable spotlights: " + usableSpotlights,
            this
        );
    }

    private bool ClosestPointsOnRays(
        Vector3 p1,
        Vector3 d1,
        Vector3 p2,
        Vector3 d2,
        out Vector3 point1,
        out Vector3 point2)
    {
        point1 = Vector3.zero;
        point2 = Vector3.zero;

        Vector3 w = p1 - p2;

        float a = Vector3.Dot(d1, d1);
        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d2, d2);
        float d = Vector3.Dot(d1, w);
        float e = Vector3.Dot(d2, w);

        float denominator = a * c - b * b;

        if (Mathf.Abs(denominator) < 0.0001f)
            return false;

        float t = (b * e - c * d) / denominator;
        float u = (a * e - b * d) / denominator;

        if (t < 0f || u < 0f)
            return false;

        point1 = p1 + d1 * t;
        point2 = p2 + d2 * u;

        return true;
    }

    private void OnGUI()
    {
        if (!showStatusOnScreen)
            return;

        GUI.color = status.Contains("INTERSECTION FOUND")
            ? Color.green
            : Color.yellow;

        GUI.Label(
            new Rect(15, 15, 850, 30),
            "STEAM LIFT DEBUG: " + status
        );

        GUI.color = Color.white;
    }
}
