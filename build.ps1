<#
.SYNOPSIS
    编译 TCP 调试助手（使用 .NET Framework 4.8 自带的 C# 编译器，无需安装 .NET SDK）。

.DESCRIPTION
    产物输出到 bin\ 目录：
      TcpTool.Core.dll       核心网络库
      TcpDebugAssistant.exe  界面程序（双击即可运行）
      SelfTest.exe           核心库自检（真实 socket 回环收发）
      GuiHarness.exe         窗体验证（构造并显示主窗口，检查控件树）

.PARAMETER SkipTests
    只编译，不运行自检。

.PARAMETER Run
    编译并自检通过后启动界面程序。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build.ps1
    powershell -ExecutionPolicy Bypass -File build.ps1 -Run
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$Run,
    [switch]$FullTest
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $frameworkDir 'csc.exe'
$outDir = Join-Path $root 'bin'

if (-not (Test-Path $csc)) {
    throw "找不到 C# 编译器：$csc（需要 .NET Framework 4.x）"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 上一次运行残留的窗口进程会锁住 bin 下的 dll/exe，导致 CS0016「正由另一进程使用」
foreach ($name in @('TcpDebugAssistant', 'GuiHarness', 'SelfTest')) {
    Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $_.Kill()
            Write-Host "  已结束残留进程 $name ($($_.Id))"
            Start-Sleep -Milliseconds 200
        } catch {
        }
    }
}

$commonArgs = @(
    '/nologo',
    '/noconfig',
    '/codepage:65001',
    '/optimize+',
    '/warn:2',
    "/r:$frameworkDir\System.dll",
    "/r:$frameworkDir\System.Core.dll"
)

function Get-Sources([string]$relativeDir) {
    $dir = Join-Path $root $relativeDir
    if (-not (Test-Path $dir)) { throw "源码目录不存在：$dir" }
    $files = @(Get-ChildItem -Path $dir -Filter *.cs -File | Sort-Object Name)
    if ($files.Count -eq 0) { throw "目录中没有 .cs 文件：$dir" }
    # 返回 1 个元素时会被解包成标量，调用处必须再用 @() 包一层
    return [string[]]@($files | ForEach-Object { $_.FullName })
}

Write-Host '[1/4] 编译核心库 TcpTool.Core.dll …'
$coreSources = @(Get-Sources 'src\Core')
& $csc @commonArgs '/target:library' "/out:$outDir\TcpTool.Core.dll" @coreSources
if ($LASTEXITCODE -ne 0) { throw '核心库编译失败' }

Write-Host '[2/4] 编译界面 TcpDebugAssistant.exe …'
$appSources = @(Get-Sources 'src\App')
& $csc @commonArgs '/target:winexe' `
    "/out:$outDir\TcpDebugAssistant.exe" `
    "/win32manifest:$root\src\App\app.manifest" `
    "/r:$frameworkDir\System.Drawing.dll" `
    "/r:$frameworkDir\System.Windows.Forms.dll" `
    "/r:$outDir\TcpTool.Core.dll" `
    @appSources
if ($LASTEXITCODE -ne 0) { throw '界面编译失败' }

Write-Host '[3/4] 编译核心自检 SelfTest.exe …'
$testSources = @(Get-Sources 'tests\core')
& $csc @commonArgs '/target:exe' "/out:$outDir\SelfTest.exe" "/r:$outDir\TcpTool.Core.dll" @testSources
if ($LASTEXITCODE -ne 0) { throw '自检程序编译失败' }

Write-Host '[4/4] 编译窗体验证 GuiHarness.exe …'
$guiSources = @(Get-Sources 'tests\gui')
& $csc @commonArgs '/target:exe' `
    "/out:$outDir\GuiHarness.exe" `
    "/r:$frameworkDir\System.Drawing.dll" `
    "/r:$frameworkDir\System.Windows.Forms.dll" `
    "/r:$outDir\TcpTool.Core.dll" `
    "/r:$outDir\TcpDebugAssistant.exe" `
    @guiSources
if ($LASTEXITCODE -ne 0) { throw '窗体验证程序编译失败' }

if (-not $SkipTests) {
    Write-Host ''
    Write-Host '运行核心自检 …'
    & "$outDir\SelfTest.exe" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw '核心自检未通过' }

    Write-Host ''
    Write-Host '运行窗体验证 …'
    & "$outDir\GuiHarness.exe" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw '窗体验证未通过' }

    if ($FullTest) {
        Write-Host ''
        Write-Host '运行客户端模式交互流程 …'
        & "$outDir\GuiHarness.exe" '--interactive' | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '客户端模式交互流程未通过' }

        Write-Host ''
        Write-Host '运行服务端模式交互流程 …'
        & "$outDir\GuiHarness.exe" '--server' | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '服务端模式交互流程未通过' }

        Write-Host ''
        Write-Host '运行端到端冒烟（UI 自动化 + 截图）…'
        & (Join-Path $root 'tests\gui-smoke.ps1') | Out-Host
        if ($LASTEXITCODE -ne 0) { throw '端到端冒烟未通过' }
    }
}

Write-Host ''
Write-Host "构建完成：$outDir\TcpDebugAssistant.exe"

if ($Run) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Join-Path $outDir 'TcpDebugAssistant.exe'
    $psi.WorkingDirectory = $outDir
    $psi.UseShellExecute = $false
    [void][System.Diagnostics.Process]::Start($psi)
}
