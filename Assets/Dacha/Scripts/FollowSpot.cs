using UnityEngine;

// A stage-style spotlight that shines on this player from the scene's
// FollowSpotAnchor. Runs on every client for every player (it only needs the
// already-synced player position), so everyone sees everyone's spotlight.
public class FollowSpot : MonoBehaviour
{
    [SerializeField] Color color = new Color(1f, 0.96f, 0.9f);
    [SerializeField] float intensity = 2500f;
    [SerializeField] float range = 60f;
    [SerializeField] float outerAngle = 10f;
    [SerializeField] float innerAngle = 5f;
    [Tooltip("Point on the player the light aims at, relative to the player.")]
    [SerializeField] Vector3 aimOffset = new Vector3(0f, -0.5f, 0f);

    Light spot;

    void Start()
    {
        if (FollowSpotAnchor.Instance == null) return;

        var go = new GameObject($"FollowSpot ({name})");
        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = outerAngle;
        spot.innerSpotAngle = innerAngle;
        spot.shadows = LightShadows.Soft;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (spot == null || FollowSpotAnchor.Instance == null) return;
        Vector3 from = FollowSpotAnchor.Instance.transform.position;
        Vector3 target = transform.position + aimOffset;
        spot.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
    }

    void OnDestroy()
    {
        if (spot != null) Destroy(spot.gameObject);
    }
}
