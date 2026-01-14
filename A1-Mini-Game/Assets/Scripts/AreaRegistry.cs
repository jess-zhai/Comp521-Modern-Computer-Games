using UnityEngine;
// a static script that tells which area the player is at
public enum AreaType { Forest, Cavity, Goal }

public static class AreaRegistry
{
    // default to forest at start of the game, as player spawns in fixed point
    public static AreaType Current { get; private set; } = AreaType.Forest;

    // counters to handle overlaps/edges
    static int _forestCount = 0;
    static int _goalCount = 0;

    public static void Enter(AreaType t)
    {
        if (t == AreaType.Forest) _forestCount++;
        else if (t == AreaType.Goal) _goalCount++;
        Recompute();
    }

    public static void Exit(AreaType t)
    {
        if (t == AreaType.Forest) _forestCount = Mathf.Max(0, _forestCount - 1);
        else if (t == AreaType.Goal) _goalCount = Mathf.Max(0, _goalCount - 1);
        Recompute();
    }

    static void Recompute()
    {
        // Detect Forest and Goal from hitting the boxes, and Cavity is neither Forest nor goal
        // used this way to define cavity instead of another trigger, is because the CavityArea object is for generation of platforms.
        // the user may not be colliding with the CavityArea when they're on the platforms.
        if (_goalCount > 0) Current = AreaType.Goal;
        else if (_forestCount > 0) Current = AreaType.Forest;
        else Current = AreaType.Cavity;
        // Debug.Log("Area: " + Current);
    }
}
