using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy
{

    public class ItemPotion : Item
    {
        public Dictionary<string, float> EffectDictionary = new();
        public string potionId;
        public int duration;
        public int tickSec = 0;
        public float health;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                float litres = (float)entityItem.Itemstack.StackSize / props.ItemsPerLitre;

                entityItem.World.SpawnCubeParticles(entityItem.SidedPos.XYZ, entityItem.Itemstack, 0.75f, (int)(litres * 2), 0.45f);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)entityItem.SidedPos.X, (float)entityItem.SidedPos.Y, (float)entityItem.SidedPos.Z, null);
            }


            base.OnGroundIdle(entityItem);

        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string strength = Variant["strength"] is string str ? string.Intern(str) : "none";
            JsonObject potion = Attributes?["potioninfo"];
            if (potion?.Exists == true)
            {
                try
                {
                    potionId = potion["potionId"].AsString();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    potionId = "";
                }

                try
                {
                    duration = potion["duration"].AsInt();
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    duration = 0;
                }
            }
            JsonObject tickPotion = Attributes?["tickpotioninfo"];
            if (tickPotion?.Exists == true)
            {
                try
                {
                    tickSec = tickPotion["ticksec"].AsInt();
                    health = tickPotion["health"].AsFloat();
                    switch (strength)
                    {
                        case "strong":
                            health *= 4;
                            break;
                        case "medium":
                            health *= 2;
                            break;
                        default:
                            break;
                    }
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    tickSec = 0;
                    health = 0;
                }
            }
            JsonObject effects = Attributes?["effects"];
            if (effects?.Exists == true)
            {
                try
                {
                    EffectDictionary = effects.AsObject<Dictionary<string, float>>();
                    switch (strength)
                    {
                        case "strong":
                            foreach (var k in EffectDictionary.Keys.ToList())
                            {
                                EffectDictionary[k] *= 4;
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
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    EffectDictionary.Clear();
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            //api.Logger.Debug("potion {0}, {1}", dic.Count, potionId);
            if (!string.IsNullOrEmpty(potionId))
            {
                //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                /* This checks if the potion effect callback is on */
                if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
                {
                    byEntity.World.RegisterCallback((dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        {
                            if (Code.Path.Contains("portion"))
                            {
                                byEntity.World.PlaySoundAt(new AssetLocation("alchemy:sounds/player/drink"), byEntity);
                            }
                            else
                            {
                                byEntity.PlayEntitySound("eat", (byEntity as EntityPlayer)?.Player);
                            }
                        }
                    }, 200);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
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
            var content = slot.Itemstack;
            if (secondsUsed < 1.45f || byEntity.World.Side != EnumAppSide.Server || content == null ||
                byEntity is not EntityPlayer playerEntity || playerEntity.Player is not IServerPlayer serverPlayer)
            {
                base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                return;
            }

            
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
                    tickPotion["health"].AsFloat() * Util.GetStrengthModifier(content)
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
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You feel the effects of the " + content.GetName(),
                    EnumChatType.Notification
                );
            }
            else
            {
                return;
            }

            slot.TakeOut(1);
            slot.MarkDirty();
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (EffectDictionary != null)
            {
                dsc.AppendLine(Lang.Get("\n"));
                if (EffectDictionary.ContainsKey("rangedWeaponsAcc"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged accuracy", Math.Round(EffectDictionary["rangedWeaponsAcc"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("animalLootDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more animal loot", Math.Round(EffectDictionary["animalLootDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("animalHarvestingTime"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% faster animal harvest", Math.Round(EffectDictionary["animalHarvestingTime"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("animalSeekingRange"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", Math.Round(EffectDictionary["animalSeekingRange"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("maxhealthExtraPoints"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health", EffectDictionary["maxhealthExtraPoints"]));
                }
                if (EffectDictionary.ContainsKey("forageDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", Math.Round(EffectDictionary["forageDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("healingeffectivness"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% healing effectiveness", Math.Round(EffectDictionary["healingeffectivness"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("hungerrate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", Math.Round(EffectDictionary["hungerrate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("meleeWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% melee damage", Math.Round(EffectDictionary["meleeWeaponsDamage"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("mechanicalsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mechanical damage (not sure if works)", Math.Round(EffectDictionary["mechanicalsDamage"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("miningSpeedMul"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mining speed", Math.Round(EffectDictionary["miningSpeedMul"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("oreDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more ore", Math.Round(EffectDictionary["oreDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("rangedWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged damage", Math.Round(EffectDictionary["rangedWeaponsDamage"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("rangedWeaponsSpeed"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged speed", Math.Round(EffectDictionary["rangedWeaponsSpeed"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("rustyGearDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more gears from metal piles", Math.Round(EffectDictionary["rustyGearDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("walkspeed"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% walk speed", Math.Round(EffectDictionary["walkspeed"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("vesselContentsDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more vessel contents", Math.Round(EffectDictionary["vesselContentsDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("wildCropDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% wild crop", Math.Round(EffectDictionary["wildCropDropRate"] * 100, 1)));
                }
                if (EffectDictionary.ContainsKey("wholeVesselLootChance"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% chance to get whole vessel", Math.Round(EffectDictionary["wholeVesselLootChance"] * 100, 1)));
                }
            }

            if (duration != 0)
            {
                dsc.AppendLine(Lang.Get("and lasts for {0} seconds", duration));
            }
            if (health != 0)
            {
                dsc.AppendLine(Lang.Get("When potion is used: {0} health", health));
            }
            if (tickSec != 0)
            {
                dsc.AppendLine(Lang.Get("every {0} seconds", tickSec));
            }
        }
    }
}