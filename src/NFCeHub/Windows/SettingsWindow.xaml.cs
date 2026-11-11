using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NFCeHub.Core;

namespace NFCeHub;

public partial class SettingsWindow : Window
{
    private static SettingsWindow openWindow;

    private readonly Action onSave;
    private bool testing;

    private SettingsWindow(Action onSave)
    {
        this.onSave = onSave;
        InitializeComponent();
        Theme.Prepare(this);
        HeaderBar.Setup(520, "Configuração", "Conexão com o banco",
            "O programa só lê o banco do SysPDV. Nunca grava nada.", withStatus: false);
        var settings = AppSettings.Load();
        HostField.Text = settings.Host;
        PortField.Text = settings.Port;
        PathField.Text = settings.DatabasePath;
        UserField.Text = settings.User;
        PasswordField.Password = settings.Password;
        KeyDown += OnKey;
        Closed += (_, _) => openWindow = null;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(PasswordField.Password))
            {
                PasswordField.Focus();
            }
        };
    }

    public static void Open(Action onSave = null)
    {
        if (openWindow != null)
        {
            NativeMethods.Raise(openWindow);
            return;
        }
        openWindow = new SettingsWindow(onSave);
        openWindow.Show();
        NativeMethods.Raise(openWindow);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            Save();
        }
    }

    private AppSettings SettingsFromWindow()
    {
        var settings = AppSettings.Load();
        settings.Host = HostField.Text.Trim();
        settings.Port = PortField.Text.Trim();
        settings.DatabasePath = PathField.Text.Trim();
        settings.User = UserField.Text.Trim();
        settings.Password = PasswordField.Password.Trim();
        return settings;
    }

    private void Notice(string text, string color)
    {
        ResultText.Text = text;
        ResultText.Foreground = Theme.Resource(color);
    }

    private async void TestConnection(object sender, RoutedEventArgs e)
    {
        if (testing)
        {
            return;
        }
        testing = true;
        Notice("Testando a conexão…", "Muted");
        Cursor = Cursors.Wait;
        var settings = SettingsFromWindow();
        try
        {
            await Task.Run(() =>
            {
                using var db = Database.Open(settings);
                SysPdv.Ping(db);
            });
            Notice("Conectado!", "Green");
        }
        catch (Exception error)
        {
            Notice($"Não conectou: {error.Message}", "Red");
        }
        finally
        {
            testing = false;
            Cursor = null;
        }
    }

    private void SaveClick(object sender, RoutedEventArgs e) => Save();

    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private void Save()
    {
        var settings = SettingsFromWindow();
        if (string.IsNullOrEmpty(settings.Password))
        {
            Notice("Preencha a senha do banco.", "Yellow");
            PasswordField.Focus();
            return;
        }
        try
        {
            settings.Save();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Notice($"Não foi possível gravar {AppPaths.ConfigFile}: {error.Message}", "Red");
            return;
        }
        Close();
        onSave?.Invoke();
    }
}
