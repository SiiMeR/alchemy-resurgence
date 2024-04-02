using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Alchemy.Behavior;

public class CancelRecallOnHitBehavior : EntityBehavior
{
    public CancelRecallOnHitBehavior(Entity entity) : base(entity)
    {
    }

    private IServerPlayer GetIServerPlayer()
    {
        if (entity is not EntityPlayer playerEntity)
        {
            return null;
        }

        if (playerEntity.Player is not IServerPlayer serverPlayer)
        {
            return null;
        }

        return serverPlayer;
    }


    /* This override is to add the behavior to the player of when they die they also reset all of their potion effects */
    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        IServerPlayer player = GetIServerPlayer();

        if (damageSource.Source == EnumDamageSource.Player && player.Entity.WatchedAttributes.GetLong("recallpotionid") != 0)
        {
            player.Entity.WatchedAttributes.RemoveAttribute("recallpotionid");

            player.SendMessage(GlobalConstants.InfoLogChatGroup,
                "Your recall was cancelled due to taking damage from a player!", EnumChatType.Notification);
        }


        base.OnEntityReceiveDamage(damageSource, ref damage);
    }

    public override string PropertyName()
    {
        return "CancelRecallOnHitBehavior";
    }
}