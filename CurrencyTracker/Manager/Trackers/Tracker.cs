namespace CurrencyTracker.Manager.Trackers
{
    public partial class Tracker : IDisposable
    {
        public enum RecordChangeType
        {
            All,
            Positive,
            Negative
        }

        private static readonly ushort[] TriggerChatTypes = new ushort[]
        {
            57, 0, 2110, 2105, 62, 3006, 3001, 2238, 2622
        };

        public delegate void CurrencyChangedHandler(object sender, EventArgs e);

        public event CurrencyChangedHandler? OnCurrencyChanged;

        public HandlerManager HandlerManager = null!;
        public ComponentManager ComponentManager = null!;

        private Configuration? C = Plugin.Instance.Configuration;
        private Plugin? P = Plugin.Instance;

        public Tracker()
        {
            Init();
        }

        private void Init()
        {
            InitCurrencies();

            HandlerManager ??= new HandlerManager();
            ComponentManager ??= new ComponentManager();

            if (Service.ClientState.IsLoggedIn)
            {
                InitializeTracking();
            }
        }

        public void InitializeTracking()
        {
            HandlerManager.Init();
            ComponentManager.Init();

            CheckAllCurrencies("", "", RecordChangeType.All, 0);
            Service.Log.Debug("Currency Tracker Activated");
        }

        public void UninitializeTracking()
        {
            HandlerManager.Uninit();
            ComponentManager.Uninit();

            Service.Log.Debug("Currency Tracker Deactivated");
        }

        // (人为触发)发现货币发生改变时触发的事件
        public virtual void OnTransactionsUpdate(EventArgs e)
        {
            OnCurrencyChanged?.Invoke(this, e);
        }

        // 检查货币情况 Check the currency
        public bool CheckCurrency(uint currencyID, string locationName = "", string noteContent = "", RecordChangeType recordChangeType = RecordChangeType.All, uint source = 0)
        {
            if (!C.AllCurrencies.TryGetValue(currencyID, out var currencyName)) return false;

            var currencyAmount = CurrencyInfo.GetCurrencyAmount(currencyID);
            var latestTransaction = Transactions.LoadLatestSingleTransaction(currencyID);

            if (latestTransaction != null)
            {
                var currencyChange = currencyAmount - latestTransaction.Amount;
                if (currencyChange == 0) return false;

                locationName = locationName.IsNullOrEmpty() ? CurrentLocationName : locationName;
                if (currencyChange != 0 && (recordChangeType == RecordChangeType.All || (recordChangeType == RecordChangeType.Positive && currencyChange > 0) || (recordChangeType == RecordChangeType.Negative && currencyChange < 0)))
                {
                    Transactions.AppendTransaction(currencyID, DateTime.Now, currencyAmount, currencyChange, locationName, noteContent);
                    OnTransactionsUpdate(EventArgs.Empty);
                    Service.Log.Debug($"{currencyName}({currencyID}) Changed: Update Transactions Data");
                    if (P.PluginInterface.IsDev) Service.Log.Debug($"Source: {source}");
                    return true;
                }
            }
            else if (currencyAmount > 0 && (recordChangeType == RecordChangeType.All || recordChangeType == RecordChangeType.Positive))
            {
                Transactions.AddTransaction(currencyID, DateTime.Now, currencyAmount, currencyAmount, locationName, noteContent);
                OnTransactionsUpdate(EventArgs.Empty);
                Service.Log.Debug($"{currencyName}({currencyID}) Changed: Update Transactions Data");
                return true;
            }
            return false;
        }

        public bool CheckAllCurrencies(string locationName = "", string noteContent = "", RecordChangeType recordChangeType = RecordChangeType.All, uint source = 0)
        {
            var isChanged = false;
            foreach (var currency in C.AllCurrencies)
            {
                if (CheckCurrency(currency.Key, locationName, noteContent, recordChangeType, source)) isChanged = true;
            };
            return isChanged;
        }

        public bool CheckCurrencies(IEnumerable<uint> currencies, string locationName = "", string noteContent = "", RecordChangeType recordChangeType = RecordChangeType.All, uint source = 0)
        {
            var isChanged = false;
            foreach(var currency in C.AllCurrencies)
            {
                if (CheckCurrency(currency.Key, locationName, noteContent, recordChangeType, source)) isChanged = true;
            };
            return isChanged;
        }

        private void InitCurrencies()
        {
            foreach (var currency in CurrencyInfo.PresetCurrencies)
            {
                if (!C.PresetCurrencies.ContainsKey(currency.Key))
                {
                    var currencyName = CurrencyInfo.CurrencyLocalName(currency.Key);
                    if (!currencyName.IsNullOrEmpty())
                    {
                        C.PresetCurrencies.Add(currency.Key, currencyName);
                    }
                }
            }

            C.PresetCurrencies = C.PresetCurrencies.Where(kv => CurrencyInfo.PresetCurrencies.ContainsKey(kv.Key))
                                       .ToDictionary(kv => kv.Key, kv => kv.Value);
            C.Save();

            if (C.FisrtOpen)
            {
                foreach (var currencyID in CurrencyInfo.defaultCurrenciesToAdd)
                {
                    var currencyName = CurrencyInfo.CurrencyLocalName(currencyID);

                    if (currencyName.IsNullOrEmpty()) continue;

                    if (!C.CustomCurrencies.ContainsKey(currencyID))
                    {
                        C.CustomCurrencies.Add(currencyID, currencyName);
                    }
                }

                C.FisrtOpen = false;
                C.Save();
            }
        }

        public unsafe string GetWindowTitle(AddonArgs args, uint windowNodeID, uint[]? textNodeIDs = null)
        {
            textNodeIDs ??= new uint[] { 3, 4 };

            var UI = (AtkUnitBase*)args.Addon;

            if (UI == null || UI->RootNode == null || UI->RootNode->ChildNode == null || UI->UldManager.NodeList == null)
                return string.Empty;

            var windowNode = (AtkComponentBase*)UI->GetComponentNodeById(windowNodeID);
            if (windowNode == null)
                return string.Empty;

            // 国服和韩服特别处理逻辑 For CN and KR Client
            var bigTitle = windowNode->GetTextNodeById(textNodeIDs[0])->GetAsAtkTextNode()->NodeText.ToString();
            var smallTitle = windowNode->GetTextNodeById(textNodeIDs[1])->GetAsAtkTextNode()->NodeText.ToString();

            var windowTitle = !smallTitle.IsNullOrEmpty() ? smallTitle : bigTitle;

            return windowTitle;
        }

        public unsafe string GetWindowTitle(nint addon, uint windowNodeID, uint[]? textNodeIDs = null)
        {
            textNodeIDs ??= new uint[] { 3, 4 };

            var UI = (AtkUnitBase*)addon;

            if (UI == null || UI->RootNode == null || UI->RootNode->ChildNode == null || UI->UldManager.NodeList == null)
                return string.Empty;

            var windowNode = (AtkComponentBase*)UI->GetComponentNodeById(windowNodeID);
            if (windowNode == null)
                return string.Empty;

            // 国服和韩服特别处理逻辑 For CN and KR Client
            var textNode3 = windowNode->GetTextNodeById(textNodeIDs[0])->GetAsAtkTextNode()->NodeText.ToString();
            var textNode4 = windowNode->GetTextNodeById(textNodeIDs[1])->GetAsAtkTextNode()->NodeText.ToString();

            var windowTitle = !textNode4.IsNullOrEmpty() ? textNode4 : textNode3;

            return windowTitle;
        }

        public void Dispose()
        {
            UninitializeTracking();
        }
    }
}
