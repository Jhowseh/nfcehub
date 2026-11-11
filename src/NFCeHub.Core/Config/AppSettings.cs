using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace NFCeHub.Core;

public sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message)
    {
    }
}

public sealed class AppSettings
{
    private const string DatabaseSection = "database";
    private const string OutputSection = "output";
    private const string OptionsSection = "options";
    private const string SyspdvRegistryKey = @"SOFTWARE\Syspdv_server\UniConnection";

    private static readonly string[] Sections = { DatabaseSection, OutputSection, OptionsSection };

    private readonly IniFile ini;

    private AppSettings(IniFile ini)
    {
        this.ini = ini;
    }

    public string Host
    {
        get => ini.Get(DatabaseSection, "host");
        set => ini.Set(DatabaseSection, "host", value);
    }

    public string Port
    {
        get => ini.Get(DatabaseSection, "port");
        set => ini.Set(DatabaseSection, "port", value);
    }

    public string DatabasePath
    {
        get => ini.Get(DatabaseSection, "path");
        set => ini.Set(DatabaseSection, "path", value);
    }

    public string User
    {
        get => ini.Get(DatabaseSection, "user");
        set => ini.Set(DatabaseSection, "user", value);
    }

    public string Password
    {
        get => ini.Get(DatabaseSection, "password");
        set => ini.Set(DatabaseSection, "password", value);
    }

    public string XmlFolder
    {
        get => ini.Get(OutputSection, "xml_folder");
        set => ini.Set(OutputSection, "xml_folder", value);
    }

    public string Series
    {
        get => ini.Get(OptionsSection, "series", "1");
        set => ini.Set(OptionsSection, "series", value);
    }

    public bool IncludeCancellation
    {
        get => ini.GetBool(OptionsSection, "include_cancellation", true);
        set => SetFlag("include_cancellation", value);
    }

    public bool IncludeUnauthorized
    {
        get => ini.GetBool(OptionsSection, "include_unauthorized", false);
        set => SetFlag("include_unauthorized", value);
    }

    public bool ByMonth
    {
        get => ini.GetBool(OptionsSection, "by_month", false);
        set => SetFlag("by_month", value);
    }

    public ExportOptions ExportOptions
    {
        get => new()
        {
            IncludeCancellation = IncludeCancellation,
            IncludeUnauthorized = IncludeUnauthorized,
            ByMonth = ByMonth,
        };
        set
        {
            IncludeCancellation = value.IncludeCancellation;
            IncludeUnauthorized = value.IncludeUnauthorized;
            ByMonth = value.ByMonth;
        }
    }

    public static AppSettings Load()
    {
        var ini = IniFile.Load(AppPaths.ConfigFile);
        foreach (var section in Sections)
        {
            ini.AddSection(section);
        }
        foreach (var pair in SyspdvDefaults())
        {
            if (string.IsNullOrWhiteSpace(ini.Get(DatabaseSection, pair.Key)))
            {
                ini.Set(DatabaseSection, pair.Key, pair.Value);
            }
        }
        if (!ini.HasOption(DatabaseSection, "password"))
        {
            ini.Set(DatabaseSection, "password", "");
        }
        return new AppSettings(ini);
    }

    public static AppSettings LoadRequired(bool requirePassword = true)
    {
        var path = AppPaths.ConfigFile;
        if (!File.Exists(path))
        {
            throw new ConfigException($"Falta o arquivo de configuração {path}.");
        }
        var ini = IniFile.Load(path);
        if (!ini.HasSection(DatabaseSection))
        {
            throw new ConfigException($"O arquivo {path} não tem a seção [database].");
        }
        if (requirePassword && string.IsNullOrWhiteSpace(ini.Get(DatabaseSection, "password")))
        {
            throw new ConfigException($"Preencha a senha do banco em {path}.");
        }
        return new AppSettings(ini);
    }

    public void Save()
    {
        ini.RemoveOption(DatabaseSection, "fbclient");
        ini.Save(AppPaths.ConfigFile);
    }

    private void SetFlag(string key, bool value) => ini.Set(OptionsSection, key, value ? "yes" : "no");

    private static Dictionary<string, string> SyspdvDefaults()
    {
        var values = new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = "3050",
            ["path"] = @"C:\Syspdv\syspdv_srv.fdb",
            ["user"] = "SYSDBA",
        };
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var key = root.OpenSubKey(SyspdvRegistryKey);
            if (key != null)
            {
                foreach (var (registryName, field) in new[] { ("Server", "host"), ("Database", "path"), ("Port", "port") })
                {
                    var value = Convert.ToString(key.GetValue(registryName))?.Trim();
                    if (!string.IsNullOrEmpty(value) && value != "0")
                    {
                        values[field] = value;
                    }
                }
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException || e is UnauthorizedAccessException)
        {
        }
        return values;
    }
}
