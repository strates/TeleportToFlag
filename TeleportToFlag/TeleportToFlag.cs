using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using System.Collections.Generic;
using System.Linq;
using System;
using Dalamud.Utility;
using System.Numerics;

namespace SamplePlugin;

public sealed class TeleportToFlag : IDalamudPlugin
{
    private DalamudPluginInterface PluginInterface { get; init; }
    private IChatGui ChatGui { get; init; }
    private IGameGui GameGui { get; init; }
    private IKeyState KeyState { get; init; }
    private IDataManager DataManager { get; init; }
    private IClientState ClientState { get; init; }
    private ICommandManager CommandManager { get; init; }

    private ExcelSheet<Aetheryte>? Aetherytes { get; init; }
    private ExcelSheet<MapMarker>? AetheryteFlags { get; init; }

    private DalamudLinkPayload teleportLinkPayload { get; init; }

    public TeleportToFlag(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] IChatGui chatGui,
        [RequiredVersion("1.0")] IGameGui gameGui,
        [RequiredVersion("1.0")] IKeyState keyState,
        [RequiredVersion("1.0")] IDataManager dataManager,
        [RequiredVersion("1.0")] IClientState clientState,
        [RequiredVersion("1.0")] ICommandManager commandManager)
    {
        PluginInterface = pluginInterface;
        ChatGui = chatGui;
        GameGui = gameGui;
        KeyState = keyState;
        DataManager = dataManager;
        ClientState = clientState;
        CommandManager = commandManager;

        Aetherytes = DataManager.GetExcelSheet<Aetheryte>(ClientState.ClientLanguage);
        AetheryteFlags = DataManager.GetExcelSheet<MapMarker>(ClientState.ClientLanguage);

        this.teleportLinkPayload = pluginInterface.AddChatLinkHandler(1, TeleportLinkAction);
        this.ChatGui.ChatMessage += OnChatMessage;
    }

    private void TeleportLinkAction(uint id, SeString message)
    {
        if (message.Payloads[1] is MapLinkPayload mapLink)
        {
            GameGui.OpenMapWithMapLink(mapLink);
            if (KeyState.GetRawValue(VirtualKey.CONTROL) == 1)
            {
                var nearestAetheryte = GetAetheryte(mapLink);
                if (nearestAetheryte != "")
                {
                    CommandManager.ProcessCommand($"/tp {nearestAetheryte}");

                }
                else
                {
                    ChatGui.Print($"Couldn't find nearest Aetheryte for ({mapLink.PlaceName} - {mapLink.XCoord},{mapLink.YCoord})");
                }

            }
        }
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        var payloads = new List<Payload>();
        var flag = false;

        foreach (var payload in message.Payloads)
        {
            if (payload is MapLinkPayload mapLink)
            {
                flag = true;
                payloads.Add(teleportLinkPayload);
                payloads.Add(mapLink);
            }
            else
            {
                payloads.Add(payload);
            }
        }

        if (flag)
        {
            message.Payloads.Clear();
            message.Payloads.AddRange(payloads);
        }
    }

    private string GetAetheryte(MapLinkPayload mapLink)
    {
        var name = "";
        double distance = 0;

        if (Aetherytes == null || AetheryteFlags == null) { return name; }
        foreach (var aetheryte in Aetherytes)
        {
            if (!aetheryte.IsAetheryte || aetheryte.Territory.Value == null || aetheryte.PlaceName.Value == null) continue;
            if (aetheryte.Territory.Value.RowId == mapLink.TerritoryType.RowId)
            {
                var aetheryteFlag = AetheryteFlags.FirstOrDefault(f => (f.DataType == 3 && f.DataKey == aetheryte.RowId));
                if (aetheryteFlag != null)
                {
                    var pos = MapUtil.WorldToMap(new Vector2(aetheryteFlag.X-1024f, aetheryteFlag.Y-1024f), aetheryte.Map.Value!);
                    var dist = Math.Pow(pos.X - mapLink.XCoord, 2) + Math.Pow(pos.Y - mapLink.YCoord, 2);
                    
                    if (name == "" || dist < distance)
                    {
                        name = aetheryte.PlaceName.Value.Name;
                        distance = dist;
                    }
                }
            }
        }
        return name;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.RemoveChatLinkHandler(1);
    }
}
