namespace TownRpg.Core.Domain;

public enum StoryRoute
{
    Undecided,
    Cautious,
    Rescue
}

public enum ExposureStage
{
    Unnoticed,
    Watched,
    Suspected,
    Identified
}

public enum PromiseStatus
{
    Pending,
    Fulfilled,
    Explained,
    Broken
}

public enum RomanceStatus
{
    Friendship,
    Available,
    Confirmed,
    Closed
}

public enum EndingTier
{
    CurrentAwakeningStopped,
    CostlySuppression,
    SealRestored
}

public enum EventPriority
{
    Ambient = 0,
    Relationship = 10,
    Investigation = 20,
    DangerWarning = 30
}

public enum BattleAction
{
    Observe,
    Guard,
    Soothe,
    Disrupt,
    Retreat
}
