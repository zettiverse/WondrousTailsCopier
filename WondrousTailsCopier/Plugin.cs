using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.Command;
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
    private ComparisonWindow ComparisonWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        // var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ComparisonWindow = new ComparisonWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ComparisonWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "See your Wondrous Tails objectives, configure to use reduced text. Use '/wtc copy' to copy straight to clipboard."
        });


        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        // PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ComparisonWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        if (args == "copy")
        {
            ToClipboard();
        }
        else if (args == "copy list")
        {
            ToClipboard("list");
        }
        else if (args == "needed")
        {
            RemainingObjectives();
        }
        else
        {
            //OhGod();
            ToggleMainUI();
        }
    }

    public string GetLocalPlayerName()
    {
        return ClientState.LocalPlayer?.Name.ToString();
    }

    public void RemainingObjectives()
    {
        var wtData = GetWTNames(forceTrue: "listNumNeeded");
        var numNeeded = wtData.Substring(wtData.Length - 1);
        Chat.Print($"You need {numNeeded} objective{(int.Parse(numNeeded) == 1 ? "" : "s")} to finish your Wondrous Tails book.");
    }
    public void ToClipboard(string displayType = "copy")
    {
        if (HasWT())
        {
            ImGui.SetClipboardText(GetWTNames(displayType));
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
    public unsafe string GetWTNames(string displayType = "copy", string forceTrue = "")
    {
        bool reducedText = Configuration.ReducedTextBool;
        bool excludeCompleted = Configuration.ExcludeCompletedBool;
        bool listNumNeeded = Configuration.ListNumNeededBool;

        if (forceTrue.Contains("reducedText"))
        {
            reducedText = true;
        }
        else if (forceTrue.Contains("excludeCompleted"))
        {
            excludeCompleted = true;
        }
        else if (forceTrue.Contains("listNumNeeded"))
        {
            listNumNeeded = true;
        }

        if (!HasWT())
        {
            Chat.Print("How'd you get here without having a Wondrous Tails book??");
            return "Missing Wondrous Tails book. Fix me.";
        }
        var tasksInWT = "";
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

            if (reducedText)
            {
                if (dutyLocation.Contains("Dungeons "))
                {
                    var pattern = @"Dungeons \(Lv\. (\d+-\d+|\d+)";
                    var r = new Regex(pattern);
                    var m = r.Match(dutyLocation);
                    dutyLocation = m.Groups[1].Value;
                }
                else if (dutyLocation.Contains("Alliance Raids"))
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
                else if (dutyLocation.Contains("of Bahamut"))
                {
                    // May be problematic if they go back to specific "Turn x" objectives
                    var pattern = @"(.*) of Bahamut";
                    var r = new Regex(pattern);
                    var m = r.Match(dutyLocation);
                    dutyLocation = m.Groups[1].Value;
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
            }

            if (displayType == "copy")
            {
                tasksInWT += $"{dutyLocation}, ";
            }
            else // if (displayType == "list")
            {
                tasksInWT += $"{dutyLocation} \n";
            }

        }
        if (listNumNeeded && displayType == "copy")
        {
            tasksInWT += $"need {9 - numCompleted}, ";
        }
        return tasksInWT.Substring(0, tasksInWT.Length - 2);

    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleComparisonUI() => ComparisonWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
