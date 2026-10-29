using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ArenaBlockPlacement : MonoBehaviour
{
    [SerializeField] private ArenaBlock blockPrefab;
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0f)] private float clearance = 0.08f;
    private readonly List<ArenaBlock> blocks = new List<ArenaBlock>();
    private int previewOwner;

    public IReadOnlyList<ArenaBlock> Blocks => blocks;
    public ArenaBlock Preview { get; private set; }
    public bool IsPreviewValid => Preview != null && CanPlace(previewOwner, Preview.transform.position, Preview.transform.rotation);

    private void Update()
    {
        if (Preview == null)
            return;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.1f && IsBoardPointer(mouse.position.ReadValue()))
            UpdatePointerPosition(mouse.position.ReadValue());
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 movement = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            Preview.transform.position += new Vector3(movement.x, 0f, movement.y).normalized * (5f * Time.deltaTime);
            if (keyboard.qKey.wasPressedThisFrame)
                RotatePreview(-15f);
            if (keyboard.eKey.wasPressedThisFrame)
                RotatePreview(15f);
        }
        if (mouse != null && IsBoardPointer(mouse.position.ReadValue()))
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                RotatePreview(Mathf.Sign(scroll) * 15f);
        }
        Preview.ShowPlacementValidity(IsPreviewValid);
    }

    public void BeginPlacement(int owner)
    {
        CancelPlacement();
        previewOwner = owner;
        Preview = Instantiate(blockPrefab, new Vector3(owner == 0 ? -5f : 5f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));
        Preview.name = "Block placement preview";
        Preview.Initialize(owner, 3, true);
        Preview.ShowPlacementValidity(IsPreviewValid);
    }

    public void SetPreviewPose(Vector3 position, float yaw)
    {
        if (Preview == null)
            return;
        position.y = 1f;
        Preview.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Preview.ShowPlacementValidity(IsPreviewValid);
    }

    public void RotatePreview(float angle)
    {
        if (Preview != null)
            Preview.transform.Rotate(0f, angle, 0f, Space.World);
    }

    public void UpdatePointerPosition(Vector2 screenPosition)
    {
        if (Preview == null || !IsBoardPointer(screenPosition))
            return;
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.up, Vector3.up);
        if (plane.Raycast(ray, out float distance))
            Preview.transform.position = ray.GetPoint(distance);
    }

    public bool ConfirmPlacement()
    {
        if (!IsPreviewValid)
            return false;
        Preview.name = "P" + (previewOwner + 1) + " Block";
        Preview.Initialize(previewOwner, 3);
        blocks.Add(Preview);
        Preview = null;
        Physics.SyncTransforms();
        return true;
    }

    public bool CanPlace(int owner, Vector3 position, Quaternion rotation)
    {
        if (blockPrefab == null || owner < 0 || owner > 1)
            return false;
        Vector3 half = blockPrefab.HalfExtents;
        float minimumX = owner == 0 ? -12f : 0f;
        float maximumX = owner == 0 ? 0f : 12f;
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = position + rotation * new Vector3(x * half.x, 0f, z * half.z);
                if (corner.x < minimumX + clearance || corner.x > maximumX - clearance || corner.z < -8f + clearance || corner.z > 8f - clearance)
                    return false;
            }
        Physics.SyncTransforms();
        foreach (Collider collider in Physics.OverlapBox(position, half + Vector3.one * clearance, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            ArenaBlock block = collider.GetComponentInParent<ArenaBlock>();
            if ((block != null && !block.IsPreview && !block.IsBroken) || collider.GetComponentInParent<ArenaPlayerMotor>() != null)
                return false;
        }
        return true;
    }

    public bool IsBoardPointer(Vector2 screenPosition)
    {
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 640f);
        float fromTop = Screen.height - screenPosition.y;
        return fromTop > 165f * scale && fromTop < Screen.height - 124f * scale;
    }

    public void CancelPlacement()
    {
        if (Preview == null)
            return;
        Remove(Preview.gameObject);
        Preview = null;
    }

    public void ResetBoard()
    {
        CancelPlacement();
        foreach (ArenaBlock block in blocks)
            if (block != null)
                Remove(block.gameObject);
        blocks.Clear();
    }

    private static void Remove(GameObject item)
    {
        item.SetActive(false);
        if (Application.isPlaying)
            Destroy(item);
        else
            DestroyImmediate(item);
    }

    private void OnDisable() => CancelPlacement();
}
