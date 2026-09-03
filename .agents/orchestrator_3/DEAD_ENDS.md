# Dead Ends Log

| Iteration | Approach / Assumption Tried | Why It Failed | Mitigating Strategy Required |
|---|---|---|---|
| 1 | Assuming `InvalidadorCacheRealtime` Singleton subscription persists across `RealtimeService.DesconectarAsync()` | `DesconectarAsync()` clears `_suscriptores.Clear()`. Second user on shared terminal receives 0 events. | Explicit lifecycle re-subscription in `MainWindow.OnLoaded` (or `InvalidadorCacheRealtime.Suscribir()`) upon each user session start. |
| 1 | Assuming `RemoveByTagAsync("catalogos")` matches hierarchical tags like `catalogos:categoria` | `ZiggyCreatures.FusionCache 2.0.2` compares tags by exact string match (no wildcards/prefix matching). Purged 0 entries. | Entries must be registered with composite tags: `tags: new[] { TagsCache.CatalogosRaiz, $"catalogos:{nombreTabla}" }`. |
| 1 | Assuming `SocketState.Reconnect` represents restored connection | `Reconnect` in Supabase Realtime represents the reconnecting loop while still disconnected. Purging then forces failing HTTP calls instead of using Fail-Safe. | Purge must trigger on transition to `SocketState.Open` or `IConexionMonitor.Reconectado`. |
| 1 | Allowing `OperationCanceledException` to propagate from caller cancellation token | Escaping `OperationCanceledException` from `async void OnLoaded` crashes the WPF process to desktop. | `CachedCatalogoRepository` must catch `OperationCanceledException` and return `Result.Fail("Operación cancelada")` defensively. |
