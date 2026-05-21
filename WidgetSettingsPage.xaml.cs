using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Quick_Buttons_for_Game_Bar
{
    public sealed partial class WidgetSettingsPage : Page
    {
        private const double SettingsInputColumnWidth = 168;
        private const double SettingsKeyColumnWidth = 140;
        private const double SettingsActionColumnWidth = 80;
        private const double SettingsColumnSpacing = 8;

        private static readonly IReadOnlyList<string> ModifierOptions = new[]
        {
            "None", "Ctrl", "Alt", "Shift", "Ctrl + Alt", "Ctrl + Shift", "Alt + Shift", "Ctrl + Alt + Shift"
        };

        private static readonly IReadOnlyList<string> KeyOptions = new[]
        {
            "Not Set",
            "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
            "0","1","2","3","4","5","6","7","8","9",
            "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",
            "Insert","Delete","Home","End","Page Up","Page Down","Space","Tab","Escape",
            "Arrow Up","Arrow Down","Arrow Left","Arrow Right"
        };
        private WidgetSettings _draft;
        private XboxGameBarWidget _gameBarWidget;

        public WidgetSettingsPage()
        {
            try
            {
                InitializeComponent();
                InitializeCombos();
            }
            catch (Exception ex)
            {
                // Keep settings page usable even if combo initialization fails.
                _draft = WidgetSettingsDefaults.Create();
                try
                {
                    if (ValidationTextBlock != null)
                    {
                        SetValidation($"Settings UI initialization failed: {ex.Message}");
                    }
                }
                catch
                {
                    // Never allow initialization-time UI errors to crash startup.
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _gameBarWidget = e.Parameter as XboxGameBarWidget;
            try
            {
                LoadDraft();
            }
            catch (Exception ex)
            {
                _draft = WidgetSettingsDefaults.Create();

                try
                {
                    InitializeCombos();
                    SplitShortcut(_draft.BuiltInLosslessKeys, out string losslessModifier, out string losslessKey);
                    LosslessModifierCombo.SelectedItem = ModifierOptions.Contains(losslessModifier) ? losslessModifier : "Ctrl + Alt";
                    LosslessKeyCombo.SelectedItem = KeyOptions.Contains(losslessKey) ? losslessKey : "S";
                    SplitShortcut(_draft.BuiltInOverlayKeys, out string overlayModifier, out string overlayKey);
                    OverlayModifierCombo.SelectedItem = ModifierOptions.Contains(overlayModifier) ? overlayModifier : "None";
                    OverlayKeyCombo.SelectedItem = KeyOptions.Contains(overlayKey) ? overlayKey : "Insert";
                    OverlayNameTextBox.Text = WidgetSettingsStore.NormalizeOverlayDisplayName(_draft.OverlayDisplayName);
                    RefreshTopShortcutOrderComboItems(WidgetSettingsDefaults.TopShortcutOrderLosslessFirst);

                    BindCustomSlot("custom1", Custom1LabelTextBox, Custom1ModifierCombo, Custom1KeyCombo, Custom1EnabledButton);
                    BindCustomSlot("custom2", Custom2LabelTextBox, Custom2ModifierCombo, Custom2KeyCombo, Custom2EnabledButton);
                    BindCustomSlot("custom3", Custom3LabelTextBox, Custom3ModifierCombo, Custom3KeyCombo, Custom3EnabledButton);
                    BindCustomSlot("custom4", Custom4LabelTextBox, Custom4ModifierCombo, Custom4KeyCombo, Custom4EnabledButton);
                    RenderSectionOrder();
                    SetValidation($"Failed to load settings. Defaults were restored: {ex.Message}");
                }
                catch
                {
                    // Last-resort: swallow to avoid crashing settings page activation.
                }
            }
        }

        private void InitializeCombos()
        {
            SetComboItems(LosslessModifierCombo, ModifierOptions);
            SetComboItems(LosslessKeyCombo, KeyOptions);

            SetComboItems(Custom1ModifierCombo, ModifierOptions);
            SetComboItems(Custom1KeyCombo, KeyOptions);
            SetComboItems(Custom2ModifierCombo, ModifierOptions);
            SetComboItems(Custom2KeyCombo, KeyOptions);
            SetComboItems(Custom3ModifierCombo, ModifierOptions);
            SetComboItems(Custom3KeyCombo, KeyOptions);
            SetComboItems(Custom4ModifierCombo, ModifierOptions);
            SetComboItems(Custom4KeyCombo, KeyOptions);
            SetComboItems(OverlayModifierCombo, ModifierOptions);
            SetComboItems(OverlayKeyCombo, KeyOptions);
            RefreshTopShortcutOrderComboItems(WidgetSettingsDefaults.TopShortcutOrderLosslessFirst);
        }

        private void LoadDraft()
        {
            _draft = WidgetSettingsStore.Normalize(WidgetSettingsStore.Load());

            SplitShortcut(_draft.BuiltInLosslessKeys, out string losslessModifier, out string losslessKey);
            LosslessModifierCombo.SelectedItem = ModifierOptions.Contains(losslessModifier) ? losslessModifier : "Ctrl + Alt";
            LosslessKeyCombo.SelectedItem = KeyOptions.Contains(losslessKey) ? losslessKey : "S";
            SplitShortcut(_draft.BuiltInOverlayKeys, out string overlayModifier, out string overlayKey);
            OverlayModifierCombo.SelectedItem = ModifierOptions.Contains(overlayModifier) ? overlayModifier : "None";
            OverlayKeyCombo.SelectedItem = KeyOptions.Contains(overlayKey) ? overlayKey : "Insert";
            OverlayNameTextBox.Text = WidgetSettingsStore.NormalizeOverlayDisplayName(_draft.OverlayDisplayName);
            RefreshTopShortcutOrderComboItems(_draft.TopShortcutOrder);

            BindCustomSlot("custom1", Custom1LabelTextBox, Custom1ModifierCombo, Custom1KeyCombo, Custom1EnabledButton);
            BindCustomSlot("custom2", Custom2LabelTextBox, Custom2ModifierCombo, Custom2KeyCombo, Custom2EnabledButton);
            BindCustomSlot("custom3", Custom3LabelTextBox, Custom3ModifierCombo, Custom3KeyCombo, Custom3EnabledButton);
            BindCustomSlot("custom4", Custom4LabelTextBox, Custom4ModifierCombo, Custom4KeyCombo, Custom4EnabledButton);

            RenderSectionOrder();
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            ValidationTextBlock.Text = string.Empty;
        }

        private void BindCustomSlot(string slotId, TextBox labelTextBox, ComboBox modifier, ComboBox key, Button enabledButton)
        {
            if (!_draft.CustomShortcuts.TryGetValue(slotId, out CustomShortcutSlot slot) || slot == null)
            {
                slot = new CustomShortcutSlot { Keys = new List<string>(), IsEnabled = true, Label = string.Empty };
                _draft.CustomShortcuts[slotId] = slot;
            }

            labelTextBox.Text = WidgetSettingsStore.NormalizeCustomShortcutLabel(slot.Label);
            SplitShortcut(slot.Keys, out string currentModifier, out string currentKey);
            modifier.SelectedItem = ModifierOptions.Contains(currentModifier) ? currentModifier : "None";
            key.SelectedItem = KeyOptions.Contains(currentKey) ? currentKey : "Not Set";
            SetCustomEnabledButtonState(enabledButton, slot.IsEnabled);
        }

        private void RenderSectionOrder()
        {
            SectionOrderPanel.Children.Clear();
            for (int i = 0; i < _draft.SectionOrder.Count; i++)
            {
                string section = _draft.SectionOrder[i];
                var row = new Grid
                {
                    ColumnSpacing = SettingsColumnSpacing,
                    Margin = new Thickness(0, 0, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

                var name = new TextBlock
                {
                    Text = WidgetDisplayNameResolver.GetSectionDisplayName(section),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White)
                };

                var up = new Button
                {
                    Content = "\u25B2",
                    Width = 38,
                    MinWidth = 38,
                    Height = 36,
                    IsEnabled = i > 0
                };
                var down = new Button
                {
                    Content = "\u25BC",
                    Width = 38,
                    MinWidth = 38,
                    Height = 36,
                    IsEnabled = i < _draft.SectionOrder.Count - 1
                };
                var sectionEnabledButton = new Button
                {
                    Content = IsSectionHidden(section) ? "Off" : "On",
                    Width = 72,
                    MinWidth = 72,
                    Height = 36,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                int index = i;
                up.Click += (_, __) =>
                {
                    if (index <= 0)
                    {
                        return;
                    }

                    string temp = _draft.SectionOrder[index - 1];
                    _draft.SectionOrder[index - 1] = _draft.SectionOrder[index];
                    _draft.SectionOrder[index] = temp;
                    RenderSectionOrder();
                };

                down.Click += (_, __) =>
                {
                    if (index >= _draft.SectionOrder.Count - 1)
                    {
                        return;
                    }

                    string temp = _draft.SectionOrder[index + 1];
                    _draft.SectionOrder[index + 1] = _draft.SectionOrder[index];
                    _draft.SectionOrder[index] = temp;
                    RenderSectionOrder();
                };

                sectionEnabledButton.Click += (_, __) =>
                {
                    bool currentlyVisible = !IsSectionHidden(section);
                    bool hide = currentlyVisible;
                    if (hide && WouldHideAllSections(section))
                    {
                        SetValidation("At least one section must remain visible.");
                        return;
                    }

                    ValidationTextBlock.Visibility = Visibility.Collapsed;
                    ValidationTextBlock.Text = string.Empty;
                    SetSectionHidden(section, hide);
                    RenderSectionOrder();
                };

                Grid.SetColumn(name, 0);
                Grid.SetColumn(up, 1);
                Grid.SetColumn(down, 2);
                Grid.SetColumn(sectionEnabledButton, 3);
                row.Children.Add(name);
                row.Children.Add(up);
                row.Children.Add(down);
                row.Children.Add(sectionEnabledButton);
                SectionOrderPanel.Children.Add(row);
            }
        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            ValidationTextBlock.Text = string.Empty;

            if (string.Equals(LosslessKeyCombo.SelectedItem as string, "Not Set", StringComparison.OrdinalIgnoreCase))
            {
                SetValidation("Lossless Scaling shortcut must include a key.");
                return;
            }

            List<string> losslessKeys = ComposeShortcut(LosslessModifierCombo.SelectedItem as string ?? "None", LosslessKeyCombo.SelectedItem as string ?? "Not Set");
            if (losslessKeys.Count == 0)
            {
                SetValidation("Lossless Scaling shortcut must include a key.");
                return;
            }

            if (string.Equals(OverlayKeyCombo.SelectedItem as string, "Not Set", StringComparison.OrdinalIgnoreCase))
            {
                SetValidation("OptiScaler Overlay shortcut must include a key.");
                return;
            }

            List<string> overlayKeys = ComposeShortcut(
                OverlayModifierCombo.SelectedItem as string ?? "None",
                OverlayKeyCombo.SelectedItem as string ?? "Insert");
            if (overlayKeys.Count == 0)
            {
                SetValidation("OptiScaler Overlay shortcut must include a key.");
                return;
            }

            _draft.BuiltInLosslessKeys = losslessKeys;
            _draft.BuiltInOverlayKeys = overlayKeys;
            _draft.OverlayDisplayName = WidgetSettingsStore.NormalizeOverlayDisplayName(OverlayNameTextBox.Text);
            _draft.TopShortcutOrder = GetSelectedTopShortcutOrder();
            UpdateCustom("custom1", Custom1LabelTextBox, Custom1ModifierCombo, Custom1KeyCombo, Custom1EnabledButton);
            UpdateCustom("custom2", Custom2LabelTextBox, Custom2ModifierCombo, Custom2KeyCombo, Custom2EnabledButton);
            UpdateCustom("custom3", Custom3LabelTextBox, Custom3ModifierCombo, Custom3KeyCombo, Custom3EnabledButton);
            UpdateCustom("custom4", Custom4LabelTextBox, Custom4ModifierCombo, Custom4KeyCombo, Custom4EnabledButton);

            if (!HasAnyEffectivelyVisibleSection())
            {
                SetValidation("At least one section or one enabled Custom button must remain visible.");
                return;
            }

            try
            {
                _draft = WidgetSettingsStore.Normalize(_draft);
                WidgetSettingsStore.Save(_draft);
                await CloseSettingsAndReturnToMainAsync();
            }
            catch (Exception ex)
            {
                SetValidation($"Failed to save settings: {ex.Message}");
            }
        }

        private void UpdateCustom(string slotId, TextBox labelTextBox, ComboBox modifier, ComboBox key, Button enabledButton)
        {
            List<string> keys = ComposeShortcut(modifier.SelectedItem as string ?? "None", key.SelectedItem as string ?? "Not Set");

            _draft.CustomShortcuts[slotId] = new CustomShortcutSlot
            {
                Keys = keys,
                IsEnabled = IsCustomEnabledButtonOn(enabledButton),
                Label = WidgetSettingsStore.NormalizeCustomShortcutLabel(labelTextBox.Text)
            };
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await CloseSettingsAndReturnToMainAsync();
        }

        private void LosslessReset_Click(object sender, RoutedEventArgs e)
        {
            LosslessModifierCombo.SelectedItem = "Ctrl + Alt";
            LosslessKeyCombo.SelectedItem = "S";
        }

        private void Custom1EnabledButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCustomEnabled(Custom1EnabledButton);
        }

        private void OverlayReset_Click(object sender, RoutedEventArgs e)
        {
            OverlayNameTextBox.Text = WidgetSettingsDefaults.DefaultOverlayDisplayName;
            OverlayModifierCombo.SelectedItem = "None";
            OverlayKeyCombo.SelectedItem = "Insert";
        }

        private void Custom2EnabledButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCustomEnabled(Custom2EnabledButton);
        }

        private void Custom3EnabledButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCustomEnabled(Custom3EnabledButton);
        }

        private void Custom4EnabledButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCustomEnabled(Custom4EnabledButton);
        }
        
        private void SetCustomEnabledButtonState(Button button, bool isEnabled)
        {
            button.Content = isEnabled ? "On" : "Off";
        }

        private void ToggleCustomEnabled(Button button)
        {
            bool current = string.Equals(button.Content as string, "On", StringComparison.OrdinalIgnoreCase);
            SetCustomEnabledButtonState(button, !current);
        }

        private static bool IsCustomEnabledButtonOn(Button button)
        {
            return string.Equals(button.Content as string, "On", StringComparison.OrdinalIgnoreCase);
        }

        private void ResetLayout_Click(object sender, RoutedEventArgs e)
        {
            _draft.SectionOrder = new List<string>(WidgetSettingsDefaults.DefaultSectionOrder);
            _draft.HiddenSections = new List<string>();
            RenderSectionOrder();
        }

        private void TopShortcutOrderReset_Click(object sender, RoutedEventArgs e)
        {
            RefreshTopShortcutOrderComboItems(WidgetSettingsDefaults.TopShortcutOrderLosslessFirst);
            _draft.TopShortcutOrder = WidgetSettingsDefaults.TopShortcutOrderLosslessFirst;
        }

        private void SetComboItems(ComboBox combo, IReadOnlyList<string> items)
        {
            combo.Items.Clear();
            foreach (string item in items)
            {
                combo.Items.Add(item);
            }
        }

        private void RefreshTopShortcutOrderComboItems(string selectedValue)
        {
            string normalizedSelected = NormalizeTopShortcutOrderValue(selectedValue);
            TopShortcutOrderCombo.SelectionChanged -= TopShortcutOrderCombo_SelectionChanged;
            TopShortcutOrderCombo.Items.Clear();

            AddTopShortcutOrderItem(WidgetSettingsDefaults.TopShortcutOrderLosslessFirst);
            AddTopShortcutOrderItem(WidgetSettingsDefaults.TopShortcutOrderOverlayFirst);

            foreach (ComboBoxItem item in TopShortcutOrderCombo.Items)
            {
                if (string.Equals(item.Tag as string, normalizedSelected, StringComparison.OrdinalIgnoreCase))
                {
                    TopShortcutOrderCombo.SelectedItem = item;
                    break;
                }
            }

            TopShortcutOrderCombo.SelectionChanged += TopShortcutOrderCombo_SelectionChanged;
        }

        private void AddTopShortcutOrderItem(string value)
        {
            TopShortcutOrderCombo.Items.Add(new ComboBoxItem
            {
                Tag = value,
                Content = GetTopShortcutOrderLabel(value)
            });
        }

        private static string NormalizeTopShortcutOrderValue(string value)
        {
            return string.Equals(value, WidgetSettingsDefaults.TopShortcutOrderOverlayFirst, StringComparison.OrdinalIgnoreCase)
                ? WidgetSettingsDefaults.TopShortcutOrderOverlayFirst
                : WidgetSettingsDefaults.TopShortcutOrderLosslessFirst;
        }

        private string GetSelectedTopShortcutOrder()
        {
            if (TopShortcutOrderCombo.SelectedItem is ComboBoxItem item &&
                item.Tag is string value)
            {
                return NormalizeTopShortcutOrderValue(value);
            }

            return NormalizeTopShortcutOrderValue(_draft?.TopShortcutOrder);
        }

        private string GetTopShortcutOrderLabel(string topOrderValue)
        {
            if (string.Equals(topOrderValue, WidgetSettingsDefaults.TopShortcutOrderOverlayFirst, StringComparison.OrdinalIgnoreCase))
            {
                return $"{GetDisplayName(ShortcutButtonIds.OptiScalerOverlay)} / {GetDisplayName(ShortcutButtonIds.LosslessScaling)}";
            }

            return $"{GetDisplayName(ShortcutButtonIds.LosslessScaling)} / {GetDisplayName(ShortcutButtonIds.OptiScalerOverlay)}";
        }

        private string GetDisplayName(string id)
        {
            if (string.Equals(id, WidgetSettingsDefaults.TopShortcutOrderLosslessFirst, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, WidgetSettingsDefaults.TopShortcutOrderOverlayFirst, StringComparison.OrdinalIgnoreCase))
            {
                return GetTopShortcutOrderLabel(id);
            }

            if (string.Equals(id, WidgetSettingsDefaults.SectionTopShortcuts, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, WidgetSettingsDefaults.SectionResolution, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, WidgetSettingsDefaults.SectionCustom, StringComparison.OrdinalIgnoreCase))
            {
                return WidgetDisplayNameResolver.GetSectionDisplayName(id);
            }

            return WidgetDisplayNameResolver.GetShortcutDisplayName(id, _draft);
        }

        private void SetValidation(string text)
        {
            ValidationTextBlock.Text = text;
            ValidationTextBlock.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 128, 128));
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private async Task CloseSettingsAndReturnToMainAsync()
        {
            if (_gameBarWidget == null)
            {
                SetValidation("Settings widget context is not available.");
                return;
            }

            try
            {
                var control = new XboxGameBarWidgetControl(_gameBarWidget);
                await control.ActivateAsync("GamingWidget");
                await control.CloseAsync("GamingWidgetSettings");
            }
            catch (Exception ex)
            {
                SetValidation($"Failed to return to main widget: {ex.Message}");
            }
        }

        private static List<string> ComposeShortcut(string modifier, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "Not Set", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>();
            }

            var output = new List<string>();
            if (!string.IsNullOrWhiteSpace(modifier) && !string.Equals(modifier, "None", StringComparison.OrdinalIgnoreCase))
            {
                output.AddRange(modifier.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()));
            }

            output.Add(key.Trim());

            return output;
        }

        private static void SplitShortcut(IReadOnlyList<string> keys, out string modifier, out string key)
        {
            modifier = "None";
            key = "Not Set";
            if (!WidgetSettingsStore.IsValidKeys(keys))
            {
                return;
            }

            if (keys.Count == 1)
            {
                key = keys[0];
                return;
            }

            key = keys[keys.Count - 1];
            modifier = string.Join(" + ", keys.Take(keys.Count - 1));
        }

        private void TopShortcutOrderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_draft == null || TopShortcutOrderCombo.SelectedItem == null)
            {
                return;
            }

            _draft.TopShortcutOrder = GetSelectedTopShortcutOrder();
        }

        private void OverlayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_draft == null)
            {
                return;
            }

            _draft.OverlayDisplayName = WidgetSettingsStore.NormalizeOverlayDisplayName(OverlayNameTextBox.Text);
            RefreshTopShortcutOrderComboItems(GetSelectedTopShortcutOrder());
        }

        private bool IsSectionHidden(string sectionId)
        {
            return _draft.HiddenSections != null &&
                   _draft.HiddenSections.Contains(sectionId, StringComparer.OrdinalIgnoreCase);
        }

        private void SetSectionHidden(string sectionId, bool hidden)
        {
            _draft.HiddenSections ??= new List<string>();
            if (hidden)
            {
                if (!_draft.HiddenSections.Contains(sectionId, StringComparer.OrdinalIgnoreCase))
                {
                    _draft.HiddenSections.Add(sectionId);
                }
            }
            else
            {
                _draft.HiddenSections.RemoveAll(s => string.Equals(s, sectionId, StringComparison.OrdinalIgnoreCase));
            }

            _draft = WidgetSettingsStore.Normalize(_draft);
        }

        private bool WouldHideAllSections(string sectionToHide)
        {
            int visibleCount = _draft.SectionOrder.Count(section =>
                !IsSectionHidden(section) &&
                !string.Equals(section, sectionToHide, StringComparison.OrdinalIgnoreCase));
            return visibleCount <= 0;
        }

        private bool HasAnyEnabledCustomSlot()
        {
            return IsCustomEnabledButtonOn(Custom1EnabledButton) ||
                   IsCustomEnabledButtonOn(Custom2EnabledButton) ||
                   IsCustomEnabledButtonOn(Custom3EnabledButton) ||
                   IsCustomEnabledButtonOn(Custom4EnabledButton);
        }

        private bool HasAnyEffectivelyVisibleSection()
        {
            bool gamingVisible = !IsSectionHidden(WidgetSettingsDefaults.SectionTopShortcuts);
            bool displayVisible = !IsSectionHidden(WidgetSettingsDefaults.SectionResolution);
            bool customSectionVisible = !IsSectionHidden(WidgetSettingsDefaults.SectionCustom);
            bool customVisible = customSectionVisible && HasAnyEnabledCustomSlot();
            return gamingVisible || displayVisible || customVisible;
        }
    }
}
