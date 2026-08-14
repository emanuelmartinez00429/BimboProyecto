using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using CapaDatos.Modelados.Pesajes;

namespace CapaDatos.Modelados.Productos
{
    [Table("productos")]
    public class Productos : BaseModel
    {
        [PrimaryKey("id_producto")]
        public int idProducto { get; set; }
        [Column("codigo_producto")]
        public string codigoProducto { get; set; }
        [Column("nombre_producto")]
        public string nombreProducto { get; set; }
        // Las cuatro FK de abajo son NULLABLE en la base. Declararlas como int a
        // secas hacía que Newtonsoft lanzara al deserializar cualquier fila con
        // NULL ("Cannot convert null value to System.Int32"), la consulta entera
        // fallaba y la grilla se quedaba con la página anterior.
        [Column("id_presentacion")]
        public int? idPresentacion { get; set; }

        [Column("id_fabricante")]
        public int? idFabricante { get; set; }

        [Column("id_unidad")]
        public int? idUnidad { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("peso_teorico")]
        public decimal? pesoTeorico { get; set; }

        [Column("id_tara")]
        public int? idTara { get; set; }

        [Column("id_categoria")]
        public int? idCategoria { get; set; }

        [Column("contenido")]
        public string contenidoProducto { get; set; }

        [Column("id_pais")]
        public int? idPais { get; set; }

        [Column("precio_por_kg")]
        public decimal? precioPorKg { get; set; }

        [Column("created_at")]
        public DateTime? createdAt { get; set; }

        [Column("updated_at")]
        public DateTime? updatedAt { get; set; }

        /// <summary>
        /// Esto va a permitir que no tenga que estar duplicando modelados
        /// usando las navegaciones para las relaciones
        /// </summary>
        public Presentacion presentacion_producto { get; set; }
        public Fabricante Fabricante { get; set; }
        public Categoria Categoria { get; set; }
        public Paises Paises { get; set; }
        public Tara? tara { get; set; }
        public UnidadMedida? unidad_medida { get; set; }

        public string nombre_Presentacion => presentacion_producto?.nombrePresentacion ?? "Sin presentación";
        public string nombre_Fabricante => Fabricante?.nombreFabricante ?? "Sin fabricante";
        public string nombre_Proveedor => Fabricante?.Proveedores?.nombreProveedor ?? "Sin proveedor";
        public int?   id_Proveedor     => Fabricante?.idProveedor;
        public string nombre_Categoria => Categoria?.nombreCategoria ?? "Sin categoría";
        public string nombre_Pais => Paises?.nombrePais ?? "Sin país";
        // descripcion_tara viene de la BD con saltos de linea al final; sin Trim el
        // TextBox lo toma como segunda linea y el texto se ve corrido hacia arriba.
        public string descripcion_Tara => string.IsNullOrWhiteSpace(tara?.descripcionTara) ? "Sin tara" : tara!.descripcionTara!.Trim();
        public string abreviatura_Unidad => unidad_medida?.abreviatura ?? string.Empty;

    }
}
