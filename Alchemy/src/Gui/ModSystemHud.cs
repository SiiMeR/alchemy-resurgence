using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Alchemy;

public class ModSystemHud : ModSystem
{
    ICoreClientAPI capi;
    GuiDialog dialog;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        dialog = new GuiHudPotion(api);

        capi = api;

        api.Input.RegisterHotKey(
            "togglepotionhud",
            "Toggle potion hud",
            GlKeys.LBracket,
            HotkeyType.GUIOrOtherControls
        );
        api.Input.SetHotKeyHandler("togglepotionhud", ToggleGui);
        api.Input.RegisterHotKey(
            "movepotionhud",
            "Move potion hud position",
            GlKeys.RBracket,
            HotkeyType.GUIOrOtherControls
        );
        api.Input.SetHotKeyHandler("movepotionhud", MoveGui);
    }

    private bool ToggleGui(KeyCombination comb)
    {
        if (dialog.IsOpened())
            dialog.TryClose();
        else
            dialog.TryOpen();

        return true;
    }

    private bool MoveGui(KeyCombination comb)
    {
        EnumDialogArea newPosition = dialog.SingleComposer.Bounds.Alignment + 1;
        switch (newPosition)
        {
            case EnumDialogArea.LeftFixed:
                newPosition = EnumDialogArea.RightTop;
                break;
            case EnumDialogArea.RightFixed:
                newPosition = EnumDialogArea.LeftTop;
                break;
            default:
                break;
        }
        dialog.SingleComposer.Bounds.Alignment = newPosition;

        return true;
    }
}