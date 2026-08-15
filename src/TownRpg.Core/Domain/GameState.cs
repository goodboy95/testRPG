namespace TownRpg.Core.Domain;

public sealed class GameState
{
    public PlayerState Player { get; set; } = new();
    public WorldState World { get; set; } = new();
    public List<RelationshipState> Relationships { get; set; } = [];
    public List<PromiseState> Promises { get; set; } = [];
    public InvestigationState Investigation { get; set; } = new();
    public InventoryState Inventory { get; set; } = new();
    public ProgressionState Progression { get; set; } = new();
    public HashSet<string> CompletedEvents { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> AppliedOutcomes { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ReadDialogueLines { get; set; } = new(StringComparer.Ordinal);
    public PlayerSettings Settings { get; set; } = new();

    public static GameState CreateVerticalSlice()
    {
        var state = new GameState();
        foreach (var npcId in CharacterIds.CoreResidents)
        {
            state.Relationships.Add(new RelationshipState
            {
                SourceId = npcId,
                TargetId = CharacterIds.Protagonist,
                Affinity = npcId switch
                {
                    CharacterIds.KnowledgeFriend => 45,
                    CharacterIds.AbilityHolder => 20,
                    CharacterIds.TokenHolder => 30,
                    _ => 25
                },
                Trust = npcId switch
                {
                    CharacterIds.KnowledgeFriend => 40,
                    CharacterIds.AbilityHolder => 15,
                    _ => 25
                },
                Romance = npcId is CharacterIds.AbilityHolder or CharacterIds.TokenHolder
                    ? RomanceStatus.Available
                    : RomanceStatus.Friendship
            });

            if (npcId != CharacterIds.AbilityHolder)
            {
                state.Relationships.Add(new RelationshipState
                {
                    SourceId = npcId,
                    TargetId = CharacterIds.AbilityHolder,
                    Affinity = 30,
                    Trust = 25
                });
            }
        }

        return state;
    }
}

public static class CharacterIds
{
    public const string Protagonist = "protagonist";
    public const string AbilityHolder = "lin_xiaoyu";
    public const string KnowledgeFriend = "zhou_ming";
    public const string TokenHolder = "shen_yan";
    public const string Shopkeeper = "aunt_qiao";
    public const string Doctor = "doctor_he";
    public const string Teacher = "teacher_wen";
    public const string Reporter = "reporter_luo";
    public const string SiteWorker = "worker_peng";

    public static readonly IReadOnlyList<string> CoreResidents =
    [
        AbilityHolder,
        KnowledgeFriend,
        TokenHolder,
        Shopkeeper,
        Doctor,
        Teacher,
        Reporter,
        SiteWorker
    ];
}
