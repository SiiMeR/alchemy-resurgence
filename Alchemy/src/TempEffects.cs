using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class TempEffect
    {
        private EntityPlayer _affectedEntity;
        private Dictionary<string, float> _effectedList;

        string effectCode;

        string effectId;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="code"> The identity of what is changing the stat. If "code" is present on same stat then the latest Set will override it. </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        /// <param name="tickTimeSeconds">How long does a tick take</param>
        /// <param name="hpPerTick">How much HP the entity will gain/lose per tick</param>
        /// <param name="shouldTick">If the potion is a tick effect, this should be set to true.</param>
        public bool TempEntityStats(
            EntityPlayer entity,
            Dictionary<string, float> effectList,
            string code,
            int duration,
            string id,
            bool shouldTick = false,
            int tickTimeSeconds = 0,
            float hpPerTick = 0
        )
        {
            _affectedEntity = entity;
            _effectedList = effectList;
            effectCode = code;
            effectId = id;
            if (shouldTick)
            {
                HandleTickPotion(duration, tickTimeSeconds, hpPerTick);
                return true;
            }

            if (effectId == "speedpotionid" & _affectedEntity.WatchedAttributes.GetLong("recallpotionid") != 0)
            {
                return false;
            }
                
            SetTempStats(duration);
            return true;
        }

        private void HandleTickPotion(int durationSeconds, int tickTimeSeconds, float hpPerTick)
        {
            var invocations = durationSeconds / tickTimeSeconds;

            for (int i = 0; i < invocations; i++)
            {
                _affectedEntity.World.RegisterCallback((dt) =>
                {
                    _affectedEntity.ReceiveDamage(
                        new DamageSource
                        {
                            Source = EnumDamageSource.Internal,
                            Type = hpPerTick > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                        },
                        Math.Abs(hpPerTick)
                    );
                }, i * tickTimeSeconds * 1000);
            }

            var id =_affectedEntity.World.RegisterCallback(_ =>
            {
                ResetTempStats();
            }, durationSeconds * 1000);
            
            _affectedEntity.WatchedAttributes.SetLong(effectId, id);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and sets every stat provided
        /// </summary>
        /// <param name="duration"></param>
        public void SetTempStats(int duration)
        {
            if (_effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                _affectedEntity.World.Api.Logger.Debug(
                    "blendedhealth {0}",
                    _affectedEntity.Stats.GetBlended("maxhealthExtraPoints")
                );
                _affectedEntity.World.Api.Logger.Debug(
                    "maxhealthExtraPoints {0}",
                    _effectedList["maxhealthExtraPoints"]
                );
                _effectedList["maxhealthExtraPoints"] =
                    (14f + _affectedEntity.Stats.GetBlended("maxhealthExtraPoints"))
                    * _effectedList["maxhealthExtraPoints"];
            }
            foreach (KeyValuePair<string, float> stat in _effectedList)
            {
                _affectedEntity.Stats.Set(stat.Key, effectCode, stat.Value, false);
            }
            if (_effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                EntityBehaviorHealth ebh = _affectedEntity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }

            var id = _affectedEntity.World.RegisterCallback((dt) =>
            {
                ResetTempStats();
            }, duration * 1000);

            _affectedEntity.WatchedAttributes.SetLong(effectId, id);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and resets every stat provided (only resets effects that has the same effectCode)
        /// </summary>
        public void ResetTempStats()
        {
            Reset();
        }
        
        public void Reset()
        {
            _affectedEntity.World.Logger.Debug($"Resetting stats of {effectId}: {JsonUtil.ToString(_effectedList.Keys)}");
            foreach (var stat in _effectedList)
            {
                _affectedEntity.Stats.Remove(stat.Key, effectCode);
            }
            if (_effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                EntityBehaviorHealth ebh = _affectedEntity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }

            _affectedEntity.WatchedAttributes.RemoveAttribute(effectId);

            IServerPlayer player = (
                _affectedEntity.World.PlayerByUid((_affectedEntity as EntityPlayer).PlayerUID)
                    as IServerPlayer
            );

            if (effectId != "recallpotionid")
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You feel the effects of the potion dissipate",
                    EnumChatType.Notification
                );
            }

        }

        public static void ResetAllTempStats(EntityPlayer entity, string effectCode)
        {
            foreach (var stats in entity.Stats)
            {
                entity.Stats.Remove(stats.Key, effectCode);
            }
            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            ebh.MarkDirty();
        }

        public static void ResetAllAttrListeners(
            EntityPlayer entity,
            string callbackCode,
            string listenerCode
        )
        {
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
                else if (watch.Contains(listenerCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
        }

        public static void ResetAllListeners(EntityPlayer entity, string callbackCode, string listenerCode)
        {
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.World.UnregisterCallback(potionListenerId);
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
                else if (watch.Contains(listenerCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
        }
    }
}