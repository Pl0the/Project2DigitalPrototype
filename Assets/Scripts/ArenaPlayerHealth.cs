using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaPlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maximumHealth = 6;

    public int MaximumHealth => maximumHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDefeated => CurrentHealth <= 0;
    public bool WasRecentlyHit => Time.time - lastHitTime < 0.35f;

    private float lastHitTime = float.NegativeInfinity;

    private void Awake()
    {
        ResetHealth();
    }

    public void TakeDamage(int amount = 1)
    {
        if (amount <= 0 || IsDefeated)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        lastHitTime = Time.time;
    }

    public void ResetHealth()
    {
        CurrentHealth = maximumHealth;
        lastHitTime = float.NegativeInfinity;
    }
}
