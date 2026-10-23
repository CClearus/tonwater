using UnityEngine;

// Fades the flare out when something blocks the line of sight between the
// local player's camera and the flare. The flare itself draws on top of
// everything (so the ceiling can't cut it), so this is what hides it behind walls.
[RequireComponent(typeof(Renderer))]
public class FlareVisibility : MonoBehaviour
{
    [SerializeField] float fadeSpeed = 8f;

    static readonly int VisibilityId = Shader.PropertyToID("_Visibility");

    Renderer rend;
    MaterialPropertyBlock block;
    float visibility = 1f;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        var cam = PlayerMovement.LocalCamera;
        float target = 1f;
        if (cam != null)
        {
            bool blocked = Physics.Linecast(cam.transform.position, transform.position,
                                            out _, ~0, QueryTriggerInteraction.Ignore);
            target = blocked ? 0f : 1f;
        }

        visibility = Mathf.MoveTowards(visibility, target, fadeSpeed * Time.deltaTime);
        rend.GetPropertyBlock(block);
        block.SetFloat(VisibilityId, visibility);
        rend.SetPropertyBlock(block);
    }
}
