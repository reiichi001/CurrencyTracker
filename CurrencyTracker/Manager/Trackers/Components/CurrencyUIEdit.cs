namespace CurrencyTracker.Manager.Trackers.Components
{
    public class CurrencyUIEdit : ITrackerComponent
    {
        public bool Initialized
        {
            get { return _initialized; }
            set { _initialized = value; }
        }

        private bool _initialized = false;

        private unsafe IAddonEventHandle? mouseoverHandle;
        private unsafe IAddonEventHandle? mouseooutHandle;

        public void Init()
        {
            Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Currency", OnCurrencyUI);
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Currency", OnCurrencyUI);

            _initialized = true;
        }

        private void OnCurrencyUI(AddonEvent type, AddonArgs args)
        {
            switch (type)
            {
                case AddonEvent.PreDraw:
                    UITextEdit(args);
                    break;

                case AddonEvent.PostSetup:
                    UITooltipEdit(args);
                    break;
            }
        }

        private unsafe void UITextEdit(AddonArgs args)
        {
            var UI = (AtkUnitBase*)args.Addon;
            if (UI == null || UI->RootNode == null || UI->RootNode->ChildNode == null || UI->UldManager.NodeList == null) return;

            var gilNode = (AtkComponentBase*)UI->GetComponentNodeById(12);
            var gilTextNode = gilNode->GetTextNodeById(5);

            gilTextNode->GetAsAtkTextNode()->SetText(CurrencyInfo.GetCharacterCurrencyAmount(1, Plugin.Instance.CurrentCharacter).ToString("#,0"));
        }

        private unsafe void UITooltipEdit(AddonArgs args)
        {
            var UI = (AtkUnitBase*)args.Addon;
            if (UI == null || UI->RootNode == null || UI->RootNode->ChildNode == null || UI->UldManager.NodeList == null) return;

            var gilNode = (AtkComponentBase*)UI->GetComponentNodeById(12);
            var gilTextNode = gilNode->GetTextNodeById(5);

            gilTextNode->NodeFlags |= NodeFlags.EmitsEvents | NodeFlags.RespondToMouse | NodeFlags.HasCollision;

            mouseoverHandle = Service.AddonEventManager.AddEvent((nint)UI, (nint)gilTextNode, AddonEventType.MouseOver, TooltipHandler);
            mouseooutHandle = Service.AddonEventManager.AddEvent((nint)UI, (nint)gilTextNode, AddonEventType.MouseOut, TooltipHandler);
        }

        private unsafe void TooltipHandler(AddonEventType type, nint addon, nint node)
        {
            var addonId = ((AtkUnitBase*)addon)->ID;
            var inventoryAmount = CurrencyInfo.GetCurrencyAmount(1, TransactionFileCategory.Inventory, 0).ToString("N0");
            var tooltipBuilder = new StringBuilder($"{Service.Lang.GetText("Inventory")}: {inventoryAmount}");

            if (Plugin.Configuration.CharacterRetainers.TryGetValue(Plugin.Instance.CurrentCharacter.ContentID, out var retainers))
            {
                foreach (var retainer in retainers)
                {
                    var retainerAmount = CurrencyInfo.GetCurrencyAmountFromFile(1, Plugin.Instance.CurrentCharacter, TransactionFileCategory.Retainer, retainer.Key);
                    if (retainerAmount.HasValue)
                    {
                        tooltipBuilder.AppendLine();
                        tooltipBuilder.Append($"{retainer.Value}: {retainerAmount.Value.ToString("N0")}");
                    }
                }
            }

            var tooltip = tooltipBuilder.ToString();

            switch (type)
            {
                case AddonEventType.MouseOver:
                    AtkStage.GetSingleton()->TooltipManager.ShowTooltip(addonId, (AtkResNode*)node, tooltip);
                    break;

                case AddonEventType.MouseOut:
                    AtkStage.GetSingleton()->TooltipManager.HideTooltip(addonId);
                    break;
            }
        }

        public void Uninit()
        {
            Service.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "Currency", OnCurrencyUI);
            Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Currency", OnCurrencyUI);

            Service.AddonEventManager.RemoveEvent(mouseoverHandle);
            Service.AddonEventManager.RemoveEvent(mouseooutHandle);

            _initialized = false;
        }
    }
}
