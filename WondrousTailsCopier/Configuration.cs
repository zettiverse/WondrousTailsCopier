using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace WondrousTailsCopier;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool AutoResizeBookClubBool { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool ListNumNeededBool { get; set; } = true;
    public bool ExcludeCompletedBool { get; set; } = true;
    public bool ReducedTextBool { get; set; } = true;
    public List<Dictionary<string, (string, int)>> AllBooks { get; set; } = [];
    public Dictionary<string, string> AllObjectives { get; set; } = [];
    public Dictionary<string, int> CompletedObjectives { get; set; } = [];
    public List<string> PreferredObjectives { get; set; } = [];
    public List<string> IgnoredObjectives { get; set; } = [];

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
