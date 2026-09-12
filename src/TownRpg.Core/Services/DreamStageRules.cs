namespace TownRpg.Core.Services;

public sealed record DreamStageObservation(bool UpperFrame, bool LowerFrame, bool Rails, bool SideMark, bool Image)
{
    public bool Aligned => UpperFrame && LowerFrame && Rails && SideMark && Image;
}

/// <summary>Constraint evaluation is shared by the live preview and final submission.</summary>
public static class DreamStageRules
{
    public static DreamStageObservation Observe(IReadOnlyList<string> values)
    {
        if (values.Count != 7) return new(false, false, false, false, false);
        var heights = new int[4];
        for (var index = 0; index < heights.Length; index++)
            if (!int.TryParse(values[index], out heights[index]) || heights[index] is < 1 or > 4)
                return new(false, false, false, false, false);
        var (a, b, c, d) = (heights[0], heights[1], heights[2], heights[3]);
        return new(a + c == 7, b + d == 3, a - b == 2 && c - d == 2,
            c - a == 1, values[4] == "window" && values[5] == "figure" && values[6] == "bell");
    }
}
