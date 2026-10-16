using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RectangleWindows
{
    internal enum SettingAction
    {
        Maximize,
        CenterHalf,
        Third,
        Half,
        TwoThirds,
        ShotClipboard,
        ShotDesktop
    }

    internal sealed class HotkeySpec
    {
        public uint Modifiers;
        public uint Key;

        public HotkeySpec()
        {
        }

        public HotkeySpec(uint modifiers, uint key)
        {
            Modifiers = modifiers & ~Native.MOD_NOREPEAT;
            Key = key;
        }

        public bool IsEmpty
        {
            get { return Key == 0; }
        }

        public uint RegisterModifiers
        {
            get { return Modifiers | Native.MOD_NOREPEAT; }
        }

        public string Display
        {
            get
            {
                if (IsEmpty)
                {
                    return "";
                }
                return HotkeyText.Format(Modifiers, Key);
            }
        }

        public HotkeySpec Clone()
        {
            return new HotkeySpec(Modifiers, Key);
        }

        public bool EqualsSpec(HotkeySpec other)
        {
            if (other == null)
            {
                return false;
            }
            return Modifiers == other.Modifiers && Key == other.Key;
        }
    }

    internal sealed class ActionDefinition
    {
        public SettingAction Action;
        public string Title;
        public string Section;
        public HotkeySpec DefaultShortcut;
        public bool IsScreenshot;

        public ActionDefinition(SettingAction action, string section, string title, HotkeySpec defaultShortcut, bool isScreenshot)
        {
            Action = action;
            Section = section;
            Title = title;
            DefaultShortcut = defaultShortcut;
            IsScreenshot = isScreenshot;
        }
    }

    internal sealed class AppSettings
    {
        public Dictionary<SettingAction, HotkeySpec> Shortcuts;
        public bool SnapWhileDragging;
        public bool RunAtStartup;

        public static readonly ActionDefinition[] Catalog = BuildCatalog();

        public static string ConfigPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RectangleWindows");
                return Path.Combine(folder, "config.json");
            }
        }

        public static AppSettings CreateDefaults()
        {
            AppSettings settings = new AppSettings();
            settings.SnapWhileDragging = true;
            settings.RunAtStartup = false;
            settings.Shortcuts = new Dictionary<SettingAction, HotkeySpec>();
            for (int i = 0; i < Catalog.Length; i++)
            {
                settings.Shortcuts[Catalog[i].Action] = Catalog[i].DefaultShortcut.Clone();
            }
            return settings;
        }

        public static AppSettings Load()
        {
            string path = ConfigPath;
            if (!File.Exists(path))
            {
                return CreateDefaults();
            }
            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch
            {
                return CreateDefaults();
            }
        }

        public static AppSettings FromJson(string json)
        {
            AppSettings settings = CreateDefaults();
            ParseInto(settings, json);
            return settings;
        }

        public void Save()
        {
            string path = ConfigPath;
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(path, ToJson(), Encoding.UTF8);
        }

        public HotkeySpec GetShortcut(SettingAction action)
        {
            HotkeySpec spec;
            if (Shortcuts.TryGetValue(action, out spec) && spec != null)
            {
                return spec;
            }
            return new HotkeySpec();
        }

        public void SetShortcut(SettingAction action, HotkeySpec spec)
        {
            Shortcuts[action] = spec == null ? new HotkeySpec() : spec.Clone();
        }

        public void RestoreDefaults()
        {
            AppSettings defaults = CreateDefaults();
            SnapWhileDragging = defaults.SnapWhileDragging;
            Shortcuts = defaults.Shortcuts;
        }

        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"snapWhileDragging\": " + (SnapWhileDragging ? "true" : "false") + ",");
            builder.AppendLine("  \"runAtStartup\": " + (RunAtStartup ? "true" : "false") + ",");
            builder.AppendLine("  \"shortcuts\": {");
            for (int i = 0; i < Catalog.Length; i++)
            {
                ActionDefinition definition = Catalog[i];
                HotkeySpec spec = GetShortcut(definition.Action);
                builder.Append("    \"");
                builder.Append(definition.Action.ToString());
                builder.Append("\": { \"modifiers\": ");
                builder.Append(spec.Modifiers.ToString(CultureInfo.InvariantCulture));
                builder.Append(", \"key\": ");
                builder.Append(spec.Key.ToString(CultureInfo.InvariantCulture));
                builder.Append(" }");
                if (i < Catalog.Length - 1)
                {
                    builder.Append(",");
                }
                builder.AppendLine();
            }
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static ActionDefinition[] BuildCatalog()
        {
            uint ctrlAlt = Native.MOD_CONTROL | Native.MOD_ALT;
            uint ctrlAltShift = ctrlAlt | Native.MOD_SHIFT;
            return new ActionDefinition[]
            {
                new ActionDefinition(SettingAction.Maximize, "Window", "Maximize", new HotkeySpec(ctrlAlt, 0x26), false),
                new ActionDefinition(SettingAction.CenterHalf, "Halves", "Center Half", new HotkeySpec(ctrlAlt, 0x30), false),
                new ActionDefinition(SettingAction.Half, "Halves", "Left / Right Half", new HotkeySpec(ctrlAlt, 0x32), false),
                new ActionDefinition(SettingAction.Third, "Thirds", "Left / Right Third", new HotkeySpec(ctrlAlt, 0x31), false),
                new ActionDefinition(SettingAction.TwoThirds, "Thirds", "Left / Right Two Thirds", new HotkeySpec(ctrlAlt, 0x33), false),
                new ActionDefinition(SettingAction.ShotClipboard, "Screenshots", "Screenshot to Clipboard", new HotkeySpec(ctrlAltShift, 0x34), true),
                new ActionDefinition(SettingAction.ShotDesktop, "Screenshots", "Screenshot to Desktop", new HotkeySpec(ctrlAltShift, 0x33), true)
            };
        }

        private static void ParseInto(AppSettings settings, string json)
        {
            settings.SnapWhileDragging = ReadBool(json, "snapWhileDragging", settings.SnapWhileDragging);
            settings.RunAtStartup = ReadBool(json, "runAtStartup", settings.RunAtStartup);
            for (int i = 0; i < Catalog.Length; i++)
            {
                SettingAction action = Catalog[i].Action;
                string name = action.ToString();
                int index = json.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }
                int brace = json.IndexOf('{', index);
                int end = json.IndexOf('}', brace + 1);
                if (brace < 0 || end < 0)
                {
                    continue;
                }
                string block = json.Substring(brace, end - brace + 1);
                uint modifiers = ReadUInt(block, "modifiers", settings.GetShortcut(action).Modifiers);
                uint key = ReadUInt(block, "key", settings.GetShortcut(action).Key);
                settings.SetShortcut(action, new HotkeySpec(modifiers, key));
            }
        }

        private static bool ReadBool(string json, string name, bool fallback)
        {
            string marker = "\"" + name + "\"";
            int index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return fallback;
            }
            int colon = json.IndexOf(':', index);
            if (colon < 0)
            {
                return fallback;
            }
            string slice = json.Substring(colon + 1, Math.Min(12, json.Length - colon - 1)).Trim().ToLowerInvariant();
            if (slice.StartsWith("true"))
            {
                return true;
            }
            if (slice.StartsWith("false"))
            {
                return false;
            }
            return fallback;
        }

        private static uint ReadUInt(string json, string name, uint fallback)
        {
            string marker = "\"" + name + "\"";
            int index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return fallback;
            }
            int colon = json.IndexOf(':', index);
            if (colon < 0)
            {
                return fallback;
            }
            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
            {
                start++;
            }
            int end = start;
            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }
            if (end == start)
            {
                return fallback;
            }
            uint value;
            if (uint.TryParse(json.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            return fallback;
        }
    }

    internal static class HotkeyText
    {
        public static string Format(uint modifiers, uint key)
        {
            List<string> parts = new List<string>();
            if ((modifiers & Native.MOD_CONTROL) != 0)
            {
                parts.Add("Ctrl");
            }
            if ((modifiers & Native.MOD_ALT) != 0)
            {
                parts.Add("Alt");
            }
            if ((modifiers & Native.MOD_SHIFT) != 0)
            {
                parts.Add("Shift");
            }
            if ((modifiers & Native.MOD_WIN) != 0)
            {
                parts.Add("Win");
            }
            parts.Add(KeyName(key));
            return string.Join("+", parts.ToArray());
        }

        public static string KeyName(uint key)
        {
            switch (key)
            {
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Esc";
                case 0x20: return "Space";
                case 0x21: return "Page Up";
                case 0x22: return "Page Down";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x30: return "0";
                case 0x31: return "1";
                case 0x32: return "2";
                case 0x33: return "3";
                case 0x34: return "4";
                case 0x35: return "5";
                case 0x36: return "6";
                case 0x37: return "7";
                case 0x38: return "8";
                case 0x39: return "9";
                case 0x60: return "Num 0";
                case 0x61: return "Num 1";
                case 0x62: return "Num 2";
                case 0x63: return "Num 3";
                case 0x64: return "Num 4";
                case 0x65: return "Num 5";
                case 0x66: return "Num 6";
                case 0x67: return "Num 7";
                case 0x68: return "Num 8";
                case 0x69: return "Num 9";
                case 0x6A: return "Num *";
                case 0x6B: return "Num +";
                case 0x6D: return "Num -";
                case 0x6E: return "Num .";
                case 0x6F: return "Num /";
                case 0x70: return "F1";
                case 0x71: return "F2";
                case 0x72: return "F3";
                case 0x73: return "F4";
                case 0x74: return "F5";
                case 0x75: return "F6";
                case 0x76: return "F7";
                case 0x77: return "F8";
                case 0x78: return "F9";
                case 0x79: return "F10";
                case 0x7A: return "F11";
                case 0x7B: return "F12";
                case 0xBB: return "=";
                case 0xBD: return "-";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "`";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
                default:
                    if (key >= 0x41 && key <= 0x5A)
                    {
                        return ((char)key).ToString();
                    }
                    return "0x" + key.ToString("X2", CultureInfo.InvariantCulture);
            }
        }
    }
}
