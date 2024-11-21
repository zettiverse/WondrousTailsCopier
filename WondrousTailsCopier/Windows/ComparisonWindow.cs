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
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.ILayoutInstance;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLookingForGroup;

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
        Redraw();
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
                //ImGui.PopID();
                ImGui.SameLine();
            }
            ImGui.Text(" ");
            ImGui.Text(" ");
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
    private void ParseClipboard()
    {
        var allBooks = Configuration.AllBooks;

        var pattern = @"(\[\d+:\d+\]\[\w+\d\]|\[\d+:\d+\]|\[\w+\d\])(\(\W?(\w+ \w+)\) |<\W?(\w+ \w+)> )((.*), need (\d))|((.*), need (\d))";
        //var pattern = @"(\[\d+:\d+\]\[\w+\d\]|\[\d+:\d+\]|\[\w+\d\])(\(\W?(\w+ \w+)\) |<\W?(\w+ \w+)> )(.*), need (\d)|(.*), need (\d)";
        var clipboardContents = ImGui.GetClipboardText().Trim();
        //Plugin.Chat.Print(clipboardContents);
        var r = new Regex(pattern);
        var m = r.Match(clipboardContents);

        var playerName = "";
        var playerIndex = -1;

        var playerObjectives = "";
        var playerNeeded = -1;
        
        if (m.Success)
        {
            if (m.Groups[3].Value.Length == 0 && m.Groups[4].Value.Length == 0)
            {
                playerName = Plugin.GetLocalPlayerName();
                playerObjectives = m.Groups[8].Value;
                playerNeeded = int.Parse(m.Groups[10].Value);
                //playerNeeded = int.Parse(m.Groups[8].Value);
            }
            else
            {
                if (m.Groups[3].Value.Length == 0)
                {
                    playerName = m.Groups[4].Value;
                }
                else
                {
                    playerName = m.Groups[3].Value;
                }

                playerObjectives = m.Groups[5].Value;
                playerNeeded = int.Parse(m.Groups[7].Value);
                //playerNeeded = int.Parse(m.Groups[6].Value);
            }


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
                allBooks.Add(new Dictionary<string, (string, int)> {{ playerName, (playerObjectives, playerNeeded) } });
            }

            Configuration.AllBooks = allBooks;
            Configuration.Save();
        }
        else
        {
            Plugin.Chat.Print("Could not correctly parse contents of clipboard.");
        }
    }

    public override void Draw()
    {
        var allBooks = Configuration.AllBooks;

        if (ImGui.Button("Import From Clipboard"))
        {
            ParseClipboard();
            CompileBooks();
            OrganizeObjectives();
            Redraw();
        }
        ImGui.SameLine();
        if (ImGui.Button("Import Your Book"))
        {
            Plugin.ToClipboard(", ");
            ParseClipboard();
            CompileBooks();
            OrganizeObjectives();
            Redraw();
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

        if (allBooks != null)
        {
            OrganizeObjectives();
        }
        else
        {
            ResetAll();
        }


        /*
        ImGui.Text("Import Wondrous Tails Objectives from Clipboard");
        ImGui.Text(allBooks.Count.ToString());
        ImGui.SameLine();

        //if (ImGuiComponents.IconButton(FontAwesomeIcon.Clipboard))
        if (ImGui.Button("Import from Clipboard"))
        {
            Plugin.Chat.Print("Import from Clipboard!");

            ParseClipboard();
            CompileBooks();
            OrganizeObjectives();
            Redraw();
        }

        if (ImGui.Button("Remove all"))
        {
            Configuration.AllBooks = [];
            Configuration.AllObjectives = [];
            Configuration.Save();
            Redraw();
        }
        */
        /*
        ImGui.Text("Julianna Arashi");
        //ImGui.SameLine();
        ImGui.Button("70-90");
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        min.Y = max.Y;

        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[0], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[1], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[2], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[3], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[4], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[5], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[6], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[7], 8.0f);
        min.X -= 42.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, 0xFFFFFFFF, 8.0f);

        ImGui.SameLine();
        ImGui.Text("70-90");
        ImGui.SameLine();
        ImGui.Text("70-90");
        ImGui.SameLine();
        ImGui.Text("70-90");

        ImGui.Text("ARR AR");
        ImGui.SameLine();
        ImGui.Text("EW AR");
        
        ImGui.Text("Binding Coil");
        ImGui.SameLine();
        ImGui.Text("Eden's Promise");
        ImGui.SameLine();
        ImGui.Text("AAC Light-heavyweight M1 or M2");

        ImGui.Text("Crown of the Immaculate");
        ImGui.SameLine();
        ImGui.Text("Zodiark's Fall");
        ImGui.SameLine();
        ImGui.Text("Mount Ordeals");
        ImGui.SameLine();
        ImGui.Text("Hells Kier");

        ImGui.Text("Deep Dungeons");
        ImGui.SameLine();
        ImGui.Text("CC");
        */
    }
}
