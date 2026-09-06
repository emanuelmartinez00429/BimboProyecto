using System.Reflection;
using CapaAplicacion.Common;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Repositories.Pesaje;
using CapaDatos.Repositories.Usuarios;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Invariants;

/// <summary>
/// Pruebas de verificación empírica e invariantes de tipos para Milestone 2.
/// Ejecutado por Challenger M2-1.
/// </summary>
public sealed class Milestone2InvariantsVerificationTests
{
    // =========================================================================
    // 1. INVARIANTE: Inmutabilidad y estructura del DTO ResultadoAltaLoteCamiones
    // =========================================================================

    [Fact(DisplayName = "ResultadoAltaLoteCamiones es un record publico con propiedades init-only e inmutabilidad estricta")]
    public void ResultadoAltaLoteCamiones_EsRecordInmutableConPropiedadesInitOnly()
    {
        var tipo = typeof(ResultadoAltaLoteCamiones);

        // Visibilidad publica
        Assert.True(tipo.IsPublic, "ResultadoAltaLoteCamiones debe ser una clase/record publica.");

        // Es un record C# (posee metodo <Clone>$ o EqualityContract)
        var metodoClone = tipo.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var propiedadEqualityContract = tipo.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(metodoClone != null || propiedadEqualityContract != null, "ResultadoAltaLoteCamiones debe ser declarado como record.");

        // Propiedad Creados (int)
        var propCreados = tipo.GetProperty(nameof(ResultadoAltaLoteCamiones.Creados));
        Assert.NotNull(propCreados);
        Assert.Equal(typeof(int), propCreados.PropertyType);
        Assert.True(propCreados.CanRead, "Creados debe tener getter.");
        var setCreados = propCreados.GetSetMethod(nonPublic: true);
        if (setCreados != null)
        {
            var returnTypeMods = setCreados.ReturnParameter.GetRequiredCustomModifiers();
            Assert.Contains(returnTypeMods, m => m.Name == "IsExternalInit");
        }

        // Propiedad PrimerIdMovimiento (int)
        var propPrimerId = tipo.GetProperty(nameof(ResultadoAltaLoteCamiones.PrimerIdMovimiento));
        Assert.NotNull(propPrimerId);
        Assert.Equal(typeof(int), propPrimerId.PropertyType);
        Assert.True(propPrimerId.CanRead, "PrimerIdMovimiento debe tener getter.");
        var setPrimerId = propPrimerId.GetSetMethod(nonPublic: true);
        if (setPrimerId != null)
        {
            var returnTypeMods = setPrimerId.ReturnParameter.GetRequiredCustomModifiers();
            Assert.Contains(returnTypeMods, m => m.Name == "IsExternalInit");
        }

        // Propiedad IdsMovimiento (IReadOnlyList<int>)
        var propIds = tipo.GetProperty(nameof(ResultadoAltaLoteCamiones.IdsMovimiento));
        Assert.NotNull(propIds);
        Assert.Equal(typeof(IReadOnlyList<int>), propIds.PropertyType);
        Assert.True(propIds.CanRead, "IdsMovimiento debe tener getter.");
        var setIds = propIds.GetSetMethod(nonPublic: true);
        if (setIds != null)
        {
            var returnTypeMods = setIds.ReturnParameter.GetRequiredCustomModifiers();
            Assert.Contains(returnTypeMods, m => m.Name == "IsExternalInit");
        }

        // Constructor primario
        var ctor = tipo.GetConstructor(new[] { typeof(int), typeof(int), typeof(IReadOnlyList<int>) });
        Assert.NotNull(ctor);
        Assert.True(ctor.IsPublic, "El constructor de ResultadoAltaLoteCamiones debe ser publico.");

        // Comportamiento inmutable y no destructivo con 'with'
        var original = new ResultadoAltaLoteCamiones(3, 101, new[] { 101, 102, 103 });
        var modificado = original with { Creados = 4 };

        Assert.Equal(3, original.Creados);
        Assert.Equal(4, modificado.Creados);
        Assert.NotSame(original, modificado);
        Assert.NotEqual(original, modificado);

        // Igualdad por valor de record
        var clon = new ResultadoAltaLoteCamiones(3, 101, original.IdsMovimiento);
        Assert.Equal(original, clon);
        Assert.Equal(original.GetHashCode(), clon.GetHashCode());

        // Deconstruct
        var (creados, primerId, ids) = original;
        Assert.Equal(3, creados);
        Assert.Equal(101, primerId);
        Assert.Equal(3, ids.Count);
    }

    // =========================================================================
    // 2. INVARIANTE: Visibilidad y accesibilidad de ReglasProductoCamion
    // =========================================================================

