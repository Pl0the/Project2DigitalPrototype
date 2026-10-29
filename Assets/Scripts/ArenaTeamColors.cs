using UnityEngine;

public static class ArenaTeamColors
{
    public static Color Accent(int player) => player == 0
        ? new Color(0.2f, 0.65f, 1f)
        : new Color(1f, 0.26f, 0.32f);

    public static Color Clone(int player) => Color.Lerp(Accent(player), Color.white, 0.3f);
    public static Color Surface(int player) => Color.Lerp(new Color(0.025f, 0.03f, 0.045f), Accent(player), 0.18f);
    public static Color Background(int player) => Color.Lerp(new Color(0.012f, 0.016f, 0.025f), Accent(player), 0.07f);
    public static string Name(int player) => player == 0 ? "BLUE" : "RED";
}
