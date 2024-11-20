using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace WondrousTailsCopier;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public string Tester { get; set; } = "";
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool ListNumNeededBool { get; set; } = true;
    public bool ExcludeCompletedBool { get; set; } = true;
    public bool ReducedTextBool { get; set; } = true;
    public List<Dictionary<string, (string, int)>> AllBooks { get; set; } = [];
    public Dictionary<string, string> AllObjectives { get; set; } = [];

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
