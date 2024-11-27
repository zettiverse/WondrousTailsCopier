using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using Lumina.Excel.Sheets;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.ILayoutInstance;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLookingForGroup;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkHistory.Delegates;

namespace WondrousTailsCopier.Windows;

public class PreferredWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;
    private List<string> allPossible;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public PreferredWindow(Plugin plugin) : base("Wondrous Tails Copier Reading Preferences###PreferredWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(550, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;
        allPossible = AllPossible();
    }
    private List<string> AllPossible()
    {
        // Row IDs
        // 4 = Trials
        // 5 = Normal and Alliance Raids
        List<string> allPossible = new List<string>();
        var territories = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(r => r.ContentType.Value.RowId == 4)
            .Select(r => r.TerritoryType.Value)
            .ToHashSet();

        foreach (var territory in territories)
        {
            var addThis = false;
            var location = territory.ContentFinderCondition.Value.Name.ToString();

            if (location.Contains("Extreme"))
            {
                var pattern = @"(?:the )?(.*) \(Extreme\)";
                var r = new Regex(pattern);
                var m = r.Match(location);
                location = m.Groups[1].Value;
                if (location.Contains("Containment Bay"))
                {
                    var splitCB = location.Split(' ');
                    location = splitCB[2];
                }
                addThis = true;
            }
            else if (location.Contains("Minstrel's Ballad"))
            {
                var pattern = @"the Minstrel's Ballad: (.*)";
                var r = new Regex(pattern);
                var m = r.Match(location);
                location = m.Groups[1].Value;
                addThis = true;
            }
            if (addThis)
            {
                allPossible.Add(location);
            }
        }

        return allPossible;
    }
    public void Dispose() { }

    public override void Draw()
    {
        foreach (var objective in allPossible)
        {
            var isPreferred = Configuration.PreferredObjectives.Contains(objective);
            var isIgnored = Configuration.IgnoredObjectives.Contains(objective);

            var preferredPrefix = "";
            var ignoredPrefix = "";


            if (isPreferred)
            {
                preferredPrefix = "Un-";
            }

            if (isIgnored)
            {
                ignoredPrefix = "Un-";
            }

            ImGui.TextUnformatted(objective);
            ImGui.SameLine();
            if (ImGui.Button($"{preferredPrefix}Prefer###{objective}"))
            {
                if (isPreferred)
                {
                    Configuration.PreferredObjectives.Remove(objective);
                    Configuration.Save();
                }
                else
                {
                    Configuration.PreferredObjectives.Add(objective);
                    Configuration.Save();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"{ignoredPrefix}Ignore###{objective}"))
            {
                if (isIgnored)
                {
                    Configuration.IgnoredObjectives.Remove(objective);
                    Configuration.Save();
                }
                else
                {
                    Configuration.IgnoredObjectives.Add(objective);
                    Configuration.Save();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"Reset###{objective}"))
            {
                Configuration.PreferredObjectives.Remove(objective);
                Configuration.IgnoredObjectives.Remove(objective);
                Configuration.Save();
            }
        }
    }
}
