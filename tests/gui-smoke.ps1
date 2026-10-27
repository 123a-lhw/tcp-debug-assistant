<#
.SYNOPSIS
    界面端到端冒烟测试：启动 exe，用 UI 自动化点击「连接」和「发送」，
    验证真实 socket 收得到数据，并截图。

.DESCRIPTION
    依赖 .NET Framework 自带的 UIAutomation。
    测试会临时删除 %APPDATA%\TcpDebugAssistant\settings.ini，
    让程序以默认参数（127.0.0.1:8000）启动。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tests\gui-smoke.ps1
#>
[CmdletBinding()]
param(
    [int]$Port = 8000
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'bin\TcpDebugAssistant.exe'
$shot = Join-Path $root 'bin\gui-smoke.png'
if (-not (Test-Path $exe)) { throw "找不到 $exe，请先运行 build.ps1" }

$settingsFile = Join-Path (Join-Path $env:APPDATA 'TcpDebugAssistant') 'settings.ini'
if (Test-Path $settingsFile) { Remove-Item $settingsFile -Force }

$script:failures = New-Object System.Collections.ArrayList
function Step([string]$name, [scriptblock]$body) {
    try {
        & $body
        Write-Host "[通过] $name"
    } catch {
        [void]$script:failures.Add("$name：$($_.Exception.Message)")
        Write-Host "[失败] $name —— $($_.Exception.Message)"
    }
}

function Find-Element($parent, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-Button($window, [string]$name) {
    $element = Find-Element $window $name
    if ($null -eq $element) { throw "找不到按钮 [$name]" }
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $Port)
$listener.Start(4)
$proc = $null
$client = $null

try {
    Step '启动程序' {
        # 必须用 UseShellExecute=false 启动，本环境下 ShellExecute 会卡住
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $exe
        $psi.WorkingDirectory = (Split-Path $exe)
        $psi.UseShellExecute = $false
        $script:proc = [System.Diagnostics.Process]::Start($psi)
        Start-Sleep -Milliseconds 2500
        $script:proc.Refresh()
        if ($script:proc.HasExited) { throw "进程已退出，退出码 $($script:proc.ExitCode)" }
    }

    $window = $null
    Step '找到主窗口' {
        $rootElement = [System.Windows.Automation.AutomationElement]::RootElement
        $pidCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:proc.Id)
        $script:window = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCond)
        if ($null -eq $script:window) { throw 'UI 自动化找不到该进程的窗口' }
        Write-Host "      窗口标题：$($script:window.Current.Name)"
        if ($script:window.Current.Name -ne 'TCP 调试助手') { throw "窗口标题异常：$($script:window.Current.Name)" }
    }

    Step '点击「连接」并建立 TCP 连接' {
        $asyncResult = $listener.BeginAcceptTcpClient($null, $null)
        Invoke-Button $script:window '连接'
        if (-not $asyncResult.AsyncWaitHandle.WaitOne(5000)) { throw '5 秒内没有收到客户端连接' }
        $script:client = $listener.EndAcceptTcpClient($asyncResult)
        Write-Host "      已接受连接：$($script:client.Client.RemoteEndPoint)"
    }

    Step '按钮切换为「断开连接」' {
        $deadline = (Get-Date).AddSeconds(6)
        while ((Get-Date) -lt $deadline) {
            $script:proc.Refresh()
            if ($script:proc.HasExited) { throw "程序已退出，退出码 $($script:proc.ExitCode)" }
            if ($null -ne (Find-Element $script:window '断开连接')) { return }
            Start-Sleep -Milliseconds 100
        }
        $names = @()
        foreach ($child in $script:window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($child.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) {
                $names += $child.Current.Name
            }
        }
        throw "按钮没有切换成「断开连接」，当前按钮：$($names -join ' | ')"
    }

    Step '点击「发送」，对端收到默认内容' {
        $stream = $script:client.GetStream()
        $stream.ReadTimeout = 5000
        $buffer = New-Object byte[] 256
        Invoke-Button $script:window '发送 (Ctrl+Enter)'
        $read = $stream.Read($buffer, 0, $buffer.Length)
        $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        Write-Host "      收到 $read 字节：$text"
        if ($text -ne 'hello tcp') { throw "内容不符，实际：$text" }
    }

    Step '接收区出现发送回显' {
        # 只读多行 TextBox 在 UIA 里是 Document/Edit 混用，而且刷新有 60ms 批量延迟，
        # 所以这里遍历所有元素、同时尝试 ValuePattern 与 TextPattern，并给 3 秒宽限。
        $deadline = (Get-Date).AddSeconds(3)
        $dump = New-Object System.Collections.ArrayList
        while ((Get-Date) -lt $deadline) {
            $elements = $script:window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($element in $elements) {
                $value = ''
                $valuePattern = $null
                if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$valuePattern)) {
                    $value = $valuePattern.Current.Value
                } elseif ($element.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern, [ref]$valuePattern)) {
                    $value = $valuePattern.DocumentRange.GetText(4000)
                }
                if ([string]::IsNullOrEmpty($value)) { continue }
                if ($value -like '*hello tcp*' -and $value -like '*→*') { return }
                if ($value -like '*hello tcp*') { return }
                if ($value.Length -gt 2 -and -not $dump.Contains($value)) { [void]$dump.Add($value) }
            }
            Start-Sleep -Milliseconds 150
        }
        throw "接收区里没有看到发送回显；界面上的文本有：$($dump -join ' / ')"
    }
    Step '截图' {
        (New-Object -ComObject WScript.Shell).AppActivate($script:proc.Id) | Out-Null
        Start-Sleep -Milliseconds 800
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bitmap.Dispose()
        Write-Host "      截图已保存：$shot"
    }
}
finally {
    if ($null -ne $client) { $client.Close() }
    $listener.Stop()
    if ($null -ne $proc -and -not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 800
        $proc.Refresh()
        if (-not $proc.HasExited) { $proc.Kill() }
    }
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host "界面冒烟测试失败 $($script:failures.Count) 项："
    foreach ($failure in $script:failures) { Write-Host "  $failure" }
    exit 1
}
Write-Host '界面冒烟测试全部通过。'
exit 0
