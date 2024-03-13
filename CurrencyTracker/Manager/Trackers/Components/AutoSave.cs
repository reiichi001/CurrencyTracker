using System;
using System.IO;
using System.Linq;
using System.Timers;
using CurrencyTracker.Manager.Infos;
using CurrencyTracker.Manager.Transactions;

namespace CurrencyTracker.Manager.Trackers.Components;

public class AutoSave : ITrackerComponent
{
    public bool Initialized { get; set; }
    public static DateTime LastAutoSave { get; set; } = DateTime.MinValue;

    private static Timer? AutoSaveTimer;

    public void Init()
    {
        LastAutoSave = DateTime.Now;

        AutoSaveTimer ??= new Timer(1000);
        AutoSaveTimer.Elapsed += OnAutoSave;
        AutoSaveTimer.AutoReset = true;
        AutoSaveTimer.Enabled = true;
    }

    private static void OnAutoSave(object? sender, ElapsedEventArgs e)
    {
        if (DateTime.Now >= LastAutoSave + TimeSpan.FromMinutes(Service.Config.AutoSaveInterval))
        {
            AutoSaveHandler();
            LastAutoSave = DateTime.Now;
        }
    }

    private static void AutoSaveHandler()
    {
        switch (Service.Config.AutoSaveMode)
        {
            case 0:
                var filePath =
                    TransactionsHandler.BackupTransactions(P.PlayerDataFolder, Service.Config.MaxBackupFilesCount);
                if (Service.Config.AutoSaveMessage) Service.Chat.Print(Service.Lang.GetText("BackupHelp4", filePath));
                break;
            case 1:
                var failCharacters = Service.Config.CurrentActiveCharacter
                                            .Where(c => string.IsNullOrEmpty(
                                                       TransactionsHandler.BackupTransactions(
                                                           Path.Combine(P.PluginInterface.ConfigDirectory.FullName,
                                                                        $"{c.Name}_{c.Server}"),
                                                           Service.Config.MaxBackupFilesCount)))
                                            .Select(c => $"{c.Name}@{c.Server}")
                                            .ToList();

                var successCount = Service.Config.CurrentActiveCharacter.Count - failCharacters.Count;
                if (Service.Config.AutoSaveMessage)
                {
                    Service.Chat.Print(Service.Lang.GetText("BackupHelp1", successCount) +
                                       (failCharacters.Any()
                                            ? Service.Lang.GetText("BackupHelp2", failCharacters.Count)
                                            : ""));
                    if (failCharacters.Any())
                    {
                        Service.Chat.PrintError(Service.Lang.GetText("BackupHelp3"));
                        failCharacters.ForEach(x => Service.Chat.PrintError(x));
                    }
                }

                break;
        }
    }


    public void Uninit()
    {
        AutoSaveHandler();

        AutoSaveTimer?.Stop();
        if (AutoSaveTimer != null) AutoSaveTimer.Elapsed -= OnAutoSave;
        AutoSaveTimer?.Dispose();
        AutoSaveTimer = null;

        LastAutoSave = DateTime.MinValue;
    }
}
