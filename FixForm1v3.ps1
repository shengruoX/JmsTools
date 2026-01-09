# 读取文件内容，按行读取
$filePath = "Form1.cs"
$lines = Get-Content -Path $filePath

# 找到第一个AnalyzeTrend方法的结束位置
$methodEndLine = -1
$braceCount = 0
$inMethod = $false

for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    
    # 检测方法开始
    if ($line.Contains("private string AnalyzeTrend")) {
        $inMethod = $true
    }
    
    if ($inMethod) {
        # 统计大括号
        foreach ($char in $line.ToCharArray()) {
            if ($char -eq '{') {
                $braceCount++
            } elseif ($char -eq '}') {
                $braceCount--
                if ($braceCount -eq 0) {
                    $methodEndLine = $i
                    break
                }
            }
        }
        
        if ($methodEndLine -ne -1) {
            break
        }
    }
}

if ($methodEndLine -eq -1) {
    Write-Host "未找到AnalyzeTrend方法的结束位置"
    exit
}

# 只保留到方法结束位置的内容
$fixedLines = $lines[0..$methodEndLine]

# 写入修复后的内容
Set-Content -Path $filePath -Value $fixedLines
Write-Host "修复成功！已删除从第$($methodEndLine + 2)行开始的重复代码"
