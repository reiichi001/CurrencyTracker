using CurrencyTracker.Manager;
using CurrencyTracker.Manager.Trackers;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using TinyPinyin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Runtime.InteropServices;
namespace CurrencyTracker.Windows;

public partial class Main : Window, IDisposable
{
    public Main(Plugin plugin) : base("Currency Tracker")
    {
        Flags |= ImGuiWindowFlags.NoScrollbar;
        Flags |= ImGuiWindowFlags.NoScrollWithMouse;

        Initialize(plugin);
    }

    public void Dispose()
    {
        searchTimer.Elapsed -= SearchTimerElapsed;
        searchTimer.Stop();
    }

    // 初始化 Initialize
    private void Initialize(Plugin plugin)
    {
        isReversed = C.ReverseSort;
        recordMode = C.TrackMode;
        transactionsPerPage = C.RecordsPerPage;
        ordedOptions = C.OrdedOptions;
        hiddenOptions = C.HiddenOptions;
        isChangeColoring = C.ChangeTextColoring;
        positiveChangeColor = C.PositiveChangeColor;
        negativeChangeColor = C.NegativeChangeColor;
        exportDataFileType = C.ExportDataFileType;
        isShowLocationColumn = C.ShowLocationColumn;
        isShowNoteColumn = C.ShowNoteColumn;
        isShowOrderColumn = C.ShowOrderColumn;

        // 临时 Temp
        isRecordContentName = C.RecordContentName;
        isRecordTeleportDes = C.RecordTeleportDes;
        isRecordTeleport = C.RecordTeleport;
        isTrackinDuty = C.TrackedInDuty;
        isWaitExComplete = C.WaitExComplete;
        isRecordMiniCactpot = C.RecordMiniCactpot;
        isRecordTripleTriad = C.RecordTripleTriad;

        if (filterEndDate.Month == 1 && filterEndDate.Day == 1) filterStartDate = new DateTime(DateTime.Now.Year - 1, 12, 31);
        else filterStartDate = filterStartDate = filterEndDate.AddDays(-1);

        searchTimer.Elapsed += SearchTimerElapsed;
        searchTimer.AutoReset = false;

        LoadOptions();
        LoadCustomMinTrackValue();
    }

    // 将预置货币类型、玩家自定义的货币类型加入选项列表 Add preset currencies and player-customed currencies to the list of options
    private void LoadOptions()
    {
        HashSet<string> addedOptions = new HashSet<string>();

        foreach (var currency in Tracker.CurrencyType)
        {
            if (CurrencyInfo.presetCurrencies.TryGetValue(currency, out uint currencyID))
            {
                string? currencyName = currencyInfo.CurrencyLocalName(currencyID);

                if (!addedOptions.Contains(currencyName) && !hiddenOptions.Contains(currencyName))
                {
                    permanentCurrencyName.Add(currencyName);
                    options.Add(currencyName);
                    addedOptions.Add(currencyName);
                    selectedStates.Add(currencyName, new List<bool>());
                    selectedTransactions.Add(currencyName, new List<TransactionsConvertor>());
                }
            }
        }

        foreach (var currency in C.CustomCurrencyType)
        {
            if (C.CustomCurrencies.TryGetValue(currency, out _))
            {
                if (!addedOptions.Contains(currency))
                {
                    options.Add(currency);
                    addedOptions.Add(currency);
                    selectedStates.Add(currency, new List<bool>());
                    selectedTransactions.Add(currency, new List<TransactionsConvertor>());
                }
            }
        }

        if (ordedOptions == null)
        {
            ordedOptions = options;
            C.OrdedOptions = ordedOptions;
            C.Save();
        }
        else
        {
            ReloadOrderedOptions();
        }
    }

    // 初始化自定义货币最小记录值 Initialize Min Track Values
    private void LoadCustomMinTrackValue()
    {
        HashSet<string> addedCurrencies = new HashSet<string>();
        foreach (var currency in options)
        {
            if (C.MinTrackValueDic["InDuty"].ContainsKey(currency) && C.MinTrackValueDic["OutOfDuty"].ContainsKey(currency))
                continue;
            if (!addedCurrencies.Contains(currency))
            {
                C.MinTrackValueDic["InDuty"].Add(currency, 0);
                C.MinTrackValueDic["OutOfDuty"].Add(currency, 0);
                C.Save();
                addedCurrencies.Add(currency);
            }
        }
    }

