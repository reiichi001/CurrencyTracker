namespace CurrencyTracker.Manager
{
    public class Transactions
    {
        // Transactions Type Suffix:
        // Inventory - {CurrencyName}.txt
        // Retainer - {CurrencyName}_{RetainerID}.txt
        // Saddle Bag - {CurrencyName}_SB.txt
        // Premium Saddle Bag - {CurrencyName}_PSB.txt
        public enum TransactionFileCategory
        {
            Inventory = 0,
            Retainer = 1,
            SaddleBag = 2,
            PremiumSaddleBag = 3,
        }

        public static string GetTransactionFileSuffix(TransactionFileCategory category, ulong ID = 0)
        {
            var suffix = string.Empty;
            switch (category)
            {
                case TransactionFileCategory.Inventory:
                    suffix = string.Empty;
                    break;
                case TransactionFileCategory.Retainer:
                    suffix = $"_{0}";
                    break;
                case TransactionFileCategory.SaddleBag:
                    suffix = "_SB";
                    break;
                case TransactionFileCategory.PremiumSaddleBag:
                    suffix = "_PSB";
                    break;
            }
            return suffix;
        }

        // 加载全部记录 Load All Transactions
        public static List<TransactionsConvertor> LoadAllTransactions(uint currencyID)
        {
            var allTransactions = new List<TransactionsConvertor>();

            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Load All Transactions: Player Data Folder Path Missed.");
                return allTransactions;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return allTransactions;
            }

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder ?? "", $"{currencyName}.txt");

            if (!File.Exists(filePath))
            {
                return allTransactions;
            }

            try
            {
                allTransactions = TransactionsConvertor.FromFile(filePath);
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"Error Loding All Transactionsa from the data file: {ex.Message}");
            }

            return allTransactions;
        }

        // 以列表形式加载最新一条记录 Load Latest Transaction in the Form of List
        public static List<TransactionsConvertor> LoadLatestTransaction(uint currencyID)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Load Lastest Transaction: Player Data Folder Path Missed.");
                return new();
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return new();
            }

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder, $"{currencyName}.txt");

            var allTransactions = TransactionsConvertor.FromFile(filePath);

            var latestTransactions = new List<TransactionsConvertor>();

            if (allTransactions.Count > 0)
            {
                var latestTransaction = allTransactions.Last();
                latestTransactions.Add(latestTransaction);
            }
            else
            {
                var defaultTransaction = new TransactionsConvertor
                {
                    TimeStamp = DateTime.Now,
                    Amount = 0,
                    Change = 0,
                    LocationName = Service.Lang.GetText("UnknownLocation")
                };
                latestTransactions.Add(defaultTransaction);
            }

            return latestTransactions;
        }

        // 加载最新一条记录 Load Latest Transaction
        public static TransactionsConvertor? LoadLatestSingleTransaction(uint currencyID, CharacterInfo? characterInfo = null)
        {
            var playerDataFolder = string.Empty;

            if (characterInfo != null)
            {
                playerDataFolder = Path.Join(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, $"{characterInfo.Name}_{characterInfo.Server}");
            }
            else
            {
                playerDataFolder = Plugin.Instance.PlayerDataFolder;
                if (playerDataFolder.IsNullOrEmpty())
                {
                    Service.Log.Warning("Fail to Load Lastest Single Transaction: Player Data Folder Path Missed.");
                    return null;
                }
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return null;
            }

            var filePath = Path.Combine(playerDataFolder, $"{currencyName}.txt");

            if (!File.Exists(filePath))
            {
                return null;
            }

            var lastLine = File.ReadLines(filePath).LastOrDefault();

            return lastLine == null ? new() : TransactionsConvertor.FromFileLine(lastLine);
        }

        // 加载指定范围内的记录 Load Transactions in Specific Range
        public static List<TransactionsConvertor> LoadTransactionsInRange(uint currencyID, int startIndex, int endIndex)
        {
            var allTransactions = LoadAllTransactions(currencyID);

            if (startIndex < 0 || startIndex >= allTransactions.Count || endIndex < 0 || endIndex >= allTransactions.Count)
            {
                throw new ArgumentException("Invalid index range.");
            }

            var transactionsInRange = new List<TransactionsConvertor>();
            for (var i = startIndex; i <= endIndex; i++)
            {
                transactionsInRange.Add(allTransactions[i]);
            }

            return transactionsInRange;
        }

        // 删除最新一条记录 Delete Latest Transaction
        public static bool DeleteLatestTransaction(uint currencyID)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty() || !Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Warning("Fail to Delete Lastest Single Transaction: Player Data Folder Path Missed.");
                return false;
            }

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder ?? "", $"{currencyName}.txt");

            if (!File.Exists(filePath))
            {
                return false;
            }

            var tempFile = Path.GetTempFileName();
            using (var reader = new StreamReader(filePath))
            using (var writer = new StreamWriter(tempFile))
            {
                string? line;
                string? lastLine = null;

                while ((line = reader.ReadLine()) != null)
                {
                    if (lastLine != null)
                        writer.WriteLine(lastLine);

                    lastLine = line;
                }
            }

            File.Delete(filePath);
            File.Move(tempFile, filePath);

            return true;
        }

        // 编辑最新一条记录 Edit Latest Transaction
        public static void EditLatestTransaction(uint currencyID, string LocationName = "", string Note = "", bool forceEdit = false, uint timeout = 10, bool onlyEditEmpty = false)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Edit Transaction: Player Data Folder Path Missed.");
                return;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName) || !Plugin.Configuration.AllCurrencies.ContainsValue(currencyName))
            {
                return;
            }

            var editedTransaction = LoadLatestSingleTransaction(currencyID);

            if (editedTransaction == null || (!forceEdit && (DateTime.Now - editedTransaction.TimeStamp).TotalSeconds > timeout) || (onlyEditEmpty && !editedTransaction.Note.IsNullOrEmpty()))
            {
                return;
            }

            if (!DeleteLatestTransaction(currencyID))
            {
                return;
            }

            AppendTransaction(currencyID, editedTransaction.TimeStamp, editedTransaction.Amount, editedTransaction.Change, (LocationName.IsNullOrEmpty()) ? editedTransaction.LocationName : LocationName, (Note.IsNullOrEmpty()) ? editedTransaction.Note : Note);

            Plugin.Instance.Main.UpdateTransactions();
        }

        // 编辑全部货币最新一条记录 Edit All Currencies Latest Transaction
        public static void EditAllLatestTransaction(string LocationName = "", string Note = "", bool forceEdit = false, uint timeout = 10, bool onlyEditEmpty = false)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Edit Transaction: Player Data Folder Path Missed.");
                return;
            }

            foreach (var currency in Plugin.Configuration.AllCurrencies)
            {
                EditLatestTransaction(currency.Key, LocationName, Note, forceEdit, timeout, onlyEditEmpty);
            }
        }

        // 在数据末尾追加最新一条记录 Append One Transaction
        public static void AppendTransaction(uint currencyID, DateTime TimeStamp, long Amount, long Change, string LocationName, string Note)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Append Transaction: Player Data Folder Path Missed.");
                return;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return;
            }

            var singleTransaction = new TransactionsConvertor
            {
                TimeStamp = TimeStamp,
                Amount = Amount,
                Change = Change,
                LocationName = LocationName,
                Note = Note
            };

            var tempList = new List<TransactionsConvertor>
            {
                singleTransaction
            };

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder, $"{currencyName}.txt");
            TransactionsConvertor.AppendTransactionToFile(filePath, tempList);
        }

        // 新建一条数据记录 Create One New Transaction
        public static void AddTransaction(uint currencyID, DateTime TimeStamp, long Amount, long Change, string LocationName, string Note)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Append Transaction: Player Data Folder Path Missed.");
                return;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return;
            }

            var Transaction = new TransactionsConvertor
            {
                TimeStamp = TimeStamp,
                Amount = Amount,
                Change = Change,
                LocationName = LocationName,
                Note = Note
            };

            var tempList = new List<TransactionsConvertor>
            {
                Transaction
            };

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder ?? "", $"{currencyName}.txt");
            TransactionsConvertor.WriteTransactionsToFile(filePath, tempList);
            tempList.Clear();
        }

        // 根据时间重新排序文件内记录 Sort Transactions in File by Time
        public static void ReorderTransactions(uint currencyID)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Reorder Transactions: Player Data Folder Path Missed.");
                return;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return;
            }

            var allTransactions = LoadAllTransactions(currencyID).OrderBy(x => x.TimeStamp).ToList();

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder, $"{currencyName}.txt");

            TransactionsConvertor.WriteTransactionsToFile(filePath, allTransactions);
        }

        // 按照临界值合并记录 Merge Transactions By Threshold
        public static int MergeTransactionsByLocationAndThreshold(uint currencyID, long threshold, bool isOneWayMerge)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Merge Transactions: Player Data Folder Path Missed.");
                return 0;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return 0;
            }

            var allTransactions = LoadAllTransactions(currencyID);

            if (allTransactions.Count <= 1)
            {
                return 0;
            }

            var mergedTransactions = new List<TransactionsConvertor>();
            var mergedCount = 0;

            for (var i = 0; i < allTransactions.Count;)
            {
                var currentTransaction = allTransactions[i];
                var seperateMergedCount = 0;

                while (++i < allTransactions.Count &&
                    currentTransaction.LocationName == allTransactions[i].LocationName &&
                    Math.Abs(allTransactions[i].Change) < threshold)
                {
                    var nextTransaction = allTransactions[i];

                    if (!isOneWayMerge || (isOneWayMerge &&
                        (currentTransaction.Change >= 0 && nextTransaction.Change >= 0) ||
                        (currentTransaction.Change < 0 && nextTransaction.Change < 0)))
                    {
                        if (nextTransaction.TimeStamp > currentTransaction.TimeStamp)
                        {
                            currentTransaction.Amount = nextTransaction.Amount;
                            currentTransaction.TimeStamp = nextTransaction.TimeStamp;
                        }
                        currentTransaction.Change += nextTransaction.Change;

                        mergedCount += 2;
                        seperateMergedCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (seperateMergedCount > 0)
                {
                    currentTransaction.Note = $"({Service.Lang.GetText("MergedSpecificHelp", seperateMergedCount + 1)})";
                }

                mergedTransactions.Add(currentTransaction);
            }

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder ?? "", $"{currencyName}.txt");
            TransactionsConvertor.WriteTransactionsToFile(filePath, mergedTransactions);

            return mergedCount;
        }

        // 合并特定的记录 Merge Specific Transactions
        public static int MergeSpecificTransactions(uint currencyID, string LocationName, List<TransactionsConvertor> selectedTransactions, string NoteContent = "-1")
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Merge Transactions: Player Data Folder Path Missed.");
                return 0;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return 0;
            }

            var allTransactions = LoadAllTransactions(currencyID);

            if (allTransactions.Count <= 1)
            {
                return 0;
            }

            var latestTime = DateTime.MinValue;
            long overallChange = 0;
            long finalAmount = 0;
            var mergedCount = 0;

            foreach (var transaction in selectedTransactions)
            {
                var foundTransaction = allTransactions.FirstOrDefault(t => IsTransactionEqual(t, transaction));

                if (foundTransaction == null)
                {
                    continue;
                }

                if (latestTime < foundTransaction.TimeStamp)
                {
                    latestTime = foundTransaction.TimeStamp;
                    finalAmount = foundTransaction.Amount;
                }

                overallChange += foundTransaction.Change;
                allTransactions.Remove(foundTransaction);
                mergedCount++;
            }

            var finalTransaction = new TransactionsConvertor
            {
                TimeStamp = latestTime,
                Change = overallChange,
                LocationName = LocationName,
                Amount = finalAmount,
                Note = NoteContent != "-1" ? NoteContent : $"({Service.Lang.GetText("MergedSpecificHelp", mergedCount)})"
            };

            allTransactions.Add(finalTransaction);

            var filePath = Path.Combine(Plugin.Instance.PlayerDataFolder, $"{currencyName}.txt");
            TransactionsConvertor.WriteTransactionsToFile(filePath, allTransactions);

            return mergedCount;
        }

        // 清除异常记录 Clear Exceptional Records
        public static int ClearExceptionRecords(uint currencyID)
        {
            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Clear Transactions: Player Data Folder Path Missed.");
                return 0;
            }

            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return 0;
            }

            var filePath = Path.Join(Plugin.Instance.PlayerDataFolder, $"{currencyName}.txt");

            var allTransactions = TransactionsConvertor.FromFile(filePath);

            var initialCount = allTransactions.Count;
            var index = 0;

            allTransactions.RemoveAll(transaction =>
                (index++ != 0 && transaction.Change == transaction.Amount) || transaction.Change == 0);

            if (allTransactions.Count != initialCount)
            {
                TransactionsConvertor.WriteTransactionsToFile(filePath, allTransactions);
                return initialCount - allTransactions.Count;
            }
            else
            {
                return 0;
            }
        }

        // 导出数据 Export Transactions Data
        public static string ExportData(List<TransactionsConvertor> data, string fileName, uint currencyID, int exportType)
        {
            if (!Plugin.Configuration.AllCurrencies.TryGetValue(currencyID, out var currencyName))
            {
                Service.Log.Error("Currency Missed");
                return string.Empty;
            }

            string fileExtension;
            string headers;
            string lineTemplate;

            if (exportType == 0)
            {
                fileExtension = "csv";
                headers = Service.Lang.GetText("ExportFileCSVHeader");
                lineTemplate = "{0},{1},{2},{3},{4}";
            }
            else if (exportType == 1)
            {
                fileExtension = "md";
                headers = $"{Service.Lang.GetText("ExportFileMDHeader")} {currencyName}\n\n" +
                          $"{Service.Lang.GetText("ExportFileMDHeader1")}";
                lineTemplate = "| {0} | {1} | {2} | {3} | {4} |";
            }
            else
            {
                Service.Chat.PrintError(Service.Lang.GetText("ExportFileHelp2"));
                return "Fail";
            }

            if (Plugin.Instance.PlayerDataFolder.IsNullOrEmpty())
            {
                Service.Log.Warning("Fail to Export Transactions: Player Data Folder Path Missed.");
                return "Fail";
            }

            var playerDataFolder = Path.Combine(Plugin.Instance.PlayerDataFolder, "Exported");
            if (!Directory.Exists(playerDataFolder))
            {
                Directory.CreateDirectory(playerDataFolder);
            }

            var nowTime = DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss");

            string finalFileName = string.IsNullOrWhiteSpace(fileName)
                ? $"{currencyName}_{nowTime}.{fileExtension}"
                : $"{fileName}_{currencyName}_{nowTime}.{fileExtension}";

            var filePath = Path.Combine(playerDataFolder, finalFileName);

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine(headers);

                foreach (var transaction in data)
                {
                    var line = string.Format(lineTemplate, transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), transaction.Amount, transaction.Change, transaction.LocationName, transaction.Note);
                    writer.WriteLine(line);
                }
            }
            return filePath;
        }
    }
}
