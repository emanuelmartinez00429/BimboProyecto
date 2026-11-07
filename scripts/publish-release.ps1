<#
.SYNOPSIS
    Compila, empaqueta y (opcionalmente) publica un release de Velopack.

.DESCRIPTION
    Punto único de entrada del pipeline de release. La versión se lee de
    CapaUI/CapaUI.csproj para que no exista un número escrito a mano en dos
    lugares (el plan mayor ya lo marcaba como requisito).

    Pasos:
      1. dotnet publish (Release, win-x64, self-contained, sin single-file).
      2. vpk download github del release anterior (necesario para generar deltas).
      3. vpk pack -> Setup.exe + .nupkg + releases.win.json en ./Releases.
      4. vpk upload github --publish (solo con -Upload).

    Sin -Upload no sale nada de la máquina: todo queda en ./Releases y sirve
    para probar el Setup.exe instalado en local.

.PARAMETER Upload
    Publica el release en GitHub. Requiere un token con permiso de escritura
    (-Token o la variable de entorno GH_TOKEN).

.PARAMETER Version
    Override explícito de la versión. Por defecto se lee <Version> del csproj.

.PARAMETER Token
    Token de GitHub. Si se omite se usa $env:GH_TOKEN.

.PARAMETER SkipDownload
    No intenta bajar el release anterior (útil en el primer release o sin red).

.EXAMPLE
    ./scripts/publish-release.ps1

    Solo empaquetado local. Deja ./Releases listo para instalar a mano.

.EXAMPLE
    $env:GH_TOKEN = 'ghp_...'; ./scripts/publish-release.ps1 -Upload

    Publica la versión actual del csproj como release estable.
#>
[CmdletBinding()]
param(
    [switch]$Upload,
    [string]$Version,
    [string]$Token = $env:GH_TOKEN,
    [switch]$SkipDownload
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# --- Constantes del paquete (deben coincidir con el AppId del runtime) -------
$PackId      = 'BimboPesaje'
$PackTitle   = 'Bimbo Pesaje Honduras'
$PackAuthors = 'Bimbo Honduras'
$MainExe     = 'CapaUI.exe'
$Channel     = 'win'
$Runtime     = 'win-x64'

$repoRoot    = Split-Path -Parent $PSScriptRoot
$csprojPath  = Join-Path $repoRoot 'CapaUI\CapaUI.csproj'
$appConfig   = Join-Path $repoRoot 'CapaUI\App.config'
$publishPath = Join-Path $repoRoot 'publish'
$releasePath = Join-Path $repoRoot 'Releases'

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "No se encontró '$Name' en el PATH. Instalalo antes de continuar."
    }
}

function Assert-LastExit([string]$Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step falló con código de salida $LASTEXITCODE."
    }
}

if (-not (Test-Path $csprojPath)) {
    throw "No se encontró el proyecto en '$csprojPath'. Corré el script desde el repo."
}

Assert-Command 'dotnet'
Assert-Command 'vpk'

# --- Versión ----------------------------------------------------------------
if (-not $Version) {
    [xml]$proj = Get-Content $csprojPath -Raw
    $Version = @($proj.Project.PropertyGroup.Version) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "El csproj no declara <Version>. Definila o pasá -Version."
}

if ($Version -notmatch '^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') {
    throw "'$Version' no parece una versión semántica válida (ej. 1.0.0, 1.1.0)."
}

# --- Repo de GitHub ---------------------------------------------------------
# Se toma de App.config para que el runtime y el pipeline apunten al mismo lado.
$repoUrl = $null
if (Test-Path $appConfig) {
    [xml]$cfg = Get-Content $appConfig -Raw
    $repoUrl = @($cfg.configuration.appSettings.add) |
        Where-Object { $_.key -eq 'UPDATE_REPO_URL' } |
        Select-Object -First 1 -ExpandProperty value
}

if ([string]::IsNullOrWhiteSpace($repoUrl)) {
    throw "No se pudo resolver UPDATE_REPO_URL desde App.config. Agregalo o pasalo explícito."
}

Write-Host "==> Versión : $Version"
Write-Host "==> Repo    : $repoUrl"
Write-Host "==> Salida  : $releasePath"

# --- 1. Publish -------------------------------------------------------------
Write-Host ''
Write-Host '==> [1/4] dotnet publish'
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $csprojPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -o $publishPath

Assert-LastExit 'dotnet publish'

$mainExePath = Join-Path $publishPath $MainExe
if (-not (Test-Path $mainExePath)) {
    throw "El publish no generó '$MainExe'. Revisá OutputType/StartupObject del csproj."
}

# --- 2. Release anterior (deltas) -------------------------------------------
Write-Host ''
Write-Host '==> [2/4] vpk download github (release anterior)'
if ($SkipDownload) {
    Write-Host '    Omitido (-SkipDownload). No se generarán deltas.'
}
else {
    vpk download github --repoUrl $repoUrl --outputDir $releasePath --channel $Channel
    if ($LASTEXITCODE -ne 0) {
        # Primer release, repo sin releases o sin red: los deltas son opcionales.
        Write-Warning 'No se pudo descargar el release anterior. Se empaqueta sin deltas.'
    }
}

# --- 3. Pack ----------------------------------------------------------------
Write-Host ''
Write-Host '==> [3/4] vpk pack'
vpk pack `
    -u $PackId `
    -v $Version `
    -p $publishPath `
    -o $releasePath `
    -c $Channel `
    -r $Runtime `
    --mainExe $MainExe `
    --packTitle $PackTitle `
    --packAuthors $PackAuthors

Assert-LastExit 'vpk pack'

# Velopack nombra el instalador '{packId}-{channel}-Setup.exe'.
$setupPath = Get-ChildItem $releasePath -Filter '*-Setup.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$manifestPath = Join-Path $releasePath "releases.$Channel.json"

foreach ($required in @($setupPath, $manifestPath)) {
    if (-not $required -or -not (Test-Path $required)) {
        throw "vpk pack no generó el instalador o '$Channel.json'. Revisá la salida de arriba."
    }
}

Write-Host "    OK: $(Split-Path -Leaf $setupPath) + $(Split-Path -Leaf $manifestPath)"

# --- 4. Upload --------------------------------------------------------------
if (-not $Upload) {
    Write-Host ''
    Write-Host '==> [4/4] Upload omitido.'
    Write-Host "    Para publicar: `$env:GH_TOKEN='...'; ./scripts/publish-release.ps1 -Upload"
    Write-Host "    Instalador local: $setupPath"
    return
}

Write-Host ''
Write-Host '==> [4/4] vpk upload github --publish'
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw 'Falta el token de GitHub. Pasá -Token o definí $env:GH_TOKEN.'
}

vpk upload github `
    --repoUrl $repoUrl `
    --token $Token `
    --outputDir $releasePath `
    --channel $Channel `
    --publish `
    --tag "v$Version" `
    --releaseName "$PackTitle $Version"

Assert-LastExit 'vpk upload github'

Write-Host ''
Write-Host "==> Release v$Version publicado."
