using System;
using System.Windows;
using System.Windows.Controls;
using CapaUI.Core.Validacion;
using CapaAplicacion.Common;
using CapaAplicacion.Contactos.Proveedores.Dtos;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.ContactosProveedores;

public partial class ContactoProveedorModal : UserControl
{
    private readonly IContactoProveedorRepository _repo;
    private readonly ProveedorDto                 _proveedor;
    private readonly ContactoProveedorDto?        _contacto;
    private readonly bool                         _esNuevo;
    private ValidadorFormulario                   _validador = null!;

    public event Action? Cerrado;
    public event Action? Guardado;

    public ContactoProveedorModal(
        IContactoProveedorRepository repo,
        ProveedorDto                 proveedor,
        ContactoProveedorDto?        contacto)
    {
        _repo      = repo;
        _proveedor = proveedor;
        _contacto  = contacto;
        _esNuevo   = contacto == null;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Telefono y correo son opcionales, pero si se llenan tienen que tener
        // forma valida. Hasta ahora iban crudos a la base sin mirarlos.
        _validador = ValidadorFormulario.Nuevo()
            .Campo(TxtNombre, "El nombre").Obligatorio().LargoMaximo(100)
            .Campo(TxtTelefono, "El telefono").Telefono()
            .Campo(TxtCorreo, "El correo").Correo()
            .ValidarAlSalirDelCampo();

        TxtModalContext.Text    = _esNuevo ? "NUEVO · CONTACTO" : "EDICIÓN · CONTACTO";
        TxtModalTitle.Text      = _esNuevo ? "Agregar contacto" : "Editar contacto";
        TxtProveedorNombre.Text = _proveedor.Nombre;
        BtnGuardar.Tag          = _esNuevo ? "Agregar" : "Guardar cambios";

        if (!_esNuevo && _contacto is not null)
        {
            TxtNombre.Text   = _contacto.Nombre;
            TxtTelefono.Text = _contacto.Telefono;
            TxtCorreo.Text   = _contacto.Correo;
        }

        // Foco en el primer campo al abrir: el usuario no tiene que clickear
        // nada para empezar a escribir.
        TxtNombre.Focus();
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (!_validador.Validar()) return;

        // Guardar es un viaje de red: sin este aviso la espera se lee como
        // que la aplicacion se colgo.
        var etiquetaGuardar  = BtnGuardar.Tag;
        BtnGuardar.IsEnabled = false;
        BtnGuardar.Tag       = "Guardando...";
        try
        {
            var dto = new ContactoProveedorDto
            {
                Id          = _esNuevo ? 0 : _contacto!.Id,
                IdProveedor = _proveedor.Id,
                Nombre      = TxtNombre.Text.Trim(),
                Telefono    = TxtTelefono.Text.Trim(),
                Correo      = TxtCorreo.Text.Trim(),
                IdEstado    = EstadoRegistro.Activo,
            };

            bool exito;
            string error;

            if (_esNuevo)
            {
                var r = await _repo.CreateAsync(dto);
                (exito, error) = (r.Success, r.Error);
            }
            else
            {
                var r = await _repo.UpdateAsync(dto);
                (exito, error) = (r.Success, r.Error);
            }

            if (!exito)
            {
                ErroresRepositorio.Mostrar(error, null, TxtNombre);
                return;
            }

            Guardado?.Invoke();
        }
        catch (Exception ex)
        {
            ErroresRepositorio.MostrarInesperado(ex);
        }
        finally
        {
            BtnGuardar.Tag       = etiquetaGuardar;
            BtnGuardar.IsEnabled = true;
        }
    }
}
