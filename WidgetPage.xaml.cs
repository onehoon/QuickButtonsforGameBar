using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Core;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Quick_Buttons_for_Game_Bar
{
    public sealed partial class WidgetPage : Page
    {
        private string _resolutionAction1;
        private string _resolutionAction2;
        private string _resolutionAction3;
        private string _resolutionAction4;
        private WidgetSettings _settings;
        private XboxGameBarWidget _gameBarWidget;
        private bool _eventsHooked;
        private bool _isReloadingSettings;

        private const string ActionOverlay = "insert";
        private const string ActionCustom1 = "custom1";
        private const string ActionCustom2 = "custom2";
        private const string ActionCustom3 = "custom3";
        private const string ActionCustom4 = "custom4";
        private const string ActionLosslessScaling = "losslessscaling";
        private const string ActionDetectResolutionPresets = "detect-resolution-presets";
        private const string ActionSetResolution1200 = "set-resolution-1920-1200";
        private const string ActionSetResolution1080 = "set-resolution-1920-1080";
        private const string ActionSetResolution1050 = "set-resolution-1680-1050";
        private const string ActionSetResolution900 = "set-resolution-1600-900";
        private const string ActionSetResolution1440x900 = "set-resolution-1440-900";
        private const string ActionSetResolution720 = "set-resolution-1280-720";

        // Must stay in sync with desktop:ParameterGroup GroupId values in Package.appxmanifest.
        private const string GroupOverlay = "InsertCommand";
        private const string GroupCustom1 = "Custom1Command";
        private const string GroupCustom2 = "Custom2Command";
        private const string GroupCustom3 = "Custom3Command";
        private const string GroupCustom4 = "Custom4Command";
        private const string GroupLosslessScaling = "LosslessScalingCommand";
        private const string GroupDetectResolutionPresets = "DetectResolutionPresetsCommand";
        private const string GroupSetResolution1200 = "SetResolution1920x1200Command";
        private const string GroupSetResolution1080 = "SetResolution1920x1080Command";
        private const string GroupSetResolution1050 = "SetResolution1680x1050Command";
        private const string GroupSetResolution900 = "SetResolution1600x900Command";
        private const string GroupSetResolution1440x900 = "SetResolution1440x900Command";
        private const string GroupSetResolution720 = "SetResolution1280x720Command";

        public WidgetPage()
        {
            InitializeComponent();
            _settings = WidgetSettingsStore.Load();
            DiagnosticsLog.Write("WidgetPage ctor");
            Loaded += WidgetPage_Loaded;
        }

        private async void WidgetPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplySettingsToUi();
                if (IsSectionVisible(WidgetSettingsDefaults.SectionResolution))
                {
                    await InitializeResolutionSectionAsync();
                }
                else
                {
                    DisplayResolutionSection.Visibility = Visibility.Collapsed;
                    DiagnosticsLog.Write("Display Resolution initialization skipped because section is disabled.");
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.WriteException("WidgetPage_Loaded failed", ex);
                DisplayResolutionSection.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ApplyStateBrush(sender as Button, "ButtonBackgroundPointerOver");
        }

        private void Button_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ApplyStateBrush(sender as Button, "ButtonBackgroundPressed");
        }

        private void Button_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ApplyStateBrush(sender as Button, "ButtonBackground");
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ApplyStateBrush(sender as Button, "ButtonBackground");
        }

        private void ApplyStateBrush(Button button, string key)
        {
            if (button == null)
            {
                return;
            }

            var paletteTag = (button.Tag as string)?.ToLowerInvariant() ?? "default";
            var prefix = paletteTag switch
            {
                "capture" => "CaptureButton",
                "overlay" => "OverlayButton",
                _ => "DefaultButton"
            };

            var resourceKey = key switch
            {
                "ButtonBackgroundPressed" => $"{prefix}BackgroundPressed",
                "ButtonBackgroundPointerOver" => $"{prefix}BackgroundPointerOver",
                _ => $"{prefix}Background"
            };

            if (Resources.ContainsKey(resourceKey) && Resources[resourceKey] is Brush rootBrush)
            {
                button.Background = rootBrush;
            }
        }

        private async System.Threading.Tasks.Task<bool> LaunchHelperActionAsync(string action)
        {
            DiagnosticsLog.Write($"LaunchHelperAction start action={action}");

            string groupId = action switch
            {
                ActionOverlay => GroupOverlay,
                ActionCustom1 => GroupCustom1,
                ActionCustom2 => GroupCustom2,
                ActionCustom3 => GroupCustom3,
                ActionCustom4 => GroupCustom4,
                ActionLosslessScaling => GroupLosslessScaling,
                ActionDetectResolutionPresets => GroupDetectResolutionPresets,
                ActionSetResolution1200 => GroupSetResolution1200,
                ActionSetResolution1080 => GroupSetResolution1080,
                ActionSetResolution1050 => GroupSetResolution1050,
                ActionSetResolution900 => GroupSetResolution900,
                ActionSetResolution1440x900 => GroupSetResolution1440x900,
                ActionSetResolution720 => GroupSetResolution720,
                _ => null
            };

            if (string.IsNullOrEmpty(groupId))
            {
                DiagnosticsLog.Write($"LaunchHelperAction unknown action={action}");
                return false;
            }

            try
            {
                DiagnosticsLog.Write($"LaunchFullTrust group={groupId}");
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(groupId);
                DiagnosticsLog.Write($"LaunchFullTrust success action={action}");
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write($"LaunchFullTrust fail action={action} ex={ex.GetType().Name} msg={ex.Message}");
                return false;
            }
        }

        private async System.Threading.Tasks.Task InitializeResolutionSectionAsync()
        {
            DisplayResolutionSection.Visibility = Visibility.Collapsed;
            ApplySectionOrder();
            if (!IsSectionVisible(WidgetSettingsDefaults.SectionResolution))
            {
                DiagnosticsLog.Write("InitializeResolutionSectionAsync skipped: section disabled.");
                return;
            }

            DateTimeOffset launchStamp = DateTimeOffset.UtcNow;
            bool launched = await LaunchHelperActionAsync(ActionDetectResolutionPresets);
            if (!launched)
            {
                ApplySectionOrder();
                return;
            }

            ResolutionFeatureState state = await ResolutionFeatureStateStore.WaitForStateRefreshAsync(launchStamp, 2200);
            DiagnosticsLog.Write($"Resolution state current={state.CurrentWidth}x{state.CurrentHeight}@{state.CurrentRefreshRate}");
            UpdateCurrentDisplayStatusText(state);
            if (!state.Available)
            {
                DisplayResolutionSection.Visibility = Visibility.Collapsed;
                CurrentDisplayStatusTextBlock.Text = string.Empty;
                CurrentDisplayStatusTextBlock.Visibility = Visibility.Collapsed;
                ApplySectionOrder();
                return;
            }

            if (state.Group == ResolutionPresetGroup.Group1200)
            {
                ConfigureResolutionButtons(
                    "1200p", ActionSetResolution1200,
                    "1080p", ActionSetResolution1080,
                    "1050p", ActionSetResolution1050,
                    "900p", ActionSetResolution1440x900);
                ApplyPresetVisibility(state.Support1200p, state.Support1080p, state.Support1050p, state.Support1440x900);
                return;
            }

            if (state.Group == ResolutionPresetGroup.Group1080)
            {
                ConfigureResolutionButtons(
                    "1080p", ActionSetResolution1080,
                    "900p", ActionSetResolution900,
                    "720p", ActionSetResolution720,
                    string.Empty, null);
                ApplyPresetVisibility(state.Support1080p, state.Support900p, state.Support720p, false);
            }
        }

        private void ConfigureResolutionButtons(
            string label1,
            string action1,
            string label2,
            string action2,
            string label3,
            string action3,
            string label4,
            string action4)
        {
            ResolutionButton1.Content = label1;
            ResolutionButton2.Content = label2;
            ResolutionButton3.Content = label3;
            ResolutionButton4.Content = label4;
            _resolutionAction1 = action1;
            _resolutionAction2 = action2;
            _resolutionAction3 = action3;
            _resolutionAction4 = action4;
        }

        private void ApplyPresetVisibility(bool showFirst, bool showSecond, bool showThird, bool showFourth)
        {
            ResolutionButton1.Visibility = showFirst ? Visibility.Visible : Visibility.Collapsed;
            ResolutionButton2.Visibility = showSecond ? Visibility.Visible : Visibility.Collapsed;
            ResolutionButton3.Visibility = showThird ? Visibility.Visible : Visibility.Collapsed;
            ResolutionButton4.Visibility = showFourth ? Visibility.Visible : Visibility.Collapsed;

            _resolutionAction1 = showFirst ? _resolutionAction1 : null;
            _resolutionAction2 = showSecond ? _resolutionAction2 : null;
            _resolutionAction3 = showThird ? _resolutionAction3 : null;
            _resolutionAction4 = showFourth ? _resolutionAction4 : null;

            var visibleButtons = new List<Button>(4);
            if (showFirst)
            {
                visibleButtons.Add(ResolutionButton1);
            }

            if (showSecond)
            {
                visibleButtons.Add(ResolutionButton2);
            }

            if (showThird)
            {
                visibleButtons.Add(ResolutionButton3);
            }

            if (showFourth)
            {
                visibleButtons.Add(ResolutionButton4);
            }

            Grid.SetColumn(ResolutionButton1, 0);
            Grid.SetColumn(ResolutionButton2, 0);
            Grid.SetColumn(ResolutionButton3, 0);
            Grid.SetColumn(ResolutionButton4, 0);

            ResolutionButtonsGrid.ColumnDefinitions.Clear();
            for (int i = 0; i < visibleButtons.Count; i++)
            {
                ResolutionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(visibleButtons[i], i);
            }

            bool anyVisible = visibleButtons.Count > 0;
            bool userAllowsDisplay = IsSectionVisible(WidgetSettingsDefaults.SectionResolution);
            DisplayResolutionSection.Visibility = anyVisible && userAllowsDisplay ? Visibility.Visible : Visibility.Collapsed;
            ApplySectionOrder();
        }

        private async void OverlayButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteBuiltInShortcutAsync(_settings.BuiltInOverlayKeys, ActionOverlay);
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            await LaunchHelperActionAsync(ActionLosslessScaling);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _gameBarWidget = e.Parameter as XboxGameBarWidget;
            DiagnosticsLog.Write($"WidgetPage OnNavigatedTo: gameBarWidget is {(_gameBarWidget == null ? "null" : "not null")}");

            if (_gameBarWidget != null)
            {
                DiagnosticsLog.Write($"SettingsSupported before set: {_gameBarWidget.SettingsSupported}");
                _gameBarWidget.SettingsSupported = true;
                DiagnosticsLog.Write($"SettingsSupported after set: {_gameBarWidget.SettingsSupported}");
            }

            if (_eventsHooked)
            {
                return;
            }

            Window.Current.Activated += CurrentWindow_Activated;
            WidgetSettingsStore.SettingsSaved += WidgetSettingsStore_SettingsSaved;
            if (_gameBarWidget != null)
            {
                _gameBarWidget.SettingsClicked += Widget_SettingsClicked;
            }

            _eventsHooked = true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_eventsHooked)
            {
                Window.Current.Activated -= CurrentWindow_Activated;
                WidgetSettingsStore.SettingsSaved -= WidgetSettingsStore_SettingsSaved;
                if (_gameBarWidget != null)
                {
                    _gameBarWidget.SettingsClicked -= Widget_SettingsClicked;
                }

                _eventsHooked = false;
            }

            base.OnNavigatedFrom(e);
        }

        private async void Widget_SettingsClicked(XboxGameBarWidget sender, object args)
        {
            DiagnosticsLog.Write("SettingsClicked received");
            await OpenGameBarSettingsAsync();
        }

        private async void CurrentWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                return;
            }

            await ReloadSettingsAsync();
        }

        private async void WidgetSettingsStore_SettingsSaved(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _ = ReloadSettingsAsync();
            });
        }

        private async void CustomButton1_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCustomSlotAsync("custom1", ActionCustom1);
        }

        private async void CustomButton2_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCustomSlotAsync("custom2", ActionCustom2);
        }

        private async void CustomButton3_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCustomSlotAsync("custom3", ActionCustom3);
        }

        private async void CustomButton4_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCustomSlotAsync("custom4", ActionCustom4);
        }

        private async Task ExecuteCustomSlotAsync(string slotId, string action)
        {
            if (!_settings.CustomShortcuts.TryGetValue(slotId, out CustomShortcutSlot slot) ||
                slot == null ||
                !slot.IsEnabled)
            {
                return;
            }

            if (!WidgetSettingsStore.IsConfigured(slot))
            {
                await OpenGameBarSettingsAsync();
                return;
            }

            await LaunchHelperActionAsync(action);
        }

        private async Task ExecuteBuiltInShortcutAsync(IReadOnlyList<string> keys, string action)
        {
            if (!WidgetSettingsStore.IsValidKeys(keys))
            {
                await OpenGameBarSettingsAsync();
                return;
            }

            await LaunchHelperActionAsync(action);
        }

        private async Task OpenGameBarSettingsAsync()
        {
            DiagnosticsLog.Write("OpenGameBarSettingsAsync start");
            if (_gameBarWidget == null)
            {
                DiagnosticsLog.Write("OpenGameBarSettings skipped because widget context is null.");
                return;
            }

            try
            {
                await _gameBarWidget.ActivateSettingsAsync();
                DiagnosticsLog.Write("ActivateSettingsAsync succeeded.");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write($"ActivateSettingsAsync failed msg={ex.Message}");

                try
                {
                    var control = new XboxGameBarWidgetControl(_gameBarWidget);
                    await control.ActivateAsync("GamingWidgetSettings");
                    DiagnosticsLog.Write("Fallback XboxGameBarWidgetControl.ActivateAsync GamingWidgetSettings succeeded.");
                }
                catch (Exception fallbackEx)
                {
                    DiagnosticsLog.Write($"Fallback settings activation failed msg={fallbackEx.Message}");
                }
            }
        }

        private void ApplySettingsToUi()
        {
            _settings = WidgetSettingsStore.Normalize(_settings);
            LosslessShortcutTextBlock.Text = $"({FormatShortcut(_settings.BuiltInLosslessKeys)})";
            OverlayTitleTextBlock.Text = WidgetSettingsStore.NormalizeOverlayDisplayName(_settings.OverlayDisplayName);
            OverlayShortcutTextBlock.Text = $"({FormatShortcut(_settings.BuiltInOverlayKeys)})";
            if (!IsSectionVisible(WidgetSettingsDefaults.SectionResolution))
            {
                DisplayResolutionSection.Visibility = Visibility.Collapsed;
                CurrentDisplayStatusTextBlock.Text = string.Empty;
                CurrentDisplayStatusTextBlock.Visibility = Visibility.Collapsed;
            }

            ApplyCustomButtonsLayout();

            ApplyTopShortcutOrder();
            ApplySectionOrder();
        }

        private async Task ReloadSettingsAsync()
        {
            if (_isReloadingSettings)
            {
                DiagnosticsLog.Write("ReloadSettingsAsync skipped because reload is already running.");
                return;
            }

            _isReloadingSettings = true;
            try
            {
                bool wasResolutionVisible = IsSectionVisible(WidgetSettingsDefaults.SectionResolution);
                _settings = WidgetSettingsStore.Load();
                bool isResolutionVisible = IsSectionVisible(WidgetSettingsDefaults.SectionResolution);
                ApplySettingsToUi();
                if (isResolutionVisible && !wasResolutionVisible)
                {
                    await InitializeResolutionSectionAsync();
                }
                else if (wasResolutionVisible && !isResolutionVisible)
                {
                    DisplayResolutionSection.Visibility = Visibility.Collapsed;
                    CurrentDisplayStatusTextBlock.Text = string.Empty;
                    CurrentDisplayStatusTextBlock.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.WriteException("ReloadSettingsAsync failed", ex);
                _settings = WidgetSettingsDefaults.Create();
                ApplySettingsToUi();
            }
            finally
            {
                _isReloadingSettings = false;
            }
        }

        private void ApplyCustomButtonContent(string slotId, TextBlock labelTextBlock, TextBlock shortcutTextBlock)
        {
            if (!_settings.CustomShortcuts.TryGetValue(slotId, out CustomShortcutSlot slot) || slot == null)
            {
                labelTextBlock.Text = "Not Set";
                shortcutTextBlock.Text = string.Empty;
                shortcutTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            string label = WidgetSettingsStore.NormalizeCustomShortcutLabel(slot.Label);
            bool configured = WidgetSettingsStore.IsConfigured(slot);
            string shortcutText = configured ? FormatShortcut(slot.Keys) : "Not Set";

            if (!string.IsNullOrWhiteSpace(label))
            {
                labelTextBlock.Text = label;
                shortcutTextBlock.Text = $"({shortcutText})";
                shortcutTextBlock.Visibility = Visibility.Visible;
                return;
            }

            labelTextBlock.Text = configured ? shortcutText : "Not Set";
            shortcutTextBlock.Text = string.Empty;
            shortcutTextBlock.Visibility = Visibility.Collapsed;
        }

        private void ApplySectionOrder()
        {
            var map = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
            {
                [WidgetSettingsDefaults.SectionTopShortcuts] = GamingSection,
                [WidgetSettingsDefaults.SectionResolution] = DisplayResolutionSection,
                [WidgetSettingsDefaults.SectionCustom] = CustomSection
            };

            ReorderableSectionsPanel.Children.Clear();
            foreach (string section in _settings.SectionOrder)
            {
                if (!IsSectionVisible(section))
                {
                    continue;
                }

                if (map.TryGetValue(section, out FrameworkElement element))
                {
                    if (element.Visibility == Visibility.Collapsed)
                    {
                        continue;
                    }

                    ReorderableSectionsPanel.Children.Add(element);
                }
            }
        }

        private bool IsCustomSlotEnabled(string slotId)
        {
            return _settings.CustomShortcuts.TryGetValue(slotId, out CustomShortcutSlot slot) &&
                   slot != null &&
                   slot.IsEnabled;
        }

        private void ApplyCustomButtonsLayout()
        {
            var allButtons = new List<(Button Button, string SlotId, TextBlock Label, TextBlock Shortcut)>
            {
                (CustomButton1, "custom1", Custom1LabelTextBlock, Custom1ShortcutTextBlock),
                (CustomButton2, "custom2", Custom2LabelTextBlock, Custom2ShortcutTextBlock),
                (CustomButton3, "custom3", Custom3LabelTextBlock, Custom3ShortcutTextBlock),
                (CustomButton4, "custom4", Custom4LabelTextBlock, Custom4ShortcutTextBlock)
            };

            var visibleButtons = allButtons.Where(item => IsCustomSlotEnabled(item.SlotId)).ToList();
            CustomButtonsGrid.ColumnDefinitions.Clear();

            foreach (var item in allButtons)
            {
                item.Button.Visibility = Visibility.Collapsed;
                ApplyCustomButtonContent(item.SlotId, item.Label, item.Shortcut);
                Grid.SetColumn(item.Button, 0);
            }

            for (int i = 0; i < visibleButtons.Count; i++)
            {
                CustomButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                visibleButtons[i].Button.Visibility = Visibility.Visible;
                Grid.SetColumn(visibleButtons[i].Button, i);
            }

            bool anyCustomEnabled = visibleButtons.Count > 0;
            bool customSectionAllowed = IsSectionVisible(WidgetSettingsDefaults.SectionCustom);
            CustomSection.Visibility = anyCustomEnabled && customSectionAllowed ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsSectionVisible(string sectionId)
        {
            return _settings.HiddenSections == null ||
                   !_settings.HiddenSections.Contains(sectionId, StringComparer.OrdinalIgnoreCase);
        }

        private async void ResolutionButton1_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_resolutionAction1))
            {
                await LaunchHelperActionAsync(_resolutionAction1);
                await Task.Delay(500);
                await RefreshResolutionSectionAsync();
            }
        }

        private async void ResolutionButton2_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_resolutionAction2))
            {
                await LaunchHelperActionAsync(_resolutionAction2);
                await Task.Delay(500);
                await RefreshResolutionSectionAsync();
            }
        }

        private async void ResolutionButton3_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_resolutionAction3))
            {
                await LaunchHelperActionAsync(_resolutionAction3);
                await Task.Delay(500);
                await RefreshResolutionSectionAsync();
            }
        }

        private async void ResolutionButton4_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_resolutionAction4))
            {
                await LaunchHelperActionAsync(_resolutionAction4);
                await Task.Delay(500);
                await RefreshResolutionSectionAsync();
            }
        }

        private void ApplyTopShortcutOrder()
        {
            if (CaptureButton == null || OverlayButton == null)
            {
                return;
            }

            bool overlayFirst = string.Equals(
                _settings.TopShortcutOrder,
                WidgetSettingsDefaults.TopShortcutOrderOverlayFirst,
                StringComparison.OrdinalIgnoreCase);

            Grid.SetColumn(OverlayButton, overlayFirst ? 0 : 1);
            Grid.SetColumn(CaptureButton, overlayFirst ? 1 : 0);
            DiagnosticsLog.Write($"ApplyTopShortcutOrder order={_settings.TopShortcutOrder}");
        }

        private void UpdateCurrentDisplayStatusText(ResolutionFeatureState state)
        {
            if (CurrentDisplayStatusTextBlock == null)
            {
                return;
            }

            if (state != null &&
                state.CurrentWidth > 0 &&
                state.CurrentHeight > 0)
            {
                CurrentDisplayStatusTextBlock.Text =
                    $"{state.CurrentWidth}x{state.CurrentHeight}p";
                CurrentDisplayStatusTextBlock.Visibility = Visibility.Visible;
                DiagnosticsLog.Write($"Display status: {CurrentDisplayStatusTextBlock.Text}");
                return;
            }

            CurrentDisplayStatusTextBlock.Text = string.Empty;
            CurrentDisplayStatusTextBlock.Visibility = Visibility.Collapsed;
            DiagnosticsLog.Write("Display status: unavailable");
        }

        private async Task RefreshResolutionSectionAsync()
        {
            if (!IsSectionVisible(WidgetSettingsDefaults.SectionResolution))
            {
                DisplayResolutionSection.Visibility = Visibility.Collapsed;
                CurrentDisplayStatusTextBlock.Text = string.Empty;
                CurrentDisplayStatusTextBlock.Visibility = Visibility.Collapsed;
                ApplySectionOrder();
                DiagnosticsLog.Write("RefreshResolutionSectionAsync skipped: section disabled.");
                return;
            }

            try
            {
                await InitializeResolutionSectionAsync();
            }
            catch (Exception ex)
            {
                DisplayResolutionSection.Visibility = Visibility.Collapsed;
                ApplySectionOrder();
                DiagnosticsLog.WriteException("RefreshResolutionSectionAsync failed", ex);
            }
        }

        private static string FormatShortcut(IReadOnlyList<string> keys)
        {
            return WidgetSettingsStore.IsValidKeys(keys) ? string.Join("+", keys) : "Not Set";
        }

    }
}
