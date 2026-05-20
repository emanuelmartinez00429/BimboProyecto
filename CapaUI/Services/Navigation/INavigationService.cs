namespace CapaUI.Services.Navigation;

public interface INavigationService
{
    void NavigateTo(string entityType, object? navigationParam = null);
}
