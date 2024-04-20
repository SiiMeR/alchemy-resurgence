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

        public Dictionary<string, float> EffectDictionary = new();

        public string potionId = "";

        public int duration = 0;
        
        public float health = 0;

        public override void OnLoaded(ICoreAPI coreApi)
        {
            base.OnLoaded(coreApi);

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
                health = 0;
                EffectDictionary.Clear();
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (!contentStack.MatchesSearchText(byEntity.World, "potion"))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            var strength = !string.IsNullOrWhiteSpace(contentStack.Item.Variant["strength"])
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
                var effects = contentStack.ItemAttributes?["effects"];
                if (effects?.Exists ?? false)
                {
                    EffectDictionary = effects.AsObject<Dictionary<string, float>>();
                    switch (strength)
                    {
                        case "strong":
                            foreach (var k in EffectDictionary.Keys.ToList())
                            {
                                EffectDictionary[k] *= 3;
                            }

                            break;
                        case "medium":
                            foreach (var k in EffectDictionary.Keys.ToList())
                            {
                                EffectDictionary[k] *= 2;
                            }

                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    EffectDictionary.Clear();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(
                    "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                    Code,
                    e
                );
                EffectDictionary.Clear();
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

            if (byEntity.World is not IClientWorldAccessor) return true;
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

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            var content = GetContent(slot.Itemstack);
            if (secondsUsed < 1.45f || byEntity.World.Side != EnumAppSide.Server || content == null ||
                byEntity is not EntityPlayer playerEntity)
            {
                base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                return;
            }

            if (!content.MatchesSearchText(playerEntity.World, "potion"))
            {
                base.OnHeldInteractStop(secondsUsed, slot, playerEntity, blockSel, entitySel);
                return;
            }
            
            if (playerEntity.Player is not IServerPlayer serverPlayer)
            {
                api.Logger.Debug($"playerEntity.Player is null for {playerEntity.PlayerUID}");
                base.OnHeldInteractStop(secondsUsed, slot, playerEntity, blockSel, entitySel);
                return;
            }

            if (potionId == "nutritionpotionid")
            {
            }
            else
            {
                JsonObject tickPotion = content.ItemAttributes?["tickpotioninfo"];
                bool effectSuccessfullyApplied;
                if (tickPotion?.Exists ?? false)
                {
                    TempEffect potionEffect = new TempEffect();
                    effectSuccessfullyApplied = potionEffect.TempEntityStats(
                        playerEntity,
                        EffectDictionary,
                        "potionmod",
                        duration,
                        potionId,
                        true,
                        tickPotion["ticksec"].AsInt(5),
                        tickPotion["health"].AsFloat()
                    );
                }
                else
                {
                    TempEffect potionEffect = new TempEffect();
                    effectSuccessfullyApplied =  potionEffect.TempEntityStats(
                        playerEntity,
                        EffectDictionary,
                        "potionmod",
                        duration,
                        potionId
                    );
                }

                if (effectSuccessfullyApplied)
                {
                    if (potionId != "recallpotionid")
                    {
                        serverPlayer.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            "You feel the effects of the " + content.GetName(),
                            EnumChatType.Notification
                        );
                    }

                }
                else
                {
                    return;
                }
            }
            

            
            switch (potionId)
            {
                case "recallpotionid":
                {
                    if (api.Side.IsServer())
                    {
                        playerEntity.World.RegisterCallback(
                            (dt) =>
                            {
                                // if the player was hit in the meantime, don't recall
                                if (playerEntity.WatchedAttributes.GetLong("recallpotionid") != 0)
                                {
                                    serverPlayer.SendMessage(
                                        GlobalConstants.InfoLogChatGroup,
                                        "You feel the effects of the " + content.GetName(),
                                        EnumChatType.Notification
                                    );
                                    FuzzyEntityPos spawn = serverPlayer.GetSpawnPosition(false);
                                    byEntity.TeleportTo(spawn);
                                }
                                
                                playerEntity.WatchedAttributes.RemoveAttribute("recallpotionid");


                            },
                            20000
                        );
                    }

                    serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup,
                        "As you sip the recall potion, a wave of fatigue washes over you, your body growing weary as memories of home flood your mind.",
                        EnumChatType.Notification, null);
                    break;
                }
                case "nutritionpotionid":
                {
                    ITreeAttribute hungerTree = playerEntity.WatchedAttributes.GetTreeAttribute(
                        "hunger"
                    );
                    if (hungerTree != null)
                    {
                        var totalSatiety =
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
                        playerEntity.WatchedAttributes.MarkPathDirty("hunger");
                    }

                    break;
                }
            }

            splitStackAndPerformAction(
                playerEntity,
                slot,
                (stack) => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
            );
            slot.MarkDirty();

            playerEntity.Player.InventoryManager.BroadcastHotbarSlot();

            base.OnHeldInteractStop(secondsUsed, slot, playerEntity, blockSel, entitySel);
        }

        #endregion

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.SourceSlot.Itemstack.Collectible.Class == "BlockPotionFlask" &&
                op.SinkSlot.Itemstack.Collectible.Class == "BlockPotionFlask")
            {
                return;
            }
            
            base.TryMergeStacks(op);
        }

        private int splitStackAndPerformAction(
            Entity byEntity,
            ItemSlot slot, System.Func<ItemStack, int> action
        )
        {
            int moved = 0;
            
            if (slot.Itemstack.StackSize == 1)
            {
                moved = action(slot.Itemstack);

                if (moved > 0)
                {
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
            }
            else
            {
                ItemStack containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;

                moved = action(containerStack);

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

            }

            return moved;

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
            content?.Collectible.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
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