[CmdletBinding()]
param(
    [string]$SeedFile
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$seedDirectory = Join-Path $repositoryRoot 'tools\kg'
$ontologyFile = Join-Path $repositoryRoot 'tools\kg\seed\ontology.cypher'

function Invoke-ComposeNeo4j {
    param([string[]]$Arguments)

    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-ComposeNeo4jSeed {
    param([string]$Path)

    # Stage the seed inside the bind-mounted tree and load it with --file, the same way the
    # ontology is loaded. Piping it to cypher-shell's stdin instead would break under
    # Windows PowerShell 5.1, which prefixes a UTF-8 BOM onto a native command's stdin
    # and makes Neo4j reject the very first statement.
    #
    # The staged copy is byte-for-byte the seed: CypherEmitter omits null properties rather
    # than emitting `key: null`, so nothing has to be rewritten on the way in and the graph
    # matches the file it was loaded from. Staging survives because -SeedFile may point
    # outside the bind mount.
    $stagedHostPath = Join-Path $seedDirectory '.load-seed.staged.cypher'
    Copy-Item -Path $Path -Destination $stagedHostPath -Force
    try {
        Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', '--file', '/tools/kg/.load-seed.staged.cypher')
    }
    finally {
        Remove-Item -Path $stagedHostPath -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path $ontologyFile -PathType Leaf)) {
    throw "Required ontology file was not found: $ontologyFile"
}

if ([string]::IsNullOrWhiteSpace($SeedFile)) {
    $seed = Get-ChildItem -Path $seedDirectory -Filter 'kg-seed.*.cypher' -File |
        Sort-Object LastWriteTimeUtc, Name -Descending |
        Select-Object -First 1
    if ($null -eq $seed) {
        throw "No generated seed file was found. Generate one with: dotnet run --project tools/kg/kg-codegen/KgCodegen -- --root ."
    }
    $seedFile = $seed.FullName
} else {
    $seedFile = (Resolve-Path $SeedFile).Path
}

if (-not (Test-Path $seedFile -PathType Leaf)) {
    throw "Seed file was not found: $seedFile"
}

Push-Location $repositoryRoot
try {
    Write-Host "Selected seed: $seedFile"
    Write-Host 'Waiting for Neo4j to become healthy...'
    $healthy = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        $status = docker inspect --format '{{.State.Health.Status}}' ecommerceapp-neo4j 2>$null
        if ($status -eq 'healthy') {
            $healthy = $true
            Write-Host "Neo4j health: healthy (attempt $attempt)"
            break
        }

        if ([string]::IsNullOrWhiteSpace($status)) {
            Write-Host "Neo4j health: unavailable (attempt $attempt)"
        } else {
            Write-Host "Neo4j health: $status (attempt $attempt)"
        }
        Start-Sleep -Seconds 2
    }

    if (-not $healthy) {
        throw "Neo4j did not become healthy within 60 seconds. Start it with: docker compose --profile kg up -d neo4j"
    }

    Write-Host 'Wiping existing graph data...'
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH (n) DETACH DELETE n')

    Write-Host "Loading ontology: $ontologyFile"
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', '--file', '/tools/kg/seed/ontology.cypher')
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'CALL db.awaitIndexes()')

    # Display only. -SeedFile may point outside tools\kg, so this must not assume a common prefix.
    if ($seedFile.StartsWith($seedDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        $displaySeed = $seedFile.Substring($seedDirectory.Length).TrimStart('\', '/').Replace('\', '/')
    } else {
        $displaySeed = $seedFile
    }
    Write-Host "Loading seed: $displaySeed"
    Invoke-ComposeNeo4jSeed $seedFile
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'CALL db.awaitIndexes()')

    Write-Host 'Graph summary:'
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH (n) RETURN labels(n)[0] AS label, count(*) AS count ORDER BY label')
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH ()-[r]->() RETURN ''Edges'' AS label, count(r) AS count')
}
finally {
    Pop-Location
}
