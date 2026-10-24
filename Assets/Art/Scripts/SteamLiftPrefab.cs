
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SteamLiftPrefab : MonoBehaviour
{
    [Header("Beam Intersection")]
    public float beamTolerance = 0.6f;

    [Header("Beam Colors")]
    public Color redReference = Color.red;
    public Color blueReference = Color.blue;

    [Range(0.1f, 1f)]
    public float colorMatchThreshold = 0.5f;

    [Header("Steam Effect")]
    public ParticleSystem steamParticles;

    [Header("Player Lift")]
    public string playerTag = "Player";
    public float liftSpeed = 5f;
    public float maxLiftSpeed = 8f;
    public float rigidbodyLiftForce = 15f;

    [Header("Debug")]
    public bool showDebugLines = true;

    private BoxCollider liftTrigger;
    private bool liftActive;

    private void Awake()
    {
        liftTrigger = GetComponent<BoxCollider>();
        liftTrigger.isTrigger = true;

        SetLiftActive(false);
    }

    private void Update()
    {
        bool redFound = false;
        bool blueFound = false;

        foreach (LightSource source in LightSource.All)
        {
            if (source == null || source.SourceLight == null)
                continue;

            Light beam = source.SourceLight;

            if (!beam.enabled ||
                beam.intensity <= 0f ||
                beam.type != LightType.Spot)
                continue;

            if (!BeamReachesLift(beam))
                continue;

            if (MatchesColor(beam.color, redReference))
                redFound = true;

            if (MatchesColor(beam.color, blueReference))
                blueFound = true;

            if (redFound && blueFound)
                break;
        }

        SetLiftActive(redFound && blueFound);
    }

    private bool BeamReachesLift(Light beam)
    {
        Vector3 origin = beam.transform.position;
        Vector3 target = transform.position;
        Vector3 toTarget = target - origin;

        float distance = toTarget.magnitude;

        if (distance < 0.01f || distance > beam.range)
            return false;

        Vector3 direction = toTarget / distance;

        float angle = Vector3.Angle(
            beam.transform.forward,
            direction
        );

        if (angle > beam.spotAngle * 0.5f)
            return false;

        // Check that the beam isn't blocked before reaching the lift.
        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < distance - beamTolerance)
                return false;
        }

        if (showDebugLines)
        {
            Debug.DrawLine(
                origin,
                target,
                beam.color
            );
        }

        return true;
    }

    private bool MatchesColor(Color actual, Color reference)
    {
        Vector3 a = new Vector3(actual.r, actual.g, actual.b);
        Vector3 b = new Vector3(reference.r, reference.g, reference.b);

        if (a.sqrMagnitude < 0.0001f ||
            b.sqrMagnitude < 0.0001f)
            return false;

        a.Normalize();
        b.Normalize();

        return Vector3.Distance(a, b) <= colorMatchThreshold;
    }

    private void SetLiftActive(bool active)
    {
        if (liftActive == active)
            return;

        liftActive = active;

        if (steamParticles == null)
            return;

        if (active)
        {
            if (!steamParticles.isPlaying)
                steamParticles.Play();
        }
        else
        {
            steamParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting
            );
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!liftActive || !IsPlayer(other))
            return;

        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        if (controller != null && controller.enabled)
        {
            controller.Move(
                Vector3.up * liftSpeed * Time.deltaTime
            );
            return;
        }

        Rigidbody body = other.attachedRigidbody;

        if (body != null && !body.isKinematic &&
            body.velocity.y < maxLiftSpeed)
        {
            body.AddForce(
                Vector3.up * rigidbodyLiftForce,
                ForceMode.Acceleration
            );
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        return root.CompareTag(playerTag);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = liftActive ? Color.green : Color.gray;
        Gizmos.DrawWireCube(
            transform.position,
            GetComponent<BoxCollider>() != null
                ? GetComponent<BoxCollider>().size
                : Vector3.one
        );
    }

    private void OnGUI()
    {
        GUI.color = liftActive ? Color.green : Color.red;

        GUI.Label(
            new Rect(20, 20, 400, 30),
            liftActive
                ? "STEAM LIFT: ACTIVE - BOTH BEAMS DETECTED"
                : "STEAM LIFT: INACTIVE - WAITING FOR RED + BLUE"
        );

        GUI.color = Color.white;
    }
}
