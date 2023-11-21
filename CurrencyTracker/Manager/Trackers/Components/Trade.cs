using CurrencyTracker.Manager.Libs;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using System;
using System.Threading.Tasks;

namespace CurrencyTracker.Manager.Trackers
{
    public class Trade : ITrackerComponent
    {
        private bool isOnTrade = false;
        private string tradeTargetName = string.Empty;

        public Trade() 
        {
            Init();
        }

        public void Init()
        {
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Trade", StartTrade);
        }

        private void StartTrade(AddonEvent type, AddonArgs args)
        {
            if (isOnTrade) return;

            var TGUI = Service.GameGui.GetAddonByName("Trade");

            if (TGUI != nint.Zero)
            {
                isOnTrade = true;
                if (Service.TargetManager.Target != null)
                {
                    tradeTargetName = Service.TargetManager.Target.Name.TextValue;
                }

                Service.Tracker.ChatHandler.isBlocked = true;
                Service.Framework.Update += OnFrameworkUpdate;
                Service.PluginLog.Debug("Trade Starts");
            }
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (!isOnTrade)
            {
                Service.Framework.Update -= OnFrameworkUpdate;
                return;
            }

            if (Flags.OccupiedInEvent()) return;

            EndTradeHandler();
        }

        private void EndTradeHandler()
        {
            isOnTrade = false;

            Parallel.ForEach(Plugin.Instance.Configuration.AllCurrencies, currency =>
            {
                Service.Tracker.CheckCurrency(currency.Value, "", $"({Service.Lang.GetText("TradeWith", tradeTargetName)})");
            });

            tradeTargetName = string.Empty;

            Service.Tracker.ChatHandler.isBlocked = false;
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.PluginLog.Debug("Trade Ends");
        }

        public void Uninit()
        {
            isOnTrade = false;
            tradeTargetName = string.Empty;

            Service.Framework.Update -= OnFrameworkUpdate;
            Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Trade", StartTrade);
        }
    }
}
