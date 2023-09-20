namespace MySamples
{
    public enum ShieldRemovalSource : byte
    {
        Expired = 0,
        ConsumedByDamage = 1,
        Dispelled = 2,
    }
    public unsafe abstract partial class ShieldData
    {
        [Tooltip("The ID you want to associate with the effect, this must match the visual id for the shield status effect to automatically be linked")]
        public int shieldId;
        [Tooltip("The flat value of the shield")]
        public int shieldValue;
        [Tooltip("Ability Power ratio")]
        public FP apRatio;
        [Tooltip("Attack Damage ratio")]
        public FP adRatio;
        [Tooltip("Base duration")]
        public FP shieldDuration;
        [Tooltip("Shield cannot exceed this % of max health")]
        public FP maxHealthLimit = FP._1;

        public virtual int GetShieldValue(Frame f, EntityRef owner, Shield* shield)
        {
            return shieldValue;
        }

        /// <summary>
        /// Adds a shield to an entity
        /// </summary>
        /// <param name="f">Current frame</param>
        /// <param name="owner">The entity to apply the shield to</param>
        /// <param name="shield">Pointer to the shield</param>
        /// <param name="overrideShieldValue">Override the flat amount with a different value</param>
        /// <param name="additiveValue">Add to the current value of the shield? If false, it will set the value as if there was no shield value before</param>
        public virtual void ApplyShield(Frame f, EntityRef owner, Shield* shield, int overrideShieldValue = 0, bool additiveValue = false)
        {
            shield->shieldId = shieldId;
            int originalValue = shield->shieldValue;
            if (additiveValue)
            {
                if (overrideShieldValue > 0) shield->shieldValue += overrideShieldValue;
                else shield->shieldValue += GetShieldValue(f, owner, shield);
            }
            else
            {
                if (overrideShieldValue > 0) shield->shieldValue = overrideShieldValue;
                else shield->shieldValue = GetShieldValue(f, owner, shield);
            }

            if (shield->caster != default && f.Unsafe.TryGetPointer<CharacterProfile>(shield->caster, out var profile))
            {
                if (apRatio > 0) shield->shieldValue += FPMath.RoundToInt(profile->GetStat(f, ProfileHelper.STAT_ABILITY_DAMAGE) * apRatio);
                if (adRatio > 0) shield->shieldValue += FPMath.RoundToInt(profile->GetStat(f, ProfileHelper.STAT_ATTACK_DAMAGE) * adRatio);
                shield->shieldValue += FPMath.RoundToInt(profile->GetStat(f, ProfileHelper.STAT_SHIELDING_POWER) * shield->shieldValue);
                if (owner != default && owner != shield->caster && f.Unsafe.TryGetPointer<Health>(owner, out var health))
                {
                    health->RegisterAttacker(f, shield->caster, true);
                }
            }
            shield->timeRemaining = shieldDuration > 0 ? shieldDuration : -FP._1;
            if (f.TryGet<Health>(owner, out var h))
            {
                if (shield->shieldValue > maxHealthLimit * h.maximumHealth)
                {
                    shield->shieldValue = FPMath.RoundToInt(maxHealthLimit * h.maximumHealth);
                }

            }
            f.Events.ShieldEvent(owner, shield->caster, shieldId, ShieldEventType.ShieldAdded);

            OnShieldApplied(f, owner, shield);
        }
        public virtual void RemoveShield(Frame f, EntityRef owner, Shield* shield, EntityRef remover, ShieldRemovalSource source)
        {
            f.Events.ShieldEvent(owner, shield->caster, shieldId, ShieldEventType.ShieldRemoved);

            switch (source)
            {
                case ShieldRemovalSource.Expired:
                    OnShieldExpired(f, owner, shield);
                    break;
                case ShieldRemovalSource.ConsumedByDamage:
                    OnShieldConsumed(f, owner, shield);
                    break;
                case ShieldRemovalSource.Dispelled:
                    OnShieldDispelled(f, owner, shield);
                    break;
            }
            OnShieldRemoved(f, owner, shield);
        }
        protected virtual void OnShieldApplied(Frame f, EntityRef owner, Shield* shield)
        {
            if (f.Unsafe.TryGetPointer<ItemInventory>(owner, out var inventory))
            {
                inventory->OnReceivedShield(f, shield, this);
            }
            if (shield->caster != default && f.Unsafe.TryGetPointer<ItemInventory>(shield->caster, out var cinventory))
            {
                cinventory->OnCastShield(f, owner, shield, this);
            }
        }
        protected virtual void OnShieldExpired(Frame f, EntityRef owner, Shield* shield)
        {
            if (f.Unsafe.TryGetPointer<ItemInventory>(owner, out var inventory))
            {
                inventory->OnLostShield(f, shield, this, ShieldRemovalSource.Expired);
            }
        }
        protected virtual void OnShieldConsumed(Frame f, EntityRef owner, Shield* shield)
        {
            if (f.Unsafe.TryGetPointer<ItemInventory>(owner, out var inventory))
            {
                inventory->OnLostShield(f, shield, this, ShieldRemovalSource.ConsumedByDamage);
            }
        }
        protected virtual void OnShieldDispelled(Frame f, EntityRef owner, Shield* shield)
        {
            if (f.Unsafe.TryGetPointer<ItemInventory>(owner, out var inventory))
            {
                inventory->OnLostShield(f, shield, this, ShieldRemovalSource.Dispelled);
            }
        }
        protected virtual void OnShieldRemoved(Frame f, EntityRef owner, Shield* shield)
        {
            // remove any representing effect
            if (f.Unsafe.TryGetPointer<EffectHandler>(owner, out var handler))
            {
                var list = handler->GetEffects(f);
                for (int i = 0; i < list.Count; i++)
                {
                    if (FPMath.Abs(list[i].effectId - shieldId) <= 2)
                    {
                        handler->ConsumeEffect(f, list[i].effectId, EffectConsumeType.ConsumeAll, owner);
                        break;
                    }
                }
            }
        }
        protected virtual void OnShieldUpdate(Frame f, EntityRef owner, Shield* shield)
        {

        }
        public virtual (int remainder, int absorbed) AbsorbDamage(Frame f, EntityRef owner, Shield* shield, EntityRef dealer, int damageValue, int damagePreMitigation, bool isCrit)
        {
            // make sure negative shields expire - this will never trigger. But I like it here. It makes me feel better.
            if (shield->shieldValue < 0)
            {
                shield->shieldValue = 0;
                return (damageValue, 0);
            }
            // can be fully absorbed
            if (shield->shieldValue >= damageValue)
            {
                shield->shieldValue -= damageValue;
                OnAbsorbedDamage(f, owner, shield, dealer, damageValue, damagePreMitigation, isCrit);
                return (0, damageValue);
            }
            // burns through our shield
            int remainer = damageValue - shield->shieldValue;
            int absorbed = shield->shieldValue;
            shield->shieldValue = 0;
            return (remainer, absorbed);
        }
        protected virtual void OnAbsorbedDamage(Frame f, EntityRef owner, Shield* shield, EntityRef dealer, int damageValue, int damagePreMitigation, bool isCrit)
        {

        }
    }
}