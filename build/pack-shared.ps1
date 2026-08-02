#!/usr/bin/env pwsh
# Packs the shared layer into the local feed (artifacts/packages).
#
# Order matters and cannot be replaced by `dotnet pack TelegramLike.Shared.slnx` on a cold
# feed: Shared.Application restores TelegramLike.Shared.Domain as a *package*, so that package
# has to exist before this project can even restore. That is the versioned-dependency boundary
# doing its job — in a real setup each of these is its own pipeline publishing to a feed.
#
# Run this after cloning, and after any change to the shared layer.
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent

# NuGet caches an extracted package by id+version, so re-packing 1.0.0 with new code leaves
# every consumer compiling against the previous 1.0.0 — silently, exactly like the stale-publish
# Docker gotcha in CLAUDE.md. A real feed refuses to re-publish a version; here we emulate that
# by evicting only our own ids (never `nuget locals --clear`, which would discard every
# third-party package too). Bumping the version is still the right move for anything a consumer
# should notice — this only keeps local iteration from lying to you.
$globalPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
if (Test-Path $globalPackages) {
    Get-ChildItem $globalPackages -Directory -Filter 'telegramlike.*' |
        Where-Object { $_.Name -ne 'telegramlike.client' } |
        ForEach-Object {
            Write-Host "==> evicting cached $($_.Name)"
            Remove-Item $_.FullName -Recurse -Force
        }
}

$projects = @(
    'src/TelegramLike.Contracts'
    'src/shared/TelegramLike.Shared.Domain'
    'src/shared/TelegramLike.Shared.Application'
    'src/shared/TelegramLike.Shared.Infrastructure'
    'src/shared/TelegramLike.Shared.Api'
)

foreach ($project in $projects) {
    Write-Host "==> packing $project"
    dotnet pack (Join-Path $root $project) -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "pack failed for $project" }
}

Write-Host "Shared packages are in artifacts/packages."
