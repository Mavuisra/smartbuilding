namespace SmartBuilding.Desktop.WPF.Services;

public class NavigationService
{
    public event Action<Type>? NavigationRequested;

    public void NavigateTo<TViewModel>() where TViewModel : class =>
        NavigationRequested?.Invoke(typeof(TViewModel));
}
