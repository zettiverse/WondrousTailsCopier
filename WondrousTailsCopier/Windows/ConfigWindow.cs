using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace WondrousTailsCopier.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Wondrous Tails Copier Configuration###ConfigWindow",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 255),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        /*
            public bool ListNumNeededBool { get; set; } = true;
            public bool ExcludeCompletedBool { get; set; } = true;
            public bool ReducedTextBool { get; set; } = true;
        */

        /*
        // can't ref a property, so use a local copy
        var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }
        */

        var autoResizeBookClubValue = Configuration.AutoResizeBookClubBool;
        if (ImGui.Checkbox("Auto-resize Book Club Window", ref autoResizeBookClubValue))
        {
            Configuration.AutoResizeBookClubBool = autoResizeBookClubValue;
            Configuration.Save();
        }

        var reducedTextValue = Configuration.ReducedTextBool;
        if (ImGui.Checkbox("Use Reduced Text", ref reducedTextValue))
        {
            Configuration.ReducedTextBool = reducedTextValue;
            Configuration.Save();
            Plugin.RecalculatePreferred();
        }

        var excludeCompletedValue = Configuration.ExcludeCompletedBool;
        if (ImGui.Checkbox("Exclude Completed from List", ref excludeCompletedValue))
        {
            Configuration.ExcludeCompletedBool = excludeCompletedValue;
            Configuration.Save();
        }

        var ListNumNeededValue = Configuration.ListNumNeededBool;
        if (ImGui.Checkbox("List Number of Objectives Needed", ref ListNumNeededValue))
        {
            Configuration.ListNumNeededBool = ListNumNeededValue;
            Configuration.Save();
        }

        if (ImGui.Button("Open Preferred Objectives Window"))
        {
            Plugin.TogglePreferredUI();
        }
    }
}
