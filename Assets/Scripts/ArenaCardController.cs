using System.Collections.Generic;
using UnityEngine;

public enum ArenaCardKind { Attack, Block }

[DisallowMultipleComponent]
public sealed class ArenaCardController : MonoBehaviour
{
    [SerializeField, Min(1)] private int handLimit = 6;
    [SerializeField, Min(1)] private int cardsPerRound = 3;

    private readonly List<ArenaCardKind>[] hands = { new List<ArenaCardKind>(), new List<ArenaCardKind>() };
    private readonly int[] attacks = new int[2];
    private readonly bool[] finished = new bool[2];
    private readonly int[] discarded = new int[2];

    public int HandLimit => handLimit;
    public int ActivePlayerIndex { get; private set; } = -1;
    public int FirstPlayerToPlay { get; private set; } = -1;
    public bool IsSetupComplete => finished[0] && finished[1];
    public string LastAction { get; private set; } = "";

    public IReadOnlyList<ArenaCardKind> GetHand(int player) => hands[player];
    public int GetAttackCount(int player) => attacks[player];
    public bool HasFinished(int player) => finished[player];
    public int GetDiscardedCount(int player) => discarded[player];

    public void ResetMatch()
    {
        for (int player = 0; player < 2; player++)
        {
            hands[player].Clear();
            attacks[player] = 0;
            finished[player] = false;
            discarded[player] = 0;
        }
        ActivePlayerIndex = -1;
        FirstPlayerToPlay = -1;
        LastAction = "";
    }

    public void BeginRound(int round, int firstPlayer)
    {
        FirstPlayerToPlay = -1;
        LastAction = "";
        for (int player = 0; player < 2; player++)
        {
            attacks[player] = 0;
            finished[player] = false;
            discarded[player] = 0;
            if (round == 1)
            {
                // A predictable opening lets both players try shooting and placement.
                DrawCard(player, ArenaCardKind.Attack);
                DrawCard(player, ArenaCardKind.Block);
                DrawCard(player, ArenaCardKind.Attack);
                DrawCard(player, ArenaCardKind.Block);
                DrawCard(player, ArenaCardKind.Block);
            }
            else
            {
                // Draw the guaranteed attack first so overflow discards the newest cards.
                DrawCard(player, ArenaCardKind.Attack);
                for (int card = 1; card < cardsPerRound; card++)
                    DrawCard(player, Random.value < 0.5f ? ArenaCardKind.Attack : ArenaCardKind.Block);
            }
        }
        ActivePlayerIndex = Mathf.Clamp(firstPlayer, 0, 1);
    }

    public bool DrawCard(int player, ArenaCardKind kind)
    {
        if (hands[player].Count >= handLimit)
        {
            discarded[player]++;
            return false;
        }
        hands[player].Add(kind);
        return true;
    }

    public bool PlayCard(int index)
    {
        int player = ActivePlayerIndex;
        if (player < 0 || finished[player] || index < 0 || index >= hands[player].Count)
            return false;
        ArenaCardKind kind = hands[player][index];
        hands[player].RemoveAt(index);
        if (kind == ArenaCardKind.Attack)
            attacks[player]++;
        if (FirstPlayerToPlay < 0)
            FirstPlayerToPlay = player;
        LastAction = "P" + (player + 1) + " played " + kind.ToString().ToUpperInvariant() + ".";
        if (hands[player].Count == 0)
        {
            finished[player] = true;
            LastAction += " Their hand is empty.";
        }
        AdvanceTurn(player);
        return true;
    }

    public void EndSetup()
    {
        int player = ActivePlayerIndex;
        if (player < 0 || finished[player])
            return;
        finished[player] = true;
        LastAction = "P" + (player + 1) + " finished setup and kept their remaining cards.";
        AdvanceTurn(player);
    }

    private void AdvanceTurn(int player)
    {
        ActivePlayerIndex = IsSetupComplete ? -1 : finished[1 - player] ? player : 1 - player;
    }
}
