namespace MySamples.Azure
{
    public static class TribeCreate
    {

        [FunctionName("TribeCreate")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            AKMResult syncResult = new AKMResult();
            string body = await req.ReadAsStringAsync();
            var requestData = PlayFabTools.ReadPlayFabFunctionRequest(body);

            TribeCreationData tribeData = null;
            if (requestData.parameters.ContainsKey("tribe") && requestData.parameters["tribe"] != null)
            {
                tribeData = JsonConvert.DeserializeObject<TribeCreationData>(requestData.parameters["tribe"].ToString());
            }
            TribeMember creatorData = null;
            if (requestData.parameters.ContainsKey("member") && requestData.parameters["member"] != null)
            {
                creatorData = JsonConvert.DeserializeObject<TribeMember>(requestData.parameters["member"].ToString());
            }
            if (tribeData == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NullResult, "No tribe data", log);
            }
            // Cannot exceed free icons on creation
            if (tribeData.icon >= 12) tribeData.icon = 0;

            if (creatorData == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NullResult, "No member data", log);
            }
            tribeData.creatorId = requestData.playFabId;
            bool usingNaynar = false;
            if (requestData.parameters.ContainsKey("use-naynar"))
            {
                bool.TryParse(requestData.parameters["use-naynar"].ToString(), out usingNaynar);
            }
            string groupId = null;
            if (requestData.parameters.ContainsKey("group-id"))
            {
                groupId = requestData.parameters["group-id"].ToString();
            }
            bool consumeCost = false;
            string currencyCode = "CC";
            int amount = 200;
            // We're creating a new group!
            if (string.IsNullOrEmpty(groupId))
            {
                consumeCost = true;
                tribeData.creationDate = System.DateTime.UtcNow;
                // -- Get inventory
                var inv = await PlayFabTools.GetInventory(requestData);
                if (inv.inventory == null || inv.playfabError != null)
                {
                    return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "Failed to retrieve inventory", log, inv.playfabError);
                }
                var inventory = inv.inventory.Inventory;
                if (inventory == null || inventory.Count == 0)
                {
                    return syncResult.ReturnFailAndPrint(AKMError.NoMatchingItem, "0 Items in inventory or inventory was null", log);
                }

                // does the player have the resources to create the tribe

                if (usingNaynar)
                {
                    currencyCode = "NC";
                    amount = 200;
                }
                if (inv.inventory.VirtualCurrency.ContainsKey(currencyCode) == false || inv.inventory.VirtualCurrency[currencyCode] < amount)
                {
                    return syncResult.ReturnFailAndPrint(AKMError.CheatSuspected_ItemCostMismatch, $"Not enough to cover cost. {currencyCode}:{amount}", log);
                }
                var entity = new PlayFab.GroupsModels.EntityKey()
                {
                    Id = requestData.authContext.EntityId,
                    Type = requestData.authContext.EntityType
                };
                // is the player already a member of a tribe?
                // is the tribe tag available? (Check title data)
                if (RedisDB.SetContains(RedisDB.KEY_TRIBE_LIST, tribeData.tag))
                {
                    RedisDB.CloseDBConnection();
                    return syncResult.ReturnFailAndPrint(AKMError.TribeTagTaken, "Tag already claimed", log);
                }
                // is the tribe name available? (Check title data)
                if (RedisDB.SetContains(RedisDB.KEY_TRIBE_NAME_LIST, tribeData.name))
                {
                    RedisDB.CloseDBConnection();
                    return syncResult.ReturnFailAndPrint(AKMError.TribeNameTaken, "name already claimed", log);
                }

                // Create group
                var request = new PlayFab.GroupsModels.CreateGroupRequest();
                request.GroupName = tribeData.tag;
                request.Entity = entity;
                request.AuthenticationContext = requestData.authContext;
                var result = await PlayFab.PlayFabGroupsAPI.CreateGroupAsync(request);
                // Put the data in the group
                //var groupDataRequest = new PlayFab.GroupsModels.UpdateGroupRequest();
                if (result == null || result.Error != null || result.Result == null)
                {
                    return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "CreateGroupAsync failed.", log);
                }
                groupId = result.Result.Group.Id;

                // add our guild to possible 
                tribeData.creatorId = requestData.playFabId;
                TribeTools.CreateRedisTribe(groupId, tribeData);
            }
            // get the group data first ------------------------
            var tribeEntity = new PlayFab.DataModels.EntityKey();
            tribeEntity.Id = groupId;
            tribeEntity.Type = "group";
            var coRequest = new PlayFab.DataModels.GetObjectsRequest();
            coRequest.AuthenticationContext = requestData.authContext;
            coRequest.Entity = tribeEntity;
            var currentObjects = await PlayFab.PlayFabDataAPI.GetObjectsAsync(coRequest);
            if (currentObjects.Error != null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "GetObjectsAsync failed.", log, currentObjects.Error);
            }
            if (currentObjects.Result == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "GetObjectsAsync failed.", log);
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
            //---------------------------------------------------
            bool added = false;
            for (int i = 0; i < dataList.Count; i++)
            {
                if (dataList[i].ObjectName == "tribe-info")
                {
                    added = true;
                    dataList[i].DataObject = JsonConvert.SerializeObject(tribeData);
                }
            }
            // this is not an update - add our member
            if (added == false)
            {
                dataList.Add(new PlayFab.DataModels.SetObject()
                {
                    ObjectName = "tribe-info",
                    DataObject = JsonConvert.SerializeObject(tribeData)
                });

                string members = creatorData.CompressedMember;
                dataList.Add(new PlayFab.DataModels.SetObject()
                {
                    ObjectName = "members-0",
                    DataObject = members
                });
            }
            var updateDataRequest = new PlayFab.DataModels.SetObjectsRequest();
            updateDataRequest.AuthenticationContext = requestData.authContext;
            updateDataRequest.Entity = tribeEntity;
            updateDataRequest.Objects = dataList;
            var updateResult = await PlayFab.PlayFabDataAPI.SetObjectsAsync(updateDataRequest);
            if (updateResult.Error != null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "SetObjectsAsync failed.", log, updateResult.Error);
            }
            if (updateResult.Result == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "SetObjectsAsync failed.", log);
            }
            // consume cost post update
            if (consumeCost)
            {
                var consumeResult = await PlayFabTools.SubtractCurrency(requestData, currencyCode, amount);
            }
            return syncResult.ReturnSuccessAndPrint(log);
        }
    }
}
