namespace CapaUI.Core.Controls;

public class SuggestionItemData
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Meta   { get; set; } = "";
    public bool   Activo { get; set; }
    public object Source { get; set; } = null!;
}
