using System.Diagnostics.CodeAnalysis;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Libc;
using Dalamud.IoC;
using Dalamud.Plugin;
using CurrencyTracker.Manager;

namespace CurrencyTracker;

public class Service
{
    public static void Initialize(DalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Service>();
    [PluginService] public static ClientState ClientState { get; private set; } = null!;
    [PluginService] public static Framework Framework { get; private set; } = null!;
    [PluginService] public static Condition Condition { get; private set; } = null!;
    public static Tracker Tracker { get; set; } = null!;


}
