namespace MySamples
{
    internal unsafe class BotActions
    {
        public static bool ExploreWorld(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._0_75, true);
        }
        public static bool GoToATM(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._2, true);
        }
        public static bool AvoidDeathZone(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._1_50);
        }
        public static bool GrabCollectible(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._0_25, true);
        }
        public static bool DeliverFlag(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._0_25, true);
        }

        public static bool MoveToHill(Frame f, ref BRBotSystem.Filter filter)
        {
            return MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._5, true);
        }

        /// <summary>
        /// A target-aware movement
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        public static bool MoveToTarget(Frame f, ref BRBotSystem.Filter filter)
        {
            if (filter.bot->target != default && f.Unsafe.TryGetPointer<Transform2D>(filter.bot->target, out var targetTransform))
            {
                var pos = BotHelper.BestGuessPositionOnTarget(f, filter.entity, filter.transform, filter.bot->target, null, null, default);
                FP stopDist = filter.controller->attackRange;
                if (BotHelper.ViewToTargetIsObstructed(f, filter.controller, targetTransform) || BotHelper.IsEntityStealthToUs(f, filter.entity, filter.bot->target))
                {
                    stopDist = FP._0_33;
                }
                return MoveToPosition(f, ref filter, pos.XOY, stopDist, false);
            }
            return false;
        }

        /// <summary>
        /// Generic move to a point funciton
        /// </summary>
        /// <param name="allowMiddlePoints">Allows inserting curved middle points to give more randomness to pathing. Points are only added between long stretches</param>
        /// <returns></returns>
        public static bool MoveToPosition(Frame f, ref BRBotSystem.Filter filter, FPVector3 position, FP stopDist, bool allowMiddlePoints = false)
        {
            if (f.Unsafe.TryGetPointer<Playable>(filter.entity, out var playable))
            {
                filter.bot->currentStopDist = stopDist;
                filter.bot->destination = position.XZ;
                bool destinationReached = FPVector2.Distance(position.XZ, filter.transform->Position) <= stopDist;

                // no need to move
                if (destinationReached)
                {
                    //Log.Info($"{f.Number}: Stop navingation on {filter.entity}");
                    filter.navigator->Stop(f, filter.entity);
                    filter.controller->StopRun(playable);
                    return true;
                }

                // move towards target
                if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
                {
                    filter.navigator->SetTarget(f, position, f.Map.NavMeshes["navmesh"]);
                    filter.bot->allowsMiddlePoint = allowMiddlePoints;
                    // force the agent to perform a repath. This is usually set to true when the bot is stuck or pushed away from destination
                    if (filter.bot->forceRepath)
                    {
                        filter.navigator->ForceRepath(f);
                        filter.bot->forceRepath = false;
                        filter.bot->middlePoint = default;
                    }
                }
            }
            return false;
        }
        public static void StopMoving(Frame f, ref BRBotSystem.Filter filter, bool stopNavigatorToo = true)
        {
            if (stopNavigatorToo)
            {
                filter.navigator->Stop(f, filter.entity);
            }
            if (f.Unsafe.TryGetPointer<Playable>(filter.entity, out var playable))
            {
                filter.controller->StopRun(playable);
            }
        }
        /// <summary>
        /// Try to attack enemy
        /// </summary>
        /// <returns>Whether we successfully ATTEMPTED to attack an enemy</returns>
        public static bool AttackEnemy(Frame f, ref BRBotSystem.Filter filter)
        {
            switch (filter.bot->category)
            {
                case BotCategory.SurvivalEnemy:
                case BotCategory.Minion:
                    if (f.TryGet<BRTargets_Minion>(filter.entity, out var targets) && targets.enemy != default)
                    {
                        if (BotHelper.ActivateBestAbilityOption(f, filter.entity, targets.enemy, ReasonForUse.KillTarget, true))
                        {
                            return true;
                        }
                    }
                    break;
                case BotCategory.Champion:
                    if (f.TryGet<BRMemory>(filter.entity, out var ctargets) && ctargets.enemy != default)
                    {
                        if (BotHelper.ActivateBestAbilityOption(f, filter.entity, ctargets.enemy, ReasonForUse.KillTarget, true))
                        {
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }
        /// <summary>
        /// Triggers a chance for a bot to emote (10%)
        /// </summary>
        /// <param name="f"></param>
        /// <param name="entity"></param>
        /// <param name="reason"></param>
        public static void Emote(Frame f, EntityRef entity, EmoteOccasion reason)
        {
            // 10% chance to emote
            if (f.RNG->Next() > FP._0_10) return;
            f.Events.BotShouldUseEmoteEvent(entity, reason);
        }
    }
}
