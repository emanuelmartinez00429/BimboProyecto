using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Pesaje.Interfaces;
using CapaDatos.Repositories.Pesaje;
using Xunit;

namespace BimboProyecto.Tests.Pesaje;

public sealed class PesajeRepositoryLoteTests
{
#pragma warning disable CS0067
    private sealed class FakeConexionMonitor : IConexionMonitor
    {
        public EstadoConexion Estado { get; set; } = EstadoConexion.Conectado;
        public event EventHandler<EstadoConexion>? EstadoCambiado;
        public event EventHandler? Reconectado;
        public void Iniciar() { }
        public void Detener() { }
    }
#pragma warning restore CS0067

    [Fact(DisplayName = "RegistrarCamionesLoteAsync con coleccion vacia retorna Ok(0,0,[]) sin invocar backend")]
    public async Task RegistrarCamionesLoteAsync_ColeccionVacia_RetornaOkConCeroInmediatamente()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.Conectado };
        var repo = new PesajeRepository(monitor);

        var resultado = await repo.RegistrarCamionesLoteAsync(
            Array.Empty<(string Placa, int IdProveedor, string? Observaciones)>(),
            Guid.NewGuid());

        Assert.True(resultado.Success);
        Assert.NotNull(resultado.Value);
        Assert.Equal(0, resultado.Value.Creados);
        Assert.Equal(0, resultado.Value.PrimerIdMovimiento);
        Assert.Empty(resultado.Value.IdsMovimiento);
    }

    [Fact(DisplayName = "RegistrarCamionesLoteAsync con coleccion nula retorna Ok(0,0,[]) sin invocar backend")]
    public async Task RegistrarCamionesLoteAsync_ColeccionNula_RetornaOkConCeroInmediatamente()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.Conectado };
        var repo = new PesajeRepository(monitor);

        var resultado = await repo.RegistrarCamionesLoteAsync(
            null!,
            Guid.NewGuid());

        Assert.True(resultado.Success);
        Assert.NotNull(resultado.Value);
        Assert.Equal(0, resultado.Value.Creados);
        Assert.Equal(0, resultado.Value.PrimerIdMovimiento);
        Assert.Empty(resultado.Value.IdsMovimiento);
    }

    [Fact(DisplayName = "RegistrarCamionesLoteAsync sin conexion retorna Fail('Sin conexión a internet.') de inmediato")]
    public async Task RegistrarCamionesLoteAsync_SinConexion_RetornaFailFailFast()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.SinConexion };
        var repo = new PesajeRepository(monitor);

        var camiones = new List<(string Placa, int IdProveedor, string? Observaciones)>
        {
            ("ABC-123", 1, "Prueba"),
        };

        var resultado = await repo.RegistrarCamionesLoteAsync(camiones, Guid.NewGuid());

        Assert.False(resultado.Success);
        Assert.Equal("Sin conexión a internet.", resultado.Error);
    }

    [Fact(DisplayName = "RegistrarCamionesLoteAsync captura errores dentro de TryAsync y retorna Result.Fail sin crashear")]
    public async Task RegistrarCamionesLoteAsync_CapturaExcepcionesDeInfraestructuraConTryAsync()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.Conectado };
        var repo = new PesajeRepository(monitor);

        var camiones = new List<(string Placa, int IdProveedor, string? Observaciones)>
        {
            ("ABC-123", 1, "Prueba"),
        };

        // En un entorno de tests unitarios sin credenciales reales ni mock de Supabase,
        // la llamada a ConexionSupabase.GetClientAsync() o PostgREST lanza excepción.
        // TryAsync DEBE capturarla y retornar Result.Fail con el prefijo de contexto.
        var resultado = await repo.RegistrarCamionesLoteAsync(camiones, Guid.NewGuid());

        Assert.False(resultado.Success);
        Assert.NotNull(resultado.Error);
        Assert.StartsWith("Registrar camiones en lote:", resultado.Error);
    }

    [Fact(DisplayName = "RegistrarCamionesLoteAsync con CancellationToken cancelado propaga OperationCanceledException")]
    public async Task RegistrarCamionesLoteAsync_TokenCancelado_LanzaOperationCanceledException()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.Conectado };
        var repo = new PesajeRepository(monitor);

        var camiones = new List<(string Placa, int IdProveedor, string? Observaciones)>
        {
            ("ABC-123", 1, "Prueba"),
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await repo.RegistrarCamionesLoteAsync(camiones, Guid.NewGuid(), cts.Token);
        });
    }

    [Fact(DisplayName = "RegistrarCamionesLoteAsync maneja elementos con Placa nula u Observaciones nulas sin lanzar NullReferenceException")]
    public async Task RegistrarCamionesLoteAsync_ElementosConValoresNulos_NoLanzanNullReference()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.Conectado };
        var repo = new PesajeRepository(monitor);

        var camiones = new List<(string Placa, int IdProveedor, string? Observaciones)>
        {
            (null!, 1, null),
            ("   ", 2, "   "),
        };

        // No debe lanzar NullReferenceException al armar loteJson dentro del TryAsync
        var resultado = await repo.RegistrarCamionesLoteAsync(camiones, Guid.Empty);

        Assert.False(resultado.Success);
        Assert.DoesNotContain("NullReferenceException", resultado.Error);
        Assert.StartsWith("Registrar camiones en lote:", resultado.Error);
    }
}
