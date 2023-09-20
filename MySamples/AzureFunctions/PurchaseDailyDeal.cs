namespace MySamples.Azure
{
    public static class PurchaseDailyDeal
    {
        [System.Serializable]
        public class Deal
        {
            // if true, user will automatically have this promoted to them in-game
            public bool promote;
            // deal is exclusive and has already been purchased
            public bool alreadyPurchased;
            public string dealId;
            public DealItem[] contents;
            // is this a common, odd, rare, epic or legendary deal.
            public ColorTheme colorTheme;
            public string _currency;
            public string currency
            {
                get
                {
                    if (string.IsNullOrEmpty(_currency)) return "CC";
                    return _currency;
                }
            }
            public int discount;
            public DealDiscountType discountType;
        }

        [FunctionName("PurchaseDailyDeal")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            AKMResult syncResult = new AKMResult();
            string body = await req.ReadAsStringAsync();
            var requestData = PlayFabTools.ReadPlayFabFunctionRequest(body);

            int dealType = 0;
            if (requestData.parameters.ContainsKey("deal-type"))
            {
                int.TryParse(requestData.parameters["deal-type"].ToString(), out dealType);
            }
            int dealIndex = 1;
            if (requestData.parameters.ContainsKey("deal-index"))
            {
                int.TryParse(requestData.parameters["deal-index"].ToString(), out dealIndex);
            }

            //Get Store costs
            var storeRequest = new PlayFab.ServerModels.GetStoreItemsServerRequest();
            storeRequest.AuthenticationContext = requestData.authContext;
            storeRequest.PlayFabId = requestData.playFabId;
            storeRequest.StoreId = "default";
            var storeResult = await PlayFab.PlayFabServerAPI.GetStoreItemsAsync(storeRequest);
            if (storeResult.Result == null || storeResult.Error != null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "Failed to get store items", log, storeResult.Error);
            }

            var playerDataRequest = new PlayFab.ServerModels.GetUserDataRequest();
            playerDataRequest.AuthenticationContext = requestData.authContext;
            playerDataRequest.PlayFabId = requestData.playFabId;
            playerDataRequest.Keys = new List<string>()
            {

                PlayFabTools.PLAYERDATA_READONLY_DEALS
            };
            var readOnlyData = await PlayFab.PlayFabServerAPI.GetUserReadOnlyDataAsync(playerDataRequest);
            PlayerDeals playerDeals = null;
            if (readOnlyData.Error == null && readOnlyData.Result != null)
            {
                if (readOnlyData.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_READONLY_DEALS))
                {
                    playerDeals = JsonConvert.DeserializeObject<PlayerDeals>(readOnlyData.Result.Data[PlayFabTools.PLAYERDATA_READONLY_DEALS].Value);
                }
            }
            if (playerDeals == null || playerDeals.deals == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "Failed to get player data", log, readOnlyData.Error);
            }

            GenerateDailyDeals.Deal deal = null;
            if (dealType == 0)
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.bigDeals.Length)
                {
                    deal = playerDeals.deals.bigDeals[dealIndex];
                }
            }
            else if (dealType == 1)
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.babyDeals.Length)
                {
                    deal = playerDeals.deals.babyDeals[dealIndex];
                }
            }
            else
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.lootBoxes.Length)
                {
                    deal = playerDeals.deals.lootBoxes[dealIndex];
                }
            }

            if (deal == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NoData, "Deal is null", log);
            }

            if (deal.alreadyPurchased)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NoData, "Deal has already been purchased.", log);
            }

            // Get our inventory
            var inv = await PlayFabTools.GetInventory(requestData);
            if (inv.playfabError != null && inv.inventory == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.PlayFabError, "Failed to get inventory", log, inv.playfabError);
            }
            var items = inv.inventory.Inventory;

            // find the deal in the store
            PlayFab.ServerModels.StoreItem storeItem = null;
            for (int i = 0; i < storeResult.Result.Store.Count; i++)
            {
                if (storeResult.Result.Store[i].ItemId == deal.dealId)
                {
                    storeItem = storeResult.Result.Store[i];
                    break;
                }
            }

            if (storeItem == null)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NoData, "Deal not found in the store. Deal: [" + deal.dealId + "]", log);
            }
            uint cost = 0;
            if (storeItem.VirtualCurrencyPrices == null || storeItem.VirtualCurrencyPrices.ContainsKey(deal.currency) == false)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NoData, "Store item did not have a matching currency. Required currency [" + deal.currency + "]. Store item [" + storeItem.ItemId + "]", log);
            }
            cost = storeItem.VirtualCurrencyPrices[deal.currency];

            var currencies = await PlayFabTools.GetPlayerCurrencies(requestData);
            if (currencies.currencies == null || currencies.currencies.ContainsKey(deal.currency) == false || currencies.currencies[deal.currency] < cost)
            {
                return syncResult.ReturnFailAndPrint(AKMError.NoMatchingCurrency, "Not enough currency to cover " + cost + " of " + deal.currency, log);
            }

            // Give the item(s) to the player
            var grant = await GrantDealToPlayer(requestData, deal, inv.inventory);
            if (grant.grantResult.success == false)
            {
                return syncResult.ReturnFailAndPrint(AKMError.GenericError, grant.grantResult.errorMessage, log, grant.grantResult.playfabError);
            }


            // Mark it as purchased
            if (dealType == 0)
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.bigDeals.Length)
                {
                    playerDeals.deals.bigDeals[dealIndex].alreadyPurchased = true;
                }
            }
            else if (dealType == 1)
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.babyDeals.Length)
                {
                    playerDeals.deals.babyDeals[dealIndex].alreadyPurchased = true;
                }
            }
            else
            {
                if (dealIndex >= 0 && dealIndex < playerDeals.deals.lootBoxes.Length)
                {
                    playerDeals.deals.lootBoxes[dealIndex].alreadyPurchased = true;
                }
            }

            // Consume Cost
            await PlayFabTools.SubtractCurrency(requestData, deal.currency, (int)cost);

            await PlayFabTools.UpdatePlayerData(new Dictionary<string, string>()
            {
                {PlayFabTools.PLAYERDATA_READONLY_DEALS, JsonConvert.SerializeObject(playerDeals)}
            }, requestData);

            return syncResult.ReturnSuccessAndPrint(log);
        }

        public struct GrantResult
        {
            public bool success;
            public PlayFab.PlayFabError playfabError;
            public string errorMessage;
            public bool grantedAvatarEmoteOrChroma;
        }
        public enum GrantType
        {
            Standard = 0,
            StarterKit = 1,
        }
        public static async Task<(GrantResult grantResult, GenerateDailyDeals.Deal modifiedDeal)> GrantDealToPlayer(PlayFabTools.PlayFabRequestData requestData, GenerateDailyDeals.Deal deal, PlayFab.ServerModels.GetUserInventoryResult inventory = null, GrantType grantType = GrantType.Standard,
        string friendPlayfabId = null, ILogger log = null, bool isLoC = false)
        {
            GrantResult result = new GrantResult();
            result.success = false;

            if (inventory == null)
            {
                var inv = await PlayFabTools.GetInventory(requestData);
                if (inv.playfabError != null && inv.inventory == null)
                {
                    return (result, deal);
                }
                inventory = inv.inventory;
            }

            if (deal.contents == null)
            {
                result.errorMessage = "Deal had no contents";
                return (result, deal);
            }
            List<GrantedDealItem> itemsWithQuality = new List<GrantedDealItem>();
            List<GrantedDealItem> petsWithQuality = new List<GrantedDealItem>();
            List<string> eggs = new List<string>();
            List<string> currencyBundles = new List<string>();
            Dictionary<string, int> currencies = new Dictionary<string, int>();
            List<string> champions = new List<string>();
            List<string> skins = new List<string>();
            List<string> shards = new List<string>();
            List<string> emotes = new List<string>();
            List<string> avatars = new List<string>();
            // Holds the name of the champion or skin
            List<string> chromaBundles = new List<string>();
            List<string> chromas = new List<string>();
            int xpBoost = 0;
            int passLevels = 0;
            bool pass = false;
            bool exists = false;
            System.Random rand = new System.Random();
            for (int i = 0; i < deal.contents.Length; i++)
            {
                switch (deal.contents[i].type)
                {
                    case GenerateDailyDeals.RewardType.Item:
                        exists = false;
                        for (int j = 0; j < itemsWithQuality.Count; j++)
                        {
                            if (itemsWithQuality[j].itemId == deal.contents[i].id)
                            {
                                exists = true;
                                itemsWithQuality[j].count++;
                            }
                        }
                        if (exists == false)
                        {
                            itemsWithQuality.Add(new GrantedDealItem()
                            {
                                itemId = deal.contents[i].id,
                                quality = deal.contents[i].quality
                            });
                        }
                        //items.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Character:
                        //items.Add(deal.contents[i].id);
                        champions.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Avatar:
                        result.grantedAvatarEmoteOrChroma = true;
                        if (deal.contents[i].id == "avatar")
                        {
                            var a = "avatar-" + rand.Next(18, 202);
                            deal.contents[i].id = a;
                            avatars.Add(a);
                        }
                        else avatars.Add(deal.contents[i].id);

                        break;
                    case GenerateDailyDeals.RewardType.Boost:
                        if (deal.contents[i].id.Contains("-1")) xpBoost += 1;
                        else if (deal.contents[i].id.Contains("-2")) xpBoost += 3;
                        else if (deal.contents[i].id.Contains("-3")) xpBoost += 7;
                        else if (deal.contents[i].id.Contains("-4")) xpBoost += 14;
                        else if (deal.contents[i].id.Contains("-5")) xpBoost += 30;
                        break;
                    case GenerateDailyDeals.RewardType.Chroma:
                        result.grantedAvatarEmoteOrChroma = true;
                        if (deal.contents[i].id == "chroma")
                        {
                            var c = GenerateDailyDeals.GetRandomChroma(inventory.Inventory, true, isLoC);
                            deal.contents[i].id = c.skinId + "-" + c.chroma;
                            chromas.Add(c.skinId + "-" + c.chroma);
                        }
                        else
                        {
                            if (deal.contents[i].id == "bundle")
                            {
                                chromaBundles.Add(deal.contents[i].id);
                            }
                            else chromas.Add(deal.contents[i].id);
                        }
                        break;
                    case GenerateDailyDeals.RewardType.Skin:
                        if (deal.contents[i].id == "skin")
                        {
                            deal.contents[i].id = GenerateDailyDeals.GetRandomSkin(inventory.Inventory);
                        }
                        skins.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.UpgradeShard:
                        if (deal.contents[i].count > 0)
                        {
                            for (int j = 0; j < deal.contents[i].count; j++)
                            {
                                shards.Add(deal.contents[i].id);
                            }
                        }
                        else shards.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Currency:
                        if (deal.contents[i].id.Length == 2)
                        {
                            if (currencies.ContainsKey(deal.contents[i].id)) currencies[deal.contents[i].id] += deal.contents[i].count;
                            else currencies.Add(deal.contents[i].id, deal.contents[i].count);
                        }
                        else currencyBundles.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Egg:
                        if (deal.contents[i].count > 0)
                        {
                            for (int j = 0; j < deal.contents[i].count; j++)
                            {
                                eggs.Add(deal.contents[i].id);
                            }
                        }
                        else eggs.Add(deal.contents[i].id);
                        //items.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Emote:
                        result.grantedAvatarEmoteOrChroma = true;
                        if (deal.contents[i].id == "emote")
                        {
                            var e = "emote-" + rand.Next(0, 84);
                            deal.contents[i].id = e;
                            emotes.Add(e);
                        }
                        else emotes.Add(deal.contents[i].id);
                        break;
                    case GenerateDailyDeals.RewardType.Pass:
                        pass = true;
                        break;
                    case GenerateDailyDeals.RewardType.PassLevel:
                        passLevels += System.Math.Max(1, deal.contents[i].count);
                        break;
                    case GenerateDailyDeals.RewardType.Pet:
                        //items.Add(deal.contents[i].id);
                        exists = false;
                        for (int j = 0; j < petsWithQuality.Count; j++)
                        {
                            if (petsWithQuality[j].itemId == deal.contents[i].id)
                            {
                                exists = true;
                                petsWithQuality[j].count++;
                            }
                        }
                        if (exists == false)
                        {
                            petsWithQuality.Add(new GrantedDealItem()
                            {
                                itemId = deal.contents[i].id,
                                quality = deal.contents[i].quality
                            });
                        }
                        break;
                }
            }

            if (emotes.Count > 0 || avatars.Count > 0 || chromaBundles.Count > 0 || chromas.Count > 0)
            {
                await GrantEmotesAvatarsOrChromas(emotes, avatars, chromaBundles, chromas, requestData);
            }
            if (currencyBundles.Count > 0)
            {
                var grantRequest = new PlayFab.ServerModels.GrantItemsToUserRequest();
                grantRequest.AuthenticationContext = requestData.authContext;
                grantRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                grantRequest.ItemIds = currencyBundles;
                var grantResult = await PlayFab.PlayFabServerAPI.GrantItemsToUserAsync(grantRequest);
                if (grantResult.Error != null)
                {
                    result.playfabError = grantResult.Error;
                    return (result, deal);
                }
                if (grantResult.Result == null)
                {
                    result.errorMessage = "Null grant item result";
                    return (result, deal);
                }
            }
            if (currencies.Count > 0)
            {
                foreach (var c in currencies)
                {
                    await PlayFabTools.GiveCurrency(requestData, c.Key, c.Value, string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId);
                }
            }

            if (champions.Count > 0 || itemsWithQuality.Count > 0 || eggs.Count > 0 || petsWithQuality.Count > 0 || skins.Count > 0 || shards.Count > 0)
            {
                var grantRequest = new PlayFab.ServerModels.GrantItemsToUsersRequest();
                grantRequest.ItemGrants = new List<PlayFab.ServerModels.ItemGrant>();
                grantRequest.AuthenticationContext = requestData.authContext;
                if (grantType == GrantType.StarterKit)
                {
                    var kitItem = new PlayFab.ServerModels.ItemGrant();
                    kitItem.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    kitItem.ItemId = "starter-kit-bundle";
                    grantRequest.ItemGrants.Add(kitItem);
                }
                System.Random random = new System.Random();
                // skins
                for (int i = skins.Count - 1; i >= 0; i--)
                {
                    if (PlayFabTools.OwnsItem(skins[i], inventory) == false)
                    {
                        var item = new PlayFab.ServerModels.ItemGrant();
                        item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                        item.ItemId = skins[i];
                        grantRequest.ItemGrants.Add(item);
                    }
                    skins.RemoveAt(i);
                }
                // Shards
                for (int i = shards.Count - 1; i >= 0; i--)
                {
                    //if(PlayFabTools.OwnsItem(shards[i], inventory) == false)
                    //{
                    var item = new PlayFab.ServerModels.ItemGrant();
                    item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    item.ItemId = shards[i];
                    grantRequest.ItemGrants.Add(item);
                    //}
                    shards.RemoveAt(i);
                }
                // --------- Grant champions
                for (int i = champions.Count - 1; i >= 0; i--)
                {
                    if (PlayFabTools.OwnsItem(champions[i], inventory))
                    {
                        continue;
                    }

                    var item = new PlayFab.ServerModels.ItemGrant();
                    item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    item.ItemId = champions[i];
                    //string spec = string.Empty;
                    int mastery = 3;
                    // for(int j = 0; j < 29; j++)
                    // {
                    //     spec += random.Next(1,3).ToString();
                    // }
                    item.Data = new Dictionary<string, string>()
                    {
                        {"mastery", mastery.ToString()}
                    };
                    grantRequest.ItemGrants.Add(item);
                    champions.RemoveAt(i);
                }
                // ---------- Grant items
                for (int i = itemsWithQuality.Count - 1; i >= 0; i--)
                {
                    if (PlayFabTools.OwnsItem(itemsWithQuality[i].itemId, inventory))
                    {
                        continue;
                    }

                    var item = new PlayFab.ServerModels.ItemGrant();
                    item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    item.ItemId = itemsWithQuality[i].itemId;
                    ItemCounts counts = new ItemCounts();
                    counts.counts = new int[10];
                    if (itemsWithQuality[i].quality >= 0 && itemsWithQuality[i].quality < 10) counts.counts[itemsWithQuality[i].quality] = System.Math.Max(1, itemsWithQuality[i].count);
                    else counts.counts[0] = System.Math.Max(1, itemsWithQuality[i].count);
                    item.Data = new Dictionary<string, string>()
                    {
                        {"count", JsonConvert.SerializeObject(counts)}
                    };
                    grantRequest.ItemGrants.Add(item);
                    itemsWithQuality.RemoveAt(i);
                }
                // ---------- Grant Eggs
                for (int i = eggs.Count - 1; i >= 0; i--)
                {
                    var item = new PlayFab.ServerModels.ItemGrant();
                    item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    item.ItemId = eggs[i];
                    item.Data = PlayFabTools.GetGrantEggData(eggs[i]);
                    grantRequest.ItemGrants.Add(item);
                    eggs.RemoveAt(i);
                }
                // --------- Grant pets
                for (int i = petsWithQuality.Count - 1; i >= 0; i--)
                {
                    if (PlayFabTools.OwnsItem(petsWithQuality[i].itemId, inventory))
                    {
                        continue;
                    }

                    var item = new PlayFab.ServerModels.ItemGrant();
                    item.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                    item.ItemId = petsWithQuality[i].itemId;
                    PetCounts counts = new PetCounts();
                    counts.counts = new int[10];
                    if (petsWithQuality[i].quality >= 0 && petsWithQuality[i].quality < 10) counts.counts[petsWithQuality[i].quality] = System.Math.Max(1, petsWithQuality[i].count);
                    else counts.counts[0] = System.Math.Max(1, petsWithQuality[i].count);
                    item.Data = new Dictionary<string, string>()
                    {
                        {"count", JsonConvert.SerializeObject(counts)}
                    };
                    grantRequest.ItemGrants.Add(item);
                    petsWithQuality.RemoveAt(i);
                }
                if (grantRequest.ItemGrants.Count > 0)
                {
                    var grant = await PlayFab.PlayFabServerAPI.GrantItemsToUsersAsync(grantRequest);
                    if (grant.Result == null || grant.Error != null)
                    {
                        result.playfabError = grant.Error;
                        result.errorMessage = "GrantItemsToUsersAsync failed";
                        return (result, deal);
                    }
                }


                // --------- Do we have over flow items?
                if (champions.Count > 0 || petsWithQuality.Count > 0 || itemsWithQuality.Count > 0)
                {
                    // ----------- Set champion to level 30
                    for (int i = 0; i < champions.Count; i++)
                    {
                        for (int j = 0; j < inventory.Inventory.Count; j++)
                        {
                            if (inventory.Inventory[j].ItemId == champions[i])
                            {
                                var updateInventoryRequest = new PlayFab.ServerModels.UpdateUserInventoryItemDataRequest();
                                updateInventoryRequest.AuthenticationContext = requestData.authContext;
                                updateInventoryRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                                updateInventoryRequest.ItemInstanceId = inventory.Inventory[j].ItemInstanceId;
                                int mastery = 0;

                                if (inventory.Inventory[j].CustomData != null && inventory.Inventory[j].CustomData.ContainsKey("mastery") && string.IsNullOrEmpty(inventory.Inventory[j].CustomData["mastery"]) == false)
                                {
                                    int.TryParse(inventory.Inventory[j].CustomData["mastery"], out mastery);
                                }
                                if (mastery < 3) mastery = 3;
                                updateInventoryRequest.Data = new Dictionary<string, string>()
                                {
                                    {"mastery", mastery.ToString()}
                                };
                                var updateInventoryResult = await PlayFab.PlayFabServerAPI.UpdateUserInventoryItemCustomDataAsync(updateInventoryRequest);
                                break;
                            }
                        }
                    }
                    // ----------- Set item quality
                    for (int i = 0; i < itemsWithQuality.Count; i++)
                    {
                        for (int j = 0; j < inventory.Inventory.Count; j++)
                        {
                            if (inventory.Inventory[j].ItemId == itemsWithQuality[i].itemId)
                            {
                                var updateInventoryRequest = new PlayFab.ServerModels.UpdateUserInventoryItemDataRequest();
                                updateInventoryRequest.AuthenticationContext = requestData.authContext;
                                updateInventoryRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                                updateInventoryRequest.ItemInstanceId = inventory.Inventory[j].ItemInstanceId;
                                ItemCounts counts = new ItemCounts();
                                if (inventory.Inventory[j].CustomData != null && inventory.Inventory[j].CustomData.ContainsKey("count"))
                                {
                                    try
                                    {
                                        counts = JsonConvert.DeserializeObject<ItemCounts>(inventory.Inventory[j].CustomData["count"]);
                                    }
                                    catch { }
                                }
                                bool noRank = inventory.Inventory[j].CustomData == null || inventory.Inventory[j].CustomData.ContainsKey("rank") == false;
                                if (counts.counts == null)
                                {
                                    counts.counts = new int[10];
                                    if (noRank) counts.counts[0] = 1;
                                }
                                if (counts.counts.Length < 10)
                                {
                                    List<int> c = new List<int>(counts.counts);
                                    for (int k = c.Count; k < 10; k++)
                                    {
                                        if (noRank)
                                        {
                                            if (k == 0) c.Add(1);
                                            else c.Add(0);
                                        }
                                        else
                                        {
                                            c.Add(0);
                                        }
                                    }
                                    counts.counts = c.ToArray();
                                }
                                if (itemsWithQuality[i].quality >= 0 && itemsWithQuality[i].quality < 10) counts.counts[itemsWithQuality[i].quality] += System.Math.Max(1, itemsWithQuality[i].count);
                                else counts.counts[0] += System.Math.Max(1, itemsWithQuality[i].count);
                                updateInventoryRequest.Data = new Dictionary<string, string>()
                                {
                                    {"count", JsonConvert.SerializeObject(counts)}
                                };
                                var updateInventoryResult = await PlayFab.PlayFabServerAPI.UpdateUserInventoryItemCustomDataAsync(updateInventoryRequest);
                                break;
                            }
                        }
                    }
                    // ----------- Set pet quality
                    for (int i = 0; i < petsWithQuality.Count; i++)
                    {
                        for (int j = 0; j < inventory.Inventory.Count; j++)
                        {
                            if (inventory.Inventory[j].ItemId == petsWithQuality[i].itemId)
                            {
                                var updateInventoryRequest = new PlayFab.ServerModels.UpdateUserInventoryItemDataRequest();
                                updateInventoryRequest.AuthenticationContext = requestData.authContext;
                                updateInventoryRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
                                updateInventoryRequest.ItemInstanceId = inventory.Inventory[j].ItemInstanceId;
                                PetCounts counts = new PetCounts();
                                if (inventory.Inventory[j].CustomData != null && inventory.Inventory[j].CustomData.ContainsKey("count"))
                                {
                                    try
                                    {
                                        counts = JsonConvert.DeserializeObject<PetCounts>(inventory.Inventory[j].CustomData["count"]);
                                    }
                                    catch { }
                                }
                                if (counts.counts == null) counts.counts = new int[10];
                                if (counts.counts.Length < 10)
                                {
                                    List<int> c = new List<int>(counts.counts);
                                    for (int k = c.Count; k < 10; k++)
                                    {
                                        c.Add(0);
                                    }
                                    counts.counts = c.ToArray();
                                }
                                if (petsWithQuality[i].quality >= 0 && petsWithQuality[i].quality < 10) counts.counts[petsWithQuality[i].quality] += System.Math.Max(1, petsWithQuality[i].count);
                                else counts.counts[0] += System.Math.Max(1, petsWithQuality[i].count);
                                updateInventoryRequest.Data = new Dictionary<string, string>()
                                {
                                    {"count", JsonConvert.SerializeObject(counts)}
                                };
                                var updateInventoryResult = await PlayFab.PlayFabServerAPI.UpdateUserInventoryItemCustomDataAsync(updateInventoryRequest);
                                break;
                            }
                        }
                    }
                }
            }

            if (pass)
            {
                await PurchaseStoreItem.GrantKhashemPass(requestData, null);
            }

            if (passLevels > 0)
            {
                await PlayFabTools.GiveCurrency(requestData, "PX", 100 * passLevels);
            }

            if (xpBoost > 0)
            {
                await GrantXPBoost(requestData, xpBoost);
            }

            result.success = true;
            return (result, deal);
        }

        public static async Task<bool> GrantXPBoost(PlayFabTools.PlayFabRequestData requestData, int days)
        {
            var bdata = await PlayFabTools.GetPlayerData(new List<string>() { PlayFabTools.PLAYERDATA_READONLY_BOOSTS }, requestData);
            Boosts boosts = null;
            if (bdata.Item1 != null && bdata.Item2 == null && bdata.Item1.Data != null && bdata.Item1.Data.ContainsKey(PlayFabTools.PLAYERDATA_READONLY_BOOSTS))
            {
                boosts = JsonConvert.DeserializeObject<Boosts>(bdata.Item1.Data[PlayFabTools.PLAYERDATA_READONLY_BOOSTS].Value);
            }
            if (boosts == null) boosts = new Boosts();
            if (boosts.xpBoost == default || (System.DateTime.UtcNow - boosts.xpBoost).TotalHours >= 0) boosts.xpBoost = System.DateTime.UtcNow;
            boosts.xpBoost = boosts.xpBoost.AddDays(days);

            await PlayFabTools.UpdatePlayerData(new Dictionary<string, string>(){
                    {PlayFabTools.PLAYERDATA_READONLY_BOOSTS, JsonConvert.SerializeObject(boosts)}
                }, requestData);

            return true;
        }


        public static async Task<(PlayFab.ServerModels.UpdateUserDataResult, PlayFab.PlayFabError)> GrantEmotesAvatarsOrChromas(List<string> _emotes, List<string> _avatars, List<string> _chromaKeys, List<string> _singleChromas, PlayFabTools.PlayFabRequestData requestData, string friendPlayfabId = null)
        {
            var playerDataRequest = new PlayFab.ServerModels.GetUserDataRequest();
            playerDataRequest.AuthenticationContext = requestData.authContext;
            playerDataRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
            playerDataRequest.Keys = new List<string>()
            {
                PlayFabTools.PLAYERDATA_READONLY_EMOTES,
                PlayFabTools.PLAYERDATA_READONLY_AVATARS,
                PlayFabTools.PLAYERDATA_READONLY_CHROMAS
            };
            var readOnlyData = await PlayFab.PlayFabServerAPI.GetUserReadOnlyDataAsync(playerDataRequest);
            PlayerEmotes emotes = null;
            PlayerEmotes avatars = null;
            PlayerChromas chromas = null;
            if (readOnlyData.Error == null && readOnlyData.Result != null)
            {
                if (readOnlyData.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_READONLY_EMOTES))
                {
                    emotes = JsonConvert.DeserializeObject<PlayerEmotes>(readOnlyData.Result.Data[PlayFabTools.PLAYERDATA_READONLY_EMOTES].Value);
                }
                if (readOnlyData.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_READONLY_AVATARS))
                {
                    avatars = JsonConvert.DeserializeObject<PlayerEmotes>(readOnlyData.Result.Data[PlayFabTools.PLAYERDATA_READONLY_AVATARS].Value);
                }
                if (readOnlyData.Result.Data.ContainsKey(PlayFabTools.PLAYERDATA_READONLY_CHROMAS))
                {
                    chromas = JsonConvert.DeserializeObject<PlayerChromas>(readOnlyData.Result.Data[PlayFabTools.PLAYERDATA_READONLY_CHROMAS].Value);
                }
            }
            else return (null, readOnlyData.Error);

            if (emotes == null) emotes = new PlayerEmotes();
            if (avatars == null) avatars = new PlayerEmotes();
            if (chromas == null) chromas = new PlayerChromas();
            if (emotes.owned == null) emotes.owned = new List<string>();
            if (avatars.owned == null) avatars.owned = new List<string>();
            if (chromas.owned == null) chromas.owned = new Dictionary<string, List<int>>();

            if (_chromaKeys != null && _chromaKeys.Count > 0)
            {
                for (int i = 0; i < _chromaKeys.Count; i++)
                {
                    int ckyes = GenerateDailyDeals.GetChromaCount(_chromaKeys[i]);
                    for (int j = 0; j < ckyes; j++)
                    {
                        if (chromas.owned.ContainsKey(_chromaKeys[i]))
                        {
                            if (chromas.owned[_chromaKeys[i]].Contains(j) == false)
                            {
                                if (chromas.owned[_chromaKeys[i]].Contains(j) == false) chromas.owned[_chromaKeys[i]].Add(j);
                            }
                        }
                        else
                        {
                            chromas.owned.Add(_chromaKeys[i], new List<int>() { j });
                        }
                    }
                }
            }
            if (_singleChromas != null && _singleChromas.Count > 0)
            {
                for (int i = 0; i < _singleChromas.Count; i++)
                {
                    int chromaIndex = -1;
                    string[] split = _singleChromas[i].Split('-');
                    string key = _singleChromas[i];
                    if (split.Length > 1)
                    {
                        if (int.TryParse(split[split.Length - 1], out var x))
                        {
                            chromaIndex = x;
                        }
                    }
                    if (chromaIndex >= 0) key = key.Replace("-" + chromaIndex, "");
                    else chromaIndex = 0;

                    if (chromas.owned.ContainsKey(key))
                    {
                        if (chromas.owned[key].Contains(chromaIndex) == false) chromas.owned[key].Add(chromaIndex);
                    }
                    else
                    {
                        chromas.owned.Add(key, new List<int>() { chromaIndex });
                    }
                }
            }
            if (_emotes != null && _emotes.Count > 0)
            {
                for (int i = 0; i < _emotes.Count; i++)
                {
                    if (emotes.owned.Contains(_emotes[i]) == false) emotes.owned.Add(_emotes[i]);
                }
            }
            if (_avatars != null && _avatars.Count > 0)
            {
                for (int i = 0; i < _avatars.Count; i++)
                {
                    if (avatars.owned.Contains(_avatars[i]) == false) avatars.owned.Add(_avatars[i]);
                }
            }

            var updateReadOnlyDataRequest = new PlayFab.ServerModels.UpdateUserDataRequest();
            updateReadOnlyDataRequest.AuthenticationContext = requestData.authContext;
            updateReadOnlyDataRequest.PlayFabId = string.IsNullOrEmpty(friendPlayfabId) ? requestData.playFabId : friendPlayfabId;
            updateReadOnlyDataRequest.Data = new Dictionary<string, string>()
            {
                {PlayFabTools.PLAYERDATA_READONLY_EMOTES,JsonConvert.SerializeObject(emotes)},
                {PlayFabTools.PLAYERDATA_READONLY_AVATARS,JsonConvert.SerializeObject(avatars)},
                {PlayFabTools.PLAYERDATA_READONLY_CHROMAS,JsonConvert.SerializeObject(chromas)}
            };
            var result = await PlayFab.PlayFabServerAPI.UpdateUserReadOnlyDataAsync(updateReadOnlyDataRequest);
            if (result.Error != null) return (null, result.Error);
            return (result.Result, null);
        }
    }



    public class GrantedDealItem
    {
        public int quality;
        public string itemId;
        public int count = 1;
    }
}
