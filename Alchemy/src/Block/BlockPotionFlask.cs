using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened
    {
        LiquidTopOpenContainerProps Props;

        protected override float liquidYTranslatePerLitre => liquidMaxYTranslate / CapacityLitres;

        public override float TransferSizeLitres => Props.TransferSizeLitres;

        public override float CapacityLitres => Props.CapacityLitres;

        public Dictionary<string, float> effectsDictionary = new();

        public string potionId = "";

        public int duration = 0;

        public int tickSec = 0;

        public float health = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                Props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(
                    null,
                    Code.Domain
                );
            }
        }

        #region Render

        public new MeshData GenMesh(
            ICoreClientAPI capi,
            ItemStack contentStack,
            BlockPos forBlockPos = null
        )
        {
            if (this == null || Code.Path.Contains("clay"))
                return null;
            Shape shape = null;
            MeshData flaskmesh = null;

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);
                if (props == null)
                    return null;

                FlaskTextureSource contentSource = new FlaskTextureSource(
                    capi,
                    contentStack,
                    props.Texture,
                    this
                );

                float level = contentStack.StackSize / props.ItemsPerLitre;
                if (Code.Path.Contains("flask-normal"))
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level <= 0.25)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/flask-liquid-1.json")
                            .ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/flask-liquid-2.json")
                            .ToObject<Shape>();
                    }
                    else if (level <= 0.75)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/flask-liquid-3.json")
                            .ToObject<Shape>();
                    }
                    else if (level > 0.75)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/flask-liquid.json")
                            .ToObject<Shape>();
                    }
                }
                else if (Code.Path.Contains("flask-round"))
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/roundflask-liquid-1.json")
                            .ToObject<Shape>();
                    }
                    else if (level > 0.5)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/roundflask-liquid.json")
                            .ToObject<Shape>();
                    }
                }
                else
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level > 0)
                    {
                        shape = capi.Assets
                            .TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json")
                            .ToObject<Shape>();
                    }
                }

                capi.Tesselator.TesselateShape(
                    "potionflask",
                    shape,
                    out flaskmesh,
                    contentSource,
                    new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ)
                );
            }

            return flaskmesh;
        }

        public override void OnBeforeRender(
            ICoreClientAPI capi,
            ItemStack itemstack,
            EnumItemRenderTarget target,
            ref ItemRenderInfo renderinfo
        )
        {
            if (Code.Path.Contains("clay"))
                return;
            Dictionary<string, MultiTextureMeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                meshrefs = obj as Dictionary<string, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = new Dictionary<string, MultiTextureMeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null)
                return;

            MultiTextureMeshRef meshRef = null;

            if (
                !meshrefs.TryGetValue(
                    contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize,
                    out meshRef
                )
            )
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                if (meshdata == null)
                    return;

                meshrefs[contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize] =
                    meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null)
                return;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                if (obj is Dictionary<string, MultiTextureMeshRef> meshrefs)
                {
                    foreach (var val in meshrefs)
                    {
                        val.Value.Dispose();
                    }
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        #endregion

        #region Interaction

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            var contentStack = GetContent(slot.Itemstack);
            if (contentStack == null)
            {
                potionId = "";
                duration = 0;
                tickSec = 0;
                health = 0;
                effectsDictionary.Clear();
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (!contentStack.MatchesSearchText(byEntity.World, "potion"))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }
            
            var strength = string.IsNullOrWhiteSpace(contentStack.Item.Variant["strength"])
                ? string.Intern(contentStack.Item.Variant["strength"])
                : "none";
            try
            {
                var potionInfo = contentStack.ItemAttributes?["potioninfo"];
                if (potionInfo?.Exists ?? false)
                {
                    potionId = potionInfo["potionId"].AsString();
                    duration = potionInfo["duration"].AsInt();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(
                    "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                    Code,
                    e
                );
                potionId = "";
                duration = 0;
            }

            try
            {
                var tickPotionInfo = contentStack.ItemAttributes?["tickpotioninfo"];
                if (tickPotionInfo?.Exists ?? false)
                {
                    tickSec = tickPotionInfo["ticksec"].AsInt();
                    health = tickPotionInfo["health"].AsFloat();
                    switch (strength)
                    {
                        case "strong":
                            health *= 3;
                            break;
                        case "medium":
                            health *= 2;
                            break;
                    }
                }
                else
                {
                    tickSec = 0;
                    health = 0;
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(
                    "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                    Code,
                    e
                );
                tickSec = 0;
                health = 0;
            }

            try
            {
                var effects = contentStack.ItemAttributes?["effects"];
                if (effects?.Exists ?? false)
                {
                    effectsDictionary = effects.AsObject<Dictionary<string, float>>();
                    switch (strength)
                    {
                        case "strong":
                            foreach (var k in effectsDictionary.Keys.ToList())
                            {
                                effectsDictionary[k] *= 3;
                            }

                            break;
                        case "medium":
                            foreach (var k in effectsDictionary.Keys.ToList())
                            {
                                effectsDictionary[k] *= 2;
                            }

                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    effectsDictionary.Clear();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(
                    "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                    Code,
                    e
                );
                effectsDictionary.Clear();
            }

            if (string.IsNullOrEmpty(potionId)) 
                return;
            
            if (byEntity.WatchedAttributes.GetLong(potionId) != 0) return;
            
            byEntity.World.RegisterCallback(
                (dt) => playEatSound(byEntity, "drink", 1),
                500
            );
            
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            IPlayer player = (byEntity as EntityPlayer).Player;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -=
                    Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X +=
                    Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y +=
                        GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                return secondsUsed <= 1.5f;
            }

            return true;
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            ItemStack content = GetContent(slot.Itemstack);
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server && content != null)
            {
                if (content.MatchesSearchText(byEntity.World, "potion"))
                {
                    if (potionId == "nutritionpotionid")
                    {
                    }
                    else if (tickSec == 0)
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempEntityStats(
                            (byEntity as EntityPlayer),
                            effectsDictionary,
                            "potionmod",
                            duration,
                            potionId
                        );
                    }
                    else
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempTickEntityStats(
                            (byEntity as EntityPlayer),
                            effectsDictionary,
                            "potionmod",
                            duration,
                            potionId,
                            tickSec,
                            health
                        );
                    }

                    if (byEntity is EntityPlayer)
                    {
                        IServerPlayer sPlayer = (
                            byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID)
                                as IServerPlayer
                        );
                        if (potionId == "recallpotionid")
                        {
                            if (api.Side.IsServer())
                            {
                                byEntity.World.RegisterCallback(
                                    (dt) =>
                                    {
                                        sPlayer.SendMessage(
                                            GlobalConstants.InfoLogChatGroup,
                                            "You feel the effects of the " + content.GetName(),
                                            EnumChatType.Notification
                                        );
                                        FuzzyEntityPos spawn = sPlayer.GetSpawnPosition(false);
                                        byEntity.TeleportTo(spawn);
                                    },
                                    30000
                                );
                            }

                            sPlayer.SendMessage(GlobalConstants.InfoLogChatGroup,
                                "As you sip the recall potion, a wave of fatigue washes over you, your body growing weary as memories of home flood your mind.", EnumChatType.Notification, null);
                        }
                        else if (potionId == "nutritionpotionid")
                        {
                            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute(
                                "hunger"
                            );
                            if (hungerTree != null)
                            {
                                // SortedList<string, float> oldSatietyLevels = new SortedList<string, float>();
                                // oldSatietyLevels.Add("fruitlevel", hungerTree.GetFloat("fruitLevel"));
                                // oldSatietyLevels.Add("vegetableLevel", hungerTree.GetFloat("vegetableLevel"));
                                // oldSatietyLevels.Add("grainLevel", hungerTree.GetFloat("grainLevel"));
                                // oldSatietyLevels.Add("proteinLevel", hungerTree.GetFloat("proteinLevel"));
                                // oldSatietyLevels.Add("dairyLevel", hungerTree.GetFloat("dairyLevel"));
                                // foreach( KeyValuePair<string, float> kvp in oldSatietyLevels )
                                // {
                                //     byEntity.World.Logger.Debug("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                                // }
                                // var newSatietyLevels = oldSatietyLevels.OrderByDescending(kvp => kvp.Value);
                                // foreach( KeyValuePair<string, float> kvp in newSatietyLevels )
                                // {
                                //     byEntity.World.Logger.Debug("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                                // }
                                float totalSatiety =
                                (
                                    hungerTree.GetFloat("fruitLevel")
                                    + hungerTree.GetFloat("vegetableLevel")
                                    + hungerTree.GetFloat("grainLevel")
                                    + hungerTree.GetFloat("proteinLevel")
                                    + hungerTree.GetFloat("dairyLevel")
                                ) * 0.9f;
                                hungerTree.SetFloat("fruitLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat(
                                    "vegetableLevel",
                                    Math.Max(totalSatiety / 5, 0)
                                );
                                hungerTree.SetFloat("grainLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat("proteinLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat("dairyLevel", Math.Max(totalSatiety / 5, 0));
                                byEntity.WatchedAttributes.MarkPathDirty("hunger");
                            }
                        }
                        else
                        {
                            sPlayer.SendMessage(
                                GlobalConstants.InfoLogChatGroup,
                                "You feel the effects of the " + content.GetName(),
                                EnumChatType.Notification
                            );
                        }
                    }

                    IPlayer player = null;
                    if (byEntity is EntityPlayer)
                        player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                    splitStackAndPerformAction(
                        byEntity,
                        slot,
                        (stack) => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
                    );
                    slot.MarkDirty();

                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    if (entityPlayer == null)
                    {
                        potionId = "";
                        duration = 0;
                        tickSec = 0;
                        health = 0;
                        effectsDictionary.Clear();
                        return;
                    }

                    entityPlayer.Player.InventoryManager.BroadcastHotbarSlot();
                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        #endregion

        private int splitStackAndPerformAction(
            Entity byEntity,
            ItemSlot slot, System.Func<ItemStack, int> action
        )
        {
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory(
                        (pslot) =>
                        {
                            if (
                                pslot.Empty
                                || pslot is ItemSlotCreative
                                || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize
                            )
                                return true;
                            int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(
                                slot.Itemstack,
                                pslot.Itemstack,
                                EnumMergePriority.DirectMerge
                            );
                            if (mergableq == 0)
                                return true;

                            var selfLiqBlock =
                                slot.Itemstack.Collectible as BlockLiquidContainerBase;
                            var invLiqBlock =
                                pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                            if (
                                (selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0)
                                != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0)
                            )
                                return true;

                            slot.Itemstack.StackSize += mergableq;
                            pslot.TakeOut(mergableq);

                            slot.MarkDirty();
                            pslot.MarkDirty();
                            return true;
                        }
                    );
                }

                return moved;
            }
            else
            {
                ItemStack containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;

                int moved = action(containerStack);

                if (moved > 0)
                {
                    slot.TakeOut(1);
                    if (
                        (byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(
                            containerStack,
                            true
                        ) != true
                    )
                    {
                        api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    }

                    slot.MarkDirty();
                }

                return moved;
            }
        }

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            ItemStack content = GetContent(inSlot.Itemstack);
            if (content != null)
            {
                content.Collectible.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            }
        }
    }

    public class FlaskTextureSource : ITexPositionSource
    {
        public ItemStack forContents;

        private ICoreClientAPI capi;

        TextureAtlasPosition contentTextPos;

        TextureAtlasPosition blockTextPos;

        TextureAtlasPosition corkTextPos;

        TextureAtlasPosition bracingTextPos;

        CompositeTexture contentTexture;

        public FlaskTextureSource(
            ICoreClientAPI capi,
            ItemStack forContents,
            CompositeTexture contentTexture,
            Block flask
        )
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(flask, "topper");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(flask, "glass");
            this.bracingTextPos = capi.BlockTextureAtlas.GetPosition(flask, "bracing");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "topper" && corkTextPos != null)
                    return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null)
                    return blockTextPos;
                if (textureCode == "bracing" && bracingTextPos != null)
                    return bracingTextPos;
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(
                        capi,
                        "contenttexture-" + contentTexture.ToString(),
                        () =>
                        {
                            TextureAtlasPosition texPos;
                            int id = 0;

                            BitmapRef bmp = capi.Assets
                                .TryGet(
                                    contentTexture.Base
                                        .Clone()
                                        .WithPathPrefixOnce("textures/")
                                        .WithPathAppendixOnce(".png")
                                )
                                ?.ToBitmap(capi);
                            if (bmp != null)
                            {
                                capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                                bmp.Dispose();
                            }

                            return id;
                        }
                    );

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}