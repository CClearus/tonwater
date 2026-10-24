
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public CharacterController characterController;

    [Header("Movement Settings")]
    public float walkThreshold = 0.1f;

    [Header("Animator Parameters")]
    public string walkParameter = "walk";
    public string jumpParameter = "jump";

    private Vector3 previousPosition;
    private bool wasGrounded;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (characterController == null)
            characterController =
                GetComponentInParent<CharacterController>();

        previousPosition = transform.position;
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError(
                "PlayerAnimationController: No Animator found.",
                this
            );
            enabled = false;
            return;
        }

        wasGrounded = characterController != null &&
                      characterController.isGrounded;
    }

    private void Update()
    {
        UpdateWalkAnimation();
        UpdateJumpAnimation();
    }

    private void UpdateWalkAnimation()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - previousPosition;

        // Ignore vertical movement so jumping alone doesn't count as walking.
        movement.y = 0f;

        float movementSpeed = movement.magnitude / Time.deltaTime;

        bool isWalking = movementSpeed > walkThreshold;

        animator.SetBool(walkParameter, isWalking);

        previousPosition = currentPosition;
    }

    private void UpdateJumpAnimation()
    {
        if (characterController == null)
            return;

        bool isGrounded = characterController.isGrounded;

        // Detect when the player leaves the ground.
        if (wasGrounded && !isGrounded)
        {
            animator.SetTrigger(jumpParameter);
        }

        wasGrounded = isGrounded;
    }
}
