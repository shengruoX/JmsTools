# 读取文件内容
$filePath = "e:\事件合约开源\JmsTools\Form1.cs"
$content = Get-Content -Path $filePath -Raw

# 找到AnalyzeTrend方法的开始位置
$methodStartPattern = "private string AnalyzeTrend"
$methodStartIndex = $content.IndexOf($methodStartPattern)
if ($methodStartIndex -eq -1) {
    Write-Host "未找到AnalyzeTrend方法"
    exit
}

# 找到方法的结束位置（第一个闭合的大括号）
$braceCount = 0
$methodEndIndex = -1
for ($i = $methodStartIndex; $i -lt $content.Length; $i++) {
    if ($content[$i] -eq '{') {
        $braceCount++
    } elseif ($content[$i] -eq '}') {
        $braceCount--
        if ($braceCount -eq 0) {
            $methodEndIndex = $i + 1  # 包括闭合大括号
            break
        }
    }
}

if ($methodEndIndex -eq -1) {
    Write-Host "未找到AnalyzeTrend方法的结束位置"
    exit
}

# 找到方法结束后的重复代码开始位置
$duplicateStartIndex = $methodEndIndex
while ($duplicateStartIndex -lt $content.Length -and [char]::IsWhiteSpace($content[$duplicateStartIndex])) {
    $duplicateStartIndex++
}

# 检查是否存在重复代码
$checkLength = [Math]::Min(30, $content.Length - $duplicateStartIndex)
if ($duplicateStartIndex -lt $content.Length -and $content.Substring($duplicateStartIndex, $checkLength).Contains("初始化各指标得分")) {
    # 找到重复代码的结束位置（直到文件结束或下一个方法开始）
    $duplicateEndIndex = $content.Length
    
    # 查找下一个方法的开始位置，作为重复代码的结束位置
    $nextMethodIndex = $content.IndexOf("private", $duplicateStartIndex + 1)
    if ($nextMethodIndex -ne -1) {
        $duplicateEndIndex = $nextMethodIndex
    }

    # 构建修复后的内容：文件开头 + 优化后的方法 + 重复代码之后的内容
    $fixedContent = $content.Substring(0, $methodEndIndex) + $content.Substring($duplicateEndIndex)
    
    # 写入修复后的内容
    Set-Content -Path $filePath -Value $fixedContent -NoNewline
    Write-Host "修复成功！"
} else {
    Write-Host "未找到重复代码，文件可能已经修复"
}