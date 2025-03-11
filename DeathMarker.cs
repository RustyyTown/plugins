// Plugin formatted by redBDGR's Plugin Formatting Tool
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Oxide.Plugins {[Info("DeathMarker", "redBDGR", "1.0.11")] [Description("Show your death location on your map.")] class DeathMarker : RustPlugin { private const string permissionName = "deathmarker.use"; private bool Changed; private float radiusSize = 0.1f; private float markerAlpha = 1f; private float markerLenght = 300f; private string colour = "yellow"; private float messageDelay = 5f; private bool sendNotificationMessage = true; private bool use3DMarker = true; private bool use3DRadius = false; private bool arrowEnabled = true; private bool ingameTextEnabled = true; private float arrowVerticalOffset = 150f; private float textVerticalOffset = 170f; private float ingameRadiusSize = 5f; private bool ingameRadiusRandomized = false; private float ingameVisualsLength = 60f; private string visualsColour = "red"; private bool useDeathConsoleDebug = false; private class MarkerInfo { public MapMarkerGenericRadius radiusMarker; public VendingMachineMapMarker vendingMarker; } private Dictionary<string, MarkerInfo> playerDic = new Dictionary<string, MarkerInfo>(); private List<MapMarker> mapMarkers = new List<MapMarker>(); private void LoadVariables() { radiusSize = Convert.ToSingle(GetConfig("Settings", "Radius Size", 2f)); markerLenght = Convert.ToSingle(GetConfig("Map Marker", "Marker Show Length", 300f)); colour = Convert.ToString(GetConfig("Map Marker", "Marker Colour1", "yellow")); sendNotificationMessage = Convert.ToBoolean(GetConfig("Settings", "Send Notification On Respawn", true)); messageDelay = Convert.ToSingle(GetConfig("Settings", "Notification Message Delay", 5f)); useDeathConsoleDebug = Convert.ToBoolean(GetConfig("Settings", "Use Console Death Debug", false)); use3DMarker = Convert.ToBoolean(GetConfig("In-Game Visuals", "Use In-Game Visuals", true)); use3DRadius = Convert.ToBoolean(GetConfig("In-Game Visuals", "Use Radius Sphere", true)); ingameRadiusSize = Convert.ToSingle(GetConfig("In-Game Visuals", "Sphere Radius", 5f)); ingameRadiusRandomized = Convert.ToBoolean(GetConfig("In-Game Visuals", "Radius Randomised Offset", false)); ingameVisualsLength = Convert.ToSingle(GetConfig("In-Game Visuals", "Visuals Length", 60f)); arrowEnabled = Convert.ToBoolean(GetConfig("In-Game Visuals", "Arrow enabled", true)); ingameTextEnabled = Convert.ToBoolean(GetConfig("In-Game Visuals", "Text Enabeld", true)); arrowVerticalOffset = Convert.ToSingle(GetConfig("In-Game Visuals", "Arrow Vertical Offset", 150f)); textVerticalOffset = Convert.ToSingle(GetConfig("In-Game Visuals", "Text Vertical Offset", 170f)); if (!Changed) return; SaveConfig(); Changed = false; } protected override void LoadDefaultConfig() { Config.Clear(); LoadVariables(); } private void Init() { LoadVariables(); permission.RegisterPermission(permissionName, this); Unsubscribe(nameof(CanNetworkTo)); lang.RegisterMessages(new Dictionary<string, string> { ["Chat Notification"] = "You can see your last death location on your map", ["Marker Title"] = "You died here", }, this); } private void Unload() { foreach (var entry in playerDic) { entry.Value.radiusMarker?.Kill(); entry.Value.vendingMarker?.Kill(); } } private object CanNetworkTo(BaseNetworkable entity, BasePlayer target) { if (!entity.GetComponent<MapMarker>()) return null; MapMarkerGenericRadius radius = entity.GetComponent<MapMarkerGenericRadius>(); if (radius) { if (!mapMarkers.Contains(radius)) return null; MarkerInfo info; if (!playerDic.TryGetValue(target.UserIDString, out info)) return false; return radius == info.radiusMarker; } VendingMachineMapMarker vending = entity.GetComponent<VendingMachineMapMarker>(); if (vending) { if (vending.server_vendingMachine) return null; if (!mapMarkers.Contains(vending)) return null; MarkerInfo info; if (!playerDic.TryGetValue(target.UserIDString, out info)) return false; return vending == info.vendingMarker; } return null; } private object OnPlayerDie(BasePlayer player, HitInfo info) { if (!permission.UserHasPermission(player.UserIDString, permissionName)) return null; MarkerInfo markerInfo; if (playerDic.TryGetValue(player.UserIDString, out markerInfo)) { markerInfo.radiusMarker?.Kill(); markerInfo.vendingMarker?.Kill(); playerDic.Remove(player.UserIDString); } if (useDeathConsoleDebug) Puts($"Death Debug: {player.displayName} ({player.UserIDString}) died at ({player.transform.position.x}, {player.transform.position.y}, {player.transform.position.z})"); MapMarkerGenericRadius radiusMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", player.transform.position).GetComponent<MapMarkerGenericRadius>(); VendingMachineMapMarker vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", player.transform.position).GetComponent<VendingMachineMapMarker>(); radiusMarker.radius = radiusSize; radiusMarker.color1 = ConvertColourString(colour); radiusMarker.alpha = markerAlpha; radiusMarker.enabled = true; Subscribe(nameof(CanNetworkTo)); playerDic.Add(player.UserIDString, new MarkerInfo { radiusMarker = radiusMarker, vendingMarker = vendingMarker }); vendingMarker.markerShopName = msg("Marker Title"); mapMarkers.Add(vendingMarker); vendingMarker.Spawn(); vendingMarker.enabled = false; mapMarkers.Add(radiusMarker); radiusMarker.Spawn(); radiusMarker.SendUpdate(); string userID = player.UserIDString; timer.Once(markerLenght, () => { if (radiusMarker != null) { if (mapMarkers.Contains(radiusMarker)) mapMarkers.Remove(radiusMarker); radiusMarker.Kill(); } if (vendingMarker != null) { if (mapMarkers.Contains(vendingMarker)) mapMarkers.Remove(vendingMarker); vendingMarker.Kill(); } if (playerDic.ContainsKey(userID)) playerDic.Remove(userID); if (playerDic.Count == 0) Unsubscribe(nameof(CanNetworkTo)); }); return null; } private void OnPlayerRespawned(BasePlayer player) { if (sendNotificationMessage) if (permission.UserHasPermission(player.UserIDString, permissionName)) timer.Once(messageDelay, () => { if (player) player.ChatMessage(msg("Chat Notification")); }); MarkerInfo info = null; if (!playerDic.TryGetValue(player.UserIDString, out info)) return; if (use3DMarker) { if (player.IsAdmin) { if (arrowEnabled) player.SendConsoleCommand("ddraw.arrow", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(0, arrowVerticalOffset, 0), info.radiusMarker.transform.position + new Vector3(0, 5f, 0), 2f); if (ingameTextEnabled) player.SendConsoleCommand("ddraw.text", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(0, textVerticalOffset, 0), msg("Marker Title")); } else { player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true); player.SendNetworkUpdateImmediate(); if (arrowEnabled) player.SendConsoleCommand("ddraw.arrow", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(0, arrowVerticalOffset, 0), info.radiusMarker.transform.position + new Vector3(0, 5f, 0), 2f); if (ingameTextEnabled) player.SendConsoleCommand("ddraw.text", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(0, textVerticalOffset, 0), msg("Marker Title")); player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false); player.SendNetworkUpdateImmediate(); } } if (use3DRadius) { if (player.IsAdmin) { if (ingameRadiusRandomized) player.SendConsoleCommand("ddraw.sphere", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(UnityEngine.Random.Range(-ingameRadiusSize, ingameRadiusSize), 0, UnityEngine.Random.Range(-ingameRadiusSize, ingameRadiusSize)), ingameRadiusSize); else player.SendConsoleCommand("ddraw.sphere", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position, ingameRadiusSize); } else { player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true); player.SendNetworkUpdate(); if (ingameRadiusRandomized) player.SendConsoleCommand("ddraw.sphere", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position + new Vector3(UnityEngine.Random.Range(-ingameRadiusSize, ingameRadiusSize), 0, UnityEngine.Random.Range(-ingameRadiusSize, ingameRadiusSize)), ingameRadiusSize); else player.SendConsoleCommand("ddraw.sphere", ingameVisualsLength, ConvertColourString(visualsColour), info.radiusMarker.transform.position, ingameRadiusSize); player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false); player.SendNetworkUpdate(); } } } private static Color ConvertColourString(string colourString) { switch (colourString.ToLower()) { case "red": return Color.red; case "blue": return Color.blue; case "green": return Color.green; case "black": return Color.black; case "clear": return Color.clear; case "cyan": return Color.cyan; case "gray": return Color.gray; case "grey": return Color.grey; case "magenta": return Color.magenta; case "white": return Color.white; case "yellow": return Color.yellow; } return Color.red; } private object GetConfig(string menu, string datavalue, object defaultValue) { var data = Config[menu] as Dictionary<string, object>; if (data == null) { data = new Dictionary<string, object>(); Config[menu] = data; Changed = true; } object value; if (!data.TryGetValue(datavalue, out value)) { value = defaultValue; data[datavalue] = value; Changed = true; } return value; } private string msg(string key, string id = null) => lang.GetMessage(key, this, id); } }
// If you wish to view this code in a formatted state, please contact redBDGR on discord @ redBDGR #0001
