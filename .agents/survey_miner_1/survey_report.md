# Reporte de Prospección y Especificación Técnica: Capa de Dominio y Esquema de Base de Datos

**Fecha:** 2026-09-02  
**Autor:** `survey_miner_1` (teamwork_preview_spec_miner)  
**Proyecto:** Bimbo Honduras — Sistema de Control y Pesaje WPF (.NET 8)  
**Objetivo:** Prospección autoritativa y exhaustiva de las reglas de dominio, modelos de datos, esquema de PostgreSQL (Supabase), topes de UI para columnas `text`, requerimientos de validación R1 y especificación de pruebas R5.

---

## 1. Features Discovered (Matriz de Características y Reglas)

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|---|---|---|---|---|---|---|
| 1 | Dominio / Reglas | `ReglasProducto.Codigo` | Código de producto único y obligatorio | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 50) | Rechazado si vacío o > 50 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.productos.codigo_producto` |
| 2 | Dominio / Reglas | `ReglasProducto.Nombre` | Nombre descriptivo del producto | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 200) | Rechazado si vacío o > 200 caracteres (Actualmente desalineado en 150) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.productos.nombre_producto` |
| 3 | Dominio / Reglas | `ReglasProducto.Contenido` | Contenido neto o descripción del empaque | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 100) | Rechazado si > 100 caracteres (Actualmente ausente en Dominio) | `CapaDominio/Entities/Producto.cs`, `public.productos.contenido` |
| 4 | Dominio / Reglas | `ReglasProducto.PesoTeorico` | Peso teórico para cálculo de tolerancia | Texto decimal opcional | `ReglaCampo` (Formato: FormatoCampo.Decimal) | Rechazado si no es formato decimal válido | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.productos.peso_teorico` |
| 5 | Dominio / Reglas | `ReglasProducto.PrecioPorKg` | Precio base por kilogramo | Texto decimal opcional | `ReglaCampo` (Formato: FormatoCampo.Decimal) | Rechazado si no es formato decimal válido | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.productos.precio_por_kg` |
| 6 | Dominio / Reglas | `ReglasCategoria.Nombre` | Nombre único de la categoría | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 100) | Rechazado si vacío o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.categoria.nombre_categoria` |
| 7 | Dominio / Reglas | `ReglasCategoria.Descripcion` | Descripción informativa de la categoría | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 200) | Rechazado si > 200 caracteres (Actualmente en 255, viola BD) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.categoria.descripcion_categoria` |
| 8 | Dominio / Reglas | `ReglasPresentacion.Nombre` | Nombre de la presentación del producto | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 100) | Rechazado si vacío o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.presentacion_producto.nombre_presentacion` |
| 9 | Dominio / Reglas | `ReglasPresentacion.Descripcion` | Descripción de la presentación (Columna `text`) | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 500) | Rechazado si > 500 caracteres (Tope UI, actualmente en 255) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.presentacion_producto.descripcion_presentacion` |
| 10 | Dominio / Reglas | `ReglasFabricante.Nombre` | Razón social o nombre comercial del fabricante | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 200) | Rechazado si vacío o > 200 caracteres (Actualmente en 100) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.fabricante.nombre_fabricante` |
| 11 | Dominio / Reglas | `ReglasFabricante.Descripcion` | Descripción o notas del fabricante (Columna `text`) | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 500) | Rechazado si > 500 caracteres (Tope UI, actualmente en 255) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.fabricante.descripcion_fabricante` |
| 12 | Dominio / Reglas | `ReglasProveedor.Nombre` | Nombre o razón social del proveedor | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 200) | Rechazado si vacío o > 200 caracteres (Actualmente en 100) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.proveedores.nombre_proveedor` |
| 13 | Dominio / Reglas | `ReglasProveedor.Rtn` | Registro Tributario Nacional (14 dígitos) | Texto opcional (14 dígitos) | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Rtn) | Rechazado si formato no cumple 14 dígitos o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.proveedores.rtn_proveedor` |
| 14 | Dominio / Reglas | `ReglasProveedor.Telefono` | Teléfono de contacto del proveedor | Texto opcional (8 a 15 dígitos / E.164) | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono) | Rechazado si formato inválido o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.proveedores.telefono_proveedor` |
| 15 | Dominio / Reglas | `ReglasProveedor.Correo` | Correo electrónico institucional | Texto opcional formato email | `ReglaCampo` (Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo) | Rechazado si formato inválido o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.proveedores.correo_proveedor` |
| 16 | Dominio / Reglas | `ReglasProveedor.Direccion` | Dirección física del proveedor (Columna `text`) | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 500) | Rechazado si > 500 caracteres (Tope UI, actualmente en 255) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.proveedores.direccion_proveedor` |
| 17 | Dominio / Reglas | `ReglasEmpleado.Nombre` | Nombres del empleado | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 100) | Rechazado si vacío o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empleados.nombre_empleado` |
| 18 | Dominio / Reglas | `ReglasEmpleado.Apellido` | Apellidos del empleado | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 100) | Rechazado si vacío o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empleados.apellido_empleado` |
| 19 | Dominio / Reglas | `ReglasEmpleado.Identidad` | Cédula / DNI hondureño (13 dígitos) | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 20) | Rechazado si vacío o > 20 caracteres (Actualmente ausente en Dominio) | `CapaDominio/Entities/Empleado.cs`, `public.empleados.numero_identidad` |
| 20 | Dominio / Reglas | `ReglasEmpleado.Telefono` | Teléfono personal del empleado | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono) | Rechazado si formato inválido o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empleados.telefono_empleado` |
| 21 | Dominio / Reglas | `ReglasEmpleado.Correo` | Correo electrónico del empleado | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo) | Rechazado si formato inválido o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empleados.correo_empleado` |
| 22 | Dominio / Reglas | `ReglasUsuario.Correo` | Alias / Email de usuario en el sistema | Texto formato correo | `ReglaCampo` (Obligatorio: true, LargoMaximo: 50, Formato: FormatoCampo.Correo) | Rechazado si vacío, formato inválido o > 50 caracteres (Actualmente ausente en Dominio) | `CapaDatos/Modelados/Usuarios/Usuarios.cs`, `public.usuarios.alias_usuario` |
| 23 | Dominio / Reglas | `ReglasUsuario.Password` | Contraseña para Supabase Auth | Texto con longitud min 6, max 72 | `ReglaCampo` (Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72) | Rechazado si < 6 caracteres o > 72 caracteres (Límite Bcrypt) | `CapaDominio/Reglas/ReglasEntidades.cs`, Supabase Auth specs |
| 24 | Dominio / Reglas | `ReglasUsuario.Empleado` | Empleado asociado al usuario | Selección de combobox | `ReglaCampo` (Obligatorio: true) | Rechazado si no se selecciona empleado al crear | `CapaDominio/Reglas/ReglasEntidades.cs` |
| 25 | Dominio / Reglas | `ReglasUsuario.Rol` | Rol RBAC asignado | Selección de combobox | `ReglaCampo` (Obligatorio: true) | Rechazado si no se selecciona rol | `CapaDominio/Reglas/ReglasEntidades.cs` |
| 26 | Dominio / Reglas | `ReglasRol.Nombre` | Nombre único del rol en RBAC | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 50) | Rechazado si vacío o > 50 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.roles.nombre_rol` |
| 27 | Dominio / Reglas | `ReglasContacto.Nombre` | Nombre del contacto (Fabricante o Proveedor) | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 100) | Rechazado si vacío o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `contactos_fabricante`/`contactos_proveedor` |
| 28 | Dominio / Reglas | `ReglasContacto.Telefono` | Teléfono del contacto | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono) | Rechazado si formato inválido o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `contactos_fabricante`/`contactos_proveedor` |
| 29 | Dominio / Reglas | `ReglasContacto.Correo` | Correo del contacto | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo) | Rechazado si formato inválido o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `contactos_fabricante`/`contactos_proveedor` |
| 30 | Dominio / Reglas | `ReglasEmpresa.Nombre` | Nombre comercial / Razón social de la empresa | Texto no nulo ni vacío | `ReglaCampo` (Obligatorio: true, LargoMaximo: 200) | Rechazado si vacío o > 200 caracteres (Actualmente en 150) | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empresa.nombre_empresa` |
| 31 | Dominio / Reglas | `ReglasEmpresa.Rtn` | RTN corporativo | Texto opcional (14 dígitos) | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Rtn) | Rechazado si formato inválido o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empresa.rtn_empresa` |
| 32 | Dominio / Reglas | `ReglasEmpresa.Telefono` | Teléfono de la empresa | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono) | Rechazado si formato inválido o > 20 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empresa.telefono_empresa` |
| 33 | Dominio / Reglas | `ReglasEmpresa.Correo` | Correo institucional general | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo) | Rechazado si formato inválido o > 100 caracteres | `CapaDominio/Reglas/ReglasEntidades.cs`, `public.empresa.correo_empresa` |
| 34 | Dominio / Reglas | `ReglasEmpresa.Direccion` | Dirección fiscal de la empresa (Columna `text`) | Texto opcional | `ReglaCampo` (Obligatorio: false, LargoMaximo: 500) | Rechazado si > 500 caracteres (Tope UI, actualmente ausente) | `CapaDatos/Modelados/Empresa.cs`, `public.empresa.direccion_empresa` |
| 35 | Dominio / Formato | `ReglasFormato.NoExcedeLargo` | Predicado de longitud máxima con trim | `(string? texto, int largoMaximo)` | `bool` | Retorna `false` si `(texto?.Trim().Length ?? 0) > largoMaximo` | `CapaDominio/Reglas/ReglasFormato.cs:63` |
| 36 | Dominio / Formato | `ReglasFormato.TieneLargoMinimo` | Predicado de longitud mínima | `(string? texto, int largoMinimo)` | `bool` | Retorna `false` si `(texto?.Length ?? 0) < largoMinimo` | `CapaDominio/Reglas/ReglasFormato.cs:66` |
| 37 | Dominio / Formato | `ReglasFormato.EsCorreo` | Validación permisiva de email | `string? texto` | `bool` | `null`/vacío es `true`. Falla si no cumple `^[^@\s]+@[^@\s]+\.[^@\s]+$` | `CapaDominio/Reglas/ReglasFormato.cs:48` |
| 38 | Dominio / Formato | `ReglasFormato.EsRtn` | Validación de RTN hondureño | `string? texto` | `bool` | `null`/vacío es `true`. Falla si `SoloDigitos(texto)` != 14 dígitos | `CapaDominio/Reglas/ReglasFormato.cs:51` |
| 39 | Dominio / Formato | `ReglasFormato.EsTelefono` | Validación de teléfono (local o E.164) | `string? texto` | `bool` | `null`/vacío es `true`. Falla si `SoloDigitos` no está entre 8 y 15 dígitos | `CapaDominio/Reglas/ReglasFormato.cs:54` |
| 40 | Dominio / Login | `ReglasLogin.CredencialesCompletas` | Habilita botón de ingreso | `(string email, string password)` | `bool` | `email` no vacío ni whitespace y `password.Length > 0` | `CapaDominio/Reglas/ReglasLogin.cs:26` |
| 41 | Dominio / Login | `ReglasLogin.OtpValido` | Valida código OTP de 8 dígitos | `string otp` | `bool` | Requiere exactamente 8 caracteres numéricos | `CapaDominio/Reglas/ReglasLogin.cs:38` |
| 42 | Dominio / Password | `ReglasContrasena.TieneLargoMinimo` | Longitud mínima para cambio/recuperación | `string pwd` | `bool` | Requiere `pwd.Length >= 8` | `CapaDominio/Reglas/ReglasContrasena.cs:27` |
| 43 | Dominio / Password | `ReglasContrasena.CumpleTodasLasReglas` | Valida largo, mayúscula, número y símbolo | `string pwd` | `bool` | Requiere todas las 4 condiciones simultáneas | `CapaDominio/Reglas/ReglasContrasena.cs:44` |

---

## 2. Edge Cases (Comportamientos de Frontera Observados y Auditados)

| # | Feature | Input | Observed / Expected Behavior |
|---|---|---|---|
| 1 | `ReglasFormato.NoExcedeLargo` | `null`, `50` | Retorna `true` (longitud tratada como 0). |
| 2 | `ReglasFormato.NoExcedeLargo` | `""`, `50` | Retorna `true` (longitud 0). |
| 3 | `ReglasFormato.NoExcedeLargo` | `"   "`, `50` | Retorna `true` (longitud post-trim es 0). |
| 4 | `ReglasFormato.NoExcedeLargo` | `new string('x', 50)`, `50` | Retorna `true` (exactamente en el límite). |
| 5 | `ReglasFormato.NoExcedeLargo` | `new string('x', 51)`, `50` | Retorna `false` (supera por 1 carácter). |
| 6 | `ReglasFormato.NoExcedeLargo` | `"  " + new string('x', 50) + "  "`, `50` | Retorna `true` (los espacios perimetrales se limpian con `.Trim()`). |
| 7 | `ReglasFormato.NoExcedeLargo` | `"  " + new string('x', 51) + "  "`, `50` | Retorna `false` (el texto útil supera el límite). |
| 8 | `ReglasFormato.TieneLargoMinimo` | `null`, `6` | Retorna `false` (0 < 6). |
| 9 | `ReglasFormato.TieneLargoMinimo` | `null`, `0` | Retorna `true` (0 >= 0). |
| 10 | `ReglasFormato.TieneLargoMinimo` | `""`, `6` | Retorna `false` (0 < 6). |
| 11 | `ReglasFormato.TieneLargoMinimo` | `new string('a', 6)`, `6` | Retorna `true` (exactamente igual al mínimo). |
| 12 | `ReglasFormato.TieneLargoMinimo` | `new string('a', 5)`, `6` | Retorna `false` (menor al mínimo). |
| 13 | `ReglasFormato.EsCorreo` | `null`, `""`, `"   "` | Retorna `true` (los campos vacíos se consideran válidos para no forzar obligatoriedad accidental). |
| 14 | `ReglasFormato.EsCorreo` | `"usuario@empresa.com"` | Retorna `true`. |
| 15 | `ReglasFormato.EsCorreo` | `"nombre.apellido+tag@sub.empresa.co.hn"` | Retorna `true`. |
| 16 | `ReglasFormato.EsCorreo` | `"invalido.com"` | Retorna `false` (falta `@`). |
| 17 | `ReglasFormato.EsCorreo` | `"usuario@dominio"` | Retorna `false` (falta extensión/TLD con punto). |
| 18 | `ReglasFormato.EsCorreo` | `"usuario@@dominio.com"` | Retorna `false` (doble `@`). |
| 19 | `ReglasFormato.EsCorreo` | `"usuario @dominio.com"` | Retorna `false` (espacio interno). |
| 20 | `ReglasFormato.EsRtn` | `"08011990123456"` (14 dígitos) | Retorna `true`. |
| 21 | `ReglasFormato.EsRtn` | `"0801-1990-123456"` (con guiones) | Retorna `true` (`SoloDigitos` extrae 14 dígitos). |
| 22 | `ReglasFormato.EsRtn` | `"0801 1990 123456"` (con espacios) | Retorna `true` (`SoloDigitos` extrae 14 dígitos). |
| 23 | `ReglasFormato.EsRtn` | `"0801199012345"` (13 dígitos) | Retorna `false` (corto). |
| 24 | `ReglasFormato.EsRtn` | `"080119901234567"` (15 dígitos) | Retorna `false` (largo). |
| 25 | `ReglasFormato.EsTelefono` | `"22334455"` (8 dígitos) | Retorna `true` (número local hondureño). |
| 26 | `ReglasFormato.EsTelefono` | `"+50422334455"` (12 dígitos + signo) | Retorna `true` (E.164 con código de país). |
| 27 | `ReglasFormato.EsTelefono` | `"+123456789012345"` (15 dígitos) | Retorna `true` (máximo permitido por E.164). |
| 28 | `ReglasFormato.EsTelefono` | `"1234567"` (7 dígitos) | Retorna `false` (inferior al mínimo de 8). |
| 29 | `ReglasFormato.EsTelefono` | `"+1234567890123456"` (16 dígitos) | Retorna `false` (superior al máximo de 15). |
| 30 | `ReglasUsuario.Password` | Contraseña de 72 caracteres | Válida para Supabase Auth / Bcrypt. |
| 31 | `ReglasUsuario.Password` | Contraseña de 73 caracteres | Truncada o rechazada silenciosamente por Bcrypt si supera 72 bytes; el tope defensivo de UI (72) previene desincronización de credenciales. |
| 32 | `GenerarEmail` | `"NombreMuyLargoQueExcedaElTopeDelSistemaYMuchosApellidos"` | Si supera 50 chars totales, debe truncar la parte local preservando `@empresa.com`. |

---

## 3. Análisis Detallado de Requerimientos R1 (Dominio vs Base de Datos)

### 3.1. Estado Actual vs Estado Requerido en `CapaDominio/Reglas/ReglasEntidades.cs`

A continuación se muestra la auditoría comparativa campo por campo:

| Entidad | Campo | Regla Actual en Código | Especificación Requerida (R1) | Columna Física PostgreSQL | Tipo BD (`information_schema`) | Estado / Acción Requerida |
|---|---|---|---|---|---|---|
| **ReglasProducto** | `Codigo` | `Obligatorio: true, LargoMaximo: 50` | `Obligatorio: true, LargoMaximo: 50` | `codigo_producto` | `character varying(50)` | Correcto |
| | `Nombre` | `Obligatorio: true, LargoMaximo: 150` | `Obligatorio: true, LargoMaximo: 200` | `nombre_producto` | `character varying(200)` | **Desalineado**: actualizar a 200 |
| | `Contenido` | *(No existe)* | `Obligatorio: false, LargoMaximo: 100` | `contenido` | `character varying(100)` | **Faltante**: agregar `Contenido` |
| | `PesoTeorico` | `Formato: Decimal` | `Formato: Decimal` | `peso_teorico` | `numeric` | Correcto |
| | `PrecioPorKg` | `Formato: Decimal` | `Formato: Decimal` | `precio_por_kg` | `numeric` | Correcto |
| **ReglasCategoria** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 100` | `nombre_categoria` | `character varying(100)` | Correcto |
| | `Descripcion` | `LargoMaximo: 255` | `LargoMaximo: 200` | `descripcion_categoria` | `character varying(200)` | **Peligroso**: regla actual (255) es más permisiva que BD (200). Actualizar a 200 |
| **ReglasPresentacion** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 100` | `nombre_presentacion` | `character varying(100)` | Correcto |
| | `Descripcion` | `LargoMaximo: 255` | `LargoMaximo: 500` (Tope UI) | `descripcion_presentacion` | `text` (sin límite) | **Actualizar**: pasar de 255 a 500 con marcador léxico `text` |
| **ReglasFabricante** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 200` | `nombre_fabricante` | `character varying(200)` | **Desalineado**: actualizar a 200 |
| | `Descripcion` | `LargoMaximo: 255` | `LargoMaximo: 500` (Tope UI) | `descripcion_fabricante` | `text` (sin límite) | **Actualizar**: pasar de 255 a 500 con marcador léxico `text` |
| **ReglasProveedor** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 200` | `nombre_proveedor` | `character varying(200)` | **Desalineado**: actualizar a 200 |
| | `Rtn` | `Formato: Rtn` | `LargoMaximo: 20, Formato: Rtn` | `rtn_proveedor` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Telefono` | `Formato: Telefono` | `LargoMaximo: 20, Formato: Telefono` | `telefono_proveedor` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Correo` | `Formato: Correo` | `LargoMaximo: 100, Formato: Correo` | `correo_proveedor` | `character varying(100)` | **Incompleto**: agregar `LargoMaximo: 100` |
| | `Direccion` | `LargoMaximo: 255` | `LargoMaximo: 500` (Tope UI) | `direccion_proveedor` | `text` (sin límite) | **Actualizar**: pasar de 255 a 500 con marcador léxico `text` |
| **ReglasEmpleado** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 100` | `nombre_empleado` | `character varying(100)` | Correcto |
| | `Apellido` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 100` | `apellido_empleado` | `character varying(100)` | Correcto |
| | `Identidad` | *(No existe)* | `Obligatorio: true, LargoMaximo: 20` | `numero_identidad` | `character varying(20)` | **Faltante**: agregar `Identidad` |
| | `Telefono` | `Formato: Telefono` | `LargoMaximo: 20, Formato: Telefono` | `telefono_empleado` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Correo` | `Formato: Correo` | `LargoMaximo: 100, Formato: Correo` | `correo_empleado` | `character varying(100)` | **Incompleto**: agregar `LargoMaximo: 100` |
| **ReglasUsuario** | `Empleado` | `Obligatorio: true` | `Obligatorio: true` | `id_empleado` | `integer` | Correcto |
| | `Rol` | `Obligatorio: true` | `Obligatorio: true` | `id_rol` | `integer` | Correcto |
| | `Correo` | *(No existe)* | `Obligatorio: true, LargoMaximo: 50, Formato: Correo` | `alias_usuario` | `character varying(50)` | **Faltante**: agregar `Correo` |
| | `Password` | `LargoMinimo: 6` | `Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72` | Supabase Auth (`auth.users`) | `encrypted_password` (Bcrypt 72 bytes) | **Incompleto**: agregar `LargoMaximo: 72` |
| **ReglasRol** | `Nombre` | `Obligatorio: true, LargoMaximo: 50` | `Obligatorio: true, LargoMaximo: 50` | `nombre_rol` | `character varying(50)` | Correcto |
| **ReglasContacto** | `Nombre` | `Obligatorio: true, LargoMaximo: 100` | `Obligatorio: true, LargoMaximo: 100` | `nombre_contacto` | `character varying(100)` | Correcto |
| | `Telefono` | `Formato: Telefono` | `LargoMaximo: 20, Formato: Telefono` | `telefono_contacto` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Correo` | `Formato: Correo` | `LargoMaximo: 100, Formato: Correo` | `correo_contacto` | `character varying(100)` | **Incompleto**: agregar `LargoMaximo: 100` |
| **ReglasEmpresa** | `Nombre` | `Obligatorio: true, LargoMaximo: 150` | `Obligatorio: true, LargoMaximo: 200` | `nombre_empresa` | `character varying(200)` | **Desalineado**: actualizar a 200 |
| | `Rtn` | `Formato: Rtn` | `LargoMaximo: 20, Formato: Rtn` | `rtn_empresa` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Telefono` | `Formato: Telefono` | `LargoMaximo: 20, Formato: Telefono` | `telefono_empresa` | `character varying(20)` | **Incompleto**: agregar `LargoMaximo: 20` |
| | `Correo` | `Formato: Correo` | `LargoMaximo: 100, Formato: Correo` | `correo_empresa` | `character varying(100)` | **Incompleto**: agregar `LargoMaximo: 100` |
| | `Direccion` | *(No existe)* | `LargoMaximo: 500` (Tope UI) | `direccion_empresa` | `text` (sin límite) | **Faltante**: agregar `Direccion` con marcador léxico `text` |

