using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using Vintagestory.API.Config;

namespace Alchemy
{
    public class GuiHudPotion : HudElement
    {
        public override string ToggleKeyCombinationCode => "hudpotion";
        public override bool Focusable => false;
        long id = 0;
        Dictionary<string, bool> potionsDic;
        AssetLocation imageLocation;

        public GuiHudPotion(ICoreClientAPI capi) : base(capi)
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            try
            {
                IAsset essences = capi.Assets.TryGet("alchemy:config/essences.json");
                imageLocation = capi.Assets.TryGet("alchemy:textures/hud/activealchemyhud.png").Location;
                if (essences != null)
                {
                    potionsDic = essences.ToObject<Dictionary<string, bool>>();
                }
            }
            catch (Exception e)
            {
                capi.World.Logger.Error(
                    "Failed loading potion effects for potion. Will ignore. Exception: {0}",
                    e.Message
                );
            }

            ElementBounds textBounds = ElementBounds.Fixed(
                EnumDialogArea.RightBottom,
                0,
                0,
                100,
                100
            );
            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            SingleComposer = capi.Gui
                .CreateCompo("potionhud", textBounds)
                .AddDynamicCustomDraw(
                    textBounds.ForkChild(),
                    new DrawDelegateWithBounds(OnDraw),
                    "potionhudactive"
                )
                .AddHoverText(
                    "shouldn't see this!",
                    font,
                    250,
                    textBounds.ForkChild(),
                    "potionstatus"
                ).Compose();

            id = capi.World.RegisterGameTickListener(dt => UpdateText(), 1000);
        }

        bool inactive = true;

        private void OnDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            BitmapRef bmp = capi.Assets.Get("alchemy:textures/hud/activealchemyhud.png").ToBitmap(capi);
            if (inactive)
                bmp.MulAlpha(30);
            Vintagestory.API.Common.SurfaceDrawImage.Image(
                surface,
                ((Vintagestory.API.Common.BitmapExternal)bmp),
                (int)currentBounds.drawX,
                (int)currentBounds.drawY,
                (int)currentBounds.InnerWidth,
                (int)currentBounds.InnerHeight
            );
            bmp.Dispose();
        }

        public void UpdateText()
        {
            bool activePotion = false;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Lang.Get("alchemy:potioneffectTrue"));
            EntityPlayer entity = capi.World.Player.Entity;
            foreach (var stat in entity.Stats)
            {
                if (stat.Value.ValuesByKey.ContainsKey("potionmod"))
                {
                    var value = stat.Value.ValuesByKey["potionmod"]?.Value;
                    stringBuilder.AppendLine(
                        string.Format("{0}: {1}", Lang.Get("alchemy:" + stat.Key), value)
                    );
                    activePotion = true;
                }
            }
            if (entity.WatchedAttributes.HasAttribute("glow"))
            {
                var value = capi.World.Player.Entity.WatchedAttributes.GetBool("glow");
                stringBuilder.AppendLine(string.Format(Lang.Get("alchemy:glow") + ": {0}", value));
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("regentickpotionid"))
            {
                stringBuilder.AppendLine(string.Format(Lang.Get("alchemy:regen") + ": {0}", true));
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("poisontickpotionid"))
            {
                stringBuilder.AppendLine(string.Format(Lang.Get("alchemy:poison") + ": {0}", true));
                activePotion = true;
            }
            if (activePotion)
            {
                SingleComposer.GetHoverText("potionstatus").SetNewText(stringBuilder.ToString());
                inactive = false;
                SingleComposer.ReCompose();
            }
            else
            {
                SingleComposer
                    .GetHoverText("potionstatus")
                    .SetNewText(Lang.Get("alchemy:potioneffectFalse"));
                inactive = true;
                SingleComposer.ReCompose();
            }
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            SetupDialog();
        }

        public override void Dispose()
        {
            base.Dispose();
            capi.World.UnregisterGameTickListener(id);
        }
    }
}
