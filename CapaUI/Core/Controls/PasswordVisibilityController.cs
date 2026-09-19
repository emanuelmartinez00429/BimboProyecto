using System;
using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Core.Controls;

/// <summary>
/// Sincroniza un PasswordBox canónico con el TextBox usado para mostrar
/// temporalmente la contraseña.
/// </summary>
internal sealed class PasswordVisibilityController
{
    private readonly PasswordBox _passwordBox;
    private readonly TextBox _visibleTextBox;
    private bool _synchronizing;

    public PasswordVisibilityController(PasswordBox passwordBox, TextBox visibleTextBox)
    {
        _passwordBox = passwordBox ?? throw new ArgumentNullException(nameof(passwordBox));
        _visibleTextBox = visibleTextBox ?? throw new ArgumentNullException(nameof(visibleTextBox));

        _passwordBox.Visibility = Visibility.Visible;
        _visibleTextBox.Visibility = Visibility.Collapsed;
        _visibleTextBox.Text = string.Empty;
        _visibleTextBox.TextChanged += VisibleTextBox_TextChanged;
    }

    public string Password => _passwordBox.Password;
    public bool IsVisible { get; private set; }

    public void Toggle() => SetVisible(!IsVisible);

    public void SetVisible(bool visible)
    {
        if (visible == IsVisible)
            return;

        _synchronizing = true;
        try
        {
            if (visible)
            {
                _visibleTextBox.MaxLength = _passwordBox.MaxLength;
                _visibleTextBox.Text = _passwordBox.Password;
                _passwordBox.Visibility = Visibility.Collapsed;
                _visibleTextBox.Visibility = Visibility.Visible;
                _visibleTextBox.Focus();
                _visibleTextBox.CaretIndex = _visibleTextBox.Text.Length;
            }
            else
            {
                _passwordBox.Password = _visibleTextBox.Text;
                _visibleTextBox.Visibility = Visibility.Collapsed;
                _passwordBox.Visibility = Visibility.Visible;
                _passwordBox.Focus();

                // No conservar otra copia en texto plano mientras está oculto.
                _visibleTextBox.Text = string.Empty;
            }

            IsVisible = visible;
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void VisibleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizing || !IsVisible)
            return;

        _synchronizing = true;
        try
        {
            // Preserva los validadores y consumidores conectados al PasswordBox.
            _passwordBox.Password = _visibleTextBox.Text;
        }
        finally
        {
            _synchronizing = false;
        }
    }
}
