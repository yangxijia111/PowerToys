# fork_sign_assets.ps1 - PowerToys Cuin 打包期代码签名/验证脚本
# =============================================================================
# 两种工作模式（docs/CODE_SIGNING.md §5）：
#   签名模式：对 release 布局（-Stage Payload）或发行三件套（-Stage Installers）
#             执行 signtool Authenticode 签名（含时间戳）。
#   验证模式：-Verify 只检查目标资产签名状态，不签名；发现未签名资产时
#             列出清单并以退出码 2 结束（CI 可据此 gate）。
#
# 凭证方式（二选一）：
#   -CertThumbprint <hex>        使用本机证书存储中的证书（开发者本机场景）
#   -PfxPath <file> -PfxPassword <pwd>  使用 PFX 文件（CI：secret 以 Base64 解出临时文件，
#                                签名后立即删除；密码经环境变量/参数传入，不写入日志）
#
# 安全纪律（docs/CODE_SIGNING.md §5.4）：
#   - 本脚本与仓库中不得包含任何真实证书/私钥/密码；
#   - 未提供任何凭证且非 -Verify 模式时，脚本直接失败（fail-loud），
#     绝不静默跳过——防止"以为签了实际没签"的伪装态；
#   - CI Secret 名称仅示例：FORK_CODE_SIGN_PFX_BASE64 / FORK_CODE_SIGN_PFX_PASSWORD。
#
# 用法示例：
#   签 payload： tools/fork_sign_assets.ps1 -Stage Payload  -Root x64\Release -CertThumbprint ABC...
#   CI 签三件套： tools/fork_sign_assets.ps1 -Stage Installers -Root artifacts -PfxPath $tmp -PfxPassword $env:PWD
#   验证：       tools/fork_sign_assets.ps1 -Stage Installers -Root artifacts -Verify
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Payload', 'Installers')]
    [string]$Stage,

    [Parameter(Mandatory = $true)]
    [string]$Root,

    # 二选一的凭证参数
    [string]$CertThumbprint,
    [string]$PfxPath,
    [string]$PfxPassword,

    [string]$TimestampUrl = 'http://timestamp.digicert.com',

    # 仅验证签名状态，不执行签名
    [switch]$Verify,

    # 排除模式（正则，作用于相对路径），默认排除调试符号与非二进制产物
    [string[]]$ExcludePattern = @('\.pdb$', '\\obj\\', '\\intermediate\\')
)

$ErrorActionPreference = 'Stop'

# ---------- 工具定位 ----------
function Find-SignTool {
    # 按版本从高到低探测 Windows SDK 自带的 signtool（CI 与开发机均具备）
    $binRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $binRoot) {
        $signtool = Get-ChildItem $binRoot -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($signtool) { return $signtool }
    }
    throw "未找到 signtool.exe（请安装 Windows SDK）。搜索根：$binRoot"
}

# ---------- 资产清单 ----------
# Payload 阶段：布局目录内全部可执行资产（安装后落盘内容，详见 docs/CODE_SIGNING.md §3.1/3.3）；
# Installers 阶段：发行三件套（EXE Bootstrapper + perUser/perMachine MSI，§3.2）。
function Get-StageAssets {
    param([string]$Stage, [string]$Root)
    if (-not (Test-Path $Root)) { throw "目录不存在：$Root" }
    if ($Stage -eq 'Payload') {
        $files = Get-ChildItem $Root -Recurse -File | Where-Object {
            $_.Extension -in '.exe', '.dll', '.msix', '.appx'
        }
    }
    else {
        $files = Get-ChildItem $Root -File | Where-Object {
            $_.Extension -in '.exe', '.msi'
        }
    }
    foreach ($f in $files) {
        # [IO.Path]::GetRelativePath 在 .NET Framework(PS5.1) 不存在，手工截取保证兼容
        $rootFull = (Resolve-Path $Root).ProviderPath.TrimEnd('\') + '\'
        $rel = $f.FullName.Substring($rootFull.Length)
        $excluded = $false
        foreach ($p in $ExcludePattern) {
            if ($rel -match $p) { $excluded = $true; break }
        }
        if (-not $excluded) { $f }
    }
}

# ---------- 验证模式 ----------
if ($Verify) {
    $assets = @(Get-StageAssets -Stage $Stage -Root $Root)
    if ($assets.Count -eq 0) { throw "未发现任何待验证资产（Stage=$Stage, Root=$Root）" }
    $unsigned = @($assets | Where-Object {
            (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' })
    foreach ($a in $assets) {
        $sig = Get-AuthenticodeSignature $a.FullName
        $mark = if ($sig.Status -eq 'Valid') { 'OK ' } else { "BAD($($sig.Status))" }
        Write-Host ("{0}  {1}" -f $mark, $a.Name)
    }
    if ($unsigned.Count -gt 0) {
        Write-Host "::error::存在未通过签名验证的资产 $($unsigned.Count)/$($assets.Count) 项（见上）"
        exit 2
    }
    Write-Host "验证通过：$($assets.Count) 项资产全部具有有效签名"
    exit 0
}

# ---------- 签名模式（fail-loud：无凭证即失败） ----------
$usePfx = $false
if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX 文件不存在：$PfxPath" }
    if (-not $PfxPassword) { throw "指定了 -PfxPath 但缺少 -PfxPassword（CI 场景经环境变量传入）" }
    $usePfx = $true
}
elseif ($CertThumbprint) {
    # 本机证书存储模式：确认指纹确实可解析到证书
    $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
        Where-Object Thumbprint -eq $CertThumbprint | Select-Object -First 1
    if (-not $cert) { throw "证书存储中未找到指纹 $CertThumbprint（CurrentUser\My / LocalMachine\My）" }
}
else {
    throw "未提供签名凭证：需要 -CertThumbprint 或 -PfxPath+-PfxPassword。拒绝静默跳过（fail-loud），Unsigned Preview 构建请勿调用本脚本的签名模式。"
}

$signtool = Find-SignTool
$assets = @(Get-StageAssets -Stage $Stage -Root $Root)
if ($assets.Count -eq 0) { throw "未发现任何待签名资产（Stage=$Stage, Root=$Root）" }
Write-Host "signtool: $signtool"
Write-Host "待签名资产 $($assets.Count) 项："
$assets | ForEach-Object { Write-Host "  $($_.FullName)" }

foreach ($f in $assets) {
    # 参数经数组传递，避免密码进入命令行/日志
    $args = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/tr', $TimestampUrl)
    if ($usePfx) {
        $args += @('/f', $PfxPath, '/p', $PfxPassword)
    }
    else {
        $args += @('/sha1', $CertThumbprint)
    }
    $args += $f.FullName
    & $signtool @args
    if ($LASTEXITCODE -ne 0) { throw "signtool 签名失败（exit $LASTEXITCODE）：$($f.FullName)" }
}

# 签名后立即复核，防止半签状态（部分文件失败被吞掉）
$bad = @($assets | Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' })
if ($bad.Count -gt 0) {
    Write-Host "::error::以下资产签名后验证未通过："
    $bad | ForEach-Object { Write-Host "  $($_.FullName)" }
    exit 3
}
Write-Host "签名完成并复核通过：$($assets.Count) 项"