    [Fact(DisplayName = "ReglasProductoCamion es clase publica estatica con campos ReglaCampo publicos estaticos readonly")]
    public void ReglasProductoCamion_CumpleEstructuraYValoresDeNegocio()
    {
        var tipo = typeof(ReglasProductoCamion);

        // Clase publica estatica (en IL: public abstract sealed)
        Assert.True(tipo.IsPublic, "ReglasProductoCamion debe ser public.");
        Assert.True(tipo.IsAbstract && tipo.IsSealed, "ReglasProductoCamion debe ser static (abstract sealed en IL).");
        Assert.Equal("CapaDominio.Reglas", tipo.Namespace);

        // Campo IdProducto
        var fIdProducto = tipo.GetField(nameof(ReglasProductoCamion.IdProducto), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fIdProducto);
        Assert.True(fIdProducto.IsInitOnly, "IdProducto debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fIdProducto.FieldType);
        Assert.True(ReglasProductoCamion.IdProducto.Obligatorio);
        Assert.Null(ReglasProductoCamion.IdProducto.LargoMaximo);
        Assert.Null(ReglasProductoCamion.IdProducto.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasProductoCamion.IdProducto.Formato);

        // Campo Cantidad
        var fCantidad = tipo.GetField(nameof(ReglasProductoCamion.Cantidad), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fCantidad);
        Assert.True(fCantidad.IsInitOnly, "Cantidad debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fCantidad.FieldType);
        Assert.True(ReglasProductoCamion.Cantidad.Obligatorio);
        Assert.Null(ReglasProductoCamion.Cantidad.LargoMaximo);
        Assert.Null(ReglasProductoCamion.Cantidad.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasProductoCamion.Cantidad.Formato);

        // Campo Observaciones (Tope 500)
        var fObs = tipo.GetField(nameof(ReglasProductoCamion.Observaciones), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fObs);
        Assert.True(fObs.IsInitOnly, "Observaciones debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fObs.FieldType);
        Assert.False(ReglasProductoCamion.Observaciones.Obligatorio);
        Assert.Equal(500, ReglasProductoCamion.Observaciones.LargoMaximo);
        Assert.Null(ReglasProductoCamion.Observaciones.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasProductoCamion.Observaciones.Formato);
    }

    // =========================================================================
    // 3. INVARIANTE: Visibilidad y accesibilidad de ReglasEntradaPesaje
    // =========================================================================

    [Fact(DisplayName = "ReglasEntradaPesaje es clase publica estatica con campos ReglaCampo publicos estaticos readonly")]
    public void ReglasEntradaPesaje_CumpleEstructuraYValoresDeNegocio()
    {
        var tipo = typeof(ReglasEntradaPesaje);

        // Clase publica estatica (en IL: public abstract sealed)
        Assert.True(tipo.IsPublic, "ReglasEntradaPesaje debe ser public.");
        Assert.True(tipo.IsAbstract && tipo.IsSealed, "ReglasEntradaPesaje debe ser static (abstract sealed en IL).");
        Assert.Equal("CapaDominio.Reglas", tipo.Namespace);

        // Campo PesoBruto
        var fPesoBruto = tipo.GetField(nameof(ReglasEntradaPesaje.PesoBruto), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fPesoBruto);
        Assert.True(fPesoBruto.IsInitOnly, "PesoBruto debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fPesoBruto.FieldType);
        Assert.True(ReglasEntradaPesaje.PesoBruto.Obligatorio);
        Assert.Null(ReglasEntradaPesaje.PesoBruto.LargoMaximo);
        Assert.Null(ReglasEntradaPesaje.PesoBruto.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasEntradaPesaje.PesoBruto.Formato);

        // Campo TaraExtra
        var fTaraExtra = tipo.GetField(nameof(ReglasEntradaPesaje.TaraExtra), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fTaraExtra);
        Assert.True(fTaraExtra.IsInitOnly, "TaraExtra debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fTaraExtra.FieldType);
        Assert.False(ReglasEntradaPesaje.TaraExtra.Obligatorio);
        Assert.Null(ReglasEntradaPesaje.TaraExtra.LargoMaximo);
        Assert.Null(ReglasEntradaPesaje.TaraExtra.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasEntradaPesaje.TaraExtra.Formato);

        // Campo Observaciones (Tope 500)
        var fObs = tipo.GetField(nameof(ReglasEntradaPesaje.Observaciones), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(fObs);
        Assert.True(fObs.IsInitOnly, "Observaciones debe ser readonly.");
        Assert.Equal(typeof(ReglaCampo), fObs.FieldType);
        Assert.False(ReglasEntradaPesaje.Observaciones.Obligatorio);
        Assert.Equal(500, ReglasEntradaPesaje.Observaciones.LargoMaximo);
        Assert.Null(ReglasEntradaPesaje.Observaciones.LargoMinimo);
        Assert.Equal(FormatoCampo.Ninguno, ReglasEntradaPesaje.Observaciones.Formato);
    }

    // =========================================================================
    // 4. INVARIANTE: Conformance estricto de interfaz IPesajeRepository -> PesajeRepository
    // =========================================================================

    [Fact(DisplayName = "PesajeRepository implementa estrictamente IPesajeRepository incluyendo RegistrarCamionesLoteAsync")]
    public void PesajeRepository_ImplementaEstrictamenteIPesajeRepository()
    {
        var tipoInterfaz = typeof(IPesajeRepository);
        var tipoRepo = typeof(PesajeRepository);

        // Asignabilidad estricta
        Assert.True(tipoInterfaz.IsAssignableFrom(tipoRepo),
            "PesajeRepository debe implementar IPesajeRepository.");

        // Mapeo de interfaz exhaustivo
        var map = tipoRepo.GetInterfaceMap(tipoInterfaz);
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            var metodoInterfaz = map.InterfaceMethods[i];
            var metodoTarget = map.TargetMethods[i];

            Assert.NotNull(metodoTarget);
            Assert.True(metodoTarget.IsPublic,
                $"El metodo {metodoTarget.Name} en PesajeRepository que implementa {metodoInterfaz.Name} debe ser publico.");
            Assert.Equal(metodoInterfaz.ReturnType, metodoTarget.ReturnType);
        }

        // Inspeccion del metodo RegistrarCamionesLoteAsync
        var metodoLote = tipoRepo.GetMethod(nameof(IPesajeRepository.RegistrarCamionesLoteAsync));
        Assert.NotNull(metodoLote);
        Assert.Equal(typeof(Task<Result<ResultadoAltaLoteCamiones>>), metodoLote.ReturnType);

        var parametros = metodoLote.GetParameters();
        Assert.Equal(3, parametros.Length);

        // Parametro 1: IReadOnlyList<(string Placa, int IdProveedor, string? Observaciones)>
        Assert.Equal(typeof(IReadOnlyList<(string Placa, int IdProveedor, string? Observaciones)>), parametros[0].ParameterType);
        Assert.Equal("camiones", parametros[0].Name);

        // Parametro 2: Guid idSolicitud
        Assert.Equal(typeof(Guid), parametros[1].ParameterType);
        Assert.Equal("idSolicitud", parametros[1].Name);

        // Parametro 3: CancellationToken ct = default
        Assert.Equal(typeof(CancellationToken), parametros[2].ParameterType);
        Assert.Equal("ct", parametros[2].Name);
        Assert.True(parametros[2].IsOptional);
    }

    // =========================================================================
    // 5. INVARIANTE: Conformance estricto de interfaz IRolPermisoRepository -> RolPermisoRepository
    // =========================================================================

    [Fact(DisplayName = "RolPermisoRepository implementa estrictamente IRolPermisoRepository incluyendo PurgarCache")]
    public void RolPermisoRepository_ImplementaEstrictamenteIRolPermisoRepository()
    {
        var tipoInterfaz = typeof(IRolPermisoRepository);
        var tipoRepo = typeof(RolPermisoRepository);

        // Asignabilidad estricta
        Assert.True(tipoInterfaz.IsAssignableFrom(tipoRepo),
            "RolPermisoRepository debe implementar IRolPermisoRepository.");

        // Mapeo de interfaz exhaustivo
        var map = tipoRepo.GetInterfaceMap(tipoInterfaz);
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            var metodoInterfaz = map.InterfaceMethods[i];
            var metodoTarget = map.TargetMethods[i];

            Assert.NotNull(metodoTarget);
            Assert.True(metodoTarget.IsPublic,
                $"El metodo {metodoTarget.Name} en RolPermisoRepository que implementa {metodoInterfaz.Name} debe ser publico.");
            Assert.Equal(metodoInterfaz.ReturnType, metodoTarget.ReturnType);
        }

        // Inspeccion del metodo PurgarCache
        var metodoPurgar = tipoRepo.GetMethod(nameof(IRolPermisoRepository.PurgarCache));
        Assert.NotNull(metodoPurgar);
        Assert.Equal(typeof(void), metodoPurgar.ReturnType);
        Assert.Empty(metodoPurgar.GetParameters());
        Assert.True(metodoPurgar.IsPublic);
    }
}