### 3.2. Criterio de Columnas `text` y Marcador Léxico (Tope de UI = 500)
En PostgreSQL, las columnas de tipo `text` no imponen una restricción de longitud máxima en el motor de BD (`character_maximum_length IS NULL`).
Sin embargo, para evitar pegados accidentales de textos gigantescos (que rompan los layouts de reportes en PDF/Excel o saturen los payloads de Realtime), se establece un **Tope Preventivo de UI de 500 caracteres**.
Para documentar autoritativamente y permitir que los tests de auditoría y deriva distingan entre columnas físicas `varchar(N)` y columnas `text`, cada regla sobre columna `text` debe incluir su comentario léxico explícito:
```csharp
// Tope de UI de 500 caracteres (columna text en BD)
public static readonly ReglaCampo Descripcion = new(LargoMaximo: 500);
```

---

## 4. Análisis Detallado de Requerimientos R5 (Suite de Pruebas de Deriva y Frontera)

### 4.1. Estructura Actual del Proyecto de Tests (`BimboProyecto.Tests/`)
- **Framework:** `xUnit 2.9.3`, `Microsoft.NET.Test.Sdk 17.14.1`, `xunit.runner.visualstudio 3.1.5`.
- **Conector BD:** `Npgsql 8.0.3` (ya instalado y disponible en `BimboProyecto.Tests.csproj`).
- **Referencias:** `CapaDominio`, `CapaAplicacion`, `CapaDatos`.
- **Patrón de Ejecución Integrada vs Offline:**
  - En `ContratoRbacTests.cs`, las pruebas de integración con Supabase / Postgres leen la variable de entorno:
    ```csharp
    var conexion = Environment.GetEnvironmentVariable("BIMBO_POSTGRES_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(conexion)) return; // Omitir limpiamente en CI/Offline
    ```

