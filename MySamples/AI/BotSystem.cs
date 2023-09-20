namespace MySamples
{
    public unsafe class BotSystem : SystemMainThreadFilter<BRBotSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef entity;
            public Health* health;
            public CharacterController* controller;
            public Transform2D* transform;
            public Team* team;
            public NavMeshPathfinder* navigator;
            public BRBot* bot;
        }
        public override void Update(Frame f, ref Filter filter)
        {
            if (f.IsVerified == false) return;
            // no need to update if we are dead
            if (filter.health->currentValue <= 0) return;
            if (GameModeHelper.InWaitingMode(f)) return;

            // --- Initialize Bot ---
            // This is called once per bot, when the bot is spawned
            if (filter.bot->initialized == false)
            {
                BRBotBrain.InitializeBot(f, ref filter);
                filter.bot->initialized = true;
            }

            int updateRate = BRBotBrain.GetUpdateRate(filter.bot->category) + filter.bot->updateOffset;
            if (updateRate < 1) updateRate = 1;
            int goalUpdateRate = BRBotBrain.GetGoalUpdateRate(filter.bot->category) + filter.bot->updateOffset;
            if (goalUpdateRate < 1) goalUpdateRate = 1;

            // --- Update Bot Goal ---
            if (f.Number % goalUpdateRate == 0 || filter.bot->updateGoal)
            {
                UpdateBotGoal(f, ref filter);
                filter.bot->updateGoal = false;
            }

            // --- Update Bot ---
            if (f.Number % updateRate == 0)
            {
                UpdateBot(f, ref filter);

            }


        }

        /// <summary>
        /// Find the best suitable goal we should be attempting to achieve
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        protected void UpdateBotGoal(Frame f, ref Filter filter)
        {
            BRBotBrain.FindBestGoal(f, ref filter);
        }

        /// <summary>
        /// Actual bot update code
        /// </summary>
        /// <param name="f">The current frame</param>
        /// <param name="filter">The system filter</param>
        protected void UpdateBot(Frame f, ref Filter filter)
        {
            // those can always be used regardless of goal
            if (filter.bot->category == BotCategory.Champion && f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival) == false) BRBotBrain.MonitorAuxiliarAbilityUsage(f, ref filter);

            switch (filter.bot->goal)
            {
                // None pretty much translates to AFK
                case BotGoal.None:
                    if (f.Unsafe.TryGetPointer<Playable>(filter.entity, out var playable))
                    {
                        filter.controller->StopRun(playable);
                    }
                    break;
                case BotGoal.DestroyNexus:
                    if (f.Has<BRBotPathMidPoint>(filter.bot->target))
                    {
                        if (BRBotActions.MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._1))
                        {
                            BRBotBrain.InitializeGoal(f, ref filter, BotGoal.DestroyNexus, false);
                        }
                    }
                    else
                    {
                        FP dist = QTools.Distance(f, filter.entity, filter.bot->target);
                        if (dist > FP._5 || (filter.controller->CanUseAbility() == false && filter.controller->CanAttack() == false) || BRBotActions.AttackEnemy(f, ref filter) == false)
                        {
                            BRBotActions.MoveToPosition(f, ref filter, filter.bot->destination.XOY, FPMath.Min(FP._4, filter.controller->attackRange));
                        }
                    }
                    break;
                case BotGoal.StayInBrush:
                    BRBotActions.StopMoving(f, ref filter, false);
                    break;
                case BotGoal.AvoidDeathZone:
                    BRBotActions.AvoidDeathZone(f, ref filter);
                    break;
                case BotGoal.DefeatEnemy:
                    if ((filter.controller->CanUseAbility() == false && filter.controller->CanAttack() == false) || BRBotActions.AttackEnemy(f, ref filter) == false)
                    {
                        BRBotActions.MoveToTarget(f, ref filter);
                    }
                    break;
                case BotGoal.FollowOwner:
                    BRBotActions.MoveToTarget(f, ref filter);
                    break;
                case BotGoal.GoToATM:
                    if (BRBotActions.GoToATM(f, ref filter))
                    {
                        // re-initialize goal to pick a new destination
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.GoToATM, false);
                    }
                    break;
                case BotGoal.RunawayFromEnemy:
                    if (BRBotActions.ExploreWorld(f, ref filter))
                    {
                        // re-initialize goal to pick a new destination
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.RunawayFromEnemy, false);
                    }
                    break;
                case BotGoal.RevivePlayer:
                    BRBotActions.MoveToPosition(f, ref filter, filter.bot->destination.XOY, FP._0_50, false);
                    break;
                case BotGoal.ExploreWorld:
                    if (BRBotActions.ExploreWorld(f, ref filter))
                    {
                        // re-initialize goal to pick a new destination
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.ExploreWorld, false);
                    }
                    break;
                case BotGoal.GrabCollectible:
                    if (BRBotActions.GrabCollectible(f, ref filter))
                    {
                        // re-initialize goal in-case we setup a midpoint
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.GrabCollectible, false);
                    }
                    break;
                case BotGoal.DeliverFlag:
                    if (BRBotActions.DeliverFlag(f, ref filter))
                    {
                        // re-initialize goal in-case we setup a midpoint
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.DeliverFlag, false);
                    }
                    break;
                case BotGoal.StayInHill:
                    if (BRBotActions.MoveToHill(f, ref filter))
                    {
                        // re-initialize goal in-case we setup a midpoint
                        BRBotBrain.InitializeGoal(f, ref filter, BotGoal.StayInHill, false);
                    }
                    break;
            }
        }
    }
}