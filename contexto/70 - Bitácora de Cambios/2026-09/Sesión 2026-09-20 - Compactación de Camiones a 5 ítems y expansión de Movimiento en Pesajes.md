---
title: "Sesión 2026-09-20 — Compactación de Camiones a 5 ítems y expansión de Movimiento en Pesajes"
tags:
  - sesion
  - pesaje
  - wpf
  - ui
  - layout
date: 2026-09-20
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) con Fernando
---

# Sesión 2026-09-20 — Compactación de Camiones a 5 ítems y expansión de Movimiento en Pesajes

> [!success] Resultado
> Se eliminó el espacio blanco vacío debajo del quinto camión en la tarjeta superior izquierda de Pesajes. Se fijó la altura de `DgCamiones` a la capacidad física máxima exacta de 5 ítems (242 px), logrando que la botonera de acciones quede inmediatamente pegada a la última fila, y se transfirió todo el espacio vertical liberado a la tarjeta de **Movimiento de Materia Prima** para que albergue más productos sin requerir scroll prematuro.

---

## 1. Problema de distribución vertical en la columna izquierda

### Contexto y limitación del andén
El andén de descarga tiene una capacidad máxima física y operativa de **5 camiones simultáneos** (`Camiones 5/5`). En el diseño original, la columna izquierda dividía el espacio en dos mitades simétricas de proporción flexible:
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="*"/>  <!-- Fila 0: Camiones de Entrega -->
    <RowDefinition Height="14"/> <!-- Fila 1: Separador -->
    <RowDefinition Height="*"/>  <!-- Fila 2: Movimiento de Materia Prima -->
</Grid.RowDefinitions>
```

### Síntomas observados
1. **Espacio muerto en Camiones:** En monitores de resolución estándar (1080p), la mitad superior otorgaba ~380 px al panel de Camiones. Al estar la grilla limitada a un máximo de 5 filas de 40 px (200 px de datos + 40 px de cabecera = 240 px), se producía una brecha vacía de más de 100 px entre la quinta fila y la botonera inferior (`+ Agregar`, `Editar`, `Cerrar camión`).
2. **Espacio comprimido en Movimiento:** Por el contrario, la tabla de productos de carga tiene cardinalidad abierta (un camión puede contener muchos productos). Al tener asignado solo un `Height="*"`, quedaba innecesariamente restringida en altura útil y forzaba el scroll vertical tras pocos items.

---

## 2. Solución técnica implementada

### A. Compactación de `DgCamiones` a 5 ítems exactos
- Se fijó la altura del `DataGrid` a `Height="242"`:
  - Cabecera de columna (`PgHeader`): **40 px**.
  - 5 filas de recepción (`RowHeight="40"`): **200 px** (5 × 40 px).
  - Tolerancia de bordes y subpíxeles: **2 px** (evita la aparición innecesaria de la barra de desplazamiento vertical de `ScrollViewer`).
- Se ajustaron las definiciones de fila de la tarjeta de Camiones a `Auto`, `Auto`, `Auto`, asegurando que la tarjeta mida exactamente lo necesario para albergar cabecera (42 px), tabla (242 px) y botonera (64 px), alcanzando ~348 px estables.

### B. Cesión de altura a Movimiento de Materia Prima
- En la columna izquierda general de `PesajeView.xaml`, se modificó la fila de Camiones a `Height="Auto"`:
  ```xml
  <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>  <!-- Camiones: tamaño compacto fijo para 5 ítems -->
      <RowDefinition Height="14"/>    <!-- Separador -->
      <RowDefinition Height="*"/>     <!-- Movimiento: absorbe todo el espacio restante -->
  </Grid.RowDefinitions>
  ```
- Al absorber todo el espacio residual vertical mediante `Height="*"`, la tabla de **Movimiento de Materia Prima** (`DgProductos`) gana más de 100 px netos de altura, permitiendo visualizar de 2 a 3 filas adicionales de productos de forma simultánea.

---

## 3. Pruebas y validación

- Se añadió la prueba de invariante en `BimboProyecto.Tests/Pesaje/PesajeWhiteBoxTests.cs`:
  - `PesajeView_DgCamionesAlturaFijaCincoItems`: valida que `DgCamiones` conserve `Height="242"` y no sufra regresiones de tamaño.
- Suite completa ejecutada: **601 pruebas unitarias pasando al 100%**.
