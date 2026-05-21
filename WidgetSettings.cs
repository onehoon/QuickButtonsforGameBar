using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Data.Json;
using Windows.Storage;

namespace Quick_Buttons_for_Game_Bar
{
    internal static class ShortcutButtonIds
    {
        internal const string LosslessScaling = "losslessScaling";
        internal const string OptiScalerOverlay = "overlay";
        internal const string Custom1 = "custom1";
        internal const string Custom2 = "custom2";
        internal const string Custom3 = "custom3";
        internal const string Custom4 = "custom4";
    }

    internal static class WidgetSettingsDefaults
    {
        // Persisted section id. Kept as "topShortcuts" for backward compatibility.
        // User-facing display name should be resolved as "Gaming".
        internal const string SectionTopShortcuts = "topShortcuts";
        internal const string SectionLosslessScaling = "LosslessScaling";
        internal const string SectionOverlay = "overlay";
        internal const string SectionResolution = "resolution";
        internal const string SectionCustom = "custom";
        internal const string TopShortcutOrderLosslessFirst = "losslessFirst";
        internal const string TopShortcutOrderOverlayFirst = "overlayFirst";
        internal const string DefaultOverlayDisplayName = "OptiScaler Overlay";
        internal const int OverlayDisplayNameMaxLength = 24;
        internal const int CustomShortcutLabelMaxLength = 16;

        internal static readonly IReadOnlyList<string> DefaultLosslessKeys = new[] { "Ctrl", "Alt", "S" };
        internal static readonly IReadOnlyList<string> DefaultOverlayKeys = new[] { "Insert" };
        internal static readonly IReadOnlyList<string> DefaultSectionOrder = new[]
        {
            SectionTopShortcuts,
            SectionResolution,
            SectionCustom
        };
        internal static readonly IReadOnlyList<string> CustomSlotIds = new[]
        {
            ShortcutButtonIds.Custom1,
            ShortcutButtonIds.Custom2,
            ShortcutButtonIds.Custom3,
            ShortcutButtonIds.Custom4
        };

        internal static WidgetSettings Create()
        {
            return new WidgetSettings
            {
                Version = 4,
                TopShortcutOrder = TopShortcutOrderLosslessFirst,
                BuiltInLosslessKeys = new List<string>(DefaultLosslessKeys),
                BuiltInOverlayKeys = new List<string>(DefaultOverlayKeys),
                OverlayDisplayName = DefaultOverlayDisplayName,
                SectionOrder = new List<string>(DefaultSectionOrder),
                HiddenSections = new List<string>(),
                CustomShortcuts = new Dictionary<string, CustomShortcutSlot>(StringComparer.OrdinalIgnoreCase)
                {
                    ["custom1"] = new CustomShortcutSlot { Keys = new List<string>(), IsEnabled = true, Label = string.Empty },
                    ["custom2"] = new CustomShortcutSlot { Keys = new List<string>(), IsEnabled = true, Label = string.Empty },
                    ["custom3"] = new CustomShortcutSlot { Keys = new List<string>(), IsEnabled = true, Label = string.Empty },
                    ["custom4"] = new CustomShortcutSlot { Keys = new List<string>(), IsEnabled = true, Label = string.Empty }
                }
            };
        }
    }

    internal sealed class WidgetSettings
    {
        internal int Version { get; set; }
        internal List<string> BuiltInLosslessKeys { get; set; } = new List<string>();
        internal List<string> BuiltInOverlayKeys { get; set; } = new List<string>();
        internal string OverlayDisplayName { get; set; } = WidgetSettingsDefaults.DefaultOverlayDisplayName;
        internal Dictionary<string, CustomShortcutSlot> CustomShortcuts { get; set; } = new Dictionary<string, CustomShortcutSlot>(StringComparer.OrdinalIgnoreCase);
        internal List<string> SectionOrder { get; set; } = new List<string>();
        internal List<string> HiddenSections { get; set; } = new List<string>();
        internal string TopShortcutOrder { get; set; } = WidgetSettingsDefaults.TopShortcutOrderLosslessFirst;
    }

    internal sealed class CustomShortcutSlot
    {
        internal List<string> Keys { get; set; } = new List<string>();
        internal bool IsEnabled { get; set; } = true;
        internal string Label { get; set; } = string.Empty;
    }

    internal static class WidgetSettingsStore
    {
        private const string SettingsFileName = "widget_settings.json";
        internal static event EventHandler SettingsSaved;

        internal static WidgetSettings Load()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                if (!File.Exists(filePath))
                {
                    var defaults = WidgetSettingsDefaults.Create();
                    Save(defaults);
                    return defaults;
                }

                string raw = File.ReadAllText(filePath);
                var parsed = Parse(raw);
                if (parsed == null)
                {
                    var defaults = WidgetSettingsDefaults.Create();
                    Save(defaults);
                    return defaults;
                }

