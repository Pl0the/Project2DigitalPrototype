using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Renderer))]
[DisallowMultipleComponent]
public sealed class ArenaBlock : MonoBehaviour
{
    [SerializeField, Min(1)] private int maximumDurability = 3;

    public int OwnerIndex { get; private set; }
    public int RemainingDurability { get; private set; }
    public int MaximumDurability => maximumDurability;
    public bool IsPreview { get; private set; }
    public bool IsTemporary { get; private set; }
    public bool IsBroken => RemainingDurability <= 0;
    public Vector3 HalfExtents => Vector3.Scale(GetComponent<BoxCollider>().size, transform.lossyScale) * 0.5f;

    public void Initialize(int owner, int durability, bool preview = false, bool temporary = false)
    {
        OwnerIndex = owner;
        RemainingDurability = Mathf.Clamp(durability, 1, maximumDurability);
        IsPreview = preview;
        IsTemporary = temporary;
        GetComponent<BoxCollider>().enabled = !preview;
        SetColor(owner == 0 ? (temporary ? new Color(0.35f, 0.9f, 1f) : new Color(0.08f, 0.72f, 0.96f)) : (temporary ? new Color(1f, 0.72f, 0.3f) : new Color(1f, 0.43f, 0.12f)));
    }

    public void ShowPlacementValidity(bool valid)
    {
        if (IsPreview)
            SetColor(valid ? new Color(0.25f, 0.95f, 0.5f) : new Color(1f, 0.18f, 0.2f));
    }

    public void RegisterHit()
    {
        if (IsPreview || IsBroken)
            return;
        RemainingDurability--;
        if (IsBroken)
        {
            // Disable immediately so another ball in this same physics step can pass through.
            GetComponent<BoxCollider>().enabled = false;
            gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }

    private void SetColor(Color color)
    {
        MaterialPropertyBlock appearance = new MaterialPropertyBlock();
        appearance.SetColor("_BaseColor", color);
        GetComponent<Renderer>().SetPropertyBlock(appearance);
    }
}