### 4.2. Especificación Arquitectónica de `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`

La nueva suite de pruebas `ReglasEntidadesTests.cs` debe implementar tres estrategias complementarias:

#### Test A — Deriva de Esquema contra PostgreSQL (`information_schema.columns`)
1. **Conexión:** Se conecta vía `NpgsqlConnection` leyendo `BIMBO_POSTGRES_CONNECTION_STRING`. Si no existe o está vacía, sale limpiamente sin fallar (permite ejecución en entornos de desarrollo desconectados y pipelines de CI estándar).
2. **Consulta:** Ejecuta una consulta SQL estructurada sobre el esquema público:
   ```sql
   SELECT 
       table_name,
       column_name,
       data_type,
       character_maximum_length,
       is_nullable
   FROM information_schema.columns
   WHERE table_schema = 'public';
   ```
3. **Validación:**
   - Para cada entrada en el mapa de auditoría:
     - Verifica que la tabla y columna existan en PostgreSQL.
     - Si la columna es `character varying` (`varchar(N)`): comprueba que `regla.LargoMaximo <= character_maximum_length`. Lanza error si la regla de dominio es más permisiva que la BD (ej. `Regla = 255` contra `BD = 200`).
     - Si la columna es `text` o `varchar` sin límite: verifica que en el mapa de auditoría esté marcada como `EsTopeUI = true` y que `regla.LargoMaximo == 500`.

