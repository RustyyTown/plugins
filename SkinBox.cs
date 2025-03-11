using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rust;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins 
{
	/* Based on 1.0.0 version by MarinWebCode */
    [Info("SkinBox", "MarinWebCode", "1.0.0")]
    class SkinBox : RustPlugin 
    {
		private static SkinBox instance;
		
        private bool skinsLoaded;        
        private static SkinBox skinBox = null;
        private bool isValidated = false;        
		
        private static Dictionary<string, LinkedList<ulong>> skinsCache = new Dictionary<string, LinkedList<ulong>>();        
        private static Dictionary<string, int> approvedSkinsCount = new Dictionary<string, int>();        
        private static Dictionary<string, DateTime> cooldownTimes = new Dictionary<string, DateTime>();
        private static Dictionary<string, string> NameToItemName = new Dictionary<string, string>();
        private static Dictionary<string, string> ItemNameToName = new Dictionary<string, string>();        
        private static List<ulong> excludedSkins = new List<ulong>();
		private static Dictionary <string, List<ulong>> manualAddedSkins = new Dictionary <string, List<ulong>>();
        private static List<object> excludedSkinsPre = new List<object>();
        private static Dictionary <ulong, Vector3> activeBoxUsers = new Dictionary <ulong, Vector3>();		
        		
        Coroutine _validateManualSkins;
        Coroutine _validateExcludedSkins;        
		
        private static Dictionary<string, object> missingWorkshopNames() 
        {
            var dp = new Dictionary<string, object>();
            dp.Add("rug", "Rug");
            dp.Add("chair", "Chair");
            dp.Add("rug.bear", "Bearskin Rug");
            dp.Add("mask.bandana", "Bandana");
            dp.Add("table", "Table");
            dp.Add("fridge", "Fridge");
            return dp;
        }        		      		                                     
        
        protected override void LoadDefaultMessages() 
        {
            lang.RegisterMessages(new Dictionary<string, string> 
            {                
                {"NoPermission", "Недостаточно прав!"},                 
                {"CooldownTime", "Что бы повторно поменять скины, вам нужно подождать {0} секунд."}                
            },this);
        }
        
        private void Init()
        {
            LoadVariables();
            LoadDefaultMessages();
			
            cmd.AddChatCommand("skin", this, "cmdSkinBox");
			cmd.AddChatCommand("skins", this, "cmdSkinBox");
			cmd.AddChatCommand("skinbox", this, "cmdSkinBox");
			
			foreach(var permissionUse in configData.permissionsUse)
				if (!permission.PermissionExists(permissionUse)) 
					permission.RegisterPermission(permissionUse, this);                                               
				
            skinsCache = new Dictionary<string, LinkedList<ulong>>();            
            approvedSkinsCount = new Dictionary<string, int>();
            NameToItemName = new Dictionary<string, string>();
            ItemNameToName = new Dictionary<string, string>();            
            activeBoxUsers = new Dictionary <ulong, Vector3>();
            skinsLoaded = false;
            skinBox = this;
        }
        
        private void OnServerInitialized() 
        {
			instance = this;
            UnsubscribeAll();
            timer.Once(65f, ()=> OnServerInit());			
        }
        
        private void Unload() 
        {
            var objs = UnityEngine.Object.FindObjectsOfType<SkinBoxHandler>().ToList();
            if (objs.Count > 0) 
				foreach (var obj in objs) 
				{
					if (obj.looter == null) continue;
					obj.looter.EndLooting();
					obj.PlayerStoppedLooting(obj.looter);
					GameObject.Destroy(obj);
				}
				
			foreach (BasePlayer player in BasePlayer.activePlayerList)				
				CuiHelper.DestroyUi(player, "Buttons");			
            
            if (Interface.Oxide.IsShuttingDown) return;
			
            if (_validateManualSkins != null) ServerMgr.Instance.StopCoroutine(_validateManualSkins);
            if (_validateExcludedSkins != null) ServerMgr.Instance.StopCoroutine(_validateExcludedSkins);            
        }
        
        private void OnServerInit() 
        {
            foreach (var itemtype in Rust.Workshop.Approved.All.Select(x=> x.Value).ToList()) 
            {
                if (itemtype.Skinnable.Name == null || itemtype.Skinnable.Name == string.Empty) continue;
                if (itemtype.Skinnable.ItemName == null || itemtype.Skinnable.ItemName == string.Empty) continue;
                if (!NameToItemName.ContainsKey(itemtype.Skinnable.Name.ToLower())) NameToItemName.Add(itemtype.Skinnable.Name.ToLower(), itemtype.Skinnable.ItemName.ToLower());
                if (!ItemNameToName.ContainsKey(itemtype.Skinnable.ItemName.ToLower()))                 
                    ItemNameToName.Add(itemtype.Skinnable.ItemName.ToLower(), itemtype.Skinnable.Name.ToLower());                                                
            }
            
            var tempMissing = new Dictionary<string, object>(configData.missingSkinNames);            
			tempMissing["rifle.lr300"] = "Lr300";
            foreach (var skin in tempMissing) 
            {
                var itemname = skin.Key.ToLower();
                var itemDef = ItemManager.FindItemDefinition(itemname);
                if (itemDef == null) continue;
                var workshopname = ((string)skin.Value).ToLower();
                NameToItemName[workshopname] = itemname;
                ItemNameToName[itemname] = workshopname;
            }
            
            foreach (var manual in configData.manualAddedSkinsPre) manualAddedSkins.Add(manual.Key.ToString(), (manual.Value as List<ulong>).ConvertAll(obj => Convert.ToUInt64(obj)));
            excludedSkins = configData.excludedSkinsPre.ConvertAll(obj => Convert.ToUInt64(obj));
            _validateManualSkins = ServerMgr.Instance.StartCoroutine(ValidateManualSkins(new Dictionary <string, List<ulong>>(manualAddedSkins), done => 
            {
                int result1 = manualAddedSkins.Values.Sum(list => list.Count);
                int result2 = done.Values.Sum(list => list.Count);
                if (result1 != result2) 
                {
                    manualAddedSkins = new Dictionary <string, List<ulong>>(done);
                    configData.manualAddedSkinsPre = manualAddedSkins;
                    SaveConfig(configData);
                }
                
                _validateExcludedSkins = ServerMgr.Instance.StartCoroutine(ValidateExcludedSkins(new List<ulong>(excludedSkins), done2 => 
                {
                    if (excludedSkins.Count != done2.Count) 
                    {
                        excludedSkins = new List<ulong>(done2);
                        configData.excludedSkinsPre = excludedSkins;
                        SaveConfig();
                    }
                    
                    _validateExcludedSkins = null;
                    isValidated = true;
                    GetItemSkins();
                }
                
                ));
                _validateManualSkins = null;
            }));
        }
        
        private IEnumerator ValidateManualSkins(Dictionary <string, List<ulong>> skinDict, System.Action<Dictionary <string, List<ulong>>> done) 
        {
            done(skinDict);
            yield return done;
        }
        
        private IEnumerator ValidateExcludedSkins(List<ulong> skinList, System.Action<List<ulong>> done) 
        {            
            done(skinList);
            yield return done;
        }
        
		private static string GetFixedItemName(string name) => name == "lr300.item" ? "rifle.lr300" : name;
		
        private void GetItemSkins() 
        {
            if (skinsLoaded) return;
            int countInbuilt = 0;
            foreach (var itemDef in ItemManager.GetItemDefinitions()) 
            {
                List<ulong> skins = new List<ulong>{0};
                skins.AddRange(ItemSkinDirectory.ForItem(itemDef).Select(skin => Convert.ToUInt64(skin.id)));
                skinsCache.Add(GetFixedItemName(itemDef.shortname), new LinkedList<ulong>(skins));
                if (skins.Count > 1) countInbuilt += (skins.Count -1);
            }
            
            if (configData.showLoadedSkinCounts) Puts($"Загружено {countInbuilt} встроенных скинов.");
            if (configData.useManualAddedSkins) 
            {
                int countManual = 0;
                foreach (var manualskins in manualAddedSkins) 
                {
                    string shortname = GetFixedItemName(manualskins.Key);
                    if (!ItemNameToName.ContainsKey(shortname)) continue;
                    string itemname = ItemNameToName[shortname];
                    List<ulong> fileids = manualskins.Value;
                    foreach (var fileid in fileids) 
                    {
                        if (!skinsCache.ContainsKey(shortname)) 
                        {
                            skinsCache.Add(shortname, new LinkedList<ulong>());
                            skinsCache[shortname].AddLast(0);
                        }
                        
                        if (!skinsCache[shortname].Contains(fileid)) 
                        {
                            skinsCache[shortname].AddLast(fileid);
                            countManual++;
                        }                                                
                    }                                        
                }
                
                if (configData.showLoadedSkinCounts && countManual > 0) Puts($"Загружено {countManual} скинов, добавленных вручную.");
            }
                        
			int countApproved = 0;
			foreach (var shopskin in Rust.Workshop.Approved.All.Select(x=> x.Value).Where(skin => skin.Skinnable.ItemName != null)) 
			{
				var skinName = GetFixedItemName(shopskin.Skinnable.ItemName);
				if (!approvedSkinsCount.ContainsKey(skinName)) approvedSkinsCount[skinName] = 0;                                       
				if (!skinsCache.ContainsKey(skinName)) skinsCache[skinName] = new LinkedList<ulong>();
				if (!skinsCache[skinName].Contains(shopskin.WorkshopdId)) 
				{
					skinsCache[skinName].AddLast(shopskin.WorkshopdId);
					approvedSkinsCount[skinName]++;
					countApproved++;
				}                                        
			}
			
			if (configData.showLoadedSkinCounts) Puts($"Загружено {countApproved} одобренных скинов.");
                        
			Interface.CallHook("OnSkinCacheUpdate", skinsCache, true);
			skinsLoaded = true;            
        }                
                
		[ConsoleCommand("skins.open")] 
		private void consoleSkinsOpen(ConsoleSystem.Arg arg) => consoleSkinboxOpen(arg);
				
        [ConsoleCommand("skinbox.open")] 
		private void consoleSkinboxOpen(ConsoleSystem.Arg arg) 
        {
            if (arg == null) return;
            if (arg.Connection == null) 
            {
				if (!skinsLoaded)
				{
					SendReply(arg, "Подождите, идет инициализация плагина.");
                    return;
				}
				
                if  (arg.Args == null || arg.Args.Length == 0) 
                {
                    SendReply(arg, $"Команда 'skinbox.open' требует наличия SteamId в качестве параметра.");
                    return;
                }
                
                ulong argId = 0uL;
                if (!ulong.TryParse(arg.Args[0], out argId)) 
                {
                    SendReply(arg, $"Указан некорректный SteamId: '{arg.Args[0]}'.");
                    return;
                }
                
                BasePlayer argPlayer = BasePlayer.FindByID(argId);
                if (argPlayer == null) 
                {
                    SendReply(arg, $"Не найден указанный игрок: '{argId}'.");
                    return;
                }
                
                if (!argPlayer.inventory.loot.IsLooting()) OpenSkinBox(argPlayer);
            }            
            else if(arg.Connection != null && arg.Connection.player != null) 
            {
                BasePlayer player = arg.Player();
                if (player.inventory.loot.IsLooting()) return;
				
                if (!HasPermission(player)) 
                {
                    player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                    return;
                }
				
				if (!skinsLoaded)
				{
					player.ChatMessage("Подождите, идет инициализация плагина.");
                    return;
				}
                                
                if (configData.enableCooldown && !player.IsAdmin)
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime time;
                    var key = player.UserIDString + "-box";
                    if (cooldownTimes.TryGetValue(key, out time)) 
                    {
                        if (time > now.AddSeconds(-configData.cooldownBox)) 
                        {
                            player.ChatMessage(string.Format(lang.GetMessage("CooldownTime", this, player.UserIDString),(time - now.AddSeconds(-configData.cooldownBox)).Seconds));
                            return;
                        }                                                
                    }                                        
                }
                
                OpenSkinBox(player);
            }                        
        }
        
        private void cmdSkinBox(BasePlayer player, string command, string[] args) 
        {
            if (player.inventory.loot.IsLooting()) return;
            if (!HasPermission(player))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
			
			if (!skinsLoaded)
			{
				player.ChatMessage("Подождите, идет инициализация плагина.");
				return;
			}
                        
            if (configData.enableCooldown && !player.IsAdmin) 
            {
                DateTime now = DateTime.UtcNow;
                DateTime time;
                var key = player.UserIDString + "-box";
                if (cooldownTimes.TryGetValue(key, out time)) 
                {
                    if (time > now.AddSeconds(-configData.cooldownBox)) 
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("CooldownTime", this, player.UserIDString),(time - now.AddSeconds(-configData.cooldownBox)).Seconds));
                        return;
                    }                                        
                }                                
            }
            
            timer.Once(0.2f, () => 
            {
                OpenSkinBox(player);
            });
        }      

		private bool HasPermission(BasePlayer player) 
		{
			if (player.IsAdmin)
				return true;
			
			foreach(var permissionName in configData.permissionsUse)
				if (!string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(player.UserIDString, permissionName))
					return true;
			
			return false;
		}	
        
        private sealed class SkinBoxHandler : MonoBehaviour 
        {
            public bool isCreating;
            public bool isBlocked;
            public bool isCleaning;
            public bool isEmptied;
			public bool isPaging;
            public int itemId;
            public Item item;
            public BasePlayer looter;
            public ItemContainer otherLoot;
			public ItemContainer showLoot;
            public BaseEntity entityOwner;
            public ulong skinId;
			public int page;
			public float lastOpen;
			
			public static List<SkinBoxHandler> SkinBoxes = new List<SkinBoxHandler>();
			
            void Awake() 
            {
                isCreating = false;
                isBlocked = false;
                isCleaning = false;
                isEmptied = false;
				isPaging = false;
                showLoot = GetComponent<DroppedItemContainer>().inventory;
                entityOwner = showLoot.entityOwner;
				page = 1;
				otherLoot = new ItemContainer();
				otherLoot.ServerInitialize(null, 1000);
				otherLoot.GiveUID();
				lastOpen = UnityEngine.Time.realtimeSinceStartup;				
				try { SkinBoxes.RemoveAll(x=> x == null); } catch {}
				SkinBoxes.Add(this);
            }
            
            public void PlayerStoppedLooting(BasePlayer player) 
            {				
                activeBoxUsers.Remove(player.userID);
                if (!isEmptied && item != null) 
                {
                    isEmptied = true;
                    player.GiveItem(item);
                }
				ClearContainer(otherLoot);
				CuiHelper.DestroyUi(looter, "Buttons");		
                if (activeBoxUsers.Count() == 0 && !Interface.Oxide.IsShuttingDown) skinBox.UnsubscribeAll();
                if (!GetComponent<BaseEntity>().IsDestroyed) GetComponent<BaseEntity>().Kill(BaseNetworkable.DestroyMode.None);
                if (configData.enableCooldown) cooldownTimes[player.UserIDString + "-box"] = DateTime.UtcNow;
            }
            
            void OnDestroy() 
            {											
                activeBoxUsers.Remove(looter.userID);
                if (!isEmptied && item!= null) 
                {
                    isEmptied = true;
                    looter.GiveItem(item);
                }				
				ClearContainer(otherLoot);
				CuiHelper.DestroyUi(looter, "Buttons");		
                looter.EndLooting();
				
				if (entityOwner != null)
					GameObject.Destroy(entityOwner);
				
				/*if ((UnityEngine.Time.realtimeSinceStartup - lastOpen) <= 0.5f)
				{
					if (looter != null)
						instance.OpenSkinBox(looter);
				}*/
            }                        
        }                
        
        private void OpenSkinBox(BasePlayer player) 
        {
			if (player == null) return;
			
			var ret = Interface.CallHook("SkinsCanUseSkins", player) as object;
			if (ret != null)
			{
				if (ret is string)
					SendReply(player, (string)ret);
				return;
			}

            if (activeBoxUsers.Count() == 0) SubscribeAll();
            			
			var skinBox = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", new Vector3(player.transform.position.x, -60f + UnityEngine.Random.Range(-4f, 4f), player.transform.position.z));
			
            var drop = skinBox as DroppedItemContainer;
			drop.playerName = configData.skinBoxTitle;	
			drop.playerSteamID = player.userID;			
			
			drop.Spawn();
														
			drop.inventory = new ItemContainer();
            drop.inventory.ServerInitialize(null, 36);
			drop.inventory.entityOwner = drop;
            drop.inventory.GiveUID();
			
			Rigidbody rigidBody = drop.GetComponent<Rigidbody>();
			rigidBody.useGravity = false;
			rigidBody.isKinematic = true;
            
			drop.SetFlag(BaseEntity.Flags.Open, true, false);						
			
            drop.gameObject.AddComponent<SkinBoxHandler>().looter = player;            
            drop.inventory.capacity = 36;						
            drop.SetFlag(BaseEntity.Flags.Open, true, false);
			
			player.inventory.loot.StartLootingEntity(drop, false);						
            player.inventory.loot.AddContainer(drop.inventory);						
            player.inventory.loot.SendImmediate();
			
			player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", drop.lootPanelName);			
            drop.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);			

            activeBoxUsers[player.userID] = player.transform.position;
        }
        
        private void StartLootingEntity(PlayerLoot loot, BaseEntity targetEntity) 
        {                        
            loot.Clear();
            if (!targetEntity) return;            
			loot.PositionChecks = false;
            loot.entitySource = targetEntity;
            loot.itemSource = null;            
			loot.MarkDirty();
        }
        
        private static void ClearContainer(ItemContainer container) 
        {
            while (container.itemList.Count > 0) 
            {
                var item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }                        
        }
        
        private object CanAcceptItem(ItemContainer container, Item item) 
        {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<SkinBoxHandler>() == null || 
				container.entityOwner != container.entityOwner.GetComponent<SkinBoxHandler>().entityOwner || 
				container.entityOwner.GetComponent<SkinBoxHandler>().isCreating || container.entityOwner.GetComponent<SkinBoxHandler>().isPaging) 
				return null;
            
			if (container.entityOwner.GetComponent<SkinBoxHandler>().isBlocked || item.amount > 1 || item.isBroken || 
				!skinsCache.ContainsKey(item.info.shortname) || (skinsCache[item.info.shortname] as LinkedList<ulong>).Count <= 1) 
				return ItemContainer.CanAcceptResult.CannotAccept;
				
            return null;
        }                
        
        private void OnItemAddedToContainer(ItemContainer container, Item item) 
        {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<SkinBoxHandler>() == null || 
				container.entityOwner != container.entityOwner.GetComponent<SkinBoxHandler>().entityOwner || 
				container.entityOwner.GetComponent<SkinBoxHandler>().isCreating || container.entityOwner.GetComponent<SkinBoxHandler>().isPaging) 
				return;
				
            var lootHandler = container.entityOwner.GetComponent<SkinBoxHandler>();
            lootHandler.isCreating = true;
            lootHandler.itemId = item.info.itemid;
            lootHandler.isEmptied = false;
            string shortname = item.info.shortname;
            bool hasCondition = item.hasCondition;
            float condition = item.condition;
			//float fuel = item.fuel;
			
			if (item.contents != null && item.contents.itemList.Count > 0)
			{
				var array = item.contents.itemList.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].info.category == ItemCategory.Weapon) continue;
					var item2 = array[i];
					lootHandler.looter.inventory.GiveItem(item2, null);
				}
			}
			
            float maxCondition = item.maxCondition;
            bool isWeapon = item.GetHeldEntity() != null && item.GetHeldEntity() is BaseProjectile;
            bool hasMods = false;
            int contents = 0;
            int capacity = 0;
            ItemDefinition ammoType = null;
            Dictionary<int, float> itemMods = new Dictionary<int, float>();
            LinkedList<ulong>  itemSkins = skinsCache[shortname] as LinkedList<ulong>;			
			
            if (isWeapon) 
            {
                contents = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents;
                capacity = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity;
                ammoType = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType;
                if (item.contents != null && item.contents.itemList.Count > 0) 
                {
                    hasMods = true;
                    foreach ( var mod in item.contents.itemList) itemMods.Add(mod.info.itemid, mod.condition);
                }                                
            }
            
            item.RemoveFromContainer();
            lootHandler.skinId = item.skin;
            lootHandler.item = item;
            container.capacity = itemSkins.Count();
            var itemDef = ItemManager.FindItemDefinition(shortname);
			int pos = 0;
			bool paged = false;						
			
            foreach (var skin in itemSkins) 
            {                				
                if (excludedSkins.Contains(skin) && !lootHandler.looter.IsAdmin) continue;
                Item newItem = ItemManager.Create(itemDef, 1, skin);
                
				if (hasCondition) 
                {
                    newItem.condition = condition;
                    newItem.maxCondition = maxCondition;
                }
				
				//newItem.fuel = fuel;
                
                if (isWeapon) 
                {
                    (newItem.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = contents;
                    (newItem.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity = capacity;
                    (newItem.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ammoType;
                    if (hasMods) 
                    {
                        foreach ( var mod in itemMods) 
                        {
                            Item newMod = ItemManager.CreateByItemID((int)mod.Key, 1);
                            newMod.condition = Convert.ToSingle(mod.Value);
                            newMod.MoveToContainer(newItem.contents, -1, false);
                        }
                        
                        newItem.contents.SetFlag(ItemContainer.Flag.IsLocked, true);
                        newItem.contents.SetFlag(ItemContainer.Flag.NoItemInput, true);
                    }                                        
                }                				
				
				if (container.itemList.Count < 36)
					newItem.MoveToContainer(container, pos, false);					
				else
				{	
					newItem.MoveToContainer(lootHandler.otherLoot, pos, false);		                
					paged = true;
				}	
				
				pos++;
            }
            
			if (paged) ShowButtons(lootHandler.looter);
			
            lootHandler.isCreating = false;
            lootHandler.isBlocked = true;
        }
        
        private void OnItemRemovedFromContainer(ItemContainer container, Item item) 
        {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.GetComponent<SkinBoxHandler>() == null || 
				container.entityOwner != container.entityOwner.GetComponent<SkinBoxHandler>().entityOwner || 
				container.entityOwner.GetComponent<SkinBoxHandler>().isCreating || container.entityOwner.GetComponent<SkinBoxHandler>().isCleaning ||
				container.entityOwner.GetComponent<SkinBoxHandler>().isPaging) 
				return;								
				
            var loothandler = container.entityOwner.GetComponent<SkinBoxHandler>();
            if (item.GetHeldEntity() != null && item.GetHeldEntity() is BaseProjectile) 
            {
                if (item.contents != null) 
                {
                    item.contents.SetFlag(ItemContainer.Flag.IsLocked, false);
                    item.contents.SetFlag(ItemContainer.Flag.NoItemInput, false);
                }                                
            }
            
            loothandler.isCleaning = true;
            ClearContainer(container);
			ClearContainer(loothandler.otherLoot);
			CuiHelper.DestroyUi(loothandler.looter, "Buttons");
			loothandler.page = 1;
            loothandler.isCleaning = false;
			
			if (loothandler.skinId != item.skin)
				Interface.CallHook("SkinsOnSkinChanged", loothandler.looter, item, loothandler.skinId);			
			
            if (loothandler.item != null) 
            {
                loothandler.item.Remove(0f);
                loothandler.item = null;
            }
            
            loothandler.isEmptied = true;
            container.capacity = 36;
            loothandler.isBlocked = false;
            if (item.skin == 0uL) 
            {
                loothandler.skinId = 0uL;
                return;
            }                        
            
            if (configData.enableCooldown && configData.activateAfterSkinTaken && !loothandler.looter.IsAdmin && item.skin != loothandler.skinId) 
            {
                activeBoxUsers.Remove(loothandler.looter.userID);
                loothandler.looter.EndLooting();
                cooldownTimes[loothandler.looter.UserIDString + "-box"] = DateTime.UtcNow;
            }
            
            loothandler.skinId = 0uL;
        }
        
        private void UnsubscribeAll() 
        {
			try { Unsubscribe(nameof(CanAcceptItem)); } catch { PrintWarning("Ошибка отписки хука CanAcceptItem"); }
            try { Unsubscribe(nameof(OnItemAddedToContainer)); } catch { PrintWarning("Ошибка отписки хука OnItemAddedToContainer"); }
            try { Unsubscribe(nameof(OnItemRemovedFromContainer)); } catch { PrintWarning("Ошибка отписки хука OnItemRemovedFromContainer"); }
        }
        
        private void SubscribeAll() 
        {
            try { Subscribe(nameof(CanAcceptItem)); } catch { PrintWarning("Ошибка подписки хука CanAcceptItem"); }
            try { Subscribe(nameof(OnItemAddedToContainer)); } catch { PrintWarning("Ошибка подписки хука OnItemAddedToContainer"); }
            try { Subscribe(nameof(OnItemRemovedFromContainer)); } catch { PrintWarning("Ошибка подписки хука OnItemRemovedFromContainer"); }
        }
        
        private void SendReplyCl(ConsoleSystem.Arg arg, string format) 
        {
            if (arg != null && arg.Connection != null) SendReply(arg, format);
            Puts(format);
        }        
		
		#region ExtHooks

		//void SkinsOnSkinChanged(BasePlayer player, Item item, ulong oldSkin);  - вызывается при смене скина у предмета
		
		#endregion
		
		#region GUI

		private void ShowButtons(BasePlayer player)
		{
			if (player == null) return;
			
			CuiHelper.DestroyUi(player, "Buttons");

			CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "Buttons",
				Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },										
                    new CuiRectTransformComponent()					
					{
                        AnchorMin = "0.655 0.070",
                        AnchorMax = "0.71 0.103"						
                    }
                }
            });                       

            CreateButton(player, container, "0.01 0.01", "0.40 0.99", true);						
            CreateButton(player, container, "0.60 0.01", "0.99 0.99", false);
            											
			CuiHelper.AddUi(player, container); 
		}
		
		private void ShowDir(BasePlayer player, bool isLeft)
		{			
			if (player == null) return;
			
			var name = player.UserIDString + (isLeft ? "_leftin" : "_rightin");
			CuiHelper.DestroyUi(player, name);
			CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {                
				Name = name,
                Parent = player.UserIDString + (isLeft ? "_left" : "_right"),
                Components =
                {                    
					new CuiRawImageComponent 
					{
						Url = isLeft ? "https://i.imgur.com/OzJdfxx.png" : "https://i.imgur.com/oM5tS89.png",
						Color = "1 1 0 0.6",
						Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = isLeft ? "0.15 0.2" : "0.07 0.2",
                        AnchorMax = isLeft ? "0.9 0.8" : "0.82 0.8"
                    }
                }
            });
			CuiHelper.AddUi(player, container);
			timer.Once(0.3f, ()=>CuiHelper.DestroyUi(player, name));
		}
				
        private void CreateButton(BasePlayer player, CuiElementContainer container, string AnchorMin, string AnchorMax, bool isLeft)
        {            
            container.Add(new CuiElement
            {
                Name = player.UserIDString + (isLeft ? "_left" : "_right"),
                Parent = "Buttons",
                Components =
                {                    
					new CuiRawImageComponent 
					{
						Url = isLeft ? "https://i.imgur.com/a9ofmZk.png" : "https://i.imgur.com/5ZUL5Ao.png",
						Color = "1 1 1 0.6",
						Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorMin,
                        AnchorMax = AnchorMax
                    }
                }
            });						            
			
			container.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = player.UserIDString + (isLeft ? "_left" : "_right"),
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = "button947283.select " + (isLeft ? "left" : "right"),                        
                        Color = "1 1 1 0"
                    },
                    new CuiRectTransformComponent()                    
                }
            });
        }									
		
		[ConsoleCommand("button947283.select")]
		private void ConsoleCmdSelect(ConsoleSystem.Arg arg)
		{
			if (arg.Connection != null)
            {
				string selectedButton = arg.Args[0];
				var player = arg.Player();
				if (player == null) return;

                switch(selectedButton)
                {
                    case "left":
						ShowDir(player, true);
						foreach(var box in SkinBoxHandler.SkinBoxes.Where(x=> x != null).ToList())
						{
							if (box.looter == player)
							{				
								if (box.page <= 1) return;
																
								int pageItemCount = 36;		

								box.showLoot.capacity = pageItemCount;
								
								int pageReadPos = (box.page-2)*36;
								int pageWritePos = (box.page-1)*36;
								
								box.isPaging = true;
								for(int ii=box.showLoot.itemList.Count-1;ii>=0;ii--)
								{
									var item = box.showLoot.itemList[ii];
									item.MoveToContainer(box.otherLoot, pageWritePos + item.position, false);									
								}	
								for(int ii=box.otherLoot.itemList.Count-1;ii>=0;ii--)
								{
									var item = box.otherLoot.itemList[ii];
									if (item.position >= pageReadPos && item.position < pageReadPos + pageItemCount)									
										item.MoveToContainer(box.showLoot, item.position - pageReadPos, false);									
								}			
								box.page--;								
								box.isPaging = false;								
								break;
							}
						}
						break;
                    case "right":						
						ShowDir(player, false);		
						foreach(var box in SkinBoxHandler.SkinBoxes.Where(x=> x != null).ToList())
						{
							if (box.looter == player)
							{																
								int pageItemCount = (box.showLoot.itemList.Count+box.otherLoot.itemList.Count)-36*box.page > 36 ? 36 : (box.showLoot.itemList.Count+box.otherLoot.itemList.Count)-36*box.page;
								if (pageItemCount <= 0) return;
																					
								box.showLoot.capacity = pageItemCount;
								
								int pageReadPos = box.page*36;
								int pageWritePos = (box.page-1)*36;
								
								box.isPaging = true;
								for(int ii=box.showLoot.itemList.Count-1;ii>=0;ii--)
								{
									var item = box.showLoot.itemList[ii];
									item.MoveToContainer(box.otherLoot, pageWritePos + item.position, false);									
								}	
								for(int ii=box.otherLoot.itemList.Count-1;ii>=0;ii--)
								{
									var item = box.otherLoot.itemList[ii];
									if (item.position >= pageReadPos && item.position < pageReadPos + pageItemCount)									
										item.MoveToContainer(box.showLoot, item.position - pageReadPos, false);									
								}								
								box.page++;
								box.isPaging = false;																
								break;
							}
						}
						break;                                        
                }                
			}
		}
		
		#endregion
		
		#region Config        						
		
        private static ConfigData configData;				
		
        private class ConfigData
        {            												
			[JsonProperty(PropertyName = "Разрешать использовать вручную добавленные скины")]
            public bool useManualAddedSkins;			
			
			[JsonProperty(PropertyName = "Надпись, отображаемая на панели скинов")]
			public string skinBoxTitle;			
			[JsonProperty(PropertyName = "Список привилегий для смены скинов")]
            public List<string> permissionsUse;			
			[JsonProperty(PropertyName = "Показывать счетчик загрузки скинов в консоле")]
            public bool showLoadedSkinCounts;						
			
			[JsonProperty(PropertyName = "Включить задержку перед повторным использованием скинов")]
			public bool enableCooldown;
			[JsonProperty(PropertyName = "Длительность задержки перед повторным использованием скинов (в секундах)")]
            public int cooldownBox;			
			[JsonProperty(PropertyName = "При использовании задержки разрешать менять только один скин")]            
            public bool activateAfterSkinTaken;
			
			[JsonProperty(PropertyName = "Список отсутствующих названий предметов, которым принадлежат скины")]
            public Dictionary<string, object> missingSkinNames;				
			[JsonProperty(PropertyName = "Список скинов, добавленных вручную")]
			public Dictionary<string, List<ulong>> manualAddedSkinsPre;			
			[JsonProperty(PropertyName = "Список скинов для исключения")]
			public List<ulong> excludedSkinsPre;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {                											
				useManualAddedSkins = true,								
				skinBoxTitle = "RUST PINK - Скины \n Перетащите предмет, чтобы изменить его модель",
				permissionsUse = new List<string>() { "skinbox.access", "skins.change" },				
				showLoadedSkinCounts = true,																		
				enableCooldown = false,
				cooldownBox = 60,									
				activateAfterSkinTaken = true,
				missingSkinNames = missingWorkshopNames(),
				manualAddedSkinsPre = new Dictionary<string, List<ulong>>(),
				excludedSkinsPre = new List<ulong>()
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=> SaveConfig(config));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
        
    }      

}