using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/
namespace Alchemy
{
    public class AlchemyMod : ModSystem
    {
        public GuiHudPotion hud;

        public override void Start(ICoreAPI api)
        {
            //api.Logger.Debug("[Potion] Start");
            base.Start(api);

            var harmony = new Harmony("Alchemy");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
            api.RegisterBlockClass("BlockPotionFlask", typeof(BlockPotionFlask));
            api.RegisterBlockEntityClass("BlockEntityPotionFlask", typeof(BlockEntityPotionFlask));
            api.RegisterBlockClass("BlockHerbRacks", typeof(BlockHerbRacks));
            api.RegisterBlockEntityClass("HerbRacks", typeof(BlockEntityHerbRacks));
        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerNowPlaying += (IServerPlayer iServerPlayer) =>
            {
                if (iServerPlayer.Entity is EntityPlayer)
                {
                    Entity entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity));

                    TempEffect tempEffect = new TempEffect();
                    EntityPlayer player = (iServerPlayer.Entity as EntityPlayer);
                    tempEffect.resetAllTempStats(player, "potionmod");
                    tempEffect.resetAllAttrListeners(player, "potionid", "tickpotionid");
                }
            };
        }
    }
}
