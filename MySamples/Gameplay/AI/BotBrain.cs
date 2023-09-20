//#define DEBUG_GOALS

namespace MySamples
{
    internal unsafe class BotBrain
    {
        /// <summary>
        /// Determins how frequently a bot should be updated given their cateogry
        /// </summary>
        /// <param name="category">The category of the bot which determines their complexity</param>
        /// <returns>The update rate targeted for this bot</returns>
        public static byte GetUpdateRate(BotCategory category)
        {
            switch (category)
            {
                default:
                case BotCategory.Minion:
                    return 10;
                case BotCategory.Champion:
                    return 7;
                case BotCategory.SurvivalEnemy:
                    return 10;
            }
        }

        /// <summary>
        /// Determins how frequently a bot should look for a new goal given their cateogry
        /// </summary>
        /// <param name="category">The category of the bot which determines their complexity</param>
        /// <returns>The update rate targeted for this bot</returns>
        public static byte GetGoalUpdateRate(BotCategory category)
        {
            switch (category)
            {
                default:
                case BotCategory.SurvivalEnemy:
                    return 20;
                case BotCategory.Minion:
                    return 30;
                case BotCategory.Champion:
                    return 12;
            }
        }

        /// <summary>
        /// Initializes a bot when it is spawned
        /// </summary>
        /// <param name="f">The current frame</param>
        /// <param name="filter">The BRBot system filter containing the components needed</param>
        public static void InitializeBot(Frame f, ref BRBotSystem.Filter filter)
        {
            filter.bot->updateOffset = (sbyte)(f.Global->botUpdateFrame - 5);
            f.Global->botUpdateFrame++;

            if (f.Global->botUpdateFrame >= 10) f.Global->botUpdateFrame = 0;
            switch (filter.bot->category)
            {
                case BotCategory.SurvivalEnemy:
                    if (f.Has<BRTargets_Minion>(filter.entity) == false)
                    {
                        f.Add<BRTargets_Minion>(filter.entity);
                    }
                    filter.bot->goals = BotGoal.DestroyNexus | BotGoal.DefeatEnemy;
                    break;
                case BotCategory.Minion:
                    if (f.Has<BRTargets_Minion>(filter.entity) == false)
                    {
                        f.Add<BRTargets_Minion>(filter.entity);
                    }
                    filter.bot->goals = BotGoal.FollowOwner | BotGoal.DefeatEnemy;
                    break;
                case BotCategory.Champion:
                    filter.bot->isPussy = f.RNG->Next() < FP._0_50;

                    if (f.AddOrGet<BRMemory>(filter.entity, out var memory))
                    {
                        for (int i = 0; i < memory->emoteSelection.Length; i++)
                        {
                            memory->emoteSelection[i].option = (short)f.RNG->Next(0, 10);
                        }
                    }
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival))
                    {
                        if (filter.team->team == ProfileHelper.NPC_TEAM)
                        {
                            filter.bot->goals = BotGoal.DefeatEnemy | BotGoal.ExploreWorld;
                        }
                        else
                        {
                            filter.bot->goals = BotGoal.DefeatEnemy | BotGoal.ExploreWorld | BotGoal.RunawayFromEnemy | BotGoal.GrabCollectible | BotGoal.RevivePlayer;
                        }
                        break;
                    }
                    //else if(f.RuntimeConfig.gameMode.HasFlag(GameMode.Tutorial))
                    //{
                    //    filter.bot->goals = BotGoal.DefeatEnemy |  BotGoal.RunawayFromEnemy;
                    //    break;
                    //}
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Tutorial))
                    {
                        filter.bot->goals = BotGoal.DefeatEnemy | BotGoal.ExploreWorld | BotGoal.GrabCollectible | BotGoal.StayInBrush;
                    }
                    else
                    {
                        filter.bot->goals = BotGoal.DefeatEnemy | BotGoal.ExploreWorld | BotGoal.GrabCollectible | BotGoal.StayInBrush | BotGoal.RunawayFromEnemy;
                    }
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.CTF))
                    {
                        filter.bot->goals |= BotGoal.DeliverFlag;
                    }
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Hill))
                    {
                        filter.bot->goals |= BotGoal.StayInHill;
                    }
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Payday))
                    {
                        filter.bot->goals |= BotGoal.GoToATM;
                    }
                    if (f.RuntimeConfig.gameMode.HasFlag(GameMode.RoyaleDuo))
                    {
                        filter.bot->goals |= BotGoal.RevivePlayer;
                    }
                    if (GameModeHelper.UsesDeathZone(f))
                    {
                        filter.bot->goals |= BotGoal.AvoidDeathZone;
                    }
                    break;
            }
            // setup their arsenal -- This allows us to know right away if we should even attempt to use
            // a type of ability
            if (f.Unsafe.TryGetPointer<AbilityInventory>(filter.entity, out var inventory))
            {
                var abilities = inventory->GetAbilities(f);
                for (int i = 0; i < abilities.Count; i++)
                {
                    if (abilities[i] == default) continue;
                    var data = f.FindAsset<AbilityData>(abilities[i].Id);
                    if (data != null)
                    {
                        if (data.botUsage.HasFlag(AbilityBotUsage.RunFromTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.RunawayFromTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.RunawayFromTarget;
                            }
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.RunawayFromTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.RunawayFromTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.DealsDamage))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.KillTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.KillTarget;
                            }
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.KillTargetWithoutLookingOrStopping) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.KillTargetWithoutLookingOrStopping;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.CCTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.CCTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.CCTarget;
                            }
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.KillTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.KillTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.BreakCC))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.BreakCC) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.BreakCC;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.FinishTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.KillTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.KillTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.AssistsAllyInCombat))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.AssistAllyInCombat) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.AssistAllyInCombat;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.HealsAlly))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.HeallAlly) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.HeallAlly;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.AvoidAttack))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.AvoidAttack) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.AvoidAttack;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.ChaseTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.ChaseTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.ChaseTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.InterruptAbility))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.InterruptEnemy) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.InterruptEnemy;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.PassThroughTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.PassThroughTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.PassThroughTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.RecoverHealth))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.RecoverHealth) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.RecoverHealth;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.ReviveDeadAlly))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.ReviveDeadAlly) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.ReviveDeadAlly;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.RunFromTarget))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.RunawayFromTarget) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.RunawayFromTarget;
                            }
                        }
                        if (data.botUsage.HasFlag(AbilityBotUsage.UseOffCooldown))
                        {
                            if (filter.bot->arsenal.HasFlag(ReasonForUse.OffCooldown) == false)
                            {
                                filter.bot->arsenal |= ReasonForUse.OffCooldown;
                            }
                        }
                    }
                }
            }

            FindBestGoal(f, ref filter);
        }



        /// <summary>
        /// Select the goal that is best suited for this bot
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        public static void FindBestGoal(Frame f, ref BRBotSystem.Filter filter)
        {
            BotGoal goal = BotGoal.None;
            BotGoal lastGoal = filter.bot->goal;
            FP prio = FP._0;
#if DEBUG_GOALS
            Log.Info(f.Number+"|"+ filter.entity + " -------------------------------");
#endif
            if (filter.bot->goals.HasFlag(BotGoal.AvoidDeathZone))
            {
                FP p = BRBotGoals.EvaluateRunFromDeathZone(f, ref filter);
#if DEBUG_GOALS
                if(p != default) Log.Info("AvoidDeathZone: " + p);
#endif
                if (p > prio)
                {
#if DEBUG_GOALS
                    Log.Info("Selecting: AvoidDeathZone");
#endif
                    goal = BotGoal.AvoidDeathZone;
                    prio = p;
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.FollowOwner))
            {
                FP p = BRBotGoals.EvaluateFollowOwner(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info("FollowOwner: " + p);
#endif
                if (p > prio)
                {
#if DEBUG_GOALS
                    Log.Info("Selecting: FollowOwner");
#endif
                    goal = BotGoal.FollowOwner;
                    prio = p;
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.StayInBrush))
            {
                FP p = BRBotGoals.EvaluateStayInBrush(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"EvaluateStayInBrush: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.StayInBrush;
                    prio = p;
#if DEBUG_GOALS
                    Log.Info("Staying in brush");
#endif
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.GrabCollectible))
            {
                FP p = BRBotGoals.EvaluateCollectible(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"GrabCollectible: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.GrabCollectible;
                    prio = p;
#if DEBUG_GOALS
                    Log.Info("Selecting: Grab collectible");
#endif
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.RevivePlayer))
            {
                FP p = BRBotGoals.EvaluateRevivePlayerSurvival(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"RevivePlayer: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.RevivePlayer;
                    prio = p;
#if DEBUG_GOALS
                    Log.Info("Selecting: RevivePlayer");
#endif
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.DefeatEnemy))
            {
                FP p = BRBotGoals.EvaluateDefeatEnemy(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info("DefeatEnemy: " + p);
#endif
                if (p > prio)
                {
#if DEBUG_GOALS
                    Log.Info("Selecting: DefeatEnemy. It is higher than (" + prio + ") which is (" + goal + ")");
#endif
                    goal = BotGoal.DefeatEnemy;
                    prio = p;
                }
            }

            if (filter.bot->goals.HasFlag(BotGoal.DestroyNexus))
            {
                FP p = BRBotGoals.EvaluateDestroyNexus(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info("DestroyNexus: " + p);
#endif
                if (p > prio)
                {
#if DEBUG_GOALS
                    Log.Info("Selecting: DestroyNexus. It is higher than (" + prio + ") which is (" + goal + ")");
#endif
                    goal = BotGoal.DestroyNexus;
                    prio = p;
                }
            }

            // ------------------------- Game mode specific order here
            // CTF
            if (filter.bot->goals.HasFlag(BotGoal.DeliverFlag))
            {
                FP p = BRBotGoals.EvaluateDeliverFlag(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"DeliverFlag: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.DeliverFlag;
#if DEBUG_GOALS
                    Log.Info("Selecting: DeliverFlag");
#endif
                    prio = p;
                }
            }

            // Hill
            if (filter.bot->goals.HasFlag(BotGoal.StayInHill))
            {
                FP p = BRBotGoals.EvaluateHillCapture(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"StayInHill: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.StayInHill;
#if DEBUG_GOALS
                    Log.Info("Selecting: StayInHill");
#endif
                    prio = p;
                }
            }

            // Payday
            if (filter.bot->goals.HasFlag(BotGoal.GoToATM))
            {
                FP p = BRBotGoals.EvaluateGoToATM(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"GoToATM: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.GoToATM;
#if DEBUG_GOALS
                    Log.Info("Selecting: GoToATM");
#endif
                    prio = p;
                }
            }
            // -------------------------------------------------------

            if (filter.bot->goals.HasFlag(BotGoal.RunawayFromEnemy))
            {
                FP p = BRBotGoals.EvaluateRunaway(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info($"RunawayFromEnemy: {p}");
#endif
                if (p > prio)
                {
                    goal = BotGoal.RunawayFromEnemy;
                    prio = p;
#if DEBUG_GOALS
                    Log.Info("Selecting: RunawayFromEnemy");
#endif
                }
            }


            if (filter.bot->goals.HasFlag(BotGoal.ExploreWorld))
            {
                FP p = BRBotGoals.EvaluateExploreWorld(f, ref filter);
#if DEBUG_GOALS
                if (p != default) Log.Info("ExploreWorld: " + p);
#endif
                if (p > prio)
                {
#if DEBUG_GOALS
                    Log.Info("Selecting: ExploreWorld");
#endif
                    goal = BotGoal.ExploreWorld;
                    prio = p;
                }
            }
#if DEBUG_GOALS
            if(lastGoal != goal)
            {
                Log.Warn("---------------------- NEW GOAL:" + goal + " ----------------------");
            }
#endif
            // process initial setup for the goal
            InitializeGoal(f, ref filter, goal, lastGoal == goal);

            filter.bot->goal = goal;
            filter.bot->goalPriority = prio;
        }

        /// <summary>
        /// Initial setup before goal is useable
        /// </summary>
        public static void InitializeGoal(Frame f, ref BRBotSystem.Filter filter, BotGoal goal, bool wasAlsoTheLastGoal)
        {
            // reset
            if (wasAlsoTheLastGoal == false)
            {
                filter.bot->middlePoint = default;
            }

            switch (goal)
            {
                case BotGoal.RevivePlayer:
                    Init_RevivePlayer(f, ref filter, wasAlsoTheLastGoal);
                    break;
                case BotGoal.StayInBrush:
                    BRBotActions.StopMoving(f, ref filter);
                    break;
                case BotGoal.GoToATM:
                    if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._5)
                    {
                        if (f.TryGet<BRMemory>(filter.entity, out var memory) && memory.closestModeEntity != default && f.Unsafe.TryGetPointer<Transform2D>(memory.closestModeEntity, out var t))
                        {
                            filter.bot->target = memory.closestModeEntity;
                            filter.bot->lastTimeGoalInitialized = f.Global->time;
                            filter.bot->destination = t->Position + (new FPVector2(f.RNG->Next(-FP._1, FP._1), f.RNG->Next(-FP._1, FP._1)) * FP._10);

                            FP distance = FPVector2.Distance(t->Position, filter.transform->Position);
                            if (distance > BotHelper.ONSCREEN_DIST * 2)
                            {
                                filter.bot->destination = GetMidPoint(f, ref filter, t);// midPoint;
                            }
                        }
                    }
                    break;
                case BotGoal.RunawayFromEnemy:
                    Init_Runaway(f, ref filter, wasAlsoTheLastGoal);
                    break;
                case BotGoal.DestroyNexus:
                    Init_DestroyNexus(f, ref filter, wasAlsoTheLastGoal);
                    break;
                case BotGoal.DefeatEnemy:
                    switch (filter.bot->category)
                    {
                        case BotCategory.SurvivalEnemy:
                        case BotCategory.Minion:
                            if (f.TryGet<BRTargets_Minion>(filter.entity, out var targets))
                            {
                                filter.bot->target = targets.enemy;
                                filter.bot->lastTimeGoalInitialized = f.Global->time;
                            }
                            break;
                        case BotCategory.Champion:
                            if (f.TryGet<BRMemory>(filter.entity, out var ctargets))
                            {
                                filter.bot->target = ctargets.enemy;
                                filter.bot->lastTimeGoalInitialized = f.Global->time;
                            }
                            break;
                    }
                    break;
                case BotGoal.FollowOwner:
                    if (f.TryGet<OwnedByEntity>(filter.entity, out var obe))
                    {
                        filter.bot->target = obe.ownerEntity;
                        filter.bot->lastTimeGoalInitialized = f.Global->time;
                    }
                    break;
                case BotGoal.ExploreWorld:
                    Init_ExploreWorld(f, ref filter, wasAlsoTheLastGoal);
                    break;
                case BotGoal.AvoidDeathZone:
                    if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._1)
                    {
                        filter.bot->destination = default;
                        if (f.Global->brManager != default && f.Unsafe.TryGetPointer<BRManager>(filter.entity, out var manager))
                        {
                            filter.bot->destination = manager->bounds.Center;
                        }
                        AdjustDestinationToBeOnNavmesh(f, ref filter);
                        filter.bot->lastTimeGoalInitialized = f.Global->time;
                    }
                    break;
                case BotGoal.GrabCollectible:
                    {
                        Init_Collectible(f, ref filter, wasAlsoTheLastGoal);
                    }
                    break;
                case BotGoal.DeliverFlag:
                    {
                        if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._5)
                        {
                            foreach (var x in f.GetComponentIterator<Flag>())
                            {
                                if (x.Component.team == filter.team->team && f.Unsafe.TryGetPointer<Transform2D>(x.Entity, out var t))
                                {
                                    filter.bot->target = x.Entity;
                                    filter.bot->lastTimeGoalInitialized = f.Global->time;
                                    filter.bot->destination = t->Position;

                                    FP distance = FPVector2.Distance(t->Position, filter.transform->Position);
                                    if (distance > BotHelper.ONSCREEN_DIST * 2)
                                    {
                                        filter.bot->destination = GetMidPoint(f, ref filter, t);// midPoint;
                                    }

                                    break;
                                }
                            }
                        }
                    }
                    break;
                case BotGoal.StayInHill:
                    {
                        Init_HillCapture(f, ref filter, wasAlsoTheLastGoal);
                    }
                    break;
            }
        }
        public static void Init_DestroyNexus(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (wasAlsoTheLastGoal && (f.Global->time - filter.bot->lastTimeGoalInitialized) < FP._5) return;

            if (filter.bot->target != default && f.TryGet<BRBotPathMidPoint>(filter.bot->target, out var mp))
            {
                if (mp.nextPoint != default && f.Unsafe.TryGetPointer<Transform2D>(mp.nextPoint, out var mpt))
                {
                    filter.bot->target = mp.nextPoint;
                    filter.bot->lastTimeGoalInitialized = f.Global->time;
                    filter.bot->destination = mpt->Position;
                    return;
                }
            }

            var comps = f.Filter<SurvivalNexus, Health, Transform2D>();
            Transform2D* toCaptureTransform = default;
            EntityRef toCapture = default;
            FP dist = FP.UseableMax;

            bool isVeryClose = false;
            while (comps.NextUnsafe(out var entity, out var nexus, out var health, out var transform))
            {
                if (health->currentValue <= 0)
                {
                    // we need to move on from them
                    //if (entity == filter.bot->target) filter.bot->forceRepath = true;
                    continue;
                }

                FP d = FPVector2.Distance(filter.transform->Position, transform->Position);
                if (d < dist)
                {
                    dist = d;
                    toCaptureTransform = transform;
                    toCapture = entity;
                    if (d < FP._6) isVeryClose = true;
                }
            }
            if (toCapture != default)
            {
                filter.bot->target = toCapture;
                filter.bot->lastTimeGoalInitialized = f.Global->time;
                filter.bot->destination = toCaptureTransform->Position;

                // get closest mid point
                if (isVeryClose == false)
                {
                    var comps2 = f.Filter<BRBotPathMidPoint, Transform2D>();
                    BRBotPathMidPoint* mpoint = default;
                    //FPVector2 dirToTarget = filter.bot->destination - filter.transform->Position;
                    while (comps2.NextUnsafe(out var entity, out var point, out var transform))
                    {
                        if (point->nextPoint != default) continue;
                        // make sure this is not an extensions
                        if (point->previousPoint != default && f.Unsafe.TryGetPointer<BRBotPathMidPoint>(point->previousPoint, out var pp) && pp->nextPoint != entity) continue;
                        FP d = FPVector2.Distance(filter.bot->destination, transform->Position);
                        if (d > FP._5) continue;
                        mpoint = point;
                    }
                    if (mpoint != default)
                    {
                        byte limit = 20;
                        toCaptureTransform = default;
                        toCapture = default;
                        dist = FP.UseableMax;
                        while (mpoint->previousPoint != default && limit > 0)
                        {
                            // move on to the next point in the parth
                            if (f.Unsafe.TryGetPointer<BRBotPathMidPoint>(mpoint->previousPoint, out mpoint) == false) break;
                            if (mpoint->previousPoint == default) break;

                            if (f.Unsafe.TryGetPointer<Transform2D>(mpoint->previousPoint, out var transform) == false) break;
                            FP d = FPVector2.Distance(filter.transform->Position, transform->Position);
                            if (d < dist)
                            {
                                dist = d;
                                toCaptureTransform = transform;
                                toCapture = mpoint->previousPoint;
                            }
                            // we've gone too far from our ideal starting point
                            else break;
                            limit--;
                        }

                        if (toCapture != default)
                        {
                            filter.bot->target = toCapture;
                            filter.bot->lastTimeGoalInitialized = f.Global->time;
                            filter.bot->destination = toCaptureTransform->Position;

                            return;
                        }
                    }
                }
            }
        }
        public static FPVector2 GetMidPoint(Frame f, ref BRBotSystem.Filter filter, Transform2D* ct)
        {
            FPVector2 midPoint = (ct->Position + filter.transform->Position) / 2;
            //Log.Info("initial: " + midPoint);
            FPVector2 dir = (ct->Position - filter.transform->Position).Normalized;
            dir = FPVector2.Rotate(dir, FP.Rad_90);
            //Log.Info($"Dir: {dir}");
            midPoint += dir * f.RNG->Next(-BotHelper.ONSCREEN_DIST, BotHelper.ONSCREEN_DIST);
            midPoint = MovePositionToClosestNavmeshSpot(f, midPoint, 2);

            return midPoint;
        }
        public static void Init_RevivePlayer(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (wasAlsoTheLastGoal && (f.Global->time - filter.bot->lastTimeGoalInitialized) < FP._5) return;

            var comps = f.Filter<Reviver, Transform2D>();
            Transform2D* toCaptureTransform = default;
            EntityRef toCapture = default;
            FP dist = FP.UseableMax;

            while (comps.NextUnsafe(out var entity, out var hill, out var transform))
            {
                FP d = FPVector2.Distance(filter.transform->Position, transform->Position);
                if (d < dist)
                {
                    dist = d;
                    toCaptureTransform = transform;
                    toCapture = entity;
                }
            }

            if (toCapture != null)
            {
                filter.bot->target = toCapture;
                filter.bot->lastTimeGoalInitialized = f.Global->time;
                filter.bot->destination = toCaptureTransform->Position;
            }
        }
        public static void Init_HillCapture(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (wasAlsoTheLastGoal && (f.Global->time - filter.bot->lastTimeGoalInitialized) < FP._5) return;

            var comps = f.Filter<Hill, Transform2D>();
            Transform2D* toCaptureTransform = default;
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
                    toCaptureTransform = transform;
                    toCapture = entity;
                }
            }
            if (toCapture != default)
            {
                filter.bot->target = toCapture;
                filter.bot->lastTimeGoalInitialized = f.Global->time;
                filter.bot->destination = toCaptureTransform->Position;

                FP distance = FPVector2.Distance(filter.bot->destination, filter.transform->Position);
                if (distance > BotHelper.ONSCREEN_DIST)
                {
                    filter.bot->destination = GetMidPoint(f, ref filter, toCaptureTransform);// midPoint;
                }
            }
        }
        public static void Init_Collectible(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (f.TryGet<BRMemory>(filter.entity, out var memory) && memory.collectible != default && f.Unsafe.TryGetPointer<Transform2D>(memory.collectible, out var ct))
            {
                filter.bot->target = memory.collectible;
                filter.bot->lastTimeGoalInitialized = f.Global->time;
                if (f.Has<BRCollectible>(memory.collectible))
                {
                    // br collectible is offsetted by 1.9f
                    FPVector2 pos = ct->Position + (ct->Up * (FP._1 + FP._0_99 - FP._0_10));
                    FPVector2 dir = pos - ct->Position;
                    filter.bot->destination = MovePositionToClosestNavmeshSpot(f, pos, dir, true);
                }
                else if (f.Has<Flag>(memory.collectible))
                {
                    if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._5)
                    {
                        FP distance = FPVector2.Distance(ct->Position, filter.transform->Position);
                        if (distance > BotHelper.ONSCREEN_DIST * 2)
                        {
                            filter.bot->destination = GetMidPoint(f, ref filter, ct);// midPoint;
                        }
                        else filter.bot->destination = ct->Position;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    filter.bot->destination = ct->Position;
                }

                // ensure our destination is on the navmesh
                if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(filter.bot->destination.XOY, default) == false)
                    {
                        filter.bot->destination = MovePositionToClosestNavmeshSpot(f, filter.bot->destination);
                        // just try going for it
                        if (f.Map.NavMeshes["navmesh"].Contains(filter.bot->destination.XOY, default) == false)
                        {
                            filter.bot->destination = ct->Position;
                        }
                    }
                }
            }
        }
        protected static void Init_Runaway(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._5)
            {
                // nearby healthpack?
                var comps2 = f.Filter<Collectible, Transform2D>();
                EntityRef toCollect = default;
                FP dist = FP.UseableMax;
                FPVector2 toCollectPosition = default;
                while (comps2.NextUnsafe(out var entity, out var collectible, out var transform))
                {
                    if (collectible->collected) continue;
                    if (collectible->id != CollectibleId.HealthPack) continue;

                    // check distance between each transform, if less that dist, set toCollect to entity and dist to distance
                    FP newDist = FPVector2.Distance(filter.transform->Position, transform->Position);
                    if (newDist < dist)
                    {
                        toCollect = entity;
                        dist = newDist;
                        toCollectPosition = transform->Position;
                    }
                }
                if (toCollect != default)
                {
                    filter.bot->destination = MovePositionToClosestNavmeshSpot(f, toCollectPosition);
                    filter.bot->lastTimeGoalInitialized = f.Global->time;
                    return;
                }

                // fuck it, pick an exploration location
                Init_ExploreWorld(f, ref filter, wasAlsoTheLastGoal);
            }
        }
        protected static void Init_ExploreWorld(Frame f, ref BRBotSystem.Filter filter, bool wasAlsoTheLastGoal)
        {
            if (wasAlsoTheLastGoal == false || (f.Global->time - filter.bot->lastTimeGoalInitialized) > FP._5)
            {
                // hammock?
                FP acceptableDist = FP._10 * FP._4 * f.RNG->Next();
                foreach (var h in f.GetComponentIterator<Hammock>())
                {
                    var comps = f.Filter<Hammock, Transform2D>();
                    FP dist = FP.UseableMax;
                    EntityRef closestHammock = default;
                    Transform2D* closest = default;
                    while (comps.NextUnsafe(out var entity, out var hammock, out var transform))
                    {
                        // don't pick a hammock that would kill us
                        if (BotHelper.IsDestinationInDeathZone(f, transform->Position)) continue;

                        FP d = FPVector2.DistanceSquared(transform->Position, filter.transform->Position);
                        if (d < dist)
                        {
                            dist = d;
                            closestHammock = entity;
                            closest = transform;
                        }
                    }

                    if (closest != default && FPVector2.Distance(closest->Position, filter.transform->Position) < acceptableDist)
                    {
                        filter.bot->destination = MovePositionToClosestNavmeshSpot(f, closest->Position, closest->Up, true);
                        filter.bot->lastTimeGoalInitialized = f.Global->time;
                        return;
                    }
                }

                // in survival mode we want to stay around our vases
                if (f.RuntimeConfig.gameMode.HasFlag(GameMode.Survival))
                {
                    GoToNearestNexus(f, ref filter);
                }
                else
                {
                    // select a random point inside of our manager bounds
                    if (f.Global->brManager != default && f.Unsafe.TryGetPointer<BRManager>(f.Global->brManager, out var manager))
                    {
                        FP offset = manager->minimumRadius / 2;
                        var bounds = manager->bounds;
                        FP factor = FP._1 - FP._0_05;
                        filter.bot->destination = new FPVector2(
                            f.RNG->Next((manager->bounds.Min.X + offset) * factor, (manager->bounds.Max.X - offset) * factor),
                            f.RNG->Next((manager->bounds.Min.Y + offset) * factor, (manager->bounds.Max.Y - offset) * factor));
                        filter.bot->destination += manager->bounds.Center;

                        AdjustDestinationToBeOnNavmesh(f, ref filter);
                        filter.bot->lastTimeGoalInitialized = f.Global->time;
                    }
                }
            }
        }

        public static bool GoToNearestNexus(Frame f, ref BRBotSystem.Filter filter)
        {
            var comps = f.Filter<SurvivalNexus, Transform2D>();
            FP dist = FP.UseableMax;
            EntityRef closestComponent = default;
            Transform2D* closest = default;
            while (comps.NextUnsafe(out var entity, out var component, out var transform))
            {
                // don't pick a hammock that would kill us
                if (BotHelper.IsDestinationInDeathZone(f, transform->Position)) continue;

                FP d = FPVector2.DistanceSquared(transform->Position, filter.transform->Position);
                if (d < dist)
                {
                    dist = d;
                    closestComponent = entity;
                    closest = transform;
                }
            }

            if (closest != default) //&& FPVector2.Distance(closest->Position, filter.transform->Position) < acceptableDist)
            {
                filter.bot->destination = closest->Position + new FPVector2(f.RNG->Next(-FP._5, FP._5), f.RNG->Next(-FP._5, FP._5));// MovePositionToClosestNavmeshSpot(f, closest->Position, closest->Up, true);
                AdjustDestinationToBeOnNavmesh(f, ref filter);
                filter.bot->lastTimeGoalInitialized = f.Global->time;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fixes the destination to be on the navmesh
        /// </summary>
        public static void AdjustDestinationToBeOnNavmesh(Frame f, ref BRBotSystem.Filter filter)
        {
            if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
            {
                if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(filter.bot->destination.XOY, FP._3, f.RNG, filter.navigator->RegionMask, out var adjustedPosition))
                {
                    filter.bot->destination = adjustedPosition.XZ;
                }
                else if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(filter.bot->destination.XOY, FP._6, f.RNG, filter.navigator->RegionMask, out var adjustedPosition2))
                {
                    filter.bot->destination = adjustedPosition2.XZ;
                }
                else if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(filter.bot->destination.XOY, FP._10, f.RNG, filter.navigator->RegionMask, out var adjustedPosition3))
                {
                    filter.bot->destination = adjustedPosition3.XZ;
                }
                else if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(filter.bot->destination.XOY, FP._10 * FP._5, f.RNG, filter.navigator->RegionMask, out var adjustedPosition4))
                {
                    filter.bot->destination = adjustedPosition4.XZ;
                }
            }
        }
        /// <summary>
        /// Returns a random position close to our given position that is also on the navmesh
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        /// <param name="closeTo"></param>
        /// <param name="radius"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool GetRandomPositionOnNavmesh(Frame f, NavMeshPathfinder* navigator, FPVector2 closeTo, FP radius, out FPVector2 result)
        {
            if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
            {
                if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(closeTo.XOY, radius, f.RNG, navigator->RegionMask, out var adjustedPosition))
                {
                    result = adjustedPosition.XZ;
                    return true;
                }
            }
            result = closeTo;
            return false;
        }

        /// <summary>
        /// Fixes the destination to be on the navmesh
        /// </summary>
        public static FPVector2 MovePositionToClosestNavmeshSpot(Frame f, FPVector2 position, FPVector2 directionToSearch, bool useVerySmallRadius = false)
        {
            directionToSearch = directionToSearch.Normalized;
            if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
            {
                //f.Map.NavMeshes["navmesh"].Find
                for (int i = 0; i < 10; i++)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(position.XOY, default))
                    {
                        return position;
                    }
                    else
                    {
                        if (useVerySmallRadius)
                        {
                            position += directionToSearch * FP._0_50;
                        }
                        else
                        {
                            position += directionToSearch;
                        }
                    }
                }
                if (f.Map.NavMeshes["navmesh"].FindRandomPointOnNavmesh(position.XOY, FP._2, f.RNG, default, out var adjustedPosition))
                {
                    return adjustedPosition.XZ;
                }
                return position;
            }
            return position;
        }

        public static FPVector2 MovePositionToClosestNavmeshSpot(Frame f, FPVector2 position, int iteration = 1)
        {
            FPVector2 initial = position;
            if (f.Map.NavMeshes != null && f.Map.NavMeshes.Count > 0)
            {
                //f.Map.NavMeshes["navmesh"].Find
                for (int i = 0; i < 2; i++)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(position.XOY, default))
                    {
                        return position;
                    }
                    else
                    {
                        position += FPVector2.Right * iteration;
                    }
                }
                position = initial;
                for (int i = 0; i < 2; i++)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(position.XOY, default))
                    {
                        return position;
                    }
                    else
                    {
                        position += FPVector2.Left * iteration;
                    }
                }
                position = initial;
                for (int i = 0; i < 2; i++)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(position.XOY, default))
                    {
                        return position;
                    }
                    else
                    {
                        position += FPVector2.Up * iteration;
                    }
                }
                position = initial;
                for (int i = 0; i < 2; i++)
                {
                    if (f.Map.NavMeshes["navmesh"].Contains(position.XOY, default))
                    {
                        return position;
                    }
                    else
                    {
                        position += FPVector2.Down * iteration;
                    }
                }
                if (iteration < 6)
                {
                    return MovePositionToClosestNavmeshSpot(f, position, iteration + 1);
                }

                return position;
            }
            return position;
        }

        /// <summary>
        /// Monitor secondary ability usage
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        public static void MonitorAuxiliarAbilityUsage(Frame f, ref BRBotSystem.Filter filter)
        {
            if (filter.bot->goal == BotGoal.RunawayFromEnemy)
            {
                BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.RunawayFromTarget, true);
                return;
            }

            Playable* bot = default;
            f.Unsafe.TryGetPointer<Playable>(filter.entity, out bot);
            FPVector2 lastDir = default;
            if (bot != default) lastDir = bot->botData.botInput.MovementDirection;
            if (f.Unsafe.TryGetPointer<BRMemory>(filter.entity, out var memory) == false) return;

            // break cc?
            if (filter.controller->IsStunned() || (f.Unsafe.TryGetPointer<EffectHandler>(filter.entity, out var handler) && handler->IsUnderEffectCategory(f, EffectCategory.Slow)))
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.BreakCC, true))
                {
                    return;
                }
            }

            // heal self?
            if (filter.health->CurrentPercentage < FP._0_75)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.RecoverHealth, true))
                {
                    return;
                }
            }
            // -------------- we're tryna be sneaky!
            if (filter.bot->goal == BotGoal.StayInBrush) return;

            // are we getting attacked?
            if (filter.controller->senses.detectedAttackAvoidanceWindow > FP._0 && filter.controller->senses.detectedAttackSource != default)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, filter.controller->senses.detectedAttackSource, ReasonForUse.AvoidAttack, true))
                {
                    return;
                }
            }

            // is there an incoming projectile?

            // nearby ally needs help?
            /*
            for (int i = 0; i < filter.world->players.Length; i++)
            {
                if (filter.world->players[i].affiliation != BotWorldCharacterAffiliation.Ally) continue;
                if (filter.world->players[i].distance > 12) continue;
                if (filter.world->players[i].entity == filter.entity) continue;

                if (filter.world->players[i].health <= FP._0_75 && filter.world->players[i].health > FP._0)
                {
                    if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.HeallAlly, true))
                    {
                        filter.world->Emote(f, EmoteOccasion.HelpedAlly);
                        return;
                    }
                }
                if (f.Unsafe.TryGetPointer<CharacterController>(filter.world->players[i].entity, out var controller) && controller->IsInCombat(f))
                {
                    if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.AssistAllyInCombat, true))
                    {
                        filter.world->Emote(f, EmoteOccasion.HelpedAlly);
                        return;
                    }
                }
            }
            */

            // chasing enemy? / running toward objective?
            if (filter.bot->goal == BotGoal.DefeatEnemy && memory->enemy != default && f.Has<Playable>(memory->enemy))
            {
                // make sure we have room
                if (QTools.Distance(f, filter.entity, memory->enemy) > FP._5)
                {
                    if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.ChaseTarget, true))
                    {
                        return;
                    }
                }
            }
            else if (filter.bot->goal == BotGoal.SecureFlag || filter.bot->goal == BotGoal.DeliverFlag)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.ChaseTarget, true))
                {
                    return;
                }
            }
            // if we have any ability that can hit a target in our range, use it regardless of what our current goal / context is
            /*
            if (filter.world->entities.vase != default && filter.world->distances.vase < 4)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, filter.world->entities.vase, ReasonForUse.KillTarget, true))
                {
                    return;
                }
            }
            if (filter.world->entities.mouse != default && filter.world->distances.mouse < 4)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, filter.world->entities.mouse, ReasonForUse.KillTarget, true))
                {
                    return;
                }
            }
            */
            if (memory->enemy != default && filter.bot->goal != BotGoal.DefeatEnemy)
            {
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.RunawayFromTarget, true) || BotHelper.ActivateBestAbilityOption(f, filter.entity, memory->enemy, ReasonForUse.CCTarget, true))
                {
                    BRBotActions.Emote(f, filter.entity, EmoteOccasion.Runaway);
                    return;
                }
                if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.KillTargetWithoutLookingOrStopping, true, true))
                {
                    // dont make those impact our direction
                    if (bot != default && bot->botData.botInput.MovementDirection != lastDir) bot->botData.botInput.MovementDirection = lastDir;
                    return;
                }
            }

            // ultimately always attempt to cast (the use off-cooldown) type of abilities
            if (BotHelper.ActivateBestAbilityOption(f, filter.entity, default, ReasonForUse.OffCooldown, true))
            {
                return;
            }
        }
    }
}
