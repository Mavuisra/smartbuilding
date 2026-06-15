using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class LoginView : UserControl
{
    private TextBox? _passwordTextBox;
    private bool _passwordVisible;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ResetPasswordFields();
    }

    private void ResetPasswordFields()
    {
        PasswordBox.Password = string.Empty;
        if (_passwordTextBox is not null)
            _passwordTextBox.Text = string.Empty;

        if (DataContext is ViewModels.LoginViewModel vm)
            vm.ClearPassword();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LoginViewModel vm)
            vm.Password = _passwordVisible && _passwordTextBox is not null
                ? _passwordTextBox.Text
                : PasswordBox.Password;
    }

    private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (!_passwordVisible)
        {
            _passwordTextBox = new TextBox
            {
                Style = (Style)FindResource("LoginInnerTextBox"),
                Text = PasswordBox.Password,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 12, 4, 12)
            };
            _passwordTextBox.TextChanged += (_, _) =>
            {
                if (DataContext is ViewModels.LoginViewModel vm)
                    vm.Password = _passwordTextBox!.Text;
            };

            var parent = PasswordBox.Parent as Grid;
            if (parent is not null)
            {
                Grid.SetColumn(_passwordTextBox, 1);
                parent.Children.Remove(PasswordBox);
                parent.Children.Add(_passwordTextBox);
            }
            _passwordVisible = true;
            EyeIcon.Kind = PackIconKind.EyeOffOutline;
            TogglePasswordButton.ToolTip = "Masquer le mot de passe";
        }
        else
        {
            if (_passwordTextBox is not null)
            {
                PasswordBox.Password = _passwordTextBox.Text;
                var parent = _passwordTextBox.Parent as Grid;
                parent?.Children.Remove(_passwordTextBox);
                parent?.Children.Add(PasswordBox);
                Grid.SetColumn(PasswordBox, 1);
                _passwordTextBox = null;
            }
            _passwordVisible = false;
            EyeIcon.Kind = PackIconKind.EyeOutline;
            TogglePasswordButton.ToolTip = "Afficher le mot de passe";
            if (DataContext is ViewModels.LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        }
    }
}
