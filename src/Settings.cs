using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Blend2Unity
{
    /// <summary>Simple key=value settings file in %APPDATA%\Blend2Unity\settings.ini.</summary>
    sealed class Settings
    {
        readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blend2Unity", "settings.ini");

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var eq = line.IndexOf('=');
                    if (eq > 0) s._values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
                }
            }
            catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var sb = new StringBuilder();
                foreach (var kv in _values) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public string Get(string key, string fallback) => _values.TryGetValue(key, out var v) ? v : fallback;
        public bool GetBool(string key, bool fallback) => _values.TryGetValue(key, out var v) ? v == "1" : fallback;
        public void Set(string key, string value) => _values[key] = (value ?? "").Replace("\r", "").Replace("\n", "");
        public void Set(string key, bool value) => _values[key] = value ? "1" : "0";
    }
}
