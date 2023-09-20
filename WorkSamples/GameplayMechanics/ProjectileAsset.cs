namespace MySamples
{

    public unsafe abstract partial class ProjectileData
    {
        [Flags]
        public enum ProjectileFlags : UInt32
        {
            None = 0,
            Bounces = 1,
            RandomizeBounceDirection = 2,
            DestroyOnCollision = 4,
            IsConsideredAnAttack = 8,
            DestroyOnSurfaceCollision = 16,
            DontDealDirectDamage = 32,
            InvincibleToDamageZone = 64,
        }
        [Tooltip("Used by the visual simulation to render the correct projectile")]
        public int projectileId;
        [Header("Motion")]
        [Tooltip("The maximum distance the projectile is allowed to travel")]
        public FP distanceLimit = 15;
        [Tooltip("How fast does this projectile travel per second")]
        public FP speed = 10;
        public ProjectileFlags flags;
        [Tooltip("only used with targetting behaviors that use it")]
        public FPVector2 angleVector;
        [Tooltip("To distinguish what spell triggered this projectile.")]
        public byte inputId;

        [Header("Collision")]
        public bool isPlayer;
        [Tooltip("Is this unique to player controlled entities only?")]
        public FP thickness = 0;
        [Tooltip("Who can interact with the projectile")]
        public ProjectileCollision collisionTargets = ProjectileCollision.Enemies;


        [Header("Damage")]
        public int damageValue;
        public FP apRatio;
        public FP adRatio;
        public byte feedbackId;
        [Tooltip("Basically whether this is an attack or ability (for damage calculation purposes)")]
        public AssetRefEffectData[] effects;
        [Tooltip("Target will be marked as already hit by the damage zones if this true AND the target was hit. This doesn't make them immune to all zone hits. It just registers them as hit. So invincibilty duration of the zone will control that")]
        public AbilityDamage[] impactDamageZones;

        /// <summary>
        /// Spawn projectile
        /// </summary>
        /// <param name="f"></param>
        /// <param name="spawner">Spawning source: usually a player controlled entity or minion</param>
        /// <param name="position">Spawning position</param>
        /// <param name="direction">if default, we'll calculate a direction</param>
        /// <param name="dataRef">A reference to the actual projectile implementation that we want to use</param>
        /// <returns></returns>
        public virtual Projectile* Spawn(Frame f, EntityRef spawner, FPVector2 position, FPVector2 direction = default, AssetRefProjectileData dataRef = default, int abilityId = 0)
        {
            EntityRef e = default;

            var prototype = f.FindAsset<EntityPrototype>(PrototypeHelper.PATH_PROJECTILE_BASE);
            e = PrototypeHelper.CreateEntity(f, prototype, CreationSource.ProjectileAsset);

            if (f.AddOrGet<Transform2D>(e, out var transform) && f.AddOrGet<Projectile>(e, out var projectile))
            {
                transform->Position = position;
                // share direction in case we want to use forward
                if (f.Unsafe.TryGetPointer<Transform2D>(spawner, out var spawnerTransform))
                {
                    transform->Rotation = spawnerTransform->Rotation;
                }
                projectile->owner = spawner;
                projectile->projectileId = projectileId;
                projectile->abilityId = abilityId;
                projectile->entity = e;
                if (dataRef != default) projectile->projectileData = dataRef;
                else projectile->projectileData = this;
                projectile->isPlayer = isPlayer;
                Initialize(f, projectile, transform, direction);
                return projectile;
            }

            return default;
        }
        /// <summary>
        /// How far this projectile is allowed to travel
        /// </summary>
        protected virtual FP GetDistanceLimit(Frame f, Projectile* projectile)
        {
            if (f.Unsafe.TryGetPointer<CharacterProfile>(projectile->owner, out var profile))
            {
                return distanceLimit + profile->GetStat(f, ProfileHelper.STAT_PROJECTILE_RANGE);
            }
            return distanceLimit;
        }
        /// <summary>
        /// Called when a projectile is created
        /// </summary>
        public virtual void Initialize(Frame f, Projectile* projectile, Transform2D* transform, FPVector2 direction = default)
        {
            projectile->projectileId = projectileId;
            if (f.Unsafe.TryGetPointer<CharacterProfile>(projectile->owner, out var profile))
            {
                projectile->bonusSpeed = speed * profile->GetStat(f, ProfileHelper.STAT_PROJECTILE_SPEED);
            }
            // no direction was set
            if (direction == default)
            {
                if (f.Unsafe.TryGetPointer<CharacterController>(projectile->owner, out var controller) && controller->state.abilityDirection != default)
                {
                    direction = controller->state.abilityDirection;
                }
                else if (projectile->owner != default && f.TryGet<Transform2D>(projectile->owner, out var ot))
                {
                    direction = ot.Up;
                }
                else direction = transform->Forward;
            }
            projectile->direction = direction.Normalized;
            projectile->startPosition = transform->Position;
            // decide how many rays we need
            SetupQueryRays(f, projectile);

            f.Events.ProjectileEvent(projectile->owner, projectile->entity, default, projectileId, transform->Position, ProjectileEventType.Fired);
            f.Signals.ProjectileState(projectile->entity, default, default, ProjectileEventType.Fired);

            projectile->original_owner = projectile->owner;
        }
        FPVector2 RotateVector(FPVector2 v, FP degrees)
        {
            return RotateRadians(v, degrees * FP.Deg2Rad);
        }
        FPVector2 RotateRadians(FPVector2 v, FP radians)
        {
            var ca = FPMath.Cos(radians);
            var sa = FPMath.Sin(radians);
            return new FPVector2(ca * v.X - sa * v.Y, sa * v.X + ca * v.Y);
        }
        /// <summary>
        /// Setup ray count and spacing for broad phase raycasting
        /// </summary>
        /// <param name="f"></param>
        /// <param name="projectile"></param>
        protected virtual void SetupQueryRays(Frame f, Projectile* projectile)
        {
            int count = 1;
            if (thickness > FP._0)
            {
                if (thickness > FP._2)
                {
                    count = 1 + FPMath.RoundToInt(thickness / FP._0_75);
                }
                else if (thickness > FP._0_50)
                {
                    count = FPMath.CeilToInt(thickness / FP._0_50);
                }
                else
                {
                    count = 1 + FPMath.RoundToInt(thickness / FP._0_50);
                }
            }
            var list = projectile->GetQueries(f);
            for (int i = 0; i < count; i++)
            {
                list.Add(0);
            }
        }

        /// <summary>
        /// Called on the frame a projectile is destroyed
        /// </summary>
        public virtual void Terminate(Frame f, Projectile* projectile, FPVector2 pointOfImpact, bool haveHitSomething = false, EntityRef hitTarget = default)
        {
            // no double termination sources (hit / terminate (expire) at the same time)
            if (projectile->isTerminated)
            {
                return;
            }
            projectile->isTerminated = true;
            if (hitTarget != default || haveHitSomething)
            {
                f.Events.ProjectileEvent(projectile->original_owner, projectile->entity, hitTarget, projectileId, pointOfImpact, ProjectileEventType.Hit);
                f.Signals.ProjectileState(projectile->entity, hitTarget, pointOfImpact, ProjectileEventType.Hit);
            }
            else
            {
                f.Events.ProjectileEvent(projectile->original_owner, projectile->entity, hitTarget, projectileId, pointOfImpact, ProjectileEventType.Terminated);
                f.Signals.ProjectileState(projectile->entity, hitTarget, pointOfImpact, ProjectileEventType.Terminated);
            }
            if (f.Unsafe.TryGetPointer<Transform2D>(projectile->entity, out var t))
            {
                t->Position = pointOfImpact;
            }
            OnProjectileTerminated(f, projectile, pointOfImpact, haveHitSomething, hitTarget);
        }
        /// <summary>
        /// Projectile has just ended. The pointer is still valid during this function and can be safely used.
        /// </summary>
        protected virtual void OnProjectileTerminated(Frame f, Projectile* projectile, FPVector2 pointOfImpact, bool haveHitSomething, EntityRef hitTarget)
        {

        }

        /// <summary>
        /// Projectile movement. This function must calculate distance traveled and distance traveled last frame.
        /// Must set distanceTraveledLastFrame
        /// </summary>
        public virtual void Move(Frame f, Projectile* projectile, Transform2D* transform)
        {
            FPVector2 delta = projectile->direction * (speed + projectile->bonusSpeed) * f.DeltaTime;
            transform->Position += delta;
            projectile->distanceTraveledLastFrame = delta.Magnitude;
            FP limit = GetDistanceLimit(f, projectile);
            // did we overshoot?
            if (projectile->distanceTraveled + projectile->distanceTraveledLastFrame > limit)
            {
                FP overshotAmount = (projectile->distanceTraveled + projectile->distanceTraveledLastFrame) - limit;
                transform->Position -= delta.Normalized * overshotAmount;
                projectile->distanceTraveledLastFrame -= overshotAmount;
            }
        }

        public virtual void MonitorDistanceTraveled(Frame f, Projectile* projectile, Transform2D* transform)
        {
            //Using on last frame allows us to run collision queries before terminating (since collision raycast comes from a different system during broad phase)
            if (projectile->onLastFrame)
            {
                // create damage zones
                CreateImpactZones(f, projectile, transform->Position, false);
                Terminate(f, projectile, transform->Position);
            }
            else
            {
                projectile->distanceTraveled += projectile->distanceTraveledLastFrame;
                if (projectile->distanceTraveled >= GetDistanceLimit(f, projectile))
                {
                    projectile->onLastFrame = true;
                }
            }
        }

        /// <summary>
        /// Handles if a collision event has occurred
        /// </summary>
        /// <param name="f"></param>
        /// <param name="projectile"></param>
        public virtual void ProcessCollision(Frame f, Projectile* projectile)
        {
            projectile->SetFrameHits(f);
            var hits = projectile->GetFrameHits(f);
            var alreadyHit = projectile->GetAlreadyHit(f);
            if (flags.HasFlag(ProjectileFlags.DontDealDirectDamage))
            {
                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (hits[i].Entity != default)
                    {
                        hits.RemoveAt(i);
                    }
                }
            }
            EntityRef lastOwner = projectile->owner;
            if (hits.Count > 0)
            {
                // -- calculate point of impact if we had multiple hits
                FPVector2 impactPoint = hits[0].Point;
                EntityRef hitTarget = default;
                bool surfaceHit = false;
                byte allyHits = 0;
                if (hits.Count > 1)
                {
                    for (int i = 1; i < hits.Count; i++)
                    {
                        impactPoint += hits[i].Point;
                    }
                    impactPoint /= hits.Count;
                }
                for (int i = 0; i < hits.Count; i++)
                {
                    if (hits[i].Entity == projectile->owner)
                    {
                        continue;
                    }
                    if (hits[i].Entity == default)
                    {
                        surfaceHit = true;
                        continue;
                    }
                    bool isContained = false;
                    for (int j = 0; j < alreadyHit.Count; j++)
                    {
                        if (alreadyHit[j] == hits[i].Entity)
                        {
                            isContained = true;
                            break;
                        }
                    }
                    if (isContained)
                    {
                        continue;
                    }
                    bool areEnemies = AIHelper.AreEnemies(f, hits[i].Entity, projectile->owner);
                    bool isAPet = f.Has<OwnedByEntity>(hits[i].Entity);
                    if ((isAPet == false || collisionTargets.HasFlag(ProjectileCollision.IgnoreSpawns) == false) &&
                        ((areEnemies && collisionTargets.HasFlag(ProjectileCollision.Enemies)) || (areEnemies == false && collisionTargets.HasFlag(ProjectileCollision.Allies))))
                    {
                        if (HitTarget(f, projectile, hits[i].Entity))
                        {
                            hitTarget = hits[i].Entity;
                        }

                        // got stolen / reflected?
                        if (lastOwner != projectile->owner)
                        {
                            return;
                        }
                        alreadyHit.Add(hits[i].Entity);
                    }
                    // don't prevent colliding with walls and inanimate objects
                    else if (f.Has<Health>(hits[i].Entity))
                    {
                        allyHits++;
                    }
                }
                // should not collide
                if (allyHits == hits.Count) return;

                if (hitTarget == default && surfaceHit && flags.HasFlag(ProjectileFlags.Bounces))
                {
                    // execute bounce
                    if (projectile->lastHitPosition == default || FPVector2.Distance(projectile->lastHitPosition, impactPoint) > FP._1)
                    {
                        projectile->lastHitPosition = impactPoint;
                        //projectile->bounceFrame = f.Number;
                        Bounce(f, projectile, hits[0]);
                    }
                    return;
                }
                projectile->lastHitPosition = impactPoint;
                // create damage zones
                CreateImpactZones(f, projectile, impactPoint, true);
                if (hitTarget == default && flags.HasFlag(ProjectileFlags.DestroyOnSurfaceCollision) && surfaceHit)
                {
                    f.Events.ProjectileEvent(projectile->original_owner, projectile->entity, default, projectileId, impactPoint, ProjectileEventType.Hit);
                    f.Signals.ProjectileState(projectile->entity, default, impactPoint, ProjectileEventType.Hit);

                    Terminate(f, projectile, impactPoint, true, hitTarget);
                    return;
                }
                // regardless of outcome, having hit something destroys us
                if (flags.HasFlag(ProjectileFlags.DestroyOnCollision))
                {
                    Terminate(f, projectile, impactPoint, true, hitTarget);
                }
            }
        }
        /// <summary>
        /// Initiate projectile re-direct
        /// </summary>
        protected virtual void Bounce(Frame f, Projectile* projectile, Physics2D.Hit hit)
        {
            projectile->direction = FPVector2.Reflect(projectile->direction, hit.Normal);
            if (flags.HasFlag(ProjectileFlags.RandomizeBounceDirection))
            {
                projectile->direction = FPVector2.Rotate(projectile->direction, f.RNG->Next());
            }
            projectile->distanceTraveledLastFrame = FP._0;
            if (f.Unsafe.TryGetPointer<Transform2D>(projectile->entity, out var transform))
            {
                transform->Position = hit.Point;
            }

            f.Events.ProjectileEvent(projectile->original_owner, projectile->entity, default, projectileId, hit.Point, ProjectileEventType.Hit);
            f.Signals.ProjectileState(projectile->entity, default, hit.Point, ProjectileEventType.Hit);
        }

        /// <summary>
        /// Handles creating impact zones caused by a landing hit
        /// </summary>
        public virtual void CreateImpactZones(Frame f, Projectile* projectile, FPVector2 impactCenter, bool useAlreadyHit)
        {
            if (impactDamageZones != null)
            {
                for (int i = 0; i < impactDamageZones.Length; i++)
                {
                    if (impactDamageZones[i] != null)
                    {
                        var alreadyHit = projectile->GetAlreadyHit(f);
                        var damageZoneEntity = DamageZoneHelper.MaterializeDamageZone(projectileId, f, projectile->owner, impactDamageZones[i], impactCenter, projectile, this);
                        if (flags.HasFlag(ProjectileFlags.InvincibleToDamageZone) && damageZoneEntity != null && useAlreadyHit && alreadyHit.Count > 0 && f.Unsafe.TryGetPointer<DamageZone>(damageZoneEntity, out var zone))
                        {
                            for (int j = 0; j < alreadyHit.Count; j++)
                            {
                                zone->AddToDamageHistory(f, alreadyHit[j]);
                            }
                        }
                    }
                }
            }
        }
        public virtual int GetFeedbackId(Frame f, Projectile* projectile)
        {
            return feedbackId;
        }
        /// <summary>
        ///  Process was happens when we hit a target
        /// </summary>
        public virtual bool HitTarget(Frame f, Projectile* projectile, EntityRef targetRef)
        {
            if (CanStillHitTargets(f, projectile) == false || projectile->HasBeenHit(f, targetRef))
            {
                return false;
            }

            if (f.Unsafe.TryGetPointer<Health>(targetRef, out var targetHealth))
            {
                if (collisionTargets.HasFlag(ProjectileCollision.DontHurtAllies) == false || AIHelper.AreEnemies(f, targetRef, projectile->owner))
                {
                    OnAboutToDamage(f, projectile, targetRef, targetHealth);
                    projectile->AddToDamageHistory(f, targetRef);
                    targetHealth->DealDamage(projectileId, f, projectile->owner, damageValue + projectile->bonusDamage, apRatio, adRatio, GenericDamageSource.Projectile, GetFeedbackId(f, projectile), false, effects, projectile, flags.HasFlag(ProjectileFlags.IsConsideredAnAttack) ? DamageCategory.AttackDamage : DamageCategory.AbilityDamage);
                    OnDealtDamage(f, projectile, targetRef, targetHealth);
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// After this call, within this frame, damage will occur
        /// </summary>
        public virtual void OnAboutToDamage(Frame f, Projectile* projectile, EntityRef target, Health* targetHealth)
        {

        }
        /// <summary>
        /// Damage has just occurred.
        /// </summary>
        public virtual void OnDealtDamage(Frame f, Projectile* projectile, EntityRef target, Health* targetHealth)
        {

        }

        public virtual bool CanStillHitTargets(Frame f, Projectile* projectile)
        {
            if (flags.HasFlag(ProjectileFlags.DontDealDirectDamage)) return false;

            return true;
        }
    }
}