using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class ArenaCloneBody : MonoBehaviour
{
    [SerializeField] private Renderer aimMarker;
    private CharacterController character;
    private Renderer[] renderers;

    public ArenaPlayerMotor Owner { get; private set; }
    public int OwnerIndex { get; private set; }
    public bool IsPreview { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsAlive => !IsPreview && !IsDestroyed && gameObject.activeInHierarchy;
    public CharacterController BodyCollider => GetComponent<CharacterController>();

    public void Initialize(ArenaPlayerMotor owner, int ownerIndex, bool preview = false)
    {
        Disconnect();
        Owner = owner;
        OwnerIndex = ownerIndex;
        IsPreview = preview;
        IsDestroyed = false;
        character = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
        character.enabled = !preview;
        transform.rotation = owner.transform.rotation;
        SetColor(ArenaTeamColors.Clone(ownerIndex));
        if (!preview)
        {
            Owner.MovementRequested += CopyMovement;
            Owner.AimChanged += CopyAim;
        }
    }

    private void CopyMovement(Vector3 requestedMovement)
    {
        if (!IsAlive)
            return;
        // Copy the requested input, not the owner's resulting displacement.
        // A blocked clone keeps receiving later input and can move away again.
        Vector3 target = transform.position + requestedMovement;
        float margin = character.radius + character.skinWidth;
        target.x = Mathf.Clamp(target.x, Owner.AreaMinimum.x + margin, Owner.AreaMaximum.x - margin);
        target.z = Mathf.Clamp(target.z, Owner.AreaMinimum.y + margin, Owner.AreaMaximum.y - margin);
        character.Move(target - transform.position);
    }

    private void CopyAim(Quaternion rotation)
    {
        if (IsAlive)
            transform.rotation = rotation;
    }

    public void SetAimMarkerVisible(bool visible)
    {
        if (aimMarker != null)
            aimMarker.enabled = visible;
    }

    public void ShowPlacementValidity(bool valid)
    {
        if (IsPreview)
            SetColor(valid ? new Color(0.25f, 0.95f, 0.5f) : new Color(1f, 0.18f, 0.2f));
    }

    public void Shatter()
    {
        if (IsDestroyed)
            return;
        IsDestroyed = true;
        Disconnect();
        BodyCollider.enabled = false;
        gameObject.SetActive(false);
        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    private void SetColor(Color color)
    {
        MaterialPropertyBlock appearance = new MaterialPropertyBlock();
        appearance.SetColor("_BaseColor", color);
        foreach (Renderer item in renderers)
            item.SetPropertyBlock(appearance);
    }

    private void Disconnect()
    {
        if (Owner == null)
            return;
        Owner.MovementRequested -= CopyMovement;
        Owner.AimChanged -= CopyAim;
    }

    private void OnDisable() => Disconnect();
}
