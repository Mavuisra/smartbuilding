using System.Windows;
using System.Windows.Controls;

namespace SmartBuilding.Desktop.WPF.Services;

public static class SbmsDialogService
{
    public static void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void ShowWarning(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static string? PromptText(string title, string message, string defaultValue = "")
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        string? result = null;
        var accepted = false;

        var window = new Window
        {
            Title = title,
            Width = 440,
            Height = 200,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White
        };

        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        panel.Children.Add(textBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancel = new Button { Content = "Annuler", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => window.Close();

        var ok = new Button { Content = "OK", Padding = new Thickness(20, 8, 20, 8), IsDefault = true };
        ok.Click += (_, _) =>
        {
            accepted = true;
            result = textBox.Text;
            window.Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        window.Content = panel;
        textBox.Focus();
        textBox.SelectAll();

        window.ShowDialog();
        return accepted ? result?.Trim() : null;
    }
}
