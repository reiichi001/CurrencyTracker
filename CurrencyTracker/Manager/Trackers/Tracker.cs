namespace CurrencyTracker.Manager.Trackers
{
    public class Tracker : IDisposable
    {
        public enum RecordChangeType
        {
            All = 0,
            Positive = 1,
            Negative = 2
        }

        public delegate void CurrencyChangedHandler(object sender, EventArgs e);
        public event CurrencyChangedHandler? OnCurrencyChanged;

        public HandlerManager HandlerManager = null!;
        public ComponentManager ComponentManager = null!;

        private Configuration? C = Plugin.Configuration;
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

        public void InitializeTracking()
        {
            HandlerManager.Init();
            ComponentManager.Init();

            CheckAllCurrencies("", "", RecordChangeType.All, 0);
            Service.Log.Debug("Currency Tracker Activated");
        }

        public bool CheckCurrency(uint currencyID, string locationName = "", string noteContent = "", RecordChangeType recordChangeType = 0, uint source = 0, TransactionFileCategory category = 0, ulong ID = 0)
        {
            if (!C.AllCurrencies.TryGetValue(currencyID, out var currencyName)) return false;
            if (!CheckCurrencyRules(currencyID)) return false;

            var currencyAmount = CurrencyInfo.GetCurrencyAmount(currencyID, category, ID);
            var previousAmount = CurrencyInfo.GetCurrencyAmountFromFile(currencyID, P.CurrentCharacter, category, ID);

            if (previousAmount == null && currencyAmount <= 0) return false;

            var currencyChange = currencyAmount - (previousAmount ?? 0);
            if (currencyChange == 0) return false;

            locationName = locationName.IsNullOrEmpty() ? CurrentLocationName : locationName;

            if (recordChangeType == RecordChangeType.All || (recordChangeType == RecordChangeType.Positive && currencyChange > 0) || (recordChangeType == RecordChangeType.Negative && currencyChange < 0))
            {
                if (previousAmount != null)
                {
                    Transactions.AppendTransaction(currencyID, DateTime.Now, currencyAmount, currencyChange, locationName, noteContent, category, ID);
                }
                else
                {
                    Transactions.AddTransaction(currencyID, DateTime.Now, currencyAmount, currencyAmount, locationName, noteContent, category, ID);
                }
                PostTransactionUpdate(currencyID, currencyName, source, category, ID);
                return true;
            }
            return false;
        }

        public bool CheckCurrencyRules(uint currencyID)
        {
            if (!C.CurrencyRules.TryGetValue(currencyID, out var rule))
            {
                C.CurrencyRules.Add(currencyID, rule = new());
                C.Save();
            }
            else
            {
                // 地点限制 Location Restrictions
                if (!rule.RegionRulesMode) // 黑名单 Blacklist
                {
                    if (rule.RestrictedAreas.Contains(CurrentLocationID)) return false;
                }
                else // 白名单 Whitelist
                {
                    if (!rule.RestrictedAreas.Contains(CurrentLocationID)) return false;
                }
            }

            return true;
        }

        public bool CheckCurrencies(IEnumerable<uint> currencies, string locationName = "", string noteContent = "", RecordChangeType recordChangeType = RecordChangeType.All, uint source = 0, TransactionFileCategory category = 0, ulong ID = 0)
        {
            var isChanged = false;
            foreach (var currency in currencies)
            {
                if (CheckCurrency(currency, locationName, noteContent, recordChangeType, source, category, ID)) isChanged = true;
            };
            return isChanged;
        }

        public bool CheckAllCurrencies(string locationName = "", string noteContent = "", RecordChangeType recordChangeType = RecordChangeType.All, uint source = 0, TransactionFileCategory category = 0, ulong ID = 0)
        {
            var isChanged = false;
            foreach (var currency in C.AllCurrencies)
            {
                if (CheckCurrency(currency.Key, locationName, noteContent, recordChangeType, source, category, ID)) isChanged = true;
            };
            return isChanged;
        }

        private void PostTransactionUpdate(uint currencyID, string currencyName, uint source, TransactionFileCategory category, ulong ID)
        {
            OnTransactionsUpdate(EventArgs.Empty);
            Service.Log.Debug($"{currencyName}({currencyID}) Changed: Update Transactions Data");
            if (P.PluginInterface.IsDev) Service.Log.Debug($"Source: {source}");
        }

        public virtual void OnTransactionsUpdate(EventArgs e)
        {
            OnCurrencyChanged?.Invoke(this, e);
        }

        public void UninitializeTracking()
        {
            HandlerManager.Uninit();
            ComponentManager.Uninit();

            Service.Log.Debug("Currency Tracker Deactivated");
        }

        public void Dispose()
        {
            UninitializeTracking();
        }
    }
}
