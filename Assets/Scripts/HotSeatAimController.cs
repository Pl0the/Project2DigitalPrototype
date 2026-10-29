using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class HotSeatAimController : MonoBehaviour
{
    public enum Phase { Overview, Handoff, Aiming, Resolving, Locked, MatchOver, Setup, CardHandoff, Hand }
    public enum MatchResult { InProgress, PlayerOneWins, PlayerTwoWins, Draw }

    [SerializeField] private ArenaPlayerMotor playerOne;
    [SerializeField] private ArenaPlayerMotor playerTwo;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ArenaVolleyController volleyController;
    [SerializeField] private ArenaCardController cardController;
    [SerializeField] private ArenaBlockPlacement blockPlacement;
    [SerializeField] private ArenaCloneController cloneController;
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
    private int pendingBlockCard = -1;
    private int pendingCloneCard = -1;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;

    public Phase CurrentPhase { get; private set; }
    public int ActivePlayerIndex { get; private set; } = -1;
    public int RoundNumber { get; private set; } = 1;
    public bool IsPaused => paused;
    public MatchResult Result { get; private set; }
    public ArenaPlayerHealth PlayerOneHealth { get; private set; }
    public ArenaPlayerHealth PlayerTwoHealth { get; private set; }
    public ArenaCardController Cards => cardController;
    public ArenaBlockPlacement Placement => blockPlacement;
    public ArenaCloneController Clones => cloneController;
    private ArenaPlayerMotor ActivePlayer => ActivePlayerIndex == 0 ? playerOne : playerTwo;
    private Color TurnAccent => ActivePlayerIndex >= 0 ? ArenaTeamColors.Accent(ActivePlayerIndex) : new Color(0.55f, 0.65f, 0.75f);
    private Color TurnSurface => ActivePlayerIndex >= 0 ? ArenaTeamColors.Surface(ActivePlayerIndex) : new Color(0.035f, 0.045f, 0.06f);
    private string TurnName => "P" + (ActivePlayerIndex + 1) + " " + ArenaTeamColors.Name(ActivePlayerIndex);

    private void Awake()
    {
        if (playerOne == null || playerTwo == null || viewCamera == null || volleyController == null || cardController == null || blockPlacement == null || cloneController == null)
        {
            Debug.LogError("Assign both players, the camera, and the volley, card, block, and clone controllers.", this);
            enabled = false;
            return;
        }

        PlayerOneHealth = playerOne.GetComponent<ArenaPlayerHealth>();
        PlayerTwoHealth = playerTwo.GetComponent<ArenaPlayerHealth>();
        if (PlayerOneHealth == null || PlayerTwoHealth == null)
        {
            Debug.LogError("Add arena health to both players before starting the match.", this);
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
        cardController.ResetMatch();
        ShowOverview(Phase.Overview);
    }

    private void OnEnable()
    {
        if (volleyController != null)
            volleyController.Completed += CompleteVolley;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (CurrentPhase == Phase.Overview && keyboard.enterKey.wasPressedThisFrame)
            BeginSetupPhase();
        else if (CurrentPhase == Phase.Locked && keyboard.enterKey.wasPressedThisFrame)
            BeginNextRound();
        else if (CurrentPhase == Phase.MatchOver && keyboard.enterKey.wasPressedThisFrame)
            Restart();
        else if (CurrentPhase == Phase.Handoff && keyboard.spaceKey.wasPressedThisFrame)
            Ready();
        else if (CurrentPhase == Phase.Setup)
        {
            if (keyboard.tabKey.wasPressedThisFrame)
                EndSetup();
            else if (keyboard.hKey.wasPressedThisFrame)
                OpenHand();
            else if (pendingCloneCard >= 0)
                HandleCloneInput(keyboard);
            else if (pendingBlockCard >= 0)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    CancelBlockPlacement();
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                    ConfirmBlockPlacement();
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && blockPlacement.IsBoardPointer(Mouse.current.position.ReadValue()))
                {
                    blockPlacement.UpdatePointerPosition(Mouse.current.position.ReadValue());
                    ConfirmBlockPlacement();
                }
            }
        }
        else if (CurrentPhase == Phase.CardHandoff)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
                RevealHand();
            else if (keyboard.escapeKey.wasPressedThisFrame)
                ShowSetupView();
        }
        else if (CurrentPhase == Phase.Hand)
        {
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame)
                ShowSetupView();
            else if (keyboard.tabKey.wasPressedThisFrame)
                EndSetup();
            else
            {
                for (int i = 0; i < 6; i++)
                    if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    {
                        SelectCard(i);
                        break;
                    }
            }
        }
        else if (CurrentPhase == Phase.Aiming)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
            else if (!paused && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                ConfirmAim();
        }
    }

    private void HandleCloneInput(Keyboard keyboard)
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
            CancelBlockPlacement();
        else if (keyboard.bKey.wasPressedThisFrame)
            cloneController.ChooseBody();
        else if (keyboard.kKey.wasPressedThisFrame)
            cloneController.ChooseBlock();
        else if (cloneController.Mode == ArenaCloneController.PlacementMode.ChooseBlock)
        {
            if (keyboard.qKey.wasPressedThisFrame)
                cloneController.CycleSource(-1);
            else if (keyboard.eKey.wasPressedThisFrame)
                cloneController.CycleSource(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                cloneController.BeginBlockCopy();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                cloneController.ChooseSourceAt(Mouse.current.position.ReadValue());
        }
        else if (cloneController.Mode == ArenaCloneController.PlacementMode.ChooseTarget)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                cloneController.ChooseSourceAt(Mouse.current.position.ReadValue());
        }
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            ConfirmClonePlacement();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && blockPlacement.IsBoardPointer(Mouse.current.position.ReadValue()))
        {
            cloneController.UpdatePointerPosition(Mouse.current.position.ReadValue());
            ConfirmClonePlacement();
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
        if (CurrentPhase != Phase.Setup || !cardController.IsSetupComplete)
            return;

        confirmedPlayers = 0;
        // Whoever placed first gets the last opportunity to move and aim.
        ShowHandoff(1 - firstPlacementPlayer);
    }

    public void BeginSetupPhase()
    {
        if (CurrentPhase != Phase.Overview)
            return;
        cardController.BeginRound(RoundNumber, firstPlacementPlayer);
        ShowSetupView();
    }

    public void OpenHand()
    {
        if (CurrentPhase != Phase.Setup)
            return;
        CancelBlockPlacement();
        CurrentPhase = Phase.CardHandoff;
        viewCamera.cullingMask = 0;
    }

    public void RevealHand()
    {
        if (CurrentPhase == Phase.CardHandoff)
            CurrentPhase = Phase.Hand;
    }

    public void SelectCard(int index)
    {
        if (CurrentPhase != Phase.Hand || index < 0 || index >= cardController.GetHand(ActivePlayerIndex).Count)
            return;
        if (cardController.GetHand(ActivePlayerIndex)[index] == ArenaCardKind.Block)
        {
            pendingBlockCard = index;
            ShowSetupView();
            blockPlacement.BeginPlacement(ActivePlayerIndex);
        }
        else if (cardController.GetHand(ActivePlayerIndex)[index] == ArenaCardKind.Clone)
        {
            pendingCloneCard = index;
            ShowSetupView();
            cloneController.BeginClone(ActivePlayerIndex);
        }
        else
        {
            cardController.PlayCard(index);
            AfterSetupAction();
        }
    }

    public void ConfirmBlockPlacement()
    {
        if (CurrentPhase != Phase.Setup || pendingBlockCard < 0 || !blockPlacement.ConfirmPlacement())
            return;
        cardController.PlayCard(pendingBlockCard);
        pendingBlockCard = -1;
        AfterSetupAction();
    }

    public void CancelBlockPlacement()
    {
        cloneController.CancelPlacement();
        blockPlacement.CancelPlacement();
        pendingBlockCard = -1;
        pendingCloneCard = -1;
    }

    public void ConfirmClonePlacement()
    {
        if (CurrentPhase != Phase.Setup || pendingCloneCard < 0 || !cloneController.ConfirmPlacement())
            return;
        cardController.PlayCard(pendingCloneCard);
        pendingCloneCard = -1;
        AfterSetupAction();
    }

    public void EndSetup()
    {
        if (CurrentPhase != Phase.Setup && CurrentPhase != Phase.Hand && CurrentPhase != Phase.CardHandoff)
            return;
        CancelBlockPlacement();
        cardController.EndSetup();
        AfterSetupAction();
    }

    private void AfterSetupAction()
    {
        ShowSetupView();
        if (!cardController.IsSetupComplete)
            return;
        if (cardController.FirstPlayerToPlay >= 0)
            firstPlacementPlayer = cardController.FirstPlayerToPlay;
        BeginAimingPhase();
    }

    private void ShowSetupView()
    {
        ShowOverview(Phase.Setup);
        ActivePlayerIndex = cardController.ActivePlayerIndex;
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
        cloneController.SetAimMarkersVisible(1 - ActivePlayerIndex, false);
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
        {
            ShowOverview(Phase.Resolving);
            volleyController.BeginVolley(playerOne, playerTwo, cardController.GetAttackCount(0), cardController.GetAttackCount(1));
        }
    }

    private void CompleteVolley()
    {
        if (CurrentPhase != Phase.Resolving)
            return;

        // Resolve the entire simultaneous volley before deciding the winner.
        // Both players can still be hit after the first knockout, allowing a draw.
        bool oneDefeated = PlayerOneHealth.IsDefeated;
        bool twoDefeated = PlayerTwoHealth.IsDefeated;
        Result = oneDefeated && twoDefeated ? MatchResult.Draw
            : oneDefeated ? MatchResult.PlayerTwoWins
            : twoDefeated ? MatchResult.PlayerOneWins
            : MatchResult.InProgress;
        cloneController.ExpireClones();
        ShowOverview(Result == MatchResult.InProgress ? Phase.Locked : Phase.MatchOver);
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
        volleyController.Cancel();
        cloneController.ExpireClones();
        blockPlacement.ResetBoard();
        cardController.ResetMatch();
        pendingBlockCard = -1;
        pendingCloneCard = -1;
        StopBothPlayers();
        playerOne.ResetPose(playerOneStart, playerOneRotation);
        playerTwo.ResetPose(playerTwoStart, playerTwoRotation);
        PlayerOneHealth.ResetHealth();
        PlayerTwoHealth.ResetHealth();
        Result = MatchResult.InProgress;
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
        cloneController.SetAimMarkersVisible(0, true);
        cloneController.SetAimMarkersVisible(1, true);
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
        if (cloneController != null)
            cloneController.CancelPlacement();
        if (blockPlacement != null)
            blockPlacement.CancelPlacement();
        if (volleyController != null)
        {
            volleyController.Completed -= CompleteVolley;
            volleyController.Cancel();
        }
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
        if (PlayerOneHealth == null || PlayerTwoHealth == null)
            return;

        EnsureStyles();
        titleStyle.normal.textColor = ActivePlayerIndex >= 0 ? Color.Lerp(TurnAccent, Color.white, 0.25f) : Color.white;
        bool privateView = CurrentPhase == Phase.Handoff || CurrentPhase == Phase.CardHandoff || CurrentPhase == Phase.Hand || (CurrentPhase == Phase.Aiming && paused);
        if (privateView)
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), ArenaTeamColors.Background(ActivePlayerIndex));

        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 640f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (ActivePlayerIndex >= 0)
        {
            Fill(new Rect(0f, 0f, 5f, height), TurnAccent);
            Fill(new Rect(width - 5f, 0f, 5f, height), TurnAccent);
        }

        if (!privateView)
        {
            DrawBlockDurability(scale);
            DrawCloneLabels(scale);
        }

        if (CurrentPhase == Phase.CardHandoff)
        {
            Rect panel = CenterPanel(width, height);
            Panel(panel, TurnName + " TEAM", "Private hand.\nOther player: look away before revealing cards.");
            if (ThemedButton(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "Reveal hand  [Space]", buttonStyle))
                RevealHand();
        }
        else if (CurrentPhase == Phase.Hand)
            DrawHand(width, height);
        else if (CurrentPhase == Phase.Setup)
            DrawSetup(width, height);
        else if (CurrentPhase == Phase.Handoff)
        {
            Rect panel = CenterPanel(width, height);
            Panel(panel, TurnName + " TEAM", "Your turn to move and aim.\nOther player: look away before continuing.");
            if (ThemedButton(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "Ready  [Space]", buttonStyle))
                Ready();
        }
        else if (CurrentPhase == Phase.Aiming && paused)
        {
            Rect panel = CenterPanel(width, height);
            Panel(panel, TurnName + "  |  PAUSED", "The mouse is released.\nResume when you are ready to keep aiming.");
            if (ThemedButton(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "Resume  [Esc]", buttonStyle))
                SetPaused(false);
        }
        else if (CurrentPhase == Phase.Aiming)
        {
            Header(width, TurnName + "  |  MOVE & AIM", "WASD: move    Mouse: aim    Enter: confirm    Esc: pause");
            Fill(new Rect(width * 0.5f - 8f, height * 0.5f - 1f, 16f, 2f), Color.white);
            Fill(new Rect(width * 0.5f - 1f, height * 0.5f - 8f, 2f, 16f), Color.white);
        }
        else if (CurrentPhase == Phase.Resolving)
        {
            Header(width, "ROUND " + RoundNumber + "  |  VOLLEY " + volleyController.VolleyNumber + " / " + volleyController.VolleyCount, volleyController.TimeRemaining.ToString("0.0") + "s remaining  |  Movement and aim are locked");
            Fill(new Rect(20f, height - 38f, width - 40f, 12f), new Color(0.12f, 0.15f, 0.2f));
            float fraction = Mathf.Clamp01(volleyController.TimeRemaining / volleyController.Duration);
            Fill(new Rect(20f, height - 38f, (width - 40f) * fraction, 12f), new Color(0.35f, 0.8f, 0.95f));
        }
        else if (CurrentPhase == Phase.MatchOver)
        {
            Header(width, "MATCH COMPLETE", "The full volley has finished. Both players are locked.");
            Rect panel = CenterPanel(width, height);
            string resultTitle = Result == MatchResult.Draw ? "DRAW" : Result == MatchResult.PlayerOneWins ? "PLAYER 1 WINS" : "PLAYER 2 WINS";
            string resultDetail = Result == MatchResult.Draw ? "Both players were knocked out in the same volley." : "The other player has no health remaining.";
            Panel(panel, resultTitle, resultDetail);
            if (ThemedButton(new Rect(panel.x + 50f, panel.y + 182f, 420f, 48f), "New match  [Enter]", buttonStyle))
                Restart();
        }
        else
        {
            bool locked = CurrentPhase == Phase.Locked;
            string title = locked ? "ROUND COMPLETE" : "ROUND " + RoundNumber + "  |  OVERVIEW";
            string detail = locked ? (volleyController.VolleyCount == 0 ? "No Attack cards played. No shots fired." : "Shots cleared. Health and surviving blocks carry over.") : "P" + (firstPlacementPlayer + 1) + " starts setup  |  The first player to play a card aims last";
            Header(width, title, detail);
            if (locked)
            {
                if (ThemedButton(new Rect(width - 280f, height - 66f, 260f, 46f), "Next round  [Enter]", buttonStyle))
                    BeginNextRound();
            }
            else
            {
                if (ThemedButton(new Rect(20f, height - 66f, 300f, 46f), "First to place: P" + (firstPlacementPlayer + 1), buttonStyle))
                    SetFirstPlacementPlayer(1 - firstPlacementPlayer);
                if (ThemedButton(new Rect(width - 280f, height - 66f, 260f, 46f), "Start setup  [Enter]", buttonStyle))
                    BeginSetupPhase();
            }
            if (locked && ThemedButton(new Rect(20f, height - 66f, 220f, 46f), "Restart", buttonStyle))
                Restart();
        }

        if (!privateView)
            DrawHealth(width);
        GUI.matrix = previousMatrix;
    }

    private void DrawSetup(float width, float height)
    {
        if (pendingCloneCard >= 0)
        {
            DrawCloneSetup(width, height);
            return;
        }
        bool placing = pendingBlockCard >= 0;
        string title = "ROUND " + RoundNumber + "  |  " + TurnName + (placing ? " PLACE BLOCK" : " PLAY ONE CARD");
        string detail = "Queued shots: P1 " + cardController.GetAttackCount(0) + "  /  P2 " + cardController.GetAttackCount(1);
        Header(width, title, detail);
        TurnPanel(new Rect(16f, height - 124f, width - 32f, 112f));
        string message = placing ? "Mouse / WASD: place  |  Q / E / scroll: rotate  |  Green: valid  |  Tab: finish setup" : cardController.LastAction;
        if (string.IsNullOrEmpty(message))
            message = "Public board. Reveal your hand privately, play one card, then pass the computer.";
        GUI.Label(new Rect(26f, height - 116f, width - 52f, 36f), message, textStyle);
        if (placing)
        {
            if (ThemedButton(new Rect(26f, height - 68f, 180f, 46f), "Cancel  [Esc]", buttonStyle))
                CancelBlockPlacement();
            if (ThemedButton(new Rect(222f, height - 68f, 110f, 46f), "-15  [Q]", buttonStyle))
                blockPlacement.RotatePreview(-15f);
            if (ThemedButton(new Rect(344f, height - 68f, 110f, 46f), "+15  [E]", buttonStyle))
                blockPlacement.RotatePreview(15f);
            bool previous = GUI.enabled;
            GUI.enabled = blockPlacement.IsPreviewValid;
            if (ThemedButton(new Rect(width - 280f, height - 68f, 254f, 46f), "Place  [Enter]", buttonStyle))
                ConfirmBlockPlacement();
            GUI.enabled = previous;
        }
        else
        {
            if (ThemedButton(new Rect(26f, height - 68f, 240f, 46f), "Private hand  [H]", buttonStyle))
                OpenHand();
            string status = cardController.HasFinished(1 - ActivePlayerIndex) ? "Opponent finished. Keep playing if you want." : "One card per turn. Keep any cards you do not use.";
            GUI.Label(new Rect(280f, height - 74f, width - 570f, 60f), status, textStyle);
            if (ThemedButton(new Rect(width - 280f, height - 68f, 254f, 46f), "Finish setup  [Tab]", buttonStyle))
                EndSetup();
        }
    }

    private void DrawHand(float width, float height)
    {
        var hand = cardController.GetHand(ActivePlayerIndex);
        Header(width, TurnName + "  |  PRIVATE HAND", hand.Count + " / " + cardController.HandLimit + " cards  |  Newest cards discarded this round: " + cardController.GetDiscardedCount(ActivePlayerIndex));
        GUI.Label(new Rect(24f, 140f, width - 48f, 60f), "Choose one card. Unplayed cards stay in your hand for later rounds.", textStyle);
        float slotWidth = Mathf.Min(150f, (width - 48f) / 6f);
        float left = (width - hand.Count * slotWidth) * 0.5f;
        for (int i = 0; i < hand.Count; i++)
        {
            string text = hand[i] == ArenaCardKind.Attack ? "ATTACK\n\n1 shot" : hand[i] == ArenaCardKind.Block ? "BLOCK\n\n3 HP wall" : "CLONE\n\nBody / wall\n1 round";
            if (ThemedButton(new Rect(left + i * slotWidth + 4f, height * 0.5f - 70f, slotWidth - 8f, 180f), text + "\n\n[" + (i + 1) + "]", buttonStyle))
            {
                SelectCard(i);
                break;
            }
        }
        if (ThemedButton(new Rect(24f, height - 70f, 240f, 46f), "Back to board  [Esc]", buttonStyle))
            ShowSetupView();
        if (ThemedButton(new Rect(width - 280f, height - 70f, 256f, 46f), "Finish setup  [Tab]", buttonStyle))
            EndSetup();
    }

    private void DrawBlockDurability(float scale)
    {
        ArenaBlock source = cloneController.Mode == ArenaCloneController.PlacementMode.ChooseBlock ? cloneController.SelectedSource : null;
        foreach (ArenaBlock block in blockPlacement.Blocks)
        {
            if (block == null || block.IsBroken || !block.gameObject.activeInHierarchy)
                continue;
            Vector3 target = block.transform.position;
            if (!viewCamera.orthographic && Physics.Linecast(viewCamera.transform.position, target, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ArenaBlock>() != block)
                continue;
            Vector3 screen = viewCamera.WorldToScreenPoint(target + Vector3.up * 1.15f);
            if (screen.z <= 0f)
                continue;
            float labelWidth = block == source ? 156f : 110f;
            Rect label = new Rect(screen.x / scale - labelWidth * 0.5f, (Screen.height - screen.y) / scale - 14f, labelWidth, 28f);
            Fill(label, new Color(0.015f, 0.02f, 0.03f, 0.95f));
            GUI.Label(label, (block == source ? "SOURCE " : block.IsTemporary ? "C " : "") + block.RemainingDurability + " / " + block.MaximumDurability, textStyle);
        }
    }

    private void DrawCloneSetup(float width, float height)
    {
        ArenaCloneController.PlacementMode mode = cloneController.Mode;
        bool body = mode == ArenaCloneController.PlacementMode.Body;
        bool block = mode == ArenaCloneController.PlacementMode.Block;
        bool selecting = mode == ArenaCloneController.PlacementMode.ChooseBlock;
        string title = body ? "PLACE BODY CLONE" : block ? "PLACE CLONED BLOCK" : selecting ? "CHOOSE SOURCE BLOCK" : "CHOOSE CLONE TARGET";
        Header(width, TurnName + "  |  " + title, "All clones expire after this round's final volley.");
        TurnPanel(new Rect(16f, height - 124f, width - 32f, 112f));
        ArenaBlock source = selecting ? cloneController.SelectedSource : null;
        string message = body ? "Mouse / WASD: place  |  Copies your movement and aim  |  Hit once: disappears"
            : block ? "Mouse / WASD: place  |  Q / E / scroll: rotate  |  Copies the source's remaining durability"
            : selecting ? "Source " + cloneController.SourceNumber + " / " + cloneController.SourceCount + (source == null ? "" : "  |  " + source.RemainingDurability + " / 3 durability") + "  |  Click a block or use Q / E"
            : "Choose your body or an owned block. The Clone card is spent only after a valid placement.";
        GUI.Label(new Rect(26f, height - 116f, width - 52f, 36f), message, textStyle);
        if (ThemedButton(new Rect(26f, height - 68f, 180f, 46f), "Cancel  [Esc]", buttonStyle))
            CancelBlockPlacement();
        if (body || block)
        {
            if (block)
            {
                if (ThemedButton(new Rect(222f, height - 68f, 110f, 46f), "-15  [Q]", buttonStyle))
                    blockPlacement.RotatePreview(-15f);
                if (ThemedButton(new Rect(344f, height - 68f, 110f, 46f), "+15  [E]", buttonStyle))
                    blockPlacement.RotatePreview(15f);
            }
            bool previous = GUI.enabled;
            GUI.enabled = cloneController.IsPlacementValid;
            if (ThemedButton(new Rect(width - 280f, height - 68f, 254f, 46f), "Place clone  [Enter]", buttonStyle))
                ConfirmClonePlacement();
            GUI.enabled = previous;
        }
        else if (selecting)
        {
            if (ThemedButton(new Rect(222f, height - 68f, 110f, 46f), "Prev [Q]", buttonStyle))
                cloneController.CycleSource(-1);
            if (ThemedButton(new Rect(344f, height - 68f, 110f, 46f), "Next [E]", buttonStyle))
                cloneController.CycleSource(1);
            bool previous = GUI.enabled;
            GUI.enabled = source != null;
            if (ThemedButton(new Rect(width - 280f, height - 68f, 254f, 46f), "Copy block  [Enter]", buttonStyle))
                cloneController.BeginBlockCopy();
            GUI.enabled = previous;
        }
        else
        {
            if (ThemedButton(new Rect(222f, height - 68f, 230f, 46f), "Clone body  [B]", buttonStyle))
                cloneController.ChooseBody();
            bool previous = GUI.enabled;
            GUI.enabled = cloneController.SourceCount > 0;
            if (ThemedButton(new Rect(width - 280f, height - 68f, 254f, 46f), "Clone block  [K]", buttonStyle))
                cloneController.ChooseBlock();
            GUI.enabled = previous;
        }
    }

    private void DrawCloneLabels(float scale)
    {
        foreach (ArenaCloneBody clone in cloneController.Bodies)
        {
            if (clone == null || !clone.IsAlive)
                continue;
            Vector3 target = clone.transform.position;
            if (!viewCamera.orthographic && Physics.Linecast(viewCamera.transform.position, target, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ArenaCloneBody>() != clone)
                continue;
            Vector3 screen = viewCamera.WorldToScreenPoint(target + Vector3.up * 1.2f);
            if (screen.z <= 0f)
                continue;
            Rect label = new Rect(screen.x / scale - 40f, (Screen.height - screen.y) / scale - 14f, 80f, 28f);
            Fill(label, new Color(0.015f, 0.02f, 0.03f, 0.95f));
            GUI.Label(label, "CLONE", textStyle);
        }
    }

    private void DrawHealth(float width)
    {
        float panelWidth = (width - 48f) * 0.5f;
        HealthPanel(new Rect(16f, 108f, panelWidth, 50f), 0, PlayerOneHealth);
        HealthPanel(new Rect(32f + panelWidth, 108f, panelWidth, 50f), 1, PlayerTwoHealth);
    }

    private void HealthPanel(Rect panel, int player, ArenaPlayerHealth health)
    {
        Color teamColor = ArenaTeamColors.Accent(player);
        bool active = ActivePlayerIndex == player;
        Fill(panel, health.WasRecentlyHit ? new Color(0.5f, 0.08f, 0.08f, 0.96f) : ArenaTeamColors.Surface(player));
        if (active)
            Fill(new Rect(panel.x, panel.y, panel.width, 3f), teamColor);
        string playerName = "P" + (player + 1) + " " + ArenaTeamColors.Name(player);
        GUI.Label(new Rect(panel.x + 8f, panel.y + 3f, panel.width - 16f, 28f), playerName + "  |  " + health.CurrentHealth + " / " + health.MaximumHealth + " HP" + (active ? "  |  YOUR TURN" : ""), textStyle);
        float segmentWidth = (panel.width - 24f) / health.MaximumHealth;
        for (int i = 0; i < health.MaximumHealth; i++)
            Fill(new Rect(panel.x + 12f + i * segmentWidth, panel.y + 36f, segmentWidth - 4f, 7f), i < health.CurrentHealth ? teamColor : new Color(0.19f, 0.22f, 0.27f));
    }

    private void Header(float width, string title, string detail)
    {
        TurnPanel(new Rect(16f, 12f, width - 32f, 88f));
        GUI.Label(new Rect(30f, 18f, width - 60f, 38f), title, titleStyle);
        GUI.Label(new Rect(30f, 58f, width - 60f, 30f), detail, textStyle);
    }

    private void Panel(Rect panel, string title, string detail)
    {
        TurnPanel(panel);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 25f, panel.width - 50f, 45f), title, titleStyle);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 85f, panel.width - 50f, 65f), detail, textStyle);
    }

    private void TurnPanel(Rect panel)
    {
        Fill(panel, TurnSurface);
        Fill(new Rect(panel.x, panel.y, panel.width, 4f), TurnAccent);
    }

    private bool ThemedButton(Rect rect, string text, GUIStyle style)
    {
        bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
        Color border = GUI.enabled ? TurnAccent : new Color(0.24f, 0.27f, 0.32f);
        Color background = GUI.enabled ? Color.Lerp(TurnSurface, TurnAccent, hovered ? 0.5f : 0.25f) : new Color(0.075f, 0.085f, 0.1f);
        Fill(rect, border);
        Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), background);
        return GUI.Button(rect, text, style);
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
        buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        buttonStyle.normal.textColor = Color.white;
    }
}
