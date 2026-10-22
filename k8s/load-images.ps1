<#
.SYNOPSIS
    Builds the Option B service images and loads them into every node of a
    Kubernetes cluster (kind-style clusters use a per-node containerd, so images
    must be imported explicitly).

.EXAMPLE
    ./k8s/load-images.ps1 -Context docker-desktop
    ./k8s/load-images.ps1 -Context kind-desktop
#>
param(
    [string]$Context = "docker-desktop",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    if (-not $SkipBuild) {
        Write-Host "Building images..." -ForegroundColor Cyan
        docker compose -f docker-compose.option-b.yml build
        if ($LASTEXITCODE -ne 0) { throw "Image build failed." }
    }

    $nodes = kubectl --context $Context get nodes -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}'
    if ($LASTEXITCODE -ne 0) { throw "Could not list nodes for context '$Context'." }
    $nodes = $nodes | Where-Object { $_.Trim() -ne "" }
    if (-not $nodes) { throw "No nodes found for context '$Context'." }

    $images = @(
        "dotnetcore-hello:latest",
        "dotnetcore-audit:latest",
        "dotnetcore-notify:latest",
        "rabbitmq:3-management"
    )

    foreach ($image in $images) {
        foreach ($node in $nodes) {
            Write-Host "Importing $image -> $node" -ForegroundColor DarkGray
            # Pipe the image tarball straight into the node's containerd.
            docker save $image | docker exec -i $node ctr -n k8s.io images import -
            if ($LASTEXITCODE -ne 0) { throw "Failed to import $image into $node." }
        }
    }

    Write-Host ""
    Write-Host "Images loaded into all nodes:" -ForegroundColor Green
    foreach ($node in $nodes) {
        Write-Host "  $node"
    }
}
finally {
    Pop-Location
}
