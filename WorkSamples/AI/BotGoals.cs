namespace MySamples
{
    internal unsafe class BotGoals
    {
        public static FP PRIORITY_MUST = FP._10;
        public static FP PRIORITY_HIGHEST = FP._3;
        public static FP PRIORITY_HIGH = FP._2;
        public static FP PRIORITY_STANDARD = FP._1;
        public static FP PRIORITY_LOW = FP._0_50;
        public static FP PRIORITY_NOT_APPLICABLE = FP._0;
        /************************************************************************************************************************/
        // Guidelines for prioritizing goals
        // 10 = MUST DO
        // 3 = highest
        // 2 = high
        // 1 = STANDARD
        // 0.5 = low
        // 0 = not applicable

        // if goals share equal priorities, the first goal in the list will be selected
        /************************************************************************************************************************/

        /// <summary>
        /// Select the goal that is best suited for this bot
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        public static FP EvaluateDefeatEnemy(Frame f, ref BRBotSystem.Filter filter)
        {
            if(f.RuntimeConfig.gameMode.HasFlag(GameMode.Tutorial))
            {
                if (f.Global->tutorialStage < 10) return PRIORITY_NOT_APPLICABLE;
            }

            switch (filter.bot->category)
            {
                case BotCategory.SurvivalEnemy:
                    return EvaluateDefeatEnemy_Survival(f, ref filter);
                case BotCategory.Minion:
                    return EvaluateDefeatEnemy_Minion(f, ref filter);
                default:
                case BotCategory.Champion:
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival) && (filter.team->team != ProfileHelper.NPC_TEAM)) return EvaluateDefeatEnemy_ChampionSurvival(f, ref filter);
                    return EvaluateDefeatEnemy_Champion(f, ref filter);
            }
        }

        public static FP EvaluateDestroyNexus(Frame f, ref BRBotSystem.Filter filter)
        {
            FP factor = FP._1;
            // If we haven't been attacked for a while, focus more on the win condition
            if (f.Global->time > filter.health->lastTimeHitByDirectAbility + FP._5)
            {
                factor = FP._2;
            }
            // A living nexus exists
            if (f.ComponentCount<SurvivalNexus>() > 0)
            {
                return PRIORITY_STANDARD * factor;
            }

            return PRIORITY_NOT_APPLICABLE;
        }

        public static FP EvaluateRevivePlayerSurvival(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival))
            {
                if (f.ComponentCount<Reviver>() > 0)
                {
                    return PRIORITY_HIGH;
                }
            }
            else
            {
                var comps = f.Filter<Reviver, Team>();

                while (comps.NextUnsafe(out var entity, out var rev, out var t))
                {
                    if (t->team == filter.team->team) return PRIORITY_HIGH;
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        public static FP EvaluateStayInBrush(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BrushUser>(filter.entity, out var user) && f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory))
            {
                if (user->IsInBrush())
                {
                    // 20% chance to stay in brush
                    if (filter.bot->goal != BotGoal.StayInBrush && f.RNG->Next() > FP._0_10 + FP._0_02) return PRIORITY_NOT_APPLICABLE;
                    // don't randomly stay in a bush when chasing an enemy
                    if (filter.bot->goal == BotGoal.DefeatEnemy) return PRIORITY_NOT_APPLICABLE;

                    // --- did an enemy enter our brush? can't waste time hiding. gotta deal with the threat
                    foreach (var x in f.GetComponentIterator<BrushUser>())
                    {
                        if (x.Component.brush == user->brush && x.Entity != filter.entity)
                        {
                            if (AIHelper.AreEnemies(f, filter.entity, x.Entity))
                            {
                                return PRIORITY_NOT_APPLICABLE;
                            }
                        }
                    }
                    FP max = FP._10 * FP._2;
                    FP factor = FP._1 - (FPMath.Clamp(memory->closestEnemy, FP._0, max) / max);

                    if (memory->closestEnemy < BotHelper.ONSCREEN_DIST / 2 && f.Global->time - filter.health->lastTimeHitByDirectAbility > FP._2)
                    {
                        if (user->enterTime + FP._5 > f.Global->time) return PRIORITY_HIGHEST * factor;
                        else if (user->enterTime + FP._10 > f.Global->time) return PRIORITY_HIGH * factor;
                        else if (user->enterTime + 15 > f.Global->time) return PRIORITY_STANDARD * factor;
                        else if (user->enterTime + 20 > f.Global->time) return PRIORITY_LOW * factor;
                        return PRIORITY_NOT_APPLICABLE;
                    }
                }
            }

            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Evaluates whether a bot should runaway or not
        /// </summary>
        /// <returns></returns>
        public static FP EvaluateRunaway(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory) == false || memory->enemy == default) return PRIORITY_NOT_APPLICABLE;
            if (f.Unsafe.TryGetPointer<Health>(memory->enemy, out var enemyHealth) == false) return PRIORITY_NOT_APPLICABLE;

            if(filter.bot->isPussy && filter.health->CurrentPercentage < FP._0_10 + FP._0_05 && enemyHealth->CurrentPercentage > FP._0_25)
            {
                    return PRIORITY_MUST * (FP._1 - FP._0_10);
            }

            // health threshold
            FP prio = PRIORITY_NOT_APPLICABLE;
            if (filter.health->CurrentPercentage < f.RNG->Next() * FP._0_75 && filter.health->CurrentPercentage > FP._0)
            {
                if (enemyHealth->CurrentPercentage > filter.health->CurrentPercentage * FP._1_50)
                {
                    prio += FPMath.Clamp(enemyHealth->CurrentPercentage / filter.health->CurrentPercentage, FP._0, PRIORITY_HIGH);
                    if (filter.bot->arsenal.HasFlag(ReasonForUse.RunawayFromTarget)) prio += FP._1;
                }
            }
            // check cooldowns (if we're CD reliant)
            if (f.Unsafe.TryGetPointer<AbilityInventory>(filter.entity, out var inventory))
            {
                var list = inventory->GetActiveAbilities(f);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].abilityInput < 4 || list[i].abilityInput > 6) continue;
                    prio += FP._0_50;
                }
            }
            return prio;
        }

        /// <summary>
        /// Determines whether or not the bot should follow their owner
        /// </summary>
        /// <returns>Priority</returns>
        public static FP EvaluateFollowOwner(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.TryGet<OwnedByEntity>(filter.entity, out var obe))
            {
                FP dist = QTools.Distance(f, obe.ownerEntity, filter.entity);
                if (dist > BotHelper.ONSCREEN_DIST)
                {
                    return PRIORITY_STANDARD;
                }
                else if (dist > FP._3)
                {
                    return PRIORITY_LOW;
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }
        /// <summary>
        /// Determine whether or not the bot should run from the death zone
        /// </summary>
        /// <returns></returns>
        public static FP EvaluateRunFromDeathZone(Frame f, ref BRBotSystem.Filter filter)
        {
            if (GameModeHelper.UsesDeathZone(f) == false) return PRIORITY_NOT_APPLICABLE;
            // while grabbing collectibles , we don't care about the death zone
            if (filter.bot->goal == BotGoal.GrabCollectible) return PRIORITY_NOT_APPLICABLE;

            if (f.Global->brManager != default && f.Unsafe.TryGetPointer<BRManager>(f.Global->brManager, out var manager))
            {
                // our offseted position
                var pos = filter.transform->Position + manager->bounds.Center;

                // are we inside? then GTFO
                if (pos.X < manager->bounds.Min.X || pos.X > manager->bounds.Max.X || pos.Y < manager->bounds.Min.Y || pos.Y > manager->bounds.Max.Y)
                {
                    // special case. We have no better option during this then to get out of the DZ
                    if (filter.bot->goal == BotGoal.RunawayFromEnemy) return PRIORITY_MUST;
                    return PRIORITY_HIGH;
                }

                // are we on the left or right side?
                FP dist = FP.UseableMax;
                // if we're close to the edge of the death zone, we should run
                if (pos.X > manager->bounds.Center.X)
                {
                    if (pos.Y > manager->bounds.Center.Y)
                    {
                        dist = FPMath.Min(FPMath.Abs(pos.X - manager->bounds.Max.X), FPMath.Abs(pos.Y - manager->bounds.Max.Y));
                    }
                    else
                    {
                        dist = FPMath.Min(FPMath.Abs(pos.X - manager->bounds.Max.X), FPMath.Abs(pos.Y - manager->bounds.Min.Y));
                    }
                }
                else
                {
                    if (pos.Y > manager->bounds.Center.Y)
                    {
                        dist = FPMath.Min(FPMath.Abs(pos.X - manager->bounds.Min.X), FPMath.Abs(pos.Y - manager->bounds.Max.Y));
                    }
                    else
                    {
                        dist = FPMath.Min(FPMath.Abs(pos.X - manager->bounds.Min.X), FPMath.Abs(pos.Y - manager->bounds.Min.Y));
                    }
                }

                // we now have the closest dist we have to an edge
                if (dist < FP._2)
                {
                    return PRIORITY_STANDARD;
                }
                else if (dist < FP._4)
                {
                    return PRIORITY_LOW;
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        // always an option
        public static FP EvaluateExploreWorld(Frame f, ref BRBotSystem.Filter filter)
        {
            return PRIORITY_LOW;
        }

        /// <summary>
        /// Evaluate whether we should deliver flag or not
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static FP EvaluateDeliverFlag(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Has<CarriedFlag>(filter.entity))
            {
                foreach (var x in f.GetComponentIterator<Flag>())
                {
                    if (x.Component.team == filter.team->team && x.Component.state == FlagState.OnBase)
                    {
                        return PRIORITY_HIGHEST;
                    }
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        public static FP EvaluateGoToATM(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory) == false) return PRIORITY_NOT_APPLICABLE;

            // nearby ATM?
            var comps = f.Filter<PaydayATM, Transform2D>();
            EntityRef toCapture = default;
            FP dist = FP.UseableMax;

            while (comps.NextUnsafe(out var entity, out var atm, out var transform))
            {
                if (atm->isActive == false && atm->activating == false) continue;

                FP d = FPVector2.Distance(filter.transform->Position, transform->Position);
                if (d < dist)
                {
                    dist = d;
                    toCapture = entity;
                }
                if (toCapture != default)
                {
                    memory->closestModeEntity = toCapture;
                    return PRIORITY_HIGH;
                }
            }

            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Evaluates whether a bot needs to capture a hill or not
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static FP EvaluateHillCapture(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory) == false) return PRIORITY_NOT_APPLICABLE;

            // nearby hill?
            var comps = f.Filter<Hill, Transform2D>();
            EntityRef toCapture = default;
            FP dist = FP.UseableMax;

            while (comps.NextUnsafe(out var entity, out var hill, out var transform))
            {
                if (hill->isActive == false) continue;
                if (hill->GetOwningTeam(f) == filter.team->team) continue;


                FP d = FPVector2.Distance(filter.transform->Position, transform->Position);
                if (d < dist)
                {
                    dist = d;
                    toCapture = entity;
                }
            }

            // priority based on how convinient it is for us to cap the hill
            if (toCapture != default)
            {
                memory->closestModeEntity = toCapture;
                if (dist < BotHelper.ONSCREEN_DIST / 2) return PRIORITY_HIGHEST;
                else if (dist < BotHelper.ONSCREEN_DIST) return PRIORITY_HIGH;
                else return PRIORITY_STANDARD;
            }

            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Evalulates whether we should go for a collectible or not.
        /// This handles:
        /// 1: Power ups
        /// 2: Health pickups
        /// 3: Flags
        /// 4: delivering flags
        /// 5: Payday gold
        /// </summary>
        /// <returns></returns>
        public static FP EvaluateCollectible(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory) == false) return PRIORITY_NOT_APPLICABLE;

            // nearby collectible?
            var comps = f.Filter<BRCollectible, Transform2D>();
            EntityRef toCollect = default;
            FP dist = FP.UseableMax;
            FP prio = PRIORITY_NOT_APPLICABLE;

            while (comps.NextUnsafe(out var entity, out var collectible, out var transform))
            {
                if (collectible->collected) continue;
                if (BattleRoyalSystem.IsPositionInsideDeathZone(f, transform->Position, FP._2)) continue;
                // check distance between each transform, if less that dist, set toCollect to entity and dist to distance
                FP newDist = FPVector2.Distance(filter.transform->Position, transform->Position);
                if (newDist < dist)
                {
                    toCollect = entity;
                    dist = newDist;
                }
            }
            if (dist < FP._5)
            {
                prio = PRIORITY_HIGHEST;
            }
            else if (dist < FP._10)
            {
                prio = PRIORITY_HIGH;
            }
            else if (dist < BotHelper.ONSCREEN_DIST)
            {
                prio = PRIORITY_STANDARD;
            }
            else if (dist < BotHelper.ONSCREEN_DIST + FP._5)
            {
                prio = PRIORITY_LOW;
            }
            if (prio > PRIORITY_NOT_APPLICABLE) memory->collectible = toCollect;


            // Health pickup?
            if (filter.health->CurrentPercentage < FP._0_75 + FP._0_10 + FP._0_05)
            {
                var comps2 = f.Filter<Collectible, Transform2D>();
                toCollect = default;
                dist = FP.UseableMax;
                while (comps2.NextUnsafe(out var entity, out var collectible, out var transform))
                {
                    if (collectible->collected) continue;
                    if (collectible->id != CollectibleId.HealthPack) continue;
                    if (BattleRoyalSystem.IsPositionInsideDeathZone(f, transform->Position, FP._2)) continue;

                    // check distance between each transform, if less that dist, set toCollect to entity and dist to distance
                    FP newDist = FPVector2.Distance(filter.transform->Position, transform->Position);
                    if (newDist < dist)
                    {
                        toCollect = entity;
                        dist = newDist;
                    }
                }
                FP hprio = PRIORITY_NOT_APPLICABLE;
                if (dist < BotHelper.ONSCREEN_DIST)
                {
                    if (filter.health->CurrentPercentage > FP._0_75) hprio = PRIORITY_LOW;
                    else if (filter.health->CurrentPercentage > FP._0_50) hprio = PRIORITY_STANDARD;
                    else if (filter.health->CurrentPercentage > FP._0_25) hprio = PRIORITY_HIGH;
                    else hprio = PRIORITY_HIGHEST;
                }

                if (hprio > prio && toCollect != default)
                {
                    prio = hprio;
                    memory->collectible = toCollect;
                }
            }

            // pickup flag
            if (f.RuntimeConfig.gameMode.HasFlag(GameMode.CTF))
            {
                var comps2 = f.Filter<Flag, Team, Transform2D>();
                toCollect = default;
                dist = FP.UseableMax;
                while (comps2.NextUnsafe(out var entity, out var flag, out var team, out var transform))
                {
                    bool canPickup = false;
                    if (team->team != filter.team->team && (flag->state == FlagState.OnBase || flag->state == FlagState.Dropped))
                    {
                        canPickup = true;
                    }
                    else if (team->team == filter.team->team && (flag->state == FlagState.Dropped))
                    {
                        canPickup = true;
                    }
                    if (canPickup)
                    {
                        // check distance between each transform, if less that dist, set toCollect to entity and dist to distance
                        FP newDist = FPVector2.Distance(filter.transform->Position, transform->Position);
                        if (newDist < dist)
                        {
                            toCollect = entity;
                            dist = newDist;
                        }
                    }
                }

                FP fprio = PRIORITY_NOT_APPLICABLE;
                if (toCollect != default)
                {
                    if (dist < BotHelper.ONSCREEN_DIST / 4) fprio = PRIORITY_MUST;
                    else if (dist < BotHelper.ONSCREEN_DIST / 2) fprio = PRIORITY_HIGHEST;
                    else if (dist < BotHelper.ONSCREEN_DIST) fprio = PRIORITY_HIGH;
                    else fprio = PRIORITY_STANDARD;
                }

                if (fprio > prio && toCollect != default)
                {
                    prio = fprio;
                    memory->collectible = toCollect;
                }
            }

            return prio;
        }

        /// <summary>
        /// Determine minion defeat enemy priority
        /// </summary>
        public static FP EvaluateDefeatEnemy_Minion(Frame f, ref BRBotSystem.Filter filter)
        {
            EntityRef ourEntity = filter.entity;
            // use our owner's location if we have one
            if (f.TryGet<OwnedByEntity>(ourEntity, out var obe))
            {
                ourEntity = obe.ownerEntity;

            }

            if (f.Unsafe.TryGetPointer<BRTargets_Minion>(filter.entity, out var targets))
            {
                EntityRef enemy = default;

                // we already have an enemy?
                if (targets->enemy != default)
                {
                    if (f.Unsafe.TryGetPointer<Health>(targets->enemy, out var enemyHealth))
                    {
                        if (enemyHealth->currentValue <= 0)
                        {
                            targets->enemy = default;
                        }
                        // are they alive AND in range? continue targetting them
                        else if (QTools.Distance(f, ourEntity, targets->enemy) <= BotHelper.ONSCREEN_DIST)
                        {
                            return PRIORITY_STANDARD;
                        }
                    }
                }
                if (ourEntity != default && f.Unsafe.TryGetPointer<CharacterController>(ourEntity, out var controller) && controller->abilityTarget != default)
                {
                    enemy = controller->abilityTarget;
                }
                else
                {
                    // find a new enemy
                    enemy = AIHelper.GetClosestHostile(f, ourEntity, false, filter.transform, default, default, BotHelper.ONSCREEN_DIST);
                }
                if (enemy != default)
                {
                    targets->enemy = enemy;
                    return PRIORITY_STANDARD;
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Determine minion defeat enemy priority
        /// </summary>
        /// <returns>Priority</returns>
        public static FP EvaluateDefeatEnemy_Survival(Frame f, ref BRBotSystem.Filter filter)
        {
            EntityRef ourEntity = filter.entity;
            // use our owner's location if we have one
            if (f.TryGet<OwnedByEntity>(ourEntity, out var obe))
            {
                ourEntity = obe.ownerEntity;
            }

            if (f.Unsafe.TryGetPointer<BRTargets_Minion>(filter.entity, out var targets))
            {
                // we already have an enemy?
                if (targets->enemy != default)
                {
                    if (f.Unsafe.TryGetPointer<Health>(targets->enemy, out var enemyHealth))
                    {
                        if (enemyHealth->currentValue <= 0)
                        {
                            targets->enemy = default;
                        }
                        // are they alive AND in range? continue targetting them
                        var dist = QTools.Distance(f, ourEntity, targets->enemy);
                        if (dist < 6) return PRIORITY_HIGHEST;
                        else if (dist < 10)
                        {
                            return PRIORITY_HIGH;
                        }
                        else if (dist <= BotHelper.ONSCREEN_DIST)
                        {
                            return PRIORITY_STANDARD;
                        }
                    }
                }

                // find a new enemy
                var enemy = AIHelper.GetClosestHostile(f, ourEntity, false, filter.transform, default, default, BotHelper.ONSCREEN_DIST);
                if (enemy != default)
                {
                    targets->enemy = enemy;
                    var dist = QTools.Distance(f, ourEntity, targets->enemy);
                    if (dist < 6) return PRIORITY_HIGHEST;
                    else if (dist < 10)
                    {
                        return PRIORITY_HIGH;
                    }
                    else if (dist <= BotHelper.ONSCREEN_DIST)
                    {
                        return PRIORITY_STANDARD;
                    }
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Considers whether it's time to give up chasing an enemy
        /// </summary>
        public static void EvaluateDroppingEnemy(Frame f, ref BRBotSystem.Filter filter, BRMemory* memory)
        {
            if (memory->enemy == default || f.Unsafe.TryGetPointer<Health>(memory->enemy, out var enemyHealth) == false) return;

            EntityRef lastTarget = memory->enemy;

            // target is dead?
            if (enemyHealth->currentValue <= 0)
            {
                memory->enemy = default;
                return;
            }
            // no other enemies to drop for
            if (GameModeHelper.GetPlayerLimit(f) <= 2 || (f.Global->time - filter.bot->lastTimeGoalInitialized) < FP._6)
            {
                return;
            }
            // we haven't managed to hit the target for a while?
            if (enemyHealth->WasAttackedBy(f, filter.entity, FP._8) == false)
            {
                // are they far away? or stealth?
                if (QTools.Distance(f, filter.entity, lastTarget) > BotHelper.ONSCREEN_DIST)
                {
                    memory->enemy = default;
                    return;
                }
            }
        }
        public static FP EvaluateDefeatEnemy_ChampionSurvival(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var targets) == false)
            {
                targets->enemy = default;
                return PRIORITY_NOT_APPLICABLE;
            }

            // do we already have a target
            if (targets->enemy != default && f.Unsafe.TryGetPointer<Health>(targets->enemy, out var enemyHealth))
            {
                if (enemyHealth->currentValue > 0) return PRIORITY_HIGH;
                else targets->enemy = default;
            }

            var comps = f.Filter<Team, Health, Transform2D>();

            while (comps.NextUnsafe(out var entity, out var team, out var health, out var transform))
            {
                if (health->currentValue <= 0 || f.Has<SurvivalNexus>(entity)) continue;

                if (team->team != filter.team->team)
                {
                    targets->enemy = entity;
                    return PRIORITY_HIGH;
                }
            }
            return PRIORITY_NOT_APPLICABLE;
        }

        /// <summary>
        /// Determine champion defeat enemy priority
        /// </summary>
        /// <returns>Priority</returns>
        public static FP EvaluateDefeatEnemy_Champion(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var targets) == false)
            {
                return PRIORITY_NOT_APPLICABLE;
            }

            EvaluateDroppingEnemy(f, ref filter, targets);

            EntityRef bestTarget = default;
            EntityRef lastTarget = targets->enemy;

            FP targetDist = FP.UseableMax;
            bool requiresWithinRange = filter.team->team != ProfileHelper.NPC_TEAM || f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival) == false;
            for (int i = 0; i < f.Global->pvpMatchInfo.players.Length; i++)
            {
                if (f.Global->pvpMatchInfo.players[i].entity == default) continue;
                var p = f.Global->pvpMatchInfo.players[i];
                // it's us
                if (p.entity == filter.entity) continue;
                // it's an ally
                if (p.team == filter.team->team) continue;
                // dead
                if (f.TryGet<Health>(p.entity, out var ph) && ph.currentValue <= 0) continue;
                // out of range
                FP d = QTools.Distance(f, filter.entity, p.entity);
                // stealth?
                if(BotHelper.IsEntityStealthToUs(f, filter.entity, p.entity))
                {
                    continue;
                }

                // can only target onscreen players
                if (d > BotHelper.ONSCREEN_DIST && requiresWithinRange) continue;

                if (d < targetDist)
                {
                    targetDist = d;
                    bestTarget = p.entity;
                }
            }

            targets->closestEnemy = targetDist;

            if (f.RuntimeConfig.gameMode.HasFlag(GameMode.CTF))
            {
                if (bestTarget != default && f.Has<CarriedFlag>(bestTarget))
                {
                    // we have a flag carrier, target them
                    targets->enemy = bestTarget;
                    if (targetDist < BotHelper.ONSCREEN_DIST / 2) return PRIORITY_MUST;
                    return PRIORITY_HIGHEST;
                }
            }

            // is our last target better than our current target?
            if (lastTarget != default)
            {
                if (bestTarget != default)
                {
                    // last target is favored very slightly (only if they are similarly valued)
                    bestTarget = CompareTargets(f, ref filter, lastTarget, bestTarget);
                }
                else bestTarget = lastTarget;
            }

            targets->enemy = bestTarget;
            targetDist = bestTarget != default ? QTools.Distance(f, filter.entity, bestTarget) : FP.UseableMax;

            // if player is very close don't even consider spawns, high priority targetting
            if (targetDist < FP._5)
            {
                if (filter.bot->goal == BotGoal.RunawayFromEnemy) return PRIORITY_STANDARD;
                return PRIORITY_HIGHEST;
            }

            // who were we hit by last
            var list = filter.health->GetHistory(f);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == default) continue;
                if (f.Global->time - list[i].timestamp > FP._5) continue;
                if (f.TryGet<OwnedByEntity>(list[i].owner, out var obe) == false) continue;
                if (f.TryGet<Team>(list[i].owner, out var pt) && pt.team == filter.team->team) continue;
                FP d = QTools.Distance(f, filter.entity, list[i].owner);
                if (d > BotHelper.ONSCREEN_DIST) continue;
                if (d > targetDist) continue;

                // is it dead?
                if (f.TryGet<Health>(list[i].owner, out var ph) == false || ph.currentValue <= 0) continue;
                // is it a disabled turret?
                if (f.TryGet<Turret>(list[i].owner, out var pturret) && pturret.isDisabled) continue;
                // stealth?
                if (BotHelper.IsEntityStealthToUs(f, filter.entity, list[i].owner))
                {
                    continue;
                }

                targetDist = d;
                bestTarget = list[i].owner;
                targets->enemy = bestTarget;
            }



            //if (bestTarget == default) return PRIORITY_NOT_APPLICABLE;

            // prority based on distance from this point
            FP priority = PRIORITY_NOT_APPLICABLE;
            if (bestTarget != default)
            {
                if (targetDist < BotHelper.ONSCREEN_DIST / 2) priority = PRIORITY_HIGHEST;
                else if (targetDist < BotHelper.ONSCREEN_DIST) priority = PRIORITY_HIGH;
                else priority = PRIORITY_STANDARD;
            }

            if (priority < PRIORITY_HIGH)
            {
                // --------- Break things? Like a power up chest
                var comps = f.Filter<SpawnsEntity, Health, Transform2D>();
                FP dist = FP.UseableMax;
                bestTarget = default;
                FP prio = PRIORITY_NOT_APPLICABLE;

                while (comps.NextUnsafe(out var entity, out var spawner, out var health, out var transform))
                {
                    if (spawner->spawnerType != SpawnerType.BRLoot) continue;
                    if (health->currentValue <= 0) continue;
                    FP newDist = FPVector2.Distance(filter.transform->Position, transform->Position);
                    if (newDist >= BotHelper.ONSCREEN_DIST) continue;

                    if (newDist < dist)
                    {
                        bestTarget = entity;
                        dist = newDist;

                        if (dist < BotHelper.ONSCREEN_DIST / 3) prio = PRIORITY_HIGHEST;
                        else if (dist < BotHelper.ONSCREEN_DIST / 2) prio = PRIORITY_HIGH;
                        else prio = PRIORITY_STANDARD;
                    }
                }

                if (prio > priority && bestTarget != default && dist < BotHelper.ONSCREEN_DIST)
                {
                    targets->enemy = bestTarget;
                    priority = prio;
                }
            }
            if (filter.bot->goal == BotGoal.RunawayFromEnemy) return priority * FP._0_75;
            return priority;
        }

        /// <summary>
        /// Compares two targets and returns which is better to fight
        /// </summary>
        /// <param name="f"></param>
        /// <param name="e1"></param>
        /// <param name="e2"></param>
        /// <returns></returns>
        public static EntityRef CompareTargets(Frame f, ref BRBotSystem.Filter filter, EntityRef e1, EntityRef e2)
        {
            int p1 = 0;
            int p2 = 0;

            FP d1 = QTools.Distance(f, e1, filter.entity);
            FP d2 = QTools.Distance(f, e2, filter.entity);

            int power1 = 0;
            int power2 = 0;

            for (int i = 0; i < f.Global->pvpMatchInfo.players.Length; i++)
            {
                if (f.Global->pvpMatchInfo.players[i].entity == e1)
                {
                    if (f.Unsafe.TryGetPointer<PlayerSocialInfo>(e1, out var psi))
                    {
                        power1 = psi->power;
                    }
                }
                else if (f.Global->pvpMatchInfo.players[i].entity == e2)
                {
                    if (f.Unsafe.TryGetPointer<PlayerSocialInfo>(e2, out var psi))
                    {
                        power2 = psi->power;
                    }
                }
            }

            if (power1 == 0) return e2;
            if (power2 == 0) return e1;

            if (power1 != 0 && power2 != 0)
            {
                if (power1 > power2)
                {
                    if (power1 / power2 >= 2) p2 += 2;
                    else p2 += 1;
                }
                else
                {
                    if (power2 / power1 >= 2) p1 += 2;
                    else p1 += 1;
                }
            }

            // if one is on the screen and the other isn't, return the visible one
            if (d1 > BotHelper.ONSCREEN_DIST && d2 <= BotHelper.ONSCREEN_DIST) return e2;
            else if (d2 > BotHelper.ONSCREEN_DIST && d1 <= BotHelper.ONSCREEN_DIST) return e1;

            // Point for distance
            if (d1 < d2) p1 += 1;
            else p2 += 1;

            // 2 points for health, 1 for max health and 1 for current
            if (f.TryGet<Health>(e1, out var h1) && f.TryGet<Health>(e2, out var h2))
            {
                // if one is dead ignore it
                if (h1.currentValue <= 0 && h2.currentValue > 0) return e2;
                else if (h2.currentValue <= 0 && h1.currentValue > 0) return e1;

                if (h1.maximumHealth > h2.maximumHealth)
                {
                    p2 += 1;
                }
                else
                {
                    p1 += 1;
                }

                if (h1.currentValue > h2.currentValue)
                {
                    p2 += 1;
                }
                else
                {
                    p1 += 1;
                }
            }

            // who hit us last?
            FP lastTimeHitBy1 = default;
            FP lastTimeHitBy2 = default;
            var list = filter.health->GetHistory(f);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == e1)
                {
                    lastTimeHitBy1 = f.Global->time - list[i].timestamp;
                }
                else if (list[i].owner == e2)
                {
                    lastTimeHitBy2 = f.Global->time - list[i].timestamp;
                }
            }

            if (lastTimeHitBy1 != default && (lastTimeHitBy1 < lastTimeHitBy2 || lastTimeHitBy2 == default))
            {
                p1 += 1;
            }
            else if (lastTimeHitBy2 != default && (lastTimeHitBy2 < lastTimeHitBy1 || lastTimeHitBy1 == default))
            {
                p2 += 1;
            }

            if (p1 >= p2) return e1;
            return e2;
        }
    }
}
