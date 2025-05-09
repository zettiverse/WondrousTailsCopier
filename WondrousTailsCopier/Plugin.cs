using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using WondrousTailsCopier.Windows;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Dalamud.Game.Text;
using static FFXIVClientStructs.FFXIV.Client.Game.UI.PublicInstance;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLookingForGroup;

namespace WondrousTailsCopier;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;

    private const string CommandName = "/wtc";
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("WondrousTailsCopier");
    private PreferredWindow PreferredWindow { get; init; }
    private ComparisonWindow ComparisonWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public string TesterString = "TBD";
    public bool ChatListenerEnabled = false;
    public string ChatListenerUser = "";
    public string ChatListenerMessage = "";
    public bool ChatListenerNewMessage = false;
    public int ChatListenerLimit = -1;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        // var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        PreferredWindow = new PreferredWindow(this);
        ComparisonWindow = new ComparisonWindow(this);
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(PreferredWindow);
        WindowSystem.AddWindow(ComparisonWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "See your Wondrous Tails objectives, configure to use reduced text. Use '/wtc copy' to copy straight to clipboard."
        });


        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Chat.ChatMessage += Chat_OnChatMessage;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ComparisonWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        Chat.ChatMessage -= Chat_OnChatMessage;
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        if (args == "copy")
        {
            ToClipboard(", ");
        }
        else if (args == "copy list")
        {
            ToClipboard("\n");
        }
        else if (args == "needed")
        {
            RemainingObjectives();
        }
        else if (args == "bc")
        {
            ToggleComparisonUI();
        }
        else if (args.StartsWith("bl"))
        {
            ChatListenerEnabled = true;
            var pattern = @"^bl (\d+)$";
            var r = new Regex(pattern);
            var m = r.Match(args);
            if (m.Success)
            {
                ChatListenerLimit = int.Parse(m.Groups[1].Value);
            }
            ComparisonWindow.IsOpen = true;
            Chat.Print("Listening for books...");
        }
        else if (args == "preferred")
        {
            TogglePreferredUI();
        }
        else
        {
            //TestFunc();
            ToggleMainUI();
        }
    }
    private void TestFunc()
    {
        List<string> trials = new List<string>();
        var territories = DataManager.GetExcelSheet<WeeklyBingoOrderData>()!
            //.Where(r => r.RowId <= 16)
            .Select(r => r.Text.Value.Description.ToString())
            .ToHashSet();

        foreach (var territory in territories)
        {
            Chat.Print(territory);
        }
    }
    private void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {      
        if (!ChatListenerEnabled)
        {
            return;
        }
        else
        {
            //TesterString = $"({sender.ToString()}) {message.TextValue}";

            var pattern = @", need (\d)$";
            var r = new Regex(pattern);
            var m = r.Match(message.TextValue);
            if (!m.Success)
            {
                return;
            }

            // From https://github.com/Haplo064/ChatBubbles/blob/main/ChatBubbles/OnChat.cs#L29
            List<char> toRemove = new()
            {
                //src: https://na.finalfantasyxiv.com/lodestone/character/10080203/blog/2891974/
                '','','','','','','','','','','','','','','','','','','','','','','','','','','','','','','','',
            };

            var sanitizedSender = sender.ToString();
            ChatListenerMessage = message.TextValue;


            foreach (var c in toRemove)
            {
                // Removes all special characters related to Party List numbering
                sanitizedSender = sanitizedSender.Replace(c.ToString(), string.Empty);
            }
            ChatListenerUser = sanitizedSender;
            ChatListenerNewMessage = true;
        }
    }
    private unsafe List<string> GetWTDutyList(bool excludeCompleted, out int numNeeded)
    {
        List<string> dutyList = [];
        var numCompleted = 0;

        foreach (var index in Enumerable.Range(0, 16))
        {
            var taskId = PlayerState.Instance()->WeeklyBingoOrderData[index];
            var bingoOrderData = DataManager.GetExcelSheet<WeeklyBingoOrderData>().GetRow(taskId);

            var bingoState = PlayerState.Instance()->GetWeeklyBingoTaskStatus(index);

            if (bingoState != PlayerState.WeeklyBingoTaskStatus.Open)
            {
                numCompleted++;
                if (excludeCompleted)
                {
                    continue;
                }
            }

            var dutyLocation = "";
            if (bingoOrderData.Type == 0)
            {
                var duty = DataManager.GetExcelSheet<ContentFinderCondition>()!
                    .Where(c => c.Content.RowId == bingoOrderData.Data.RowId)
                    .OrderBy(row => row.SortKey)
                    .Select(c => c.TerritoryType.RowId)
                    .ToList().FirstOrDefault();
                var territoryType = DataManager.GetExcelSheet<TerritoryType>()!.GetRow(duty);
                var cfc = territoryType.ContentFinderCondition.Value;
                dutyLocation = cfc.Name.ToString();
            }
            else
            {
                dutyLocation = bingoOrderData.Text.Value.Description.ToString();
            }
            dutyList.Add(dutyLocation);
        }

        numNeeded = numCompleted < 9 ? 9 - numCompleted : 0;

        return dutyList;
    }

    public string ReduceWTDutyName(string dutyLocation)
    {
        if (dutyLocation.Contains("Dungeons "))
        {
            var isHighLevelOnly = false;

            var pattern = @"Dungeons \(Lv\. (\d+-\d+|\d+ & \d+|\d+)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);

            dutyLocation = m.Groups[1].Value;

            if (dutyLocation.Contains('-'))
            {
                var levelRange = dutyLocation.Split("-");
                var lowerLevel = int.Parse(levelRange[0]);
                var higherLevel = int.Parse(levelRange[1]);

                /*
                if (lowerLevel % 10 == 0 && higherLevel % 10 == 0)
                {
                    isHighLevelOnly = true;
                }
                */
            }
            else if (dutyLocation.Contains('&'))
            {
                var levelRange = dutyLocation.Split("&");
                var lowerLevel = int.Parse(levelRange[0]);
                var higherLevel = int.Parse(levelRange[1]);

                if (lowerLevel % 10 == 0 && higherLevel % 10 == 0)
                {
                    isHighLevelOnly = true;
                }
            }
            else
            {
                if (int.Parse(dutyLocation) % 10 == 0)
                {
                    isHighLevelOnly = true;
                }
            }

            if (isHighLevelOnly)
            {
                dutyLocation = dutyLocation.Replace("&", " or ");
                dutyLocation += " Only";
            }
        }
        else if (dutyLocation.Contains("Alliance Raids"))
        {
            var pattern = @"Lv\. (\d+)-(\d+)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            //dutyLocation = "";

            if (m.Success)
            {
                var lowerLevel = int.Parse(m.Groups[1].Value);
                var higherLevel = int.Parse(m.Groups[2].Value);

                var lowerAR = "";
                var higherAR = "";

                if (lowerLevel == 50)
                {
                    lowerAR = "ARR";
                }
                else if (lowerLevel == 60)
                {
                    lowerAR = "HW";
                }
                else if (lowerLevel == 70)
                {
                    lowerAR = "StB";
                }
                else if (lowerLevel == 80)
                {
                    lowerAR = "ShB";
                }
                else if (lowerLevel == 90)
                {
                    lowerAR = "EW";
                }
                else if (lowerLevel == 100)
                {
                    lowerAR = "DT";
                }

                if (higherLevel == 50)
                {
                    higherAR = "ARR";
                }
                else if (higherLevel == 60)
                {
                    higherAR = "HW";
                }
                else if (higherLevel == 70)
                {
                    higherAR = "StB";
                }
                else if (higherLevel == 80)
                {
                    higherAR = "ShB";
                }
                else if (higherLevel == 90)
                {
                    higherAR = "EW";
                }
                else if (higherLevel == 100)
                {
                    higherAR = "DT";
                }
                dutyLocation = $"{lowerAR}-{higherAR} ARs";
            }
            else
            {
                if (dutyLocation.Contains("A Realm Reborn"))
                {
                    dutyLocation = "ARR";
                }
                else if (dutyLocation.Contains("Heavensward"))
                {
                    dutyLocation = "HW";
                }
                else if (dutyLocation.Contains("Stormblood"))
                {
                    dutyLocation = "StB";
                }
                else if (dutyLocation.Contains("Shadowbringers"))
                {
                    dutyLocation = "ShB";
                }
                else if (dutyLocation.Contains("Endwalker"))
                {
                    dutyLocation = "EW";
                }
                else if (dutyLocation.Contains("Dawntrail"))
                {
                    dutyLocation = "DT";
                }
                dutyLocation += " AR";
            }
        }
        else if (dutyLocation.Contains("of Bahamut"))
        {
            // May be problematic if they go back to specific "Turn x" objectives
            var pattern = @"(.*) of Bahamut";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            dutyLocation = m.Groups[1].Value;
        }
        else if (dutyLocation.Contains("Circles"))
        {
            var pattern = @"^A.*s: (.* Circles)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            dutyLocation = $"{m.Groups[1].Value}";
        }
        else if (dutyLocation.Contains("-heavyweight"))
        {
            var pattern = @"(AAC) \w+-heavyweight (.*)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            dutyLocation = $"{m.Groups[1].Value} {m.Groups[2].Value}";
        }
        else if (dutyLocation.Contains("Extreme"))
        {
            var pattern = @"(?:the )?(.*) \(Extreme\)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            dutyLocation = m.Groups[1].Value;
            if (dutyLocation.Contains("Containment Bay"))
            {
                var splitCB = dutyLocation.Split(' ');
                dutyLocation = splitCB[2];
            }
        }
        else if (dutyLocation.Contains("Minstrel's Ballad"))
        {
            var pattern = @"the Minstrel's Ballad: (.*)";
            var r = new Regex(pattern);
            var m = r.Match(dutyLocation);
            dutyLocation = m.Groups[1].Value;
        }
        else if (dutyLocation == "Crystalline Conflict")
        {
            dutyLocation = "CC";
        }
        else if (dutyLocation == "Frontline")
        {
            dutyLocation = "FL";
        }
        else if (dutyLocation == "Rival Wings")
        {
            dutyLocation = "RW";
        }

        return dutyLocation;
    }

    public string GetLocalPlayerName()
    {
        return ClientState.LocalPlayer?.Name.ToString();
    }

    public void RemainingObjectives()
    {
        GetWTDutyList(false, out var numNeeded);
        Chat.Print($"You need {numNeeded} objective{(numNeeded == 1 ? "" : "s")} to finish your Wondrous Tails book.");
    }
    public void ToClipboard(string separator)
    {
        if (HasWT())
        {
            ImGui.SetClipboardText(GetWTNames(separator));
            Chat.Print("Wondrous Tails objectives copied to clipboard.");
        }
        else
        {
            Chat.Print("You don't have a Wondrous Tails book!");
        }
    }
    public unsafe bool HasWT()
    {
        return PlayerState.Instance()->HasWeeklyBingoJournal;
    }
    public string GetWTNames(string separator)
    {
        var reducedText = Configuration.ReducedTextBool;
        var excludeCompleted = Configuration.ExcludeCompletedBool;
        var listNumNeeded = Configuration.ListNumNeededBool;

        if (!HasWT())
        {
            Chat.Print("How'd you get here without having a Wondrous Tails book??");
            return "Missing Wondrous Tails book. Fix me.";
        }
        var dutyList = GetWTDutyList(excludeCompleted, out int numNeeded);

        if (reducedText)
        {
            dutyList = dutyList.Select(ReduceWTDutyName).ToList();
        }
        if (listNumNeeded)
        {
            dutyList.Add($"need {numNeeded}");
        }
        return string.Join(separator, dutyList);
    }

    private void DrawUI() => WindowSystem.Draw();

    public void RecalculatePreferred() => PreferredWindow.allPossible = PreferredWindow.AllPossible();
    public void TogglePreferredUI() => PreferredWindow.Toggle();
    public void ToggleComparisonUI() => ComparisonWindow.Toggle();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