    public override void Draw()
    {
        if (!Service.ClientState.IsLoggedIn) return;

        if (!showRecordOptions) ImGui.TextColored(ImGuiColors.DalamudGrey, Service.Lang.GetText("ConfigLabel1"));
        else ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("ConfigLabel1"));
        if (ImGui.IsItemClicked())
        {
            showRecordOptions = !showRecordOptions;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Service.Lang.GetText("ConfigLabelHelp"));
        }
        if (showRecordOptions)
        {
            TempRecordSettings();
            ImGui.SameLine();
            MinRecordValueInDuty();
            ImGui.SameLine();
            MergeTransactions();
            ImGui.SameLine();
            ClearExceptions();
        }

        if (!showRecordOptions && !showOthers) ImGui.SameLine();

        if (!showOthers) ImGui.TextColored(ImGuiColors.DalamudGrey, Service.Lang.GetText("ConfigLabel2"));
        else ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("ConfigLabel2"));
        if (ImGui.IsItemClicked())
        {
            showOthers = !showOthers;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Service.Lang.GetText("ConfigLabelHelp"));
        }
        if (showOthers)
        {
            ExportData();
            ImGui.SameLine();
            OpenDataFolder();
            ImGui.SameLine();
            OpenGitHubPage();
            ImGui.SameLine();
            HelpPages();
            ImGui.SameLine();
            LanguageSwitch();
            if (P.PluginInterface.IsDev)
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
        /*
        ImGui.SameLine();
        if (!Service.ClientState.IsPvP)
        {
            ImGui.Text($"当前区域ID:{Service.ClientState.TerritoryType}, 当前区域名: {Tracker.TerritoryNames[Service.ClientState.TerritoryType]}");
            ImGui.SameLine();
            if (Tracker.IsBoundByDuty())
            {
                ImGui.Text($"副本名:{Tracker.ContentNames[Service.ClientState.TerritoryType]}");
            }
        }

        ImGui.SameLine();
        var Wards = new int[]
        {
            425, 426, 427, 2341, 3687
        };
        Dictionary<uint, string> WardNamesDE = Service.DataManager.GetExcelSheet<PlaceName>(Dalamud.ClientLanguage.German)
                .Where(x => x.RowId == 425 || x.RowId == 426 || x.RowId == 427 || x.RowId == 2341 || x.RowId == 3687)
                .ToDictionary(
                x => x.RowId,
                x => x.Name.RawString);

        Dictionary<uint, string> WardNamesFR = Service.DataManager.GetExcelSheet<PlaceName>(Dalamud.ClientLanguage.French)
                .Where(x => x.RowId == 425 || x.RowId == 426 || x.RowId == 427 || x.RowId == 2341 || x.RowId == 3687)
                .ToDictionary(
                x => x.RowId,
                x => x.Name.RawString
            );
        if (ImGui.Button("德语法语"))
        {
            var name = string.Empty;
            foreach (var entry in WardNamesDE)
            {
                name += $"{entry.Value},";
            }
            name += "\n";
            foreach (var entry in WardNamesFR)
            {
                name += $"{entry.Value},";
            }
            ImGui.SetClipboardText(name);
        }

        
        if (ImGui.Button("获取测试数据"))
        {
            testResult = currencyInfo.GetRetainerAmount(1);
            testResult2 = currencyInfo.GetRetainerID();
        }
        ImGui.SameLine();
        ImGui.Text($"测试1:{testResult}测试2:{testResult2}");
        */
    }

    // (临时)记录设置 (Temp)Record Settings
    private void TempRecordSettings()
    {
        if (ImGui.Button(Service.Lang.GetText("RecordSettings") + "[DEV]"))
        {
            ImGui.OpenPopup("RecordSettings");
        }

        if (ImGui.BeginPopup("RecordSettings"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("Content"));
            ImGui.Separator();

            if (ImGui.Checkbox(Service.Lang.GetText("TrackInDuty"), ref isTrackinDuty))
            {
                C.TrackedInDuty = isTrackinDuty;
                C.Save();
                if (isTrackinDuty)
                {
                    Service.Tracker.UninitDutyRewards();
                    Service.Tracker.InitDutyRewards();
                }
                else
                {
                    Service.Tracker.UninitDutyRewards();
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("TrackInDutyHelp"));

            if (isTrackinDuty)
            {
                ImGui.BulletText("");
                ImGui.SameLine();
                if (ImGui.Checkbox(Service.Lang.GetText("RecordContentName"), ref isRecordContentName))
                {
                    C.RecordContentName = isRecordContentName;
                    C.Save();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Service.Lang.GetText("RecordContentNameHelp"));
            }

            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("Overworld"));
            ImGui.Separator();
            if (ImGui.Checkbox(Service.Lang.GetText("RecordTPCosts"), ref isRecordTeleport))
            {
                C.RecordTeleport = isRecordTeleport;
                C.Save();

                if (isRecordTeleport)
                {
                    Service.Tracker.UninitTeleportCosts();
                    Service.Tracker.InitTeleportCosts();
                }
                else
                {
                    Service.Tracker.UninitTeleportCosts();
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("RecordTPCostsHelp"));

            if (isRecordTeleport)
            {
                ImGui.BulletText("");
                ImGui.SameLine();
                if (ImGui.Checkbox(Service.Lang.GetText("RecordTPDest"), ref isRecordTeleportDes))
                {
                    C.RecordTeleportDes = isRecordTeleportDes;
                    C.Save();
                }
            }

            if (ImGui.Checkbox(Service.Lang.GetText("WaitExchange"), ref isWaitExComplete))
            {
                C.WaitExComplete = isWaitExComplete;
                C.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("WaitExchangeHelp"));

            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("GoldSaucer"));
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("RecordGoldSaucerHelp"));
            ImGui.Separator();

            if (ImGui.Checkbox(Service.Lang.GetText("RecordMiniCactpot"), ref isRecordMiniCactpot))
            {
                C.RecordMiniCactpot = isRecordMiniCactpot;
                C.Save();
            }

            if (ImGui.Checkbox(Service.Lang.GetText("RecordTripleTriad"), ref isRecordTripleTriad))
            {
                C.RecordTripleTriad = isRecordTripleTriad;
                C.Save();
            }

            ImGui.EndPopup();
        }
    }

    // 帮助页面 Help Pages
    private void HelpPages()
    {
        if (Widgets.IconButton(FontAwesomeIcon.Question, $"{Service.Lang.GetText("NeedHelp")}?", "NeedHelp"))
        {
            ImGui.OpenPopup("NeedHelp");
        }

        if (ImGui.BeginPopup("NeedHelp"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Guide")}:");
            ImGui.Separator();
            if (ImGui.Button($"{Service.Lang.GetText("OperationGuide")} (GitHub)"))
            {
                Widgets.OpenUrl("https://github.com/AtmoOmen/CurrencyTracker/wiki/Operations");
            }

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("SuggestOrReport")}?");
            ImGui.Separator();
            ImGui.Text("GitHub - AtmoOmen, Discord - AtmoOmen#0");
            if (ImGui.Button("GitHub Issue"))
            {
                Widgets.OpenUrl("https://github.com/AtmoOmen/CurrencyTracker/issues");
            }
            ImGui.SameLine();
            if (ImGui.Button("Discord Thread"))
            {
                Widgets.OpenUrl("https://discord.com/channels/581875019861328007/1019646133305344090/threads/1163039624957010021");
            }
            if (C.SelectedLanguage == "ChineseSimplified")
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "请加入下面的 QQ 频道，在 XIVLauncher/Dalamud 分栏下\n" +
                    "选择 插件问答帮助 频道，然后 @AtmoOmen 向我提问\n" +
                    "(如果你是国服用户, 请注意, 你的问题/建议可能已在更新的版本中已被修复/采纳)");
                if (ImGui.Button("QQ频道【艾欧泽亚泛獭保护协会】"))
                {
                    Widgets.OpenUrl("https://pd.qq.com/s/fttirpnql");
                }
            }
            
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("HelpTranslate")}!");
            ImGui.Separator();
            if (ImGui.Button($"Crowdin"))
            {
                Widgets.OpenUrl("https://crowdin.com/project/dalamud-currencytracker");
            }
            ImGui.SameLine();
            ImGui.Text($"{Service.Lang.GetText("HelpTranslateHelp")}!");            

            ImGui.EndPopup();
        }
    }

    // 倒序排序 Reverse Sort
    private void ReverseSort()
    {
        ImGui.SetCursorPosX(Widgets.SetColumnCenterAligned("     ", 0, 8));
        if (isReversed)
        {
            if (ImGui.ArrowButton("UpSort", ImGuiDir.Up))
            {
                isReversed = !isReversed;
                C.ReverseSort = isReversed;
                C.Save();
                searchTimer.Stop();
                searchTimer.Start();
            }
        }
        else
        {
            if (ImGui.ArrowButton("DownSort", ImGuiDir.Down))
            {
                isReversed = !isReversed;
                C.ReverseSort = isReversed;
                C.Save();
                searchTimer.Stop();
                searchTimer.Start();
            }
        }
    }

    // 与时间相关的功能 Functions related to Time
    private void TimeFunctions()
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("TimeFunctions");
        }

        if (ImGui.BeginPopup("TimeFunctions", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
        {
            if (ImGui.Checkbox(Service.Lang.GetText("ClusterByTime"), ref isClusteredByTime))
            {
                searchTimer.Stop();
                searchTimer.Start();
            }

            if (isClusteredByTime)
            {
                ImGui.SetNextItemWidth(115);
                if (ImGui.InputInt(Service.Lang.GetText("ClusterInterval"), ref clusterHour, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (clusterHour <= 0)
                    {
                        clusterHour = 0;
                    }
                    searchTimer.Stop();
                    searchTimer.Start();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker($"{Service.Lang.GetText("CurrentSettings")}:\n" +
                    $"{Service.Lang.GetText("ClusterByTimeHelp1")} {clusterHour} {Service.Lang.GetText("ClusterByTimeHelp2")}");
            }

            if (ImGui.Checkbox($"{Service.Lang.GetText("FilterByTime")}##TimeFilter", ref isTimeFilterEnabled))
            {
                searchTimer.Stop();
                searchTimer.Start();
            }

            var StartDateString = filterStartDate.ToString("yyyy-MM-dd");
            ImGui.SetNextItemWidth(120);
            ImGui.InputText(Service.Lang.GetText("StartDate"), ref StartDateString, 100, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemClicked())
            {
                startDateEnable = !startDateEnable;
                if (endDateEnable) endDateEnable = false;
            }

            var EndDateString = filterEndDate.ToString("yyyy-MM-dd");
            ImGui.SetNextItemWidth(120);
            ImGui.InputText(Service.Lang.GetText("EndDate"), ref EndDateString, 100, ImGuiInputTextFlags.ReadOnly);
            if (ImGui.IsItemClicked())
            {
                endDateEnable = !endDateEnable;
                if (startDateEnable) startDateEnable = false;
            }

            if (startDateEnable)
            {
                CreateDatePicker(ref filterStartDate, true);
            }

            if (endDateEnable)
            {
                CreateDatePicker(ref filterEndDate, false);
            }

            ImGui.EndPopup();
        }
    }

    // 与地点相关的功能 Functions related to Location
    private void LocationFunctions()
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("LocationSearch");
        }
        if (ImGui.BeginPopup("LocationSearch"))
        {
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##LocationSearch", Service.Lang.GetText("PleaseSearch"), ref searchLocationName, 80))
            {
                if (!searchLocationName.IsNullOrEmpty())
                {
                    isLocationFilterEnabled = true;
                    searchTimer.Stop();
                    searchTimer.Start();
                }
                else
                {
                    isLocationFilterEnabled = false;
                    searchTimer.Stop();
                    UpdateTransactions();
                }
            }

            ImGui.EndPopup();
        }
    }

    // 与备注相关的功能 Functions related to Note
    private void NoteFunctions()
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("NoteSearch");
        }
        if (ImGui.BeginPopup("NoteSearch"))
        {
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##NoteSearch", Service.Lang.GetText("PleaseSearch"), ref searchNoteContent, 80))
            {
                if (!searchNoteContent.IsNullOrEmpty())
                {
                    isNoteFilterEnabled = true;
                    searchTimer.Stop();
                    searchTimer.Start();
                }
                else
                {
                    isNoteFilterEnabled = false;
                    searchTimer.Stop();
                    UpdateTransactions();
                }
            }

            ImGui.EndPopup();
        }
    }

    // 与收支相关的功能 Functions related to Change
    private void ChangeFunctions()
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup("ChangeFunctions");
        }

        if (ImGui.BeginPopup("ChangeFunctions"))
        {
            if (ImGui.Checkbox($"{Service.Lang.GetText("ChangeFilterEnabled")}##ChangeFilter", ref isChangeFilterEnabled))
            {
                searchTimer.Stop();
                searchTimer.Start();
            }

            if (isChangeFilterEnabled)
            {
                if (ImGui.RadioButton($"{Service.Lang.GetText("Greater")}##FilterMode", ref filterMode, 0))
                {
                    searchTimer.Stop();
                    searchTimer.Start();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"{Service.Lang.GetText("Less")}##FilterMode", ref filterMode, 1))
                {
                    searchTimer.Stop();
                    searchTimer.Start();
                }

                ImGui.SetNextItemWidth(130);
                if (ImGui.InputInt($"##FilterValue", ref filterValue, 100, 100000, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    searchTimer.Stop();
                    searchTimer.Start();
                }
                ImGuiComponents.HelpMarker($"{Service.Lang.GetText("CurrentSettings")}:\n{Service.Lang.GetText("ChangeFilterLabel")} {(Service.Lang.GetText(filterMode == 0 ? "Greater" : filterMode == 1 ? "Less" : ""))} {filterValue} {Service.Lang.GetText("ChangeFilterValueLabel")}");
            }

            if (ImGui.Checkbox($"{Service.Lang.GetText("ChangeTextColoring")}##ChangeColoring", ref isChangeColoring))
            {
                C.ChangeTextColoring = isChangeColoring;
                C.Save();
            }

            if (isChangeColoring)
            {
                if (ImGui.ColorButton("##PositiveColor", positiveChangeColor))
                {
                    ImGui.OpenPopup("PositiveColor");
                }
                ImGui.SameLine();
                ImGui.Text(Service.Lang.GetText("PositiveChange"));

                if (ImGui.BeginPopup("PositiveColor"))
                {
                    if (ImGui.ColorPicker4("", ref positiveChangeColor))
                    {
                        isChangeColoring = true;
                        C.ChangeTextColoring = isChangeColoring;
                        C.PositiveChangeColor = positiveChangeColor;
                        C.Save();
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                if (ImGui.ColorButton("##NegativeColor", negativeChangeColor))
                {
                    ImGui.OpenPopup("NegativeColor");
                }
                ImGui.SameLine();
                ImGui.Text(Service.Lang.GetText("NegativeChange"));

                if (ImGui.BeginPopup("NegativeColor"))
                {
                    if (ImGui.ColorPicker4("", ref negativeChangeColor))
                    {
                        isChangeColoring = true;
                        C.ChangeTextColoring = isChangeColoring;
                        C.NegativeChangeColor = negativeChangeColor;
                        C.Save();
                    }
                    ImGui.EndPopup();
                }
            }

            ImGui.EndPopup();
        }
    }

    // 最小记录值 Minimum Change Permitted to Create a New Transaction
    private void MinRecordValueInDuty()
    {
        if (ImGui.Button(Service.Lang.GetText("MinimumRecordValue")))
        {
            if (selectedCurrencyName != null)
            {
                ImGui.OpenPopup("MinTrackValue");
                inDutyMinTrackValue = C.MinTrackValueDic["InDuty"][selectedCurrencyName];
                outDutyMinTrackValue = C.MinTrackValueDic["OutOfDuty"][selectedCurrencyName];
            }
            else
            {
                Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
                return;
            }
        }

        if (ImGui.BeginPopup("MinTrackValue"))
        {
            if (selectedCurrencyName != null)
            {
                ImGui.Text($"{Service.Lang.GetText("Now")}:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, selectedCurrencyName);
                ImGui.SameLine(10);
                ImGuiComponents.HelpMarker($"{Service.Lang.GetText("Current Settings")}:\n\n" +
                    $"{Service.Lang.GetText("MinimumRecordValueHelp")} {C.MinTrackValueDic["InDuty"][selectedCurrencyName]}\n" +
                    $"{Service.Lang.GetText("MinimumRecordValueHelp1")} {C.MinTrackValueDic["OutOfDuty"][selectedCurrencyName]}\n" +
                    $"{Service.Lang.GetText("MinimumRecordValueHelp2")}");

                ImGui.Separator();
                ImGui.Text($"{Service.Lang.GetText("MinimumRecordValueLabel")}{C.MinTrackValueDic["InDuty"][selectedCurrencyName]}");
                ImGui.SetNextItemWidth(175);

                var flags = C.TrackedInDuty ? 0 : ImGuiInputTextFlags.ReadOnly;

                if (ImGui.InputInt("##MinInDuty", ref inDutyMinTrackValue, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue | flags))
                {
                    C.MinTrackValueDic["InDuty"][selectedCurrencyName] = inDutyMinTrackValue;
                    C.Save();
                }

                if (inDutyMinTrackValue < 0) inDutyMinTrackValue = 0;

                ImGui.Text($"{Service.Lang.GetText("MinimumRecordValueLabel1")}{C.MinTrackValueDic["OutOfDuty"][selectedCurrencyName]}");
                ImGui.SetNextItemWidth(175);
                if (ImGui.InputInt("##MinOutDuty", ref outDutyMinTrackValue, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    C.MinTrackValueDic["OutOfDuty"][selectedCurrencyName] = outDutyMinTrackValue;
                    C.Save();
                }
                if (inDutyMinTrackValue < 0) inDutyMinTrackValue = 0;
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
        if (Widgets.IconButton(FontAwesomeIcon.Plus, Service.Lang.GetText("Add"), "CustomCurrencyAdd"))
        {
            ImGui.OpenPopup("CustomCurrency");
        }

        if (ImGui.BeginPopup("CustomCurrency", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("CustomCurrencyTracker"));
            ImGuiComponents.HelpMarker(Service.Lang.GetText("CustomCurrencyHelp"));
            ImGui.Text($"{Service.Lang.GetText("Now")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(210);

            if (ImGui.BeginCombo("", Tracker.ItemNames.TryGetValue(customCurrency, out var selected) ? selected : Service.Lang.GetText("PleaseSelect"), ImGuiComboFlags.HeightLarge))
            {
                int startIndex = currentItemPage * itemsPerPage;
                int endIndex = Math.Min(startIndex + itemsPerPage, Tracker.ItemNames.Count);

                ImGui.SetNextItemWidth(200f);
                ImGui.InputTextWithHint("##selectflts", Service.Lang.GetText("PleaseSearch"), ref searchFilter, 50);
                ImGui.SameLine();
                if (Widgets.IconButton(FontAwesomeIcon.Backward))
                    currentItemPage = 0;
                ImGui.SameLine();
                if (ImGui.ArrowButton("CustomPreviousPage", ImGuiDir.Left) && currentItemPage > 0)
                    currentItemPage--;
                ImGui.SameLine();
                if (ImGui.ArrowButton("CustomNextPage", ImGuiDir.Right))
                    currentItemPage++;
                ImGui.SameLine();

                if ((ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && ImGui.GetIO().MouseWheel > 0) && currentItemPage > 0)
                    currentItemPage--;

                if ((ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && ImGui.GetIO().MouseWheel < 0))
                    currentItemPage++;

                ImGui.Separator();

                int visibleItems = 0;

                foreach (var x in Tracker.ItemNames)
                {
                    var itemName = x.Value.Normalize(NormalizationForm.FormKC);

                    var isChineseSimplified = C.SelectedLanguage == "ChineseSimplified";
                    var pinyin = isChineseSimplified ? PinyinHelper.GetPinyin(itemName, "") : string.Empty;

                    if (options.All(y => !itemName.Contains(y)) && (!filterNamesForCCT.Any(filter => itemName.Contains(filter))))
                    {
                        if (string.IsNullOrWhiteSpace(searchFilter) || (isChineseSimplified ? pinyin.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) : itemName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            if (visibleItems >= startIndex && visibleItems < endIndex)
                            {
                                if (ImGui.Selectable(x.Value))
                                {
                                    customCurrency = x.Key;
                                }

                                if (ImGui.IsWindowAppearing() && customCurrency == x.Key)
                                {
                                    ImGui.SetScrollHereY();
                                }
                            }
                            visibleItems++;
                        }

                        if (visibleItems >= endIndex)
                        {
                            break;
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();

            if (Widgets.IconButton(FontAwesomeIcon.Plus, "None", "AddCustomCurrency"))
            {
                if (string.IsNullOrEmpty(selected))
                {
                    Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
                    return;
                }

                if (options.Contains(selected))
                {
                    Service.Chat.PrintError(Service.Lang.GetText("CustomCurrencyHelp1"));
                    return;
                }

                if (!C.CustomCurrencyType.Contains(selected))
                {
                    C.CustomCurrencyType.Add(selected);
                }

                if (C.CustomCurrencyType.Contains(selected) && !C.CustomCurrencies.ContainsKey(selected))
                {
                    C.CustomCurrencies.Add(selected, customCurrency);
                }

                if (!C.MinTrackValueDic["InDuty"].ContainsKey(selected) && !C.MinTrackValueDic["OutOfDuty"].ContainsKey(selected))
                {
                    C.MinTrackValueDic["InDuty"].Add(selected, 0);
                    C.MinTrackValueDic["OutOfDuty"].Add(selected, 0);
                }
                C.Save();
                options.Add(selected);
                selectedStates.Add(selected, new List<bool>());
                selectedTransactions.Add(selected, new List<TransactionsConvertor>());
                ReloadOrderedOptions();

                if (recordMode == 1)
                {
                    Service.Tracker.InitializeChatTracking();
                    Service.Tracker.OnTransactionsUpdate(EventArgs.Empty);
                }

                selectedOptionIndex = ordedOptions.Count - 1;
                selectedCurrencyName = selected;
                currentTypeTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                lastTransactions = currentTypeTransactions;

                customCurrency = 0;

                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    // 按临界值合并记录 Merge Transactions By Threshold
    private void MergeTransactions()
    {
        transactions ??= new Transactions();

        if (ImGui.Button(Service.Lang.GetText("MergeTransactionsLabel")))
        {
            ImGui.OpenPopup("MergeTransactions");
        }

        if (ImGui.BeginPopup("MergeTransactions"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("MergeTransactionsLabel4"));
            ImGui.Text(Service.Lang.GetText("Threshold"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            ImGui.InputInt("##MergeThreshold", ref mergeThreshold, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue);
            if (mergeThreshold < 0)
            {
                mergeThreshold = 0;
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Service.Lang.GetText("MergeTransactionsHelp3")}{Service.Lang.GetText("TransactionsHelp2")}");

            // 双向合并 Two-Way Merge
            if (ImGui.Button(Service.Lang.GetText("TwoWayMerge")))
            {
                int mergeCount = MergeTransactions(false);
                if (mergeCount == 0)
                    return;
            }

            ImGui.SameLine();

            // 单向合并 One-Way Merge
            if (ImGui.Button(Service.Lang.GetText("OneWayMerge")))
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
        if (ImGui.Button(Service.Lang.GetText("ClearExTransactionsLabel")))
        {
            ImGui.OpenPopup("ClearExceptionNote");
        }

        if (ImGui.BeginPopup("ClearExceptionNote"))
        {
            if (ImGui.Button(Service.Lang.GetText("Confirm")))
            {
                if (string.IsNullOrEmpty(selectedCurrencyName))
                {
                    Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
                    return;
                }

                var removedCount = transactions.ClearExceptionRecords(selectedCurrencyName);
                if (removedCount > 0)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("ClearExTransactionsHelp2")}{removedCount}{Service.Lang.GetText("ClearExTransactionsHelp3")}");
                    UpdateTransactions();
                }
                else
                {
                    Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp"));
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Service.Lang.GetText("ClearExTransactionsHelp")}{Service.Lang.GetText("ClearExTransactionsHelp1")}{Service.Lang.GetText("TransactionsHelp2")}");
            ImGui.EndPopup();
        }
    }

    // 导出数据为.CSV文件 Export Transactions To a .csv File
    private void ExportData()
    {
        if (ImGui.Button(Service.Lang.GetText("Export")))
        {
            ImGui.OpenPopup(str_id: "ExportFileRename");
        }

        if (ImGui.BeginPopup("ExportFileRename"))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("ExportFileType")}:");
            ImGui.SameLine();

            if (ImGui.RadioButton(".csv", ref exportDataFileType, 0))
            {
                C.ExportDataFileType = exportDataFileType;
                C.Save();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton(".md", ref exportDataFileType, 1))
            {
                C.ExportDataFileType = exportDataFileType;
                C.Save();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("ExportFileHelp"));

            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("FileRenameLabel"));
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(Service.Lang.GetText("ExportFileHelp1"));
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputText($"_{selectedCurrencyName}_{Service.Lang.GetText("FileRenameLabel2")}{(exportDataFileType == 0 ? ".csv" : ".md")}", ref fileName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (selectedCurrencyName == null)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
                    return;
                }
                if (currentTypeTransactions == null || currentTypeTransactions.Count == 0)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("ExportCsvMessage1"));
                    return;
                }
                var filePath = transactions.ExportData(currentTypeTransactions, fileName, selectedCurrencyName, exportDataFileType);
                Service.Chat.Print($"{Service.Lang.GetText("ExportCsvMessage3")}{filePath}");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{Service.Lang.GetText("FileRenameHelp1")}{selectedCurrencyName}_{Service.Lang.GetText("FileRenameLabel2")}.csv");
            }
            ImGui.EndPopup();
        }
    }

    // 打开数据文件夹 Open Folder Containing Data Files
    private void OpenDataFolder()
    {
        if (ImGui.Button(Service.Lang.GetText("OpenDataFolder")))
        {
            var playerName = Service.ClientState.LocalPlayer?.Name?.TextValue;
            var serverName = Service.ClientState.LocalPlayer?.HomeWorld?.GameData?.Name;
            string playerDataFolder = Path.Join(P.PluginInterface.ConfigDirectory.FullName, $"{playerName}_{serverName}");

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
                    Service.PluginLog.Error("Unsupported OS");
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error($"Error :{ex.Message}");
            }
        }
    }

    // 打开插件 GitHub 页面 Open Plugin GitHub Page
    private void OpenGitHubPage()
    {
        if (ImGui.Button("GitHub"))
        {
            Widgets.OpenUrl("https://github.com/AtmoOmen/CurrencyTracker");
        }
    }

    // 界面语言切换功能 Language Switch
    private void LanguageSwitch()
    {
        var AvailableLangs = Service.Lang.AvailableLanguage();

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
                    Service.Chat.PrintError(Service.Lang.GetText("UnknownCurrency"));
                    return;
                }
                if (ImGui.Button(langname))
                {
                    Service.Lang = new LanguageManager(language);

                    playerLang = language;

                    C.SelectedLanguage = playerLang;
                    C.Save();
                }
            }
            ImGui.EndPopup();
        }
    }

    // 记录模式切换(已废弃) Record Mode Change (Abandoned)
    private void RecordMode()
    {
        /*
        if (ImGui.Button($"{Service.Lang.GetText("TrackModeLabel")}"))
        {
            ImGui.OpenPopup("RecordMode");
        }
        if (ImGui.BeginPopup("RecordMode"))
        {
            if (ImGui.RadioButton($"{Service.Lang.GetText("TrackModeLabel1")}##RecordMode", ref recordMode, 0))
            {
                C.TrackMode = recordMode;
                C.Save();
                Service.Tracker.ChangeTracker();
            }
            if (recordMode == 0)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(135);
                if (ImGui.InputInt($"{Service.Lang.GetText("TrackModeLabel3")}##TimerInterval", ref timerInterval, 100, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (timerInterval < 100) timerInterval = 100;
                    C.TimerInterval = timerInterval;
                    C.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{Service.Lang.GetText("TrackModeHelp3")}");
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Service.Lang.GetText("TrackModeHelp")} {timerInterval} {Service.Lang.GetText("TrackModeHelp1")}");
            if (ImGui.RadioButton($"{Service.Lang.GetText("TrackModeLabel2")}##RecordMode", ref recordMode, 1))
            {
                C.TrackMode = recordMode;
                C.Save();
                Service.Tracker.ChangeTracker();
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker($"{Service.Lang.GetText("TrackModeHelp2")}");
            ImGui.EndPopup();
        }
        */
    }

    // 货币列表顶端工具栏 Listbox tools
    private void ListboxTools()
    {
        ImGui.SetCursorPosX(25);
        CustomCurrencyTracker();

        ImGui.SameLine();

        if (ImGui.ArrowButton("UpArrow", ImGuiDir.Up) && selectedOptionIndex > 0)
        {
            SwapOptions(selectedOptionIndex, selectedOptionIndex - 1);
            selectedOptionIndex--;
        }

        ImGui.SameLine();
        {
            if (string.IsNullOrWhiteSpace(selectedCurrencyName) || selectedOptionIndex == -1)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                Widgets.IconButton(FontAwesomeIcon.EyeSlash);
                ImGui.PopStyleVar();
            }
            else if (!permanentCurrencyName.Contains(selectedCurrencyName))
            {
                Widgets.IconButton(FontAwesomeIcon.Trash, "Delete\n(Double Right-Click)", "ToolsDelete");
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
                {
                    if (string.IsNullOrEmpty(selectedCurrencyName))
                    {
                        Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
                        return;
                    }
                    if (!options.Contains(selectedCurrencyName))
                    {
                        Service.Chat.PrintError(Service.Lang.GetText("CustomCurrencyHelp2"));
                        return;
                    }
                    C.CustomCurrencies.Remove(selectedCurrencyName);
                    C.CustomCurrencyType.Remove(selectedCurrencyName);
                    C.Save();
                    options.Remove(selectedCurrencyName);
                    selectedStates.Remove(selectedCurrencyName);
                    selectedTransactions.Remove(selectedCurrencyName);
                    ReloadOrderedOptions();
                    selectedCurrencyName = string.Empty;
                }
            }
            else
            {
                Widgets.IconButton(FontAwesomeIcon.EyeSlash, Service.Lang.GetText("Hide"));
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
                {
                    if (string.IsNullOrWhiteSpace(selectedCurrencyName) || selectedOptionIndex == -1 || !permanentCurrencyName.Contains(selectedCurrencyName)) return;

                    options.Remove(selectedCurrencyName);
                    selectedStates[selectedCurrencyName].Clear();
                    selectedTransactions[selectedCurrencyName].Clear();
                    hiddenOptions.Add(selectedCurrencyName);
                    if (!C.HiddenOptions.Contains(selectedCurrencyName))
                        C.HiddenOptions.Add(selectedCurrencyName);
                    C.Save();
                    ReloadOrderedOptions();
                    selectedCurrencyName = string.Empty;
                    selectedOptionIndex = -1;
                }
            }
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
            Widgets.IconButton(FontAwesomeIcon.TrashRestore, Service.Lang.GetText("RestoreHidden"));
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
            {
                if (hiddenOptions.Count == 0)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("OrderChangeHelp"));
                    return;
                }
                HashSet<string> addedOptions = new HashSet<string>();

                foreach (var option in hiddenOptions)
                {
                    if (!addedOptions.Contains(option))
                    {
                        options.Add(option);
                        permanentCurrencyName.Add(option);
                        selectedStates.Add(option, new List<bool>());
                        selectedTransactions.Add(option, new List<TransactionsConvertor>());
                        addedOptions.Add(option);
                    }
                }
                hiddenOptions.Clear();
                C.HiddenOptions.Clear();
                C.Save();
                Service.Chat.Print($"{Service.Lang.GetText("OrderChangeHelp1")} {addedOptions.Count} {Service.Lang.GetText("OrderChangeHelp2")}");
                ReloadOrderedOptions();
            }
        }
    }

    // 图表工具栏 Table Tools
    private void TableTools()
    {
        ImGui.Text($"{Service.Lang.GetText("Now")}: {selectedTransactions[selectedCurrencyName].Count} {Service.Lang.GetText("Transactions")}");
        ImGui.Separator();

        // 取消选择 Unselect
        if (ImGui.Selectable(Service.Lang.GetText("Unselect")))
        {
            if (selectedTransactions[selectedCurrencyName].Count == 0)
            {
                Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                return;
            }
            selectedStates[selectedCurrencyName].Clear();
            selectedTransactions[selectedCurrencyName].Clear();
        }

        // 全选 Select All
        if (ImGui.Selectable(Service.Lang.GetText("SelectAll")))
        {
            selectedTransactions[selectedCurrencyName].Clear();

            foreach (var transaction in currentTypeTransactions)
            {
                selectedTransactions[selectedCurrencyName].Add(transaction);
            }

            for (int i = 0; i < selectedStates[selectedCurrencyName].Count; i++)
            {
                selectedStates[selectedCurrencyName][i] = true;
            }
        }

        // 反选 Inverse Select
        if (ImGui.Selectable(Service.Lang.GetText("InverseSelect")))
        {
            for (var i = 0; i < selectedStates[selectedCurrencyName].Count; i++)
            {
                selectedStates[selectedCurrencyName][i] = !selectedStates[selectedCurrencyName][i];
            }

            foreach (var transaction in currentTypeTransactions)
            {
                var exists = selectedTransactions[selectedCurrencyName].Any(selectedTransaction => Widgets.IsTransactionEqual(selectedTransaction, transaction));

                if (exists)
                {
                    selectedTransactions[selectedCurrencyName].RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                }
                else
                {
                    selectedTransactions[selectedCurrencyName].Add(transaction);
                }
            }
        }

        // 复制 Copy
        if (ImGui.Selectable(Service.Lang.GetText("Copy")))
        {
            string columnData = string.Empty;
            string header = string.Empty;

            if (exportDataFileType == 0)
            {
                header = Service.Lang.GetText("ExportFileCSVHeader");
            }
            else if (exportDataFileType == 1)
            {
                header = Service.Lang.GetText("ExportFileMDHeader1");
            }

            int count = selectedTransactions[selectedCurrencyName].Count;

            columnData += header;

            for (int t = 0; t < count; t++)
            {
                var record = selectedTransactions[selectedCurrencyName][t];
                string change = $"{record.Change:+ #,##0;- #,##0;0}";

                if (exportDataFileType == 0)
                {
                    columnData += $"\n{record.TimeStamp},{record.Amount},{change},{record.LocationName},{record.Note}";
                }
                else if (exportDataFileType == 1)
                {
                    columnData += $"\n{record.TimeStamp} | {record.Amount} | {change} | {record.LocationName} | {record.Note}";
                }
            }

            if (!string.IsNullOrEmpty(columnData))
            {
                ImGui.SetClipboardText(columnData);
                Service.Chat.Print($"{Service.Lang.GetText("CopyTransactionsHelp")} {selectedTransactions[selectedCurrencyName].Count} {Service.Lang.GetText("CopyTransactionsHelp1")}");
            }
            else
            {
                Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                return;
            }
        }

        // 删除 Delete
        if (ImGui.Selectable(Service.Lang.GetText("Delete")))
        {
            if (selectedTransactions[selectedCurrencyName].Count == 0)
            {
                Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                return;
            }
            foreach (var selectedTransaction in selectedTransactions[selectedCurrencyName])
            {
                var playerName = Service.ClientState.LocalPlayer?.Name?.TextValue;
                var serverName = Service.ClientState.LocalPlayer?.HomeWorld?.GameData?.Name;
                string filePath = Path.Combine(P.PluginInterface.ConfigDirectory.FullName, $"{playerName}_{serverName}", $"{selectedCurrencyName}.txt");
                var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                var foundTransaction = editedTransactions.FirstOrDefault(t => Widgets.IsTransactionEqual(t, selectedTransaction));

                if (foundTransaction != null)
                {
                    editedTransactions.Remove(foundTransaction);
                }

                transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
            }
            UpdateTransactions();
        }

        // 导出 Export
        if (ImGui.Selectable(Service.Lang.GetText("Export")))
        {
            if (selectedTransactions[selectedCurrencyName].Count == 0)
            {
                Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                return;
            }
            var filePath = transactions.ExportData(selectedTransactions[selectedCurrencyName], "", selectedCurrencyName, exportDataFileType);
            Service.Chat.Print($"{Service.Lang.GetText("ExportCsvMessage3")}{filePath}");
        }

        // 合并 Merge
        ImGui.Selectable(Service.Lang.GetText("Merge"), ref isOnMergingTT, ImGuiSelectableFlags.DontClosePopups);

        if (isOnMergingTT)
        {
            if (isOnEdit) isOnEdit = !isOnEdit;

            ImGui.Separator();
            

            if (selectedTransactions[selectedCurrencyName].Count != 0)
            {
                editedLocationName = selectedTransactions[selectedCurrencyName].FirstOrDefault().LocationName;
                editedNoteContent = selectedTransactions[selectedCurrencyName].FirstOrDefault().Note;
            }
            else
            {
                editedLocationName = string.Empty;
                editedNoteContent = string.Empty;
            }

            ImGui.Text($"{Service.Lang.GetText("Location")}:");
            ImGui.SetNextItemWidth(210);
            ImGui.InputText("##MergeLocationName", ref editedLocationName, 80);

            ImGui.Text($"{Service.Lang.GetText("Note")}:");
            ImGui.SetNextItemWidth(210);
            ImGui.InputText("##MergeNoteContent", ref editedNoteContent, 150);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{Service.Lang.GetText("MergeNoteHelp")}");
            }

            if (ImGui.SmallButton(Service.Lang.GetText("Confirm")))
            {
                if (selectedTransactions[selectedCurrencyName].Count == 0)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                    return;
                }

                if (selectedTransactions[selectedCurrencyName].Count == 1)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("MergeTransactionsHelp4"));
                    return;
                }

                if (editedLocationName.IsNullOrWhitespace())
                {
                    Service.Chat.PrintError(Service.Lang.GetText("EditHelp1"));
                    return;
                }

                var mergeCount = transactions.MergeSpecificTransactions(selectedCurrencyName, editedLocationName, selectedTransactions[selectedCurrencyName], editedNoteContent.IsNullOrEmpty() ? "-1" : editedNoteContent);
                Service.Chat.Print($"{Service.Lang.GetText("MergeTransactionsHelp1")}{mergeCount}{Service.Lang.GetText("MergeTransactionsHelp2")}");

                UpdateTransactions();
                isOnMergingTT = false;
            }
        }

        // 编辑 Edit
        ImGui.Selectable(Service.Lang.GetText("Edit"), ref isOnEdit, ImGuiSelectableFlags.DontClosePopups);

        if (isOnEdit)
        {
            if (isOnMergingTT) isOnMergingTT = !isOnMergingTT;

            ImGui.Separator();

            if (selectedTransactions[selectedCurrencyName].Count != 0)
            {
                editedLocationName = selectedTransactions[selectedCurrencyName].FirstOrDefault().LocationName;
                editedNoteContent = selectedTransactions[selectedCurrencyName].FirstOrDefault().Note;
            }
            else
            {
                editedLocationName = string.Empty;
                editedNoteContent = string.Empty;
            }

            ImGui.Text($"{Service.Lang.GetText("Location")}:");
            ImGui.SetNextItemWidth(210);
            if (ImGui.InputTextWithHint("##EditLocationName", Service.Lang.GetText("EditHelp"), ref editedLocationName, 80, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (selectedTransactions[selectedCurrencyName].Count == 0)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                    return;
                }

                if (editedLocationName.IsNullOrWhitespace())
                {
                    Service.Chat.PrintError(Service.Lang.GetText("EditHelp1"));
                    return;
                }

                var filePath = Path.Combine(P.PlayerDataFolder, $"{selectedCurrencyName}.txt");
                var failCounts = 0; 

                foreach (var selectedTransaction in selectedTransactions[selectedCurrencyName])
                {
                    var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);

                    var index = -1;
                    for (var i = 0; i < editedTransactions.Count; i++)
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
                    else
                    {
                        failCounts++;
                    }
                }

                if (failCounts == 0)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("EditHelp2")} {selectedTransactions[selectedCurrencyName].Count} {Service.Lang.GetText("EditHelp3")} {editedLocationName}");

                    UpdateTransactions();
                }
                else if (failCounts > 0 && failCounts < selectedTransactions[selectedCurrencyName].Count)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("EditHelp2")} {selectedTransactions[selectedCurrencyName].Count - failCounts} {Service.Lang.GetText("EditHelp3")} {editedLocationName}");
                    Service.Chat.PrintError($"({Service.Lang.GetText("EditFailed")}: {failCounts})");

                    UpdateTransactions();
                }
                else
                {
                    Service.Chat.PrintError($"{Service.Lang.GetText("EditFailed")}");
                }

            }

            ImGui.Text($"{Service.Lang.GetText("Note")}:");
            ImGui.SetNextItemWidth(210);
            if (ImGui.InputTextWithHint("##EditNoteContent", Service.Lang.GetText("EditHelp"), ref editedNoteContent, 80, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (selectedTransactions[selectedCurrencyName].Count == 0)
                {
                    Service.Chat.PrintError(Service.Lang.GetText("NoTransactionsSelected"));
                    return;
                }

                var filePath = Path.Combine(P.PlayerDataFolder, $"{selectedCurrencyName}.txt");
                var failCounts = 0;

                foreach (var selectedTransaction in selectedTransactions[selectedCurrencyName])
                {
                    var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);

                    var index = -1;
                    for (var i = 0; i < editedTransactions.Count; i++)
                    {
                        if (Widgets.IsTransactionEqual(editedTransactions[i], selectedTransaction))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index != -1)
                    {
                        editedTransactions[index].Note = editedNoteContent;
                        transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
                    }
                    else
                    {
                        failCounts++;
                    }
                }

                if (failCounts == 0)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("EditHelp2")} {selectedTransactions[selectedCurrencyName].Count} {Service.Lang.GetText("EditHelp3")} {editedLocationName}");

                    UpdateTransactions();
                }
                else if (failCounts > 0 && failCounts < selectedTransactions[selectedCurrencyName].Count)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("EditHelp2")} {selectedTransactions[selectedCurrencyName].Count - failCounts} {Service.Lang.GetText("EditHelp3")} {editedLocationName}");
                    Service.Chat.PrintError($"({Service.Lang.GetText("EditFailed")}: {failCounts})");

                    UpdateTransactions();
                }
                else
                {
                    Service.Chat.PrintError($"{Service.Lang.GetText("EditFailed")}");
                }
            }

            if (!editedNoteContent.IsNullOrEmpty())
            {
                ImGui.TextWrapped(editedNoteContent);
            }
        }
    }

    // 顶端工具栏 Transactions Paging Tools
    private void TransactionsPagingTools()
    {
        int pageCount = (currentTypeTransactions.Count > 0) ? (int)Math.Ceiling((double)currentTypeTransactions.Count / transactionsPerPage) : 0;
        currentPage = (pageCount > 0) ? Math.Clamp(currentPage, 0, pageCount - 1) : 0;

        if (pageCount == 0)
        {
            if (P.Graph.IsOpen) P.Graph.IsOpen = false;
        }

        // 图表 Graphs
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 360) / 2 - 57 - ImGui.CalcTextSize(Service.Lang.GetText("    ")).X);
        if (Widgets.IconButton(FontAwesomeIcon.ChartBar, Service.Lang.GetText("Graphs")) && pageCount > 0)
        {
            if (selectedCurrencyName != null && currentTypeTransactions.Count != 1 && currentTypeTransactions != null)
            {
                LinePlotData = currentTypeTransactions.Select(x => x.Amount).ToArray();
                P.Graph.IsOpen = !P.Graph.IsOpen;
            }
            else return;
        }

        ImGui.SameLine();

        // 首页 First Page
        float pageButtonPosX = (ImGui.GetWindowWidth() - 360) / 2 - 40;
        ImGui.SetCursorPosX(pageButtonPosX);
        if (Widgets.IconButton(FontAwesomeIcon.Backward))
            currentPage = 0;

        ImGui.SameLine();

        // 上一页 Last Page
        if (ImGui.ArrowButton("PreviousPage", ImGuiDir.Left) && currentPage > 0)
            currentPage--;

        ImGui.SameLine();

        // 页数显示 Pages
        ImGui.Text($"{Service.Lang.GetText("Di")}{currentPage + 1}{Service.Lang.GetText("Page")} / {Service.Lang.GetText("Gong")}{pageCount}{Service.Lang.GetText("Page")}");

        // 每页显示记录条数 Transactions Per Page
        if (ImGui.IsItemClicked())
        {
            ImGui.OpenPopup("TransactionsPerPage");
        }

        if (ImGui.BeginPopup("TransactionsPerPage"))
        {
            ImGui.Text(Service.Lang.GetText("TransactionsPerPage"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);

            if (ImGui.InputInt("##TransactionsPerPage", ref transactionsPerPage))
            {
                transactionsPerPage = Math.Max(transactionsPerPage, 0);
                C.RecordsPerPage = transactionsPerPage;
                C.Save();
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();

        // 下一页 Next Page
        if (ImGui.ArrowButton("NextPage", ImGuiDir.Right) && currentPage < pageCount - 1)
            currentPage++;

        ImGui.SameLine();

        // 尾页 Final Page
        if (Widgets.IconButton(FontAwesomeIcon.Forward) && currentPage >= 0)
            currentPage = pageCount;

        ImGui.SameLine();

        // 表格外观
        if (Widgets.IconButton(FontAwesomeIcon.Table, Service.Lang.GetText("TableAppearance"), "TableAppearance"))
            ImGui.OpenPopup("TableAppearence");

        if (ImGui.BeginPopup("TableAppearence"))
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("ColumnsDisplayed")}:");

            if (ImGui.Checkbox($"{Service.Lang.GetText("Order")}##DisplayOrderColumn", ref isShowOrderColumn))
            {
                C.ShowOrderColumn = isShowOrderColumn;
                C.Save();
            }

            ImGui.SameLine();

            if (ImGui.Checkbox($"{Service.Lang.GetText("Location")}##DisplayLocationColumn", ref isShowLocationColumn))
            {
                C.ShowLocationColumn = isShowLocationColumn;
                C.Save();
            }

            ImGui.SameLine();

            if (ImGui.Checkbox($"{Service.Lang.GetText("Note")}##DisplayNoteColumn", ref isShowNoteColumn))
            {
                C.ShowNoteColumn = isShowNoteColumn;
                C.Save();
            }

            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("TransactionsPerPage"));
            ImGui.SetNextItemWidth(150);

            if (ImGui.InputInt("##TransactionsPerPage", ref transactionsPerPage))
            {
                transactionsPerPage = Math.Max(transactionsPerPage, 0);
                C.RecordsPerPage = transactionsPerPage;
                C.Save();
            }

            ImGui.EndPopup();
        }

        visibleStartIndex = currentPage * transactionsPerPage;
        visibleEndIndex = Math.Min(visibleStartIndex + transactionsPerPage, currentTypeTransactions.Count);

        // 鼠标滚轮控制 Logic controlling Mouse Wheel Filpping
        {
            if (!ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup))
            {
                if ((ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().MouseWheel > 0) && currentPage > 0)
                    currentPage--;

                if ((ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().MouseWheel < 0) && currentPage < pageCount - 1)
                    currentPage++;
            }
        }
    }

    // 存储可用货币名称选项的列表框 Listbox Containing Available Currencies' Name
    private void CurrenciesList()
    {
        var ChildFrameHeight = ChildframeHeightAdjust();

        Vector2 childScale = new Vector2(243, ChildFrameHeight);
        if (ImGui.BeginChildFrame(2, childScale, ImGuiWindowFlags.NoScrollbar))
        {
            ListboxTools();

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

                    currentTypeTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                    lastTransactions = currentTypeTransactions;
                }
            }

            ImGui.EndChildFrame();
        }
    }

    // 显示收支记录 Childframe Used to Show Transactions in Form
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
            currentTypeTransactions = ApplyFilters(currentTypeTransactions);

            TransactionsPagingTools();

            var columnCount = 4 + Convert.ToInt32(isShowOrderColumn) + Convert.ToInt32(isShowLocationColumn) + Convert.ToInt32(isShowNoteColumn);

            if (ImGui.BeginTable("Transactions", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable, new Vector2(ImGui.GetWindowWidth() - 175, 1)))
            {
                if (isShowOrderColumn) ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, ImGui.CalcTextSize((currentTypeTransactions.Count + 1).ToString()).X + 10, 0);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.None, 150, 0);
                ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.None, 130, 0);
                ImGui.TableSetupColumn("Change", ImGuiTableColumnFlags.None, 100, 0);
                if (isShowLocationColumn) ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.None, 150, 0);
                if (isShowNoteColumn) ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.None, 160, 0);
                ImGui.TableSetupColumn("Selected", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, 30, 0);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

                if (isShowOrderColumn)
                {
                    ImGui.TableNextColumn();
                    ReverseSort();
                }

                ImGui.TableNextColumn();

                ImGui.Selectable($" {Service.Lang.GetText("Time")}{CalcNumSpaces()}");
                TimeFunctions();

                ImGui.TableNextColumn();
                ImGui.Text($" {Service.Lang.GetText("Amount")}{CalcNumSpaces()}");

                ImGui.TableNextColumn();
                ImGui.Selectable($" {Service.Lang.GetText("Change")}{CalcNumSpaces()}");
                ChangeFunctions();

                if (isShowLocationColumn)
                {
                    ImGui.TableNextColumn();
                    ImGui.Selectable($" {Service.Lang.GetText("Location")}{CalcNumSpaces()}");
                    LocationFunctions();
                }

                if (isShowNoteColumn)
                {
                    ImGui.TableNextColumn();
                    ImGui.Selectable($" {Service.Lang.GetText("Note")}{CalcNumSpaces()}");
                    NoteFunctions();
                }

                ImGui.TableNextColumn();
                if (Widgets.IconButton(FontAwesomeIcon.EllipsisH))
                {
                    ImGui.OpenPopup("TableTools");
                }

                ImGui.TableNextRow();

                if (currentTypeTransactions.Count > 0)
                {
                    for (int i = visibleStartIndex; i < visibleEndIndex; i++)
                    {
                        var transaction = currentTypeTransactions[i];
                        while (selectedStates[selectedCurrencyName].Count <= i)
                        {
                            selectedStates[selectedCurrencyName].Add(false);
                        }

                        bool selected = selectedStates[selectedCurrencyName][i];

                        // 序号 Order Number
                        if (isShowOrderColumn)
                        {
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
                        }

                        // 时间 Time
                        ImGui.TableNextColumn();
                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl) && ImGui.IsMouseDown(ImGuiMouseButton.Right))
                        {
                            ImGui.Selectable($"{transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss")}##_{i}", selected, ImGuiSelectableFlags.SpanAllColumns);
                            if (ImGui.IsItemHovered())
                            {
                                selectedStates[selectedCurrencyName][i] = selected = true;

                                if (selected)
                                {
                                    bool exists = selectedTransactions[selectedCurrencyName].Any(t => Widgets.IsTransactionEqual(t, transaction));

                                    if (!exists)
                                    {
                                        selectedTransactions[selectedCurrencyName].Add(transaction);
                                    }
                                }
                                else
                                {
                                    selectedTransactions[selectedCurrencyName].RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                                }
                            }
                        }
                        else if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Selectable($"{transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss")}##_{i}", ref selected, ImGuiSelectableFlags.SpanAllColumns))
                            {
                                selectedStates[selectedCurrencyName][i] = selected;

                                if (selected)
                                {
                                    bool exists = selectedTransactions[selectedCurrencyName].Any(t => Widgets.IsTransactionEqual(t, transaction));

                                    if (!exists)
                                    {
                                        selectedTransactions[selectedCurrencyName].Add(transaction);
                                    }
                                }
                                else
                                {
                                    selectedTransactions[selectedCurrencyName].RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                                }
                            }
                        }
                        else
                        {
                            ImGui.Selectable($"{transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss")}##_{i}");
                        }

                        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            ImGui.SetClipboardText(transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"));
                            Service.Chat.Print($"{Service.Lang.GetText("CopiedToClipboard")}: {transaction.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss")}");
                        }

                        // 货币数 Amount
                        ImGui.TableNextColumn();
                        ImGui.Selectable($"{transaction.Amount.ToString("#,##0")}##_{i}");

                        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            ImGui.SetClipboardText(transaction.Amount.ToString("#,##0"));
                            Service.Chat.Print($"{Service.Lang.GetText("CopiedToClipboard")}: {transaction.Amount.ToString("#,##0")}");
                        }

                        // 收支 Change
                        ImGui.TableNextColumn();
                        if (isChangeColoring)
                        {
                            if (transaction.Change > 0)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, positiveChangeColor);
                            }
                            else if (transaction.Change == 0)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
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

                        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            ImGui.SetClipboardText(transaction.Change.ToString("+ #,##0;- #,##0;0"));
                            Service.Chat.Print($"{Service.Lang.GetText("CopiedToClipboard")} : {transaction.Change.ToString("+ #,##0;- #,##0;0")}");
                        }

                        // 地名 Location
                        if (isShowLocationColumn)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Selectable($"{transaction.LocationName}##_{i}");

                            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && !ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                ImGui.OpenPopup($"EditLocationName##_{i}");
                                editedLocationName = transaction.LocationName;
                            }

                            if (ImGui.BeginPopup($"EditLocationName##_{i}"))
                            {
                                if (!editedLocationName.IsNullOrEmpty())
                                {
                                    ImGui.TextWrapped(editedLocationName);
                                }
                                ImGui.SetNextItemWidth(270);
                                if (ImGui.InputText($"##EditLocationContent_{i}", ref editedLocationName, 150, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                                {
                                    var filePath = Path.Combine(P.PlayerDataFolder, $"{selectedCurrencyName}.txt");
                                    var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                                    var index = -1;

                                    for (var d = 0; d < editedTransactions.Count; d++)
                                    {
                                        if (Widgets.IsTransactionEqual(editedTransactions[d], transaction))
                                        {
                                            index = d;
                                            break;
                                        }
                                    }
                                    if (index != -1)
                                    {
                                        editedTransactions[index].LocationName = editedLocationName;
                                        transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
                                        searchTimer.Stop();
                                        searchTimer.Start();
                                    }
                                    else
                                    {
                                        Service.Chat.PrintError($"{Service.Lang.GetText("EditFailed")}");
                                    }
                                }

                                ImGui.EndPopup();
                            }
                        }

                        // 备注 Note
                        if (isShowNoteColumn)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Selectable($"{transaction.Note}##_{i}");

                            if (ImGui.IsItemHovered())
                            {
                                if (!transaction.Note.IsNullOrEmpty())
                                {
                                    ImGui.SetTooltip(transaction.Note);
                                }
                            }

                            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && !ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                ImGui.OpenPopup($"EditTransactionNote##_{i}");
                                editedNoteContent = transaction.Note;
                            }

                            if (ImGui.BeginPopup($"EditTransactionNote##_{i}"))
                            {
                                if (!editedNoteContent.IsNullOrEmpty())
                                {
                                    ImGui.TextWrapped(editedNoteContent);
                                }
                                ImGui.SetNextItemWidth(270);
                                if (ImGui.InputText($"##EditNoteContent_{i}", ref editedNoteContent, 150, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                                {
                                    var filePath = Path.Combine(P.PlayerDataFolder, $"{selectedCurrencyName}.txt");
                                    var editedTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
                                    var index = -1;

                                    for (var d = 0; d < editedTransactions.Count; d++)
                                    {
                                        if (Widgets.IsTransactionEqual(editedTransactions[d], transaction))
                                        {
                                            index = d;
                                            break;
                                        }
                                    }
                                    if (index != -1)
                                    {
                                        editedTransactions[index].Note = editedNoteContent;
                                        transactionsConvertor.WriteTransactionsToFile(filePath, editedTransactions);
                                        searchTimer.Stop();
                                        searchTimer.Start();
                                    }
                                    else
                                    {
                                        Service.Chat.PrintError($"{Service.Lang.GetText("EditFailed")}");
                                    }
                                }

                                ImGui.EndPopup();
                            }
                        }

                        // 勾选框 Checkboxes
                        ImGui.TableNextColumn();
                        if (ImGui.Checkbox($"##select_{i}", ref selected))
                        {
                            selectedStates[selectedCurrencyName][i] = selected;

                            if (selected)
                            {
                                bool exists = selectedTransactions[selectedCurrencyName].Any(t => Widgets.IsTransactionEqual(t, transaction));

                                if (!exists)
                                {
                                    selectedTransactions[selectedCurrencyName].Add(transaction);
                                }
                            }
                            else
                            {
                                selectedTransactions[selectedCurrencyName].RemoveAll(t => Widgets.IsTransactionEqual(t, transaction));
                            }
                        }

                        ImGui.TableNextRow();
                    }

                    if (ImGui.BeginPopup("TableTools"))
                    {
                        TableTools();
                        ImGui.EndPopup();
                    }
                }

                ImGui.EndTable();
            }

            ImGui.EndChildFrame();
        }
    }
}
