using System;
using System.IO;
using System.Text.RegularExpressions;

class FixForm1
{
    static void Main()
    {
        // 读取原始文件内容
        string filePath = "e:\\事件合约开源\\JmsTools\\Form1.cs";
        string content = File.ReadAllText(filePath);

        // 找到AnalyzeTrend方法的开始位置
        int methodStartIndex = content.IndexOf("private string AnalyzeTrend");
        if (methodStartIndex == -1)
        {
            Console.WriteLine("未找到AnalyzeTrend方法");
            return;
        }

        // 找到方法的结束位置（第一个闭合的大括号）
        int braceCount = 0;
        int methodEndIndex = -1;
        for (int i = methodStartIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                braceCount++;
            }
            else if (content[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    methodEndIndex = i + 1; // 包括闭合大括号
                    break;
                }
            }
        }

        if (methodEndIndex == -1)
        {
            Console.WriteLine("未找到AnalyzeTrend方法的结束位置");
            return;
        }

        // 提取优化后的AnalyzeTrend方法内容
        string optimizedMethod = content.Substring(methodStartIndex, methodEndIndex - methodStartIndex);

        // 找到方法结束后的重复代码开始位置
        int duplicateStartIndex = methodEndIndex;
        while (duplicateStartIndex < content.Length && (char.IsWhiteSpace(content[duplicateStartIndex]) || content[duplicateStartIndex] == '\n' || content[duplicateStartIndex] == '\r'))
        {
            duplicateStartIndex++;
        }

        // 检查是否存在重复代码
        if (duplicateStartIndex < content.Length && content.Substring(duplicateStartIndex, Math.Min(30, content.Length - duplicateStartIndex)).Contains("初始化各指标得分"))
        {
            // 找到重复代码的结束位置（直到文件结束或下一个方法开始）
            int duplicateEndIndex = content.Length;
            
            // 查找下一个方法的开始位置，作为重复代码的结束位置
            int nextMethodIndex = content.IndexOf("private", duplicateStartIndex + 1);
            if (nextMethodIndex != -1)
            {
                duplicateEndIndex = nextMethodIndex;
            }

            // 构建修复后的内容：文件开头 + 优化后的方法 + 重复代码之后的内容
            string fixedContent = content.Substring(0, methodEndIndex) + content.Substring(duplicateEndIndex);
            
            // 写入修复后的内容
            File.WriteAllText(filePath, fixedContent);
            Console.WriteLine("修复成功！");
        }
        else
        {
            Console.WriteLine("未找到重复代码，文件可能已经修复");
        }
    }
}