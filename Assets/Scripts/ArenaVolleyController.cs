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
    [SerializeField] private ArenaCloneController cloneController;

    private readonly List<ArenaBall> balls = new List<ArenaBall>();
    private ArenaPlayerMotor firstPlayer;
    private ArenaPlayerMotor secondPlayer;
    private int firstShots;
    private int secondShots;

    public event Action Completed;
    public bool IsResolving { get; private set; }
    public float TimeRemaining { get; private set; }
    public float Duration => duration;
    public IReadOnlyList<ArenaBall> ActiveBalls => balls;
    public int VolleyNumber { get; private set; }
    public int VolleyCount { get; private set; }

    public void BeginVolley(ArenaPlayerMotor playerOne, ArenaPlayerMotor playerTwo, int playerOneShots = 1, int playerTwoShots = 1)
    {
        if (ballPrefab == null)
            throw new InvalidOperationException("Assign the arena ball prefab before firing.");

        Cancel();
        firstPlayer = playerOne;
        secondPlayer = playerTwo;
        firstShots = Mathf.Max(0, playerOneShots);
        secondShots = Mathf.Max(0, playerTwoShots);
        VolleyCount = Mathf.Max(firstShots, secondShots);
        if (VolleyCount == 0)
        {
            Completed?.Invoke();
            return;
        }
        IsResolving = true;
        BeginBatch();
    }

    private void BeginBatch()
    {
        VolleyNumber++;
        Physics.SyncTransforms();
        if (VolleyNumber <= firstShots)
            Spawn(firstPlayer, "P1 Ball", new Color(0.08f, 0.72f, 0.96f));
        if (VolleyNumber <= secondShots)
            Spawn(secondPlayer, "P2 Ball", new Color(1f, 0.43f, 0.12f));
        TimeRemaining = duration;
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
            TimeRemaining = 0f;
            ClearBalls();
            if (VolleyNumber >= VolleyCount || firstPlayer.GetComponent<ArenaPlayerHealth>().IsDefeated || secondPlayer.GetComponent<ArenaPlayerHealth>().IsDefeated)
            {
                IsResolving = false;
                Completed?.Invoke();
            }
            else
                BeginBatch();
        }
    }

    public void Cancel()
    {
        IsResolving = false;
        TimeRemaining = 0f;
        VolleyNumber = 0;
        VolleyCount = 0;
        ClearBalls();
    }

    private void Spawn(ArenaPlayerMotor shooter, string name, Color color)
    {
        SpawnBody(shooter, shooter.transform.position, shooter.GetComponent<CharacterController>(), name, color);
        if (cloneController == null)
            return;
        foreach (ArenaCloneBody clone in cloneController.Bodies)
            if (clone != null && clone.IsAlive && clone.Owner == shooter)
                SpawnBody(shooter, clone.transform.position, clone.BodyCollider, name + " (Clone)", color);
    }

    private void SpawnBody(ArenaPlayerMotor shooter, Vector3 position, Collider body, string name, Color color)
    {
        // Start inside the shooter's footprint so a nearby wall cannot put the
        // muzzle outside the arena. The ball ignores its shooter until clear.
        position.y = ballHeight;
        ArenaBall ball = Instantiate(ballPrefab, position, Quaternion.identity);
        ball.name = name;
        ball.Initialize(shooter, position, shooter.AimDirection, ballSpeed, color, body);
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
