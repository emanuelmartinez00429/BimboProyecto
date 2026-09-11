---
title: "Velopack y GitHub Actions — Referencias para actualizaciones WPF"
tags: [referencia, velopack, github-actions, wpf, despliegue]
date: 2026-09-08
lifecycle: draft
---

# Velopack y GitHub Actions — Referencias para actualizaciones WPF

Documentación oficial consultada durante la planificación del 2026-09-08. No hay versión de Velopack elegida ni prueba de concepto ejecutada. Revalidar APIs, licencias, límites y disponibilidad al autorizar implementación; estas referencias no acreditan capacidades configuradas en la cuenta del proyecto.

## Integración, instalación y cierre

- [Integración general](https://docs.velopack.io/integrating/overview): bootstrap temprano, consulta/descarga/aplicación explícita y opción de desactivar autoaplicación al inicio. El timeout documentado de `WaitExitThenApplyUpdates` puede terminar el proceso; no sustituye la coordinación del trabajo de negocio.
- [Integración WPF](https://docs.velopack.io/getting-started/wpf): punto de entrada explícito antes de inicializar WPF, con el ajuste correspondiente del proyecto para no generar dos entradas.
- [Instalador](https://docs.velopack.io/packaging/installer): opciones de empaquetado e instalación; validar MSI por máquina, requisitos de herramientas y elevación con la versión que se fije.
- [Comportamiento en Windows](https://docs.velopack.io/packaging/operating-systems/windows): tratamiento de archivos/procesos durante la actualización. La barrera de la app debe completarse antes de entregar el control al actualizador.
- [Repositorio oficial y licencia](https://github.com/velopack/velopack): referencia de código, licencia MIT, versiones e incidencias; revisar la versión concreta antes de incorporarla, sin ejecutar scripts externos por esta consulta.

## Canales, publicación y recuperación

- [Canales](https://docs.velopack.io/packaging/channels) y [cambio de canal](https://docs.velopack.io/integrating/switching-channels): distinguir el canal embebido de empaquetado del canal explícito que consulta una estación. Probar `ExplicitChannel` y restricciones de downgrade antes de adoptar un mecanismo de promoción.
- [Fuentes de actualización](https://docs.velopack.io/integrating/update-sources): fuente GitHub y descarga pública sin token de cliente; descarga privada requiere otra configuración/autorización.
- [GitHub Actions](https://docs.velopack.io/distributing/github-actions): coherencia entre CLI y biblioteca y uso de paquetes previos para generar deltas.
- [Firma](https://docs.velopack.io/packaging/signing): firma integrada al empaquetado y gestión del material firmante. Firmar antes de probar/promover el artefacto, no cambiar sus bytes después del piloto.
- [Instalación de una versión concreta](https://docs.velopack.io/integrating/specific-version): distinguir la posibilidad de aplicar una versión de un sistema automático de diagnóstico y rollback, que no se presupone.

## GitHub: costes, autorización y separación de repositorios

- [Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases): activos descargables asociados a versiones; distinguir activos de Release del almacenamiento de artefactos temporales de Actions.
- [Protecciones de ramas](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches): disponibilidad dependiente de plan y visibilidad. No asumir protección de ramas privadas con Free.
- [Entornos y aprobaciones](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments): los controles disponibles también dependen de visibilidad/plan. Verificar dónde puede residir un gate efectivo de promoción.
- [Autenticación con GITHUB_TOKEN](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token): permisos acotados al repositorio; para publicación entre repositorios, evaluar una GitHub App con acceso específico y token temporal.
- [Facturación de Actions](https://docs.github.com/en/billing/concepts/product-billing/github-actions): revisar cuotas incluidas, almacenamiento, límites y prevención de cargos. No extrapolar «repositorio público» a uso ilimitado de CI privado.

## Confianza Windows, runtime y backend

- [Microsoft: SmartScreen y reputación](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation): firma, confianza y reputación son conceptos distintos; no prometer ausencia de avisos por el mero hecho de firmar.
- [Microsoft: ciclo de vida de .NET](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core): .NET 8 finaliza soporte el 2026-11-10. La solución ya migró a .NET 10 (`net10.0-windows` / `net10.0`) el 2026-09-10 para asegurar soporte extendido continuo.
- [Supabase: gestión de entornos](https://supabase.com/docs/guides/deployment/managing-environments): separación de desarrollo/validación/producción. La decisión de no contratar staging inicialmente no elimina la necesidad de validar migraciones y compatibilidad antes de producción.

## Qué falta demostrar en este proyecto

No se han probado Velopack, firma, elevación MSI, promoción de los mismos bytes entre canales, recuperación, cuotas de cuenta, gates GitHub ni compatibilidad de migraciones en entorno limpio. Todas esas verificaciones pertenecen a las fases y matriz del plan, no a esta consulta documental.

## Relaciones

- [[Conocimiento Principal]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Sesión 2026-09-08 - Plan de CI-CD y actualizaciones remotas]]
