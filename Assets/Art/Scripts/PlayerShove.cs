
using UnityEngine;

public class PlayerShove : MonoBehaviour
{
    [Header("Input")]
    public KeyCode shoveKey = KeyCode.F;

    [Header("Shove Settings")]
    [Min(0.1f)]
    public float shoveRange = 2.2f;

    [Range(1f, 180f)]
    public float shoveAngle = 90f;

    [Min(0f)]
    public float shoveForce = 8f;

    [Min(0f)]
    public float upwardForce = 1.5f;

    [Min(0f)]
    public float shoveCooldown = 0.8f;

    [Header("Detection")]
    public Transform shoveOrigin;
    public LayerMask shoveLayers = ~0;

    [Tooltip("Optional. Assign the player's own colliders to ignore them.")]
    public Collider[] playerColliders;

    [Header("Debug")]
    public bool showDebug = true;

    private float nextShoveTime;

    private void Awake()
    {
        if (shoveOrigin == null)
            shoveOrigin = transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(shoveKey))
            TryShove();
    }

    public void TryShove()
    {
        if (Time.time < nextShoveTime)
            return;

        nextShoveTime = Time.time + shoveCooldown;

        Vector3 origin = shoveOrigin.position;
        Vector3 forward = shoveOrigin.forward.normalized;

        Collider[] hits = Physics.OverlapSphere(
            origin + forward * (shoveRange * 0.5f),
            shoveRange * 0.5f + 0.5f,
            shoveLayers,
            QueryTriggerInteraction.Ignore
        );

        bool shovedSomething = false;

        foreach (Collider hit in hits)
        {
            if (hit == null || IsOwnCollider(hit))
                continue;

            Rigidbody body = hit.attachedRigidbody;

            if (body == null || body.isKinematic)
                continue;

            Vector3 targetPosition = body.worldCenterOfMass;
            Vector3 directionToTarget = targetPosition - origin;

            if (directionToTarget.sqrMagnitude < 0.001f)
                directionToTarget = forward;

            float angle = Vector3.Angle(
                forward,
                directionToTarget.normalized
            );

            if (angle > shoveAngle * 0.5f)
                continue;

            Vector3 pushDirection =
                directionToTarget.normalized + Vector3.up * upwardForce;

            pushDirection.Normalize();

            body.AddForce(
                pushDirection * shoveForce,
                ForceMode.Impulse
            );

            shovedSomething = true;

            if (showDebug)
            {
                Debug.DrawLine(
                    origin,
                    targetPosition,
                    Color.green,
                    1f
                );
            }
        }

        if (showDebug)
        {
            Debug.Log(
                shovedSomething
                    ? "Shove hit a target!"
                    : "Shove missed."
            );
        }
    }

    private bool IsOwnCollider(Collider target)
    {
        if (target.transform == transform ||
            target.transform.IsChildOf(transform))
            return true;

        if (playerColliders != null)
        {
            foreach (Collider ownCollider in playerColliders)
            {
                if (ownCollider == target)
                    return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform =
            shoveOrigin != null ? shoveOrigin : transform;

        Vector3 origin = originTransform.position;
        Vector3 forward = originTransform.forward;

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            origin + forward * (shoveRange * 0.5f),
            shoveRange * 0.5f + 0.5f
        );

        Gizmos.color = Color.red;

        Gizmos.DrawRay(
            origin,
            forward * shoveRange
        );
    }
}
