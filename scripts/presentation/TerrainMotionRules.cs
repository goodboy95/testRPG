namespace TownRpg.Presentation;

/// <summary>Opt-in terrain locomotion constants. No dependency on scene state or save data.</summary>
public static class TerrainMotionRules
{
    public const float Gravity = 18;
    public const float TerminalSpeed = 30;
    public const float FloorSnap = .35f;
    public const float MaximumSlopeDegrees = 35;
    public static float VerticalVelocity(float velocity, bool grounded, float delta) =>
        grounded ? -.4f : Math.Max(-TerminalSpeed, velocity - Gravity * Math.Max(0,delta));
    public static bool HeightAllowsInteraction(float playerFeet, float targetFeet) => Math.Abs(playerFeet-targetFeet) <= .85f;
}
