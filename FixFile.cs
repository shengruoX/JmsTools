using System;
using System.IO;
using System.Text;

class FixFile {
    static void Main() {
        string filePath = "Form1.cs";
        string content = File.ReadAllText(filePath);
        
        // Find the first AnalyzeTrend method
        int methodStart = content.IndexOf("private string AnalyzeTrend");
        if (methodStart == -1) {
            Console.WriteLine("Method not found");
            return;
        }
        
        // Find the end of the first AnalyzeTrend method
        int openBraces = 0;
        int methodEnd = -1;
        for (int i = methodStart; i < content.Length; i++) {
            if (content[i] == '{') {
                openBraces++;
            } else if (content[i] == '}') {
                openBraces--;
                if (openBraces == 0) {
                    methodEnd = i + 1;
                    break;
                }
            }
        }
        
        if (methodEnd == -1) {
            Console.WriteLine("Could not find end of method");
            return;
        }
        
        // Find the end of the class
        int classEnd = content.LastIndexOf("}");
        if (classEnd == -1) {
            Console.WriteLine("Could not find end of class");
            return;
        }
        
        // Find the start of the last method after AnalyzeTrend
        int lastMethodStart = content.LastIndexOf("private", methodEnd, classEnd - methodEnd);
        if (lastMethodStart == -1) {
            Console.WriteLine("Could not find start of last method");
            return;
        }
        
        // Build new content
        StringBuilder newContent = new StringBuilder();
        newContent.Append(content.Substring(0, methodEnd));
        newContent.Append(content.Substring(lastMethodStart));
        
        // Write fixed file
        File.WriteAllText(filePath, newContent.ToString());
        Console.WriteLine("File fixed successfully");
    }
}
