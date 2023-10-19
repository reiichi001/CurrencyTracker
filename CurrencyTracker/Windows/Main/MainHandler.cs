using CurrencyTracker.Manager;
using Dalamud.Interface;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using TinyPinyin;

namespace CurrencyTracker.Windows;

public partial class Main
{
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
            if (transaction.TimeStamp >= filterStartDate && transaction.TimeStamp <= filterEndDate.AddDays(1))
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }

    // 按地点名显示交易记录 Hide Unmatched Transactions By Location
    private List<TransactionsConvertor> ApplyLocationFilter(List<TransactionsConvertor> transactions, string query)
    {
        query = query.Normalize(NormalizationForm.FormKC);
        if (query.IsNullOrEmpty())
        {
            return transactions;
        }

        List<TransactionsConvertor> filteredTransactions = new List<TransactionsConvertor>();
        var isChineseSimplified = C.SelectedLanguage == "ChineseSimplified";

        foreach (var transaction in transactions)
        {
            var normalizedLocation = transaction.LocationName.Normalize(NormalizationForm.FormKC);
            var pinyin = isChineseSimplified ? PinyinHelper.GetPinyin(normalizedLocation, "") : string.Empty;

            if (normalizedLocation.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (isChineseSimplified && pinyin.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }

    // 按备注显示交易记录 Hide Unmatched Transactions By Note
    private List<TransactionsConvertor> ApplyNoteFilter(List<TransactionsConvertor> transactions, string query)
    {
        query = query.Normalize(NormalizationForm.FormKC);
        if (query.IsNullOrEmpty())
        {
            return transactions;
        }

        List<TransactionsConvertor> filteredTransactions = new List<TransactionsConvertor>();
        var isChineseSimplified = C.SelectedLanguage == "ChineseSimplified";

        foreach (var transaction in transactions)
        {
            var normalizedNote = transaction.Note.Normalize(NormalizationForm.FormKC);
            var pinyin = isChineseSimplified ? PinyinHelper.GetPinyin(normalizedNote, "") : string.Empty;

            if (normalizedNote.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (isChineseSimplified && pinyin.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                filteredTransactions.Add(transaction);
            }
        }
        return filteredTransactions;
    }


    private void SearchTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateTransactions();
    }

    // 合并交易记录用 Used to simplified merging transactions code
    private int MergeTransactions(bool oneWay)
    {
        if (string.IsNullOrEmpty(selectedCurrencyName))
        {
            Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp1"));
            return 0;
        }

        int threshold = (mergeThreshold == 0) ? int.MaxValue : mergeThreshold;
        int mergeCount = transactions.MergeTransactionsByLocationAndThreshold(selectedCurrencyName, threshold, oneWay);

        if (mergeCount > 0)
            Service.Chat.Print($"{Service.Lang.GetText("MergeTransactionsHelp1")}{mergeCount}{Service.Lang.GetText("MergeTransactionsHelp2")}");
        else
            Service.Chat.PrintError(Service.Lang.GetText("TransactionsHelp"));

        UpdateTransactions();
        return mergeCount;
    }

    // 调整列表框和表格高度用 Used to adjust the height of listbox and chart
    private float ChildframeHeightAdjust()
    {
        var trueCount = Convert.ToInt32(showOthers) + Convert.ToInt32(showRecordOptions);
        var ChildFrameHeight = ImGui.GetWindowHeight() - 245;

        if (trueCount == 2) ChildFrameHeight = ImGui.GetWindowHeight() - 185;
        if (trueCount == 1) ChildFrameHeight = ImGui.GetWindowHeight() - 150;
        if (trueCount == 0) ChildFrameHeight = ImGui.GetWindowHeight() - 85;

        return ChildFrameHeight;
    }

    // 调整文本长度用 Used to adjust the length of the text in header columns.
    private string CalcNumSpaces()
    {
        var fontSize = ImGui.GetFontSize() / 2;
        var numSpaces = (int)(ImGui.GetColumnWidth() / fontSize);
        var spaces = new string('　', numSpaces);

        return spaces;
    }

    // 应用筛选器 Apply Filters
    private List<TransactionsConvertor> ApplyFilters(List<TransactionsConvertor> currentTypeTransactions)
    {
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

        if (isNoteFilterEnabled)
            currentTypeTransactions = ApplyNoteFilter(currentTypeTransactions, searchNoteContent);

        if (isReversed)
        {
            currentTypeTransactions = currentTypeTransactions.OrderByDescending(item => item.TimeStamp).ToList();
        }

        return currentTypeTransactions;
    }

    // 日期筛选器 Date Picker
    private void CreateDatePicker(ref DateTime currentDate, bool enableStartDate)
    {
        ImGui.Separator();

        if (Widgets.IconButton(FontAwesomeIcon.Backward, "None", "LastYear") && enableStartDate)
        {
            currentDate = currentDate.AddYears(-1);
            searchTimer.Stop();
            searchTimer.Start();
        }

        ImGui.SameLine();

        if (ImGui.ArrowButton("LastMonth", ImGuiDir.Left) && enableStartDate)
        {
            currentDate = currentDate.AddMonths(-1);
            searchTimer.Stop();
            searchTimer.Start();
        }

        ImGui.SameLine();
        ImGui.Text($"{currentDate.Year}.{string.Format("{0:MM}", currentDate)}");
        ImGui.SameLine();

        if (ImGui.ArrowButton("NextMonth", ImGuiDir.Right))
        {
            currentDate = currentDate.AddMonths(1);
            searchTimer.Stop();
            searchTimer.Start();
        }

        ImGui.SameLine();

        if (Widgets.IconButton(FontAwesomeIcon.Forward, "None", "NextYear") && enableStartDate)
        {
            currentDate = currentDate.AddYears(1);
            searchTimer.Stop();
            searchTimer.Start();
        }

        if (ImGui.BeginTable("DatePicker", 7, ImGuiTableFlags.NoBordersInBody))
        {
            var weekDaysData = Service.Lang.GetText("WeekDays");
            string[] weekDays = weekDaysData.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var day in weekDays)
            {
                ImGui.TableNextColumn();
                ImGui.Text(day);
            }

            ImGui.TableNextRow(ImGuiTableRowFlags.None);

            DateTime firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            int daysInMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);

            for (int i = 0; i < firstDayOfWeek; i++)
            {
                ImGui.TableNextColumn();
                ImGui.Text("");
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                ImGui.TableNextColumn();
                if (currentDate.Day == day)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.6f, 1.0f, 1.0f));
                    if (ImGui.Selectable(day.ToString(), selectTimeDeco, ImGuiSelectableFlags.DontClosePopups))
                    {
                        currentDate = new DateTime(currentDate.Year, currentDate.Month, day);
                    }
                    ImGui.PopStyleColor();
                }
                else
                {
                    if (enableStartDate && (currentDate.Year == filterEndDate.Year && currentDate.Month == filterEndDate.Month && day >= filterEndDate.Day) || currentDate.Year > filterEndDate.Year || currentDate.Month > filterEndDate.Month)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                        ImGui.Text(day.ToString());
                        ImGui.PopStyleColor();
                    }
                    else if (!enableStartDate && (currentDate.Year == filterStartDate.Year && currentDate.Month == filterStartDate.Month && day <= filterStartDate.Day) || currentDate.Year < filterStartDate.Year || currentDate.Month < filterStartDate.Month)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                        ImGui.Text(day.ToString());
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        if (ImGui.Selectable(day.ToString(), selectTimeDeco, ImGuiSelectableFlags.DontClosePopups))
                        {
                            currentDate = new DateTime(currentDate.Year, currentDate.Month, day);
                            searchTimer.Stop();
                            searchTimer.Start();
                        }
                    }
                }
            }
            ImGui.EndTable();
        }
    }

    // 用于在记录新增时更新记录 Used to update transactions when transactions added
    public void UpdateTransactionsEvent(object sender, EventArgs e)
    {
        if (selectedCurrencyName != null)
        {
            UpdateTransactions();
        }
    }

    // 用于在筛选时更新记录 Used to update transactions
    public void UpdateTransactions(int ifClearSelectedStates = 1)
    {
        if (currentTypeTransactions == null || selectedCurrencyName.IsNullOrEmpty())
        {
            return;
        }

        if (ifClearSelectedStates == 1)
        {
            selectedStates[selectedCurrencyName].Clear();
            selectedTransactions[selectedCurrencyName].Clear();
        }

        currentTypeTransactions = transactions.LoadAllTransactions(selectedCurrencyName);
    }
}