#### Test B — Valores Fijados para Ejecución Offline / CI (`[Theory]`)
Pruebas unitarias puras y deterministas que no requieren red ni base de datos:
- `[Theory]` que valida las 34 propiedades y reglas de `ReglasEntidades` contra los valores esperados de negocio:
  - `Obligatorio` exacto (true / false).
  - `LargoMaximo` exacto (número entero esperado).
  - `LargoMinimo` exacto (para Password / OTP).
  - `Formato` exacto (`FormatoCampo.Correo`, `Rtn`, `Telefono`, `Decimal`, `Ninguno`).

#### Test C — Auditoría Exhaustiva por Reflexión
Prueba unitaria que utiliza Reflection (`typeof(ReglasProducto).Assembly`):
1. Encuentra todas las clases estáticas en el namespace `CapaDominio.Reglas`.
2. Inspecciona todos los campos `public static readonly ReglaCampo`.
3. Verifica que **cada uno de los campos declarados** esté presente en el mapa central de auditoría.
4. Previene la introducción futura de nuevas reglas o entidades sin su respectivo test de verificación.

### 4.3. Especificación de Pruebas de Frontera en `ReglasFormatoTests.cs`
Extender `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs` con casos de prueba para:
1. `NoExcedeLargo(null, N)` -> `true`
2. `NoExcedeLargo("", N)` -> `true`
3. `NoExcedeLargo("   ", N)` -> `true` (trim)
4. `NoExcedeLargo(exacto_N, N)` -> `true`
5. `NoExcedeLargo(exacto_N_mas_1, N)` -> `false`
6. `TieneLargoMinimo(null, N)` -> `false` (cuando N > 0)
7. `TieneLargoMinimo("", 0)` -> `true`
8. `TieneLargoMinimo(exacto_N, N)` -> `true`
9. `TieneLargoMinimo(exacto_N_menos_1, N)` -> `false`

