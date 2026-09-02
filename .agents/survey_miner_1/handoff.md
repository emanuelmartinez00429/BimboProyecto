# Handoff Report — survey_miner_1

## 1. Observation
- Investigamos de forma directa y exhaustiva el código de la solución Bimbo Honduras (.NET 8):
  - `CapaDominio/Reglas/ReglasEntidades.cs` (líneas 1 a 95): Contiene actualmente 9 clases de reglas (`ReglasProducto`, `ReglasCategoria`, `ReglasPresentacion`, `ReglasFabricante`, `ReglasProveedor`, `ReglasEmpleado`, `ReglasUsuario`, `ReglasRol`, `ReglasContacto`, `ReglasEmpresa`). Se detectaron desalineaciones críticas: `ReglasCategoria.Descripcion` declara `LargoMaximo: 255` cuando la columna física en PostgreSQL `descripcion_categoria` es `varchar(200)`; `ReglasProducto.Nombre` tiene 150 vs 200 en BD; `ReglasFabricante.Nombre` tiene 100 vs 200 en BD; `ReglasProveedor.Nombre` tiene 100 vs 200 en BD; `ReglasEmpresa.Nombre` tiene 150 vs 200 en BD; faltan campos en dominio como `ReglasProducto.Contenido` (100), `ReglasEmpleado.Identidad` (20, obligatorio), `ReglasUsuario.Correo` (50, `FormatoCampo.Correo`), `ReglasUsuario.Password` (min 6, max 72), `ReglasEmpresa.Direccion` (500 - tope UI); y los campos `Rtn`, `Telefono` y `Correo` en múltiples entidades no tenían `LargoMaximo` explícito.
  - `CapaDominio/Reglas/ReglasFormato.cs` (líneas 1 a 80): Contiene la lógica estática pura de formatos (`NoExcedeLargo`, `TieneLargoMinimo`, `EsCorreo`, `EsRtn`, `EsTelefono`, `TieneContenido`).
  - `CapaDominio/Reglas/ReglaCampo.cs` (líneas 1 a 46): Declara el record `ReglaCampo(bool Obligatorio = false, int? LargoMaximo = null, int? LargoMinimo = null, FormatoCampo Formato = FormatoCampo.Ninguno)`.
  - Modelos de datos en `CapaDatos/Modelados/` (`Productos.cs`, `Categoria.cs`, `PresentacionCrud.cs`, `FabricanteCrud.cs`, `Proveedores.cs`, `Empleados.cs`, `Usuarios.cs`, `Roles.cs`, `ContactoFabricanteModel.cs`, `ContactoProveedorModel.cs`, `Empresa.cs`).
  - Migraciones SQL en `supabase/migrations/` con RPCs y DDL de tablas.
  - `BimboProyecto.Tests/BimboProyecto.Tests.csproj` (líneas 1 a 27): Ya cuenta con el paquete `Npgsql` (v8.0.3) y xUnit (v2.9.3).
  - `BimboProyecto.Tests/Rbac/ContratoRbacTests.cs` (líneas 48 a 70): Muestra el patrón estándar de integración contra Postgres leyendo `BIMBO_POSTGRES_CONNECTION_STRING` y retornando si no está presente.
  - Ejecución de `dotnet test`: 118 pruebas superadas con 0 errores.

## 2. Logic Chain
1. Al comparar `ReglasEntidades.cs` contra el esquema físico de PostgreSQL (`information_schema.columns`) y los requerimientos R1:
   - `ReglasCategoria.Descripcion = 255` permite que un usuario ingrese 255 caracteres en la UI, pero el backend PostgreSQL fallará con error 22001 (value too long for type character varying(200)). Debe corregirse a 200.
   - Columnas `text` en PostgreSQL (`presentacion_producto.descripcion_presentacion`, `fabricante.descripcion_fabricante`, `proveedores.direccion_proveedor`, `empresa.direccion_empresa`) no tienen tope en BD, por lo que el dominio debe estandarizar un tope preventivo de UI de 500 caracteres documentado con marcador léxico.
   - Campos de texto con formato (`Rtn`, `Telefono`, `Correo`) en BD tienen longitud física de 20, 20 y 100 caracteres respectivamente; deben incluir tanto la regla de formato como el `LargoMaximo` correspondiente en `ReglaCampo`.
2. Para los requerimientos R5:
   - `ReglasEntidadesTests.cs` debe implementar tres tipos de tests:
     - **Test A (Deriva):** Conexión vía `Npgsql` leyendo `BIMBO_POSTGRES_CONNECTION_STRING` (omitiendo limpiamente si está vacía) para contrastar contra `information_schema.columns`.
     - **Test B (Valores fijados):** Tests `[Theory]` offline/CI que validan las 34 propiedades contra la especificación.
     - **Test C (Auditoría por reflexión):** Asegura por reflexión que todo `ReglaCampo` público declarado en `CapaDominio.Reglas` esté registrado en la matriz de auditoría.
   - `ReglasFormatoTests.cs` debe incorporar pruebas de frontera exhaustivas (`null`, empty, whitespace, exact bound, bound+1, bound-1).

## 3. Caveats
- No se realizaron modificaciones de código fuente en `CapaDominio` ni en `BimboProyecto.Tests` en este paso, respetando el rol de Specification Miner (read-only).
- En caso de que la variable de entorno `BIMBO_POSTGRES_CONNECTION_STRING` no esté configurada en la máquina local o pipeline, el Test A de deriva de esquema se omitirá de forma limpia y transparente, mientras que los Tests B y C correrán al 100% de manera determinista.

## 4. Conclusion
- Se completó el análisis exhaustivo de la capa de dominio y del esquema de base de datos.
- Se documentó la matriz completa de características, 43 reglas de dominio/formato/login, 32 casos de frontera y el catálogo de auditoría de 34 campos entidad-columna en `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md`.
- El equipo de desarrollo puede proceder con la implementación de R1 en `ReglasEntidades.cs`, R2/R3/R4 en UI, y R5 en `BimboProyecto.Tests/Dominio/`.

## 5. Verification Method
- Inspeccionar el reporte generado en `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md`.
- Verificar compilación y tests actuales con:
  ```powershell
  dotnet build BimboProyecto.sln
  dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
  ```
