using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class SubtitleSettingsPreview : Control
{
    private SubtitleView _subtitle = null!;
    private float _lastScale = -1;
    private TownRpg.Core.Domain.SubtitleBackground _lastBackground;
    private string _lastName = "";
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 118);
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.All;
        var gradient = new Gradient { Colors = [new Color("777d78"), new Color("20282b")] };
        var backdrop = new TextureRect { Texture = new GradientTexture2D { Gradient = gradient,
            FillFrom = Vector2.Zero, FillTo = Vector2.Right }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        _subtitle = new SubtitleView { Name = "SettingsSubtitleSample" }; AddChild(_subtitle);
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        var session = GetNode<GameSession>("/root/GameSession");
        var settings = session.State.Settings;
        if (_lastScale != settings.EffectiveSubtitleFontScale || _lastBackground != settings.SubtitleBackground || _lastName != session.PlayerName)
        {
            _lastScale = settings.EffectiveSubtitleFontScale; _lastBackground = settings.SubtitleBackground; _lastName = session.PlayerName;
            _subtitle.ShowPage(_lastName, DreamPrologueText.Get("DREAM_V6_STORY_INTRO_002"), _lastScale, _lastBackground);
        }
        _subtitle.Position = new Vector2((Size.X - _subtitle.Size.X) / 2, (Size.Y - _subtitle.Size.Y) / 2);
    }
}
