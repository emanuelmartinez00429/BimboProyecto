-- =============================================================================
-- Migración 0001: Esquema de Auditoría Inmutable (WORM)
-- Proyecto Supabase: bimbo-auditoria-dev (independiente de la app de la báscula)
-- Requisito R4 — devsecops y gobernanza de datos
-- =============================================================================

-- 1. Crear esquema dedicado de auditoría
CREATE SCHEMA IF NOT EXISTS auditoria;

-- 2. Tabla de control de acceso: Lectores autorizados
CREATE TABLE IF NOT EXISTS auditoria.lectores (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email text NOT NULL UNIQUE,
    creado_en timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE auditoria.lectores IS 'Lista blanca de usuarios autenticados autorizados para consultar registros de auditoría.';

-- 3. Tabla principal de sesiones archivadas
CREATE TABLE IF NOT EXISTS auditoria.sesiones (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    date date NOT NULL,
    autor text NOT NULL,
    agente text NOT NULL,
    modelo text,
    rama text,
    commits text[],
    titulo text NOT NULL,
    tags text[],
    summary text NOT NULL,
    deuda text[],
    adr text[],
    cuerpo_md text NOT NULL,
    ruta_original text NOT NULL,
    sha256 text NOT NULL UNIQUE,
    archivado_en timestamptz NOT NULL DEFAULT now(),
    commit_archivo text
);

COMMENT ON TABLE auditoria.sesiones IS 'Registro inmutable de notas de sesión históricas de la bitácora archivadas fuera del repo.';

-- 4. Tabla de deuda técnica resuelta archivada
CREATE TABLE IF NOT EXISTS auditoria.deuda_resuelta (
    id_p text NOT NULL PRIMARY KEY,
    titulo text NOT NULL,
    prioridad text,
    cerrada_en date NOT NULL,
    cuerpo_md text NOT NULL,
    sha256 text NOT NULL UNIQUE,
    archivado_en timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE auditoria.deuda_resuelta IS 'Registro inmutable de ítems cerrados de deuda técnica cerrados hace más de 60 días.';

-- 5. Índices de texto completo (FTS) en español con GIN
CREATE INDEX IF NOT EXISTS idx_sesiones_fts_spanish ON auditoria.sesiones
USING gin (to_tsvector('spanish', coalesce(titulo, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(cuerpo_md, '')));

CREATE INDEX IF NOT EXISTS idx_deuda_resuelta_fts_spanish ON auditoria.deuda_resuelta
USING gin (to_tsvector('spanish', coalesce(id_p, '') || ' ' || coalesce(titulo, '') || ' ' || coalesce(cuerpo_md, '')));

-- Índices B-tree complementarios para consultas frecuentes
CREATE INDEX IF NOT EXISTS idx_sesiones_date ON auditoria.sesiones (date DESC);
CREATE INDEX IF NOT EXISTS idx_sesiones_autor ON auditoria.sesiones (autor);
CREATE INDEX IF NOT EXISTS idx_sesiones_agente ON auditoria.sesiones (agente);
CREATE INDEX IF NOT EXISTS idx_sesiones_deuda ON auditoria.sesiones USING gin (deuda);
CREATE INDEX IF NOT EXISTS idx_sesiones_adr ON auditoria.sesiones USING gin (adr);
CREATE INDEX IF NOT EXISTS idx_deuda_resuelta_cerrada_en ON auditoria.deuda_resuelta (cerrada_en DESC);

-- 6. Función disparadora WORM (Write Once, Read Many): Inmutabilidad estricta
CREATE OR REPLACE FUNCTION auditoria.impedir_modificacion_o_borrado()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    RAISE EXCEPTION 'Los registros de auditoria son inmutables';
END;
$$;

COMMENT ON FUNCTION auditoria.impedir_modificacion_o_borrado() IS 'Garantiza WORM rechazando cualquier intento de UPDATE o DELETE en tablas de auditoría.';

-- Triggers para sesiones y deuda_resuelta
DROP TRIGGER IF EXISTS trg_sesiones_inmutables ON auditoria.sesiones;
CREATE TRIGGER trg_sesiones_inmutables
BEFORE UPDATE OR DELETE ON auditoria.sesiones
FOR EACH ROW
EXECUTE FUNCTION auditoria.impedir_modificacion_o_borrado();

DROP TRIGGER IF EXISTS trg_deuda_resuelta_inmutable ON auditoria.deuda_resuelta;
CREATE TRIGGER trg_deuda_resuelta_inmutable
BEFORE UPDATE OR DELETE ON auditoria.deuda_resuelta
FOR EACH ROW
EXECUTE FUNCTION auditoria.impedir_modificacion_o_borrado();

-- 7. Row Level Security (RLS)
ALTER TABLE auditoria.sesiones ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditoria.deuda_resuelta ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditoria.lectores ENABLE ROW LEVEL SECURITY;

-- Políticas SELECT solo para authenticated en lista de lectores
DROP POLICY IF EXISTS "lectores_pueden_consultar_sesiones" ON auditoria.sesiones;
CREATE POLICY "lectores_pueden_consultar_sesiones"
ON auditoria.sesiones
FOR SELECT
TO authenticated
USING (
    (auth.jwt() ->> 'email') IN (SELECT email FROM auditoria.lectores)
);

DROP POLICY IF EXISTS "lectores_pueden_consultar_deuda_resuelta" ON auditoria.deuda_resuelta;
CREATE POLICY "lectores_pueden_consultar_deuda_resuelta"
ON auditoria.deuda_resuelta
FOR SELECT
TO authenticated
USING (
    (auth.jwt() ->> 'email') IN (SELECT email FROM auditoria.lectores)
);

DROP POLICY IF EXISTS "lectores_pueden_consultar_lectores" ON auditoria.lectores;
CREATE POLICY "lectores_pueden_consultar_lectores"
ON auditoria.lectores
FOR SELECT
TO authenticated
USING (
    (auth.jwt() ->> 'email') IN (SELECT email FROM auditoria.lectores)
);

-- Ninguna política para anon (acceso bloqueado por defecto con RLS habilitado).
-- El rol service_role de Supabase omite RLS (bypassrls) para ingestión de datos por pipeline.

-- 8. Concesión de privilegios mínimos sobre el esquema auditoria
GRANT USAGE ON SCHEMA auditoria TO authenticated, service_role;
GRANT SELECT ON ALL TABLES IN SCHEMA auditoria TO authenticated;
GRANT ALL ON ALL TABLES IN SCHEMA auditoria TO service_role;

REVOKE ALL ON SCHEMA auditoria FROM anon;
REVOKE ALL ON ALL TABLES IN SCHEMA auditoria FROM anon;

-- Configuración de PostgREST:
-- El esquema auditoria no está expuesto a la Data API de PostgREST por defecto (pgrst.db_schemas='public').
-- Para exponerlo de forma controlada a PostgREST en bimbo-auditoria-dev:
-- ALTER ROLE authenticator SET pgrst.db_schemas = 'public, auditoria';
-- NOTIFY pgrst, 'reload config';
