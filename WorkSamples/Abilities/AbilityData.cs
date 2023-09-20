namespace MySamples
{
    [Flags]
    public enum AbilityBotUsage : UInt32
    {
        None = 0,
        DealsDamage = 1,
        ChaseTarget = 2,
        RunFromTarget = 4,
        CCTarget = 8,
        FinishTarget = 16,
        AvoidAttack = 32,
        ReviveDeadAlly = 64,
        InterruptAbility = 128,
        PassThroughTarget = 256,
        BreakCC = 512,
        UseOffCooldown = 1024,
        RecoverHealth = 2048,
        HealsAlly = 4096,
        AssistsAllyInCombat = 8192,
    }
    [Flags]
    public enum AbilityBotUniqueBehavior : UInt32
    {
        None = 0,
        KeepFacingTarget = 1,
        WaitStackingHandlerMax = 2,
        KeepMovingTowardTarget = 4,
        ControllableProjectile = 8,
        TryToBounceToTarget = 16,
        JumpIfTargetIsJumping = 32,
        JumpIfAboutToFallOrNotGrounded = 64,
        AimAtTargetFeet = 128,
        PrefersRootedTarget = 256,
        UsableWhileStealth = 512,
        CancelIfNoGround = 1024,
        DoesntRequireVision = 2048,
    }
    [Flags]
    public enum AbilityUserStateRestriction : UInt32
    {
        None = 0,
        NotCarryingFlag = 1,
        NotInCombat = 2,
        END_OF_OPTIONS = 4,
    }
    [Flags]
    public enum AbilitySpecialBehavior : UInt32
    {
        None = 0,
        DontInterruptAbilities = 1,
        IgnoreAttackSpeed = 2,
        ShowAimLine = 4,
        InvertFacingOnStart = 8,
        IsAlwaysAThreat = 16,
        IsAlwaysAThreatWithinRange = 32,
        CannotTurnDuring = 64,
        ShowUsableInUI = 128,
        ReduceDurationWithFlag = 256,
        UsableWhileDead = 512,
        CanChangeFacingDuring = 1024,
        NotUseableWhileKnockedback = 2048,
        ClickingOnCDInterrupts = 4096,
        DisabledCharacterCollision = 8192,
        CannotBeUsedIfRooted = 16384,
        NonInterruptable = 32768,
        CDScalesWithPetQuality = 65536,
        DoesntBreakBrushStealth = 131072,
        NoAutoAim = 262144,
    }
    [Flags]
    public enum AbilityDamageBehavior : UInt32
    {
        None = 0,
        FollowOwnerMovement = 1,
        DestroyOnAbilityEnd = 2,
        TerminatesAbility = 4,
        SpawnsOnAbilityEnd = 8,
        SpawnOnAbilityEndIfDealtDamage = 16,
    }

    public enum AbilityButtonType : byte
    {
        Standard = 0,
        Aimable = 1,
        Charged = 2,
        Hold = 3,
    }
    public enum DamageShape : byte
    {
        Box = 0,
        Circle = 1,
    }
    [System.Serializable]
    public partial class AbilityDamage
    {
        public int value;
        public FP apRatio;
        public FP adRatio;
        public FP delay;
        public FP lifepsan;
        public byte feebackId;
        public FP invincibilityDuration;
        public DamageCategory damageCateogry = DamageCategory.SameAsAbility;
        public AbilityDamageBehavior behavior;
        public QBoolean followOwnerMovement;
        public QBoolean destroyOnEnd;
        public DamageZoneTargets targetType;
        public DamageShape shape = DamageShape.Box;
        public FPBounds2 bounds;
        public AssetRefEffectData[] effects;
        public FP abilityDirectionBonus;
        [Tooltip("If player didn't provide an aim, what's the default aim")]
        public FP defaultAbilityDirectionBonus = FP._0_50;
    }
    [System.Serializable]
    public partial class MotionData
    {
        public FPVector2 force;
        [Tooltip("How much % of our force lingers after motion is naturally terminated")]
        public FP residualPercentage;
        public FP distance;
        public FP delay;
        public MotionFlags motionFlags;
        public FP stopDistBonus;
    }
    [System.Serializable]
    public class AbilitySequence
    {
        public SequenceDecider decider;
        public AssetRefAbilityData sequencedAbilityRef;
        public FP skippableAfter;
        [Range(0, 100)]
        [DrawIf("decider", 1)]
        public byte diceOdds;
    }

    public abstract partial class AbilityData
    {
        public int abilityId;
        public CharacterPermissions permissions;
        public FP cooldown;
        public FP duration;
        public AbilityButtonType buttonType;
        public AbilityType abilityType;
        public AbilityUserStateRestriction restrictions;
        public AbilitySpecialBehavior specialBehavior;
        public QBoolean setsAbilityTarget = true;
        public QBoolean dismounts;
        public AbilityDamage[] damage;

        public QBoolean damageZonesShareHistory;
        public MotionData[] motion;
        [Header("Player")]
        public byte abilityInput;
        public int abilityCost;
        [Header("AI")]
        /// <summary>
        /// How far can this ability possibly reach.
        /// Used for BOT calculations
        /// </summary>
        public FP maxReach;
        /// <summary>
        /// AI can use this to decide to not use an ability in too close of a range
        /// </summary>
        public FP minReach;
        public QBoolean dontPredictReach;
        public QBoolean requiresTargetInVision;
        public AbilityBotUsage botUsage;
        public AbilityBotUniqueBehavior botBehavior;
        [Header("Sequencing")]
        public AbilitySequence[] sequence;

        protected virtual unsafe bool CanUseAbility(Frame f, CharacterController* controller)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.NotUseableWhileKnockedback)) return controller->CanUseAbility();
            else return controller->CanUseAbility(true);
        }
        protected virtual unsafe bool CanUseAttack(Frame f, CharacterController* controller)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.NotUseableWhileKnockedback)) return controller->CanAttack();
            else return controller->CanAttack(true);
        }
        protected virtual unsafe bool CanUseUtility(Frame f, CharacterController* controller)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.NotUseableWhileKnockedback)) return controller->CanUseUtility();
            else return controller->CanUseUtility(true);
        }
        /// <summary>
        /// Can we use this ability in our current situation?
        /// </summary>
        /// <returns></returns>
        public virtual unsafe bool CanUse(Frame f, EntityRef entity)
        {
            if (abilityType == AbilityType.Utility && f.Unsafe.TryGetPointer<Energy>(entity, out var energy))
            {
                if (energy->currentValue < abilityCost)
                {
                    return false;
                }
            }
            if (restrictions.HasFlag(AbilityUserStateRestriction.NotCarryingFlag) && f.Has<CarriedFlag>(entity))
            {
                return false;
            }
            if (f.Unsafe.TryGetPointer<CharacterController>(entity, out var controller))
            {
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.CannotBeUsedIfRooted))
                {
                    if (controller->parameters.cannotUseMotion) return false;
                }
                // cannot use abilities while dead, yeah?
                if (controller->IsDead() && specialBehavior.HasFlag(AbilitySpecialBehavior.UsableWhileDead) == false)
                {
                    return false;
                }

                if (restrictions.HasFlag(AbilityUserStateRestriction.NotInCombat) && controller->IsInCombat(f))
                {
                    return false;
                }
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.UsableWhileDead) == false)
                {
                    switch (abilityType)
                    {
                        case AbilityType.Jump:
                            if (controller->CanJump() == false) return false;
                            break;
                        default:
                        case AbilityType.Ability:
                            if (CanUseAbility(f, controller) == false) return false;
                            break;
                        case AbilityType.Attack:
                            if (CanUseAttack(f, controller) == false) return false;
                            break;
                        case AbilityType.Dodge:
                            if (controller->CanDodge() == false) return false;
                            break;
                        case AbilityType.Utility:
                            if (CanUseUtility(f, controller) == false) return false;
                            break;
                    }
                }

                // check ability conditions
                if (conditions != null)
                {
                    if (MeetsConditions(f, entity, conditions, controller) == false)
                    {
                        return false;
                    }
                }
            }
            // cant instantly use abilities (need time for the visual side to load feedbacks)
            if (f.TryGet<OwnedByEntity>(entity, out var obe) && f.Global->time - obe.spawnTime < FP._0_20)
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Cannot move
        /// </summary>
        protected virtual unsafe bool TargetIsImmobile(Frame f, EntityRef e) => TargetIsUnderEffect(f, e, 0);
        /// <summary>
        /// Is under a full stun effect
        /// </summary>
        protected virtual unsafe bool TargetIsStunned(Frame f, EntityRef e) => TargetIsUnderEffect(f, e, 1);
        /// <summary>
        /// Cannot initiate a dodge
        /// </summary>
        protected virtual unsafe bool TargetCannotDodge(Frame f, EntityRef e) => TargetIsUnderEffect(f, e, 2);
        /// <summary>
        /// target is knocked up in the air
        /// </summary>
        protected virtual unsafe bool TargetIsKnockedUp(Frame f, EntityRef e) => TargetIsUnderEffect(f, e, 10);

        /// <param name="type">0: Immobile, 1: stunned, 2: cannot dodge</param>
        /// <returns></returns>
        protected virtual unsafe bool TargetIsUnderEffect(Frame f, EntityRef e, byte type)
        {
            if (e == default) return false;
            if (target != default && f.Unsafe.TryGetPointer<CharacterController>(target, out var controller))
            {
                switch (type)
                {
                    // immobile
                    case 0:
                        if (f.Unsafe.TryGetPointer<CharacterProfile>(target, out var profile))
                        {
                            return profile->GetStat(f, ProfileHelper.STAT_MOVESPEED) <= FP._0;
                        }
                        return controller->CanMove() == false;
                    // stunned
                    case 1: return controller->IsStunned();
                    // cannot dodge
                    case 2: return controller->CanDodge() == false;
                }
            }
            return false;
        }
        /// <summary>
        /// cooldown that considers any influences
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public virtual unsafe FP GetCooldown(Frame f, EntityRef owner)
        {
            // practice view, we want players to be able to keep using abilities
            if (f.RuntimeConfig.gameMode.HasFlag(GameMode.ChampionView))
            {
                return FP._1;
            }

            if (owner != null && f.Unsafe.TryGetPointer<CharacterProfile>(owner, out var profile))
            {
                FP cdrStat = FP._0;
                // is an attack
                if (abilityType == AbilityType.Attack) cdrStat = FP._0;
                else
                {
                    cdrStat = profile->GetStat(f, ProfileHelper.STAT_CDR);
                    if (abilityInput == 6)
                    {
                        cdrStat += profile->GetStat(f, ProfileHelper.STAT_ULTIMATE_CDR);
                    }
                }
#if !DEBUG
                if (cdrStat > FP._0_75 + FP._0_05) cdrStat = FP._0_75 + FP._0_05;
#else
                if (cdrStat > FP._0_99) cdrStat = FP._0_99;
#endif
                cdrStat = cooldown * (1 - cdrStat);

                // it's a pet ability, scale it's cooldown
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.CDScalesWithPetQuality))
                {
                    FP Factor = 1;
                    switch (profile->petQuality)
                    {
                        case 0:
                            break;
                        case 1:
                            Factor = FP._0_75 + FP._0_20;
                            break;
                        case 2:
                            Factor = FP._0_75 + FP._0_20 - FP._0_05;
                            break;
                        case 3:
                            Factor = FP._0_75 + FP._0_05;
                            break;
                        case 4:
                            Factor = FP._0_50 + FP._0_20;
                            break;
                        case 5:
                            Factor = FP._0_50 + FP._0_10;
                            break;
                        case 6:
                            Factor = FP._0_50 + FP._0_05;
                            break;
                        case 7:
                            Factor = FP._0_50 - FP._0_05;
                            break;
                        case 8:
                            Factor = FP._0_50 - FP._10;
                            break;
                        default:
                            Factor = FP._0_33;
                            break;
                    }
                    if (Factor > 1) Factor = 1;
                    else if (Factor < FP._0_33) Factor = FP._0_33;
                    return cdrStat * Factor;
                }

                return cdrStat;
            }
            return cooldown;
        }
        /// <summary>
        /// For display in the character UI only, never using this in a quantum simulation
        /// </summary>
        /// <param name="f"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        public virtual unsafe FP GetCharacterUICooldown(Frame f, EntityRef owner)
        {
            if (owner != null && f.Unsafe.TryGetPointer<CharacterProfile>(owner, out var profile))
            {
                FP cdrStat = FP._0;
                // is an attack
                if (abilityType == AbilityType.Attack) cdrStat = FP._0;
                else
                {
                    cdrStat = profile->GetStat(f, ProfileHelper.STAT_CDR);
                    if (cdrStat > FP._0_75) cdrStat -= FP._1;
                    if (abilityInput == 6)
                    {
                        cdrStat += profile->GetStat(f, ProfileHelper.STAT_ULTIMATE_CDR);
                    }
                }
                if (cdrStat > 1) cdrStat = 1;
                return cooldown * (1 - cdrStat);
            }
            return cooldown;
        }
        // Create the ability's damage zones. Outside of projectiles and overriden damage behavior, this is how abilities deal damage.
        protected virtual unsafe EntityRef MaterializeDamageZone(Frame f, EntityRef entity, AbilityDamage abilityDamage, FPVector2 position, Ability* ability, int overrideDamageValue = 0, FPVector2 sizeOverride = default, byte damageIndexInAbility = 0)
        {
            return DamageZoneHelper.MaterializeDamageZone(abilityId, f, entity, abilityDamage, position, ability, this, overrideDamageValue, sizeOverride, damageIndexInAbility);
        }
        protected virtual unsafe void CreateDamageZones(Frame f, EntityRef owner, Ability* ability)
        {
            if (damage != null)
            {
                for (int i = 0; i < damage.Length; i++)
                {
                    if (damage[i] != null && damage[i].behavior.HasFlag(AbilityDamageBehavior.SpawnsOnAbilityEnd) == false
                        && damage[i].behavior.HasFlag(AbilityDamageBehavior.SpawnOnAbilityEndIfDealtDamage) == false)
                    {
                        MaterializeDamageZone(f, ability->owner, damage[i], GetDamageZonePosition(f, ability), ability, 0, default, (byte)i);
                    }
                }
            }
        }
        // Create damage zones at the end of ability execution
        protected virtual unsafe void CreateDamageZonesAtEnd(Frame f, EntityRef owner, Ability* ability)
        {
            if (damage != null)
            {
                bool created = false;
                for (int i = 0; i < damage.Length; i++)
                {
                    if (damage[i] != null && (damage[i].behavior.HasFlag(AbilityDamageBehavior.SpawnsOnAbilityEnd)
                        || (ability->dealtDamage && damage[i].behavior.HasFlag(AbilityDamageBehavior.SpawnOnAbilityEndIfDealtDamage))))
                    {
                        created = true;
                        MaterializeDamageZone(f, ability->owner, damage[i], GetDamageZonePosition(f, ability), ability, 0, default, (byte)i);
                    }
                }

                if (damageZonesShareHistory)
                {
                    var comps = f.Filter<DamageZone>();
                    while (comps.NextUnsafe(out var entity, out var dz))
                    {
                        if (dz->owner == owner && dz->sourceAbility == ability->abilityData)
                        {
                            dz->shareDamageHistoryId = abilityId;
                        }
                    }
                }

                if (created && damageZonesShareHistory)
                {
                    var comps = f.Filter<DamageZone>();
                    var comps2 = f.GetComponentIterator<DamageZone>();
                    while (comps.NextUnsafe(out var entity, out var dz))
                    {
                        // we have another damage source that has dealt damage to someone before
                        if (dz->owner == owner && dz->shareDamageHistoryId == abilityId && dz->GetHistory(f).Count == 0)
                        {
                            var list = dz->GetHistory(f);
                            foreach (var source in comps2)
                            {
                                if (source.Entity != entity && source.Component.owner == owner && source.Component.shareDamageHistoryId == abilityId && source.Component.GetHistory(f).Count > 0)
                                {
                                    var sourceList = source.Component.GetHistory(f);
                                    for (int i = 0; i < sourceList.Count; i++)
                                    {
                                        list.Add(sourceList[i]);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // insures that no remaining motion effects generated from this ability still linger.
        protected virtual unsafe void ClearLastMotion(Frame f, EntityRef owner, Ability* ability)
        {
            // only clear if we're introducing a new motion
            if (motion == null || motion.Length == 0) return;

            if (f.Unsafe.TryGetPointer<CharacterController>(owner, out var controller))
            {
                foreach (var m in f.GetComponentIterator<Motion>())
                {
                    if (m.Component.entity == owner && m.Component.terminated == 0 && m.Component.isHammock == false)
                    {
                        if (f.Unsafe.TryGetPointer<Motion>(m.Entity, out var motion))
                        {
                            MotionSystem.TerminateMotion(f, m.Entity, motion, controller, false);
                        }
                    }
                }
            }
        }
        // Generates motion effects related to the ability. (Dash effects and what not)
        protected virtual unsafe void CreateMotionZones(Frame f, EntityRef owner, Ability* ability)
        {
            ClearLastMotion(f, owner, ability);

            if (f.TryGet<CharacterController>(owner, out var c) && c.parameters.cannotUseMotion)
            {
                return;
            }

            if (motion != null)
            {
                for (int i = 0; i < motion.Length; i++)
                {
                    if (motion[i] != null)
                    {
                        MaterializeMotion(f, ability->owner, motion[i], ability);
                    }
                }
            }
        }
        // So we can allow players instant feedback on ability use
        protected virtual unsafe void InterruptPreviousAbilityBeforeStart(Frame f, EntityRef owner, Ability* ability)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.DontInterruptAbilities)) return;

            if (f.Unsafe.TryGetPointer<AbilityInventory>(owner, out var inventory))
            {
                inventory->InterruptAllAbilities(f, default, -1, abilityId, true);
            }
        }
        // This determins how quickly an ability executes. 1 = 100% speed.
        public virtual unsafe FP GetAbilitySpeed(Frame f, EntityRef owner, Ability* ability)
        {
            return FP._1;
        }

        // only meant to be overriden -- has no default behavior. Default behavior is from AutoAim function.
        protected virtual unsafe void UpdateBotAiming(Frame f, EntityRef owner)
        {
        }

        // For ability that have an energy overhead
        protected virtual unsafe void ConsumeAbilityCost(Frame f, EntityRef owner, Ability* ability, Energy* energy)
        {
            energy->ChangeEnergy(f, -abilityCost, EnergyChangeSource.Ability);
        }

        /// <summary>
        /// Determines whether or not the ability will break brush stealth
        /// </summary>
        /// <param name="f"></param>
        /// <param name="owner"></param>
        /// <param name="ability"></param>
        protected virtual unsafe void ConsiderBreakingBrushStealth(Frame f, EntityRef owner, Ability* ability)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.DoesntBreakBrushStealth)) return;

            if (f.Unsafe.TryGetPointer<BrushUser>(owner, out var user))
            {
                if (user->IsInBrush() == false)
                {
                    return;
                }
            }
            if (f.Unsafe.TryGetPointer<Stealth>(owner, out var stealth))
            {
                stealth->brushLockout = f.Global->time + FP._2;
                if (stealth->stealthItems[Stealth.ITEM_INDEX_BRUSH].isStealth)
                {
                    if (stealth->stealthItems[Stealth.ITEM_INDEX_BRUSH].stealthData != default && stealth->stealthItems[Stealth.ITEM_INDEX_BRUSH].stealthData.Id != default)
                    {
                        if (f.TryFindAsset<StealthData>(stealth->stealthItems[Stealth.ITEM_INDEX_BRUSH].stealthData.Id, out var data))
                        {
                            data.SetStealthState(f, owner, stealth, Stealth.ITEM_INDEX_BRUSH, false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Start an ability
        /// </summary>
        public virtual unsafe void StartAbility(Frame f, EntityRef owner, Ability* ability)
        {
            if (ability->hasEnded)
            {
                return;
            }
            // consume cost
            if (abilityType == AbilityType.Utility && f.Unsafe.TryGetPointer<Energy>(owner, out var energy))
            {
                ConsumeAbilityCost(f, owner, ability, energy);
            }


            InterruptPreviousAbilityBeforeStart(f, owner, ability);

            // initialize ability
            if (f.Unsafe.TryGetPointer<Playable>(owner, out var playable))
            {
                ability->isBot = playable->isBot;
            }
            ability->abilitySpeed = GetAbilitySpeed(f, owner, ability);
            ability->cooldownTimer = GetCooldown(f, owner);
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.ReduceDurationWithFlag) && f.Has<CarriedFlag>(owner)) ability->inAbilityTimer = duration / (FP._2 + FP._0_50);
            else ability->inAbilityTimer = duration;
            ability->lastTimeDelayUsed = 0;
            ability->timeElapsed = 0;
            ability->abilityId = abilityId;
            ability->abilityInput = abilityInput;
            ability->mark = AbilityMark.None;

            ability->owner = owner;

            bool isKnockedback = false;
            if (f.Unsafe.TryGetPointer<CharacterController>(owner, out var controller))
            {
                controller->parameters.distanceCrossedDuringLastAbility = FP._0;

                if (specialBehavior.HasFlag(AbilitySpecialBehavior.DisabledCharacterCollision))
                {
                    controller->permissions.noCharacterCollision++;
                }
                isKnockedback = controller->IsKnockedback();

                // face position
                FaceIntendedPosition(f, ability, owner, controller, playable);
                // do this AFTER we've faced our position
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.CannotTurnDuring))
                {
                    controller->state.cannotTurn = true;
                }
            }
            if (dismounts)
            {
                if (f.Unsafe.TryGetPointer<Mount>(ability->owner, out var mount))
                {
                    if (mount->mountData != default)
                    {
                        var data = f.FindAsset<MountData>(mount->mountData.Id);
                        if (data != null)
                        {
                            data.SetMountedState(f, owner, mount, false);
                        }
                    }
                }
            }
            if (f.Unsafe.TryGetPointer<CharacterProfile>(owner, out var profile))
            {
                if (abilityType == AbilityType.Attack && specialBehavior.HasFlag(AbilitySpecialBehavior.IgnoreAttackSpeed) == false)
                {
                    ability->abilitySpeed = profile->GetStat(f, ProfileHelper.STAT_ATTACK_SPEED);
                }
            }
            CreateDamageZones(f, owner, ability);
            CreateMotionZones(f, owner, ability);
            if (damageZonesShareHistory)
            {
                var comps = f.Filter<DamageZone>();
                while (comps.NextUnsafe(out var entity, out var dz))
                {
                    if (dz->owner == owner && dz->sourceAbility == ability->abilityData)
                    {
                        dz->shareDamageHistoryId = abilityId;
                    }
                }
            }
            f.Events.AbilityEvent(ability->owner, abilityInput, AbilityPlaytime.Start, ability->abilitySpeed, abilityId);

            f.Signals.AbilityStarted(ability, this);
            AbilityInventory.TriggerUpdateUIEvent(f, ability->owner);
            // character permissions
            ChangePermissions(f, ability, ability->owner, true);

            ConsiderBreakingBrushStealth(f, owner, ability);
            if (sequence != null)
            {
                for (int i = 0; i < sequence.Length; i++)
                {
                    if (sequence[i] != null)
                    {
                        if (sequence[i].decider == SequenceDecider.DealtDamage)
                        {
                            ability->abilityCallbacks |= AbilityCallbacks.DealtDamage;
                            if (f.Unsafe.TryGetPointer<AbilityInventory>(owner, out var inv)) { inv->abilityCallbacks |= AbilityCallbacks.DealtDamage; }
                        }
                        if (sequence[i].decider == SequenceDecider.ReceivedDamage)
                        {
                            ability->abilityCallbacks |= AbilityCallbacks.ReceivedDamage;
                            if (f.Unsafe.TryGetPointer<AbilityInventory>(owner, out var inv)) { inv->abilityCallbacks |= AbilityCallbacks.ReceivedDamage; }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles auto aiming abilities for both bots and players. When a player has manual aim, must pass valid currentAim, otherwise player input will be overriden
        /// </summary>
        protected virtual unsafe (bool hadResult, FPVector2 direction, FP range, EntityRef target) AutoAim(Frame f, Ability* ability, EntityRef owner, Transform2D* transform, CharacterController* controller, FPVector2 currentAim)
        {
            if (transform == default)
            {
                f.Unsafe.TryGetPointer<Transform2D>(owner, out transform);
            }
            if (f.Unsafe.TryGetPointer<Team>(owner, out var ourTeam) == false || transform == default)
            {
                return (false, currentAim, default, default);
            }

            var comps = f.Filter<Transform2D, Health>();
            FP dist = 9999;
            FP range = FP._1;
            FP maxRange = FP._1;
            Transform2D* targetTransform = default;
            EntityRef target = default;
            EntityRef autoAttackTarget = default;
            Transform2D* autoAttackTargetTransform = default;
            bool foundResult = false;
            if (damage != null && damage.Length > 0)
            {
                maxRange = damage[0].bounds.Center.Y + damage[0].abilityDirectionBonus;
            }
            while (comps.NextUnsafe(out var e, out var t, out var health))
            {
                if (health->currentValue <= 0)
                {
                    //dead
                    continue;
                }
                // too far to auto aim
                if (FPVector2.Distance(t->Position, transform->Position) > BotHelper.ONSCREEN_DIST + FP._2)
                {
                    // too far
                    continue;
                }
                // don't target allies
                if (f.Unsafe.TryGetPointer<Team>(e, out var team) && team->team == ourTeam->team)
                {
                    // ally
                    continue;
                }


                FP ar = controller->attackRange + FP._2;
                FP d = FPVector2.Distance(transform->Position, t->Position);
                if (d < dist)
                {
                    if (abilityInput == 1 && d > ar) continue;

                    // 90 angle max
                    if (FPVector2.Angle(t->Position - transform->Position, transform->Up) > 90)
                    {
                        // auto attack redirecting only works on real enemies
                        if (abilityInput == 1 && f.Has<CharacterController>(e))
                        {
                            autoAttackTarget = e;
                            autoAttackTargetTransform = t;
                        }
                        continue;
                    }
                    // stealth?
                    if (BotHelper.IsEntityStealthToUs(f, owner, e))
                    {
                        continue;
                    }

                    target = e;
                    targetTransform = t;
                    dist = d;
                }
            }

            if (autoAttackTarget != default && target == default)
            {
                target = autoAttackTarget;
                targetTransform = autoAttackTargetTransform;
            }

            if (target != default && targetTransform != default)
            {

                FPVector2 pos = targetTransform->Position;
                // ----------------------------- BOT PREDICTION
                if (f.Has<BRBot>(owner) && f.Unsafe.TryGetPointer<CharacterController>(target, out var tc))
                {
                    FP attackDelay = FP._0_50;
                    if (this.GetType() == typeof(AbilityProjectile))
                    {
                        var p = ((AbilityProjectile)this);
                        attackDelay = p.projectileDelay;
                        // factor in how long it will take our projectile to get there
                        if (p.projectile != default && f.TryFindAsset<ProjectileData>(p.projectile.Id, out var pdata) && pdata.speed > FP._0)
                        {
                            attackDelay += dist / pdata.speed;
                        }
                    }
                    else
                    {
                        if (damage.Length > 0)
                        {
                            attackDelay = damage[0].delay;
                            for (int j = 0; j < damage.Length; j++)
                            {
                                if (damage[j].delay < attackDelay)
                                {
                                    attackDelay = damage[j].delay;
                                }
                            }
                        }
                    }
                    FP rng = f.RNG->Next();
                    // 33% chance to perfectly predict
                    if (rng <= FP._0_33)
                    {

                    }
                    else
                    {
                        attackDelay += f.RNG->Next(-FP._0_25, FP._0_25);
                    }

                    if (attackDelay > FP._0) pos = tc->GetPredictedPosition(f, FPMath.CeilToInt(attackDelay * GlobalSystem.TickRate));
                }
                currentAim = (pos - transform->Position);
                if (maxRange > FP._0)
                {
                    range = FPMath.Clamp(dist / maxRange, FP._0_10, FP._1);
                }
                foundResult = true;
                return (foundResult, currentAim.Normalized, range, target);
            }
            return (false, currentAim, default, default);
        }

        // An optimized aiming function that removes anything but target selection. Used in cases where you would want to know what auto aim would target, or want to take advantage of other functionality
        protected virtual unsafe EntityRef GetIdealTarget(Frame f, Ability* ability, EntityRef owner, Transform2D* transform, CharacterController* controller, FPVector2 currentAim)
        {
            if (transform == default)
            {
                f.Unsafe.TryGetPointer<Transform2D>(owner, out transform);
            }
            if (f.Unsafe.TryGetPointer<Team>(owner, out var ourTeam) == false || transform == default)
            {
                return default;
            }

            var comps = f.Filter<Transform2D, Health>();
            FP dist = 9999;
            FP range = FP._1;
            FP maxRange = FP._1;
            Transform2D* targetTransform = default;
            EntityRef target = default;
            if (damage != null && damage.Length > 0)
            {
                maxRange = damage[0].bounds.Center.Y + damage[0].abilityDirectionBonus;
            }
            while (comps.NextUnsafe(out var e, out var t, out var health))
            {
                if (health->currentValue <= 0)
                {
                    continue;
                }
                // too far to auto aim
                if (FPVector2.Distance(t->Position, transform->Position) > BotHelper.ONSCREEN_DIST + FP._2)
                {
                    continue;
                }
                // don't target allies
                if (f.Unsafe.TryGetPointer<Team>(e, out var team) && team->team == ourTeam->team)
                {
                    continue;
                }



                FP ar = controller->attackRange + FP._2;
                FP d = FPVector2.Distance(transform->Position, t->Position);
                if (d < dist)
                {
                    // 90 angle max
                    if (FPVector2.Angle(t->Position - transform->Position, currentAim) > 25)
                    {
                        continue;
                    }
                    // stealth?
                    if (BotHelper.IsEntityStealthToUs(f, owner, e))
                    {
                        continue;
                    }

                    target = e;
                    targetTransform = t;
                    dist = d;
                }
            }

            if (target != default && targetTransform != default)
            {
                return target;
            }
            return default;
        }

        protected virtual unsafe void FaceIntendedPosition(Frame f, Ability* ability, EntityRef owner, CharacterController* controller = default, Playable* playable = default, bool isAPerFrameCall = false)
        {
            if (controller == default)
            {
                f.Unsafe.TryGetPointer<CharacterController>(owner, out controller);
            }
            if (controller == default) return;
            if (playable == default)
            {
                f.Unsafe.TryGetPointer<Playable>(owner, out playable);
            }
            if (playable == default) return;
            bool faceWrongDirection = false;

            if (specialBehavior.HasFlag(AbilitySpecialBehavior.InvertFacingOnStart))
            {
                controller->RotateController(f, FPVector2.Rotate(controller->state.direction, FP.Rad_180));
            }
            // face our INTENDED direction
            var input = PlayableHelper.GetInput(f, playable);
            var inputDir = input->AbilityDirection;
            FP r = input->abilityRange;
            r /= FP._100;
            if (setsAbilityTarget) controller->abilityTarget = default;
            bool alreadyAimed = false;
            if (f.Unsafe.TryGetPointer<Transform2D>(owner, out var transform))
            {
                if (isAPerFrameCall == false)
                {
                    if ((inputDir == default || f.Has<BRBot>(owner)))
                    {
                        // this is an auto attack
                        if (abilityInput == 1 && buttonType == AbilityButtonType.Standard && input->MovementDirection != default)
                        {
                            controller->RotateController(f, input->MovementDirection);
                            //return;
                        }
                        // we still want to auto aim
                        //else
                        if (specialBehavior.HasFlag(AbilitySpecialBehavior.NoAutoAim) == false)
                        {
                            var aim = AutoAim(f, ability, owner, transform, controller, inputDir);
                            alreadyAimed = true;
                            if (setsAbilityTarget) controller->abilityTarget = aim.target;
                            // AUTO AIM
                            if (aim.hadResult)
                            {
                                inputDir = aim.direction;
                                r = aim.range;
                            }
                        }
                    }
                    // we need to set a target in-case ability has a motion
                    else
                    {
                        if (setsAbilityTarget && abilityInput != 1)
                        {
                            controller->abilityTarget = GetIdealTarget(f, ability, owner, transform, controller, inputDir);
                        }
                    }
                }
            }
            ability->abilityDirection = inputDir * r;
            controller->state.abilityDirection = ability->abilityDirection;
            if (ability->abilityDirection != default)
            {
                bool invertDirection = false;
                // we want to reverse direction to make it make sense to player
                // when they use directional button to go in a direction, they don't go in the opposite
                //if (motion != null && motion.Length > 0 && motion[0].force.X < 0) invertDirection = true;
                if (faceWrongDirection) invertDirection = !invertDirection;

                controller->RotateController(f, ability->abilityDirection * (invertDirection ? -FP._1 : FP._1));
            }
            else
            {
                ability->abilityDirection = controller->state.direction;
            }
            if (alreadyAimed == false && isAPerFrameCall == false && setsAbilityTarget && (abilityInput == 1 || inputDir == default || f.Has<BRBot>(owner)))
            {
                var aim = AutoAim(f, ability, owner, transform, controller, inputDir);
                controller->abilityTarget = aim.target;
            }

            if (isAPerFrameCall == false) input->AbilityDirection = default;
        }

        /// <summary>
        /// Gets where we want to spawn the damageZone
        /// </summary>
        /// <returns></returns>
        public virtual unsafe FPVector2 GetDamageZonePosition(Frame f, Ability* ability)
        {
            return f.Unsafe.GetPointer<Transform2D>(ability->owner)->Position;
        }

        /// <summary>
        /// Every frame while ability is running
        /// </summary>
        public virtual unsafe void UpdateAbility(Frame f, Ability* ability, AbilityData abilityData)
        {
            if (ability->hasEnded) return;

            if (ability->isBot)
            {
                if (f.Unsafe.TryGetPointer<CharacterController>(ability->owner, out var controller)
                    && f.Unsafe.TryGetPointer<Transform2D>(ability->owner, out var transform)
                    && f.Unsafe.TryGetPointer<Playable>(ability->owner, out var playable))
                {
                    if (specialBehavior.HasFlag(AbilitySpecialBehavior.CanChangeFacingDuring))
                    {
                        //TODO
                    }

                    HandleUniqueBotBehavior(f, ability, abilityData, controller, transform, playable);
                }
            }
            else
            {
                if (buttonType == AbilityButtonType.Hold)
                {
                    if (f.Unsafe.TryGetPointer<CharacterController>(ability->owner, out var controller) && f.Unsafe.TryGetPointer<Playable>(ability->owner, out var playable))
                    {
                        var input = PlayableHelper.GetInput(f, playable);
                        if (controller->CanRotate() && input->AbilityDirection != default)
                        {
                            controller->RotateController(f, input->AbilityDirection);
                            controller->state.abilityDirection = input->AbilityDirection;
                        }
                    }
                }

                if (specialBehavior.HasFlag(AbilitySpecialBehavior.CanChangeFacingDuring))
                {
                    //TODO
                }
            }



            SequenceIfNeeded(f, ability, false);
        }

        public static bool IsFacingAway(FPVector2 position, FPVector2 direction, FPVector2 positionToCheck)
        {
            FPVector2 dir = positionToCheck - position;
            return FPVector2.Dot(dir, direction) < 0;
        }

        public virtual unsafe void HandleUniqueBotBehavior(Frame f, Ability* ability, AbilityData abilityData, CharacterController* controller, Transform2D* transform, Playable* playable)
        {
            // charge until we're under threat
            if (buttonType == AbilityButtonType.Charged)
            {
                bool holdIt = true;

                if (holdIt && f.Unsafe.TryGetPointer<Health>(ability->owner, out var health) && f.Global->time < health->lastTimeHitByDirectAbility + FP._0_05)
                {
                    if (f.RNG->Next() <= FP._0_50) holdIt = false;
                }
                // character is leaving our attack range?
                if (holdIt)
                {
                    EntityRef target = AIHelper.GetTarget(f, ability->owner);
                    if (target != default && f.Unsafe.TryGetPointer<Transform2D>(target, out var targetTransform) && f.Unsafe.TryGetPointer<CharacterController>(target, out var targetController))
                    {
                        //bool targetLeaving = false;
                        // we're on the left of our target
                        bool targetLeaving = IsFacingAway(transform->Position, targetController->state.direction, targetTransform->Position);

                        if (targetLeaving && FPVector2.Distance(targetTransform->Position, transform->Position) > maxReach * (FP._0_75 + FP._0_10))
                        {
                            holdIt = false;
                        }
                    }
                }
                playable->botData.botInput.abilityButton = holdIt;
            }
            if (botBehavior.HasFlag(AbilityBotUniqueBehavior.KeepFacingTarget))
            {
                var target = AIHelper.GetTarget(f, ability->owner);
                if (target != default && f.Unsafe.TryGetPointer<Transform2D>(target, out var targetTransform))
                {
                    transform->Rotation = FPMath.Lerp(transform->Rotation, FPVector2.RadiansSigned(FPVector2.Up, targetTransform->Position - transform->Position), f.DeltaTime * FP._3);
                }
            }
            if (botBehavior.HasFlag(AbilityBotUniqueBehavior.KeepMovingTowardTarget))
            {
                var target = AIHelper.GetTarget(f, ability->owner);
                if (target != default)
                {
                    if (f.Unsafe.TryGetPointer<Transform2D>(target, out var targetTransform))
                    {
                        if (FPVector2.Distance(transform->Position, targetTransform->Position) > FP._1_50)
                        {
                            controller->MoveToward(f, targetTransform->Position, transform, playable);
                        }
                    }
                }
            }
            if (botBehavior.HasFlag(AbilityBotUniqueBehavior.ControllableProjectile))
            {
                var target = AIHelper.GetTarget(f, ability->owner);
                if (target != default)
                {
                    if (f.Unsafe.TryGetPointer<Transform2D>(target, out var targetTransform) && BotHelper.IsEntityStealthToUs(f, ability->owner, target) == false)
                    {
                        FPVector2 targetPos = BotHelper.BestGuessPositionOnTarget_NoNavmeshConsideration(f, ability->owner, target, targetTransform);
                        if (ability->tempEntity != default && f.Unsafe.TryGetPointer<Transform2D>(ability->tempEntity, out var pt))
                        {
                            playable->botData.botInput.MovementDirection = (targetPos - pt->Position).Normalized;
                        }
                        else playable->botData.botInput.MovementDirection = (targetPos - transform->Position).Normalized; //((targetTransform->Position + FPVector2.Up) - (transform->Position + FPVector2.Up)).Normalized;
                    }
                }

            }
        }
        public virtual unsafe void ClickedWhileOnCD(Frame f, Ability* ability, AbilityData abilityData)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.ClickingOnCDInterrupts))
            {
                if (ability != default && ability->timeElapsed < FP._0_33 && buttonType != AbilityButtonType.Hold) return;

                FastForwardAbility(f, ability);
            }
        }
        /// <summary>
        /// End an ability
        /// </summary>
        public virtual unsafe void EndAbility(Frame f, Ability* ability)
        {
            if (ability->hasEnded) return;

            f.Signals.AbilityEnded(ability, this);

            SequenceIfNeeded(f, ability, true);
        }
        /// <summary>
        /// Interrupt an active ability
        /// </summary>
        public virtual unsafe void InterruptAbility(Frame f, Ability* ability, FP lockout = default)
        {
            ability->mark |= AbilityMark.MarkedForInterruption;
            if (lockout > 0)
            {
                if (ability->cooldownTimer < lockout)
                {
                    ability->cooldownTimer = lockout;
                }
            }
        }
        /// <summary>
        /// Must be called when an ability ends / interrupted
        /// </summary>
        /// <param name="f"></param>
        /// <param name="owner"></param>
        public virtual unsafe void CleanUp(Frame f, Ability* ability, EntityRef owner)
        {
            if (ability->hasEnded) return;

            ability->hasEnded = true;

            // clear bot ability direction
            if (f.Unsafe.TryGetPointer<Playable>(owner, out var playable) && playable->isBot)
            {
                playable->botData.botInput.AbilityDirection = FPVector2.Zero;
            }

            // count ability mashing
            for (int i = 0; i < f.Global->pvpMatchInfo.players.Length; i++)
            {
                if (f.Global->pvpMatchInfo.players[i].entity == owner)
                {
                    if (f.Unsafe.TryGetPointer<PlayerGameStats>(owner, out var pgs))
                    {
                        pgs->abilitiesUsed++;
                    }
                }
            }

            ChangePermissions(f, ability, owner, false);
            // create end damage zones
            CreateDamageZonesAtEnd(f, owner, ability);
            CharacterController* controller = default;
            if (f.Unsafe.TryGetPointer<CharacterController>(owner, out controller))
            {
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.DisabledCharacterCollision))
                {
                    controller->permissions.noCharacterCollision--;
                }
                if (specialBehavior.HasFlag(AbilitySpecialBehavior.CannotTurnDuring))
                {
                    controller->state.cannotTurn = false;
                }

                if (motion.Length > 0) //&& f.Global->time - controller->parameters.hammockHitTime > FP._0_50)
                {
                    controller->parameters.abilityMotion = FPVector2.Zero;
                }
                if (ability->gravityWasDisabled)
                {
                    controller->GravityActive(true);
                }
            }

            // delete any damage zones that should be gone with us
            foreach (var zone in f.GetComponentIterator<DamageZone>())
            {
                if (zone.Component.owner == owner && zone.Component.destroyOnAbilityEnd >= 0 && zone.Component.destroyOnAbilityEnd == abilityId)
                {
                    f.Destroy(zone.Entity);
                }
            }

            // delete any effects that should be gone with us
            if (f.Unsafe.TryGetPointer<EffectHandler>(owner, out var handler))
            {
                switch (abilityType)
                {
                    case AbilityType.Ability:
                        handler->RemoveByCustomBehavior(f, CustomEffectBehavior.RemoveWhenOwnerExitAbility);
                        break;
                    case AbilityType.Dodge:
                    case AbilityType.Jump:
                    case AbilityType.Utility:
                        handler->RemoveByCustomBehavior(f, CustomEffectBehavior.RemoveWhenOwnerExitUtility);
                        break;
                    case AbilityType.Attack:
                        handler->RemoveByCustomBehavior(f, CustomEffectBehavior.RemoveWhenOwnerExitAttack);
                        break;
                    case AbilityType.ConsumeItem:
                        handler->RemoveByCustomBehavior(f, CustomEffectBehavior.RemoveWhenOwnerExitConsumeItem);
                        break;
                }
            }

            // destroy any motion
            if (motion.Length > 0)
            {
                foreach (var m in f.GetComponentIterator<Motion>())
                {
                    if (m.Component.entity == owner && m.Component.isHammock == false && (m.Component.abilityId == default || m.Component.abilityId == abilityId) && m.Component.terminated == 0)
                    {
                        if (f.Unsafe.TryGetPointer<Motion>(m.Entity, out var motion))
                        {
                            MotionSystem.TerminateMotion(f, m.Entity, motion, controller, false);
                        }
                    }
                }
            }

            if (controller != default && controller->parameters.distanceCrossedDuringLastAbility > FP._0)
            {
                OnDistanceCrossedDuringAbility(f, owner, ability, controller->parameters.distanceCrossedDuringLastAbility);
                if (f.Unsafe.TryGetPointer<ItemInventory>(owner, out var itemInventory))
                {
                    itemInventory->OnDistanceCrossedDuringAbility(f, owner, ability, controller->parameters.distanceCrossedDuringLastAbility);
                }
            }

            AbilityInventory.TriggerUpdateUIEvent(f, ability->owner);
        }
        public virtual unsafe void OnDistanceCrossedDuringAbility(Frame f, EntityRef owner, Ability* ability, FP distance)
        {

        }
        protected virtual unsafe void ChangePermissions(Frame f, Ability* ability, EntityRef owner, bool add)
        {
            // character permissions
            if (owner != null && f.Unsafe.TryGetPointer<CharacterController>(owner, out var controller))
            {
                if (add)
                {
                    if (!permissions.movement) controller->permissions.movement++;
                    if (!permissions.ability) controller->permissions.ability++;
                    if (!permissions.utility) controller->permissions.utility++;
                    if (!permissions.attack) controller->permissions.attack++;
                    if (permissions.noRotate) controller->permissions.noRotate++;
                    if (permissions.noCharacterCollision) controller->permissions.noCharacterCollision++;
                    controller->inAbilityCount++;
                }
                else
                {
                    if (!permissions.movement) controller->permissions.movement--;
                    if (!permissions.ability) controller->permissions.ability--;
                    if (!permissions.utility) controller->permissions.utility--;
                    if (!permissions.attack) controller->permissions.attack--;
                    if (permissions.noRotate) controller->permissions.noRotate--;
                    if (permissions.noCharacterCollision) controller->permissions.noCharacterCollision--;
                    controller->inAbilityCount--;
                }
            }
            if (owner != null && f.Unsafe.TryGetPointer<AbilityInventory>(owner, out var inventory))
            {
                if (add) inventory->inAbilityCount++;
                else inventory->inAbilityCount--;
            }
        }

        /// <summary>
        /// Looks for an ability that matches nextAbilityData, then interrupts currentAbility and starts the new one
        /// </summary>
        public virtual unsafe void SequenceIntoAbility(Frame f, Ability* currentAbility, AssetRefAbilityData nextAbilityRef)
        {
            if (nextAbilityRef == default) return;

            // we need to wrap up this ability
            FastForwardAbility(f, currentAbility);

            // we need the actual ability from character inventory
            if (currentAbility->owner != default && f.Unsafe.TryGetPointer<AbilityInventory>(currentAbility->owner, out var inventory))
            {
                inventory->sequencedAbility = nextAbilityRef;

                // set this as our last attack if it's an attack. To avoid conflict with buffered input
                if (abilityType == AbilityType.Attack && sequence != null && sequence.Length > 0 && sequence[0].decider == SequenceDecider.BufferedInput)
                {
                    inventory->lastAttack = nextAbilityRef;
                    inventory->lastAttackTimer = AbilityInventory.BUFFERED_ATTACK_WINDOW;
                }
            }
        }
        /// <summary>
        /// Instantly ENDS an ability (this in not an interrupt)
        /// </summary>
        public virtual unsafe void FastForwardAbility(Frame f, Ability* ability)
        {
            ability->mark |= AbilityMark.MarkedForFastForward;
        }
        /// <summary>
        /// Brings to life a motion controller
        /// </summary>
        /// <param name="f"></param>
        public virtual unsafe void MaterializeMotion(Frame f, EntityRef entity, MotionData motionData, Ability* ability, FP distanceOverride = default)
        {
            if (motionData == null || entity == null)
            {
                return;
            }
            EntityRef r = PrototypeHelper.CreateEntity(f, CreationSource.AbilityMaterializeMotion);
            if (f.AddOrGet<Motion>(r, out var motion))
            {
                motion->entity = entity;
                motion->distance = distanceOverride == 0 ? motionData.distance : distanceOverride;
                motion->force = motionData.force;
                motion->stopDistance = motionData.stopDistBonus;
                if (ability != default && ability->abilitySpeed > 0)
                {
                    motion->delay = motionData.delay / ability->abilitySpeed;
                }
                else
                {
                    motion->delay = motionData.delay;
                }

                if (motionData.motionFlags.HasFlag(MotionFlags.DistanceByAbilityDirection))
                {
                    motion->force.X = FPMath.Lerp(FP._5, motion->force.X, ability->abilityDirection.Magnitude);
                }

                //motion->stopDistance = 1 + FP._0_25;
                motion->ResidualForce = motionData.residualPercentage;
                motion->motionFlags = motionData.motionFlags;
                motion->abilityId = abilityId;

                if (motionData.motionFlags.HasFlag(MotionFlags.StopBehindTargetIfStealth))
                {
                    // set stop behind target if we triggered from stealth
                    if (f.Unsafe.TryGetPointer<Stealth>(motion->entity, out var stealth))
                    {
                        if (stealth->IsStealth) motion->motionFlags |= MotionFlags.StopBehindTarget;
                    }
                }

                if (motionData.interruptAbilityOnEnd)
                {
                    motion->interruptId = abilityId;
                }
                else
                {
                    motion->interruptId = -1;
                }
            }
            // something went wrong? prevent lingering entity
            else
            {
                f.Destroy(r);
            }
        }



        public unsafe virtual void SequenceIfNeeded(Frame f, Ability* ability, bool isCalledOnEnd)
        {
            if (sequence == null || sequence.Length == 0) return;

            for (int i = 0; i < sequence.Length; i++)
            {
                // should we sequence into another ability?
                if (sequence[i].sequencedAbilityRef != null)
                {
                    if (SequenceConditionsMet(f, sequence[i], ability, isCalledOnEnd))
                    {
                        SequenceIntoAbility(f, ability, sequence[i].sequencedAbilityRef);
                        return;
                    }
                }

                // movement interrupts this ability?
                if (sequence[i].skippableAfter > 0 && ability->timeElapsed > sequence[i].skippableAfter)
                {
                    if (sequence[i].decider != SequenceDecider.DealtDamage && sequence[i].decider != SequenceDecider.ReceivedDamage)
                    {
                        if (f.Unsafe.TryGetPointer<Playable>(ability->owner, out var bot))
                        {
                            var input = PlayableHelper.GetInput(f, bot);
                            if (input != default && input->MovementDirection != default)
                            {
                                InterruptAbility(f, ability);
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Returns whether or not we're allowed to go to next ability
        /// </summary>
        public unsafe virtual bool SequenceConditionsMet(Frame f, AbilitySequence sequence, Ability* ability, bool isCalledOnEnd = false)
        {
            switch (sequence.decider)
            {
                case SequenceDecider.AlwaysPlay:
                    return isCalledOnEnd;
                case SequenceDecider.DiceRoll:
                    if (isCalledOnEnd == false) return false;
                    else
                    {
                        FP min = 0;
                        FP max = 100;
                        FP result = f.RNG->NextInclusive(min, max);
                        return result <= sequence.diceOdds;
                    }
                case SequenceDecider.BufferedInput:
                    if (ability->timeElapsed > sequence.skippableAfter)
                    {
                        if (ability->owner != null && f.Unsafe.TryGetPointer<Playable>(ability->owner, out Playable* playable))
                        {
                            var input = PlayableHelper.GetInput(f, playable);
                            if (input != null)
                            {
                                if (input->abilityButton.IsDown && input->abilityID == abilityInput)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                case SequenceDecider.DealtDamage:
                    return ability->dealtDamage;
                case SequenceDecider.ReceivedDamage:
                    return ability->receivedDamage;
                case SequenceDecider.DidntDealDamage:
                    // our ability ended and we didn't deal damage
                    return isCalledOnEnd && ability->dealtDamage == false;
            }
            return false;
        }

        /// <summary>
        /// Returns whether an ability poses a threat to given target
        /// </summary>
        /// <param name="f"></param>
        /// <param name="owner"></param>
        /// <param name="target"></param>
        /// <param name="targetTransform"></param>
        /// <param name="targetController"></param>
        /// <returns></returns>
        public unsafe virtual bool IsAThreatTo(Frame f, EntityRef owner, EntityRef target, Transform2D* targetTransform, CharacterController* targetController, bool updateAiming = false)
        {
            if (specialBehavior.HasFlag(AbilitySpecialBehavior.IsAlwaysAThreat))
            {
                return true;
            }

            if (targetTransform == default || f.Unsafe.TryGetPointer<Transform2D>(owner, out var transform) == false)
            {
                return false;
            }

            // we look for our damage delay, if none we assume 1 seconds is how long until damage hits
            FP attackDelay = FP._0_50;
            FP damageReach = FP._0;
            FP damageCenter = FP._0;
            FP controllableRange = FP._0;
            if (damage.Length > 0)
            {
                attackDelay = damage[0].delay;
                for (int j = 0; j < damage.Length; j++)
                {
                    FP reach = (damage[j].bounds.Extents.Y + damage[j].bounds.Center.Y) / FP._2;
                    if (damage[j].abilityDirectionBonus > controllableRange)
                    {
                        controllableRange = damage[j].abilityDirectionBonus;
                    }
                    if (damageCenter < damage[j].bounds.Center.Y)
                    {
                        damageCenter = damage[j].bounds.Center.Y;
                    }
                    if (reach > damageReach)
                    {
                        damageReach = reach;
                    }
                    if (damage[j].delay < attackDelay)
                    {
                        attackDelay = damage[j].delay;
                    }
                }
            }

            // avoids cases where our distance is almost literally 0 and enemy will never attack us
            FP min = minReach;
            FP max = maxReach;

            FP targetDist = FPVector2.Distance(transform->Position, targetTransform->Position);

            if (f.TryGet<BRMemory>(owner, out var championTargets) && botBehavior.HasFlag(AbilityBotUniqueBehavior.DoesntRequireVision) == false)
            {
                // having our view obstructed doesn't mean we can't attempt to use an ability. it just means our reach is not limited to our hitbox.
                // MOtion and projectiles don't factor in anymore
                if (BotHelper.ViewToTargetIsObstructed(f, owner, target))
                {
                    if (max > damageReach) max = damageReach;
                    if (max <= FP._0) return false;
                }//return false;
                //Log.Info($"{f.Number}: View to {target} who is at position {targetTransform->Position} is not obstructed from {transform->Position}");
            }

            if (min == FP._0) min = -FP._1;
            if (max <= min) max = min + FP._0_25;

            // -- 2
            FPVector2 targetPositionAfterDelay = targetTransform->Position;
            if (targetController != default) targetPositionAfterDelay = BotHelper.BestGuessPositionOnTarget_NoNavmeshConsideration(f, owner, target, targetTransform, targetController, attackDelay);
            FP predictedDistance = FPVector2.Distance(transform->Position, targetPositionAfterDelay);
            // Bot ability range aiming
            if (updateAiming && f.Unsafe.TryGetPointer<Playable>(owner, out var playable))
            {
                playable->botData.botInput.AbilityDirection = (targetTransform->Position - transform->Position).Normalized;
                if (controllableRange > FP._0)
                {
                    //Log.Info($"Controllable Range: {controllableRange}");
                    FP d = predictedDistance - damageCenter;
                    if (d <= FP._0) d = FP._0;
                    else if (d >= controllableRange) d = controllableRange;
                    FP range = (d / controllableRange) * FP._100;
                    if (range > FP._0 && range <= 250)
                    {
                        playable->botData.botInput.abilityRange = (byte)FPMath.RoundToInt(range);
                    }
                }
            }

            if (dontPredictReach)
            {
                return targetDist <= max && targetDist >= min;
            }
            else if (predictedDistance <= max && predictedDistance >= min)
            {
                return true;
            }
            return false;
        }
        public virtual unsafe void OnAboutToCalculateDealtDamage(Frame f, DamageCalculationData* damageData, Ability* ability = default, AssetRefAbilityData abilityRef = default)
        {
        }
        public virtual unsafe void OnAboutToCalculateReceivedDamage(Frame f, DamageCalculationData* damageData, Ability* ability = default, AssetRefAbilityData abilityRef = default)
        {
        }
        // ---------------------------------------------- About To deal
        /// <summary>
        /// Player has dealt damage to a target
        /// If ability* is not default, this was called while ability is active
        /// </summary>
        public virtual unsafe void OnAboutToDealAbilityDamage(Frame f, EntityRef owner, EntityRef target, DamageResult* damageResult, Ability* ability = default, AssetRefAbilityData abilityRef = default, bool isFirstHit = true)
        {
        }
        /// <summary>
        /// Player has dealt damage to a target
        /// If ability* is not default, this was called while ability is active
        /// </summary>
        public virtual unsafe void OnAboutToReceiveAbilityDamage(Frame f, EntityRef owner, EntityRef dealer, DamageResult* damageResult, Ability* ability = default, AssetRefAbilityData abilityRef = default)
        {
        }
        // ----------------------------------------------- Dealt
        /// <summary>
        /// Player has dealt damage to a target
        /// If ability* is not default, this was called while ability is active
        /// </summary>
        public virtual unsafe void OnDealtAbilityDamage(Frame f, EntityRef owner, EntityRef target, DamageResult* damageResult, Ability* ability = default, AssetRefAbilityData abilityRef = default, bool isFirstHit = true)
        {
            if (ability != default)
            {
                ability->dealtDamage = true;

                // the damage is supposed to terminate ability?
                if (damageResult != default && damageResult->damageZone != default)
                {
                    if (damage != null && damageResult->damageZone->damageIndexInAbility < damage.Length && damageResult->damageZone->damageIndexInAbility >= 0)
                    {
                        if (damage[damageResult->damageZone->damageIndexInAbility].behavior.HasFlag(AbilityDamageBehavior.TerminatesAbility))
                        {
                            FastForwardAbility(f, ability);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Player has dealt damage to a target
        /// If ability* is not default, this was called while ability is active
        /// </summary>
        public virtual unsafe void OnReceivedAbilityDamage(Frame f, EntityRef owner, EntityRef dealer, DamageResult* damageResult, Ability* ability = default, AssetRefAbilityData abilityRef = default)
        {
            if (ability != default)
            {
                ability->receivedDamage = true;
            }
        }
        /// <summary>
        /// Player has dealt damage to a target
        /// If ability* is not default, this was called while ability is active
        /// </summary>
        public virtual unsafe void OnAboutToDealGenericDamage(Frame f, EntityRef owner, EntityRef target, DamageResult* damageResult, GenericDamageData genericData, Ability* ability = default)
        {
        }
        /// <summary>
        /// Player has dealt damage to a target
        /// If Generic* is not default, this was called while Generic is active
        /// </summary>
        public virtual unsafe void OnAboutToReceiveGenericDamage(Frame f, EntityRef owner, EntityRef dealer, DamageResult* damageResult, GenericDamageData genericData, Ability* ability = default)
        {
        }
        // ----------------------------------------------- Dealt
        /// <summary>
        /// Player has dealt damage to a target
        /// If Generic* is not default, this was called while Generic is active
        /// </summary>
        public virtual unsafe void OnDealtGenericDamage(Frame f, EntityRef owner, EntityRef target, DamageResult* damageResult, GenericDamageData genericData, Ability* ability = default)
        {
        }
        /// <summary>
        /// Player has dealt damage to a target
        /// If Generic* is not default, this was called while Generic is active
        /// </summary>
        public virtual unsafe void OnReceivedGenericDamage(Frame f, EntityRef owner, EntityRef dealer, DamageResult* damageResult, GenericDamageData genericData, Ability* ability = default)
        {

        }
        /// <summary>
        /// We've hit something with out projectile
        /// </summary>
        /// <param name="f"></param>
        /// <param name="projectileEntity"></param>
        /// <param name="hitTarget"></param>
        public virtual unsafe void OnProjectileEvent(Frame f, Projectile* projectile, EntityRef hitTarget, Ability* ability, FPVector2 hitPoint, ProjectileEventType type, bool whileAbilityIsActive)
        {

        }
        /// <summary>
        /// If ability is not default, then it is active
        /// </summary>
        /// <param name="f"></param>
        /// <param name="entity"></param>
        /// <param name="collector"></param>
        /// <param name="collectible"></param>
        /// <param name="ability"></param>
        public virtual unsafe void OnCollectedCollectible(Frame f, EntityRef entity, EntityRef collector, Collectible* collectible, Ability* ability)
        {

        }

        public virtual unsafe void OnTakedown(Frame f, EntityRef ourEntity, EntityRef deadEntity, EntityRef killer, bool isKiller, Ability* ability)
        {

        }

        public virtual unsafe void OnKilled(Frame f, EntityRef ourEntity, EntityRef killer, bool isSuicide, Ability* ability)
        {

        }
    }
}