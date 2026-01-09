# 读取文件内容
$content = Get-Content -Path Form1.cs -Raw

# 正则表达式匹配AnalyzeTrend方法
$methodRegex = '(private string AnalyzeTrend\([^)]*\)\s*\{[\s\S]*?^\s*\})'

# 查找所有匹配的方法
$matches = [regex]::Matches($content, $methodRegex, [System.Text.RegularExpressions.RegexOptions]::Multiline)

if ($matches.Count -eq 0) {
    Write-Host "未找到AnalyzeTrend方法"
    exit 1
}

# 只保留第一个方法
$firstMethod = $matches[0].Value

# 找到所有方法的起始和结束位置
$methodStarts = @()
$methodEnds = @()
foreach ($match in $matches) {
    $methodStarts += $match.Index
    $methodEnds += $match.Index + $match.Length
}

# 构建新文件内容
$newContent = ""

# 添加第一个方法之前的内容
$newContent += $content.Substring(0, $methodStarts[0])

# 添加第一个方法
$newContent += $firstMethod

# 添加最后一个方法之后的内容
$newContent += $content.Substring($methodEnds[-1])

# 写入修复后的文件
Set-Content -Path Form1.cs -Value $newContent

Write-Host "已成功修复重复的AnalyzeTrend方法"
