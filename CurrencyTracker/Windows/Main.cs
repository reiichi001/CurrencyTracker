using CurrencyTracker.Manager;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace CurrencyTracker.Windows;

public class Main : Window, IDisposable
{
    // 图表按钮是否右对齐 If Graphs button right-aligned
    private bool graphsRightAligned = false;

    // 计时器触发间隔 Timer Trigger Interval
    private int timerInterval = 500;

    // 记录模式: 0为计时器模式, 1为聊天记录模式 Record Mode: 0 for Timer Mode, 1 for Chat Mode
    private int recordMode = 0;

    // 是否显示筛选排序选项 If Show Sort Options
    private bool showSortOptions = true;

    // 是否显示记录选项 If Show Record Options
    private bool showRecordOptions = true;

    // 是否显示其他 If Show Others
    private bool showOthers = true;

    // 时间聚类 Time Clustering
    private int clusterHour;

    // 时间聚类开关 Time Clustering Switch
    private bool isClusteredByTime;

    // 倒序排序开关 Reverse Sorting Switch
    internal bool isReversed;

    // 副本内记录开关 Duty Tracking Switch
    private bool isTrackedinDuty;

    // 收支筛选开关 Income/Expense Filter Switch
    private bool isChangeFilterEnabled;

    // 时间筛选开关 Time Filter Switch
    private bool isTimeFilterEnabled;

    // 地点筛选开关 Location Filter Switch
    private bool isLocationFilterEnabled;

    // 地点筛选名称 Locatio Filter Key
    private string? searchLocationName = string.Empty;

    // 筛选时间段的起始 Filtering Time Period
    private DateTime filterStartDate = new DateTime(DateTime.Now.Year, 1, 1);

    private DateTime filterEndDate = DateTime.Now;

    // 筛选模式：0为大于，1为小于 Filtering Mode: 0 for Above, 1 for Below
    private int filterMode;

    // 用户指定的筛选值 User-Specified Filtering Value
    private int filterValue;

    // 每页显示的交易记录数 Number of Transaction Records Displayed Per Page
    private int transactionsPerPage = 20;

    // 当前页码 Current Page Number
    private int currentPage;

    // 自定义追踪物品ID Custom Tracked Currency ID
    private uint customCurrency = uint.MaxValue;

    // CSV文件名 CSV File Name
    private string fileName = string.Empty;

    // 默认选中的选项 Default Selected Option
    internal int selectedOptionIndex = -1;

    // 选择的语言 Selected Language
    internal string playerLang = string.Empty;

    // 当前选中的货币名称 Currently Selected Currency Name
    internal string? selectedCurrencyName;

    // 搜索框值 Search Filter
    private static string searchFilter = string.Empty;

    // 合并的临界值 Merge Threshold
    private int mergeThreshold;

    // 当前页索引 Current Page Index
    private int visibleStartIndex;

    private int visibleEndIndex;

    // 最小值 Min Value to Make a new record
    private int inDutyMinTrackValue;

    private int outDutyMinTrackValue;

    // 修改后地点名 Location Name after Editing
    private string? editedLocationName = string.Empty;

    // 编辑页面开启状态 Edit Popup
    private bool isOnEdit = false;

    // 收支染色开启状态 Change Text Coloring
    private bool isChangeColoring = false;

    private Vector4 positiveChangeColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private Vector4 negativeChangeColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

    private Dictionary<string, List<TransactionsConvertor>> cache = new Dictionary<string, List<TransactionsConvertor>>();
    internal List<bool>? selectedStates = new List<bool>();
    internal List<TransactionsConvertor>? selectedTransactions = new List<TransactionsConvertor>();
    private Transactions transactions = new Transactions();
    private TransactionsConvertor transactionsConvertor = new TransactionsConvertor();
    private CurrencyInfo? currencyInfo = null!;
    private static LanguageManager? Lang;
    private List<string> permanentCurrencyName = new List<string>();
    internal List<string> options = new List<string>();
    internal List<string>? ordedOptions = new List<string>();
    internal List<string>? hiddenOptions = new List<string>();
    internal List<TransactionsConvertor> currentTypeTransactions = new List<TransactionsConvertor>();
    private List<TransactionsConvertor> lastTransactions = new List<TransactionsConvertor>();
    internal long[]? LinePlotData;

    public Main(Plugin plugin) : base("Currency Tracker")
    {
        Flags |= ImGuiWindowFlags.NoScrollbar;
        Flags |= ImGuiWindowFlags.NoScrollWithMouse;

        Initialize(plugin);
    }

    public void Dispose()
    {
    }

#pragma warning disable CS8602
#pragma warning disable CS8604

    // 初始化 Initialize
    private void Initialize(Plugin plugin)
    {
        transactions ??= new Transactions();

        isReversed = plugin.Configuration.ReverseSort;
        isTrackedinDuty = plugin.Configuration.TrackedInDuty;
        recordMode = plugin.Configuration.TrackMode;
        timerInterval = plugin.Configuration.TimerInterval;
        transactionsPerPage = plugin.Configuration.RecordsPerPage;
        ordedOptions = plugin.Configuration.OrdedOptions;
        hiddenOptions = plugin.Configuration.HiddenOptions;
        isChangeColoring = plugin.Configuration.ChangeTextColoring;
        positiveChangeColor = plugin.Configuration.PositiveChangeColor;
        negativeChangeColor = plugin.Configuration.NegativeChangeColor;

        LoadOptions();
        LoadLanguage(plugin);
        LoadCustomMinTrackValue();
    }

    // 将预置货币类型、玩家自定义的货币类型加入选项列表 Add preset currencies and player-customed currencies to the list of options
    private void LoadOptions()
    {
        currencyInfo ??= new CurrencyInfo();
        HashSet<string> addedOptions = new HashSet<string>();

        foreach (var currency in Tracker.CurrencyType)
        {
            if (currencyInfo.permanentCurrencies.TryGetValue(currency, out uint currencyID))
            {
                string? currencyName = currencyInfo.CurrencyLocalName(currencyID);

                if (!addedOptions.Contains(currencyName) && !hiddenOptions.Contains(currencyName))
                {
                    permanentCurrencyName.Add(currencyName);
                    options.Add(currencyName);
                    addedOptions.Add(currencyName);
                }
            }
        }

        foreach (var currency in Plugin.Instance.Configuration.CustomCurrencyType)
        {
            if (Plugin.Instance.Configuration.CustomCurrencies.TryGetValue(currency, out _))
            {
                if (!addedOptions.Contains(currency))
                {
                    options.Add(currency);
                    addedOptions.Add(currency);
                }
            }
        }

        if (ordedOptions == null)
        {
            ordedOptions = options;
            Plugin.Instance.Configuration.OrdedOptions = ordedOptions;
            Plugin.Instance.Configuration.Save();
        }
        else
        {
            ReloadOrderedOptions();
        }
    }

