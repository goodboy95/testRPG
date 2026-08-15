using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class TimeService(GameState state)
{
    public const double GameMinutesPerMovingSecond = 0.75;
    private double _fractionalMinutes;

    public void AdvanceMovementSeconds(double seconds, bool modalOpen)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (modalOpen || state.World.IsDeepNight)
        {
            return;
        }

        _fractionalMinutes += seconds * GameMinutesPerMovingSecond;
        var wholeMinutes = (int)Math.Floor(_fractionalMinutes);
        if (wholeMinutes == 0)
        {
            return;
        }

        _fractionalMinutes -= wholeMinutes;
        state.World.AdvanceMinutes(wholeMinutes);
    }

    public void Sleep()
    {
        _fractionalMinutes = 0;
        state.World.StartNextDay();
    }
}
