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

    # Neo4j rejects null properties inside MERGE maps; the syntax seed uses null
    # to represent values the parser could not infer, so omit those properties.
    $content = (Get-Content -Path $Path -Raw) -replace ', [A-Za-z][A-Za-z0-9_]*: null', ''
    $content | & docker compose '--profile' 'kg' 'exec' '-T' 'neo4j' 'cypher-shell' '--format' 'plain'
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose command failed with exit code $LASTEXITCODE."
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
        throw 'Neo4j did not become healthy within 60 seconds.'
    }

    Write-Host 'Wiping existing graph data...'
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH (n) DETACH DELETE n')

    Write-Host "Loading ontology: $ontologyFile"
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', '--file', '/tools/kg/seed/ontology.cypher')
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'CALL db.awaitIndexes()')

    $relativeSeed = $seedFile.Substring($seedDirectory.Length).TrimStart('\', '/').Replace('\', '/')
    Write-Host "Loading seed: $relativeSeed"
    Invoke-ComposeNeo4jSeed $seedFile
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'CALL db.awaitIndexes()')

    Write-Host 'Graph summary:'
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH (n) RETURN labels(n)[0] AS label, count(*) AS count ORDER BY label')
    Invoke-ComposeNeo4j @('--profile', 'kg', 'exec', '-T', 'neo4j', 'cypher-shell', '--format', 'plain', 'MATCH ()-[r]->() RETURN ''Edges'' AS label, count(r) AS count')
}
finally {
    Pop-Location
}
