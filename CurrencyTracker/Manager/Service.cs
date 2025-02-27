using System.Linq;
using CurrencyTracker.Helpers;
using CurrencyTracker.Helpers.TaskHelper;
using CurrencyTracker.Infos;
using CurrencyTracker.Manager.Trackers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LanguageManager = CurrencyTracker.Manager.Langs.LanguageManager;

namespace CurrencyTracker.Manager;

public class Service
{
    public static void Init(IDalamudPluginInterface PI)
    {
        Config = PI.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(PI);
        PI.Create<Service>();
        PluginInterface = PI;
        UiBuilder = PI.UiBuilder;

        InitExcludeServices();
    }

    public static void InitExcludeServices()
    {
        InitLanguage();
        InitCharacter();
        PresetFont.Init();

        Tracker.Init();
        CurrencyInfo.Init();
    }

    public static void Uninit()
    {
        Tracker.Dispose();
        CurrencyInfo.Uninit();
        Config.Uninit();
        TaskHelper.DisposeAll();
        LuminaCache.ClearCache();
    }

    private static void InitLanguage()
    {
        var playerLang = Config.SelectedLanguage;
        if (string.IsNullOrEmpty(playerLang))
        {
            playerLang = ClientState.ClientLanguage.ToString();
            if (LanguageManager.LanguageNames.All(x => x.Language != playerLang))
                playerLang = "English";

            Config.SelectedLanguage = playerLang;
            Config.Save();
        }

        Lang = new LanguageManager(playerLang);
    }

    private static void InitCharacter()
    {
        if (ClientState.LocalPlayer != null && ClientState.LocalContentId != 0)
            P.CurrentCharacter = P.GetCurrentCharacter();
    }

    [PluginService] public static IClientState            ClientState       { get; private set; } = null!;
    [PluginService] public static IFramework              Framework         { get; private set; } = null!;
    [PluginService] public static ICondition              Condition         { get; private set; } = null!;
    [PluginService] public static IDataManager            DataManager       { get; private set; } = null!;
    [PluginService] public static IChatGui                Chat              { get; private set; } = null!;
    [PluginService] public static ICommandManager         CommandManager    { get; set; }         = null!;
    [PluginService] public static IPluginLog              Log               { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider    Hook              { get; private set; } = null!;
    [PluginService] public static IGameGui                GameGui           { get; private set; } = null!;
    [PluginService] public static IGameInventory          GameInventory     { get; private set; } = null!;
    [PluginService] public static ITargetManager          Target            { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle         AddonLifecycle    { get; private set; } = null!;
    [PluginService] public static IAddonEventManager      AddonEventManager { get; private set; } = null!;
    [PluginService] public static ITextureProvider        TextureProvider   { get; set; }         = null!;
    [PluginService] public static IDtrBar                 DtrBar            { get; private set; } = null!;
    public static                 IDalamudPluginInterface PluginInterface   { get; private set; } = null!;
    public static                 IUiBuilder              UiBuilder         { get; private set; } = null!;
    public static                 SigScanner              SigScanner        { get; private set; } = new();
    public static                 Configuration           Config            { get; set; }         = null!;
    public static                 LanguageManager         Lang              { get; set; }         = null!;
}
