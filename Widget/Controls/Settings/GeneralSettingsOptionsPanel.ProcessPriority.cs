using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class GeneralSettingsOptionsPanel
    {
        private bool _suppressProcessPriorityEvents = true;
        private bool _processPriorityRequestPending;

        private void InitializeProcessPrioritySettings()
        {
            PopulatePrioritySelector(GameBarPrioritySelector);
            PopulatePrioritySelector(GameBarFtServerPrioritySelector);
            PopulatePrioritySelector(KillConfirmWidgetPrioritySelector);
            SelectProcessPrioritySettings();
        }

        private void SelectProcessPrioritySettings()
        {
            ProcessPrioritySettingsValues settings = ProcessPrioritySettingsStore.Load();
            _suppressProcessPriorityEvents = true;
            try
            {
                ProcessPriorityPersistenceToggle.IsOn = settings.PersistenceEnabled;
                SelectTaggedItem(GameBarPrioritySelector, settings.GameBarPriority);
                SelectTaggedItem(
                    GameBarFtServerPrioritySelector,
                    settings.GameBarFtServerPriority);
                SelectTaggedItem(
                    KillConfirmWidgetPrioritySelector,
                    settings.KillConfirmWidgetPriority);
            }
            finally
            {
                _suppressProcessPriorityEvents = false;
            }
        }

        private void ApplyProcessPriorityLanguage()
        {
            ProcessPriorityTitleText.Text = LocalizationManager.Text("ProcessPriorityTitle");
            ProcessPriorityHintText.Text = LocalizationManager.Text("ProcessPriorityHint");
            ProcessPriorityRefreshButton.Content = LocalizationManager.Text("Refresh");
            ProcessPriorityPersistenceToggle.OffContent = LocalizationManager.Text("Off");
            ProcessPriorityPersistenceToggle.OnContent = LocalizationManager.Text("On");
            ProcessPriorityPersistenceHintText.Text =
                LocalizationManager.Text("ProcessPriorityPersistenceHint");

            _suppressProcessPriorityEvents = true;
            try
            {
                RefreshPrioritySelectorLanguage(GameBarPrioritySelector);
                RefreshPrioritySelectorLanguage(GameBarFtServerPrioritySelector);
                RefreshPrioritySelectorLanguage(KillConfirmWidgetPrioritySelector);
            }
            finally
            {
                _suppressProcessPriorityEvents = false;
            }
        }

        internal async Task RefreshProcessPriorityStateAsync()
        {
            if (_processPriorityRequestPending)
            {
                return;
            }

            _processPriorityRequestPending = true;
            ProcessPriorityRefreshButton.IsEnabled = false;
            try
            {
                SetAllProcessPriorityStatus(LocalizationManager.Text("ProcessPriorityReading"));
                IReadOnlyDictionary<string, ProcessPriorityStatus> statuses =
                    await ProcessPrioritySettingsStore.GetCurrentAsync();
                bool selectActual = !ProcessPriorityPersistenceToggle.IsOn;
                UpdateProcessPriorityStatus(
                    ProcessPrioritySettingsStore.GameBarTarget,
                    GameBarPrioritySelector,
                    GameBarPriorityStatusText,
                    statuses,
                    selectActual);
                UpdateProcessPriorityStatus(
                    ProcessPrioritySettingsStore.GameBarFtServerTarget,
                    GameBarFtServerPrioritySelector,
                    GameBarFtServerPriorityStatusText,
                    statuses,
                    selectActual);
                UpdateProcessPriorityStatus(
                    ProcessPrioritySettingsStore.KillConfirmWidgetTarget,
                    KillConfirmWidgetPrioritySelector,
                    KillConfirmWidgetPriorityStatusText,
                    statuses,
                    selectActual);
            }
            catch (Exception ex)
            {
                SetAllProcessPriorityStatus(
                    LocalizationManager.Text("ProcessPriorityServiceUnavailable"));
                App.Log("Read process priorities failed: " + ex);
            }
            finally
            {
                ProcessPriorityRefreshButton.IsEnabled = true;
                _processPriorityRequestPending = false;
            }
        }

        private async void OnProcessPrioritySelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_suppressProcessPriorityEvents
                || _processPriorityRequestPending
                || !(sender is ComboBox selector))
            {
                return;
            }

            string target = GetPriorityTarget(selector);
            string priority = GetSelectedPriority(selector);
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(priority))
            {
                return;
            }

            ProcessPrioritySettingsStore.SavePriority(target, priority);
            TextBlock statusText = GetPriorityStatusText(target);
            selector.IsEnabled = false;
            statusText.Text = LocalizationManager.Text("ProcessPriorityApplying");
            try
            {
                ProcessPriorityStatus status =
                    await ProcessPrioritySettingsStore.SetCurrentAsync(target, priority);
                SetProcessPriorityStatusText(statusText, status);
                if (!ProcessPriorityPersistenceToggle.IsOn
                    && status.Running
                    && IsSelectablePriority(status.Priority))
                {
                    SelectPriorityWithoutEvents(selector, status.Priority);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = LocalizationManager.Text("ProcessPriorityApplyFailed");
                App.Log("Set process priority failed for " + target + ": " + ex);
            }
            finally
            {
                selector.IsEnabled = true;
            }
        }

        private async void OnProcessPriorityPersistenceToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressProcessPriorityEvents)
            {
                return;
            }

            SaveSelectedProcessPriorities();
            bool enabled = ProcessPriorityPersistenceToggle.IsOn;
            ProcessPrioritySettingsStore.SavePersistenceEnabled(enabled);
            if (!enabled)
            {
                await RefreshProcessPriorityStateAsync();
                return;
            }

            SetAllProcessPriorityStatus(LocalizationManager.Text("ProcessPriorityApplying"));
            try
            {
                await ProcessPrioritySettingsStore.ApplyPersistedAsync();
                await RefreshProcessPriorityStateAsync();
            }
            catch (Exception ex)
            {
                SetAllProcessPriorityStatus(
                    LocalizationManager.Text("ProcessPriorityApplyFailed"));
                App.Log("Enable persisted process priorities failed: " + ex);
            }
        }

        private async void OnProcessPriorityRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshProcessPriorityStateAsync();
        }

        private void SaveSelectedProcessPriorities()
        {
            ProcessPrioritySettingsStore.SavePriority(
                ProcessPrioritySettingsStore.GameBarTarget,
                GetSelectedPriority(GameBarPrioritySelector));
            ProcessPrioritySettingsStore.SavePriority(
                ProcessPrioritySettingsStore.GameBarFtServerTarget,
                GetSelectedPriority(GameBarFtServerPrioritySelector));
            ProcessPrioritySettingsStore.SavePriority(
                ProcessPrioritySettingsStore.KillConfirmWidgetTarget,
                GetSelectedPriority(KillConfirmWidgetPrioritySelector));
        }

        private static string GetPriorityTarget(ComboBox selector)
        {
            if (selector == null)
            {
                return string.Empty;
            }
            if (string.Equals(selector.Name, "GameBarPrioritySelector", StringComparison.Ordinal))
            {
                return ProcessPrioritySettingsStore.GameBarTarget;
            }
            if (string.Equals(selector.Name, "GameBarFtServerPrioritySelector", StringComparison.Ordinal))
            {
                return ProcessPrioritySettingsStore.GameBarFtServerTarget;
            }
            if (string.Equals(selector.Name, "KillConfirmWidgetPrioritySelector", StringComparison.Ordinal))
            {
                return ProcessPrioritySettingsStore.KillConfirmWidgetTarget;
            }
            return string.Empty;
        }

        private TextBlock GetPriorityStatusText(string target)
        {
            switch (target)
            {
                case ProcessPrioritySettingsStore.GameBarTarget:
                    return GameBarPriorityStatusText;
                case ProcessPrioritySettingsStore.GameBarFtServerTarget:
                    return GameBarFtServerPriorityStatusText;
                default:
                    return KillConfirmWidgetPriorityStatusText;
            }
        }

        private void UpdateProcessPriorityStatus(
            string target,
            ComboBox selector,
            TextBlock statusText,
            IReadOnlyDictionary<string, ProcessPriorityStatus> statuses,
            bool selectActual)
        {
            if (!statuses.TryGetValue(target, out ProcessPriorityStatus status))
            {
                statusText.Text = LocalizationManager.Text("ProcessPriorityNotRunning");
                return;
            }

            SetProcessPriorityStatusText(statusText, status);
            if (selectActual && status.Running && IsSelectablePriority(status.Priority))
            {
                SelectPriorityWithoutEvents(selector, status.Priority);
                ProcessPrioritySettingsStore.SavePriority(target, status.Priority);
            }
        }

        private void SelectPriorityWithoutEvents(ComboBox selector, string priority)
        {
            bool previous = _suppressProcessPriorityEvents;
            _suppressProcessPriorityEvents = true;
            try
            {
                foreach (object entry in selector.Items)
                {
                    if (entry is ComboBoxItem item
                        && string.Equals(item.Tag as string, priority, StringComparison.OrdinalIgnoreCase))
                    {
                        selector.SelectedItem = item;
                        return;
                    }
                }
            }
            finally
            {
                _suppressProcessPriorityEvents = previous;
            }
        }

        private static string GetSelectedPriority(ComboBox selector)
        {
            return selector?.SelectedItem is ComboBoxItem item
                ? ProcessPrioritySettingsStore.NormalizePriority(
                    item.Tag as string,
                    ProcessPrioritySettingsStore.NormalPriority)
                : ProcessPrioritySettingsStore.NormalPriority;
        }

        private static bool IsSelectablePriority(string priority)
        {
            return string.Equals(priority, ProcessPrioritySettingsStore.RealtimePriority, StringComparison.Ordinal)
                || string.Equals(priority, ProcessPrioritySettingsStore.HighPriority, StringComparison.Ordinal)
                || string.Equals(priority, ProcessPrioritySettingsStore.AboveNormalPriority, StringComparison.Ordinal)
                || string.Equals(priority, ProcessPrioritySettingsStore.NormalPriority, StringComparison.Ordinal)
                || string.Equals(priority, ProcessPrioritySettingsStore.BelowNormalPriority, StringComparison.Ordinal)
                || string.Equals(priority, ProcessPrioritySettingsStore.IdlePriority, StringComparison.Ordinal);
        }

        private void SetProcessPriorityStatusText(
            TextBlock statusText,
            ProcessPriorityStatus status)
        {
            if (!status.Running)
            {
                statusText.Text = LocalizationManager.Text("ProcessPriorityNotRunning");
                return;
            }
            if (!string.IsNullOrWhiteSpace(status.Error))
            {
                statusText.Text = LocalizationManager.Text("ProcessPriorityApplyFailed");
                return;
            }

            statusText.Text = LocalizationManager.Text("ProcessPriorityCurrentPrefix")
                + PriorityDisplayName(status.Priority);
        }

        private void SetAllProcessPriorityStatus(string text)
        {
            GameBarPriorityStatusText.Text = text;
            GameBarFtServerPriorityStatusText.Text = text;
            KillConfirmWidgetPriorityStatusText.Text = text;
        }

        private static void PopulatePrioritySelector(ComboBox selector)
        {
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.RealtimePriority));
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.HighPriority));
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.AboveNormalPriority));
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.NormalPriority));
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.BelowNormalPriority));
            selector.Items.Add(CreatePriorityItem(ProcessPrioritySettingsStore.IdlePriority));
        }

        private static ComboBoxItem CreatePriorityItem(string priority)
        {
            return new ComboBoxItem
            {
                Tag = priority,
                Content = PriorityDisplayName(priority)
            };
        }

        private static void RefreshPrioritySelectorLanguage(ComboBox selector)
        {
            foreach (object entry in selector.Items)
            {
                if (entry is ComboBoxItem item)
                {
                    item.Content = PriorityDisplayName(item.Tag as string);
                }
            }
        }

        private static string PriorityDisplayName(string priority)
        {
            switch (priority)
            {
                case ProcessPrioritySettingsStore.RealtimePriority:
                    return LocalizationManager.Text("ProcessPriorityRealtime");
                case ProcessPrioritySettingsStore.HighPriority:
                    return LocalizationManager.Text("ProcessPriorityHigh");
                case ProcessPrioritySettingsStore.AboveNormalPriority:
                    return LocalizationManager.Text("ProcessPriorityAboveNormal");
                case ProcessPrioritySettingsStore.BelowNormalPriority:
                    return LocalizationManager.Text("ProcessPriorityBelowNormal");
                case ProcessPrioritySettingsStore.IdlePriority:
                    return LocalizationManager.Text("ProcessPriorityIdle");
                case "mixed":
                    return LocalizationManager.Text("ProcessPriorityMixed");
                case "unknown":
                    return LocalizationManager.Text("ProcessPriorityUnknown");
                default:
                    return LocalizationManager.Text("ProcessPriorityNormal");
            }
        }
    }
}
