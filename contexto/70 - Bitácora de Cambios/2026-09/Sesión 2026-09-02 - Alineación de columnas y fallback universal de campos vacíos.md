---
title: "Sesión 2026-09-02 — Alineación de columnas y fallback universal de campos vacíos"
tags:
  - sesion
  - ui
  - ux
  - wpf
  - datagrid
  - catalogos
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (sesión gestionada por Emanuel)
---

# Sesión 2026-09-02 — Alineación de columnas y fallback universal de campos vacíos

> [!success] Resultado
> Se estandarizó la alineación visual en los DataGrids (texto e identificadores a la izquierda, estado e iconos centrados) y se implementó un mecanismo universal de fallback (`TextoVacioConverter` y `TextoCeldaConFallback`) para reemplazar celdas vacías o nulas por `—` atenuado o `Sin descripción` / `Sin dirección` en cursiva, eliminando huecos en blanco y badges vacíos en todo el sistema.

---

## Problema / Motivo

1. **Alineación inconsistente en tablas de catálogo:**
   En `ProveedoresView.xaml`, las celdas de Nombre, Correo y Dirección estaban alineadas a la izquierda, mientras que el encabezado global forzaba alineación centrada y columnas como RTN y Teléfono estaban centradas. Esto producía un efecto de lectura en "zigzag" visualmente desordenado.
2. **Celdas vacías y badges huecos:**
   Cuando la base de datos traía campos opcionales nulos o en blanco (`Rtn`, `Telefono`, `Correo`, `Direccion`, `Descripcion`, `Tara`, etc.), las celdas quedaban completamente vacías, generando incertidumbre en el usuario sobre si la información no existía o no había terminado de cargar. En el caso del RTN, se renderizaba una pastilla azul vacía `[   ]`.

---

## Decisiones de Diseño y Arquitectura Aplicadas

### 1. Principio de Alineación en Tablas (UI/UX)
- **Texto, códigos y contactos:** Alineados a la izquierda tanto en cabecera (`DataGridColumnHeader`) como en celda con padding estandarizado (`Padding="10,0"`), proporcionando una guía vertical limpia e ininterrumpida.
- **Estados, booleanos e iconos:** Centrados mediante el estilo especializado `HeaderCentrado` y `CellStyle="{StaticResource CeldaCentrada}"`.

### 2. Conversor y Estilo Reactivo de Fallback
- **`TextoVacioConverter` (`CapaUI/Converters/TextoVacioConverter.cs`):**  
  Detecta valores `null`, strings vacíos o cadenas con espacios en blanco. Si no se provee `ConverterParameter`, retorna el guion tipográfico largo `—`. Si se provee parámetro, retorna dicho texto (ej. `Sin descripción`, `Sin dirección`).
- **`TextoCeldaConFallback` (`CapaUI/Resources/Styles.xaml`):**  
  Estilo de `TextBlock` para celdas de DataGrid que reacciona al texto emitido:
  - Cuando el texto es `—`, atenúa automáticamente el color a `#9CA3AF`.
  - Cuando el texto es `Sin descripción` o `Sin dirección`, asigna `#9CA3AF` y activa `FontStyle="Italic"`.
- **Badges Condicionales:**  
  En `ProveedoresView.xaml`, el `Border` del RTN se colapsa (`Visibility="Collapsed"`) mediante DataTrigger si `Rtn` es vacío o nulo, mostrando un guion plano en su lugar.

---

## Cambios Aplicados

### 1. Núcleo UI y Conversores
- `CapaUI/Converters/TextoVacioConverter.cs`: Nuevo conversor que implementa `IValueConverter` con soporte de `ConverterParameter`.
- `CapaUI/App.xaml`: Registro global de `TextoVacioConverter` en los recursos de la aplicación.
- `CapaUI/Resources/Styles.xaml`:
  - Declarado `TextoVacioConverter` en `Conversores Compartidos`.
  - Definido el estilo reutilizable `TextoCeldaConFallback` con disparadores para `—`, `Sin descripción` y `Sin dirección`.

### 2. Vistas y Tablas Actualizadas
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml`:
  - Encabezado general alineado a la izquierda con padding `10,0`.
  - Creado estilo `HeaderCentrado` para la columna `ESTADO`.
  - `DgProveedores` configurado con `CellStyle="{StaticResource CeldaIzquierda}"`.
  - Anchos dinámicos con `Width="Auto"` y `MinWidth` defensivos para Nombre, RTN, Teléfono, Correo, dejando Dirección como elástica (`Width="*"`, `MinWidth="260"`).
  - Scroll horizontal activado (`ScrollViewer.HorizontalScrollBarVisibility="Auto"`).
  - RTN con colapso de badge ante vacíos; Teléfono, Correo con fallback `—`; Dirección con fallback `Sin dirección`.
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml`:
  - `DESCRIPCIÓN` con fallback `Sin descripción` en cursiva tenue.
  - `PROVEEDOR` y `PAÍS` con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml`:
  - `DESCRIPCIÓN` con fallback `Sin descripción`.
- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionesView.xaml`:
  - `DESCRIPCIÓN` con fallback `Sin descripción`.
- `CapaUI/Formularios/Principal/Pantallas/ContactosProveedores/ContactosProveedoresView.xaml`:
  - `DgProveedores`: RTN y Teléfono con fallback `—`.
  - `DgContactos`: Teléfono y Correo con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesView.xaml`:
  - `DgFabricantes`: Descripción con fallback `Sin descripción`; Proveedor con fallback `—`.
  - `DgContactos`: Teléfono y Correo con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml`:
  - Identidad, Teléfono y Correo con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml`:
  - Email y Último Acceso (con `TargetNullValue="—"`) con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`:
  - Tara, Fabricante, Proveedor, País, Contenido y Presentación con fallback `—`.
- `CapaUI/Formularios/Principal/Pantallas/Bitacora/BitacoraView.xaml`:
  - Campo Afectado y Detalle con fallback `—`.

---

## Verificación

- `dotnet build BimboProyecto.sln`: 0 errores, 0 advertencias.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`: **223/223 pruebas superadas (100%)**.
- Validación de estilos y precedencia de dependencias de WPF confirmada.

---

## Relaciones

- [[Módulos de Catálogos Administrativos]]
- [[Módulo Productos]]
- [[Módulo Usuarios]]
- [[Módulo Empleados]]
- [[Módulo Contactos (Drill-down)]]
- [[Módulo Bitácora]]
- [[Arquitectura Actual]]
