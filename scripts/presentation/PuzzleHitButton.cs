using Godot;

namespace TownRpg.Presentation;

/// <summary>Native focus and drag/drop over a drawn object. The payload is local to one canvas.</summary>
public partial class PuzzleHitButton : Button
{
    public int Slot { get; set; }
    public Control DragOwner { get; set; } = null!;
    public bool Draggable { get; set; }
    public event Action<int, int>? SwapRequested;
    public override void _Input(InputEvent input)
    {
        if (input.IsActionPressed("ui_cancel") && CancelOwnedDrag()) GetViewport().SetInputAsHandled();
    }
    private bool CancelOwnedDrag()
    {
        if (!IsInsideTree() || DragOwner is null || !GetViewport().GuiIsDragging()) return false;
        var data = GetViewport().GuiGetDragData();
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("owner") || payload["owner"].AsString() != DragOwner.GetInstanceId().ToString()) return false;
        GetViewport().GuiCancelDrag(); return true;
    }
    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationVisibilityChanged && !IsVisibleInTree()) CancelOwnedDrag();
    }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Draggable || Disabled || !IsVisibleInTree()) return default;
        var preview = new Label { Text = TooltipText, Theme = UiStyles.CreateTheme(UiStyles.Scale(this)) };
        preview.AddThemeStyleboxOverride("normal", UiStyles.Panel(UiStyles.Surface)); SetDragPreview(preview);
        return new Godot.Collections.Dictionary { ["owner"] = DragOwner.GetInstanceId().ToString(), ["slot"] = Slot };
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!Draggable || Disabled || !IsVisibleInTree() || data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        return payload.ContainsKey("owner") && payload.ContainsKey("slot") &&
            payload["owner"].AsString() == DragOwner.GetInstanceId().ToString() && payload["slot"].AsInt32() != Slot;
    }
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data)) SwapRequested?.Invoke(data.AsGodotDictionary()["slot"].AsInt32(), Slot);
    }
}
