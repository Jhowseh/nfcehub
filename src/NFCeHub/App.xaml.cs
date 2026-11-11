using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NFCeHub.Core;

namespace NFCeHub;

public partial class App : Application
{
    public const string Title = "NFCeHub";

    private Mutex instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instanceMutex = new Mutex(false, @"Local\NFCeHub", out var created);
        if (!created)
        {
            NativeMethods.BringToFront(Title);
            Shutdown();
            return;
        }
        Log.Setup("nfcehub");
        NativeMethods.SetAppId("NFCeHub");
        Theme.Apply(Resources);
        DispatcherUnhandledException += OnUnhandledException;
        var window = new global::NFCeHub.MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Window error", e.Exception);
        e.Handled = true;
        try
        {
            Dialogs.Error(Current.MainWindow, "Algo deu errado", e.Exception.Message);
        }
        catch (Exception dialogError)
        {
            Log.Error("Error dialog failed", dialogError);
            MessageBox.Show(e.Exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
