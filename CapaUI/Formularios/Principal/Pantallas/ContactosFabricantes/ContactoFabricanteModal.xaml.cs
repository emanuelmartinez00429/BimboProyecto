using System;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common;
using CapaAplicacion.Contactos.Fabricantes.Dtos;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.ContactosFabricantes;

public partial class ContactoFabricanteModal : UserControl
{
    private readonly IContactoFabricanteRepository _repo;
    private readonly FabricanteDto                 _fabricante;
    private readonly ContactoFabricanteDto?        _contacto;
    private readonly bool                          _esNuevo;

    public event Action? Cerrado;
    public event Action? Guardado;

    public ContactoFabricanteModal(
        IContactoFabricanteRepository repo,
        FabricanteDto                 fabricante,
        ContactoFabricanteDto?        contacto)
    {
        _repo       = repo;
        _fabricante = fabricante;
        _contacto   = contacto;
        _esNuevo    = contacto == null;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TxtModalContext.Text     = _esNuevo ? "NUEVO · CONTACTO" : "EDICIÓN · CONTACTO";
        TxtModalTitle.Text       = _esNuevo ? "Agregar contacto" : "Editar contacto";
        TxtFabricanteNombre.Text = _fabricante.Nombre;
        BtnGuardar.Tag           = _esNuevo ? "Agregar" : "Guardar cambios";

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
        if (string.IsNullOrWhiteSpace(TxtNombre.Text))
        {
            MessageBox.Show("El nombre es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Guardar es un viaje de red: sin este aviso la espera se lee como
        // que la aplicacion se colgo.
        var etiquetaGuardar  = BtnGuardar.Tag;
        BtnGuardar.IsEnabled = false;
        BtnGuardar.Tag       = "Guardando...";
        try
        {
            var dto = new ContactoFabricanteDto
            {
                Id           = _esNuevo ? 0 : _contacto!.Id,
                IdFabricante = _fabricante.Id,
                Nombre       = TxtNombre.Text.Trim(),
                Telefono     = TxtTelefono.Text.Trim(),
                Correo       = TxtCorreo.Text.Trim(),
                IdEstado     = EstadoRegistro.Activo,
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
            BtnGuardar.Tag       = etiquetaGuardar;
            BtnGuardar.IsEnabled = true;
        }
    }
}
