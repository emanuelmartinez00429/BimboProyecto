using System;
using System.Windows;
using System.Windows.Controls;
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
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text))
        {
            MessageBox.Show("El nombre es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnGuardar.IsEnabled = false;
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

            if (_esNuevo)
            {
                var r = await _repo.CreateAsync(dto);
                if (!r.Success)
                {
                    MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                var r = await _repo.UpdateAsync(dto);
                if (!r.Success)
                {
                    MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            Guardado?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error inesperado: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnGuardar.IsEnabled = true;
        }
    }
}
