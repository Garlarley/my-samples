namespace MySamples.Azure
{
    public static class TribeTools
    {
        // Creates our tribe in Redis Cache
        public static async Task<bool> CreateRedisTribe(string groupIdInPlayfab, TribeCreationData tribeData, bool closeConnection = true, int memberCount = 1)
        {
            await RedisDB.AddToSetAsync(RedisDB.KEY_TRIBE_LIST, tribeData.tag);
            await RedisDB.AddToSetAsync(RedisDB.KEY_TRIBE_NAME_LIST, tribeData.name);
            RedisTribe tribe = new RedisTribe();
            tribe.info = tribeData;
            tribe.groupIdInPlayfab = groupIdInPlayfab;
            tribe.memberCount = memberCount;
            await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeData.tag, JsonConvert.SerializeObject(tribe));
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static async Task<bool> ModifyRedisTribeMemberCount(string tribeTag, int change, bool closeConnection = true)
        {
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag);
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    if (tribe.memberCount < 1) tribe.memberCount = 1;
                    tribe.memberCount += change;
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static async Task<bool> SetRedisTribeMemberCount(string tribeTag, int change, bool closeConnection = true)
        {
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag);
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    tribe.memberCount = change;
                    if (tribe.memberCount < 1) tribe.memberCount = 1;
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static async Task<bool> ModifyRedisTribeInfo(TribeCreationData tribeData, bool closeConnection = true)
        {
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeData.tag);
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    // we need to re-allow the usage
                    if (tribe.info.name != tribeData.name)
                    {
                        await RedisDB.RemoveFromSetAsync(RedisDB.KEY_TRIBE_NAME_LIST, tribe.info.name);
                        await RedisDB.AddToSetAsync(RedisDB.KEY_TRIBE_NAME_LIST, tribeData.name);
                    }
                    // we're changing our faction
                    if (tribe.info.faction != tribeData.faction)
                    {
                        // changing faction allegience resets contribution
                        tribe.factionContribution = 0;
                    }
                    // can't change tag!
                    tribeData.tag = tribe.info.tag;
                    tribe.info = tribeData;
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeData.tag, JsonConvert.SerializeObject(tribe));
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        //-- Creator name is to keep it up to date
        public static async Task<bool> ModifyRedisTribeXP(string playerId, string tribeTag, int change, uint lifetimeChange, bool closeConnection = true, PlayFabTools.PlayFabRequestData requestData = null, int memberCount = 0)
        {
            if (change <= 0) return false;
            int tribeFaction = -1;
            // -------- Tribe XP
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag);
            int tribeCont = 0;
            int icon = 0;
            string tribeName = string.Empty;

            // --------------------------------------------

            RedisTribe tribe = null;
            if (tribeStr.HasValue)
            {
                tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    if (memberCount > 0) tribe.memberCount = memberCount;
                    if (playerId == tribe.creatorId && requestData != null)
                    {
                        string creatorName = null;
                        // load our name ------------------------------
                        var infoRequest = new PlayFab.ServerModels.GetPlayerProfileRequest();
                        infoRequest.AuthenticationContext = requestData.authContext;
                        infoRequest.PlayFabId = requestData.playFabId;
                        var infoResult = await PlayFab.PlayFabServerAPI.GetPlayerProfileAsync(infoRequest);
                        if (infoResult.Error == null && infoResult != null)
                        {
                            if (infoResult.Result != null && infoResult.Result.PlayerProfile != null)
                            {
                                creatorName = infoResult.Result.PlayerProfile.DisplayName;
                            }
                        }
                        if (string.IsNullOrEmpty(creatorName) == false) tribe.creatorName = creatorName;
                    }
                    if (tribe.info != null)
                    {
                        tribeFaction = tribe.info.faction;
                        tribeName = tribe.info.name;
                        icon = tribe.info.icon;
                    }
                    // last bit of energy
                    if (tribe.Level < 20)
                    {
                        if (change > tribe.energy)
                        {
                            tribe.xp += tribe.energy;
                            tribe.energy = 0;
                        }
                        else
                        {
                            tribe.xp += change;
                            tribe.energy -= change;
                        }
                    }
                    tribe.factionContribution += change;
                    tribeCont = tribe.factionContribution;
                    if (tribe.contributions == null) tribe.contributions = new Dictionary<string, uint>();
                    if (tribe.contributions.ContainsKey(playerId) == false) tribe.contributions.Add(playerId, 0);
                    // we probably lost our cache, use the backed up contribution
                    if (tribe.contributions[playerId] == 0 && lifetimeChange > change) tribe.contributions[playerId] = lifetimeChange;
                    // otherwise, add onto existing one
                    else tribe.contributions[playerId] += (uint)change;
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                }
            }
            // ------- Faction War
            if (tribeFaction >= 0)
            {
                var factionStr = await RedisDB.GetAsync(RedisDB.KEY_FACTION_WAR);
                RedisFactionWar fwar = null;
                if (factionStr.HasValue)
                {
                    fwar = JsonConvert.DeserializeObject<RedisFactionWar>(factionStr.ToString());
                }
                else
                {
                    fwar = new RedisFactionWar();
                }
                fwar.ResetWarIfNeedBe();
                fwar.AddScore(change, tribeFaction);
                await RedisDB.SetAsync(RedisDB.KEY_FACTION_WAR, JsonConvert.SerializeObject(fwar));
            }

            // -------- Tribe leaderboards
            if (tribeCont > 0)
            {
                var lstr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_LEADERBOARD_SEASON);
                RedisTribeLeaderboard lb = null;
                if (lstr.HasValue)
                {
                    lb = JsonConvert.DeserializeObject<RedisTribeLeaderboard>(lstr.ToString());
                }
                else
                {
                    lb = new RedisTribeLeaderboard();
                }
                if (lb.entries == null) lb.entries = new List<RedisLeaderboardEntry>();
                bool isLarger = false;
                bool tribeExists = false;
                for (int i = 0; i < lb.entries.Count; i++)
                {
                    if (lb.entries[i].score < tribeCont)
                    {
                        isLarger = true;
                    }
                    if (lb.entries[i].tribeTag == tribeTag)
                    {
                        tribeExists = true;
                    }
                }
                // we should exist in this leaderboard
                if (isLarger || lb.entries.Count < 50)
                {
                    if (tribeExists == false)
                    {
                        if (lb.entries.Count > 50) lb.entries.RemoveAt(lb.entries.Count - 1);
                        var e = new RedisLeaderboardEntry();
                        e.tribeTag = tribeTag;
                        e.tribeName = tribeName;
                        e.score = tribeCont;
                        e.icon = icon;
                        e.faction = tribeFaction;
                        if (tribe != null)
                        {
                            e.level = tribe.Level;
                            e.members = tribe.memberCount;
                            e.leaderName = tribe.creatorName;
                        }
                        lb.entries.Add(e);
                    }
                    else
                    {
                        for (int i = 0; i < lb.entries.Count; i++)
                        {
                            if (lb.entries[i].tribeTag == tribeTag)
                            {
                                lb.entries[i].score = tribeCont;
                                lb.entries[i].tribeName = tribeName;
                                lb.entries[i].faction = tribeFaction;
                                lb.entries[i].icon = icon;
                                break;
                            }
                        }
                    }

                    // re-sort
                    lb.entries.Sort((x, y) => y.score.CompareTo(x.score));
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_LEADERBOARD_SEASON, JsonConvert.SerializeObject(lb));
                }
            }

            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static async Task<bool> AddRedisTribeApplicant(string tribeTag, string entityId, TribeMember applicantAsMemberData, bool closeConnection = true)
        {
            if (applicantAsMemberData == null) return false;
            TribeApplicant applicant = new TribeApplicant();
            applicant.avatar = applicantAsMemberData.avatar;
            applicant.entityId = entityId;
            applicant.id = applicantAsMemberData.id;
            applicant.name = applicantAsMemberData.name;
            applicant.rank = applicantAsMemberData.rank;
            applicant.level = applicantAsMemberData.level;
            applicant.power = applicantAsMemberData.power;
            return await AddRedisTribeApplicant(tribeTag, applicant, closeConnection);
        }
        public static async Task<bool> AddRedisTribeApplicant(string tribeTag, TribeApplicant applicant, bool closeConnection = true)
        {
            if (applicant == null) return false;
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag);
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    if (tribe.applicants == null) tribe.applicants = new List<TribeApplicant>();
                    bool exists = false;
                    for (int i = 0; i < tribe.applicants.Count; i++)
                    {
                        if (tribe.applicants[i].id == applicant.id || tribe.applicants[i].entityId == applicant.entityId)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (exists == false) tribe.applicants.Add(applicant);
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static async Task<bool> RemoveRedisTribeApplicant(string tribeTag, string memberEntityId, bool closeConnection = true)
        {
            var tribeStr = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag);
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    if (tribe.applicants == null) tribe.applicants = new List<TribeApplicant>();
                    for (int i = tribe.applicants.Count - 1; i >= 0; i--)
                    {
                        if (tribe.applicants[i].entityId == memberEntityId)
                        {
                            tribe.applicants.RemoveAt(i);
                        }
                    }
                    await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return true;
        }
        public static string GetRedisTribe(string groupId, string tag, PlayFabTools.PlayFabRequestData requestData, bool closeConnection = true, bool autoKeepUpToDate = true)
        {
            var t = RedisDB.Get(RedisDB.KEY_TRIBE_PREFIX + tag);

            if (autoKeepUpToDate)
            {
                KeepTribeUpToDate(groupId, t, tag, requestData, closeConnection);
            }
            else if (closeConnection) RedisDB.CloseDBConnection();
            return t;
        }
        static async Task<bool> KeepTribeUpToDate(string groupId, RedisValue tribeStr, string tribeTag, PlayFabTools.PlayFabRequestData requestData, bool closeConnection)
        {
            if (tribeStr.HasValue)
            {
                var tribe = JsonConvert.DeserializeObject<RedisTribe>(tribeStr.ToString());
                if (tribe != null)
                {
                    // -------------- This runs before GetRedisTribe
                    bool tribeChanged = false;
                    if (tribe.GrantEnergy()) tribeChanged = true;
                    var backup = await BackupRedisTribe(groupId, tribe, requestData);
                    if (backup) tribeChanged = true;
                    // -------------- This will not be returned to player
                    if (tribeChanged)
                    {
                        await RedisDB.SetAsync(RedisDB.KEY_TRIBE_PREFIX + tribeTag, JsonConvert.SerializeObject(tribe));
                    }
                    if (closeConnection) RedisDB.CloseDBConnection();
                    return true;
                }
            }
            if (closeConnection) RedisDB.CloseDBConnection();
            return false;
        }
        public static string GetRedisEntry(string key, bool closeConnection = true)
        {
            var t = RedisDB.Get(key);
            if (closeConnection) RedisDB.CloseDBConnection();
            return t;
        }
        public static (List<string>, List<string>) GetRedisTribeList(List<string> invitations, string targetId = null, int count = 15, bool closeConnection = true)
        {
            //invitations
            List<string> strTags = new List<string>();
            if (string.IsNullOrEmpty(targetId))
            {
                if (invitations != null)
                {
                    if (invitations.Count > 10)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            strTags.Add(invitations[i]);
                        }
                    }
                    else strTags.AddRange(invitations);
                    count = count - invitations.Count;
                    if (count < 5) count = 5;
                }
                var tags = RedisDB.GetSetRandom(RedisDB.KEY_TRIBE_LIST, count);
                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags[i].HasValue && strTags.Contains(tags[i].ToString()) == false)
                    {
                        strTags.Add(tags[i].ToString());
                    }
                }
            }
            else strTags.Add(targetId);

            var tribes = GetRedisTribeListInfo(strTags);
            if (closeConnection) RedisDB.CloseDBConnection();
            return (strTags, tribes);
        }
        public static List<string> GetRedisTribeListInfo(List<string> tags)
        {
            List<string> tribes = new List<string>();
            for (int i = 0; i < tags.Count; i++)
            {
                var tribe = RedisDB.Get(RedisDB.KEY_TRIBE_PREFIX + tags[i]);
                if (string.IsNullOrEmpty(tribe) == false) tribes.Add(tribe);
            }
            return tribes;
        }
        public static async Task<(List<string>, List<string>)> GetRedisTribeListAsync(int count = 15, bool closeConnection = true)
        {
            var tags = await RedisDB.GetSetRandomAsync(RedisDB.KEY_TRIBE_LIST, count);
            List<string> strTags = new List<string>();
            for (int i = 0; i < tags.Length; i++) strTags.Add(tags[i].ToString());
            var tribes = await GetRedisTribeListInfoAsync(tags);
            if (closeConnection) RedisDB.CloseDBConnection();
            return (strTags, tribes);
        }
        public static async Task<List<string>> GetRedisTribeListInfoAsync(RedisValue[] tags)
        {
            List<string> tribes = new List<string>();
            //for(int x = 0; x < 8; x++) // 16 tribe test
            //{
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].HasValue)
                {
                    var tribe = await RedisDB.GetAsync(RedisDB.KEY_TRIBE_PREFIX + tags[i].ToString());
                    if (string.IsNullOrEmpty(tribe) == false) tribes.Add(tribe);
                }
            }
            //}
            return tribes;
        }
        public static async Task<List<PlayFab.DataModels.SetObject>> GetTribeObjects(string groupId, PlayFabTools.PlayFabRequestData requestData)
        {
            var tribeEntity = new PlayFab.DataModels.EntityKey();
            tribeEntity.Id = groupId;
            tribeEntity.Type = "group";

            var coRequest = new PlayFab.DataModels.GetObjectsRequest();
            coRequest.AuthenticationContext = requestData.authContext;
            coRequest.Entity = tribeEntity;
            var currentObjects = await PlayFab.PlayFabDataAPI.GetObjectsAsync(coRequest);
            if (currentObjects.Error != null)
            {
                return null;
            }
            if (currentObjects.Result == null)
            {
                return null;
            }
            var currentDataList = currentObjects.Result.Objects;
            var dataList = new List<PlayFab.DataModels.SetObject>();
            if (currentDataList != null)
            {
                foreach (var d in currentDataList)
                {
                    var so = new PlayFab.DataModels.SetObject();
                    so.ObjectName = d.Key;
                    so.DataObject = d.Value.DataObject;
                    dataList.Add(so);
                }
            }
            return dataList;
        }
        public static async Task<bool> UpdateGroupObjects(string groupId, List<PlayFab.DataModels.SetObject> groupData, PlayFabTools.PlayFabRequestData requestData)
        {
            var tribeEntity = new PlayFab.DataModels.EntityKey();
            tribeEntity.Id = groupId;
            tribeEntity.Type = "group";
            var updateDataRequest = new PlayFab.DataModels.SetObjectsRequest();
            updateDataRequest.AuthenticationContext = requestData.authContext;
            updateDataRequest.Entity = tribeEntity;
            updateDataRequest.Objects = groupData;
            var updateResult = await PlayFab.PlayFabDataAPI.SetObjectsAsync(updateDataRequest);
            if (updateResult.Error != null || updateResult.Result == null)
            {
                return false;
            }
            return true;
        }
        public static async Task<bool> UpdateMemberData(string groupId, TribeMember member, PlayFabTools.PlayFabRequestData requestData)
        {
            if (member == null) return false;

            if (string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                bool modified = false;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (modified) break;
                        if (groupData[i].ObjectName.Contains("members-"))
                        {
                            var compressed = groupData[i].DataObject.ToString();
                            if (string.IsNullOrEmpty(compressed) == false)
                            {
                                string[] splitMembers = compressed.Split("|");
                                int memberIndex = -1;
                                for (int j = 0; j < splitMembers.Length; j++)
                                {
                                    if (splitMembers[j].Contains(member.id))
                                    {
                                        memberIndex = j;
                                    }
                                }
                                if (memberIndex >= 0)
                                {
                                    string newMemberList = string.Empty;
                                    string modifiedMember = member.CompressedMember;
                                    for (int j = 0; j < splitMembers.Length; j++)
                                    {
                                        if (newMemberList.Length > 0) newMemberList += "|";
                                        if (j != memberIndex) newMemberList += splitMembers[j];
                                        else newMemberList += modifiedMember;
                                    }
                                    if (newMemberList.Length < 1000)
                                    {
                                        groupData[i].DataObject = newMemberList;
                                        modified = true;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else return false;
                // we need to update data
                if (modified)
                {
                    var result = await UpdateGroupObjects(groupId, groupData, requestData);
                    if (result == false) return false;
                }
            }
            else return false;
            return true;
        }
        public static async Task<bool> AddMemberToData(string groupId, TribeMember member, PlayFabTools.PlayFabRequestData requestData)
        {
            return await AddMembersToData(groupId, new List<TribeMember> { member }, requestData);
        }
        public static async Task<bool> AddMembersToData(string groupId, List<TribeMember> members, PlayFabTools.PlayFabRequestData requestData)
        {
            if (members == null || members.Count == 0) return false;
            for (int i = members.Count - 1; i >= 0; i--)
            {
                if (members[i] == null) members.RemoveAt(i);
            }
            if (members.Count == 0) return false;

            int requiredBytes = 60 * members.Count;
            if (/*string.IsNullOrEmpty(member.id) == false && */string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                // object index, if not 0, we can append to existing member list, if < 0, we need to create a new object
                int oi = -1;
                string membersToAppendTo = string.Empty;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (groupData[i].ObjectName.Contains("members-"))
                        {
                            var compressed = groupData[i].DataObject.ToString();
                            for (int j = members.Count - 1; j >= 0; j--)
                            {
                                if (compressed.Contains(members[j].id))
                                {
                                    members.RemoveAt(j);
                                }
                            }
                        }
                    }
                    // no need to add, accounted for all members that we wanted to tadd
                    if (members.Count == 0) return true;
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (oi >= 0) break;
                        if (groupData[i].ObjectName.Contains("members-"))
                        {
                            var compressed = groupData[i].DataObject.ToString();
                            // we need at least 50 bytes to safely add a member
                            if (compressed.Length > 1000 - requiredBytes) continue;
                            membersToAppendTo = compressed;
                            oi = i;
                        }
                    }
                }
                else return false;

                if (membersToAppendTo.Length > 2 && membersToAppendTo.EndsWith('|') == false) membersToAppendTo += "|";
                for (int i = 0; i < members.Count; i++)
                {
                    if (i != 0) membersToAppendTo += "|";
                    membersToAppendTo += members[i].CompressedMember;
                }
                // we need to update data
                if (oi >= 0)
                {
                    groupData[oi].DataObject = membersToAppendTo;
                }
                else
                {
                    // -- TODO allow creation of a new group that can be linked to our guild and add members to it
                    if (groupData.Count >= 5) return false;
                    groupData.Add(new PlayFab.DataModels.SetObject()
                    {
                        ObjectName = "members-" + (groupData.Count - 1),
                        DataObject = membersToAppendTo
                    });
                }
                var result = await UpdateGroupObjects(groupId, groupData, requestData);
                if (result == false) return false;
            }
            else return false;
            return true;
        }
        public static async Task<bool> RemoveMemberFromData(string groupId, string memberPlayfabId, PlayFabTools.PlayFabRequestData requestData)
        {
            return await RemoveMembersFromData(groupId, new List<string> { memberPlayfabId }, requestData);
        }
        public static async Task<bool> RemoveMembersFromData(string groupId, List<string> membersPlayfabId, PlayFabTools.PlayFabRequestData requestData)
        {
            if (membersPlayfabId == null || membersPlayfabId.Count == 0) return false;
            for (int i = membersPlayfabId.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(membersPlayfabId[i])) membersPlayfabId.RemoveAt(i);
            }
            if (membersPlayfabId.Count == 0) return false;

            if (string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                bool deleted = false;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (deleted) break;
                        if (groupData[i].ObjectName.Contains("members-"))
                        {
                            var compressed = groupData[i].DataObject.ToString();
                            if (string.IsNullOrEmpty(compressed) == false)
                            {
                                string[] splitMembers = compressed.Split("|");
                                List<int> toDelete = new List<int>();
                                for (int j = 0; j < splitMembers.Length; j++)
                                {
                                    for (int k = 0; k < membersPlayfabId.Count; k++)
                                    {
                                        if (splitMembers[j].Contains(membersPlayfabId[k]))
                                        {
                                            toDelete.Add(j);
                                            break;
                                        }
                                    }
                                }
                                if (toDelete.Count > 0)
                                {
                                    string newMemberList = string.Empty;
                                    for (int j = 0; j < splitMembers.Length; j++)
                                    {
                                        bool delete = false;
                                        for (int k = 0; k < toDelete.Count; k++)
                                        {
                                            if (j == toDelete[k])
                                            {
                                                delete = true;
                                                break;
                                            }
                                        }
                                        bool containsMembersAlready = false;
                                        for (int k = 0; k < membersPlayfabId.Count; k++)
                                        {
                                            if (splitMembers[j].Contains(membersPlayfabId[k]))
                                            {
                                                containsMembersAlready = true;
                                                break;
                                            }
                                        }
                                        if (delete == false && containsMembersAlready == false)
                                        {
                                            if (newMemberList.Length > 0) newMemberList += "|";
                                            newMemberList += splitMembers[j];
                                        }
                                    }
                                    if (newMemberList.EndsWith('|')) newMemberList = newMemberList.Substring(0, newMemberList.Length - 1);
                                    groupData[i].DataObject = newMemberList;
                                    deleted = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else return false;
                // we need to update data
                if (deleted)
                {
                    var result = await UpdateGroupObjects(groupId, groupData, requestData);
                    if (result == false) return false;
                }
            }
            else return false;
            return true;
        }
        public static async Task<bool> EditMemberData(string groupId, string memberPlayfabId, TribeMemberProperty property, string newStr, int newInt, PlayFabTools.PlayFabRequestData requestData)
        {
            if (string.IsNullOrEmpty(memberPlayfabId) == false && string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                bool modified = false;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (modified) break;
                        if (groupData[i].ObjectName.Contains("members-"))
                        {
                            var compressed = groupData[i].DataObject.ToString();
                            if (string.IsNullOrEmpty(compressed) == false)
                            {
                                string[] splitMembers = compressed.Split("|");
                                int memberIndex = -1;
                                for (int j = 0; j < splitMembers.Length; j++)
                                {
                                    if (splitMembers[j].Contains(memberPlayfabId))
                                    {
                                        memberIndex = j;
                                    }
                                }
                                if (memberIndex >= 0)
                                {
                                    string newMemberList = string.Empty;
                                    var m = TribeMember.Decompressed(splitMembers[memberIndex]);
                                    switch (property)
                                    {
                                        case TribeMemberProperty.Name:
                                            m.name = newStr;
                                            break;
                                        case TribeMemberProperty.Role:
                                            m.role = newStr;
                                            break;
                                        case TribeMemberProperty.Rank:
                                            m.rank = newInt;
                                            break;
                                        case TribeMemberProperty.Contribution:
                                            m.contribution = newInt;
                                            break;
                                        case TribeMemberProperty.Avatar:
                                            m.avatar = newInt;
                                            break;
                                    }
                                    string modifiedMember = m.CompressedMember;
                                    for (int j = 0; j < splitMembers.Length; j++)
                                    {
                                        if (newMemberList.Length > 0) newMemberList += "|";
                                        if (j != memberIndex) newMemberList += splitMembers[j];
                                        else newMemberList += modifiedMember;
                                    }
                                    groupData[i].DataObject = newMemberList;
                                    modified = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else return false;
                // we need to update data
                if (modified)
                {
                    var result = await UpdateGroupObjects(groupId, groupData, requestData);
                    if (result == false) return false;
                }
            }
            else return false;
            return true;
        }
        public static async Task<TribeCreationData> GetTribeData(string groupId, PlayFabTools.PlayFabRequestData requestData)
        {
            if (string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (groupData[i].ObjectName == "tribe-info")
                        {
                            return JsonConvert.DeserializeObject<TribeCreationData>(groupData[i].DataObject.ToString());
                        }
                    }
                }
            }
            return null;
        }
        public static async Task<bool> EditTribeData(string groupId, TribeCreationData newData, PlayFabTools.PlayFabRequestData requestData)
        {
            if (string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                bool updated = false;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (groupData[i].ObjectName == "tribe-info")
                        {
                            var data = JsonConvert.DeserializeObject<TribeCreationData>(groupData[i].DataObject.ToString());
                            if (data != null)
                            {
                                data.autoApprove = newData.autoApprove;
                                data.isPublic = newData.isPublic;
                                if (string.IsNullOrEmpty(newData.description) == false) data.description = newData.description;
                                if (string.IsNullOrEmpty(newData.language) == false) data.language = newData.language;
                                if (string.IsNullOrEmpty(newData.name) == false) data.name = newData.name;
                                if (newData.labels != null) data.labels = newData.labels;
                                if (newData.faction >= 0) data.faction = newData.faction;
                                if (newData.minRank >= 0) data.minRank = newData.minRank;
                                if (newData.icon >= 0) data.icon = newData.icon;
                            }
                            groupData[i].DataObject = JsonConvert.SerializeObject(data);
                            updated = true;
                        }
                    }
                }
                else return false;
                // we need to update data
                if (updated)
                {
                    var result = await UpdateGroupObjects(groupId, groupData, requestData);
                    if (result == false) return false;
                }
            }
            else return false;
            return true;
        }
        public static async Task<bool> BackupRedisTribe(string groupId, RedisTribe rtribe, PlayFabTools.PlayFabRequestData requestData)
        {
            if (rtribe.ShouldBackup() == false) return false;

            if (string.IsNullOrEmpty(groupId) == false)
            {
                var groupData = await TribeTools.GetTribeObjects(groupId, requestData);
                bool updated = false;
                if (groupData != null)
                {
                    for (int i = 0; i < groupData.Count; i++)
                    {
                        if (groupData[i].ObjectName == "tribe-info")
                        {
                            var data = JsonConvert.DeserializeObject<TribeCreationData>(groupData[i].DataObject.ToString());
                            if (data != null)
                            {
                                data.xp = rtribe.xp;
                                data.cont = rtribe.factionContribution;
                                data.energy = rtribe.energy;
                                groupData[i].DataObject = JsonConvert.SerializeObject(data);
                                updated = true;
                            }

                        }
                    }
                }
                else return false;
                // we need to update data
                if (updated)
                {
                    var result = await UpdateGroupObjects(groupId, groupData, requestData);
                    if (result == false) return false;
                }
            }
            else return false;
            return true;
        }

        public static async Task<bool> AddToPlayerAppliedTribeList(string tribeTag, string playerId, PlayFabTools.PlayFabRequestData requestData)
        {
            var playerDataRequest = new PlayFab.ServerModels.GetUserDataRequest();
            playerDataRequest.AuthenticationContext = requestData.authContext;
            playerDataRequest.PlayFabId = playerId;
            playerDataRequest.Keys = new List<string>()
            {
                PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS
            };
            var data = await PlayFab.PlayFabServerAPI.GetUserDataAsync(playerDataRequest);
            if (data.Error == null && data.Result != null)
            {
                PlayerTribeApplications tribes = null;
                if (data.Result.Data != null && data.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS) && data.Result.Data[PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS] != null)
                {
                    tribes = JsonConvert.DeserializeObject<PlayerTribeApplications>(data.Result.Data[PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS].Value.ToString());
                }
                if (tribes == null) tribes = new PlayerTribeApplications();
                if (tribes.tribes == null) tribes.tribes = new List<string>();
                if (tribes.tribes.Contains(tribeTag) == false)
                {
                    tribes.tribes.Add(tribeTag);
                    var updateRequest = new PlayFab.ServerModels.UpdateUserDataRequest();
                    updateRequest.AuthenticationContext = requestData.authContext;
                    updateRequest.PlayFabId = playerId;
                    updateRequest.Data = new Dictionary<string, string>()
                    {
                        {PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS, JsonConvert.SerializeObject(tribes)}
                    };
                    var updateResult = await PlayFab.PlayFabServerAPI.UpdateUserDataAsync(updateRequest);
                }
                return true;
            }
            return false;
        }

        public static async Task<bool> ClearPlayerAppliedTribeList(string exceptionTag, string playerId, string playerEntityId, PlayFabTools.PlayFabRequestData requestData, bool closeConnection = false)
        {
            var playerDataRequest = new PlayFab.ServerModels.GetUserDataRequest();
            playerDataRequest.AuthenticationContext = requestData.authContext;
            playerDataRequest.PlayFabId = playerId;
            playerDataRequest.Keys = new List<string>()
            {
                PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS
            };
            var data = await PlayFab.PlayFabServerAPI.GetUserDataAsync(playerDataRequest);
            if (data.Error == null && data.Result != null)
            {
                PlayerTribeApplications tribes = null;
                if (data.Result.Data != null && data.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS) && data.Result.Data[PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS] != null)
                {
                    tribes = JsonConvert.DeserializeObject<PlayerTribeApplications>(data.Result.Data[PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS].Value.ToString());
                    if (tribes != null && tribes.tribes != null && tribes.tribes.Count > 0)
                    {
                        for (int i = 0; i < tribes.tribes.Count; i++)
                        {
                            if (string.IsNullOrEmpty(exceptionTag) || tribes.tribes[i] != exceptionTag)
                            {
                                RemoveRedisTribeApplicant(tribes.tribes[i], playerEntityId, false);
                            }
                        }
                        if (closeConnection) RedisDB.CloseDBConnection();
                        var updateRequest = new PlayFab.ServerModels.UpdateUserDataRequest();
                        updateRequest.AuthenticationContext = requestData.authContext;
                        updateRequest.PlayFabId = playerId;
                        tribes.tribes.Clear();
                        updateRequest.Data = new Dictionary<string, string>()
                        {
                            {PlayFabTools.PLAYERDATA_PUBLIC_TRIBE_APPLICATIONS, JsonConvert.SerializeObject(tribes)}
                        };
                        var updateResult = await PlayFab.PlayFabServerAPI.UpdateUserDataAsync(updateRequest);
                    }
                }
                return true;
            }
            return false;
        }
    }
}