    // 处理插件语言表达 Handel the plugin UI's language
    private void LoadLanguage(Plugin plugin)
    {
        playerLang = plugin.Configuration.SelectedLanguage;

        if (string.IsNullOrEmpty(playerLang))
        {
            playerLang = Service.ClientState.ClientLanguage.ToString();
        }

        Lang = new LanguageManager(playerLang);
    }

    // 初始化自定义货币最小记录值
    private void LoadCustomMinTrackValue()
    {
        HashSet<string> addedCurrencies = new HashSet<string>();
        foreach (var currency in options)
        {
            if (Plugin.Instance.Configuration.MinTrackValueDic["InDuty"].ContainsKey(currency) && Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"].ContainsKey(currency))
                continue;
            if (!addedCurrencies.Contains(currency))
            {
                Plugin.Instance.Configuration.MinTrackValueDic["InDuty"].Add(currency, 0);
                Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"].Add(currency, 0);
                Plugin.Instance.Configuration.Save();
                addedCurrencies.Add(currency);
            }
        }
    }

    public override void Draw()
    {
        if (!Service.ClientState.IsLoggedIn) return;
        transactions ??= new Transactions();

        if (!showSortOptions) ImGui.TextColored(ImGuiColors.DalamudGrey, Lang.GetText("ConfigLabel"));
        else ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("ConfigLabel"));
        if (ImGui.IsItemClicked())
        {
            showSortOptions = !showSortOptions;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Lang.GetText("ConfigLabelHelp"));
        }

        if (showSortOptions)
        {
            ReverseSort();
            ImGui.SameLine();
            TimeClustering();
            ImGui.SameLine();
            SortByLocation();
            ImGui.SameLine();
            SortByChange();

            if (isTimeFilterEnabled)
            {
                SortByTime();
            }
            else
            {
                ImGui.SameLine();
                SortByTime();
            }
        }

        if (!showSortOptions && !showRecordOptions) ImGui.SameLine();

