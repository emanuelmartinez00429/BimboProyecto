using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Services.Navigation;

public class EntityDetailWindow : Window
{
    public EntityDetailWindow(string entityType, object? id)
    {
        Title  = $"Detalle — {entityType}";
        Width  = 480;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;

        Content = new TextBlock
        {
            Text = $"Vista de detalle para {entityType}\nId: {id}",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextAlignment       = System.Windows.TextAlignment.Center,
        };
    }
}
