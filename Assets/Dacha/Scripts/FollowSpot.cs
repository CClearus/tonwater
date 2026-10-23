using UnityEngine;

// A stage-style spotlight that shines on this player from the scene's
// FollowSpotAnchor, with a soft visible beam. Runs on every client for every
// player (it only needs the already-synced player position), so everyone sees
// everyone's spotlight.
public class FollowSpot : MonoBehaviour
{
    [SerializeField] Color color = new Color(1f, 0.96f, 0.9f);
    [SerializeField] float intensity = 900f;
    [SerializeField] float range = 60f;
    [SerializeField] float outerAngle = 20f;
    [SerializeField] float innerAngle = 2f;
    [Tooltip("Pattern the light projects (a light cookie). Optional.")]
    [SerializeField] Texture cookie;
    [Tooltip("Point on the player the light aims at, relative to the player.")]
    [SerializeField] Vector3 aimOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Visible Beam")]
    [Tooltip("Material using Dacha/LightBeam. Leave empty for no beam.")]
    [SerializeField] Material beamMaterial;
    [Tooltip("How far the beam continues past the player (so it reaches the floor).")]
    [SerializeField] float beamOvershoot = 1.5f;

    [Header("Flicker")]
    [SerializeField] bool flicker = true;
    [Range(0f, 0.5f)] [SerializeField] float shimmer = 0.06f;
    [Range(0f, 2f)] [SerializeField] float dipsPerSecond = 0.08f;

    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    static readonly int IsOwnBeamId = Shader.PropertyToID("_IsOwnBeam");
    static Mesh coneMesh;

    Light spot;
    Unity.Netcode.NetworkObject networkObject;
    Transform beam;
    Renderer beamRenderer;
    Material beamInstance;

    void Start()
    {
        if (FollowSpotAnchor.Instance == null) return;
        networkObject = GetComponent<Unity.Netcode.NetworkObject>();

        var go = new GameObject($"FollowSpot ({name})");
        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = outerAngle;
        spot.innerSpotAngle = innerAngle;
        spot.shadows = LightShadows.Soft;
        spot.cookie = cookie;
        if (flicker)
        {
            var f = go.AddComponent<LightFlicker>();
            f.shimmer = shimmer;
            f.dipsPerSecond = dipsPerSecond;
        }

        if (beamMaterial != null)
        {
            var beamGo = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            beam = beamGo.transform;
            beam.SetParent(go.transform, false);
            beamGo.GetComponent<MeshFilter>().sharedMesh = GetConeMesh();
            beamRenderer = beamGo.GetComponent<MeshRenderer>();
            beamInstance = new Material(beamMaterial); // own copy so flicker is per light
            beamRenderer.sharedMaterial = beamInstance;
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
        }
        LateUpdate();
    }

    void LateUpdate()
    {
        if (spot == null || FollowSpotAnchor.Instance == null) return;
        Vector3 from = FollowSpotAnchor.Instance.transform.position;
        Vector3 target = transform.position + aimOffset;
        spot.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));

        if (beam != null)
        {
            // Stretch the unit cone to reach just past the player, matching the light's angle.
            float length = Vector3.Distance(from, target) + beamOvershoot;
            float radius = Mathf.Tan(outerAngle * 0.5f * Mathf.Deg2Rad) * length;
            beam.localScale = new Vector3(radius, radius, length);

            // Follow the light's flicker.
            beamInstance.SetFloat(BrightnessId, intensity > 0f ? spot.intensity / intensity : 0f);
            // Your own beam only shows when you look up at the light; others see it normally.
            beamInstance.SetFloat(IsOwnBeamId, networkObject != null && networkObject.IsOwner ? 1f : 0f);
        }
    }

    void OnDestroy()
    {
        if (spot != null) Destroy(spot.gameObject);
        if (beamInstance != null) Destroy(beamInstance);
    }

    // Open cone: tip at the origin, 1 unit long along +Z, base radius 1.
    // uv.y runs 0 (tip) to 1 (base) for the beam shader's fades.
    static Mesh GetConeMesh()
    {
        if (coneMesh != null) return coneMesh;
        const int segments = 32;
        var vertices = new Vector3[(segments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 normal = (dir - Vector3.forward).normalized;
            vertices[i * 2] = Vector3.zero;
            vertices[i * 2 + 1] = dir + Vector3.forward;
            normals[i * 2] = normals[i * 2 + 1] = normal;
            uvs[i * 2] = new Vector2(i / (float)segments, 0f);
            uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
        }
        for (int i = 0; i < segments; i++)
        {
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 3; triangles[t + 2] = v + 1;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        coneMesh = new Mesh { name = "FollowSpotCone", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
        coneMesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));
        return coneMesh;
    }
}
