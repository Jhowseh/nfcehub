using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace NFCeHub;

public partial class AboutPanel : UserControl
{
    public const string Company = "PBtec - Soluções em Automação";
    public const string Phone = "(83) 3224-3080";

    public static readonly string[] Developers = { "José Henrique", "Erick Henrique", "Eduardo Vasconcelos" };

    public AboutPanel()
    {
        InitializeComponent();
        Header.AddSpaced(VersionLabel, "Versão " + Version);
        CompanyText.Text = Company;
        PhoneText.Text = Phone;
        DeveloperList.ItemsSource = Developers;
    }

    public event EventHandler Back;

    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version.Build > 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
        }
    }

    public void FocusBack() => BackButton.Focus();

    private void BackClick(object sender, RoutedEventArgs e) => Back?.Invoke(this, EventArgs.Empty);
}
