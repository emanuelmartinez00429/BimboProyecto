---
title: Módulo Contactos (Drill-down)
tags:
  - bimbo
  - modulo
  - contactos
  - drill-down
date: 2026-06-21
---

# Módulo Contactos (Drill-down)

> [!abstract]
> Patrón de vista de detalle anidada: el usuario ve una lista principal (Fabricantes o Proveedores) y al hacer doble clic entra a ver y gestionar los contactos de ese registro. Un solo `UserControl` alterna entre dos paneles mediante un bool `IsViewingContacts`.

Implementado para dos entidades en paralelo:
- **Contactos Fabricantes** — `ContactosFabricantesView` / `ContactosFabricantesViewModel`
- **Contactos Proveedores** — `ContactosProveedoresView` / `ContactosProveedoresViewModel`

---

## Archivos

```
CapaAplicacion4/Contactos/
  Fabricantes/
    Dtos/ContactoFabricanteDto.cs         → { Id, IdFabricante, Nombre, Telefono, Correo, IdEstado }
    Interfaces/IContactoFabricanteRepository.cs
  Proveedores/
    Dtos/ContactoProveedorDto.cs          → { Id, IdProveedor, Nombre, Telefono, Correo, IdEstado }
    Interfaces/IContactoProveedorRepository.cs

CapaDatos/
  Modelados/Contactos/
    ContactoFabricanteModel.cs            → [Table("contactos_fabricante")], hereda BaseModel
    ContactoProveedorModel.cs             → [Table("contactos_proveedor")], hereda BaseModel
  Repositories/Contactos/
    ContactoFabricanteCrudRepository.cs   → : RepositorioBase, IContactoFabricanteRepository
    ContactoProveedorCrudRepository.cs    → : RepositorioBase, IContactoProveedorRepository

CapaUI/Formularios/Principal/Pantallas/
  ContactosFabricantes/
    ContactosFabricantesView.xaml(.cs)    → UserControl con PanelFabricantes + PanelContactos
    ContactosFabricantesViewModel.cs      → hereda RealtimeAwareViewModel
    ContactoFabricanteModal.xaml(.cs)     → modal crear/editar contacto
  ContactosProveedores/
    ContactosProveedoresView.xaml(.cs)
    ContactosProveedoresViewModel.cs
    ContactoProveedorModal.xaml(.cs)
```

---

## Contratos de repositorio

```csharp
// Ambas interfaces tienen la misma forma — ejemplo para Fabricantes:
public interface IContactoFabricanteRepository
{
    Task<Result<IReadOnlyList<ContactoFabricanteDto>>> GetByFabricanteAsync(int idFabricante, CancellationToken ct = default);
    Task<Result<int>>  CreateAsync(ContactoFabricanteDto dto, CancellationToken ct = default);
    Task<Result>       UpdateAsync(ContactoFabricanteDto dto, CancellationToken ct = default);
    Task<Result>       DeleteAsync(int id,                    CancellationToken ct = default);
}
```

> [!important] Soft-delete
> `DeleteAsync` **no ejecuta `DELETE`**. Hace `UPDATE id_estado = EstadoRegistro.Inactivo (2)`.
> `GetByFabricanteAsync` filtra `id_estado = EstadoRegistro.Activo (1)` → los inactivos no aparecen nunca en la UI.

---

## Implementación del repositorio

```csharp
// ContactoFabricanteCrudRepository : RepositorioBase, IContactoFabricanteRepository
// Todo método usa TryAsync — sin try/catch manual.

// Lectura: filtros server-side, orden por nombre ASC
public Task<Result<IReadOnlyList<ContactoFabricanteDto>>> GetByFabricanteAsync(int idFabricante, ...) =>
    TryAsync(async () =>
    {
        var client = await ConexionSupabase.GetClientAsync();
        var result = await client.From<ContactoFabricanteModel>()
            .Filter("id_fabricante", Op.Equals, idFabricante.ToString())
            .Filter("id_estado",     Op.Equals, EstadoRegistro.Activo.ToString())
            .Order("nombre_contacto", Ord.Ascending)
            .Get();
        return (IReadOnlyList<ContactoFabricanteDto>)result.Models.Select(Map).ToList();
    }, "Cargar contactos fabricante");

// Creación: ORM Insert, devuelve la PK nueva
public Task<Result<int>> CreateAsync(ContactoFabricanteDto dto, ...) =>
    TryAsync(async () =>
    {
        var client  = await ConexionSupabase.GetClientAsync();
        var nuevo   = new ContactoFabricanteModel { idFabricante = dto.IdFabricante, ... };
        var result  = await client.From<ContactoFabricanteModel>().Insert(nuevo);
        return result.Models.First().idContactoFabricante;   // PK del row insertado
    }, "Crear contacto fabricante");

// Soft-delete
public Task<Result> DeleteAsync(int id, ...) =>
    TryAsync(async () =>
    {
        var client = await ConexionSupabase.GetClientAsync();
        await client.From<ContactoFabricanteModel>()
            .Where(c => c.idContactoFabricante == id)
            .Set(c => c.idEstado, EstadoRegistro.Inactivo)
            .Update();
    }, "Eliminar contacto fabricante");
```

