---
title: "Módulo Configuración de Empresa"
tags: [bimbo, modulo, configuracion, empresa, tema, storage]
date: 2026-08-14
---

# Módulo Configuración de Empresa

## Propósito

Administra la única fila de `public.empresa` desde el engranaje de la barra superior. La pantalla permite editar los datos generales, reemplazar el logo corporativo y cambiar el color principal de toda la interfaz.

Desde 2026-08-15 también administra `empresa.icono_sidebar`: una segunda imagen independiente que reemplaza el recurso empacado `Resources/bimbo-logo.png` en la barra superior. El recurso local se conserva únicamente como fallback cuando la columna está vacía o la descarga falla.

El acceso exige la acción literal `Modificar Configuración` en tres niveles: visibilidad/apertura del modal, repositorio C# y políticas RLS de Supabase.

## Flujo vigente

```text
MainWindow (engranaje)
    ↓ overlay global
ConfiguracionEmpresaModal + ConfiguracionEmpresaViewModel
    ↓ IEmpresaRepository
EmpresaRepository : RepositorioBase
    ├─ public.empresa
    └─ Storage/empresa-logos (logo principal + ícono del sidebar)
```

- La empresa es un singleton funcional: la pantalla solo edita la fila existente.
- `EmpresaRepository` usa `Result`, `TryAsync` y la sesión inyectada.
- El cliente actualiza únicamente campos editables; el timestamp de modificación queda bajo responsabilidad del trigger configurado en la base.
- Las validaciones de negocio de RTN, correo, teléfono y dominio siguen pospuestas. El logo sí se limita técnicamente a PNG/JPG/JPEG decodificable y 2 MB.
- **2026-09-18, `ConfiguracionEmpresaView`:**
  - En "Identidad visual" la columna izquierda es el **Logo** y la derecha el **Ícono del menú lateral**. Los títulos estaban cruzados respecto de sus bindings.
  - El aviso "Si cambia el dominio…" solo aparece mientras el campo de dominio tiene foco, en un recuadro amarillo pálido.
  - Ver [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]].

## Logo corporativo

El reemplazo usa un nombre nuevo por versión, conservando [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]:

1. Subir el archivo nuevo.
2. Actualizar `empresa.logo_empresa`.
3. Si la actualización falla, borrar el archivo nuevo como compensación.
4. Si funciona, reconciliar el bucket y conservar las dos rutas vigentes: logo principal e ícono lateral.
5. Copiar el archivo seleccionado al caché local para que el siguiente login no vuelva a descargarlo.

La lectura del bucket sigue siendo pública porque el login ocurre antes de autenticar. Insertar, actualizar y borrar exige `Modificar Configuración` mediante RLS.

## Tema empresarial

`EmpresaThemeService` carga el último color desde `%APPDATA%\BimboPesaje\TemaEmpresa\color.txt` antes de mostrar el login, luego lo revalida contra `empresa.color_empresa`.

Los azules de marca se reemplazaron por recursos `DynamicResource` compartidos en shell, login, controles, pantallas y modales. Al guardar un color `#RRGGBB`, la aplicación deriva variantes oscuras y claras y repinta el árbol visual sin reiniciar. Los colores de estado y acento permanecen sin cambios.

`NULL`, `sin_color` o un valor no interpretable usan el azul histórico `#1E3A8A`.

## Pendiente de verificación manual

- Abrir el modal con un usuario que tenga el permiso y con otro que no lo tenga.
- Reemplazar un logo real y comprobar que el bucket termina con un solo objeto.
- Cambiar el color y recorrer las pantallas/modales a 960×600 y con DPI distinto.
- Confirmar en el entorno objetivo que el trigger de timestamp está asociado a `empresa`; la inspección MCP del 2026-08-14 encontró la función general, pero no un trigger enlazado a esta tabla.

## Relaciones

- [[Arquitectura Actual]]
- [[ADR-019 - Configuración de empresa y tema dinámico global]]
- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]
- [[Deuda Técnica - Pendientes]]
- [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]]
- [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]] — títulos logo/ícono y aviso del dominio con foco
