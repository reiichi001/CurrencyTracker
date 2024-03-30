using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CurrencyTracker.Manager.Infos;
using CurrencyTracker.Manager.Tasks;
using CurrencyTracker.Windows;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace CurrencyTracker.Manager.Trackers.Components;

public unsafe class MoneyAddonExpand : ITrackerComponent
{
    public class Overlay : Window
    {
        private CharacterCurrencyInfo? characterCurrencyInfo;

        public Overlay() : base("MoneyAddonExpandOverlay###CurrencyTracker", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
        {
            RespectCloseHotkey = false;

            if (P.WindowSystem.Windows.Any(x => x.WindowName == WindowName))
                P.WindowSystem.RemoveWindow(P.WindowSystem.Windows.FirstOrDefault(x => x.WindowName == WindowName));
            P.WindowSystem.AddWindow(this);
        }

        public override void OnOpen()
        {
            if (Main.CharacterCurrencyInfos.Count <= 0) Main.LoadDataMCS();

            Main._isWindowOpenMCS = false;
        }

        public override void Draw()
        {
            if (!TryGetAddonByName<AtkUnitBase>("_Money", out var addon))
            {
                IsOpen = false;
                return;
            }

            var pos = new Vector2(addon->GetX(), addon->GetY() - ImGui.GetWindowSize().Y);
            ImGui.SetWindowPos(pos);

            if (EzThrottler.Throttle("MoneyAddonExpandGetMCS", 1000))
                characterCurrencyInfo = Main.CharacterCurrencyInfos.FirstOrDefault(x => x.Character.Equals(P.CurrentCharacter));
            if (characterCurrencyInfo == null) return;

            ImGui.SetWindowFontScale(1.1f);
            if (ImGui.BeginTable($"###{characterCurrencyInfo.Character.ContentID}", 2, ImGuiTableFlags.BordersInnerH))
            {
                foreach (var currency in Service.Config.AllCurrencies)
                {
                    var amount = characterCurrencyInfo.CurrencyAmount.GetValueOrDefault(currency.Key, 0);
                    if (amount == 0) continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.Image(Service.Config.AllCurrencyIcons[currency.Key].ImGuiHandle,
                                ImGuiHelpers.ScaledVector2(16.0f));

                    ImGui.SameLine();
                    ImGui.Text($"{currency.Value}  ");

                    ImGui.SameLine();
                    ImGui.Spacing();

                    ImGui.TableNextColumn();
                    ImGui.Text($"{amount:N0}  ");
                }

                ImGui.EndTable();
            }
            ImGui.SetWindowFontScale(1f);
        }
    }

    public bool Initialized { get; set; }

    private static IAddonEventHandle? mouseoverHandle;
    private static IAddonEventHandle? mouseoutHandle;

    private static Overlay? overlay;

    public void Init()
    {
        overlay = new();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_Money", OnMoneyUI);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_Money", OnMoneyUI);
    }

    private static void OnMoneyUI(AddonEvent type, AddonArgs args)
    {
        if (!EzThrottler.Throttle("MoneyAddonExpand", 1000)) return;

        if (!TryGetAddonByName<AtkUnitBase>("_Money", out var addon)) return;
        var counterNode = addon->GetNodeById(3);
        if (counterNode == null) return;

        mouseoverHandle ??= Service.AddonEventManager.AddEvent((nint)addon, (nint)counterNode, AddonEventType.MouseOver, DisplayAndHideTooltip);
        mouseoutHandle ??= Service.AddonEventManager.AddEvent((nint)addon, (nint)counterNode, AddonEventType.MouseOut, DisplayAndHideTooltip);
    }

    private static void DisplayAndHideTooltip(AddonEventType type, nint addon, nint node)
    {
        switch (type)
        {
            case AddonEventType.MouseOver:
                overlay.IsOpen = true;
                break;
            case AddonEventType.MouseOut:
                overlay.IsOpen = false;
                break;
        }
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnMoneyUI);
        if (mouseoutHandle != null)
        {
            Service.AddonEventManager.RemoveEvent(mouseoverHandle);
            mouseoutHandle = null;
        }
        if (mouseoverHandle != null)
        {
            Service.AddonEventManager.RemoveEvent(mouseoutHandle);
            mouseoverHandle = null;
        }
        

        if (overlay != null && P.WindowSystem.Windows.Contains(overlay)) P.WindowSystem.RemoveWindow(overlay);
        overlay = null;
    }
}
