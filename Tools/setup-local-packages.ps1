# 还原 LocalPackages/ 下未入库的 UPM 本地依赖包。
# 用法（PowerShell，需在项目根目录执行）：
#   pwsh ./Tools/setup-local-packages.ps1
#
# 背景：com.github.homuler.mediapipe-0.14.4.tgz 约 237MB，超过 GitHub 单文件
# 100MB 限制，已在 .gitignore 中忽略；Packages/manifest.json 通过
# "file:../LocalPackages/..." 引用它，缺文件会导致 Unity 打开项目时解析包失败。
# 若本机已有该包（Library/PackageCache 已缓存），仍建议执行一次以保证 UPM 的
# file: 引用可解析。

$ErrorActionPreference = 'Stop'

$PackageVersion = '0.14.4'
$PackageName    = "com.github.homuler.mediapipe-$PackageVersion.tgz"
$DownloadUrl    = "https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v$PackageVersion/$PackageName"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$TargetDir   = Join-Path $ProjectRoot 'LocalPackages'
$TargetPath  = Join-Path $TargetDir $PackageName

if (Test-Path -LiteralPath $TargetPath) {
    $sizeMb = [math]::Round((Get-Item -LiteralPath $TargetPath).Length / 1MB, 2)
    Write-Host "已存在，跳过下载：$TargetPath（$sizeMb MB）"
    exit 0
}

New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
$tmp = "$TargetPath.downloading"

Write-Host "开始下载：$DownloadUrl"
try {
    # GitHub release 会 302 到对象存储，需允许重定向并禁用进度条以免卡顿
    $oldProgress = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $tmp -UseBasicParsing -TimeoutSec 0
    $ProgressPreference = $oldProgress
} catch {
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
    throw "下载失败，请手动从 $DownloadUrl 下载并放到 LocalPackages/ 目录：$($_.Exception.Message)"
}

Move-Item -LiteralPath $tmp -Destination $TargetPath -Force
$sizeMb = [math]::Round((Get-Item -LiteralPath $TargetPath).Length / 1MB, 2)
Write-Host "完成：$TargetPath（$sizeMb MB）"
