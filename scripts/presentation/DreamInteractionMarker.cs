using Godot;

namespace TownRpg.Presentation;

public partial class DreamInteractionMarker : Marker2D
{
    [Export] public string TargetId { get; set; } = string.Empty;
}
