using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace jmsTools
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // 添加调试日志
                File.WriteAllText("debug.log", $"Application starting at {DateTime.Now}" + Environment.NewLine);
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                File.AppendAllText("debug.log", $"Creating Form1 at {DateTime.Now}" + Environment.NewLine);
                
                Form1 form = new Form1();
                
                File.AppendAllText("debug.log", $"Form1 created at {DateTime.Now}" + Environment.NewLine);
                
                // 确保窗口可见
                form.Visible = true;
                form.ShowInTaskbar = true;
                form.WindowState = FormWindowState.Normal;
                
                File.AppendAllText("debug.log", $"Starting Application.Run at {DateTime.Now}" + Environment.NewLine);
                
                Application.Run(form);
                
                File.AppendAllText("debug.log", $"Application.Run completed at {DateTime.Now}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"Error at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                File.AppendAllText("error.log", ex.StackTrace + Environment.NewLine);
                throw;
            }
        }
    }
}
