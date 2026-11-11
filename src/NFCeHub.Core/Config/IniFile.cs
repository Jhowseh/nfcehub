using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NFCeHub.Core;

public sealed class IniFile
{
    private static readonly string[] TrueValues = { "yes", "true", "on", "1", "sim" };

    private readonly List<string> sectionOrder = new();
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> sections =
        new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Sections => sectionOrder;

    public static IniFile Load(string path)
    {
        var ini = new IniFile();
        if (!File.Exists(path))
        {
            return ini;
        }
        string current = null;
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
            {
                continue;
            }
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = line.Substring(1, line.Length - 2).Trim();
                ini.AddSection(current);
                continue;
            }
            var equals = line.IndexOf('=');
            if (current == null || equals <= 0)
            {
                continue;
            }
            ini.Set(current, line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
        }
        return ini;
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        foreach (var name in sectionOrder)
        {
            sb.Append('[').Append(name).Append("]\r\n");
            foreach (var pair in sections[name])
            {
                sb.Append(pair.Key).Append(" = ").Append(pair.Value).Append("\r\n");
            }
            sb.Append("\r\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public bool HasSection(string section) => sections.ContainsKey(section);

    public void AddSection(string section)
    {
        if (!sections.ContainsKey(section))
        {
            sections[section] = new List<KeyValuePair<string, string>>();
            sectionOrder.Add(section);
        }
    }

    public bool HasOption(string section, string key) =>
        sections.TryGetValue(section, out var pairs) && pairs.Any(p => Same(p.Key, key));

    public string Get(string section, string key, string fallback = "")
    {
        if (sections.TryGetValue(section, out var pairs))
        {
            foreach (var pair in pairs)
            {
                if (Same(pair.Key, key))
                {
                    return pair.Value;
                }
            }
        }
        return fallback;
    }

    public bool GetBool(string section, string key, bool fallback)
    {
        var value = Get(section, key, null);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        return TrueValues.Contains(value.Trim().ToLowerInvariant());
    }

    public void Set(string section, string key, string value)
    {
        AddSection(section);
        key = key.ToLowerInvariant();
        var pairs = sections[section];
        var index = pairs.FindIndex(p => Same(p.Key, key));
        var pair = new KeyValuePair<string, string>(key, value ?? "");
        if (index >= 0)
        {
            pairs[index] = pair;
        }
        else
        {
            pairs.Add(pair);
        }
    }

    public void RemoveOption(string section, string key)
    {
        if (sections.TryGetValue(section, out var pairs))
        {
            pairs.RemoveAll(p => Same(p.Key, key));
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
