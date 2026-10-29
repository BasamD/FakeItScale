# Publish only generated UI screenshots to a separate branch for visual review.
# Uses the existing Actions checkout authentication; never changes main or its index.
$ErrorActionPreference = 'Stop'
$entries = @(Get-ChildItem artifacts -Filter 'Preview*.png' | Sort-Object Name | ForEach-Object {
    $blob = git hash-object -w -- $_.FullName
    if ($LASTEXITCODE -ne 0) { throw 'Could not store preview.' }
    "100644 blob $blob`t$($_.Name)"
})
if ($entries.Count -eq 0) { throw 'No Windows previews were produced.' }
$tree = ($entries -join "`n") | git mktree
if ($LASTEXITCODE -ne 0) { throw 'Could not assemble preview tree.' }
$remote = git ls-remote --heads origin ui-previews
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect preview branch.' }
$commitArgs = @('-c', 'user.name=ScaleSim build', '-c', 'user.email=scalesim-build@users.noreply.github.com', 'commit-tree', $tree, '-m', "Windows UI previews for $env:GITHUB_SHA")
if ($remote) {
    git fetch origin refs/heads/ui-previews
    if ($LASTEXITCODE -ne 0) { throw 'Could not fetch preview history.' }
    $parent = git rev-parse FETCH_HEAD
    $commitArgs += @('-p', $parent)
}
$commit = & git @commitArgs
if ($LASTEXITCODE -ne 0) { throw 'Could not create preview commit.' }
git push origin "${commit}:refs/heads/ui-previews"
if ($LASTEXITCODE -ne 0) { throw 'Could not publish UI previews.' }
