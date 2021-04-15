﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

[assembly: ModInfo("AlchemyMod",
    Description = "An alchemy mod that adds a couple of player enhancing potions.",
    Website = "https://github.com/llama3013/vsmod-Alchemy",
    Authors = new[] { "Llama3013" })]

/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/

namespace Alchemy
{
    public class AlchemyMod : ModSystem
    {
        private ModConfig config;
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[Potion] Start");
            base.Start(api);

            config = ModConfig.Load(api);
            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.OnEntitySpawn += (Entity entity) =>
            {
                if (entity is EntityPlayer)
                {
                    entity.AddBehavior(new PotionFixBehavior(entity, config));
                    //api.Logger.Debug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    string potionId = "potionid";
                    string tickPotionId = "tickpotionid";
                    string[] attributeKey = entity.WatchedAttributes.Keys;
                    int attributeAmnt = entity.WatchedAttributes.Count;
                    for (int i = 0; attributeAmnt > i; i++)
                    {
                        if (attributeKey[i].Contains(potionId))
                        {
                            long potionListenerId = entity.WatchedAttributes.GetLong(attributeKey[i]);
                            if (potionListenerId != 0)
                            {
                                if (attributeKey[i].Contains(tickPotionId))
                                {
                                    entity.World.UnregisterGameTickListener(potionListenerId);
                                }
                                else
                                {
                                    entity.World.UnregisterCallback(potionListenerId);
                                }
                                entity.WatchedAttributes.SetLong(attributeKey[i], 0);
                            }
                        }
                    }

                    entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
                    entity.Stats.Set("maxhealthExtraPoints", "potionmod", 0, false);
                    EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
                    ebh.UpdateMaxHealth();
                    entity.Stats.Set("walkspeed", "potionmod", 0, false);
                    entity.Stats.Set("hungerrate", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
                    entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
                    entity.Stats.Set("walkspeed", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("mechanicalsDamage", "potionmod", 0, false);
                    entity.Stats.Set("animalLootDropRate", "potionmod", 0, false);
                    entity.Stats.Set("forageDropRate", "potionmod", 0, false);
                    entity.Stats.Set("vesselContentsDropRate", "potionmod", 0, false);
                    entity.Stats.Set("wildCropDropRate", "potionmod", 0, false);
                    entity.Stats.Set("oreDropRate", "potionmod", 0, false);
                    entity.Stats.Set("rustyGearDropRate", "potionmod", 0, false);
                    entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
                    entity.Stats.Set("animalSeekingRange", "potionmod", 0, false);
                    entity.Stats.Set("animalHarvestingTime", "potionmod", 0, false);
                }
            };
        }
    }
}