using CapaUI.Core.Validacion;
using Xunit;

namespace BimboProyecto.Tests.Validacion;

public class ChangeTrackerTests
{
    private sealed record TestSnapshot(string Nombre, string Codigo, int? CategoriaId, decimal? Precio);

    [Fact]
    public void IsDirty_WhenInitialSnapshotIsNull_AlwaysReturnsTrue()
    {
        var tracker = new ChangeTracker<TestSnapshot>(null);

        Assert.False(tracker.HasInitialSnapshot);
        Assert.Null(tracker.InitialSnapshot);
        Assert.True(tracker.IsDirty(null));
        Assert.True(tracker.IsDirty(new TestSnapshot("A", "01", 1, 10.5m)));
    }

    [Fact]
    public void IsDirty_WhenValuesAreIdentical_ReturnsFalse()
    {
        var initial = new TestSnapshot("Pan Blanco", "PB01", 5, 25.50m);
        var tracker = ChangeTracker.Create(initial);

        Assert.True(tracker.HasInitialSnapshot);
        Assert.Equal(initial, tracker.InitialSnapshot);

        var current = new TestSnapshot("Pan Blanco", "PB01", 5, 25.50m);
        Assert.False(tracker.IsDirty(current));
    }

    [Fact]
    public void IsDirty_WhenAnyStringFieldChanges_ReturnsTrue()
    {
        var initial = new TestSnapshot("Pan Blanco", "PB01", 5, 25.50m);
        var tracker = new ChangeTracker<TestSnapshot>(initial);

        var changedNombre = initial with { Nombre = "Pan Integral" };
        var changedCodigo = initial with { Codigo = "PI01" };

        Assert.True(tracker.IsDirty(changedNombre));
        Assert.True(tracker.IsDirty(changedCodigo));
    }

    [Fact]
    public void IsDirty_WhenAnyNullableOrNumericFieldChanges_ReturnsTrue()
    {
        var initial = new TestSnapshot("Pan Blanco", "PB01", 5, 25.50m);
        var tracker = new ChangeTracker<TestSnapshot>(initial);

        var changedCategoria = initial with { CategoriaId = 6 };
        var changedCategoriaNull = initial with { CategoriaId = null };
        var changedPrecio = initial with { Precio = 30.00m };

        Assert.True(tracker.IsDirty(changedCategoria));
        Assert.True(tracker.IsDirty(changedCategoriaNull));
        Assert.True(tracker.IsDirty(changedPrecio));
    }

    [Fact]
    public void IsDirty_SimulatingComplex11FieldProductoSnapshot()
    {
        var initial = new ProductoTestSnapshot(
            "COD-01", "Pan Blanco 500g", "500 g", 1, 2, 3, 4, 0.50m, 5, 6, 12.5m);

        var tracker = ChangeTracker.Create(initial);

        // Sin cambios
        var mismo = new ProductoTestSnapshot(
            "COD-01", "Pan Blanco 500g", "500 g", 1, 2, 3, 4, 0.50m, 5, 6, 12.5m);
        Assert.False(tracker.IsDirty(mismo));

        // Cambio en cada uno de los 11 campos individualmente
        Assert.True(tracker.IsDirty(initial with { CodigoInterno = "COD-02" }));
        Assert.True(tracker.IsDirty(initial with { Nombre = "Pan Negro" }));
        Assert.True(tracker.IsDirty(initial with { Contenido = "600 g" }));
        Assert.True(tracker.IsDirty(initial with { IdPresentacion = 99 }));
        Assert.True(tracker.IsDirty(initial with { IdFabricante = null }));
        Assert.True(tracker.IsDirty(initial with { IdCategoria = 10 }));
        Assert.True(tracker.IsDirty(initial with { IdPais = 20 }));
        Assert.True(tracker.IsDirty(initial with { PesoTeorico = 0.60m }));
        Assert.True(tracker.IsDirty(initial with { IdTara = null }));
        Assert.True(tracker.IsDirty(initial with { IdUnidad = 7 }));
        Assert.True(tracker.IsDirty(initial with { PrecioPorKg = 15.0m }));
    }

    private sealed record ProductoTestSnapshot(
        string CodigoInterno,
        string Nombre,
        string Contenido,
        int? IdPresentacion,
        int? IdFabricante,
        int? IdCategoria,
        int? IdPais,
        decimal? PesoTeorico,
        int? IdTara,
        int? IdUnidad,
        decimal? PrecioPorKg);
}
