# 读取文件内容为数组
$lines = Get-Content -Path Form1.cs

# 找到第一个AnalyzeTrend方法的结束位置
$methodEndLine = -1
$openBraces = 0
$methodFound = $false

for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    
    # 标记方法开始
    if ($line.Contains("private string AnalyzeTrend")) {
        $methodFound = $true
    }
    
    # 计算大括号
    if ($methodFound) {
        $openBraces += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
        $openBraces -= ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        
        # 方法结束
        if ($openBraces -eq 0) {
            $methodEndLine = $i
            break
        }
    }
}

if ($methodEndLine -eq -1) {
    Write-Host "Method not found or could not determine end"
    exit 1
}

Write-Host "Found method end at line $methodEndLine"

# 找到最后一个方法的开始位置
$lastMethodStart = -1
for ($i = $lines.Length - 1; $i -gt $methodEndLine; $i--) {
    if ($lines[$i].Contains("private ")) {
        $lastMethodStart = $i
        break
    }
}

if ($lastMethodStart -eq -1) {
    Write-Host "Could not find start of last method"
    exit 1
}

Write-Host "Found last method start at line $lastMethodStart"

# 构建新文件内容
$newLines = @()

# 添加第一个方法之前和第一个方法的内容
for ($i = 0; $i -le $methodEndLine; $i++) {
    $newLines += $lines[$i]
}

# 添加最后一个方法到文件结束的内容
for ($i = $lastMethodStart; $i -lt $lines.Length; $i++) {
    $newLines += $lines[$i]
}

# 写入修复后的文件
$newLines | Set-Content -Path Form1.cs

Write-Host "File fixed successfully"
