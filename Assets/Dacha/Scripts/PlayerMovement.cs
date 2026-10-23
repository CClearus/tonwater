using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraHolder;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -20f;
    [Tooltip("Can still jump this long after walking off an edge.")]
    [SerializeField] float coyoteTime = 0.12f;
    [Tooltip("Space pressed this long before landing still jumps.")]
    [SerializeField] float jumpBufferTime = 0.12f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 0.1f;
    [SerializeField] float maxLookAngle = 85f;

    [Header("Ground Check")]
    [SerializeField] string groundTag = "Ground";
    [SerializeField] float groundCheckDistance = 0.15f;
    [SerializeField] bool debugJump = true;

    [Header("Spawn")]
    [SerializeField] float spawnSpread = 2f;

    public bool IsGrounded { get; private set; }

    CharacterController controller;
    Camera playerCamera;
    AudioListener playerListener;
    GameObject sceneCamera;
    float verticalVelocity;
    float pitch;
    float lastGroundedTime = -999f;
    float lastJumpPressTime = -999f;
    bool inJump;
    float jumpStartY;
    float jumpPeakY;
    readonly Collider[] groundHits = new Collider[8];

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>(true);
        playerListener = GetComponentInChildren<AudioListener>(true);
        if (cameraHolder == null && playerCamera != null)
            cameraHolder = playerCamera.transform;
    }

    public override void OnNetworkSpawn()
    {
        // Only your own player renders through its camera and hears audio.
        if (playerCamera != null) playerCamera.enabled = IsOwner;
        if (playerListener != null) playerListener.enabled = IsOwner;

        if (!IsOwner) return;

        if (Camera.main != null && Camera.main != playerCamera)
        {
            sceneCamera = Camera.main.gameObject;
            sceneCamera.SetActive(false);
        }

        MoveToSpawnPoint();

        SetCursorLocked(true);
    }

    // Uses the scene's PlayerSpawnPoints (one per player, in order). Without any,
    // players spawn at the prefab position, spread out so they don't overlap.
    void MoveToSpawnPoint()
    {
        controller.enabled = false;
        var point = PlayerSpawnPoint.Get((int)OwnerClientId);
        if (point != null)
        {
            // Feet on the point: lift by half the controller's world height.
            float halfHeight = (controller.height * 0.5f - controller.center.y) * transform.lossyScale.y;
            transform.SetPositionAndRotation(point.transform.position + Vector3.up * halfHeight,
                                             Quaternion.Euler(0f, point.transform.eulerAngles.y, 0f));
        }
        else
        {
            Vector2 offset = Random.insideUnitCircle * spawnSpread;
            transform.position += new Vector3(offset.x, 0f, offset.y);
        }
        controller.enabled = true;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        if (sceneCamera != null) sceneCamera.SetActive(true);
        SetCursorLocked(false);
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked
                 && !IsPointerOverUI())
            SetCursorLocked(true);

        bool looking = Cursor.lockState == CursorLockMode.Locked;
        if (looking) Look(mouse.delta.ReadValue());

        CheckGround();
        Move(keyboard, looking);
    }

    void Look(Vector2 delta)
    {
        transform.Rotate(Vector3.up, delta.x * mouseSensitivity);
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -maxLookAngle, maxLookAngle);
        if (cameraHolder != null) cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move(Keyboard keyboard, bool hasInput)
    {
        Vector2 input = Vector2.zero;
        if (hasInput)
        {
            input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            input = Vector2.ClampMagnitude(input, 1f);
        }

        float speed = hasInput && keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        bool touchingBelow = (controller.collisionFlags & CollisionFlags.Below) != 0;
        if ((IsGrounded || touchingBelow) && verticalVelocity < 0f)
            verticalVelocity = -2f; // keep snapped to the floor

        if (IsGrounded) lastGroundedTime = Time.time;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            lastJumpPressTime = Time.time;
            if (debugJump && !(hasInput && Time.time - lastGroundedTime <= coyoteTime))
                LogJump(false, hasInput);
        }

        // Jump if Space was pressed recently and we were grounded recently.
        bool jumpBuffered = Time.time - lastJumpPressTime <= jumpBufferTime;
        bool coyoteOk = Time.time - lastGroundedTime <= coyoteTime;
        if (hasInput && jumpBuffered && coyoteOk && verticalVelocity <= 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressTime = lastGroundedTime = -999f; // consume
            inJump = true;
            jumpStartY = jumpPeakY = transform.position.y;
            if (debugJump) LogJump(true, true);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;
        CollisionFlags flags = controller.Move(move * Time.deltaTime);

        // Bonk: stop rising when the head hits something.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
            if (debugJump) Debug.Log("[Jump] Head hit a ceiling/collider above - jump was blocked.", this);
        }

        if (inJump)
        {
            jumpPeakY = Mathf.Max(jumpPeakY, transform.position.y);
            if (verticalVelocity < 0f && (flags & CollisionFlags.Below) != 0)
            {
                inJump = false;
                if (debugJump)
                    Debug.Log($"[Jump] Landed. Rose {jumpPeakY - jumpStartY:F2} m (expected ~{jumpHeight:F2} m).", this);
            }
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (debugJump && inJump && hit.normal.y < -0.5f)
            Debug.Log($"[Jump] Blocked by '{hit.collider.name}' above the player.", hit.collider);
    }

    int groundHitCount;

    void CheckGround()
    {
        GetFeetSphere(controller, out Vector3 feet, out float radius);
        groundHitCount = Physics.OverlapSphereNonAlloc(feet, radius, groundHits, ~0, QueryTriggerInteraction.Ignore);
        IsGrounded = false;
        for (int i = 0; i < groundHitCount; i++)
        {
            if (groundHits[i].transform.IsChildOf(transform)) continue;
            if (groundHits[i].CompareTag(groundTag)) { IsGrounded = true; break; }
        }
    }

    void GetFeetSphere(CharacterController cc, out Vector3 feet, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        float worldRadius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float worldHeight = Mathf.Max(cc.height * Mathf.Abs(scale.y), worldRadius * 2f);
        radius = worldRadius * 0.9f;
        feet = transform.TransformPoint(cc.center)
               + Vector3.down * (worldHeight * 0.5f - radius + groundCheckDistance);
    }

    void LogJump(bool jumped, bool hasInput)
    {
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < groundHitCount; i++)
        {
            if (groundHits[i].transform.IsChildOf(transform)) continue;
            names.Append(groundHits[i].name).Append(" (tag: ").Append(groundHits[i].tag).Append(") ");
        }

        string reason = jumped ? "JUMPED"
            : !hasInput ? "NO JUMP - cursor not locked, click the game view"
            : "NO JUMP - not touching a collider tagged '" + groundTag + "'";
        Debug.Log($"[Jump] {reason} | grounded={IsGrounded} | under feet: {(names.Length > 0 ? names.ToString() : "nothing")}", this);
    }

    static bool IsPointerOverUI()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

//ground check gizmo here nothing need to seeeeeeee
    void OnDrawGizmosSelected()
    {
        var cc = controller != null ? controller : GetComponent<CharacterController>();
        if (cc == null) return;
        GetFeetSphere(cc, out Vector3 feet, out float radius);
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(feet, radius);
    }
}
