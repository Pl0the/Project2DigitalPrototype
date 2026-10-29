using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class ArenaPlayerMotor : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField] private Vector2 areaMinimum = new Vector2(-12f, -8f);
    [SerializeField] private Vector2 areaMaximum = new Vector2(0f, 8f);
    [SerializeField] private float eyeOffset = 0.6f;
    [SerializeField] private Renderer aimMarker;

    private CharacterController character;
    private Renderer[] bodyRenderers;
    private int controlStartFrame;

    public bool CanControl { get; private set; }
    public Vector3 EyePosition => transform.position + Vector3.up * eyeOffset;
    public Vector3 AimDirection => transform.forward;
    public Vector2 AreaMinimum => areaMinimum;
    public Vector2 AreaMaximum => areaMaximum;
    public event Action<Vector3> MovementRequested;
    public event Action<Quaternion> AimChanged;

    private void Awake()
    {
        EnsureReferences();
    }

    private void Update()
    {
        if (!CanControl || Cursor.lockState != CursorLockMode.Locked)
            return;

        // Ignore the cursor warp when first locking the mouse for a new player.
        if (Time.frameCount > controlStartFrame + 1 && Mouse.current != null)
            Look(Mouse.current.delta.ReadValue().x);

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 movement = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
        Move(movement, Time.deltaTime);
    }

    public void SetControlEnabled(bool value)
    {
        CanControl = value;
        controlStartFrame = Time.frameCount;
    }

    public void Move(Vector2 input, float deltaTime)
    {
        if (!CanControl)
            return;

        EnsureReferences();
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        Vector3 requestedMovement = direction * (moveSpeed * Mathf.Max(0f, deltaTime));
        Vector3 target = transform.position + requestedMovement;
        float margin = character.radius + character.skinWidth;
        target.x = Mathf.Clamp(target.x, areaMinimum.x + margin, areaMaximum.x - margin);
        target.z = Mathf.Clamp(target.z, areaMinimum.y + margin, areaMaximum.y - margin);
        character.Move(target - transform.position);
        MovementRequested?.Invoke(requestedMovement);
    }

    public void Look(float horizontalDelta)
    {
        if (CanControl)
        {
            transform.Rotate(0f, horizontalDelta * mouseSensitivity, 0f, Space.World);
            AimChanged?.Invoke(transform.rotation);
        }
    }

    public void SetBodyVisible(bool visible)
    {
        EnsureReferences();
        foreach (Renderer bodyRenderer in bodyRenderers)
            if (bodyRenderer != null)
                bodyRenderer.enabled = visible;
    }

    public void SetAimMarkerVisible(bool visible)
    {
        if (aimMarker != null)
            aimMarker.enabled = visible;
    }

    public void ResetPose(Vector3 position, Quaternion rotation)
    {
        EnsureReferences();
        bool wasEnabled = character.enabled;
        character.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        character.enabled = wasEnabled;
    }

    private void EnsureReferences()
    {
        if (character == null)
            character = GetComponent<CharacterController>();
        if (bodyRenderers == null)
            bodyRenderers = GetComponentsInChildren<Renderer>();
    }
}
