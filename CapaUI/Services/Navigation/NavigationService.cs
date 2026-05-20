using System.Windows;

namespace CapaUI.Services.Navigation;

public class NavigationService : INavigationService
{
    public void NavigateTo(string entityType, object? navigationParam = null)
    {
        var win = new EntityDetailWindow(entityType, navigationParam);
        win.Show();
    }
}
