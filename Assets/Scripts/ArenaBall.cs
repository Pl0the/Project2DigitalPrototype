using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Renderer))]
[DisallowMultipleComponent]
public sealed class ArenaBall : MonoBehaviour
{
    private const float ContactPadding = 0.003f;
    private const int MaximumBouncesPerStep = 8;

    private Collider shooterCollider;
    private bool leavingShooter;
    private float height;
    private float radius;

    public Vector3 Direction { get; private set; }
    public float Speed { get; private set; }
    public int BounceCount { get; private set; }
    public Collider LastHitCollider { get; private set; }

    public void Initialize(ArenaPlayerMotor shooter, Vector3 position, Vector3 direction, float speed, Color color)
    {
        transform.position = position;
        height = position.y;
        direction.y = 0f;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Speed = Mathf.Max(0f, speed);
        BounceCount = 0;
        LastHitCollider = null;
        shooterCollider = shooter == null ? null : shooter.GetComponent<CharacterController>();
        leavingShooter = shooterCollider != null;
        SphereCollider sphere = GetComponent<SphereCollider>();
        radius = sphere.radius * Mathf.Abs(transform.lossyScale.x);

        MaterialPropertyBlock appearance = new MaterialPropertyBlock();
        appearance.SetColor("_BaseColor", color);
        GetComponent<Renderer>().SetPropertyBlock(appearance);
    }

    public void Advance(float deltaTime)
    {
        Vector3 position = transform.position;
        float remaining = Speed * Mathf.Max(0f, deltaTime);

        // Sweeps keep the ball from tunnelling through thin walls between frames.
        for (int bounce = 0; bounce < MaximumBouncesPerStep && remaining > 0.00001f; bounce++)
        {
            UpdateShooterClearance(position);
            if (!FindHit(position, remaining, out RaycastHit hit, out Vector3 normal))
            {
                position += Direction * remaining;
                remaining = 0f;
                break;
            }

            float travel = Mathf.Max(0f, hit.distance - ContactPadding);
            position += Direction * travel + normal * ContactPadding;
            remaining = Mathf.Max(0f, remaining - travel - ContactPadding);
            Direction = Vector3.Reflect(Direction, normal).normalized;
            LastHitCollider = hit.collider;
            BounceCount++;

            ArenaPlayerHealth health = hit.collider.GetComponentInParent<ArenaPlayerHealth>();
            if (health != null)
                health.TakeDamage();
        }

        position.y = height;
        transform.position = position;
    }

    private void UpdateShooterClearance(Vector3 position)
    {
        if (!leavingShooter || shooterCollider == null)
            return;

        Vector3 offset = position - shooterCollider.bounds.center;
        offset.y = 0f;
        float clearance = Mathf.Max(shooterCollider.bounds.extents.x, shooterCollider.bounds.extents.z) + radius + ContactPadding;
        if (offset.sqrMagnitude > clearance * clearance)
            leavingShooter = false;
    }

    private bool FindHit(Vector3 position, float distance, out RaycastHit nearest, out Vector3 normal)
    {
        nearest = default;
        normal = Vector3.zero;
        float nearestDistance = float.PositiveInfinity;
        RaycastHit[] hits = Physics.SphereCastAll(position, radius, Direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (leavingShooter && hit.collider == shooterCollider)
                continue;

            Vector3 planarNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (planarNormal.sqrMagnitude < 0.0001f)
                continue;
            planarNormal.Normalize();
            if (Vector3.Dot(Direction, planarNormal) >= -0.0001f || hit.distance >= nearestDistance)
                continue;

            nearest = hit;
            normal = planarNormal;
            nearestDistance = hit.distance;
        }
        return nearestDistance < float.PositiveInfinity;
    }
}
