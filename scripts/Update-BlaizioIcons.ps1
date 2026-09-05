<#
.SYNOPSIS
    (Re)generates the icon path data of every Blaizio icon set into its package project.

.DESCRIPTION
    One script, five sets. Each set is a catalogue entry below naming where it downloads from, how
    its families (styles) are found in the download, and how they paint - grid and stroke width -
    which is what BzIcon needs to render any of them with one component:

      Set         Package                 Members                        Source
      Tabler      Blaizio.Icons           Icons.Outline.* / Icons.Filled.*   github tabler/tabler-icons
      Lucide      Blaizio.Icons.Lucide    Lucide.Outline.*                   github lucide-icons/lucide
      Phosphor    Blaizio.Icons.Phosphor  Phosphor.{Thin,Light,Regular,Bold,Fill,Duotone}.*   github phosphor-icons/core
      Remix       Blaizio.Icons.Remix     Remix.Line.* / Remix.Fill.*        github Remix-Design/RemixIcon
      HugeIcons   Blaizio.Icons.HugeIcons HugeIcons.StrokeRounded.*          npm @hugeicons/core-free-icons

    Every family becomes one generated file (<Class>.<Family>.cs) holding a static class of
    Icon-valued expression-bodied properties: typed, self-describing (the Icon carries paint, grid
    and stroke width) and trim-friendly - no static constructor over the family, so the IL trimmer
    drops every icon a consumer never references. The set's LICENSE is copied next to the code as
    THIRD-PARTY-LICENSE.txt and packed with the NuGet package.

    The docs site derives its icon browser data from these generated files at build time
    (BlaizioIconsJson in Blaizio.Docs.csproj), so the browser can never drift from the packages.

.PARAMETER Set
    Which sets to regenerate. Default: All.

.PARAMETER Source
    A folder holding an already-downloaded copy of ONE set (its repository or npm package
    extracted), for offline runs. Only valid with a single -Set.

.PARAMETER KeepDownloads
    Keep scripts/.icons-tmp/<Set> after the run (re-runs then skip the download).

.EXAMPLE
    pwsh ./scripts/Update-BlaizioIcons.ps1
    pwsh ./scripts/Update-BlaizioIcons.ps1 -Set Lucide,Phosphor
    pwsh ./scripts/Update-BlaizioIcons.ps1 -Set Tabler -Source ~/tabler-icons