---

## Patrón drill-down — cómo funciona

El `UserControl` tiene **dos paneles** en el mismo Grid:

```xml
<!-- PanelFabricantes: lista principal con paginación y buscador -->
<Grid x:Name="PanelFabricantes" Visibility="Visible">
    <DataGrid x:Name="DgFabricantes" .../>
</Grid>

<!-- PanelContactos: detalle del fabricante seleccionado -->
<Grid x:Name="PanelContactos" Visibility="Collapsed">
    <DataGrid x:Name="DgContactos" .../>
</Grid>
```

La alternancia la controla `IsViewingContacts` en el ViewModel:

```csharp
public bool IsViewingContacts => FabricanteSeleccionado is not null;
```

En el code-behind, `OnVmPropertyChanged` llama `AlternarPaneles()` cuando `IsViewingContacts` cambia:

```csharp
private void AlternarPaneles()
{
    bool viendo = _vm.IsViewingContacts;
    PanelFabricantes.Visibility = viendo ? Visibility.Collapsed : Visibility.Visible;
    PanelContactos.Visibility   = viendo ? Visibility.Visible   : Visibility.Collapsed;
    BtnVolver.Visibility        = viendo ? Visibility.Visible   : Visibility.Collapsed;
    BtnNuevoContacto.Visibility = viendo ? Visibility.Visible   : Visibility.Collapsed;
}
```

**Flujo de navegación:**
```
DgFabricantes.MouseDoubleClick
    ↓ _vm.AbrirFabricanteAsync(fab)
    │   ├─ FabricanteSeleccionado = fab    → IsViewingContacts = true
    │   ├─ ContactoSeleccionado = null
    │   └─ CargarContactosAsync()
    ↓ OnVmPropertyChanged → AlternarPaneles()
    → PanelFabricantes se oculta, PanelContactos se muestra

BtnVolver.Click
    ↓ _vm.VolverCommand
    │   ├─ FabricanteSeleccionado = null   → IsViewingContacts = false
    │   └─ Contactos.Clear()
    ↓ OnVmPropertyChanged → AlternarPaneles()
    → PanelContactos se oculta, PanelFabricantes se muestra
```

---

## ViewModel — puntos clave

```csharp
public partial class ContactosFabricantesViewModel : RealtimeAwareViewModel
{
    // Dos repositorios: uno para la lista principal, otro para los contactos
    private readonly IFabricanteRepository         _fabRepo;
    private readonly IContactoFabricanteRepository _contactoRepo;

    // Propiedad que controla el toggle de paneles
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingContacts), nameof(Titulo))]
    private FabricanteDto? _fabricanteSeleccionado;

    public bool IsViewingContacts => FabricanteSeleccionado is not null;

    // Título dinámico: "Fabricantes" o el nombre del fabricante seleccionado
    public string Titulo => FabricanteSeleccionado is null
        ? "Fabricantes"
        : FabricanteSeleccionado.Nombre;
}
```

### Carga de contactos

```csharp
public async Task CargarContactosAsync()
{
    if (FabricanteSeleccionado is null) return;
    IsLoadingContactos = true;

    var r = await _contactoRepo.GetByFabricanteAsync(FabricanteSeleccionado.Id);
    IsLoadingContactos = false;

    if (!r.Success) { ErrorCarga = r.Error; return; }
    Contactos = new ObservableCollection<ContactoFabricanteDto>(r.Value!);
}
```

---

## Realtime — handler simplificado (sin paginación)

La lista de contactos por fabricante es pequeña → **no pagina** → no se necesita la heurística INSERT/última-página de Productos.

Sigue el patrón del [[Checklist - Replicar Módulo con Realtime]] §5 "Módulos sin paginación":

```csharp
private void OnCambioContacto(CambioRealtime cambio)
{
    // Guard: si el VM ya fue liberado o no hay fabricante activo, ignorar
    if (Disposed || FabricanteSeleccionado is null) return;

    // Solo INSERT y UPDATE disparan recarga (DELETE = soft, llega como UPDATE)
    if (!string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase)
     && !string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase)) return;

    _ = CargarContactosAsync();   // recarga completa, sin spinner
}
```

Las tablas `contactos_fabricante` e `contactos_proveedor` están registradas en `RealtimeService._pkColumns`:

```csharp
["contactos_fabricante"] = "id_contacto_fabricante",
["contactos_proveedor"]  = "id_contacto_proveedor",
```

