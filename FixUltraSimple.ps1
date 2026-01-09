# 读取文件内容
$content = [IO.File]::ReadAllText("Form1.cs")

# 找到AnalyzeTrend方法的开始
$methodStart = $content.IndexOf("private string AnalyzeTrend")

# 找到第一个方法的结束
$methodEnd = $content.IndexOf("}", $methodStart + 200) + 1

# 找到下一个方法的开始
$nextMethod = $content.IndexOf("private ", $methodEnd)

# 如果找不到下一个方法，说明只有一个方法，无需修复
if ($nextMethod -eq -1) {
    Write-Host "Only one method found"
    exit 0
}

# 构建新内容
$newContent = $content.Substring(0, $methodEnd) + $content.Substring($nextMethod)

# 写入文件
[IO.File]::WriteAllText("Form1.cs", $newContent)

Write-Host "Fixed successfully"
