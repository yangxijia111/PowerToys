[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$monacoWxsFile,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$platform,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$nugetHeatPath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($platform -eq "x64") {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x64"
} else {
    $HeatPath = Join-Path $nugetHeatPath "tools\net472\x86"
}

# Validate heat.exe exists at the resolved path; fail fast if not found.
$heatExe = Join-Path $HeatPath "heat.exe"
if (-not (Test-Path $heatExe)) {
    Write-Error "heat.exe not found at '$heatExe'. Ensure the WixToolset.Heat package (5.0.2) is restored under '$nugetHeatPath'."
    exit 1
}

$SourceDir = Join-Path $scriptDir "..\..\src\Monaco\monacoSRC"  # Now relative to script location
$OutputFile = Join-Path $scriptDir "MonacoSRC.wxs"
$ComponentGroup = "MonacoSRCHeatGenerated"
$DirectoryRef = "MonacoPreviewHandlerMonacoSRCFolder"
$Variable = "var.MonacoSRCHarvestPath"

# [fork-identity] 组件 GUID 稳定化（与 generateAllFileComponents.ps1 同一策略，详见其头部注释）：
# heat -gg 每次运行生成随机 GUID，违反 MSI 组件规则且导致 Bootstrapper 升级丢文件。
# harvest 后在后处理中把每个 cmp 组件的 GUID 替换为稳定值（冻结表优先，UUIDv5 派生兜底）。
$script:frozenComponentGuids = Import-PowerShellDataFile "$scriptDir\componentGuidMap.psd1"
$script:componentGuidNamespace = [guid]'6f9b7f6e-2c3a-5d44-9a41-1f2a3b4c5d6e'

Function Get-StableComponentGuid() {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $True, Position = 1)]
        [string]$ComponentId
    )
    if ($script:frozenComponentGuids.ContainsKey($ComponentId)) {
        return $script:frozenComponentGuids[$ComponentId]
    }
    $nsBytes = $script:componentGuidNamespace.ToByteArray()
    $nameBytes = [Text.Encoding]::UTF8.GetBytes($ComponentId)
    $sha1 = [Security.Cryptography.SHA1]::Create()
    $hash = $sha1.ComputeHash($nsBytes + $nameBytes)
    $sha1.Dispose()
    $guidBytes = $hash[0..15]
    $guidBytes[7] = ($guidBytes[7] -band 0x0F) -bor 0x50   # version 5
    $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80   # variant RFC 4122
    return ([guid][byte[]]$guidBytes).ToString().ToUpper()
}

& $heatExe dir "$SourceDir" -out "$OutputFile" -cg "$ComponentGroup" -dr "$DirectoryRef" -var "$Variable" -gg -srd -nologo

$fileWxs = Get-Content $monacoWxsFile;

$fileWxs = $fileWxs -replace " KeyPath=`"yes`" ", " "

$newFileContent = ""

$componentId = "error"
$directories = @()

$fileWxs | ForEach-Object {
    $line = $_;
    if ($line -match "<Wix xmlns=`".*`">") {
        $line +=
@"
`r`n
    <?include `$(sys.CURRENTDIR)\Common.wxi?>`r`n
"@
    }
    if ($line -match "<Component Id=`"(.*)`" Directory") {
        $componentId = $matches[1]
        # 把 heat 随机生成的 GUID 替换为跨构建稳定值
        if ($line -match 'Guid="\{[0-9A-Fa-f-]+\}"') {
            $line = $line -replace 'Guid="\{[0-9A-Fa-f-]+\}"', "Guid=`"$(Get-StableComponentGuid -ComponentId $componentId)`""
        }
    }
    if ($line -match "<Directory Id=`"(.*)`" Name=`".*`" />") {
        $directories += $matches[1]
    }
    if ($line -match "</Component>") {
        $line =
@"
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
                </RegistryKey>
            </Component>
"@
    }

    $newFileContent += $line + "`r`n";
}

$removeFolderEntries =
@"
`r`n            <Component Id="RemoveMonacoSRCFolders" Guid="$(Get-StableComponentGuid -ComponentId 'RemoveMonacoSRCFolders')" Directory="MonacoPreviewHandlerMonacoSRCFolder" >
                <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                    <RegistryValue Type="string" Name="RemoveMonacoSRCFolders" Value="" KeyPath="yes"/>
                </RegistryKey>`r`n
"@

$directories | ForEach-Object {

    $removeFolderEntries +=
@"
                <RemoveFolder Id="Remove$($_)" Directory="$($_)" On="uninstall"/>

"@
}

$removeFolderEntries +=
@"
            </Component>
"@



$newFileContent = $newFileContent -replace "\s+(</ComponentGroup>)", "$removeFolderEntries`r`n        </ComponentGroup>"

Set-Content -Path $monacoWxsFile -Value $newFileContent