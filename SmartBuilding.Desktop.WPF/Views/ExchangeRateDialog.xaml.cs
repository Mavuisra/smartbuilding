using System.Globalization;
using System.Windows;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class ExchangeRateDialog : Window
{
    public decimal? ExchangeRate { get; private set; }

    public ExchangeRateDialog(decimal? currentRate, Window? owner)
    {
        InitializeComponent();
        if (owner is not null)
            Owner = owner;
        if (currentRate is > 0)
            RateBox.Text = currentRate.Value.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        RateBox.Focus();
    }

    private void OnValidate(object sender, RoutedEventArgs e)
    {
        var raw = RateBox.Text?.Replace(" ", "").Replace(",", ".") ?? "";
        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
        {
            ErrorText.Text = "Saisissez un taux strictement positif (ex. 2850).";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ExchangeRate = rate;
        DialogResult = true;
        Close();
    }
}
