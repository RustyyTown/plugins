﻿using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlocked", "OxideBro", "1.0.2")]
    class BuildingBlocked : RustPlugin
    {
        int terrainMask = LayerMask.GetMask("Terrain");
        int constructionMask = LayerMask.GetMask("Construction");
        #region Configuration

        int waterLevel = 10;
        int maxHeight = 25;
        bool caveBuild = true;
        bool roadRestrict = true;
        bool iceBuild = false;
        bool adminIgnore = false;
        float treeRadius = 2f;
        bool buildBlock = true;
        private void LoadDefaultConfig()
        {
            GetConfig("Основные настройки", "Запрещать строительство глубже n метров под водой", ref waterLevel);
            GetConfig("Основные настройки", "Запрещать строительство выше n метров", ref maxHeight);
            GetConfig("Основные настройки", "Разрешить строительство в пещерах", ref caveBuild);
            GetConfig("Основные настройки", "Запрещать строительство в Building Block", ref buildBlock);
            // GetConfig("Основные настройки", "Разрешить строительство на айсбергах", ref iceBuild);
            GetConfig("Основные настройки", "Запрещать строительство на дорогах", ref roadRestrict);
            GetConfig("Основные настройки", "Игнорировать админов", ref adminIgnore);
            GetConfig("Основные настройки", "Минимальный радиус от дерева", ref treeRadius);
            SaveConfig();
        }


        private void GetConfig<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
            }
            Config[MainMenu, Key] = var;
        }


        #endregion

        readonly FieldInfo whiteListField = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Instance | BindingFlags.NonPublic));

        WaterCollision collision;
        public void AddHook(Plugin plugin, string hookname, int weight)
        {
            List<Hook> hookList;
            if (!hooks.TryGetValue(hookname, out hookList))
            {
                hooks[hookname] = hookList = new List<Hook>();
            }
            hookList.Add(new Hook(plugin, weight));
            SortHooks();
        }
        private readonly FieldInfo hooksField = typeof(PluginManager).GetField("hookSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic);
        new readonly Dictionary<string, List<Hook>> hooks = new Dictionary<string, List<Hook>>();
        class Hook
        {
            public Plugin plugin;
            public int weight;

            public Hook(Plugin plugin, int weight)
            {
                this.plugin = plugin;
                this.weight = weight;
            }
        }

        void SortHooks()
        {
            var hooksDescriptions =
                       ((IDictionary<string, IList<Plugin>>)hooksField.GetValue(Interface.Oxide.RootPluginManager));
            foreach (var hookname in hooks.Keys.ToList())
            {
                hooksDescriptions[hookname] =
                    hooksDescriptions[hookname].OrderByDescending(x => hooks[hookname].Find(p => p.plugin == x)?.weight ?? 0)
                        .ToList();
            }
        }
        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadConfig();
            LoadDefaultConfig();
            collision = UnityEngine.Object.FindObjectOfType<WaterCollision>();
            AddHook(this, nameof(OnEntityBuilt), 1000);
        }

        static void DrawBox(BasePlayer player, Vector3 center, Quaternion rotation, Vector3 size)
        {
            size /= 2;
            var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
            var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
            var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
            var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
            var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point1, point2);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point1, point3);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point1, point5);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point4, point2);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point4, point3);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point4, point8);

            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point5, point6);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point5, point7);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point6, point2);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point8, point6);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point8, point7);
            player.SendConsoleCommand("ddraw.line", 5, Color.blue, point7, point3);
        }

        static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }
        

        [ChatCommand("m")]
        void cmdChatMesh(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                var t = hit.transform;
                if (hit.GetEntity()?.ShortPrefabName != null)
                {
                    Puts($"ShortName: {hit.GetEntity().ShortPrefabName}");
                }//
                var comps = t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray();
                player.ChatMessage(
                    $"{t.name} [{t.gameObject.layer}: {LayerMask.LayerToName(t.gameObject.layer)}]: {string.Join(", ", comps)}");
                var msg = t.root.name + "\nQQQQQQQQQ\n";
                if (t.name == "Road Mesh")
                {
                    PrintWarning("sds");
                }
                if (t.root.childCount > 50)
                {
                    player.ChatMessage("ROOT CHILDS = "+t.root.childCount);
                    return;
                }
                var boxCollider = t.GetComponentsInChildren<Collider>();
                if (boxCollider.Length > 0)
                {
                    foreach (var b in boxCollider)
                    {
                        player.ChatMessage(b.name);
                        player.ChatMessage("Trigger: " + b.isTrigger);
                        DrawBox(player, b.bounds.center, t.rotation, b.bounds.size);
                    }
                }
            }
        }

        private bool HasCupboard(Vector3 pos, float radius, BaseEntity deployed)
        {
            var hits = Physics.OverlapSphere(pos, 25f, Rust.Layers.Server.Deployed);
            List<BuildingPrivlidge> buildingPrivilege = new List<BuildingPrivlidge>();
            foreach (var collider in hits)
            {
                var cBoard = collider.GetComponentInParent<BuildingPrivlidge>();
                if (cBoard == null || cBoard == deployed) continue;
                buildingPrivilege.Add(cBoard);
            }
            return buildingPrivilege.Count > 0;
        }

       

        void OnEntityBuilt(Planner planner, GameObject gameobject, Vector3 Pos)
        {

            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            if (player.IsAdmin && adminIgnore) return;
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (buildBlock)
            {
                if (player.IsBuildingAuthed())
                {
                    Refund(player, entity);
                    SendReply(player, string.Format(Messages["buildingBlocked"]));
                    entity.Kill();
                    return;
                }
            }
            Vector3 pos = entity.GetNetworkPosition();
            if (roadRestrict)
            {
                RaycastHit hit;
                if (Physics.Raycast(pos , Vector3.down, out hit,  terrainMask))
                {
                    if (hit.transform.name == "Road Mesh")
                    {
                        Refund(player, entity);
                        SendReply(player, Messages["roadBlock"]);
                        entity.Kill();
                        return;
                    }
                }
            }
            if (entity is BuildingPrivlidge)
            {
                if (HasCupboard(pos, 25f, entity))
                {
                    Refund(player, entity);
                    player.ChatMessage(Messages["AlreadyBuildingBuilt"]);
                    entity.Kill();
                    return;
                }
            }
            
            if (pos.y < -waterLevel)
                if (caveBuild && InCave(pos)) return;
                else
                {
                    Refund(player, entity);
                    player.ChatMessage(string.Format(Messages["waterLevel"], waterLevel));
                    entity.Kill();
                    return;
                }
            if (TerrainMeta.WaterMap.GetHeight(pos) - pos.y > waterLevel)
            {
                Refund(player, entity);
                player.ChatMessage(string.Format(Messages["waterLevel"], waterLevel));
                entity.Kill();
                return;
            }


            if (pos.y - TerrainMeta.HeightMap.GetHeight(pos) > maxHeight)
            {
                Refund(player, entity);
                SendReply(player, string.Format(Messages["heightLevel"], maxHeight));
                entity.Kill();
                return;
            }
            if (treeRadius > 0 && entity.ShortPrefabName.Contains("foundation"))
            {
                List<TreeEntity> treeList = new List<TreeEntity>();
                Vis.Entities(pos, treeRadius, treeList, LayerMask.GetMask("Tree"), QueryTriggerInteraction.Ignore);
                if (treeList.Count > 0)
                {
                    SendReply(player, string.Format(Messages["treeBlock"], (int)treeRadius));
                    Refund(player, entity);
                    entity.Kill();
                    return;
                }
            }

            if (entity.ShortPrefabName.Contains("foundation"))
            {
                List<BuildingBlock> blockList = new List<BuildingBlock>();
                Vis.Entities(pos, 4, blockList, constructionMask, QueryTriggerInteraction.Ignore);
                if (
                    blockList.Count(
                        block =>
                            block.ShortPrefabName.Contains("foundation") &&
                            CompareFoundationStacking(block.CenterPoint(), entity.CenterPoint())) > 1)
                {
                    entity.KillMessage();
                    SendReply(player, Messages["StackFoundation"], this);
                }
            }
        }
        bool CompareFoundationStacking(Vector3 vec1, Vector3 vec2)
        {
            return vec1.ToString("F4") == vec2.ToString("F4");
        }

        void SendReply(BasePlayer player, string msg)
        {
            base.SendReply(player, $"<size=16><color=#ff5400>{msg}</color></size>");
        }

        bool InCave(Vector3 vec) => collision.GetIgnore(vec);

        void Refund(BasePlayer player, BaseEntity entity)
        {
            RefundHelper.Refund(player, entity);
        }

        #region Refund
        public static class RefundHelper
        {
            private static Dictionary<uint, Dictionary<ItemDefinition, int>> refundItems =
                new Dictionary<uint, Dictionary<ItemDefinition, int>>();

            public static void Refund(BasePlayer player, BaseEntity entity, float percent = 1)
            {
                StorageContainer storage = entity as StorageContainer;
                if (storage)
                {
                    for (int i = storage.inventory.itemList.Count - 1; i >= 0; i--)
                    {
                        var item = storage.inventory.itemList[i];
                        if (item == null) continue;
                        item.amount = (int)(item.amount * percent);
                        float single = 20f;
                        Vector3 vector32 = Quaternion.Euler(UnityEngine.Random.Range(-single * 0.5f, single * 0.5f), UnityEngine.Random.Range(-single * 0.5f, single * 0.5f), UnityEngine.Random.Range(-single * 0.5f, single * 0.5f)) * Vector3.up;
                        BaseEntity baseEntity = item.Drop(storage.transform.position + (Vector3.up * 1f), vector32 * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation);
                        baseEntity.SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                    }
                }

                BuildingBlock block = entity as BuildingBlock;
                if (block != null)
                {
                    try
                    {
                        if (block.currentGrade == null) return;
                        foreach (var item in block.currentGrade.costToBuild)
                        {
                            var amount = (int)(item.amount * (Mathf.Approximately(percent, -1) ? 0.5f : percent));
                            if (amount < 1) amount = 1;
                            player.GiveItem(ItemManager.Create(item.itemDef, amount, 1));
                        }

                    }
                    catch
                    {

                    }
                    return;
                }

                Dictionary<ItemDefinition, int> items;
                if (refundItems.TryGetValue(entity.prefabID, out items))
                {
                    foreach (var item in items)
                        if (item.Value > 0)
                            player.GiveItem(ItemManager.Create(item.Key, (int)(item.Value)));
                }
            }

            private static void InitRefundItems()
            {
                foreach (var item in ItemManager.itemList)
                {
                    var deployable = item.GetComponent<ItemModDeployable>();
                    if (deployable != null)
                    {
                        if (item.Blueprint == null || deployable.entityPrefab == null) continue;
                        refundItems.Add(deployable.entityPrefab.resourceID, item.Blueprint.ingredients.ToDictionary(p => p.itemDef, p => ((int)p.amount)));
                    }
                }
            }
        }
        #endregion

        #region MESSAGES

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "buildingBlocked", "Строительство в BuildingBlocked запрещено!"},
            { "waterLevel", "Строительство глубже {0} метров под водой запрещено!"},
            { "heightLevel", "Строительство выше {0} метров запрещено!" },
            { "iceBlock", "Строительство на айсбергах запрещено!" },
            { "roadBlock", "Строительство на дорогах запрещено!" },
            { "treeBlock", "Строительство рядом с деревьями в радиусе {0}м. запрещено" },
            { "StackFoundation","Стакать фундаменты запрещено!"},
            { "AlreadyBuildingBuilt", "Шкаф уже стоит!" }
        };

        #endregion
    }
}