---

## 5. Matriz de Auditoría y Mapeo Entidad-Columna (Blueprint para Implementadores)

A continuación se detalla la estructura del catálogo que consumirán los tests de auditoría y deriva:

```csharp
public record DefinicionAuditoriaRegla(
    string Tabla,
    string Columna,
    ReglaCampo Regla,
    int? LargoMaximoEsperado,
    bool EsTopeUI = false,
    bool EsColumnaBD = true
);
```

| # | Clase de Dominio | Campo | Tabla | Columna | Largo Esperado | EsTopeUI | Formato | Obligatorio |
|---|---|---|---|---|---|---|---|---|
| 1 | `ReglasProducto` | `Codigo` | `productos` | `codigo_producto` | 50 | false | Ninguno | true |
| 2 | `ReglasProducto` | `Nombre` | `productos` | `nombre_producto` | 200 | false | Ninguno | true |
| 3 | `ReglasProducto` | `Contenido` | `productos` | `contenido` | 100 | false | Ninguno | false |
| 4 | `ReglasProducto` | `PesoTeorico` | `productos` | `peso_teorico` | null | false | Decimal | false |
| 5 | `ReglasProducto` | `PrecioPorKg` | `productos` | `precio_por_kg` | null | false | Decimal | false |
| 6 | `ReglasCategoria` | `Nombre` | `categoria` | `nombre_categoria` | 100 | false | Ninguno | true |
| 7 | `ReglasCategoria` | `Descripcion` | `categoria` | `descripcion_categoria` | 200 | false | Ninguno | false |
| 8 | `ReglasPresentacion` | `Nombre` | `presentacion_producto` | `nombre_presentacion` | 100 | false | Ninguno | true |
| 9 | `ReglasPresentacion` | `Descripcion` | `presentacion_producto` | `descripcion_presentacion` | 500 | true | Ninguno | false |
| 10 | `ReglasFabricante` | `Nombre` | `fabricante` | `nombre_fabricante` | 200 | false | Ninguno | true |
| 11 | `ReglasFabricante` | `Descripcion` | `fabricante` | `descripcion_fabricante` | 500 | true | Ninguno | false |
| 12 | `ReglasProveedor` | `Nombre` | `proveedores` | `nombre_proveedor` | 200 | false | Ninguno | true |
| 13 | `ReglasProveedor` | `Rtn` | `proveedores` | `rtn_proveedor` | 20 | false | Rtn | false |
| 14 | `ReglasProveedor` | `Telefono` | `proveedores` | `telefono_proveedor` | 20 | false | Telefono | false |
| 15 | `ReglasProveedor` | `Correo` | `proveedores` | `correo_proveedor` | 100 | false | Correo | false |
| 16 | `ReglasProveedor` | `Direccion` | `proveedores` | `direccion_proveedor` | 500 | true | Ninguno | false |
| 17 | `ReglasEmpleado` | `Nombre` | `empleados` | `nombre_empleado` | 100 | false | Ninguno | true |
| 18 | `ReglasEmpleado` | `Apellido` | `empleados` | `apellido_empleado` | 100 | false | Ninguno | true |
| 19 | `ReglasEmpleado` | `Identidad` | `empleados` | `numero_identidad` | 20 | false | Ninguno | true |
| 20 | `ReglasEmpleado` | `Telefono` | `empleados` | `telefono_empleado` | 20 | false | Telefono | false |
| 21 | `ReglasEmpleado` | `Correo` | `empleados` | `correo_empleado` | 100 | false | Correo | false |
| 22 | `ReglasUsuario` | `Correo` | `usuarios` | `alias_usuario` | 50 | false | Correo | true |
| 23 | `ReglasUsuario` | `Password` | `auth.users` | `encrypted_password` | 72 | false | Ninguno | false (min 6, max 72) |
| 24 | `ReglasUsuario` | `Empleado` | `usuarios` | `id_empleado` | null | false | Ninguno | true |
| 25 | `ReglasUsuario` | `Rol` | `usuarios` | `id_rol` | null | false | Ninguno | true |
| 26 | `ReglasRol` | `Nombre` | `roles` | `nombre_rol` | 50 | false | Ninguno | true |
| 27 | `ReglasContacto` | `Nombre` | `contactos_proveedor` | `nombre_contacto` | 100 | false | Ninguno | true |
| 28 | `ReglasContacto` | `Telefono` | `contactos_proveedor` | `telefono_contacto` | 20 | false | Telefono | false |
| 29 | `ReglasContacto` | `Correo` | `contactos_proveedor` | `correo_contacto` | 100 | false | Correo | false |
| 30 | `ReglasEmpresa` | `Nombre` | `empresa` | `nombre_empresa` | 200 | false | Ninguno | true |
| 31 | `ReglasEmpresa` | `Rtn` | `empresa` | `rtn_empresa` | 20 | false | Rtn | false |
| 32 | `ReglasEmpresa` | `Telefono` | `empresa` | `telefono_empresa` | 20 | false | Telefono | false |
| 33 | `ReglasEmpresa` | `Correo` | `empresa` | `correo_empresa` | 100 | false | Correo | false |
| 34 | `ReglasEmpresa` | `Direccion` | `empresa` | `direccion_empresa` | 500 | true | Ninguno | false |

---

## 6. Conclusiones y Guía para los Agentes de Implementación

1. **Capa de Dominio (`CapaDominio/Reglas/ReglasEntidades.cs`):**
   - Debe actualizarse para alinearse 100% con la base de datos de PostgreSQL y la tabla de especificación anterior.
   - Resolver inmediatamente la inconsistencia crítica de `ReglasCategoria.Descripcion` (actualmente en 255, pero la columna BD es `varchar(200)`).
   - Incorporar las reglas faltantes: `ReglasProducto.Contenido`, `ReglasEmpleado.Identidad`, `ReglasUsuario.Correo`, `ReglasEmpresa.Direccion`.
   - Agregar topes máximos en campos con formato (`Rtn` a 20, `Telefono` a 20, `Correo` a 100).
   - Documentar explícitamente en el código el marcador léxico de tope de UI (500) para columnas `text`.

2. **Suite de Tests (`BimboProyecto.Tests/`):**
   - Crear `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` implementando los tres tipos de pruebas (Deriva contra BD, Valores offline, Auditoría por reflexión).
   - Actualizar `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs` con pruebas de frontera para `NoExcedeLargo` y `TieneLargoMinimo`.

Este reporte concluye satisfactoriamente la prospección y prosigue con la entrega al orquestador.
