using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaVolleyController : MonoBehaviour
{
    [SerializeField] private ArenaBall ballPrefab;
    [SerializeField, Min(0.1f)] private float duration = 5f;
    [SerializeField, Min(0.1f)] private float ballSpeed = 12f;
    [SerializeField] private float ballHeight = 1.2f;

    private readonly List<ArenaBall> balls = new List<ArenaBall>();

    public event Action Completed;
    public bool IsResolving { get; private set; }
    public float TimeRemaining { get; private set; }
    public float Duration => duration;
    public IReadOnlyList<ArenaBall> ActiveBalls => balls;

    public void BeginVolley(ArenaPlayerMotor playerOne, ArenaPlayerMotor playerTwo)
    {
        if (ballPrefab == null)
            throw new InvalidOperationException("Assign the arena ball prefab before firing.");

        Cancel();
        Physics.SyncTransforms();
        Spawn(playerOne, "P1 Ball", new Color(0.08f, 0.72f, 0.96f));
        Spawn(playerTwo, "P2 Ball", new Color(1f, 0.43f, 0.12f));
        TimeRemaining = duration;
        IsResolving = true;
    }

    private void FixedUpdate()
    {
        Advance(Time.fixedDeltaTime);
    }

    public void Advance(float deltaTime)
    {
        if (!IsResolving)
            return;

        float step = Mathf.Min(Mathf.Max(0f, deltaTime), TimeRemaining);
        foreach (ArenaBall ball in balls)
            if (ball != null)
                ball.Advance(step);
        TimeRemaining = Mathf.Max(0f, TimeRemaining - step);

        if (TimeRemaining <= 0.00001f)
        {
            IsResolving = false;
            TimeRemaining = 0f;
            ClearBalls();
            Completed?.Invoke();
        }
    }

    public void Cancel()
    {
        IsResolving = false;
        TimeRemaining = 0f;
        ClearBalls();
    }

    private void Spawn(ArenaPlayerMotor shooter, string name, Color color)
    {
        // Start inside the shooter's footprint so a nearby wall cannot put the
        // muzzle outside the arena. The ball ignores its shooter until clear.
        Vector3 position = shooter.transform.position;
        position.y = ballHeight;
        ArenaBall ball = Instantiate(ballPrefab, position, Quaternion.identity);
        ball.name = name;
        ball.Initialize(shooter, position, shooter.AimDirection, ballSpeed, color);
        balls.Add(ball);
    }

    private void ClearBalls()
    {
        foreach (ArenaBall ball in balls)
        {
            if (ball == null)
                continue;
            ball.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(ball.gameObject);
            else
                DestroyImmediate(ball.gameObject);
        }
        balls.Clear();
    }

    private void OnDisable()
    {
        Cancel();
    }
}
