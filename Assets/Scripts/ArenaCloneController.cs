using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ArenaCloneController : MonoBehaviour
{
    public enum PlacementMode { None, ChooseTarget, ChooseBlock, Body, Block }

    [SerializeField] private ArenaCloneBody bodyPrefab;
    [SerializeField] private ArenaPlayerMotor playerOne;
    [SerializeField] private ArenaPlayerMotor playerTwo;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ArenaBlockPlacement blockPlacement;
    private readonly List<ArenaCloneBody> bodies = new List<ArenaCloneBody>();
    private int ownerIndex;
    private int sourceIndex;

    public IReadOnlyList<ArenaCloneBody> Bodies => bodies;
    public PlacementMode Mode { get; private set; }
    public ArenaCloneBody PreviewBody { get; private set; }
    public int SourceCount => GetSources().Count;
    public int SourceNumber => sourceIndex + 1;
    private ArenaPlayerMotor Owner => ownerIndex == 0 ? playerOne : playerTwo;
    public ArenaBlock SelectedSource
    {
        get
        {
            var sources = GetSources();
            if (sources.Count == 0)
                return null;
            sourceIndex = Mathf.Clamp(sourceIndex, 0, sources.Count - 1);
            return sources[sourceIndex];
        }
    }
    public bool IsPlacementValid => Mode == PlacementMode.Body ? PreviewBody != null && CanPlaceBody(PreviewBody.transform.position)
        : Mode == PlacementMode.Block && blockPlacement.IsPreviewValid;

    private void Update()
    {
        if (PreviewBody == null)
            return;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.1f)
            UpdatePointerPosition(mouse.position.ReadValue());
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 movement = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            PreviewBody.transform.position += new Vector3(movement.x, 0f, movement.y).normalized * (5f * Time.deltaTime);
        }
        PreviewBody.ShowPlacementValidity(IsPlacementValid);
    }

    public void BeginClone(int player)
    {
        CancelPlacement();
        ownerIndex = player;
        sourceIndex = 0;
        Mode = PlacementMode.ChooseTarget;
    }

    public void ChooseBody()
    {
        if (Mode == PlacementMode.None)
            return;
        CancelPreview();
        Mode = PlacementMode.Body;
        PreviewBody = Instantiate(bodyPrefab, new Vector3(ownerIndex == 0 ? -5f : 5f, 1f, 0f), Owner.transform.rotation);
        PreviewBody.name = "Body clone preview";
        PreviewBody.Initialize(Owner, ownerIndex, true);
        PreviewBody.ShowPlacementValidity(IsPlacementValid);
    }

    public void ChooseBlock()
    {
        if (Mode == PlacementMode.None || SourceCount == 0)
            return;
        CancelPreview();
        Mode = PlacementMode.ChooseBlock;
    }

    public void CycleSource(int direction)
    {
        int count = SourceCount;
        if (Mode == PlacementMode.ChooseBlock && count > 0)
            sourceIndex = (sourceIndex + direction % count + count) % count;
    }

    public bool ChooseSourceAt(Vector2 screenPosition)
    {
        if (Mode != PlacementMode.ChooseTarget && Mode != PlacementMode.ChooseBlock)
            return false;
        if (!blockPlacement.IsBoardPointer(screenPosition))
            return false;
        if (!Physics.Raycast(viewCamera.ScreenPointToRay(screenPosition), out RaycastHit hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        ArenaBlock block = hit.collider.GetComponentInParent<ArenaBlock>();
        if (block == null || block.OwnerIndex != ownerIndex || block.IsBroken || block.IsPreview)
            return false;
        sourceIndex = GetSources().IndexOf(block);
        Mode = PlacementMode.ChooseBlock;
        return sourceIndex >= 0;
    }

    public bool BeginBlockCopy()
    {
        ArenaBlock source = SelectedSource;
        if (Mode != PlacementMode.ChooseBlock || source == null)
            return false;
        blockPlacement.BeginPlacement(ownerIndex, source);
        Mode = PlacementMode.Block;
        return true;
    }

    public void SetBodyPreviewPose(Vector3 position)
    {
        if (PreviewBody == null)
            return;
        position.y = 1f;
        PreviewBody.transform.position = position;
        PreviewBody.ShowPlacementValidity(IsPlacementValid);
    }

    public void UpdatePointerPosition(Vector2 screenPosition)
    {
        if (Mode == PlacementMode.Block)
            blockPlacement.UpdatePointerPosition(screenPosition);
        else if (PreviewBody != null && blockPlacement.IsBoardPointer(screenPosition))
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(Vector3.up, Vector3.up);
            if (plane.Raycast(ray, out float distance))
                SetBodyPreviewPose(ray.GetPoint(distance));
        }
    }

    public bool CanPlaceBody(Vector3 position)
    {
        CharacterController character = bodyPrefab.BodyCollider;
        float radius = character.radius + 0.08f;
        if (position.x < Owner.AreaMinimum.x + radius || position.x > Owner.AreaMaximum.x - radius || position.z < Owner.AreaMinimum.y + radius || position.z > Owner.AreaMaximum.y - radius)
            return false;
        float halfCylinder = Mathf.Max(0f, character.height * 0.5f - character.radius);
        Physics.SyncTransforms();
        foreach (Collider collider in Physics.OverlapCapsule(position + Vector3.up * halfCylinder, position - Vector3.up * halfCylinder, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            ArenaBlock block = collider.GetComponentInParent<ArenaBlock>();
            ArenaCloneBody clone = collider.GetComponentInParent<ArenaCloneBody>();
            if ((block != null && !block.IsPreview && !block.IsBroken) || (clone != null && clone.IsAlive) || collider.GetComponentInParent<ArenaPlayerMotor>() != null)
                return false;
        }
        return true;
    }

    public bool ConfirmPlacement()
    {
        if (!IsPlacementValid)
            return false;
        if (Mode == PlacementMode.Body)
        {
            PreviewBody.name = "P" + (ownerIndex + 1) + " Body Clone";
            PreviewBody.Initialize(Owner, ownerIndex);
            bodies.Add(PreviewBody);
            PreviewBody = null;
        }
        else if (!blockPlacement.ConfirmPlacement())
            return false;
        Mode = PlacementMode.None;
        Physics.SyncTransforms();
        return true;
    }

    public void CancelPlacement()
    {
        CancelPreview();
        Mode = PlacementMode.None;
    }

    private void CancelPreview()
    {
        if (PreviewBody != null)
        {
            PreviewBody.Shatter();
            PreviewBody = null;
        }
        if (Mode == PlacementMode.Block)
            blockPlacement.CancelPlacement();
    }

    public void ExpireClones()
    {
        CancelPlacement();
        foreach (ArenaCloneBody body in bodies)
            if (body != null)
                body.Shatter();
        bodies.Clear();
        blockPlacement.ExpireTemporaryBlocks();
    }

    public void SetAimMarkersVisible(int player, bool visible)
    {
        foreach (ArenaCloneBody body in bodies)
            if (body != null && body.IsAlive && body.OwnerIndex == player)
                body.SetAimMarkerVisible(visible);
    }

    private List<ArenaBlock> GetSources()
    {
        var sources = new List<ArenaBlock>();
        foreach (ArenaBlock block in blockPlacement.Blocks)
            if (block != null && !block.IsBroken && !block.IsPreview && block.OwnerIndex == ownerIndex)
                sources.Add(block);
        return sources;
    }

    private void OnDisable() => CancelPlacement();
}
