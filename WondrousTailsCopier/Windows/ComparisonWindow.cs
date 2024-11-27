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

public class ComparisonWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;
    private List<UInt32> playerColors;
    private List<string> trials;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ComparisonWindow(Plugin plugin) : base("Wondrous Tails Book Club###ComparisonWindow", 
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(550, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;

        // UInt32 colors are reversed: 0xAABBGGRR
        playerColors = new List<UInt32>() 
        {
            0xFFFCAF64,
            0xFF91FEFF,
            0xFF7C7CFC,
            0xFFF52B73,
            0xFF8EFBA1,
            0xFFF88AEE,
            0xFF8036EA,
            0xFFF52300,
        };

        trials = GetTrials();
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (Configuration.AutoResizeBookClubBool)
        {
            Flags |= ImGuiWindowFlags.AlwaysAutoResize; 
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
        }
    }
    private void Redraw()
    {
        Toggle();
        Toggle();
    }
    private List<string> GetTrials()
    {
        List<string> trials = new List<string>();
        var territories = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(r => r.ContentType.Value.RowId == 4)
            .Select(r => r.TerritoryType.Value)
            .ToHashSet();

        foreach (var territory in territories)
        {
            trials.Add(territory.ContentFinderCondition.Value.Name.ToString());
        }

        return trials;
    }
    private void RestoreOriginalNeeded()
    {
        var allBooks = Configuration.AllBooks;

        for (var i = 0; i < allBooks.Count; i++)
        {            
            foreach (var book in allBooks[i])
            {
                var pattern = @"need (\d)$";
                var r = new Regex(pattern);
                var m = r.Match(book.Value.Item1);
                if (m.Success)
                {
                    var bookValues = book.Value;
                    bookValues.Item2 = int.Parse(m.Groups[1].Value);
                    allBooks[i][book.Key] = bookValues;
                }
            }
        }
        Configuration.Save();
    }
    private void ResetCompleted()
    {
        Configuration.CompletedObjectives = [];
        RestoreOriginalNeeded();
        Configuration.Save();
    }
    private void ResetAll()
    {
        Configuration.AllBooks = [];
        Configuration.AllObjectives = [];
        ResetCompleted();
        Configuration.Save();
    }
    private string PadNumbers(string input)
    {
        return Regex.Replace(input, "^[0-9]+", match => match.Value.PadLeft(10, '0'));
    }
    private void RemoveLines(string objectiveName)
    {
        var completedObjectives = Configuration.CompletedObjectives;
        if (completedObjectives.ContainsKey(objectiveName))
        {
            if (completedObjectives[objectiveName] - 1 >= 0)
            {
                completedObjectives[objectiveName]--;
            }
        }
        Configuration.CompletedObjectives = completedObjectives;
        Configuration.Save();
    }
    private void AddLines(string objectiveName)
    {
        var completedObjectives = Configuration.CompletedObjectives;
        if (completedObjectives.ContainsKey(objectiveName))
        {
            completedObjectives[objectiveName]++;
        }
        else
        {
            completedObjectives.Add(objectiveName, 1);
        }
        Configuration.CompletedObjectives = completedObjectives;
        Configuration.Save();
    }
    private void DrawLines(Vector2 min, Vector2 max, int count)
    {
        if (count == 0)
        {
            return;
        }

        var increments = (max.Y - min.Y) / (count + 1);
        var thickness = 3.0f;

        if (count >= 4)
        {
            thickness = 1.5f;
        }
        
        for (var i = 0; i < count; i++)
        {
            min.Y += increments;
            max.Y = min.Y;
            ImGui.GetWindowDrawList().AddLine(min, max, 0xFFFFFFFF, thickness);
        }
    }
    private void AddNeededViaPlayerName(int index)
    {
        var allBooks = Configuration.AllBooks;
        foreach (var book in allBooks[index])
        {
            var bookValues = book.Value;
            if (bookValues.Item2 + 1 < 10)
            {
                bookValues.Item2++;
            }
            allBooks[index][book.Key] = bookValues;
        }
        Configuration.Save();
    }
    private void SubtractNeededViaPlayerName(int index)
    {
        var allBooks = Configuration.AllBooks;
        foreach (var book in allBooks[index])
        {
            var bookValues = book.Value;
            if (bookValues.Item2 - 1 >= 0)
            {
                bookValues.Item2--;
            }
            allBooks[index][book.Key] = bookValues;
        }
        Configuration.Save();
    }
    private void AddNeededViaObjective(string[] ids)
    {
        var allBooks = Configuration.AllBooks;
        foreach (var id in ids) {
            foreach (var book in allBooks[int.Parse(id)])
            {
                var bookValues = book.Value;
                if (bookValues.Item2 + 1 < 10)
                {
                    bookValues.Item2++;
                }
                allBooks[int.Parse(id)][book.Key] = bookValues;
            }
        }
        Configuration.Save();
    }
    private void SubtractNeededViaObjective(string[] ids)
    {
        var allBooks = Configuration.AllBooks;
        foreach (var id in ids) {
            foreach (var book in allBooks[int.Parse(id)])
            {
                var bookValues = book.Value;
                if (bookValues.Item2 - 1 >= 0)
                {
                    bookValues.Item2--;
                }
                allBooks[int.Parse(id)][book.Key] = bookValues;
            }
        }
        Configuration.Save();
    }
    private void DisplayObjectives(List<Dictionary<string, string>> categorizedObjectives)
    {
        var allBooks = Configuration.AllBooks;
        var completedObjectives = Configuration.CompletedObjectives;
        var ignoredObjectives = new List<string>();

        for (var i = 0; i < allBooks.Count; i++)
        {
            foreach (var book in allBooks[i])
            {
                var playerName = book.Key;
                ImGui.Button($"{playerName} ({book.Value.Item2.ToString()})");
                //if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyShift)
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    SubtractNeededViaPlayerName(i);
                }

                //if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    AddNeededViaPlayerName(i);
                }

                //if (ImGui.IsItemHovered())
                //{
                //    ImGui.SetTooltip("Shift + left/right click to subtract/add \"needed objectives\" value, respectively.");
                //}

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                ImGui.GetWindowDrawList().AddLine(min, max, playerColors[i], 8.0f);

                ImGui.SameLine();
            }
        }

        ImGui.Text(" ");
        ImGui.Text(" ");

        // foreach (var objectiveDict in categorizedObjectives)
        foreach (var (objectiveDict, i) in categorizedObjectives.Select((objectiveDict, i) => (objectiveDict, i)))
        {
            //var sortedObjectiveDict = new SortedDictionary<string, string>(objectiveDict);
            var sortedObjectiveDict = new Dictionary<string, string>(objectiveDict);
            //Plugin.Chat.Print(i.ToString());

            if (i == 0)
            {
                // Leveling Dungeons
                sortedObjectiveDict = objectiveDict.OrderBy(pair => PadNumbers(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
            }
            else if (i == categorizedObjectives.Count - 1)
            {
                // Misc objectives
                sortedObjectiveDict = objectiveDict.OrderBy(pair => pair.Key.Length).ToDictionary(pair => pair.Key, pair => pair.Value);
            }
            else
            {
                // All other objectives
                sortedObjectiveDict = objectiveDict.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            foreach (var objective in sortedObjectiveDict)
            {
                // If on ignore list, continue to next
                if (Configuration.IgnoredObjectives.Contains(objective.Key))
                {
                    ignoredObjectives.Add(objective.Key);
                    continue;
                }

                // Calculate if we need to wrap the button to next line or keep on the same
                var lastButtonMin = ImGui.GetItemRectMin();
                var lastButtonMax = ImGui.GetItemRectMax();
                var nextWordSize = ImGui.CalcTextSize(objective.Key);
                var wrapToNext = false;

                if (ImGui.GetContentRegionAvail().X > (lastButtonMax.X - lastButtonMin.X) + nextWordSize.X)
                {
                    wrapToNext = false;
                }
                else
                {
                    wrapToNext = true;
                }

                var ids = objective.Value.Split(',');

                if (ImGui.Button($"{objective.Key}"))
                {
                    AddLines(objective.Key);
                    SubtractNeededViaObjective(ids);
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && completedObjectives[objective.Key] - 1 >= 0)
                {
                    RemoveLines(objective.Key);
                    AddNeededViaObjective(ids);
                }

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                for (var j = 0; j < ids.Length; j++)
                {
                    ImGui.GetWindowDrawList().AddLine(min, max, playerColors[int.Parse(ids[j])], 8.0f);
                    //min.X += 6.0f;
                    //Plugin.Chat.Print($"{min.X.ToString()}, {min.Y.ToString()} // {max.X.ToString()}, {max.Y.ToString()}");
                    min.X += ((max.X - min.X) / ids.Length);
                }

                if (completedObjectives.ContainsKey(objective.Key))
                {
                    var lineMin = ImGui.GetItemRectMin();
                    var lineMax = ImGui.GetItemRectMax();
                    var timesCompleted = completedObjectives[objective.Key];

                    DrawLines(lineMin, lineMax, timesCompleted);
                }

                if (Configuration.PreferredObjectives.Contains(objective.Key))
                {
                    var circleMin = ImGui.GetItemRectMin();
                    var circleMax = ImGui.GetItemRectMax();
                    circleMin.X = circleMax.X;

                    ImGui.GetWindowDrawList().AddCircleFilled(circleMin, 1.0f, 0xFF00F2FF);
                }

                if (!wrapToNext || Configuration.AutoResizeBookClubBool)
                {
                    ImGui.SameLine();
                }
                
            }
            ImGui.Text(" ");
            ImGui.Text(" ");
        }

        if (ignoredObjectives.Count > 0)
        {
            if (ImGui.Button($"{ignoredObjectives.Count} objectives are hidden and ignored."))
            {
                foreach (var objective in ignoredObjectives)
                {
                    ImGui.TextUnformatted(objective);
                    ImGui.SameLine();
                }
            }
        }
    }
    private void OrganizeObjectives()
    {
        var allObjectives = Configuration.AllObjectives;
        var dungeonObjectives = new Dictionary<string, string>() { };
        var allianceRaidObjectives = new Dictionary<string, string>() { };
        var normalRaidObjectives = new Dictionary<string, string>() { };
        var trialObjectives = new Dictionary<string, string>() { };
        var otherObjectives = new Dictionary<string, string>() { };

        foreach (var objective in allObjectives)
        {
            var pattern = @"^need \d";
            var r = new Regex(pattern);
            var m = r.Match(objective.Key);
            if (m.Success)
            {
                continue;
            }
            
            pattern = @"^\d+|Dungeons \(";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                dungeonObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            pattern = @" AR$|^Alliance Raids";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                allianceRaidObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            pattern = @"^FL$|^CC$|RW|^Deep Dungeons$|^Treasure|^Frontline|^Crystalline Conflict|^Rival Wings";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                otherObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            var foundTrial = false;
            foreach (var trial in trials)
            {                
                if (trial.Contains(objective.Key))
                {
                    trialObjectives.Add(objective.Key, objective.Value);
                    foundTrial = true;
                }
                if (foundTrial)
                {
                    break;
                }
            }
            if (foundTrial)
            {
                continue;
            }

            normalRaidObjectives.Add(objective.Key, objective.Value);
        }
        DisplayObjectives(new List<Dictionary<string, string>>() { dungeonObjectives, allianceRaidObjectives, normalRaidObjectives, trialObjectives, otherObjectives } );
    }
    private void CompileBooks()
    {
        var allBooks = Configuration.AllBooks;
        var allObjectives = new Dictionary<string, string>() { };

        for (var i = 0; i < allBooks.Count; i++)
        {
            foreach (var book in allBooks[i])
            {
                var playerName = book.Key;
                var playerObjectives = book.Value.Item1;
                var splitObjs = playerObjectives.Split(", ");

                foreach (var obj in splitObjs)
                {
                    if (allObjectives.ContainsKey(obj))
                    {
                        if (!allObjectives[obj].Contains(i.ToString()))
                        {
                            if (allObjectives[obj].Length > 0)
                            {
                                allObjectives[obj] += $",{i.ToString()}";
                            }
                            else
                            {
                                allObjectives[obj] = i.ToString();
                            }
                        }
                    }
                    else
                    {
                        allObjectives.Add(obj, i.ToString());
                    }
                }
            }
        }
        Configuration.AllObjectives = allObjectives;
        Configuration.Save();
    }
    private bool ParseContents(string messageContents)
    {
        var allBooks = Configuration.AllBooks;

        var pattern = @"(?>\(|<)[^a-zA-Z]{0,2}(?'name'\w+ \w+)(?>\)|>) |(?>\(|<)[^a-zA-Z]{0,2}[A-Z]{3} (?'name'\w+ \w+)(?>\)|>) ";
        //var pattern = @"(\[\d+:\d+\]\[\w+\d\]|\[\d+:\d+\]|\[\w+\d\])(\(\W?(\w+ \w+)\) |<\W?(\w+ \w+)> )((.*), need (\d))|((.*), need (\d))";
        
        //Plugin.Chat.Print(messageContents);
        var r = new Regex(pattern);
        var m = r.Match(messageContents);

        var playerName = "";
        var playerIndex = -1;

        var playerObjectives = "";
        var playerNeeded = -1;
        
        if (m.Success)
        {
            //foreach (string groupName in r.GetGroupNames())
            //{
            //    Plugin.Chat.Print($"Group: {groupName}, Value: {m.Groups[groupName].Value}");
            //}

            playerName = m.Groups["name"].Value;

            messageContents = messageContents.Substring(messageContents.IndexOf(playerName) + playerName.Length + 2);
        }
        else
        {
            playerName = Plugin.GetLocalPlayerName();
        }

        //Plugin.Chat.Print(playerName);

        pattern = @"(?'objectives'.*), need (?'numNeeded'\d)";

        r = new Regex(pattern);
        m = r.Match(messageContents);

        if (m.Success)
        {
            //foreach (string groupName in r.GetGroupNames())
            //{
            //    Plugin.Chat.Print($"Group: {groupName}, Value: {m.Groups[groupName].Value}");
            //}

            playerObjectives = m.Groups["objectives"].Value;
            playerNeeded = int.Parse(m.Groups["numNeeded"].Value);


            for (var i = 0; i < allBooks.Count; i++)
            {
                if (allBooks[i].ContainsKey(playerName))
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex > -1)
            {
                allBooks[playerIndex][playerName] = (playerObjectives, playerNeeded);
            }
            else
            {
                allBooks.Add(new Dictionary<string, (string, int)> { { playerName, (playerObjectives, playerNeeded) } });
            }

            Configuration.AllBooks = allBooks;
            Configuration.Save();

            return true;
        }

        return false;
    }

    public override void Draw()
    {
        var allBooks = Configuration.AllBooks;

        if (ImGui.Button("Import From Clipboard"))
        {
            if (ParseContents(ImGui.GetClipboardText().Trim()))
            {
                CompileBooks();
                OrganizeObjectives();
            }
            else
            {
                Plugin.Chat.Print("Could not correctly parse contents of clipboard.");
            }

        }
        ImGui.SameLine();
        if (ImGui.Button("Import Your Book"))
        {
            if (ParseContents(Plugin.GetWTNames(", ")))
            {
                CompileBooks();
                OrganizeObjectives();
            }
            else
            {
                Plugin.Chat.Print("Could not correctly parse contents of clipboard.");
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Remove All"))
        {
            ResetAll();
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset Needed"))
        {
            ResetCompleted();
        }
        ImGui.SameLine();
        if (ImGui.Button($"{(Plugin.ChatListenerEnabled ? "Stop" : "Start")} Listening for Books"))
        {
            Plugin.ChatListenerEnabled = !Plugin.ChatListenerEnabled;
            Plugin.ChatListenerLimit = -1;

            if (Plugin.ChatListenerEnabled)
            {
                Plugin.Chat.Print("Listening for books...");
            }
            else
            {
                Plugin.Chat.Print("No longer listening for books.");
            }
        }

        if (allBooks != null)
        {
            OrganizeObjectives();
        }
        else
        {
            ResetAll();
        }

        if (Plugin.ChatListenerEnabled && Plugin.ChatListenerNewMessage)
        {
            Plugin.ChatListenerNewMessage = false;
            if (ParseContents($"[00:00]({Plugin.ChatListenerUser}) {Plugin.ChatListenerMessage}"))
            {
                CompileBooks();
                OrganizeObjectives();
                Plugin.ChatListenerLimit--;

                if (Plugin.ChatListenerLimit == 0)
                {
                    Plugin.ChatListenerEnabled = false;
                    Plugin.Chat.Print("Listener limit reached. No longer listening for books.");
                }
            }
        }
    }
}
