using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Text;

namespace Alchemy
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockBucket
    {
        LiquidTopOpenContainerProps Props;
        public override float TransferSizeLitres => Props.TransferSizeLitres;
        public override float CapacityLitres => Props.CapacityLitres;

        protected override string meshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected override AssetLocation emptyShapeLoc => Props.EmptyShapeLoc;
        protected override AssetLocation contentShapeLoc => Props.LiquidContentShapeLoc;
        protected override AssetLocation liquidContentShapeLoc => Props.LiquidContentShapeLoc;
        protected override float liquidMaxYTranslate => Props.LiquidMaxYTranslate;
        protected override float liquidYTranslatePerLitre => liquidMaxYTranslate / CapacityLitres;

        public Dictionary<string, float> dic = new Dictionary<string, float>();
        public string potionId = "";
        public int duration = 0;
        public int tickSec = 0;
        public float health = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                Props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
            }
        }

        public new MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            if (this == null || Code.Path.Contains("clay")) return null;
            Shape shape = null;
            MeshData flaskmesh = null;


            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);
                if (props == null) return null;

                FlaskTextureSource contentSource = new FlaskTextureSource(capi, contentStack, props.Texture, this);

                float level = contentStack.StackSize / props.ItemsPerLitre;
                if (Code.Path.Contains("flask-normal"))
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level <= 0.25)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-1.json").ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-2.json").ToObject<Shape>();
                    }
                    else if (level <= 0.75)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-3.json").ToObject<Shape>();
                    }
                    else if (level > 0.75)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid.json").ToObject<Shape>();
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
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid-1.json").ToObject<Shape>();
                    }
                    else if (level > 0.5)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid.json").ToObject<Shape>();
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
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json").ToObject<Shape>();
                    }
                }

                capi.Tesselator.TesselateShape("potionflask", shape, out flaskmesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }

            return flaskmesh;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Code.Path.Contains("clay")) return;
            Dictionary<string, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = new Dictionary<string, MeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize, out meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                if (meshdata == null) return;


                meshrefs[contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize] = meshRef = capi.Render.UploadMesh(meshdata);

            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource objLso && !singleTake)
            {
                var contentStackToMove = objLso.GetContent(hotbarSlot.Itemstack);

                float litres = singlePut ? Props.TransferSizeLitres : Props.CapacityLitres;
                int moved = TryPutLiquid(blockSel.Position, contentStackToMove, litres);

                if (moved > 0)
                {
                    objLso.TryTakeContent(hotbarSlot.Itemstack, moved);
                    DoLiquidMovedEffects(byPlayer, contentStackToMove, moved, EnumLiquidDirection.Pour);
                    return true;
                }
            }

            if (obj is ILiquidSink objLsi && !singlePut)
            {
                ItemStack owncontentStack = GetContent(blockSel.Position);

                if (owncontentStack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

                var liquidStackForParticles = owncontentStack.Clone();

                float litres = singleTake ? Props.TransferSizeLitres : Props.CapacityLitres;
                int moved;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = objLsi.TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, litres);
                }
                else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = objLsi.TryPutLiquid(containerStack, owncontentStack, litres);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    DoLiquidMovedEffects(byPlayer, liquidStackForParticles, moved, EnumLiquidDirection.Fill);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            ItemStack contentStack = GetContent(slot.Itemstack);
            if (contentStack != null)
            {
                if (contentStack.MatchesSearchText(byEntity.World, "potion"))
                {
                    string strength = contentStack.Item.Variant["strength"] is string str ? string.Intern(str) : "none";
                    try
                    {
                        JsonObject potion = contentStack.ItemAttributes?["potioninfo"];
                        if (potion?.Exists == true)
                        {

                            potionId = potion["potionId"].AsString();
                            duration = potion["duration"].AsInt();
                        }
                    }
                    catch (Exception e)
                    {
                        api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                        potionId = "";
                        duration = 0;
                    }
                    try
                    {
                        JsonObject tickPotion = contentStack.ItemAttributes?["tickpotioninfo"];
                        if (tickPotion?.Exists == true)
                        {

                            tickSec = tickPotion["ticksec"].AsInt();
                            health = tickPotion["health"].AsFloat();
                            switch (strength)
                            {
                                case "strong":
                                    health *= 3;
                                    break;
                                case "medium":
                                    health *= 2;
                                    break;
                                default:
                                    break;
                            }
                            //api.Logger.Debug("potion {0}, {1}, potionId, duration);
                        }
                        else
                        {
                            tickSec = 0;
                            health = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                        tickSec = 0;
                        health = 0;
                    }
                    try
                    {
                        JsonObject effects = contentStack.ItemAttributes?["effects"];
                        if (effects?.Exists == true)
                        {

                            dic = effects.AsObject<Dictionary<string, float>>();
                            switch (strength)
                            {
                                case "strong":
                                    foreach (var k in dic.Keys.ToList())
                                    {
                                        dic[k] *= 3;
                                    }
                                    break;
                                case "medium":
                                    foreach (var k in dic.Keys.ToList())
                                    {
                                        dic[k] *= 2;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            dic.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                        dic.Clear();
                    }

                    if (potionId != "" && potionId != null)
                    {
                        //api.Logger.Debug("potion {0}, {1}", dic.Count, potionId);
                        //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                        /* This checks if the potion effect callback is on */
                        if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
                        {
                            //api.Logger.Debug("potion {0}", byEntity.WatchedAttributes.GetLong(potionId));
                            byEntity.World.RegisterCallback((dt) => playEatSound(byEntity, "drink", 1), 500);
                            handling = EnumHandHandling.PreventDefault;
                        }
                    }
                    return;
                }
            }
            else
            {
                potionId = "";
                duration = 0;
                tickSec = 0;
                health = 0;
                dic.Clear();
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            IPlayer player = (byEntity as EntityPlayer).Player;


            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -= Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y += GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                return secondsUsed <= 1.5f;
            }
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            ItemStack content = GetContent(slot.Itemstack);
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server && content != null)
            {
                if (content.MatchesSearchText(byEntity.World, "potion"))
                {
                    if (potionId == "recallpotionid")
                    {

                    }
                    else if (tickSec == 0)
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId);
                    }
                    else
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempTickEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId, tickSec, health);
                    }
                    if (byEntity is EntityPlayer)
                    {
                        IServerPlayer sPlayer = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                        if (potionId == "recallpotionid")
                        {
                            if (api.Side.IsServer())
                            {
                                FuzzyEntityPos spawn = sPlayer.GetSpawnPosition(false);
                                byEntity.TeleportTo(spawn);
                            }
                            sPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + content.GetName(), EnumChatType.Notification);
                        }
                        else
                        {
                            sPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + content.GetName(), EnumChatType.Notification);
                        }
                    }
                    IPlayer player = null;
                    if (byEntity is EntityPlayer) player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                    splitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0);
                    slot.MarkDirty();

                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    if (entityPlayer == null)
                    {
                        potionId = "";
                        duration = 0;
                        tickSec = 0;
                        health = 0;
                        dic.Clear();
                        return;
                    }
                    entityPlayer.Player.InventoryManager.BroadcastHotbarSlot();
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        private int splitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    if (entityPlayer == null)
                    {
                        return moved;
                    }
                    (byEntity as EntityPlayer)?.WalkInventory((pslot) =>
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize) return true;
                        int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableq == 0) return true;

                        BlockLiquidContainerBase selfLiqBlock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        BlockLiquidContainerBase invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                        int? num3;
                        if (selfLiqBlock == null)
                        {
                            num3 = null;
                        }
                        else
                        {
                            ItemStack content = selfLiqBlock.GetContent(slot.Itemstack);
                            num3 = ((content != null) ? new int?(content.StackSize) : null);
                        }
                        int? num4 = num3;
                        int valueOrDefault = num4.GetValueOrDefault();
                        int? num5;
                        if (invLiqBlock == null)
                        {
                            num5 = null;
                        }
                        else
                        {
                            ItemStack content2 = invLiqBlock.GetContent(pslot.Itemstack);
                            num5 = ((content2 != null) ? new int?(content2.StackSize) : null);
                        }
                        num4 = num5;
                        if (valueOrDefault != num4.GetValueOrDefault())
                        {
                            return true;
                        }

                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);

                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
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
                    if ((byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(containerStack, true) != true)
                    {
                        api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    }

                    slot.MarkDirty();
                }

                return moved;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
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

        public FlaskTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block flask)
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
                if (textureCode == "topper" && corkTextPos != null) return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null) return blockTextPos;
                if (textureCode == "bracing" && bracingTextPos != null) return bracingTextPos;
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(capi, "contenttexture-" + contentTexture.ToString(), () =>
                    {
                        TextureAtlasPosition texPos;
                        int id = 0;

                        BitmapRef bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                            bmp.Dispose();
                        }

                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}