        if (!showRecordOptions) ImGui.TextColored(ImGuiColors.DalamudGrey, Lang.GetText("ConfigLabel1"));
        else ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("ConfigLabel1"));
        if (ImGui.IsItemClicked())
        {
            showRecordOptions = !showRecordOptions;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Lang.GetText("ConfigLabelHelp"));
        }
        if (showRecordOptions)
        {
            TrackInDuty();
            ImGui.SameLine();
            MinRecordValueInDuty();
            ImGui.SameLine();
            MergeTransactions();
            ImGui.SameLine();
            CustomCurrencyTracker();
            ImGui.SameLine();
            RecordMode();
            ImGui.SameLine();
            ClearExceptions();
        }

        if (!showRecordOptions && !showOthers) ImGui.SameLine();

        if (!showOthers) ImGui.TextColored(ImGuiColors.DalamudGrey, Lang.GetText("ConfigLabel2"));
        else ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("ConfigLabel2"));
        if (ImGui.IsItemClicked())
        {
            showOthers = !showOthers;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Lang.GetText("ConfigLabelHelp"));
        }
        if (showOthers)
        {
            ExportToCSV();
            ImGui.SameLine();
            OpenDataFolder();
            ImGui.SameLine();
            OpenGitHubPage();
            ImGui.SameLine();
            LanguageSwitch();
            if (Plugin.Instance.PluginInterface.IsDev)
            {
                FeaturesUnderTest();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        CurrenciesList();

        TransactionsChildframe();
    }

    // 测试用功能区 Some features still under testing
    private void FeaturesUnderTest()
    {
    }

    // 倒序排序 Reverse Sort
    private void ReverseSort()
    {
        if (ImGui.Checkbox($"{Lang.GetText("ReverseSort")}##InverseSort", ref isReversed))
        {
            selectedStates.Clear();
            selectedTransactions.Clear();
            Plugin.Instance.Configuration.ReverseSort = isReversed;
            Plugin.Instance.Configuration.Save();
        }
    }

    // 时间聚类 Time Clustering
    private void TimeClustering()
    {
        if (!isClusteredByTime) ImGui.Checkbox(Lang.GetText("ClusterByTime"), ref isClusteredByTime);
        else ImGui.Checkbox("", ref isClusteredByTime);

        if (isClusteredByTime)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(115);
            if (ImGui.InputInt(Lang.GetText("ClusterInterval"), ref clusterHour, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (clusterHour <= 0)
                {
                    clusterHour = 0;
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("ClusterByTimeHelp1")} {clusterHour}{Lang.GetText("ClusterByTimeHelp2")}");
        }
    }

    // 按收支数筛选 Sort By Change
    private void SortByChange()
    {
        if (!isChangeFilterEnabled) ImGui.Checkbox($"{Lang.GetText("ChangeFilterEnabled")}##ChangeFilter", ref isChangeFilterEnabled);
        else ImGui.Checkbox("##ChangeFilter", ref isChangeFilterEnabled);

        if (isChangeFilterEnabled)
        {
            ImGui.SameLine();
            ImGui.RadioButton($"{Lang.GetText("Greater")}##FilterMode", ref filterMode, 0);
            ImGui.SameLine();
            ImGui.RadioButton($"{Lang.GetText("Less")}##FilterMode", ref filterMode, 1);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(130);
            ImGui.InputInt($"##FilterValue", ref filterValue, 100, 100000, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGuiComponents.HelpMarker($"{Lang.GetText("CurrentSettings")}:\n{Lang.GetText("ChangeFilterLabel")} {(Lang.GetText(filterMode == 0 ? "Greater" : filterMode == 1 ? "Less" : ""))} {filterValue} {Lang.GetText("ChangeFilterValueLabel")}");
        }
    }

    // 按收支数筛选 Sort By Time
    private void SortByTime()
    {
        if (!isTimeFilterEnabled) ImGui.Checkbox($"{Lang.GetText("FilterByTime")}##TimeFilter", ref isTimeFilterEnabled);
        else ImGui.Checkbox("##TimeFilter", ref isTimeFilterEnabled);

        if (isTimeFilterEnabled)
        {
            int startYear = filterStartDate.Year;
            int startMonth = filterStartDate.Month;
            int startDay = filterStartDate.Day;
            int endYear = filterEndDate.Year;
            int endMonth = filterEndDate.Month;
            int endDay = filterEndDate.Day;

            ImGui.SameLine();
            ImGui.SetNextItemWidth(125);
            if (ImGui.InputInt($"{Lang.GetText("Year")}##StartYear", ref startYear, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterStartDate = new DateTime(startYear, startMonth, startDay);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt($"{Lang.GetText("Month")}##StartMonth", ref startMonth, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterStartDate = new DateTime(startYear, startMonth, startDay);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt($"{Lang.GetText("Day")}##StartDay", ref startDay, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterStartDate = new DateTime(startYear, startMonth, startDay);
            }

            ImGui.SameLine();
            ImGui.Text("~");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(125);
            if (ImGui.InputInt($"{Lang.GetText("Year")}##EndYear", ref endYear, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterEndDate = new DateTime(endYear, endMonth, endDay);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt($"{Lang.GetText("Month")}##EndMonth", ref endMonth, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterEndDate = new DateTime(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt($"{Lang.GetText("Day")}##EndDay", ref endDay, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                filterEndDate = new DateTime(endYear, endMonth, endDay);
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("TimeFilterLabel")} {filterStartDate.ToString("yyyy/MM/dd")} {Lang.GetText("TimeFilterLabel1")} {filterEndDate.ToString("yyyy/MM/dd")} {Lang.GetText("TimeFilterLabel2")}");
        }
    }

    // 按地点筛选 Sort By Location
    private void SortByLocation()
    {
        if (!isLocationFilterEnabled) ImGui.Checkbox($"{Lang.GetText("LocationFilter")}##LocationFilter", ref isLocationFilterEnabled);
        else ImGui.Checkbox("##LocationFilter", ref isLocationFilterEnabled);

        if (isLocationFilterEnabled)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##LocationSearch", ref searchLocationName, 80);
        }
    }

    // 是否在副本内记录数据 Track in Duty Switch
    private void TrackInDuty()
    {
        if (ImGui.Checkbox(Lang.GetText("TrackInDuty"), ref isTrackedinDuty))
        {
            Plugin.Instance.Configuration.TrackedInDuty = isTrackedinDuty;
            Plugin.Instance.Configuration.Save();
        }

        ImGuiComponents.HelpMarker(Lang.GetText("TrackInDutyHelp"));
    }

    // 最小记录值 Minimum Change Permitted to Create a New Transaction
    private void MinRecordValueInDuty()
    {
        if (!isTrackedinDuty) return;
        if (ImGui.Button(Lang.GetText("MinimumRecordValue")))
        {
            if (selectedCurrencyName != null)
            {
                ImGui.OpenPopup("MinTrackValue");
                inDutyMinTrackValue = Plugin.Instance.Configuration.MinTrackValueDic["InDuty"][selectedCurrencyName];
                outDutyMinTrackValue = Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"][selectedCurrencyName];
            }
            else
            {
                Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
                return;
            }
        }

        if (ImGui.BeginPopup("MinTrackValue"))
        {
            if (selectedCurrencyName != null)
            {
                ImGui.Text(Lang.GetText("CustomCurrencyLabel2"));
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, selectedCurrencyName);
                ImGui.Separator();
                ImGui.Text($"{Lang.GetText("MinimumRecordValueLabel")}{Plugin.Instance.Configuration.MinTrackValueDic["InDuty"][selectedCurrencyName]}");
                ImGui.SetNextItemWidth(175);
                ImGui.InputInt("##MinInDuty", ref inDutyMinTrackValue, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue);
                if (inDutyMinTrackValue < 0) inDutyMinTrackValue = 0;
                ImGui.Text($"{Lang.GetText("MinimumRecordValueLabel1")}{Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"][selectedCurrencyName]}");
                ImGui.SetNextItemWidth(175);
                ImGui.InputInt("##MinOutDuty", ref outDutyMinTrackValue, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue);
                if (inDutyMinTrackValue < 0) inDutyMinTrackValue = 0;
                if (ImGui.Button(Lang.GetText("MinimumRecordValueLabel2")))
                {
                    Plugin.Instance.Configuration.MinTrackValueDic["InDuty"][selectedCurrencyName] = inDutyMinTrackValue;
                    Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"][selectedCurrencyName] = outDutyMinTrackValue;
                    Plugin.Instance.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"{Lang.GetText("MinimumRecordValueHelp")}{Plugin.Instance.Configuration.MinTrackValueDic["InDuty"][selectedCurrencyName]}{Lang.GetText("MinimumRecordValueHelp1")}{Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"][selectedCurrencyName]}{Lang.GetText("MinimumRecordValueHelp2")}");
            }
            else
            {
                return;
            }
            ImGui.EndPopup();
        }
    }

    // 自定义货币追踪 Custom Currencies To Track
    private void CustomCurrencyTracker()
    {
        if (ImGui.Button(Lang.GetText("CustomCurrencyLabel")))
        {
            ImGui.OpenPopup("CustomCurrency");
        }

        if (ImGui.BeginPopup("CustomCurrency"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("CustomCurrencyLabel1"));
            ImGuiComponents.HelpMarker(Lang.GetText("CustomCurrencyHelp"));
            ImGui.Text(Lang.GetText("CustomCurrencyLabel2"));
            if (ImGui.BeginCombo("", Plugin.Instance.ItemNames.TryGetValue(customCurrency, out var selected) ? selected : Lang.GetText("CustomCurrencyLabel3")))
            {
                ImGui.SetNextItemWidth(200f);
                ImGui.InputTextWithHint("##selectflts", Lang.GetText("CustomCurrencyLabel4"), ref searchFilter, 50);
                ImGui.Separator();

                foreach (var x in Plugin.Instance.ItemNames)
                {
                    var shouldSkip = false;
                    foreach (var y in permanentCurrencyName)
                    {
                        if (x.Value.Contains(y))
                        {
                            shouldSkip = true;
                            break;
                        }
                    }
                    if (shouldSkip)
                    {
                        continue;
                    }

                    if (searchFilter != string.Empty && !x.Value.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)) continue;

                    if (ImGui.Selectable(x.Value))
                    {
                        customCurrency = x.Key;
                    }

                    if (ImGui.IsWindowAppearing() && customCurrency == x.Key)
                    {
                        ImGui.SetScrollHereY();
                    }
                }
                ImGui.EndCombo();
            }

            if (ImGui.Button($"{Lang.GetText("Add")}{selected}"))
            {
                if (string.IsNullOrEmpty(selected))
                {
                    Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
                    return;
                }
                if (options.Contains(selected))
                {
                    Service.Chat.PrintError(Lang.GetText("CustomCurrencyHelp1"));
                    return;
                }
                if (Plugin.Instance.Configuration.CustomCurrencyType.Contains(selected))
                    Plugin.Instance.Configuration.CustomCurrencies.Add(selected, customCurrency);
                Plugin.Instance.Configuration.CustomCurrencyType.Add(selected);

                if (!Plugin.Instance.Configuration.MinTrackValueDic["InDuty"].ContainsKey(selected) && !Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"].ContainsKey(selected))
                {
                    Plugin.Instance.Configuration.MinTrackValueDic["InDuty"].Add(selected, 0);
                    Plugin.Instance.Configuration.MinTrackValueDic["OutOfDuty"].Add(selected, 0);
                }
                Plugin.Instance.Configuration.Save();
                options.Add(selected);
                ReloadOrderedOptions();
            }

            ImGui.SameLine();

            if (ImGui.Button($"{Lang.GetText("Delete")}{selected}"))
            {
                if (string.IsNullOrEmpty(selected))
                {
                    Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
                    return;
                }
                if (!options.Contains(selected))
                {
                    Service.Chat.PrintError(Lang.GetText("CustomCurrencyHelp2"));
                    return;
                }
                Plugin.Instance.Configuration.CustomCurrencies.Remove(selected);
                Plugin.Instance.Configuration.CustomCurrencyType.Remove(selected);
                Plugin.Instance.Configuration.Save();
                options.Remove(selected);
                ReloadOrderedOptions();
            }

            ImGui.EndPopup();
        }
    }

    // 按临界值合并记录 Merge Transactions By Threshold
    private void MergeTransactions()
    {
        transactions ??= new Transactions();

        if (ImGui.Button(Lang.GetText("MergeTransactionsLabel")))
        {
            ImGui.OpenPopup("MergeTransactions");
        }

        if (ImGui.BeginPopup("MergeTransactions"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("MergeTransactionsLabel4"));
            ImGui.Text(Lang.GetText("MergeTransactionsLabel1"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            ImGui.InputInt("##MergeThreshold", ref mergeThreshold, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue);
            if (mergeThreshold < 0)
            {
                mergeThreshold = 0;
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("MergeTransactionsHelp3")}{Lang.GetText("TransactionsHelp2")}");

            // 双向合并 Two-Way Merge
            if (ImGui.Button(Lang.GetText("MergeTransactionsLabel2")))
            {
                int mergeCount = MergeTransactions(false);
                if (mergeCount == 0)
                    return;
            }

            ImGui.SameLine();

            // 单向合并 One-Way Merge
            if (ImGui.Button(Lang.GetText("MergeTransactionsLabel3")))
            {
                int mergeCount = MergeTransactions(true);
                if (mergeCount == 0)
                    return;
            }
            ImGui.EndPopup();
        }
    }

    // 清除异常记录 Clear Exceptional Transactions
    private void ClearExceptions()
    {
        if (ImGui.Button(Lang.GetText("ClearExTransactionsLabel")))
        {
            ImGui.OpenPopup("ClearExceptionNote");
        }

        if (ImGui.BeginPopup("ClearExceptionNote"))
        {
            if (ImGui.Button(Lang.GetText("Confirm")))
            {
                if (string.IsNullOrEmpty(selectedCurrencyName))
                {
                    Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
                    return;
                }

                var removedCount = transactions.ClearExceptionRecords(selectedCurrencyName);
                if (removedCount > 0)
                {
                    Service.Chat.Print($"{Lang.GetText("ClearExTransactionsHelp2")}{removedCount}{Lang.GetText("ClearExTransactionsHelp3")}");
                }
                else
                {
                    Service.Chat.PrintError(Lang.GetText("TransactionsHelp"));
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("ClearExTransactionsHelp")}{Lang.GetText("ClearExTransactionsHelp1")}{Lang.GetText("TransactionsHelp2")}");
            ImGui.EndPopup();
        }
    }

    // 导出数据为.CSV文件 Export Transactions To a .csv File
    private void ExportToCSV()
    {
        if (ImGui.Button(Lang.GetText("ExportCsv")))
        {
            ImGui.OpenPopup(str_id: "ExportFileRename");
        }

        if (ImGui.BeginPopup("ExportFileRename"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Lang.GetText("FileRenameLabel"));
            ImGui.Text(Lang.GetText("FileRenameLabel1"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputText($"_{selectedCurrencyName}_{Lang.GetText("FileRenameLabel2")}.csv", ref fileName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (selectedCurrencyName == null)
                {
                    Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
                    return;
                }
                if (currentTypeTransactions == null || currentTypeTransactions.Count == 0)
                {
                    Service.Chat.PrintError(Lang.GetText("ExportCsvMessage1"));
                    return;
                }
                var filePath = transactions.ExportToCsv(currentTypeTransactions, fileName, selectedCurrencyName, Lang.GetText("ExportCsvMessage2"));
                Service.Chat.Print($"{Lang.GetText("ExportCsvMessage3")}{filePath}");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{Lang.GetText("FileRenameHelp1")}{selectedCurrencyName}_{Lang.GetText("FileRenameLabel2")}.csv");
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Lang.GetText("FileRenameHelp"));
            ImGui.EndPopup();
        }
    }

    // 打开数据文件夹 Open Folder Containing Data Files
    private void OpenDataFolder()
    {
        if (ImGui.Button(Lang.GetText("OpenDataFolder")))
        {
            var playerName = Service.ClientState.LocalPlayer?.Name?.TextValue;
            var serverName = Service.ClientState.LocalPlayer?.HomeWorld?.GameData?.Name;
            string playerDataFolder = Path.Join(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, $"{playerName}_{serverName}");

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start \"\" \"{playerDataFolder}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = playerDataFolder
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = playerDataFolder
                    });
                }
                else
                {
                    PluginLog.Error("Unsupported OS");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error :{ex.Message}");
            }
        }
    }

    // 打开插件 GitHub 页面 Open Plugin GitHub Page
    private void OpenGitHubPage()
    {
        if (ImGui.Button("GitHub"))
        {
            string url = "https://github.com/AtmoOmen/CurrencyTracker";
            ProcessStartInfo psi = new ProcessStartInfo();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi.FileName = url;
                psi.UseShellExecute = true;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                psi.FileName = "xdg-open";
                psi.ArgumentList.Add(url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                psi.FileName = "open";
                psi.ArgumentList.Add(url);
            }
            else
            {
                PluginLog.Error("Unsupported OS");
                return;
            }

            Process.Start(psi);
        }
    }

    // 界面语言切换功能 Language Switch
    private void LanguageSwitch()
    {
        var AvailableLangs = Lang.AvailableLanguage();

        var lang = string.Empty;

        if (Widgets.IconButton(FontAwesomeIcon.Globe, "Languages"))
        {
            ImGui.OpenPopup(str_id: "LanguagesList");
        }

        if (ImGui.BeginPopup("LanguagesList"))
        {
            foreach (var langname in AvailableLangs)
            {
                var langquery = from pair in LanguageManager.LanguageNames
                                where pair.Value == langname
                                select pair.Key;
                var language = langquery.FirstOrDefault();
                if (language.IsNullOrEmpty())
                {
                    Service.Chat.PrintError(Lang.GetText("UnknownCurrency"));
                    return;
                }
                if (ImGui.Button(langname))
                {
                    Lang = new LanguageManager(language);
                    Graph.Lang = new LanguageManager(language);

                    playerLang = language;

                    Plugin.Instance.Configuration.SelectedLanguage = playerLang;
                    Plugin.Instance.Configuration.Save();
                }
            }
            ImGui.EndPopup();
        }
    }

    // 记录模式切换 Record Mode Change
    private void RecordMode()
    {
        if (ImGui.Button($"{Lang.GetText("TrackModeLabel")}"))
        {
            ImGui.OpenPopup("RecordMode");
        }
        if (ImGui.BeginPopup("RecordMode"))
        {
            if (ImGui.RadioButton($"{Lang.GetText("TrackModeLabel1")}##RecordMode", ref recordMode, 0))
            {
                Plugin.Instance.Configuration.TrackMode = recordMode;
                Plugin.Instance.Configuration.Save();
                Service.Tracker.ChangeTracker();
            }
            if (recordMode == 0)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(135);
                if (ImGui.InputInt($"{Lang.GetText("TrackModeLabel3")}##TimerInterval", ref timerInterval, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (timerInterval < 100) timerInterval = 100;
                    Plugin.Instance.Configuration.TimerInterval = timerInterval;
                    Plugin.Instance.Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{Lang.GetText("TrackModeHelp3")}");
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("TrackModeHelp")}{timerInterval}{Lang.GetText("TrackModeHelp1")}");
            if (ImGui.RadioButton($"{Lang.GetText("TrackModeLabel2")}##RecordMode", ref recordMode, 1))
            {
                Plugin.Instance.Configuration.TrackMode = recordMode;
                Plugin.Instance.Configuration.Save();
                Service.Tracker.ChangeTracker();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Lang.GetText("TrackModeHelp2")}");
            ImGui.EndPopup();
        }
    }

    // 图表工具栏 Table Tools
    private void TableTools()
    {
        ImGui.Text($"{Lang.GetText("Now")}: {selectedTransactions.Count} {Lang.GetText("Transactions")}");
        ImGui.Separator();

        if (ImGui.Selectable(Lang.GetText("SelectAll")))
        {
            selectedTransactions.Clear();

            foreach (var transaction in currentTypeTransactions)
            {
                selectedTransactions.Add(transaction);
            }

            for (int i = 0; i < selectedStates.Count; i++)
            {
                selectedStates[i] = true;
            }
        }

        if (ImGui.Selectable(Lang.GetText("InverseSelect")))
        {
            for (int i = 0; i < selectedStates.Count; i++)
            {
                selectedStates[i] = !selectedStates[i];
            }

            foreach (var transaction in currentTypeTransactions)
            {
                bool exists = selectedTransactions.Any(selectedTransaction => Widgets.IsTransactionEqual(selectedTransaction, transaction));

                if (exists)
                {
                    selectedTransactions.RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                }
                else
                {
                    selectedTransactions.Add(transaction);
                }
            }
        }

        if (ImGui.Selectable(Lang.GetText("Unselect")))
        {
            if (selectedTransactions.Count == 0)
            {
                Service.Chat.PrintError(Lang.GetText("NoTransactionsSelected"));
                return;
            }
            selectedStates.Clear();
            selectedTransactions.Clear();
        }

        if (ImGui.Selectable(Lang.GetText("Copy")))
        {
            string columnData = string.Empty;
            int count = selectedTransactions.Count;

            for (int t = 0; t < count; t++)
            {
                var record = selectedTransactions[t];
                string change = $"{record.Change:+ #,##0;- #,##0;0}";
                columnData += $"{record.TimeStamp} | {record.Amount} | {change} | {record.LocationName}";

                if (t < count - 1)
                {
                    columnData += "\n";
                }
            }

            if (!string.IsNullOrEmpty(columnData))
            {
                ImGui.SetClipboardText(columnData);
                Service.Chat.Print($"{Lang.GetText("CopyTransactionsHelp")} {selectedTransactions.Count} {Lang.GetText("CopyTransactionsHelp1")}");
            }
            else
            {
                Service.Chat.PrintError(Lang.GetText("NoTransactionsSelected"));
                return;
            }
        }

        if (ImGui.Selectable(Lang.GetText("Export")))
        {
            if (selectedTransactions.Count == 0)
            {
                Service.Chat.PrintError(Lang.GetText("NoTransactionsSelected"));
                return;
            }
            var filePath = transactions.ExportToCsv(selectedTransactions, "", selectedCurrencyName, Lang.GetText("ExportCsvMessage2"));
            Service.Chat.Print($"{Lang.GetText("ExportCsvMessage3")}{filePath}");
        }

        if (ImGui.Selectable(Lang.GetText("Delete")))
        {
            if (selectedTransactions.Count == 0)
            {
                Service.Chat.PrintError(Lang.GetText("NoTransactionsSelected"));
                return;
            }
            foreach (var selectedTransaction in selectedTransactions)
            {
                var playerName = Service.ClientState.LocalPlayer?.Name?.TextValue;
                var serverName = Service.ClientState.LocalPlayer?.HomeWorld?.GameData?.Name;
                string filePath = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, $"{playerName}_{serverName}", $"{selectedCurrencyName}.txt");
                var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                editedTransactions.Remove(selectedTransaction);
                var foundTransaction = editedTransactions.FirstOrDefault(t => Widgets.IsTransactionEqual(t, selectedTransaction));

                if (foundTransaction != null)
                {
                    editedTransactions.Remove(foundTransaction);
                }

                transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
            }
            selectedStates.Clear();
            selectedTransactions.Clear();
        }

        if (ImGui.Selectable(Lang.GetText("Edit"), isOnEdit, ImGuiSelectableFlags.DontClosePopups))
        {
            isOnEdit = !isOnEdit;
        }

        if (isOnEdit)
        {
            ImGui.Separator();
            ImGui.Text($"{Lang.GetText("Location")}:");
            ImGui.SetNextItemWidth(210);

            if (ImGui.InputTextWithHint("", Lang.GetText("EditHelp"), ref editedLocationName, 80, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (selectedTransactions.Count == 0)
                {
                    Service.Chat.PrintError(Lang.GetText("NoTransactionsSelected"));
                    return;
                }

                if (editedLocationName.IsNullOrWhitespace())
                {
                    Service.Chat.PrintError(Lang.GetText("EditHelp1"));
                    return;
                }

                foreach (var selectedTransaction in selectedTransactions)
                {
                    var playerName = Service.ClientState.LocalPlayer?.Name?.TextValue;
                    var serverName = Service.ClientState.LocalPlayer?.HomeWorld?.GameData?.Name;
                    string filePath = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, $"{playerName}_{serverName}", $"{selectedCurrencyName}.txt");
                    var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);

                    int index = -1;
                    for (int i = 0; i < editedTransactions.Count; i++)
                    {
                        if (Widgets.IsTransactionEqual(editedTransactions[i], selectedTransaction))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index != -1)
                    {
                        editedTransactions[index].LocationName = editedLocationName;
                        transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
                    }
                }

                Service.Chat.Print($"{Lang.GetText("EditHelp2")} {selectedTransactions.Count} {Lang.GetText("EditHelp3")} {editedLocationName}");
                selectedStates.Clear();
                selectedTransactions.Clear();
                isOnEdit = false;
            }
        }
    }

    // 收支文本染色 Change Text Coloring
    private void ChangeTextColoring()
    {
        if (ImGui.IsItemClicked())
        {
            ImGui.OpenPopup("ChangeTextColoring");
        }

        if (ImGui.BeginPopup("ChangeTextColoring"))
        {
            ImGui.Text(Lang.GetText("ChangeTextColoring"));
            ImGui.SameLine();
            if (ImGui.Checkbox("##ChangeColoring", ref isChangeColoring))
            {
                Plugin.Instance.Configuration.ChangeTextColoring = isChangeColoring;
                Plugin.Instance.Configuration.Save();
            }
            ImGui.Separator();

            if (ImGui.ColorButton("##PositiveColor", positiveChangeColor))
            {
                ImGui.OpenPopup("PositiveColor");
            }
            ImGui.SameLine();
            ImGui.Text(Lang.GetText("PositiveChange"));

            if (ImGui.BeginPopup("PositiveColor"))
            {
                if (ImGui.ColorPicker4("", ref positiveChangeColor))
                {
                    isChangeColoring = true;
                    Plugin.Instance.Configuration.ChangeTextColoring = isChangeColoring;
                    Plugin.Instance.Configuration.PositiveChangeColor = positiveChangeColor;
                    Plugin.Instance.Configuration.Save();
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            if (ImGui.ColorButton("##NegativeColor", negativeChangeColor))
            {
                ImGui.OpenPopup("NegativeColor");
            }
            ImGui.SameLine();
            ImGui.Text(Lang.GetText("NegativeChange"));

            if (ImGui.BeginPopup("NegativeColor"))
            {
                if (ImGui.ColorPicker4("", ref negativeChangeColor))
                {
                    isChangeColoring = true;
                    Plugin.Instance.Configuration.ChangeTextColoring = isChangeColoring;
                    Plugin.Instance.Configuration.NegativeChangeColor = negativeChangeColor;
                    Plugin.Instance.Configuration.Save();
                }
                ImGui.EndPopup();
            }

            ImGui.EndPopup();
        }
    }

    // 存储可用货币名称选项的列表框 Listbox Containing Available Currencies' Name
    private void CurrenciesList()
    {
        var ChildFrameHeight = ChildframeHeightAdjust();

        Vector2 childScale = new Vector2(243, ChildFrameHeight);
        if (ImGui.BeginChildFrame(2, childScale, ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.SetCursorPosX(42);
            if (string.IsNullOrWhiteSpace(selectedCurrencyName) || selectedOptionIndex == -1 || !permanentCurrencyName.Contains(selectedCurrencyName))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                Widgets.IconButton(FontAwesomeIcon.EyeSlash);
                ImGui.PopStyleVar();
            }
            else
            {
                Widgets.IconButton(FontAwesomeIcon.EyeSlash, Lang.GetText("Hide"));
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
                {
                    if (string.IsNullOrWhiteSpace(selectedCurrencyName) || selectedOptionIndex == -1 || !permanentCurrencyName.Contains(selectedCurrencyName)) return;

                    options.Remove(selectedCurrencyName);
                    hiddenOptions.Add(selectedCurrencyName);
                    if (!Plugin.Instance.Configuration.HiddenOptions.Contains(selectedCurrencyName))
                        Plugin.Instance.Configuration.HiddenOptions.Add(selectedCurrencyName);
                    Plugin.Instance.Configuration.Save();
                    ReloadOrderedOptions();
                    selectedCurrencyName = string.Empty;
                    selectedOptionIndex = -1;
                }
            }
            ImGui.SameLine();
            if (ImGui.ArrowButton("UpArrow", ImGuiDir.Up) && selectedOptionIndex > 0)
            {
                SwapOptions(selectedOptionIndex, selectedOptionIndex - 1);
                selectedOptionIndex--;
            }
            ImGui.SameLine();
            if (ImGui.ArrowButton("DownArrow", ImGuiDir.Down) && selectedOptionIndex < ordedOptions.Count - 1 && selectedOptionIndex > -1)
            {
                SwapOptions(selectedOptionIndex, selectedOptionIndex + 1);
                selectedOptionIndex++;
            }
            ImGui.SameLine();

            if (hiddenOptions.Count == 0)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                Widgets.IconButton(FontAwesomeIcon.TrashRestore);
                ImGui.PopStyleVar();
            }
            else
            {
                Widgets.IconButton(FontAwesomeIcon.TrashRestore, Lang.GetText("OrderChangeLabel1"));
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
                {
                    if (hiddenOptions.Count == 0)
                    {
                        Service.Chat.PrintError(Lang.GetText("OrderChangeHelp"));
                        return;
                    }
                    HashSet<string> addedOptions = new HashSet<string>();

                    foreach (var option in hiddenOptions)
                    {
                        if (!addedOptions.Contains(option))
                        {
                            options.Add(option);
                            permanentCurrencyName.Add(option);
                            addedOptions.Add(option);
                        }
                    }
                    hiddenOptions.Clear();
                    Plugin.Instance.Configuration.HiddenOptions.Clear();
                    Plugin.Instance.Configuration.Save();
                    Service.Chat.Print($"{Lang.GetText("OrderChangeHelp1")} {addedOptions.Count} {Lang.GetText("OrderChangeHelp2")}");
                    ReloadOrderedOptions();
                }
            }

            ImGui.Separator();
            ImGui.SetNextItemWidth(235);
            for (int i = 0; i < ordedOptions.Count; i++)
            {
                string option = ordedOptions[i];
                bool isSelected = i == selectedOptionIndex;

                if (ImGui.Selectable(option, isSelected))
                {
                    selectedOptionIndex = i;
                    selectedCurrencyName = option;
                    selectedStates.Clear();
                    selectedTransactions.Clear();
                }
            }

            ImGui.EndChildFrame();
        }
    }

    // 显示收支记录的表格子窗体 Childframe Used to Show Transactions in Form
    private void TransactionsChildframe()
    {
        if (string.IsNullOrEmpty(selectedCurrencyName))
            return;
        if (Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
            return;
        if (Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            return;

        var childFrameHeight = ChildframeHeightAdjust();
        Vector2 childScale = new Vector2(ImGui.GetWindowWidth() - 100, childFrameHeight);

        ImGui.SameLine();

        if (ImGui.BeginChildFrame(1, childScale, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            currentTypeTransactions = transactions.LoadAllTransactions(selectedCurrencyName);

            if (isReversed)
                currentTypeTransactions.Reverse();

            if (isClusteredByTime && clusterHour > 0)
            {
                TimeSpan interval = TimeSpan.FromHours(clusterHour);
                currentTypeTransactions = transactions.ClusterTransactionsByTime(currentTypeTransactions, interval);
            }

            if (isChangeFilterEnabled)
                currentTypeTransactions = ApplyChangeFilter(currentTypeTransactions);

            if (isTimeFilterEnabled)
                currentTypeTransactions = ApplyDateTimeFilter(currentTypeTransactions);

            if (isLocationFilterEnabled)
                currentTypeTransactions = ApplyLocationFilter(currentTypeTransactions, searchLocationName);

            if (currentTypeTransactions.Count <= 0) return;

            if (!Widgets.AreTransactionsEqual(lastTransactions, currentTypeTransactions))
            {
                selectedStates.Clear();
                selectedTransactions.Clear();
                lastTransactions = currentTypeTransactions;
            }

            int pageCount = (int)Math.Ceiling((double)currentTypeTransactions.Count / transactionsPerPage);
            currentPage = Math.Clamp(currentPage, 0, pageCount - 1);

            if (pageCount == 0)
            {
                if (Plugin.Instance.Graph.IsOpen) Plugin.Instance.Graph.IsOpen = false;
                return;
            }

            float buttonWidth = ImGui.CalcTextSize(Lang.GetText("    ")).X;
            float buttonPosX = graphsRightAligned
                ? ImGui.GetWindowWidth() - 177 - buttonWidth
                : (ImGui.GetWindowWidth() - 360) / 2 - 57 - buttonWidth;

            ImGui.SetCursorPosX(buttonPosX);

            if (Widgets.IconButton(FontAwesomeIcon.ChartBar, Lang.GetText("Graphs")))
            {
                if (selectedCurrencyName != null && currentTypeTransactions.Count != 1 && currentTypeTransactions != null)
                {
                    LinePlotData = currentTypeTransactions.Select(x => x.Amount).ToArray();
                    Plugin.Instance.Graph.IsOpen = !Plugin.Instance.Graph.IsOpen;
                }
                else return;
            }

            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                graphsRightAligned = !graphsRightAligned;

            ImGui.SameLine();
            float pageButtonPosX = (ImGui.GetWindowWidth() - 360) / 2 - 40;
            ImGui.SetCursorPosX(pageButtonPosX);

            if (Widgets.IconButton(FontAwesomeIcon.Backward))
                currentPage = 0;

            ImGui.SameLine();

            if (ImGui.ArrowButton("PreviousPage", ImGuiDir.Left) && currentPage > 0)
                currentPage--;

            ImGui.SameLine();
            ImGui.Text($"{Lang.GetText("Di")}{currentPage + 1}{Lang.GetText("Page")} / {Lang.GetText("Gong")}{pageCount}{Lang.GetText("Page")}");

            if (ImGui.IsItemClicked())
            {
                ImGui.OpenPopup("TransactionsPerPage");
            }

            if (ImGui.BeginPopup("TransactionsPerPage"))
            {
                ImGui.Text(Lang.GetText("TransactionsPerPage"));
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120);

                if (ImGui.InputInt("##TransactionsPerPage", ref transactionsPerPage))
                {
                    transactionsPerPage = Math.Max(transactionsPerPage, 0);
                    Plugin.Instance.Configuration.RecordsPerPage = transactionsPerPage;
                    Plugin.Instance.Configuration.Save();
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine();

            if (ImGui.ArrowButton("NextPage", ImGuiDir.Right) && currentPage < pageCount - 1)
                currentPage++;

            ImGui.SameLine();

            if (Widgets.IconButton(FontAwesomeIcon.Forward) && currentPage >= 0)
                currentPage = pageCount;

            visibleStartIndex = currentPage * transactionsPerPage;
            visibleEndIndex = Math.Min(visibleStartIndex + transactionsPerPage, currentTypeTransactions.Count);

            if (ImGui.BeginTable("Transactions", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable, new Vector2(ImGui.GetWindowWidth() - 175, 1)))
            {
                ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, ImGui.CalcTextSize((currentTypeTransactions.Count + 1).ToString()).X + 10, 0);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.None, 150, 0);
                ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.None, 130, 0);
                ImGui.TableSetupColumn("Change", ImGuiTableColumnFlags.None, 100, 0);
                ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.None, 150, 0);
                ImGui.TableSetupColumn("Selected", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, 30, 0);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

                ImGui.TableNextColumn();
                ImGui.Text("");

                ImGui.TableNextColumn();
                ImGui.Text(Lang.GetText("Time"));

                ImGui.TableNextColumn();
                ImGui.Text(Lang.GetText("Amount"));

                ImGui.TableNextColumn();
                ImGui.Text(Lang.GetText("Change"));
                ChangeTextColoring();

                ImGui.TableNextColumn();
                ImGui.Text(Lang.GetText("Location"));

                ImGui.TableNextColumn();

                if (Widgets.IconButton(FontAwesomeIcon.EllipsisH))
                {
                    ImGui.OpenPopup("TableTools");
                }

                ImGui.TableNextRow();

                for (int i = visibleStartIndex; i < visibleEndIndex; i++)
                {
                    var transaction = currentTypeTransactions[i];
                    while (selectedStates.Count <= i)
                    {
                        selectedStates.Add(false);
                    }

                    bool selected = selectedStates[i];

                    ImGui.TableNextColumn();
                    if (isReversed)
                    {
                        ImGui.SetCursorPosX(Widgets.SetColumnCenterAligned((currentTypeTransactions.Count - i).ToString(), 0, 8));
                        ImGui.Text((currentTypeTransactions.Count - i).ToString());
                    }
                    else
                    {
                        ImGui.SetCursorPosX(Widgets.SetColumnCenterAligned((i + 1).ToString(), 0, 8));
                        ImGui.Text((i + 1).ToString());
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        ImGui.Selectable(transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), ref selected, ImGuiSelectableFlags.SpanAllColumns);
                        if (ImGui.IsItemHovered())
                        {
                            selectedStates[i] = selected = true;

                            if (selected)
                            {
                                bool exists = selectedTransactions.Any(t => Widgets.IsTransactionEqual(t, transaction));

                                if (!exists)
                                {
                                    selectedTransactions.Add(transaction);
                                }
                            }
                            else
                            {
                                selectedTransactions.RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                            }
                        }
                    }
                    else
                    {
                        ImGui.Selectable(transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"));
                    }

                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetClipboardText(transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"));
                        Service.Chat.Print($"{Lang.GetText("CopiedToClipboard")}: {transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss")}");
                    }

                    ImGui.TableNextColumn();
                    ImGui.Selectable(transaction.Amount.ToString("#,##0"));

                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetClipboardText(transaction.Amount.ToString("#,##0"));
                        Service.Chat.Print($"{Lang.GetText("CopiedToClipboard")}: {transaction.Amount.ToString("#,##0")}");
                    }

                    ImGui.TableNextColumn();
                    if (isChangeColoring)
                    {
                        if (transaction.Change > 0)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, positiveChangeColor);
                        }
                        else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, negativeChangeColor);
                        }
                        ImGui.Selectable(transaction.Change.ToString("+ #,##0;- #,##0;0"));
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.Selectable(transaction.Change.ToString("+ #,##0;- #,##0;0"));
                    }

                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetClipboardText(transaction.Change.ToString("+ #,##0;- #,##0;0"));
                        Service.Chat.Print($"{Lang.GetText("CopiedToClipboard")} : {transaction.Change.ToString("+ #,##0;- #,##0;0")}");
                    }

                    ImGui.TableNextColumn();
                    ImGui.Selectable(transaction.LocationName);

                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetClipboardText(transaction.LocationName);
                        Service.Chat.Print($"{Lang.GetText("CopiedToClipboard")}: {transaction.LocationName}");
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Checkbox($"##select_{i}", ref selected))
                    {
                        selectedStates[i] = selected;

                        if (selected)
                        {
                            bool exists = selectedTransactions.Any(t => Widgets.IsTransactionEqual(t, transaction));

                            if (!exists)
                            {
                                selectedTransactions.Add(transaction);
                            }
                        }
                        else
                        {
                            selectedTransactions.RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                        }
                    }

                    ImGui.TableNextRow();
                }

                if (ImGui.BeginPopup("TableTools"))
                {
                    TableTools();
                    ImGui.EndPopup();
                }

                ImGui.EndTable();
            }

            ImGui.EndChildFrame();
        }
    }

    // 用于处理选项顺序 Used to handle options' positions.
    private void ReloadOrderedOptions()
    {
        bool areEqual = ordedOptions.All(options.Contains) && options.All(ordedOptions.Contains);
        if (!areEqual)
        {
            List<string> additionalElements = options.Except(ordedOptions).ToList();
            ordedOptions.AddRange(additionalElements);

            List<string> missingElements = ordedOptions.Except(options).ToList();
            ordedOptions.RemoveAll(item => missingElements.Contains(item));

            Plugin.Instance.Configuration.OrdedOptions = ordedOptions;
            Plugin.Instance.Configuration.Save();
        }
    }

    // 用于处理选项位置变化 Used to handle option's position change.
    private void SwapOptions(int index1, int index2)
    {
        string temp = ordedOptions[index1];
        ordedOptions[index1] = ordedOptions[index2];
        ordedOptions[index2] = temp;

        Plugin.Instance.Configuration.OrdedOptions = ordedOptions;
        Plugin.Instance.Configuration.Save();
    }

    // 按收支隐藏不符合要求的交易记录 Hide Unmatched Transactions By Change
    private List<TransactionsConvertor> ApplyChangeFilter(List<TransactionsConvertor> transactions)
    {
        List<TransactionsConvertor> filteredTransactions = new List<TransactionsConvertor>();

        foreach (var transaction in transactions)
        {
            bool isTransactionValid = filterMode == 0 ?
                transaction.Change > filterValue :
                transaction.Change < filterValue;

            if (isTransactionValid)
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }

    // 按时间显示交易记录 Hide Unmatched Transactions By Time
    private List<TransactionsConvertor> ApplyDateTimeFilter(List<TransactionsConvertor> transactions)
    {
        List<TransactionsConvertor> filteredTransactions = new List<TransactionsConvertor>();

        foreach (var transaction in transactions)
        {
            if (transaction.TimeStamp >= filterStartDate && transaction.TimeStamp <= filterEndDate)
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }

    // 按地点名显示交易记录 Hide Unmatched Transactions By Location
    private List<TransactionsConvertor> ApplyLocationFilter(List<TransactionsConvertor> transactions, string LocationName)
    {
        LocationName = LocationName.Normalize(NormalizationForm.FormKC);
        if (LocationName.IsNullOrWhitespace())
        {
            return transactions;
        }

        List<TransactionsConvertor> filteredTransactions = new List<TransactionsConvertor>();

        foreach (var transaction in transactions)
        {
            var normalizedLocation = transaction.LocationName.Normalize(NormalizationForm.FormKC);

            if (normalizedLocation.IndexOf(LocationName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }

    // 合并交易记录用 Used to simplified merging transactions code
    private int MergeTransactions(bool oneWay)
    {
        if (string.IsNullOrEmpty(selectedCurrencyName))
        {
            Service.Chat.PrintError(Lang.GetText("TransactionsHelp1"));
            return 0;
        }

        int threshold = (mergeThreshold == 0) ? int.MaxValue : mergeThreshold;
        int mergeCount = transactions.MergeTransactionsByLocationAndThreshold(selectedCurrencyName, threshold, oneWay);

        if (mergeCount > 0)
            Service.Chat.Print($"{Lang.GetText("MergeTransactionsHelp1")}{mergeCount}{Lang.GetText("MergeTransactionsHelp2")}");
        else
            Service.Chat.PrintError(Lang.GetText("TransactionsHelp"));

        return mergeCount;
    }

    // 调整列表框和表格高度用 Used to adjust the height of listbox and chart
    private float ChildframeHeightAdjust()
    {
        var trueCount = Convert.ToInt32(showOthers) + Convert.ToInt32(showRecordOptions) + Convert.ToInt32(showSortOptions);
        var ChildFrameHeight = ImGui.GetWindowHeight() - 245;

        if (showRecordOptions)
        {
            if (trueCount == 2) ChildFrameHeight = ImGui.GetWindowHeight() - 210;
            if (trueCount == 1) ChildFrameHeight = ImGui.GetWindowHeight() - 175;
        }
        else
        {
            if (trueCount == 2) ChildFrameHeight = ImGui.GetWindowHeight() - 210;
            if (trueCount == 1) ChildFrameHeight = ImGui.GetWindowHeight() - 150;
            if (trueCount == 0) ChildFrameHeight = ImGui.GetWindowHeight() - 85;
        }

        if (showSortOptions) if (isTimeFilterEnabled) ChildFrameHeight -= 35;

        return ChildFrameHeight;
    }
}
