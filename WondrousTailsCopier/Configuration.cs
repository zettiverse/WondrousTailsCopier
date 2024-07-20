using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace WondrousTailsCopier;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool ListNumNeededBool { get; set; } = true;
    public bool ExcludeCompletedBool { get; set; } = true;
    public bool ReducedTextBool { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