#>
[CmdletBinding()]
param(
    [ValidateSet('Tabler', 'Lucide', 'Phosphor', 'Remix', 'HugeIcons', 'All')]
    [string[]]$Set = @('All'),
    [string]$Source,
    [string]$Namespace = 'Blaizio',
    [switch]$KeepDownloads
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tmpRoot = Join-Path $PSScriptRoot '.icons-tmp'
$today = [datetime]::UtcNow.ToString('yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)

# ---- The set catalogue ------------------------------------------------------------------------
# Family: Name (the nested class), Kind (Outline = stroked, Filled = solid), ViewBox, Stroke, and
# how its files are found - Dir (relative to the download root, matched by suffix), Recurse,
# Suffix (stripped from the file name; a family owning the unsuffixed files sets Bare).
$catalog = [ordered]@{
    Tabler = @{
        Label = 'Tabler Icons'; Project = 'Blaizio.Icons'; Class = 'Icons'
        Url = 'https://github.com/tabler/tabler-icons/archive/refs/heads/main.zip'; Repo = 'tabler/tabler-icons'; Branch = 'main'
        Home = 'https://tabler.io/icons'; LicenseName = 'MIT'
        Families = @(
            @{ Name = 'Outline'; Kind = 'Outline'; ViewBox = '0 0 24 24'; Stroke = 2; Dir = 'icons/outline'; Describe = 'stroked' }
            @{ Name = 'Filled'; Kind = 'Filled'; ViewBox = '0 0 24 24'; Stroke = 2; Dir = 'icons/filled'; Describe = 'solid' }
        )
    }
    Lucide = @{
        Label = 'Lucide'; Project = 'Blaizio.Icons.Lucide'; Class = 'Lucide'
        Url = 'https://github.com/lucide-icons/lucide/archive/refs/heads/main.zip'; Repo = 'lucide-icons/lucide'; Branch = 'main'
        Home = 'https://lucide.dev'; LicenseName = 'ISC'
        Families = @(
            @{ Name = 'Outline'; Kind = 'Outline'; ViewBox = '0 0 24 24'; Stroke = 2; Dir = 'icons'; Describe = 'stroked (the single Lucide style)' }
        )
    }
    Phosphor = @{
        Label = 'Phosphor Icons'; Project = 'Blaizio.Icons.Phosphor'; Class = 'Phosphor'
        Url = 'https://github.com/phosphor-icons/core/archive/refs/heads/main.zip'; Repo = 'phosphor-icons/core'; Branch = 'main'
        Home = 'https://phosphoricons.com'; LicenseName = 'MIT'
        Families = @(
            @{ Name = 'Thin'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/thin'; Suffix = '-thin'; Describe = 'thin weight' }
            @{ Name = 'Light'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/light'; Suffix = '-light'; Describe = 'light weight' }
            @{ Name = 'Regular'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/regular'; Describe = 'regular weight' }
            @{ Name = 'Bold'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/bold'; Suffix = '-bold'; Describe = 'bold weight' }
            @{ Name = 'Fill'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/fill'; Suffix = '-fill'; Describe = 'fill weight' }
            @{ Name = 'Duotone'; Kind = 'Filled'; ViewBox = '0 0 256 256'; Dir = 'assets/duotone'; Suffix = '-duotone'; Describe = 'duotone weight (a 20% tint layer under the solid shape)' }
        )
    }
    Remix = @{
        Label = 'Remix Icon'; Project = 'Blaizio.Icons.Remix'; Class = 'Remix'
        Url = 'https://github.com/Remix-Design/RemixIcon/archive/refs/heads/master.zip'; Repo = 'Remix-Design/RemixIcon'; Branch = 'master'
        Home = 'https://remixicon.com'; LicenseName = 'Apache-2.0'
        Families = @(
            @{ Name = 'Line'; Kind = 'Filled'; ViewBox = '0 0 24 24'; Dir = 'icons'; Recurse = $true; Suffix = '-line'; Bare = $true; Describe = 'line style (drawn as solid paths)' }
            @{ Name = 'Fill'; Kind = 'Filled'; ViewBox = '0 0 24 24'; Dir = 'icons'; Recurse = $true; Suffix = '-fill'; Describe = 'fill style' }
        )
    }
    HugeIcons = @{
        Label = 'Hugeicons'; Project = 'Blaizio.Icons.HugeIcons'; Class = 'HugeIcons'
        Npm = '@hugeicons/core-free-icons'
        Home = 'https://hugeicons.com'; LicenseName = 'MIT'
        Families = @(
            @{ Name = 'StrokeRounded'; Kind = 'Outline'; ViewBox = '0 0 24 24'; Stroke = 1.5; Dir = 'dist/esm'; Js = $true; Describe = 'stroke-rounded (the free set)' }
        )
    }
}

# ---- Helpers ----------------------------------------------------------------------------------

function ConvertTo-PascalCase {
    param([string]$Name, [hashtable]$Seen, [string[]]$Reserved)

    # Already-PascalCase input (Hugeicons: WalletAdd02, AArrowDown) is kept verbatim - ToTitleCase
    # would lower-case everything after the first letter of each run. Kebab names are title-cased
    # per word and joined.
    $id = if ($Name -cmatch '^[A-Z][A-Za-z0-9]*$') { $Name }
          else { (Get-Culture).TextInfo.ToTitleCase(($Name -replace '[-_]', ' ')) -replace '\s', '' }
    $id = $id -replace '[^A-Za-z0-9_]', ''   # a stray dot or dash in a file name is not an identifier
    if ($id -match '^\d') { $id = "_$id" }
    # A member cannot share its enclosing class's name (Remix.Line.Line) or shadow an object member
    # (Phosphor.Regular.Equals); those few take an Icon suffix.
    if ($Reserved -ccontains $id) { $id = "${id}Icon" }

    $base = $id; $n = 1
    while ($Seen.ContainsKey($id)) { $id = "$base$n"; $n++ }
    $Seen[$id] = $true
    return $id
}

function Get-SvgInner {
    param([string]$Svg, [switch]$DropNoneFills)
    if ($Svg -notmatch '(?s)<svg[^>]*>(.*)</svg>') { return $null }
    $inner = $matches[1]
    # Remix ships an invisible bounding path first; it paints nothing and only pads every icon.
    if ($DropNoneFills) { $inner = $inner -replace '<path[^>]*fill="none"[^>]*/>', '' }
    ($inner.Trim() -replace '\r?\n', ' ' -replace '\s+', ' ' -replace '/>\s+<', '/><' -replace '>\s+<', '><').Trim()
}

# Hugeicons publishes each icon as a JS array of [tag, {attrs}] pairs, not SVG. Rebuild the inner
# markup, dropping the attributes BzIcon already sets on the <svg> (stroke, width, caps) and the
# React key; anything else (a fill, an odd width, opacity) stays per element.
function Get-HugeInner {
    param([string]$Js)
    $sb = [System.Text.StringBuilder]::new()
    foreach ($el in [regex]::Matches($Js, '\[\s*"(?<tag>[a-z]+)"\s*,\s*\{(?<attrs>[^}]*)\}\s*\]')) {
        $attrs = [System.Collections.Generic.List[string]]::new()
        foreach ($a in [regex]::Matches($el.Groups['attrs'].Value, '(?<k>[A-Za-z]+):\s*"(?<v>[^"]*)"')) {
            $k = $a.Groups['k'].Value; $v = $a.Groups['v'].Value
            if ($k -eq 'key') { continue }
            $name = [regex]::Replace($k, '[A-Z]', { param($m) '-' + $m.Value.ToLowerInvariant() })
            $isDefault = ($name -eq 'stroke' -and $v -eq 'currentColor') -or
                         ($name -eq 'stroke-width' -and $v -eq '1.5') -or
                         ($name -eq 'stroke-linecap' -and $v -eq 'round') -or
                         ($name -eq 'stroke-linejoin' -and $v -eq 'round')
            if (-not $isDefault) { $attrs.Add("$name=`"$v`"") }
        }
        [void]$sb.Append("<$($el.Groups['tag'].Value) $($attrs -join ' ') />")
    }
    if ($sb.Length -gt 0) { $sb.ToString() } else { $null }
}

function Find-Dir {
    param([string]$Root, [string]$Relative)
    $needle = '/' + $Relative.TrimEnd('/')
    $hit = Get-ChildItem -Path $Root -Directory -Recurse |
        Where-Object { ($_.FullName -replace '\\', '/').EndsWith($needle) } |
        Sort-Object { $_.FullName.Length } | Select-Object -First 1
    if (-not $hit) { throw "Could not find '$Relative' under $Root." }
    $hit.FullName
}

function Get-SetDownload {
    param([string]$Name, [hashtable]$Entry)
    if ($Source) { return @{ Path = $Source; Note = "local copy at $Source" } }

    $dir = Join-Path $tmpRoot $Name
    if ((Test-Path $dir) -and $KeepDownloads) { return @{ Path = $dir; Note = 'kept download' } }
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null

    if ($Entry.Npm) {
        $meta = Invoke-RestMethod -Uri "https://registry.npmjs.org/$($Entry.Npm)/latest"
        Write-Host "Downloading $($Entry.Npm) $($meta.version)..."
        $tgz = Join-Path $dir 'package.tgz'
        Invoke-WebRequest -Uri $meta.dist.tarball -OutFile $tgz
        # .NET's own tar reader: the tar on PATH may be Git's GNU tar, which reads "D:\..." as a
        # remote host and fails.
        $stream = [System.IO.File]::OpenRead($tgz)
        try {
            $gzip = [System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionMode]::Decompress)
            [System.Formats.Tar.TarFile]::ExtractToDirectory($gzip, $dir, $true)
        } finally { $stream.Dispose() }
        Remove-Item $tgz -Force
        return @{ Path = $dir; Note = "$($Entry.Npm) $($meta.version)" }
    }

    $note = "$($Entry.Repo)@$($Entry.Branch)"
    try {
        $commit = Invoke-RestMethod -Uri "https://api.github.com/repos/$($Entry.Repo)/commits/$($Entry.Branch)" -Headers @{ 'User-Agent' = 'blaizio-icons' }
        $note = "$($Entry.Repo)@$($commit.sha.Substring(0, 7))"
    } catch {
        Write-Warning "Could not resolve the commit of $($Entry.Repo) ($($_.Exception.Message)); recording the branch only."
    }
    Write-Host "Downloading $($Entry.Label) ($note)..."
    $zip = Join-Path $dir 'source.zip'
    Invoke-WebRequest -Uri $Entry.Url -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $dir
    Remove-Item $zip -Force
    return @{ Path = $dir; Note = $note }
}

function Copy-License {
    param([string]$Name, [hashtable]$Entry, [string]$DownloadPath, [string]$ProjectDir, [string]$Note)
    $file = Get-ChildItem -Path $DownloadPath -File -Recurse -Depth 2 |
        Where-Object { $_.Name -match '^LICENSE(\.(md|txt))?$' } |
        Sort-Object { $_.FullName.Length } | Select-Object -First 1
    $header = @(
        "$($Entry.Label) - $($Entry.Home)"
        "Licence: $($Entry.LicenseName). Icon data generated from $Note on $today by scripts/Update-BlaizioIcons.ps1."
        "The Blaizio package code itself is MIT (see LICENSE.md in the repository); the icon data keeps its own licence below."
        ''
    ) -join "`n"
    $body = if ($file) { Get-Content -Path $file.FullName -Raw } else { "(The upstream package declares $($Entry.LicenseName) in its package.json and ships no licence file.)`n" }
    ($header + $body) | Out-File -FilePath (Join-Path $ProjectDir 'THIRD-PARTY-LICENSE.txt') -Encoding utf8 -NoNewline
}

function Write-Family {
    param([hashtable]$Entry, [hashtable]$Family, [hashtable]$Defs, [string]$ProjectDir, [string]$Note)

    $stroke = if ($Family.Stroke) { $Family.Stroke } else { 2 }
    $strokeLiteral = ([double]$stroke).ToString([System.Globalization.CultureInfo]::InvariantCulture) + 'f'
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("// <auto-generated /> Generated by Update-BlaizioIcons.ps1 on $today from $Note. Do not edit by hand.")
    [void]$sb.AppendLine("namespace $Namespace;")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("public static partial class $($Entry.Class)")
    [void]$sb.AppendLine('{')
    [void]$sb.AppendLine("    /// <summary>$($Entry.Label), $($Family.Describe): $($Defs.Count) icons on a $($Family.ViewBox) grid$(if ($Family.Kind -eq 'Outline') { ", stroke $stroke" }).</summary>")
    [void]$sb.AppendLine("    public static class $($Family.Name)")
    [void]$sb.AppendLine('    {')
    [void]$sb.AppendLine("        private static Icon I(string body) => new(body, IconKind.$($Family.Kind), `"$($Family.ViewBox)`", $strokeLiteral);")
    [void]$sb.AppendLine('')
    foreach ($name in ($Defs.Keys | Sort-Object)) {
        [void]$sb.AppendLine("        public static Icon $name => I(`"`"`"$($Defs[$name])`"`"`");")
    }
    [void]$sb.AppendLine('    }')
    [void]$sb.AppendLine('}')

    $outPath = Join-Path $ProjectDir "$($Entry.Class).$($Family.Name).cs"
    $sb.ToString() | Out-File -FilePath $outPath -Encoding utf8 -NoNewline
    Write-Host "  $($Family.Name): $($Defs.Count) icons -> $(Resolve-Path -Relative $outPath)"
}

# ---- Run --------------------------------------------------------------------------------------

$selected = if ($Set -contains 'All') { @($catalog.Keys) } else { $Set }
if ($Source -and $selected.Count -ne 1) { throw '-Source applies to a single -Set.' }

foreach ($name in $selected) {
    $entry = $catalog[$name]
    $projectDir = Join-Path $repoRoot 'src' $entry.Project
    New-Item -ItemType Directory -Path $projectDir -Force | Out-Null

    $download = Get-SetDownload -Name $name -Entry $entry
    Write-Host "$($entry.Label) -> $($entry.Project)"

    foreach ($family in $entry.Families) {
        $dir = Find-Dir -Root $download.Path -Relative $family.Dir
        $filter = if ($family.Js) { '*.js' } else { '*.svg' }
        $files = Get-ChildItem -Path $dir -Filter $filter -File -Recurse:([bool]$family.Recurse)
        $seen = @{}
        $defs = @{}
        $reserved = @('Equals', 'GetHashCode', 'GetType', 'ToString', 'ReferenceEquals', 'MemberwiseClone', $family.Name, $entry.Class)

        foreach ($file in $files) {
            $base = $file.BaseName
            if ($family.Js) {
                if ($base -eq 'index' -or $base.Contains('.')) { continue } # index.js, index.min.js
                $base = $base -replace 'Icon$', ''
            } elseif ($family.Suffix) {
                if ($base.EndsWith($family.Suffix)) { $base = $base.Substring(0, $base.Length - $family.Suffix.Length) }
                elseif (-not $family.Bare -or ($base -match '-(line|fill|thin|light|bold|duotone)$')) { continue }
            }

            $text = Get-Content -Path $file.FullName -Raw
            $inner = if ($family.Js) { Get-HugeInner -Js $text } else { Get-SvgInner -Svg $text -DropNoneFills:($entry.Class -eq 'Remix') }
            if ($inner) { $defs[(ConvertTo-PascalCase -Name $base -Seen $seen -Reserved $reserved)] = $inner }
        }

        Write-Family -Entry $entry -Family $family -Defs $defs -ProjectDir $projectDir -Note $download.Note
    }

    Copy-License -Name $name -Entry $entry -DownloadPath $download.Path -ProjectDir $projectDir -Note $download.Note
    if (-not $Source -and -not $KeepDownloads) { Remove-Item (Join-Path $tmpRoot $name) -Recurse -Force }
}

if ((Test-Path $tmpRoot) -and -not (Get-ChildItem $tmpRoot)) { Remove-Item $tmpRoot -Force }
Write-Host 'Done.'
