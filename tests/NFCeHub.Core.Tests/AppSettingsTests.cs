using System;
using System.IO;
using System.Text;
using Xunit;

namespace NFCeHub.Core.Tests;

[Collection("config")]
public sealed class AppSettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string original = AppPaths.ConfigFile;

    public AppSettingsTests()
    {
        Directory.CreateDirectory(folder);
        AppPaths.ConfigFile = Path.Combine(folder, "config.ini");
    }

    public void Dispose()
    {
        AppPaths.ConfigFile = original;
        Directory.Delete(folder, true);
    }

    private void WriteConfig(string text) =>
        File.WriteAllText(AppPaths.ConfigFile, text, new UTF8Encoding(true));

    [Fact]
    public void LoadRequiredFailsWithoutFile()
    {
        Assert.Throws<ConfigException>(() => AppSettings.LoadRequired());
    }

    [Fact]
    public void LoadRequiredFailsWithoutPassword()
    {
        WriteConfig("[database]\nhost = 127.0.0.1\npassword =\n");

        Assert.Throws<ConfigException>(() => AppSettings.LoadRequired());
        Assert.Equal("127.0.0.1", AppSettings.LoadRequired(requirePassword: false).Host);
    }

    [Fact]
    public void ReadsValuesIgnoringCommentsAndKeyCase()
    {
        WriteConfig("; comment\n[Database]\nHOST = server\nPassword = secret\n[options]\nby_month = sim\n");

        var settings = AppSettings.LoadRequired();

        Assert.Equal("server", settings.Host);
        Assert.Equal("secret", settings.Password);
        Assert.True(settings.ByMonth);
        Assert.True(settings.IncludeCancellation);
        Assert.False(settings.IncludeUnauthorized);
        Assert.Equal("1", settings.Series);
    }

    [Fact]
    public void SaveWritesFlagsAndDropsLegacyKey()
    {
        WriteConfig("[database]\nhost = server\nfbclient = C:\\fb\\fbclient.dll\npassword = x\n");

        var settings = AppSettings.Load();
        settings.ExportOptions = new ExportOptions { IncludeCancellation = false, IncludeUnauthorized = true, ByMonth = true };
        settings.XmlFolder = @"C:\XML";
        settings.Save();

        var text = File.ReadAllText(AppPaths.ConfigFile);
        Assert.DoesNotContain("fbclient", text);
        Assert.Contains("include_cancellation = no", text);
        Assert.Contains("include_unauthorized = yes", text);
        Assert.Contains("by_month = yes", text);
        Assert.Contains(@"xml_folder = C:\XML", text);
        Assert.Equal("server", AppSettings.LoadRequired().Host);
    }
}