                return parsed;
            }
            catch
            {
                return WidgetSettingsDefaults.Create();
            }
        }

        internal static void Save(WidgetSettings settings)
        {
            var normalized = Normalize(settings);
            string filePath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, Serialize(normalized));
            SettingsSaved?.Invoke(null, EventArgs.Empty);
        }

        internal static WidgetSettings Normalize(WidgetSettings input)
        {
            var defaults = WidgetSettingsDefaults.Create();
            if (input == null)
            {
                return defaults;
            }

            var result = WidgetSettingsDefaults.Create();
            result.Version = input.Version > 0 ? input.Version : defaults.Version;
            result.Version = Math.Max(result.Version, 4);
            result.TopShortcutOrder = NormalizeTopShortcutOrder(input.TopShortcutOrder);
            result.OverlayDisplayName = NormalizeOverlayDisplayName(input.OverlayDisplayName);

            if (IsValidKeys(input.BuiltInLosslessKeys))
            {
                result.BuiltInLosslessKeys = input.BuiltInLosslessKeys.Select(k => k.Trim()).ToList();
            }

            if (IsValidKeys(input.BuiltInOverlayKeys))
            {
                result.BuiltInOverlayKeys = input.BuiltInOverlayKeys.Select(k => k.Trim()).ToList();
            }

            foreach (string slotId in WidgetSettingsDefaults.CustomSlotIds)
            {
                if (input.CustomShortcuts != null && input.CustomShortcuts.TryGetValue(slotId, out CustomShortcutSlot slot) && slot != null)
                {
                    result.CustomShortcuts[slotId] = new CustomShortcutSlot
                    {
                        Keys = IsValidKeys(slot.Keys) ? slot.Keys.Select(k => k.Trim()).ToList() : new List<string>(),
                        IsEnabled = slot.IsEnabled,
                        Label = NormalizeCustomShortcutLabel(slot.Label)
                    };
                }
            }

            if (input.SectionOrder != null)
            {
                var cleaned = new List<string>();
                foreach (string section in input.SectionOrder)
                {
                    string normalizedSection = NormalizeSectionId(section);
                    if (!string.IsNullOrEmpty(normalizedSection) && !cleaned.Contains(normalizedSection, StringComparer.OrdinalIgnoreCase))
                    {
                        cleaned.Add(normalizedSection);
                    }
                }

                if (!cleaned.Contains(WidgetSettingsDefaults.SectionTopShortcuts, StringComparer.OrdinalIgnoreCase))
                {
                    cleaned.Insert(0, WidgetSettingsDefaults.SectionTopShortcuts);
                }

                foreach (string defaultSection in WidgetSettingsDefaults.DefaultSectionOrder)
                {
                    if (!cleaned.Contains(defaultSection, StringComparer.OrdinalIgnoreCase))
                    {
                        cleaned.Add(defaultSection);
                    }
                }

                result.SectionOrder = cleaned;
            }

            result.HiddenSections = NormalizeHiddenSections(input.HiddenSections);
            return result;
        }

        internal static bool IsConfigured(CustomShortcutSlot slot)
        {
            return slot != null && IsValidKeys(slot.Keys);
        }

        internal static bool IsValidKeys(IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(keys[i]))
                {
                    return false;
                }
            }

            string primaryKey = keys[keys.Count - 1].Trim();
            if (string.Equals(primaryKey, "Not Set", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsModifierToken(primaryKey))
            {
                return false;
            }

            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (!IsModifierToken(keys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsModifierToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim();
            return string.Equals(normalized, "Ctrl", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Control", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Alt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Shift", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSectionId(string section)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                return null;
            }

            string normalized = section.Trim();
            if (string.Equals(normalized, WidgetSettingsDefaults.SectionTopShortcuts, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Top Shortcuts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Gaming", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, WidgetSettingsDefaults.SectionLosslessScaling, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "losslessscaling", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, WidgetSettingsDefaults.SectionOverlay, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "overlay", StringComparison.OrdinalIgnoreCase))
            {
                return WidgetSettingsDefaults.SectionTopShortcuts;
            }

            if (string.Equals(normalized, WidgetSettingsDefaults.SectionResolution, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Display Resolution", StringComparison.OrdinalIgnoreCase))
            {
                return WidgetSettingsDefaults.SectionResolution;
            }

            if (string.Equals(normalized, WidgetSettingsDefaults.SectionCustom, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                return WidgetSettingsDefaults.SectionCustom;
            }

            return null;
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, SettingsFileName);
        }

        private static string Serialize(WidgetSettings settings)
        {
            var root = new JsonObject();
            root["version"] = JsonValue.CreateNumberValue(settings.Version);
            root["topShortcutOrder"] = JsonValue.CreateStringValue(NormalizeTopShortcutOrder(settings.TopShortcutOrder));
            root["overlayDisplayName"] = JsonValue.CreateStringValue(NormalizeOverlayDisplayName(settings.OverlayDisplayName));

            var builtIn = new JsonObject();
            var ls = new JsonObject();
            var lsKeys = new JsonArray();
            foreach (string key in settings.BuiltInLosslessKeys)
            {
                lsKeys.Add(JsonValue.CreateStringValue(key));
            }

            ls["keys"] = lsKeys;
            builtIn["losslessScaling"] = ls;

            var overlay = new JsonObject();
            var overlayKeys = new JsonArray();
            foreach (string key in settings.BuiltInOverlayKeys)
            {
                overlayKeys.Add(JsonValue.CreateStringValue(key));
            }

            overlay["keys"] = overlayKeys;
            builtIn["overlay"] = overlay;
            root["builtInShortcuts"] = builtIn;

            var customRoot = new JsonObject();
            foreach (var slotId in WidgetSettingsDefaults.CustomSlotIds)
            {
                var slot = settings.CustomShortcuts[slotId] ?? new CustomShortcutSlot();
                var slotObj = new JsonObject();

                var keys = new JsonArray();
                foreach (string key in slot.Keys)
                {
                    keys.Add(JsonValue.CreateStringValue(key));
                }

                slotObj["isEnabled"] = JsonValue.CreateBooleanValue(slot.IsEnabled);
                slotObj["keys"] = keys;
                slotObj["label"] = JsonValue.CreateStringValue(NormalizeCustomShortcutLabel(slot.Label));
                customRoot[slotId] = slotObj;
            }

            root["customShortcuts"] = customRoot;

            var order = new JsonArray();
            foreach (string section in settings.SectionOrder)
            {
                order.Add(JsonValue.CreateStringValue(section));
            }

            root["sectionOrder"] = order;

            var hiddenSections = new JsonArray();
            foreach (string section in settings.HiddenSections)
            {
                hiddenSections.Add(JsonValue.CreateStringValue(section));
            }

            root["hiddenSections"] = hiddenSections;
            return root.Stringify();
        }

        private static WidgetSettings Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!JsonObject.TryParse(raw, out JsonObject root))
            {
                return null;
            }

            var parsed = WidgetSettingsDefaults.Create();

            if (root.TryGetValue("version", out IJsonValue versionValue) && versionValue.ValueType == JsonValueType.Number)
            {
                parsed.Version = (int)versionValue.GetNumber();
            }

            if (root.TryGetValue("topShortcutOrder", out IJsonValue topOrderValue) &&
                topOrderValue.ValueType == JsonValueType.String)
            {
                parsed.TopShortcutOrder = topOrderValue.GetString();
            }

            if (root.TryGetValue("overlayDisplayName", out IJsonValue overlayNameValue) &&
                overlayNameValue.ValueType == JsonValueType.String)
            {
                parsed.OverlayDisplayName = overlayNameValue.GetString();
            }

            if (root.TryGetValue("builtInShortcuts", out IJsonValue builtInValue) && builtInValue.ValueType == JsonValueType.Object)
            {
                var builtInObj = builtInValue.GetObject();
                if (builtInObj.TryGetValue("losslessScaling", out IJsonValue losslessValue) && losslessValue.ValueType == JsonValueType.Object)
                {
                    var losslessObj = losslessValue.GetObject();
                    parsed.BuiltInLosslessKeys = ReadStringArray(losslessObj, "keys");
                }

                if (builtInObj.TryGetValue("overlay", out IJsonValue overlayValue) && overlayValue.ValueType == JsonValueType.Object)
                {
                    var overlayObj = overlayValue.GetObject();
                    parsed.BuiltInOverlayKeys = ReadStringArray(overlayObj, "keys");
                }
            }

            if (root.TryGetValue("customShortcuts", out IJsonValue customValue) && customValue.ValueType == JsonValueType.Object)
            {
                var customObj = customValue.GetObject();
                foreach (string slotId in WidgetSettingsDefaults.CustomSlotIds)
                {
                    if (customObj.TryGetValue(slotId, out IJsonValue slotValue) && slotValue.ValueType == JsonValueType.Object)
                    {
                        var slotObj = slotValue.GetObject();
                        var slot = parsed.CustomShortcuts[slotId];

                        slot.Keys = ReadStringArray(slotObj, "keys");
                        if (slotObj.TryGetValue("isEnabled", out IJsonValue enabledValue) &&
                            enabledValue.ValueType == JsonValueType.Boolean)
                        {
                            slot.IsEnabled = enabledValue.GetBoolean();
                        }
                        else
                        {
                            slot.IsEnabled = true;
                        }

                        if (slotObj.TryGetValue("label", out IJsonValue labelValue) &&
                            labelValue.ValueType == JsonValueType.String)
                        {
                            slot.Label = NormalizeCustomShortcutLabel(labelValue.GetString());
                        }
                        else
                        {
                            slot.Label = string.Empty;
                        }
                    }
                }
            }

            if (root.TryGetValue("sectionOrder", out IJsonValue orderValue) && orderValue.ValueType == JsonValueType.Array)
            {
                parsed.SectionOrder = new List<string>();
                foreach (IJsonValue value in orderValue.GetArray())
                {
                    if (value.ValueType == JsonValueType.String)
                    {
                        parsed.SectionOrder.Add(value.GetString());
                    }
                }
            }

            if (root.TryGetValue("hiddenSections", out IJsonValue hiddenValue) && hiddenValue.ValueType == JsonValueType.Array)
            {
                parsed.HiddenSections = new List<string>();
                foreach (IJsonValue value in hiddenValue.GetArray())
                {
                    if (value.ValueType == JsonValueType.String)
                    {
                        parsed.HiddenSections.Add(value.GetString());
                    }
                }
            }

            return Normalize(parsed);
        }

        private static List<string> ReadStringArray(JsonObject source, string key)
        {
            var result = new List<string>();
            if (!source.TryGetValue(key, out IJsonValue value) || value.ValueType != JsonValueType.Array)
            {
                return result;
            }

            foreach (IJsonValue item in value.GetArray())
            {
                if (item.ValueType == JsonValueType.String)
                {
                    string text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(text.Trim());
                    }
                }
            }

            return result;
        }

        private static string NormalizeTopShortcutOrder(string value)
        {
            if (string.Equals(value, WidgetSettingsDefaults.TopShortcutOrderOverlayFirst, StringComparison.OrdinalIgnoreCase))
            {
                return WidgetSettingsDefaults.TopShortcutOrderOverlayFirst;
            }

            return WidgetSettingsDefaults.TopShortcutOrderLosslessFirst;
        }

        internal static string NormalizeOverlayDisplayName(string value)
        {
            string normalized = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return WidgetSettingsDefaults.DefaultOverlayDisplayName;
            }

            if (normalized.Length > WidgetSettingsDefaults.OverlayDisplayNameMaxLength)
            {
                normalized = normalized.Substring(0, WidgetSettingsDefaults.OverlayDisplayNameMaxLength);
            }

            return normalized;
        }

        internal static string NormalizeCustomShortcutLabel(string value)
        {
            string normalized = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            if (normalized.Length > WidgetSettingsDefaults.CustomShortcutLabelMaxLength)
            {
                normalized = normalized.Substring(0, WidgetSettingsDefaults.CustomShortcutLabelMaxLength);
            }

            return normalized;
        }

        private static List<string> NormalizeHiddenSections(IEnumerable<string> sections)
        {
            var result = new List<string>();
            if (sections == null)
            {
                return result;
            }

            foreach (string section in sections)
            {
                string normalized = NormalizeSectionId(section);
                if (!string.IsNullOrEmpty(normalized) &&
                    !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(normalized);
                }
            }

            if (result.Count >= WidgetSettingsDefaults.DefaultSectionOrder.Count)
            {
                return new List<string>();
            }

            return result;
        }
    }

    internal static class WidgetDisplayNameResolver
    {
        internal static string GetSectionDisplayName(string sectionId)
        {
            return sectionId switch
            {
                WidgetSettingsDefaults.SectionTopShortcuts => "Gaming",
                WidgetSettingsDefaults.SectionResolution => "Display Resolution",
                WidgetSettingsDefaults.SectionCustom => "Custom Shortcuts",
                _ => sectionId
            };
        }

        internal static string GetShortcutDisplayName(string shortcutId, WidgetSettings settings)
        {
            if (string.Equals(shortcutId, ShortcutButtonIds.LosslessScaling, StringComparison.OrdinalIgnoreCase))
            {
                return "Lossless Scaling";
            }

            if (string.Equals(shortcutId, ShortcutButtonIds.OptiScalerOverlay, StringComparison.OrdinalIgnoreCase))
            {
                return WidgetSettingsStore.NormalizeOverlayDisplayName(settings?.OverlayDisplayName);
            }

            if (string.Equals(shortcutId, ShortcutButtonIds.Custom1, StringComparison.OrdinalIgnoreCase))
            {
                return "Custom 1";
            }

            if (string.Equals(shortcutId, ShortcutButtonIds.Custom2, StringComparison.OrdinalIgnoreCase))
            {
                return "Custom 2";
            }

            if (string.Equals(shortcutId, ShortcutButtonIds.Custom3, StringComparison.OrdinalIgnoreCase))
            {
                return "Custom 3";
            }
            if (string.Equals(shortcutId, ShortcutButtonIds.Custom4, StringComparison.OrdinalIgnoreCase))
            {
                return "Custom 4";
            }

            return shortcutId;
        }
    }
}

