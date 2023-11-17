using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System;
using System.Linq;

namespace CurrencyTracker.Manager.Trackers
{
    public partial class Tracker : IDisposable
    {
        // (人为触发)发现货币发生改变时触发的事件
        public virtual void OnTransactionsUpdate(EventArgs e)
        {
            OnCurrencyChanged?.Invoke(this, e);
        }

        // 收到新聊天信息时触发的事件
        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            var chatmessage = message.TextValue;
            var typeValue = (ushort)type;

            if (!TriggerChatTypes.Contains(typeValue)) return;

            if (DutyStarted)
            {
                DutyEndCheck(chatmessage);
                return;
            }

            if (isQuestReadyFinish)
            {
                QuestEndCheck(chatmessage);
                return;
            }

            UpdateCurrencies();

            /*

            if (Plugin.Instance.PluginInterface.IsDev)
            {
                if (!IgnoreChatTypes.Contains(typeValue))
                {
                    Service.PluginLog.Debug($"[{typeValue}]{chatmessage}");
                }
            }
            */
        }

        // 区域发生改变时触发的事件
        private void OnZoneChange(ushort sender)
        {
            if (P.PlayerDataFolder.IsNullOrEmpty())
            {
                return;
            }

            DebindChatEvent();

            currentLocationName = TerritoryNames.TryGetValue(Service.ClientState.TerritoryType, out var currentLocation) ? currentLocation : Service.Lang.GetText("UnknownLocation");

            if (C.TrackedInDuty)
            {
                // 强制结束 Force to end
                if (!IsBoundByDuty())
                {
                    DutyEndCheck("任务结束了");
                }
                else
                {
                    DutyStartCheck();
                }
            }

            if (C.WaitExComplete)
            {
                if (isOnExchanging) isOnExchanging = false;
            }

            // 传送费用相关计算
            WarpTPEndCheck();
            TeleportCheck();

            // 无人岛相关
            IsInIslandCheck();

            Service.Chat.ChatMessage += OnChatMessage;
        }

        // 每一帧更新时触发的事件
        private void OnFrameworkUpdate(IFramework framework)
        {
            // NPC 传送 Warp Teleport
            if (isReadyWarpTP)
            {
                WarpTPCheck();
            }

            // 无人岛工房 Island Workshop
            if (isInIsland)
            {
                IslandHandlers();
            }

            // 九宫幻卡 Triple Triad
            if (isTTOn)
            {
                TripleTriad();
            }

            // 强制任务完成 Force Quest to End
            if (isQuestReadyFinish)
            {
                QuestEndCheck("完成了任务");
            }

            // 交换检测 Detect Exchange Completion
            if (isOnSpecialExchanging)
            {
                EndSpecialExchange();
            }
        }

        private void DebindChatEvent()
        {
            for (var i = 0; i < 5; i++)
            {
                Service.Chat.ChatMessage -= OnChatMessage;
            }
        }
    }
}
