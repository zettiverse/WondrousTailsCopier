using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using ImGuiNET;

namespace WondrousTailsCopier.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("Wondrous Tails Copier##HiddenID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    private void Redraw()
    {
        Toggle();
        Toggle();
    }

    public override void Draw()
    {
        var reducedTextValue = Configuration.ReducedTextBool;
        var excludeCompletedValue = Configuration.ExcludeCompletedBool;
        var numNeededValue = Configuration.ListNumNeededBool;

        ImGui.Text("Objectives:");
        ImGui.Spacing();

        if (Plugin.HasWT())
        {
            ImGui.Text(Plugin.GetWTNames("list"));
            ImGui.Spacing();

            if (ImGui.Checkbox("Use Reduced Text", ref reducedTextValue))
            {
                Configuration.ReducedTextBool = reducedTextValue;
                Configuration.Save();
                Redraw();
            }
            if (ImGui.Checkbox("Exclude Completed Objectives", ref excludeCompletedValue))
            {
                Configuration.ExcludeCompletedBool = excludeCompletedValue;
                Configuration.Save();
                Redraw();
            }
            if (ImGui.Checkbox("List Number of Objectives Needed", ref numNeededValue))
            {
                Configuration.ListNumNeededBool = numNeededValue;
                Configuration.Save();
            }
            if (ImGui.Button("Copy to Clipboard"))
            {
                Plugin.ToClipboard();
            }
        }
        else
        {
            ImGui.Text("Get a Book!");
        }
    }
}
