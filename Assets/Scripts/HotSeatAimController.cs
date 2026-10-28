using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class HotSeatAimController : MonoBehaviour
{
    public enum Phase { Overview, Handoff, Aiming, Locked }

    [SerializeField] private ArenaPlayerMotor playerOne;
    [SerializeField] private ArenaPlayerMotor playerTwo;
    [SerializeField] private Camera viewCamera;
    [SerializeField, Range(40f, 100f)] private float firstPersonFieldOfView = 75f;

    private Vector3 overviewPosition;
    private Quaternion overviewRotation;
    private float overviewNearClip;
    private int worldCullingMask;
    private Vector3 playerOneStart;
    private Vector3 playerTwoStart;
    private Quaternion playerOneRotation;
    private Quaternion playerTwoRotation;
    private int firstPlacementPlayer;
    private int confirmedPlayers;
    private bool paused;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;

    public Phase CurrentPhase { get; private set; }
    public int ActivePlayerIndex { get; private set; } = -1;
    public int RoundNumber { get; private set; } = 1;
    public bool IsPaused => paused;
    private ArenaPlayerMotor ActivePlayer => ActivePlayerIndex == 0 ? playerOne : playerTwo;

    private void Awake()
    {
        if (playerOne == null || playerTwo == null || viewCamera == null)
        {
            Debug.LogError("Assign both arena players and the view camera.", this);
            enabled = false;
            return;
        }

        overviewPosition = viewCamera.transform.position;
        overviewRotation = viewCamera.transform.rotation;
        overviewNearClip = viewCamera.nearClipPlane;
        worldCullingMask = viewCamera.cullingMask;
        playerOneStart = playerOne.transform.position;
        playerTwoStart = playerTwo.transform.position;
        playerOneRotation = playerOne.transform.rotation;
        playerTwoRotation = playerTwo.transform.rotation;
        ShowOverview(Phase.Overview);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (CurrentPhase == Phase.Handoff && keyboard.spaceKey.wasPressedThisFrame)
            Ready();
        else if (CurrentPhase == Phase.Aiming)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
            else if (!paused && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                ConfirmAim();
        }
    }

    private void LateUpdate()
    {
        if (CurrentPhase == Phase.Aiming)
            FollowActivePlayer();
    }

    public void SetFirstPlacementPlayer(int playerIndex)
    {
        if (CurrentPhase == Phase.Overview && (playerIndex == 0 || playerIndex == 1))
            firstPlacementPlayer = playerIndex;
    }

    public void BeginAimingPhase()
    {
        if (CurrentPhase != Phase.Overview)
            return;

        confirmedPlayers = 0;
        // Whoever placed first gets the last opportunity to move and aim.
        ShowHandoff(1 - firstPlacementPlayer);
    }

    public void Ready()
    {
        if (CurrentPhase != Phase.Handoff)
            return;

        CurrentPhase = Phase.Aiming;
        paused = false;
        viewCamera.cullingMask = worldCullingMask;
        viewCamera.orthographic = false;
        viewCamera.fieldOfView = firstPersonFieldOfView;
        viewCamera.nearClipPlane = 0.05f;
        ActivePlayer.SetBodyVisible(false);
        ArenaPlayerMotor opponent = ActivePlayerIndex == 0 ? playerTwo : playerOne;
        opponent.SetAimMarkerVisible(false);
        ActivePlayer.SetControlEnabled(true);
        FollowActivePlayer();
        SetCursorCaptured(true);
    }

    public void ConfirmAim()
    {
        if (CurrentPhase != Phase.Aiming || paused)
            return;

        confirmedPlayers++;
        if (confirmedPlayers == 1)
            ShowHandoff(firstPlacementPlayer);
        else
            ShowOverview(Phase.Locked);
    }

    public void BeginNextRound()
    {
        if (CurrentPhase != Phase.Locked)
            return;

        RoundNumber++;
        ShowOverview(Phase.Overview);
    }

    public void Restart()
    {
        StopBothPlayers();
        playerOne.ResetPose(playerOneStart, playerOneRotation);
        playerTwo.ResetPose(playerTwoStart, playerTwoRotation);
        firstPlacementPlayer = 0;
        confirmedPlayers = 0;
        RoundNumber = 1;
        ShowOverview(Phase.Overview);
    }

    private void ShowHandoff(int nextPlayer)
    {
        StopBothPlayers();
        paused = false;
        ActivePlayerIndex = nextPlayer;
        CurrentPhase = Phase.Handoff;
        // The world is not rendered while the computer changes hands.
        viewCamera.cullingMask = 0;
        SetCursorCaptured(false);
    }

    private void ShowOverview(Phase phase)
    {
        StopBothPlayers();
        paused = false;
        ActivePlayerIndex = -1;
        CurrentPhase = phase;
        viewCamera.cullingMask = worldCullingMask;
        viewCamera.orthographic = true;
        viewCamera.nearClipPlane = overviewNearClip;
        viewCamera.transform.SetPositionAndRotation(overviewPosition, overviewRotation);
        SetCursorCaptured(false);
    }

    private void StopBothPlayers()
    {
        playerOne.SetControlEnabled(false);
        playerTwo.SetControlEnabled(false);
        playerOne.SetBodyVisible(true);
        playerTwo.SetBodyVisible(true);
    }

    private void FollowActivePlayer()
    {
        viewCamera.transform.SetPositionAndRotation(ActivePlayer.EyePosition, ActivePlayer.transform.rotation);
    }

    private void SetPaused(bool value)
    {
        paused = value;
        ActivePlayer.SetControlEnabled(!value);
        SetCursorCaptured(!value);
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && CurrentPhase == Phase.Aiming)
            SetPaused(true);
    }

    private void OnDisable()
    {
        if (playerOne != null)
        {
            playerOne.SetControlEnabled(false);
            playerOne.SetBodyVisible(true);
        }
        if (playerTwo != null)
        {
            playerTwo.SetControlEnabled(false);
            playerTwo.SetBodyVisible(true);
        }
        SetCursorCaptured(false);
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (CurrentPhase == Phase.Handoff || (CurrentPhase == Phase.Aiming && paused))
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.015f, 0.02f, 0.03f, 1f));

        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 640f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (CurrentPhase == Phase.Handoff)
        {
            Rect panel = CenterPanel(width, height);
            Panel(panel, "PLAYER " + (ActivePlayerIndex + 1), "Your turn to move and aim.\nOther player: look away before continuing.");
            if (GUI.Button(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "Ready  [Space]", buttonStyle))
                Ready();
        }
        else if (CurrentPhase == Phase.Aiming && paused)
        {
            Rect panel = CenterPanel(width, height);
            Panel(panel, "PAUSED", "The mouse is released.\nResume when you are ready to keep aiming.");
            if (GUI.Button(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "Resume  [Esc]", buttonStyle))
                SetPaused(false);
        }
        else if (CurrentPhase == Phase.Aiming)
        {
            Header(width, "P" + (ActivePlayerIndex + 1) + "  |  MOVE & AIM", "WASD: move    Mouse: aim    Enter: confirm    Esc: pause");
            Fill(new Rect(width * 0.5f - 8f, height * 0.5f - 1f, 16f, 2f), Color.white);
            Fill(new Rect(width * 0.5f - 1f, height * 0.5f - 8f, 2f, 16f), Color.white);
        }
        else
        {
            bool locked = CurrentPhase == Phase.Locked;
            string title = locked ? "BOTH PLAYERS READY" : "ROUND " + RoundNumber + "  |  OVERVIEW";
            string detail = locked ? "Positions and aim directions are locked." : "P" + (firstPlacementPlayer + 1) + " places first  |  Aim order: P" + (2 - firstPlacementPlayer) + " then P" + (firstPlacementPlayer + 1);
            Header(width, title, detail);
            if (locked)
            {
                if (GUI.Button(new Rect(width - 280f, height - 66f, 260f, 46f), "Next round", buttonStyle))
                    BeginNextRound();
            }
            else
            {
                if (GUI.Button(new Rect(20f, height - 66f, 300f, 46f), "First to place: P" + (firstPlacementPlayer + 1), buttonStyle))
                    SetFirstPlacementPlayer(1 - firstPlacementPlayer);
                if (GUI.Button(new Rect(width - 280f, height - 66f, 260f, 46f), "Start aiming", buttonStyle))
                    BeginAimingPhase();
            }
            if (locked && GUI.Button(new Rect(20f, height - 66f, 220f, 46f), "Restart", buttonStyle))
                Restart();
        }
        GUI.matrix = previousMatrix;
    }

    private void Header(float width, string title, string detail)
    {
        Fill(new Rect(16f, 12f, width - 32f, 88f), new Color(0.02f, 0.03f, 0.045f, 0.96f));
        GUI.Label(new Rect(30f, 18f, width - 60f, 38f), title, titleStyle);
        GUI.Label(new Rect(30f, 58f, width - 60f, 30f), detail, textStyle);
    }

    private void Panel(Rect panel, string title, string detail)
    {
        Fill(panel, new Color(0.065f, 0.085f, 0.12f));
        GUI.Label(new Rect(panel.x + 25f, panel.y + 25f, panel.width - 50f, 45f), title, titleStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 85f, panel.width - 50f, 65f), detail, textStyle);
    }

    private static Rect CenterPanel(float width, float height)
    {
        return new Rect((width - 520f) * 0.5f, (height - 260f) * 0.5f, 520f, 260f);
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        textStyle.normal.textColor = new Color(0.78f, 0.84f, 0.92f);
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
    }
}
