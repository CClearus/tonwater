using UnityEngine;

// Place on the ceiling where the followspots come from (one per scene).
// Every player's FollowSpot light shines down from here.
public class FollowSpotAnchor : MonoBehaviour
{
    public static FollowSpotAnchor Instance { get; private set; }

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 3f);
    }
}