---

## Modal crear/editar

`ContactoFabricanteModal` recibe tres parámetros:

```csharp
public ContactoFabricanteModal(
    IContactoFabricanteRepository repo,
    FabricanteDto                 fabricante,   // para mostrar nombre en la tarjeta
    ContactoFabricanteDto?        contacto)      // null = nuevo, valor = edición
```

- Campos: `TxtNombre` (obligatorio), `TxtTelefono`, `TxtCorreo`
- Validación mínima: nombre no vacío → `MessageBox.Show`
- Usa `Result<T>` del repositorio: si `!r.Success` muestra error sin lanzar excepción
- Dispara evento `Guardado` → la vista padre llama `await _vm.CargarContactosAsync()`

### Workaround `TemplateBinding Tag` para el botón Guardar

El texto del botón cambia según modo (nuevo vs editar). Como el `TextBlock` vive dentro de un `ControlTemplate`, no es accesible como elemento nombrado desde el code-behind.

**Solución:** `Text="{TemplateBinding Tag}"` en el XAML + `BtnGuardar.Tag = _esNuevo ? "Agregar" : "Guardar cambios"` en `OnLoaded`.

---

## Layout — patrón de márgenes

Igual que Productos: Grid exterior sin margen + Grid interior con `Margin="22,18,22,0"` + `ModalOverlay` fuera del Grid interior como hermano con `Panel.ZIndex="100"`.

```xml
<Grid>                               <!-- exterior: lienzo completo -->
    <Grid Margin="22,18,22,0">       <!-- interior: contenido con margen -->
        ...header, toolbar, paneles...
    </Grid>
    <Border x:Name="ModalOverlay"    <!-- overlay fuera del margen -->
            Background="#80000000"
            Visibility="Collapsed"
            Panel.ZIndex="100">
        <ContentControl x:Name="ModalContent" .../>
    </Border>
</Grid>
```

> [!note] Por qué el overlay va fuera
> Si viviera dentro del Grid con margen, la capa oscura dejaría una franja de 22 px sin cubrir en los bordes. Al sacarlo, el modal cubre toda la pantalla mientras el contenido conserva su margen.

---

## DI y navegación

```csharp
// CapaDatos/DependencyInjection.cs
services.AddTransient<IContactoFabricanteRepository, ContactoFabricanteCrudRepository>();
services.AddTransient<IContactoProveedorRepository,  ContactoProveedorCrudRepository>();

// CapaUI/App.xaml.cs
services.AddTransient<ContactosFabricantesViewModel>();
services.AddTransient<ContactosProveedoresViewModel>();
```

Navegación — patrón estándar del proyecto:

```csharp
// MainViewModel.cs — marcadores vacíos
public class ContactosFabricantesVM : ViewModelBase { }
public class ContactosProveedoresVM : ViewModelBase { }

// _routes dict
[Routes.ContactosFabricantes] = () => new ContactosFabricantesVM(),
[Routes.ContactosProveedores] = () => new ContactosProveedoresVM(),
```

```xml
<!-- MainWindow.xaml — DataTemplates -->
<DataTemplate DataType="{x:Type local:ContactosFabricantesVM}">
    <cfabs:ContactosFabricantesView/>
</DataTemplate>
<DataTemplate DataType="{x:Type local:ContactosProveedoresVM}">
    <cprovs:ContactosProveedoresView/>
</DataTemplate>
```

---

## Patrones en uso

- [[Repository Pattern]] — `IContactoXxxRepository` inyectado en ViewModel
- [[Result Pattern]] — todas las operaciones devuelven `Result<T>` / `Result`
- [[Base Repository con TryAsync]] — `RepositorioBase.TryAsync` en cada método
- [[Observer Pattern]] — `ObservableObject` + source generators
- [[Clean Architecture]] — contrato en `CapaAplicacion`, implementación en `CapaDatos`
- [[Checklist - Replicar Módulo con Realtime]] — handler simplificado (sin paginación)
- [[Módulo Productos]] — módulo de referencia para paginación, buscador y layout

---

## Notas críticas

> [!bug] `FabricanteModel` no hereda `BaseModel`
> Igual que en el módulo Productos: nunca usar `client.From<Fabricante>()`. Los fabricantes se cargan siempre vía `IFabricanteRepository`.

> [!warning] La lista de contactos **no pagina**
> Los contactos de un fabricante/proveedor son pocos. No se implementó paginación intencionalmente. Si crece, añadir paginación siguiendo el patrón de `ProductosViewModel`.

> [!info] Buscador solo en la lista principal
> El buscador (debounce 300ms + ILike) aplica a la lista de Fabricantes/Proveedores, no a los contactos dentro del detalle. En la vista de detalle se asume que la lista es corta y visible completa.
