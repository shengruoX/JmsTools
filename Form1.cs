using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Util;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc;
using System.Net.Sockets;
using NBitcoin.RPC;
using Solana.Unity.Rpc.Messages;
using Nethereum.Web3;
using System.Numerics;
using System.Globalization;
using jmsTools.Manage;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Text.RegularExpressions;
using static NBitcoin.RPC.SignRawTransactionRequest;
using System.Diagnostics;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Newtonsoft.Json.Linq;
using Timer = System.Windows.Forms.Timer;

namespace jmsTools
{
    public partial class Form1 : Form
    {
        // 存储从文件读取的RPC节点列表
        private readonly string[] EthrpcNodes;

        // 存储从文件读取的RPC节点列表
        private readonly string[] BscrpcNodes;
        // 助记词单词表
        private readonly string[] wordList;
        // 已经检查过的地址集合
        private readonly HashSet<string> seenAddresses = new HashSet<string>();
        // 程序启动时间
        private DateTime startTime;
        // 查询计数
        private int queryCount = 0;

        private bool _isRunning = false; // 控制是否继续执行任务

        private readonly SemaphoreSlim _throttle = new SemaphoreSlim(5); // 控制并发度为10

        private string selectedNetwork = "ETH"; // 默认选中的网络

        private readonly object _lockObject = new object();//线程锁

        private int threadCount = 5;//线程数量

        private string prefix = "";
        private string suffix = "";

        // 标志变量，用于指示是否已经找到匹配
        private volatile bool foundMatch = false;

        //示例（靓号生成功能）ETH地址
        private string originalEthAddress = "0x816e4a1589e363720c15c54dfd2efd16f6377070"; // 原始ETH地址  用于靓号生成功能演示
        private string currentEthAddress; // 当前ETH地址

        private int remainingSeconds;

        private string predictedTrend; // 综合趋势方向（涨或跌）

        private double initialPrice; // 用于存储初始价格
        // 分析结果保存文件路径
        private const string AnalysisResultsFile = "analysis_results.txt";
        // 预测历史保存文件路径
        private const string PredictionHistoryFile = "prediction_history.txt";
        // 用于定期检查预测到期的定时器
        private Timer predictionCheckerTimer;

        public Form1()
        {
            try
            {
                InitializeComponent();
                
                // 初始化预测检查定时器
                InitializePredictionCheckerTimer();
                
                // 确保所有UI组件可见
                this.Visible = true;
                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
                
                // 添加调试日志
                File.AppendAllText("debug.log", $"InitializeComponent completed at {DateTime.Now}" + Environment.NewLine);

                // 加载RPC节点列表和助记词单词表
                if (File.Exists("Ethrpc_nodes.txt"))
                {
                    EthrpcNodes = File.ReadAllLines("Ethrpc_nodes.txt");
                    File.AppendAllText("debug.log", $"Loaded Ethrpc_nodes.txt at {DateTime.Now}" + Environment.NewLine);
                }
                else
                {
                    EthrpcNodes = new string[0];
                    File.AppendAllText("debug.log", $"Ethrpc_nodes.txt not found at {DateTime.Now}" + Environment.NewLine);
                }
                
                if (File.Exists("Bscrpc_nodes.txt"))
                {
                    BscrpcNodes = File.ReadAllLines("Bscrpc_nodes.txt");
                    File.AppendAllText("debug.log", $"Loaded Bscrpc_nodes.txt at {DateTime.Now}" + Environment.NewLine);
                }
                else
                {
                    BscrpcNodes = new string[0];
                    File.AppendAllText("debug.log", $"Bscrpc_nodes.txt not found at {DateTime.Now}" + Environment.NewLine);
                }
                
                if (File.Exists("bip39.txt"))
                {
                    wordList = File.ReadAllLines("bip39.txt");
                    File.AppendAllText("debug.log", $"Loaded bip39.txt at {DateTime.Now}" + Environment.NewLine);
                }
                else
                {
                    wordList = new string[0];
                    File.AppendAllText("debug.log", $"bip39.txt not found at {DateTime.Now}" + Environment.NewLine);
                }

                // 初始化currentEthAddress为originalEthAddress
                currentEthAddress = originalEthAddress;
                UpdateRichTextBox();
                File.AppendAllText("debug.log", $"Updated RichTextBox at {DateTime.Now}" + Environment.NewLine);

                InitializeCountdownTimer();
                InitializePredictionCheckerTimer(); // 初始化预测检查定时器
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"Error in Form1 constructor at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                File.AppendAllText("error.log", ex.StackTrace + Environment.NewLine);
                throw;
            }
        }
        
        /// <summary>
        /// 加载预测历史到列表框
        /// </summary>
        private void LoadPredictionHistory()
        {
            try
            {
                listBoxPredictionHistory.Items.Clear();
                
                if (!File.Exists(PredictionHistoryFile))
                {
                    listBoxPredictionHistory.Items.Add("无预测历史记录");
                    return;
                }
                
                var lines = File.ReadAllLines(PredictionHistoryFile);
                
                if (lines.Length == 0)
                {
                    listBoxPredictionHistory.Items.Add("无预测历史记录");
                    return;
                }
                
                // 倒序显示，最新的预测在最上面
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 7)
                        {
                            // 检查记录是否已完成（结束价格和结果是否存在）
                            if (!string.IsNullOrWhiteSpace(parts[5]) && !string.IsNullOrWhiteSpace(parts[6]))
                            {
                                // 已完成的记录，显示完整信息
                                listBoxPredictionHistory.Items.Add($"{parts[0]} | {parts[1]} | {parts[2]} | 预测: {parts[3]} | 初始价: {parts[4]} | 结束价: {parts[5]} | 结果: {parts[6]}");
                            }
                            else
                            {
                                // 未完成的记录，只显示基本信息
                                listBoxPredictionHistory.Items.Add($"{parts[0]} | {parts[1]} | {parts[2]} | 预测: {parts[3]} | 初始价: {parts[4]} | 等待结束");
                            }
                        }
                        else if (parts.Length >= 4)
                        {
                            // 旧格式的记录，兼容显示
                            listBoxPredictionHistory.Items.Add($"{parts[0]} | {parts[1]} | {parts[2]} | {parts[3]} | 旧格式记录");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 加载预测历史失败: {ex.Message}\n";
            }
        }
        
        /// <summary>
        /// 更新总体胜率显示
        /// </summary>
        private void UpdateOverallWinRate()
        {
            try
            {
                if (!File.Exists(PredictionHistoryFile))
                {
                    labelOverallWinRate.Text = "0.00%";
                    return;
                }
                
                var lines = File.ReadAllLines(PredictionHistoryFile);
                
                if (lines.Length == 0)
                {
                    labelOverallWinRate.Text = "0.00%";
                    return;
                }
                
                // 解析预测记录
                int totalPredictions = 0;
                int correctPredictions = 0;
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]))
                    {
                        totalPredictions++;
                        
                        // 统计成功次数
                        if (parts[6] == "成功")
                        {
                            correctPredictions++;
                        }
                    }
                }
                
                // 计算胜率
                double winRate = totalPredictions > 0 ? (double)correctPredictions / totalPredictions : 0;
                
                // 更新总体胜率显示
                labelOverallWinRate.Text = $"{(winRate * 100):0.00}% ({correctPredictions}/{totalPredictions})";
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 更新总体胜率失败: {ex.Message}\n";
            }
        }
        
        /// <summary>
        /// 清空预测历史按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClearHistory_Click(object sender, EventArgs e)
        {
            try
            {
                if (MessageBox.Show("确定要清空所有预测历史吗？", "确认清空", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (File.Exists(PredictionHistoryFile))
                    {
                        File.Delete(PredictionHistoryFile);
                    }
                    
                    LoadPredictionHistory();
                    UpdateOverallWinRate();
                    analysis_Log.Text += "📝 预测历史已清空\n";
                }
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 清空预测历史失败: {ex.Message}\n";
            }
        }


        private void UpdateRichTextBox()
        {
            xample_str.Text = currentEthAddress;
            xample_str.SelectAll();
            xample_str.SelectionColor = Color.Black; // 恢复默认颜色

            string prefix = textBoxPrefix.Text;
            int prefixLength = Math.Min(prefix.Length, 6);
            if (prefixLength > 0)
            {
                xample_str.SelectionStart = 2;
                xample_str.SelectionLength = prefixLength;
                xample_str.SelectionColor = Color.Red; // 高亮颜色
            }

            string suffix = textBoxSuffix.Text;
            int suffixLength = suffix.Length;
            if (suffixLength > 0)
            {
                xample_str.SelectionStart = currentEthAddress.Length - suffixLength;
                xample_str.SelectionLength = suffixLength;
                xample_str.SelectionColor = Color.Red; // 高亮颜色
            }
        }


        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ethbtn_start_Click(object sender, EventArgs e)
        {
            startTime = DateTime.Now;
            _isRunning = true;

            LogMessage("\n 开始执行" + selectedNetwork + "主网碰撞.初始化中，请稍等..\n");
            StartProcess(); // 启动处理助记词的进程
        }

        // 开始处理助记词
        private void StartProcess()
        {
            Task.Run(() => ProcessMnemonic()); // 在新任务中运行助记词处理逻辑
        }

        // 处理助记词生成、地址生成和交易查询逻辑
        private async Task ProcessMnemonic()
        {
            while (_isRunning) // 检查_isRunning标志
            {
                await _throttle.WaitAsync(); // 等待信号量

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (!_isRunning) return; // 再次检查_isRunning标志

                        // 根据主网类型生成钱包
                        (string address, string privateKey) walletInfo;

                        switch (selectedNetwork.ToLower())
                        {
                            case "eth":
                            case "bsc":
                                var ethWallet = new Nethereum.HdWallet.Wallet(Wordlist.English, WordCount.Twelve);
                                var ethAccount = ethWallet.GetAccount(0);
                                walletInfo = (
                                    new AddressUtil().ConvertToChecksumAddress(ethAccount.Address),
                                    ethAccount.PrivateKey
                                );
                                break;
                            case "sol":
                                var solWallet = new Solana.Unity.Wallet.Wallet(Solana.Unity.Wallet.Bip39.WordCount.Twelve, Solana.Unity.Wallet.Bip39.WordList.English, "", SeedMode.Bip39);
                                walletInfo = (
                                    solWallet.Account.PublicKey.Key,
                                    solWallet.Account.PrivateKey // Solana私钥通常表示为Base58编码的字符串
                                );
                                break;
                            default:
                                throw new NotSupportedException("不支持的主网类型");
                        }

                        var address = walletInfo.address;
                        var privateKey = walletInfo.privateKey;

                        string[] rpcNodes = new string[1];
                        if (!seenAddresses.Contains(address)) // 如果该地址未被检查过
                        {
                            lock (_lockObject) // 确保线程安全地添加到集合中
                            {
                                seenAddresses.Add(address);
                            }


                            var transactionCount = await BlockManage.GetBalanceInfoByPz(address, selectedNetwork); // 查询余额，比查询交易次数更快，同样针对主网原生代币查询
                            if (transactionCount.HasValue)
                            {
                                // 在日志中包含私钥信息
                                LogMessage($"\n地址: {address} 私钥: {privateKey} 余额:{transactionCount}\n");
                                if (transactionCount > 0)
                                {
                                    // 发送通知逻辑可以在这里实现
                                }
                            }
                            else
                            {
                                // 即使没有交易，也选择记录地址和私钥
                                LogMessage($"\n地址: {address} 私钥: {privateKey} 余额:{0}\n");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录异常信息以便调试
                        //LogMessage($"\n发生错误: {ex.Message}\n");
                    }
                    finally
                    {
                        _throttle.Release(); // 释放信号量
                    }
                });

                if (!_isRunning) break; // 如果停止了，则跳出循环
            }
        }


        /// <summary>
        /// 日志记录-碰撞器
        /// </summary>
        /// <param name="message"></param>

        private void LogMessage(string message)
        {
            if (this.EthtextBoxLog.InvokeRequired)
            {
                EthtextBoxLog.Invoke(new Action<string>(LogMessage), new object[] { message });
            }
            else
            {
                EthtextBoxLog.AppendText(Environment.NewLine + $"\n{message}\n");
                EthlabelRunTime.Text = queryCount.ToString() + "次";
                queryCount++;
            }
        }


        /// <summary>
        /// 日志记录-交易记录
        /// </summary>
        /// <param name="message"></param>

        private void LogMessageTransaction(string message)
        {
            if (this.text_transaction.InvokeRequired)
            {
                text_transaction.Invoke(new Action<string>(LogMessageTransaction), new object[] { message });
            }
            else
            {
                text_transaction.AppendText(Environment.NewLine + $"\n{message}\n");
            }
        }


        /// <summary>
        /// 停止
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ethbtn_stop_Click(object sender, EventArgs e)
        {
            _isRunning = false;
            LogMessage("\n停止处理...\n");
        }


        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            com_Currency.SelectedIndex = 0; // 默认选中第一个项
            com_Cycle.SelectedIndex = 0; // 默认选中第一个项

            comboBoxMainNet.SelectedIndex = 0; // 默认选中第一个项
            selectedNetwork = comboBoxMainNet.SelectedItem.ToString();
            
            // 初始化倒计时标签，确保刚打开软件时不显示不必要的倒计时
            lbl_Countdown.Text = "倒计时: 0 分 0 秒";
            
            // 确保预测检查定时器已经初始化
            if (predictionCheckerTimer == null)
            {
                InitializePredictionCheckerTimer();
            }
            
            // 启动预测检查定时器
            predictionCheckerTimer.Start();
            analysis_Log.Text += $"📋 预测检查定时器已启动，间隔: {predictionCheckerTimer.Interval} 毫秒\n";
            File.AppendAllText("debug.log", $"Prediction checker timer started at {DateTime.Now}, interval: {predictionCheckerTimer.Interval} ms" + Environment.NewLine);
            
            // 加载预测历史并显示总体胜率 - 移到Form1_Load中确保UI组件完全初始化
            LoadPredictionHistory();
            UpdateOverallWinRate();
        }

        /// <summary>
        /// 主网切换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxMainNet_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxMainNet.SelectedItem != null)
            {
                selectedNetwork = comboBoxMainNet.SelectedItem.ToString();
                LogMessage("\n 已切换至" + selectedNetwork + "主网...\n");
            }
        }


        /// <summary>
        /// 单地址查询
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void oneaddress_cx_Click(object sender, EventArgs e)
        {
            // 判断碰撞是否正在运行
            if (_isRunning)
            {
                MessageBox.Show("请先停止碰撞进程再进行单地址查询！", "操作冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(this.oneaddress.Text))
            {
                MessageBox.Show("请输入查询地址！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await SomeMethod(oneaddress.Text, selectedNetwork);
        }


        public static long GetTimeStamp(bool accurateToMilliseconds = false)
        {
            if (accurateToMilliseconds)
            {
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            else
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        /// <summary>
        /// 单地址余额查询
        /// </summary>
        /// <param name="address"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        public async Task SomeMethod(string address, string network)
        {



            long sp = GetTimeStamp(false);
            string url = "";
            switch (network.ToLower())
            {
                case "eth":
                    url = $"https://eth.tokenview.io/api/search/{address}";
                    break;
                case "bsc":
                    url = $"https://bsc.tokenview.io/api/bsc/address/{address}";
                    break;
                default:
                    break;
            }
            var a = await ExampleRequest.ExecuteGetRequestAsync(url);

            switch (network.ToLower())
            {
                case "eth":
                    if (!string.IsNullOrEmpty(a))
                    {
                        var apiResponse = JsonConvert.DeserializeObject<EthApiResponseModel>(a);

                        if (apiResponse.EnMsg == "SUCCESS")
                        {


                            var str = "";
                            int x_ = 80;
                            foreach (var item in apiResponse.Data)
                            {
                                decimal Balance = Convert.ToDecimal(item.Balance);
                                Balance = Math.Round(Balance, 4);

                                str = str + Balance + "" + item.Network + "  ";
                                //this.Controls.Add(newLabel);
                            }

                            BalanceInfo.Text = str;
                        }

                    }
                    break;
                case "bsc":
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// 前缀
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxPrefix_TextChanged(object sender, EventArgs e)
        {
            // 使用正则表达式匹配允许的字符（数字和A到F的大写或小写字母）
            Regex regex = new Regex("^[0-9a-fA-F]*$");
            // 检查文本是否与模式匹配
            if (!regex.IsMatch(textBoxPrefix.Text))
            {
                // 提示用户只允许输入特定字符
                MessageBox.Show("请输入数值(0-9)或A-F之间的字母", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }
            prefix = textBoxPrefix.Text;
            HandleReplacement();
            TextChanged();
        }


        private bool IsAlphaNumeric(string input)
        {
            foreach (char c in input)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 后缀
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxSuffix_TextChanged(object sender, EventArgs e)
        {
            // 使用正则表达式匹配允许的字符（数字和A到F的大写或小写字母）
            Regex regex = new Regex("^[0-9a-fA-F]*$");
            // 检查文本是否与模式匹配
            if (!regex.IsMatch(textBoxSuffix.Text))
            {
                // 提示用户只允许输入特定字符
                MessageBox.Show("请输入数值(0-9)或A-F之间的字母", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }
            suffix = textBoxPrefix.Text;
            HandleReplacement();
            TextChanged();
        }

        /// <summary>
        /// 前后缀输入处理
        /// </summary>
        private void HandleReplacement()
        {
            string prefix = textBoxPrefix.Text;
            string suffix = textBoxSuffix.Text;

            // 验证前缀和后缀是否为字母或数字



            // 从原始地址开始构建新的ETH地址
            string newEthAddress = originalEthAddress;

            // 前缀替换
            int prefixLength = Math.Min(prefix.Length, 6); // 控制最多替换6位
            if (prefixLength > 0)
            {
                newEthAddress = "0x" + prefix.Substring(0, prefixLength) + originalEthAddress.Substring(2 + prefixLength);
            }

            // 后缀替换
            int suffixLength = suffix.Length;
            if (suffixLength > 0 && newEthAddress.Length >= suffixLength)
            {
                newEthAddress = newEthAddress.Substring(0, newEthAddress.Length - suffixLength) + suffix;
            }
            else if (suffixLength > 0)
            {
                MessageBox.Show("后缀长度超过了ETH地址的长度");
                return;
            }

            // 更新当前ETH地址并刷新RichTextBox显示
            currentEthAddress = newEthAddress;
            UpdateRichTextBox();
        }


        /// <summary>
        /// 详情动态
        /// </summary>
        private void TextChanged()
        {
            string prefix = textBoxPrefix.Text;
            string suffix = textBoxSuffix.Text;

            var difficulty = EthAddressDifficultyCalculator.CalculateDifficulty(prefix, suffix);
            complexity_lab.Text = difficulty.complexity.ToString();
            ygtime.Text = difficulty.estimatedTime;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 打开默认浏览器并导航到指定网址
            Process.Start(new ProcessStartInfo("https://bqbot.cn") { UseShellExecute = true });
        }


        // 线程安全的计数器
        private long totalGeneratedCount = 0;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// 地址生成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void generate_btn_Click(object sender, EventArgs e)
        {
            labelResults.Text = "";
            // 清空之前的记录
            labelResults.AppendText("正在生成中，请稍候...");
            // 重置计数器
            Interlocked.Exchange(ref totalGeneratedCount, 0);
            foundMatch = false;
            // 更新界面上的计数器
            UpdateGeneratedCountLabel();

            // 初始化取消令牌
            cancellationTokenSource = new CancellationTokenSource();

            // 启动生成任务
            Task.Run(() => GenerateWallets(cancellationTokenSource.Token));
        }



        private void GenerateWallets(CancellationToken cancellationToken)
        {
            var tasks = new Task[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // 生成钱包地址
                        var ethWallet = new Nethereum.HdWallet.Wallet(Wordlist.English, WordCount.Twelve);
                        var ethAccount = ethWallet.GetAccount(0);
                        var address = ethAccount.Address; // 转为小写

                        // 增加生成计数
                        Interlocked.Increment(ref totalGeneratedCount);

                        // 更新界面上的计数器
                        UpdateGeneratedCountLabel();

                        // 检查是否符合前后缀要求
                        if (IsAddressValid(address))
                        {
                            // 设置标志变量为 true
                            foundMatch = true;
                            var privateKey = ethAccount.PrivateKey;
                            // 更新界面上的结果
                            UpdateResultsLabel($"地址：{address}");
                            UpdateResultsLabel($"私钥：{privateKey}");
                            // 取消所有线程的任务
                            cancellationTokenSource.Cancel();
                        }
                    }
                }, cancellationToken);
            }

            try
            {
                Task.WaitAll(tasks, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 用户停止生成任务
            }
        }


        private void UpdateResultsLabel(string result)
        {
            // 确保在UI线程中更新控件
            if (labelResults.InvokeRequired)
            {
                labelResults.Invoke(new Action<string>(UpdateResultsLabel), result);
            }
            else
            {
                // 将新结果追加到现有内容中，并添加换行符
                labelResults.AppendText(Environment.NewLine + result + Environment.NewLine);
            }
        }


        private bool IsAddressValid(string address)
        {

            // 移除地址中的 "0x" 前缀
            string addressWithoutPrefix = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address.Substring(2) : address;

            // 根据用户选择决定是否区分大小写
            if (!checkBox1.Checked) // 如果未勾选，则不区分大小写
            {
                addressWithoutPrefix = addressWithoutPrefix.ToLower();
                prefix = prefix.ToLower();
                suffix = suffix.ToLower();
            }

            // 检查前缀和后缀
            bool isPrefixMatch = string.IsNullOrEmpty(prefix) || addressWithoutPrefix.StartsWith(prefix);
            bool isSuffixMatch = string.IsNullOrEmpty(suffix) || addressWithoutPrefix.EndsWith(suffix);

            return isPrefixMatch && isSuffixMatch;
        }


        private void UpdateGeneratedCountLabel()
        {
            // 确保在UI线程中更新控件
            if (generated_lab.InvokeRequired)
            {
                generated_lab.Invoke(new Action(UpdateGeneratedCountLabel));
            }
            else
            {
                generated_lab.Text = $"{totalGeneratedCount}";
            }
        }

        private void taskNum_ValueChanged(object sender, EventArgs e)
        {
            threadCount = (int)this.taskNum.Value;
        }

        private void stop_btn_Click(object sender, EventArgs e)
        {
            // 如果任务正在进行，则取消所有线程的任务
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                labelResults.AppendText("任务已手动停止" + Environment.NewLine);
            }
        }

        /// <summary>
        /// 更新倒计时 Label
        /// </summary>
        private void UpdateCountdownLabel(int remainingSeconds)
        {
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            lbl_Countdown.Text = $"倒计时: {minutes} 分 {seconds} 秒";
        }
        
        /// <summary>
        /// 初始化倒计时计时器
        /// </summary>
        private void InitializeCountdownTimer()
        {
            // 初始化计时器属性
            timer1.Interval = 1000; // 设置为1秒触发一次
            // 事件绑定已经在Form1.Designer.cs中自动生成，无需再次绑定
        }
        
        /// <summary>
        /// 初始化预测检查定时器
        /// </summary>
        private void InitializePredictionCheckerTimer()
        {
            // 初始化计时器属性
            predictionCheckerTimer = new Timer();
            predictionCheckerTimer.Tick += PredictionCheckerTimer_Tick;
            // 首次设置定时器，计算下一个最近的到期时间
            UpdatePredictionCheckerTimer();
        }
        
        /// <summary>
        /// 更新预测检查定时器，设置为下一个最近到期时间
        /// </summary>
        private void UpdatePredictionCheckerTimer()
        {
            try
            {
                if (!File.Exists(PredictionHistoryFile))
                {
                    // 如果文件不存在，设置为1分钟后检查
                    predictionCheckerTimer.Interval = 60000;
                    predictionCheckerTimer.Start();
                    return;
                }
                
                var lines = File.ReadAllLines(PredictionHistoryFile);
                if (lines.Length == 0)
                {
                    // 如果没有记录，设置为1分钟后检查
                    predictionCheckerTimer.Interval = 60000;
                    predictionCheckerTimer.Start();
                    return;
                }
                
                DateTime now = DateTime.Now;
                DateTime nextExpiryTime = now.AddDays(1); // 默认设置为明天
                bool hasUpcomingPredictions = false;
                
                // 遍历所有预测记录，找到下一个最近的到期时间
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    var parts = line.Split(',');
                    // 只处理未完成的记录
                    if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[5]) && !string.IsNullOrWhiteSpace(parts[6]))
                        continue;
                    
                    if (parts.Length < 5)
                        continue;
                    
                    try
                    {
                        string dateTimeStr = parts[0];
                        string interval = parts[2];
                        
                        if (string.IsNullOrWhiteSpace(dateTimeStr) || string.IsNullOrWhiteSpace(interval))
                            continue;
                        
                        if (DateTime.TryParse(dateTimeStr, out DateTime predictionTime))
                        {
                            int cycleInSeconds = GetCycleInSecondsFromInterval(interval);
                            DateTime expiryTime = predictionTime.AddSeconds(cycleInSeconds);
                            
                            // 只考虑未来的到期时间
                            if (expiryTime > now)
                            {
                                hasUpcomingPredictions = true;
                                // 找到最近的到期时间
                                if (expiryTime < nextExpiryTime)
                                {
                                    nextExpiryTime = expiryTime;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 忽略单个记录的解析错误
                        File.AppendAllText("error.log", $"Error updating timer: {ex.Message}" + Environment.NewLine);
                    }
                }
                
                if (hasUpcomingPredictions)
                {
                    // 计算到下一个到期时间的毫秒数
                    TimeSpan timeUntilNextExpiry = nextExpiryTime - now;
                    int nextInterval = (int)timeUntilNextExpiry.TotalMilliseconds;
                    
                    // 确保间隔至少为1秒，避免定时器立即触发
                    if (nextInterval < 1000)
                        nextInterval = 1000;
                    
                    // 设置定时器间隔
                    predictionCheckerTimer.Interval = nextInterval;
                    predictionCheckerTimer.Start();
                }
                else
                {
                    // 如果没有即将到期的预测，设置为10分钟后检查
                    predictionCheckerTimer.Interval = 600000;
                    predictionCheckerTimer.Start();
                }
            }
            catch (Exception ex)
            {
                // 如果计算失败，设置为1分钟后检查
                predictionCheckerTimer.Interval = 60000;
                predictionCheckerTimer.Start();
                File.AppendAllText("error.log", $"Error updating timer: {ex.Message}" + Environment.NewLine);
            }
        }
        
        /// <summary>
        /// 预测检查定时器的Tick事件处理函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PredictionCheckerTimer_Tick(object sender, EventArgs e)
        {
            await CheckPredictionsExpiry();
            // 处理完后，更新定时器，计算下一个最近的到期时间
            UpdatePredictionCheckerTimer();
        }
        
        /// <summary>
        /// 检查预测是否到期，并更新结果
        /// </summary>
        private async Task CheckPredictionsExpiry()
        {
            DateTime now = DateTime.Now;
            
            try
            {
                if (!File.Exists(PredictionHistoryFile))
                {
                    return;
                }
                
                var lines = File.ReadAllLines(PredictionHistoryFile);
                if (lines.Length == 0)
                {
                    return;
                }
                
                var updatedLines = lines.ToList();
                bool hasUpdated = false;
                
                // 遍历所有预测记录
                for (int i = 0; i < updatedLines.Count; i++)
                {
                    var line = updatedLines[i];
                    
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    
                    var parts = line.Split(',');
                    // 检查记录是否已完成（结束价格和结果是否存在）
                    if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[5]) && !string.IsNullOrWhiteSpace(parts[6]))
                    {
                        continue; // 已完成的记录跳过
                    }
                    
                    // 确保有足够的字段
                    if (parts.Length < 5)
                    {
                        continue;
                    }
                    
                    try
                    {
                        // 解析记录，添加空检查
                        string dateTimeStr = parts[0];
                        string symbol = parts[1];
                        string interval = parts[2];
                        string predictedTrend = parts[3];
                        
                        if (string.IsNullOrWhiteSpace(dateTimeStr) || string.IsNullOrWhiteSpace(symbol) || 
                            string.IsNullOrWhiteSpace(interval) || string.IsNullOrWhiteSpace(predictedTrend))
                        {
                            continue;
                        }
                        
                        double initialPrice;
                        if (!double.TryParse(parts[4], out initialPrice))
                        {
                            continue;
                        }
                        
                        // 解析预测时间
                        if (DateTime.TryParse(dateTimeStr, out DateTime predictionTime))
                        {
                            // 根据周期计算到期时间
                            int cycleInSeconds = GetCycleInSecondsFromInterval(interval);
                            DateTime expiryTime = predictionTime.AddSeconds(cycleInSeconds);
                            
                            // 只处理已经到期的记录
                            if (now >= expiryTime)
                            {
                                try
                                {
                                    // 获取真实的当前价格
                                    double finalPrice = await GetCurrentPriceFromBinance(symbol);
                                    
                                    // 计算实际趋势
                                    string actualTrend = finalPrice > initialPrice ? "上涨" : (finalPrice < initialPrice ? "下跌" : "平");
                                    
                                    // 判断预测是否成功
                                    string result = (predictedTrend == actualTrend) ? "成功" : "失败";
                                    
                                    // 更新记录
                                    string updatedRecord = $"{parts[0]},{parts[1]},{parts[2]},{parts[3]},{parts[4]},{finalPrice:0.00},{result}";
                                    updatedLines[i] = updatedRecord;
                                    hasUpdated = true;
                                    
                                    // 在日志中显示最终结果
                                    analysis_Log.Text += $"\n==============================================\n";
                                    analysis_Log.Text += $"📊 自动预测结果分析: {symbol} {interval}\n";
                                    analysis_Log.Text += $"📈 初始价格: {initialPrice:0.00}\n";
                                    analysis_Log.Text += $"📉 最终价格: {finalPrice:0.00}\n";
                                    analysis_Log.Text += $"🎯 预测趋势: {predictedTrend}\n";
                                    analysis_Log.Text += $"✅ 实际趋势: {actualTrend}\n";
                                    analysis_Log.Text += $"🏆 预测结果: {result}\n";
                                    analysis_Log.Text += $"==============================================\n\n";
                                }
                                catch (Exception ex)
                                {
                                    // 如果获取价格失败，记录错误但继续处理其他记录
                                    File.AppendAllText("error.log", $"Error getting price for {symbol} at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理单个记录的异常，继续处理其他记录
                        File.AppendAllText("error.log", $"Error processing prediction record at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                    }
                }
                
                // 如果有更新，保存文件并更新UI
                if (hasUpdated)
                {
                    File.WriteAllLines(PredictionHistoryFile, updatedLines);
                    
                    // 更新UI
                    LoadPredictionHistory();
                    UpdateOverallWinRate();
                    CalculateAndDisplayOverallWinRate();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"Error in CheckPredictionsExpiry at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                File.AppendAllText("error.log", ex.StackTrace + Environment.NewLine);
            }
        }
        
        /// <summary>
        /// 根据间隔字符串获取周期秒数
        /// </summary>
        /// <param name="interval">间隔字符串，如"15m", "30m", "1h", "4h"</param>
        /// <returns>返回周期秒数</returns>
        private int GetCycleInSecondsFromInterval(string interval)
        {
            switch (interval.ToLower())
            {
                case "15m": return 10 * 60; // 特殊处理：15m实际代表10分钟，返回600秒
                case "30m": return 30 * 60; // 30分钟 = 1800秒
                case "1h": return 60 * 60; // 1小时 = 3600秒
                case "4h": return 4 * 60 * 60; // 4小时 = 14400秒
                default: return 10 * 60; // 默认10分钟
            }
        }
        
        /// <summary>
        /// 将周期字符串转换为币安API所需的间隔格式
        /// </summary>
        /// <param name="cycle">周期字符串，如"10分钟"</param>
        /// <returns>返回币安API间隔格式，如"15m"</returns>
        private string CycleToInterval(string cycle)
        {
            switch (cycle)
            {
                case "10分钟":
                    return "15m"; // 币安不支持10分钟，用15分钟替代
                case "30分钟":
                    return "30m";
                case "1小时":
                    return "1h";
                case "4小时":
                    return "4h";
                default:
                    throw new ArgumentException("不支持的时间周期");
            }
        }
        
        /// <summary>
        /// 将周期字符串转换为秒数
        /// </summary>
        /// <param name="cycle">周期字符串，如"10分钟"</param>
        /// <returns>返回秒数</returns>
        private int CycleToSeconds(string cycle)
        {
            switch (cycle)
            {
                case "10分钟":
                    return 10 * 60; // 10 分钟 = 600 秒
                case "30分钟":
                    return 30 * 60; // 30 分钟 = 1800 秒
                case "1小时":
                    return 60 * 60; // 1 小时 = 3600 秒
                case "4小时":
                    return 4 * 60 * 60; // 4 小时 = 14400 秒
                default:
                    throw new ArgumentException("无效的周期格式");
            }
        }
        
        /// <summary>
        /// 计时器事件处理函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            // 从Tag中获取剩余秒数
            if (timer1.Tag != null && int.TryParse(timer1.Tag.ToString(), out int remainingSeconds))
            {
                remainingSeconds--;
                UpdateCountdownLabel(remainingSeconds);
                
                if (remainingSeconds <= 0)
                {
                    timer1.Stop();
                    // 倒计时结束，执行最终分析
                    FinalAnalysis();
                }
                else
                {
                    timer1.Tag = remainingSeconds;
                }
            }
        }
        
        /// <summary>
        /// 链接标签点击事件处理函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 这里可以添加链接点击事件处理逻辑
        }
        
        /// <summary>
        /// 倒计时结束后的最终分析
        /// </summary>
        private async void FinalAnalysis()
        {
            try
            {
                // 1. 先处理当前正在分析的记录，即使它还没到期
                await ProcessCurrentPrediction();
                
                // 2. 然后处理所有已经到期的记录
                await CheckPredictionsExpiry();
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 执行最终分析失败: {ex.Message}\n";
                File.AppendAllText("error.log", $"Error in FinalAnalysis at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                File.AppendAllText("error.log", ex.StackTrace + Environment.NewLine);
            }
        }
        
        /// <summary>
        /// 处理当前正在分析的记录，即使它还没到期
        /// </summary>
        private async Task ProcessCurrentPrediction()
        {
            try
            {
                // 获取当前选择的币种和周期
                string selectedCurrency = com_Currency.SelectedItem?.ToString() ?? "BTC/USDT";
                string selectedCycle = com_Cycle.SelectedItem?.ToString() ?? "10分钟";
                string symbol = selectedCurrency.Replace("/", ""); // 将 "BTC/USDT" 转换为 "BTCUSDT"
                string interval = CycleToInterval(selectedCycle); // 将 "10分钟" 转换为 "15m"
                
                analysis_Log.Text += $"📋 开始处理当前预测记录: {symbol} {interval}\n";
                
                // 读取预测历史文件中的所有记录
                if (File.Exists(PredictionHistoryFile))
                {
                    var lines = File.ReadAllLines(PredictionHistoryFile);
                    if (lines.Length > 0)
                    {
                        var updatedLines = lines.ToList();
                        bool hasUpdated = false;
                        
                        // 只处理最新的、与当前分析周期匹配的未完成记录
                        for (int i = updatedLines.Count - 1; i >= 0; i--)
                        {
                            var line = updatedLines[i];
                            var parts = line.Split(',');
                            if (parts.Length >= 6 && string.IsNullOrWhiteSpace(parts[5]))
                            {
                                // 获取记录的币种和周期
                                string recordSymbol = parts[1];
                                string recordInterval = parts[2];
                                
                                // 只有当记录币种和周期与当前分析匹配时才处理
                                if (recordSymbol == symbol && recordInterval == interval)
                                {
                                    // 解析记录
                                    string dateTimeStr = parts[0];
                                    string predictedTrend = parts[3];
                                    double initialPrice = double.Parse(parts[4]);
                                    
                                    // 解析预测时间
                                    if (DateTime.TryParse(dateTimeStr, out DateTime predictionTime))
                                    {
                                        // 获取真实的当前价格
                                        double finalPrice = await GetCurrentPriceFromBinance(symbol);
                                        
                                        // 计算实际趋势
                                        string actualTrend = finalPrice > initialPrice ? "上涨" : (finalPrice < initialPrice ? "下跌" : "平");
                                        
                                        // 判断预测是否成功
                                        string result = (predictedTrend == actualTrend) ? "成功" : "失败";
                                        
                                        // 更新记录
                                        string updatedRecord = $"{parts[0]},{parts[1]},{parts[2]},{parts[3]},{parts[4]},{finalPrice:0.00},{result}";
                                        updatedLines[i] = updatedRecord;
                                        hasUpdated = true;
                                        
                                        // 在日志中显示最终结果
                                        analysis_Log.Text += $"\n==============================================\n";
                                        analysis_Log.Text += $"📊 最终结果分析: {selectedCurrency} {selectedCycle}\n";
                                        analysis_Log.Text += $"📈 初始价格: {initialPrice:0.00}\n";
                                        analysis_Log.Text += $"📉 最终价格: {finalPrice:0.00}\n";
                                        analysis_Log.Text += $"🎯 预测趋势: {predictedTrend}\n";
                                        analysis_Log.Text += $"✅ 实际趋势: {actualTrend}\n";
                                        analysis_Log.Text += $"🏆 预测结果: {result}\n";
                                        analysis_Log.Text += $"==============================================\n\n";
                                        
                                        break; // 只处理最新的一条记录
                                    }
                                }
                            }
                        }
                        
                        // 如果有更新，保存文件并更新UI
                        if (hasUpdated)
                        {
                            File.WriteAllLines(PredictionHistoryFile, updatedLines);
                            
                            // 更新UI
                            LoadPredictionHistory();
                            UpdateOverallWinRate();
                            CalculateAndDisplayOverallWinRate();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 处理当前预测记录失败: {ex.Message}\n";
                File.AppendAllText("error.log", $"Error in ProcessCurrentPrediction at {DateTime.Now}: {ex.Message}" + Environment.NewLine);
                File.AppendAllText("error.log", ex.StackTrace + Environment.NewLine);
            }
        }
        
        /// <summary>
        /// 获取当前价格
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private double GetCurrentPrice(string symbol)
        {
            // 这里可以实现获取实时价格的逻辑
            // 暂时返回一个随机价格波动（基于初始价格的±5%）
            Random random = new Random();
            double priceChange = initialPrice * (random.NextDouble() * 0.1 - 0.05); // ±5%
            return initialPrice + priceChange;
        }

        /// <summary>
        /// 事件合约分析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btn_analysis_Click(object sender, EventArgs e)
        {
            // 获取用户选择的币种和周期
            string selectedCurrency = com_Currency.SelectedItem.ToString(); // 如 "BTC/USDT"
            string selectedCycle = com_Cycle.SelectedItem.ToString();       // 如 "10分钟"

            // 转换币种格式
            string symbol = selectedCurrency.Replace("/", ""); // 将 "BTC/USDT" 转换为 "BTCUSDT"

            // 转换周期格式
            string interval = CycleToInterval(selectedCycle); // 将 "10分钟" 转换为 "15m"
            string shortInterval = "5m"; // 用于结合判断趋势的更短周期K线

            analysis_Log.Text = "";


            // 转换周期格式（如 "10分钟" 转换为秒数）
            int cycleInSeconds = CycleToSeconds(selectedCycle);

            // 日志输出开始分析的消息
            analysis_Log.Text += $"开始分析 {selectedCurrency} ({selectedCycle})...\n";
            analysis_Log.Text += Environment.NewLine;

            // 记录分析开始时间
            DateTime analysisStartTime = DateTime.Now;
            analysis_Log.Text += $"分析开始时间: {analysisStartTime}\n";

            // 根据周期动态调整回测参数，包括数据量限制
            (int minDataPoints, int rsiPeriod, int maPeriod, int bollingerPeriod, int limit) = GetBacktestParamsByInterval(interval);
            
            // 调用API获取历史K线数据，根据周期使用不同的数据量
            JArray klines = await GetHistoricalData(symbol, interval, limit);
            
            // 对于10分钟周期（实际使用15m），额外获取5m K线数据用于结合判断趋势
            JArray shortKlines = null;
            if (selectedCycle == "10分钟")
            {
                shortKlines = await GetHistoricalData(symbol, shortInterval, limit);
                analysis_Log.Text += $"📊 已获取 {limit} 根 {shortInterval} K线数据用于辅助分析\n";
            }

            // 检查是否成功获取到数据
            if (klines == null || klines.Count == 0)
            {
                analysis_Log.Text += "未获取到有效数据。\n";
                return;
            }

            // 提取关键数据
            var closingPrices = klines.Select(k => (double)k[4]).ToList(); // 收盘价
            var highPrices = klines.Select(k => (double)k[2]).ToList();    // 最高价
            var lowPrices = klines.Select(k => (double)k[3]).ToList();     // 最低价
            var volumes = klines.Select(k => (double)k[5]).ToList();       // 成交量
            double currentPrice = closingPrices.Last();                   // 当前价格（最新收盘价）
            
            // 计算技术指标
            double rsi = CalculateRSI(closingPrices, rsiPeriod);
            
            double ma = CalculateMA(closingPrices, maPeriod);
            double maDistance = (currentPrice - ma) / ma * 100;
            
            var bollingerBands = CalculateBollingerBands(closingPrices, bollingerPeriod);
            double pricePosition = (currentPrice - bollingerBands.LowerBand) / (bollingerBands.UpperBand - bollingerBands.LowerBand);
            
            var macdResult = CalculateMACD(closingPrices);
            var stochastic = CalculateStochasticOscillator(highPrices, lowPrices, closingPrices);
            var atr = CalculateATR(highPrices, lowPrices, closingPrices);
            double avgVolume = CalculateAverageVolume(klines, maPeriod);

            // 斐波那契回撤需要高点和低点
            double highestHigh = highPrices.Max();
            double lowestLow = lowPrices.Min();
            var fibonacciRetracement = CalculateFibonacciRetracement(highestHigh, lowestLow);
            
            // 计算支撑和阻力距离
            double supportDistance = fibonacciRetracement.Values.Min(d => Math.Abs(d - currentPrice));
            double resistanceDistance = fibonacciRetracement.Values.Max(d => Math.Abs(d - currentPrice));

            // 综合分析，结合更短周期K线数据
            string trend = AnalyzeTrend(
                currentPrice,
                rsi,
                ma,
                bollingerBands,
                macdResult,
                stochastic,
                atr,
                avgVolume,
                fibonacciRetracement,
                interval,
                shortKlines
            );

            // 设置综合趋势方向
            predictedTrend = trend; // 在这里保存趋势方向
                                    
            initialPrice = currentPrice; // 获取当前价格并记录为初始价格
            // 执行回测，传入当前周期
            var backtestResult = Backtest(klines, interval);
            
            // 只在非中性预测时启动倒计时
            if (!trend.Contains("中性"))
            {
                // 设置倒计时初始秒数
                timer1.Tag = cycleInSeconds;
                UpdateCountdownLabel(cycleInSeconds);
                
                // 启动倒计时
                timer1.Start();
            }

            // 输出分析结果 - 改进版：更清晰的格式和更多统计信息
            analysis_Log.Text += "\n==============================================\n";
            analysis_Log.Text += "              分析结果汇总                    \n";
            analysis_Log.Text += "==============================================\n\n";
            
            // 基本信息
            analysis_Log.Text += $"📊 当前价格: {currentPrice:F2}\n";
            analysis_Log.Text += $"📈 涨跌幅: {(currentPrice - closingPrices[closingPrices.Count - 2]):F2} ({(currentPrice - closingPrices[closingPrices.Count - 2]) / closingPrices[closingPrices.Count - 2] * 100:F2}%)\n";
            
            // 技术指标详细信息
            analysis_Log.Text += "\n==============================================\n";
            analysis_Log.Text += "                技术指标                      \n";
            analysis_Log.Text += "==============================================\n";
            
            // RSI指标
            string rsiStatus = rsi > 75 ? "严重超买 (⚠️ 看跌)" : rsi > 70 ? "超买 (⚠️ 看跌)" : rsi < 25 ? "严重超卖 (📈 看涨)" : rsi < 30 ? "超卖 (📈 看涨)" : rsi > 55 ? "偏多区间 (📈 看涨)" : rsi < 45 ? "偏空区间 (⚠️ 看跌)" : "中性区间";
            analysis_Log.Text += $"\n📊 RSI ({rsiPeriod}): {rsi:F2} - {rsiStatus}\n";
            
            // 移动平均线
            string maStatus = maDistance > 2 ? "明显高于均线 (📈 看涨)" : maDistance < -2 ? "明显低于均线 (⚠️ 看跌)" : currentPrice > ma ? "略高于均线 (📈 温和看涨)" : "略低于均线 (⚠️ 温和看跌)";
            analysis_Log.Text += $"📈 移动平均线 ({maPeriod}): {ma:F2} - {maStatus}\n";
            
            // MACD指标
            string macdStatus = macdResult.MACDLine > macdResult.SignalLine ? (macdResult.Histogram > 0 ? "金叉且直方图扩大 (📈 强烈看涨)" : "金叉但直方图缩小 (📈 温和看涨)") : (macdResult.Histogram < 0 ? "死叉且直方图扩大 (⚠️ 强烈看跌)" : "死叉但直方图缩小 (⚠️ 温和看跌)");
            analysis_Log.Text += $"📊 MACD: 快线={macdResult.MACDLine:F4}, 慢线={macdResult.SignalLine:F4}, 直方图={macdResult.Histogram:F4} - {macdStatus}\n";
            
            // 布林带
            string bollingerStatus = currentPrice > bollingerBands.UpperBand ? "突破上轨 (⚠️ 看跌)" : currentPrice < bollingerBands.LowerBand ? "突破下轨 (📈 看涨)" : pricePosition > 0.75 ? "上半区间 (⚠️ 偏空)" : pricePosition < 0.25 ? "下半区间 (📈 偏多)" : "正常范围";
            analysis_Log.Text += $"📊 布林带 ({bollingerPeriod}): 上轨={bollingerBands.UpperBand:F2}, 中轨={bollingerBands.SMA:F2}, 下轨={bollingerBands.LowerBand:F2} - {bollingerStatus}\n";
            
            // 随机指标
            string stochasticStatus = stochastic.K > 85 ? "超买 (⚠️ 看跌)" : stochastic.K < 15 ? "超卖 (📈 看涨)" : stochastic.K > stochastic.D ? (stochastic.K < 80 && stochastic.D < 80 ? (stochastic.K > 50 ? "多头趋势 (📈 看涨)" : "转强信号 (📈 温和看涨)") : "") : (stochastic.K > 20 && stochastic.D > 20 ? (stochastic.K < 50 ? "空头趋势 (⚠️ 看跌)" : "转弱信号 (⚠️ 温和看跌)") : "");
            analysis_Log.Text += $"📊 随机指标: %K={stochastic.K:F2}, %D={stochastic.D:F2} - {stochasticStatus}\n";
            
            // 成交量
            string volumeStatus = avgVolume > 0 ? (currentPrice > ma && avgVolume > atr * 5 ? "价格上涨且成交量放大 (📈 看涨)" : (currentPrice < ma && avgVolume > atr * 5 ? "价格下跌且成交量放大 (⚠️ 看跌)" : "成交量正常")) : "成交量数据不足";
            analysis_Log.Text += $"📊 平均成交量: {avgVolume:F2} - {volumeStatus}\n";
            
            // 斐波那契回撤
            analysis_Log.Text += "\n📊 斐波那契回撤水平: \n";
            foreach (var kvp in fibonacciRetracement.OrderByDescending(k => k.Value))
            {
                analysis_Log.Text += $"   {kvp.Key}: {kvp.Value:F2}\n";
            }
            string fibStatus = (supportDistance > 0 && supportDistance < 1) ? "接近支撑位 (📈 看涨)" : (resistanceDistance > 0 && resistanceDistance < 1) ? "接近阻力位 (⚠️ 看跌)" : "无明显支撑阻力";
            analysis_Log.Text += $"   状态: {fibStatus}\n";
            
            // 综合分析结果
            analysis_Log.Text += "\n==============================================\n";
            analysis_Log.Text += "                综合分析                      \n";
            analysis_Log.Text += "==============================================\n";
            
            // 综合趋势
            analysis_Log.Text += $"\n🎯 综合趋势: {trend}\n";
            
            // 回测结果
            analysis_Log.Text += "\n==============================================\n";
            analysis_Log.Text += "                回测结果                      \n";
            analysis_Log.Text += "==============================================\n";
            analysis_Log.Text += $"\n📊 回测胜率: {backtestResult.WinRate:P2} ({backtestResult.CorrectPredictions}/{backtestResult.TotalPredictions})\n";
            
            // 分析结束时间
            DateTime analysisEndTime = DateTime.Now;
            TimeSpan analysisDuration = analysisEndTime - analysisStartTime;
            analysis_Log.Text += $"\n⏱️ 分析耗时: {analysisDuration.TotalSeconds:F2}秒\n";
            
            // 分隔线
            analysis_Log.Text += "\n==============================================\n\n";
            
            // 保存分析结果到文件
            SaveAnalysisResultToFile(symbol, interval, currentPrice, trend, backtestResult, analysisStartTime, analysisEndTime);
            
            // 只保存非中性的预测结果到历史文件（用于计算总体胜率）
            if (!trend.Contains("中性"))
            {
                SavePredictionToHistory(symbol, interval, trend, initialPrice);
            }
        }
        
        /// <summary>
        /// 保存分析结果到文件
        /// </summary>
        private void SaveAnalysisResultToFile(string symbol, string interval, double currentPrice, string trend, (double WinRate, int TotalPredictions, int CorrectPredictions) backtestResult, DateTime startTime, DateTime endTime)
        {
            try
            {
                // 创建文件内容
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("==============================================");
                sb.AppendLine("              分析结果汇总                    ");
                sb.AppendLine("==============================================");
                sb.AppendLine();
                sb.AppendLine($"📅 分析时间: {startTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                sb.AppendLine($"⏱️ 分析耗时: {(endTime - startTime).TotalSeconds:F2}秒");
                sb.AppendLine($"📊 币种: {symbol}");
                sb.AppendLine($"🕒 周期: {interval}");
                sb.AppendLine($"💰 当前价格: {currentPrice:F2}");
                sb.AppendLine($"🎯 综合趋势: {trend}");
                sb.AppendLine();
                sb.AppendLine("==============================================");
                sb.AppendLine("                回测结果                      ");
                sb.AppendLine("==============================================");
                sb.AppendLine($"📊 回测胜率: {backtestResult.WinRate:P2} ({backtestResult.CorrectPredictions}/{backtestResult.TotalPredictions})");
                sb.AppendLine();
                sb.AppendLine("==============================================");
                sb.AppendLine();
                
                // 追加到文件
                File.AppendAllText(AnalysisResultsFile, sb.ToString());
                
                // 在日志中显示保存成功
                analysis_Log.Text += $"📝 分析结果已保存到文件: {AnalysisResultsFile}\n";
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 保存分析结果到文件失败: {ex.Message}\n";
            }
        }
        
        /// <summary>
        /// 保存预测结果到历史文件
        /// </summary>
        private void SavePredictionToHistory(string symbol, string interval, string predictedTrend, double initialPrice)
        {
            try
            {
                // 创建预测记录行，添加初始价格字段，结束价格和结果暂时为空
                string predictionRecord = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},{symbol},{interval},{predictedTrend},{initialPrice:0.00},,\n";
                
                // 追加到文件
                File.AppendAllText(PredictionHistoryFile, predictionRecord);
                
                // 在日志中显示保存成功
                analysis_Log.Text += $"📝 预测结果已保存到历史记录: {PredictionHistoryFile}\n";
                
                // 更新UI显示
                LoadPredictionHistory();
                UpdateOverallWinRate();
                CalculateAndDisplayOverallWinRate();
                
                // 更新预测检查定时器，确保新添加的预测能在到期时被处理
                UpdatePredictionCheckerTimer();
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 保存预测历史失败: {ex.Message}\n";
            }
        }
        
        /// <summary>
        /// 计算并显示总体预测胜率
        /// </summary>
        private void CalculateAndDisplayOverallWinRate()
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(PredictionHistoryFile))
                {
                    analysis_Log.Text += $"📋 预测历史文件不存在: {PredictionHistoryFile}\n";
                    return;
                }
                
                // 读取所有预测记录
                var lines = File.ReadAllLines(PredictionHistoryFile);
                
                if (lines.Length == 0)
                {
                    analysis_Log.Text += "📋 预测历史为空\n";
                    return;
                }
                
                // 解析预测记录
                int totalPredictions = 0;
                int correctPredictions = 0;
                
                foreach (var line in lines)
                {
                    // 跳过空行
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    // 解析记录
                    var parts = line.Split(',');
                    if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]))
                    {
                        totalPredictions++;
                        
                        // 统计成功次数
                        if (parts[6] == "成功")
                        {
                            correctPredictions++;
                        }
                    }
                }
                
                // 计算胜率
                double winRate = totalPredictions > 0 ? (double)correctPredictions / totalPredictions : 0;
                
                // 在日志中显示总体预测统计
                analysis_Log.Text += $"📊 总体预测统计: 共 {totalPredictions} 次预测, 成功 {correctPredictions} 次, 失败 {totalPredictions - correctPredictions} 次\n";
                analysis_Log.Text += $"📈 总体胜率: {(winRate * 100):0.00}%\n";
            }
            catch (Exception ex)
            {
                analysis_Log.Text += $"❌ 计算总体胜率失败: {ex.Message}\n";
            }
        }



        /// <summary>
        /// 计算平均真实波幅 (ATR)。
        /// </summary>
        /// <param name="highPrices">最高价列表。</param>
        /// <param name="lowPrices">最低价列表。</param>
        /// <param name="closingPrices">收盘价列表。</param>
        /// <param name="period">计算周期，默认为14。</param>
        /// <returns>返回ATR值。</returns>
        private double CalculateATR(IList<double> highPrices, IList<double> lowPrices, IList<double> closingPrices, int period = 14)
        {
            if (highPrices == null || lowPrices == null || closingPrices == null)
                throw new ArgumentException("价格列表不能为空");

            if (highPrices.Count != lowPrices.Count || highPrices.Count != closingPrices.Count)
                throw new ArgumentException("价格列表长度必须一致");

            // 使用索引操作获取最近N个周期的价格数据
            int startIndex = Math.Max(0, highPrices.Count - period);
            var lastHighs = highPrices.Skip(startIndex).Take(period).ToList();
            var lastLows = lowPrices.Skip(startIndex).Take(period).ToList();
            var lastCloses = closingPrices.Skip(startIndex).Take(period).ToList();

            // 计算真实波幅 (TR)
            var trueRanges = new List<double>();
            for (int i = 1; i < lastHighs.Count; i++)
            {
                double tr1 = lastHighs[i] - lastLows[i]; // 当前周期的最高价 - 最低价
                double tr2 = Math.Abs(lastHighs[i] - lastCloses[i - 1]); // 当前周期的最高价 - 前一周期的收盘价
                double tr3 = Math.Abs(lastLows[i] - lastCloses[i - 1]);   // 当前周期的最低价 - 前一周期的收盘价
                trueRanges.Add(Math.Max(tr1, Math.Max(tr2, tr3)));
            }

            // 如果没有足够的数据，直接返回0
            if (trueRanges.Count == 0)
                return 0;

            // 返回真实波幅的平均值作为ATR
            return trueRanges.Average();
        }


        /// <summary>
        /// 计算平均成交量。
        /// </summary>
        /// <param name="klines">K线数据。</param>
        /// <param name="period">计算周期，默认为20。</param>
        /// <returns>返回平均成交量。</returns>
        private double CalculateAverageVolume(JArray klines, int period = 20)
        {
            if (klines == null || klines.Count == 0)
                throw new ArgumentException("K线数据不能为空");

            // 提取成交量数据
            var volumes = klines.Select(k => (double)k[5]).ToList();

            // 使用索引操作获取最近N个周期的成交量数据
            int startIndex = Math.Max(0, volumes.Count - period);
            var lastVolumes = volumes.Skip(startIndex).Take(period).ToList();

            // 返回平均成交量
            return lastVolumes.Average();
        }


        /// <summary>
        /// 计算斐波那契回撤水平。
        /// </summary>
        /// <param name="highestHigh">最高高点。</param>
        /// <param name="lowestLow">最低低点。</param>
        /// <returns>返回斐波那契回撤水平的字典。</returns>
        private Dictionary<string, double> CalculateFibonacciRetracement(double highestHigh, double lowestLow)
        {
            if (highestHigh <= lowestLow)
                throw new ArgumentException("最高高点必须大于最低低点");

            // 定义斐波那契回撤比例
            var fibonacciLevels = new Dictionary<string, double>
    {
        { "0%", highestHigh },
        { "23.6%", highestHigh - (highestHigh - lowestLow) * 0.236 },
        { "38.2%", highestHigh - (highestHigh - lowestLow) * 0.382 },
        { "50%", highestHigh - (highestHigh - lowestLow) * 0.5 },
        { "61.8%", highestHigh - (highestHigh - lowestLow) * 0.618 },
        { "100%", lowestLow }
    };

            return fibonacciLevels;
        }

        /// <summary>
        /// 获取最高高点和最低低点。
        /// </summary>
        /// <param name="highPrices">最高价列表。</param>
        /// <param name="lowPrices">最低价列表。</param>
        /// <param name="period">计算周期，默认为20。</param>
        /// <returns>返回最高高点和最低低点。</returns>
        private (double HighestHigh, double LowestLow) GetHighestAndLowest(IList<double> highPrices, IList<double> lowPrices, int period = 20)
        {
            if (highPrices == null || lowPrices == null)
                throw new ArgumentException("价格列表不能为空");

            if (highPrices.Count != lowPrices.Count)
                throw new ArgumentException("价格列表长度必须一致");

            // 使用索引操作获取最近N个周期的价格数据
            int startIndex = Math.Max(0, highPrices.Count - period);
            var lastHighs = highPrices.Skip(startIndex).Take(period).ToList();
            var lastLows = lowPrices.Skip(startIndex).Take(period).ToList();

            // 计算最高高点和最低低点
            double highestHigh = lastHighs.Max();
            double lowestLow = lastLows.Min();

            return (highestHigh, lowestLow);
        }

        /// <summary>
        /// 计算随机指标 (%K 和 %D)。
        /// </summary>
        /// <param name="highPrices">最高价列表。</param>
        /// <param name="lowPrices">最低价列表。</param>
        /// <param name="closingPrices">收盘价列表。</param>
        /// <param name="period">计算周期，默认为14。</param>
        /// <returns>返回%K和%D值。</returns>
        private (double K, double D) CalculateStochasticOscillator(IList<double> highPrices, IList<double> lowPrices, IList<double> closingPrices, int period = 14)
        {
            int totalPeriods = Math.Min(Math.Min(highPrices.Count, lowPrices.Count), closingPrices.Count);
            if (totalPeriods < period + 2) // 需要至少period+2个周期才能计算3周期%D
            {
                return (0, 0);
            }

            // 计算最近的%K值列表，用于计算%D线
            List<double> kValues = new List<double>();
            
            // 计算最近3个周期的%K值
            for (int i = 0; i < 3; i++)
            {
                int currentIndex = totalPeriods - 1 - i;
                if (currentIndex < period - 1) break;
                
                // 获取当前周期的最高价和最低价
                int currentStartIndex = currentIndex - period + 1;
                double highestHigh = highPrices.Skip(currentStartIndex).Take(period).Max();
                double lowestLow = lowPrices.Skip(currentStartIndex).Take(period).Min();
                double close = closingPrices[currentIndex];
                
                // 避免除以零
                if (highestHigh == lowestLow)
                {
                    kValues.Add(0);
                }
                else
                {
                    double k = 100 * (close - lowestLow) / (highestHigh - lowestLow);
                    kValues.Add(k);
                }
            }
            
            // 计算当前%K（最近一个周期的%K值）
            double currentK = kValues.Count > 0 ? kValues[0] : 0;
            
            // 计算%D（最近3个%K值的SMA）
            double d = CalculateSMA(kValues, 3);
            
            // 确保%K和%D在0-100范围内
            currentK = Math.Max(0, Math.Min(100, currentK));
            d = Math.Max(0, Math.Min(100, d));

            return (currentK, d);
        }

        /// <summary>
        /// 计算简单移动平均线 (SMA)。
        /// </summary>
        /// <param name="values">值列表。</param>
        /// <param name="period">计算周期。</param>
        /// <returns>返回SMA值。</returns>
        private double CalculateSMA(IList<double> values, int period)
        {
            // 使用索引操作获取最近N个周期的值
            int startIndex = Math.Max(0, values.Count - period);
            var lastValues = values.Skip(startIndex).Take(period).ToList();

            // 避免空集合的情况
            if (lastValues.Count == 0)
            {
                throw new ArgumentException("值列表为空，无法计算SMA", nameof(values));
            }

            return lastValues.Average();
        }

        /// <summary>
        /// 计算简单移动平均线 (MA)。
        /// </summary>
        /// <param name="prices">收盘价列表。</param>
        /// <param name="period">计算周期，默认为20。</param>
        /// <returns>返回移动平均值。</returns>
        private double CalculateMA(IList<double> prices, int period = 20)
        {
            if (prices == null || prices.Count == 0)
                throw new ArgumentException("价格列表不能为空", nameof(prices));
            if (period <= 0 || period > prices.Count)
                throw new ArgumentException("周期必须大于0且不超过价格列表长度", nameof(period));

            // 使用索引操作获取最近 N 个周期的价格
            int startIndex = Math.Max(0, prices.Count - period);
            var lastPrices = prices.Skip(startIndex).Take(period).ToList();

            return lastPrices.Average();
        }


        /// <summary>
        /// 执行回测并返回回测结果。
        /// </summary>
        /// <param name="klines">历史K线数据。</param>
        /// <param name="interval">K线周期，如"15m", "30m", "1h", "4h"等</param>
        /// <returns>返回一个包含回测结果的元组，包括胜率、总预测次数、正确预测次数。</returns>
        private (double WinRate, int TotalPredictions, int CorrectPredictions) Backtest(JArray klines, string interval = "15m")
        {
            // 提取关键数据
            var closingPrices = klines.Select(k => (double)k[4]).ToList(); // 收盘价
            var highPrices = klines.Select(k => (double)k[2]).ToList();    // 最高价
            var lowPrices = klines.Select(k => (double)k[3]).ToList();     // 最低价
            var volumes = klines.Select(k => (double)k[5]).ToList();       // 成交量

            // 根据不同周期调整回测参数
            (int minDataPoints, int rsiPeriod, int maPeriod, int bollingerPeriod, _) = GetBacktestParamsByInterval(interval);

            // 初始化回测变量
            int totalPredictions = 0;       // 总预测次数
            int correctPredictions = 0;     // 正确预测次数

            for (int i = minDataPoints; i < closingPrices.Count - 1; i++) // 跳过前N个数据点以避免不稳定的指标
            {
                // 截取到当前点的历史数据
                var currentClosingPrices = closingPrices.Take(i).ToList();
                var currentHighPrices = highPrices.Take(i).ToList();
                var currentLowPrices = lowPrices.Take(i).ToList();

                // 计算技术指标 - 使用根据周期调整的参数
                double rsi = CalculateRSI(currentClosingPrices, rsiPeriod);
                double ma = CalculateMA(currentClosingPrices, maPeriod);
                var bollingerBands = CalculateBollingerBands(currentClosingPrices, bollingerPeriod);
                var macdResult = CalculateMACD(currentClosingPrices);
                var stochastic = CalculateStochasticOscillator(currentHighPrices, currentLowPrices, currentClosingPrices);
                double atr = CalculateATR(currentHighPrices, currentLowPrices, currentClosingPrices);
                double avgVolume = CalculateAverageVolume(new JArray(klines.Take(i)), maPeriod);

                // 获取斐波那契回撤水平（使用最近N周期的高点和低点，N根据周期调整）
                int fibPeriod = Math.Min(maPeriod * 2, i);
                var recentHighs = currentHighPrices.Skip(i - fibPeriod).Take(fibPeriod).ToList();
                var recentLows = currentLowPrices.Skip(i - fibPeriod).Take(fibPeriod).ToList();
                double highestHigh = recentHighs.Count > 0 ? recentHighs.Max() : currentHighPrices.Last();
                double lowestLow = recentLows.Count > 0 ? recentLows.Min() : currentLowPrices.Last();
                var fibonacciRetracement = CalculateFibonacciRetracement(highestHigh, lowestLow);

                // 综合分析涨跌方向，传入null作为shortKlines参数（回测时不使用短周期数据）
                string trend = AnalyzeTrend(
                    currentClosingPrices.Last(),
                    rsi,
                    ma,
                    bollingerBands,
                    macdResult,
                    stochastic,
                    atr,
                    avgVolume,
                    fibonacciRetracement,
                    interval,
                    null
                );

                // 只统计明确的上涨/下跌信号，忽略中性信号
                if (trend == "上涨" || trend == "下跌")
                {
                    // 获取实际价格变化方向
                    double currentPrice = currentClosingPrices.Last();
                    double nextPrice = closingPrices[i + 1];
                    string actualDirection = nextPrice > currentPrice ? "上涨" : "下跌";

                    // 判断预测是否正确
                    if (trend == actualDirection)
                    {
                        correctPredictions++;
                    }

                    totalPredictions++;
                }
            }

            // 计算胜率
            double winRate = totalPredictions == 0 ? 0 : (double)correctPredictions / totalPredictions;

            // 返回回测结果
            return (winRate, totalPredictions, correctPredictions);
        }

        /// <summary>
        /// 根据不同周期获取回测参数
        /// </summary>
        /// <param name="interval">K线周期</param>
        /// <returns>返回回测参数元组：(最小数据点, RSI周期, MA周期, 布林带周期)</returns>
        private (int minDataPoints, int rsiPeriod, int maPeriod, int bollingerPeriod, int limit) GetBacktestParamsByInterval(string interval)
        {
            switch (interval.ToLower())
            {
                case "15m": // 10分钟-15分钟周期（短线）
                    return (30, 6, 10, 10, 300); // 最小30个数据点，RSI 6，MA 10，布林带 10，回测数据量300
                case "30m": // 30分钟周期
                    return (40, 9, 15, 15, 400); // 最小40个数据点，RSI 9，MA 15，布林带 15，回测数据量400
                case "1h": // 1小时周期
                    return (50, 14, 20, 20, 500); // 最小50个数据点，RSI 14，MA 20，布林带 20，回测数据量500
                case "4h": // 4小时周期
                    return (60, 14, 20, 20, 600); // 最小60个数据点，RSI 14，MA 20，布林带 20，回测数据量600
                case "1d": // 日线周期
                    return (80, 14, 20, 20, 800); // 最小80个数据点，RSI 14，MA 20，布林带 20，回测数据量800
                default:
                    return (50, 14, 20, 20, 500); // 默认参数
            }
        }



        /// <summary>
        /// 调用币安 API 获取当前价格
        /// </summary>
        private async Task<double> GetCurrentPriceFromBinance(string symbol)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10); // 设置超时时间
                string url = $"https://api.binance.com/api/v3/ticker/price?symbol={symbol}";
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject priceData = JObject.Parse(jsonResponse); // 使用 JSON 解析
                    return Convert.ToDouble(priceData["price"]);      // 提取价格字段
                }
                else
                {
                    throw new Exception($"API 请求失败: {response.StatusCode}");
                }
            }
        }

        /// <summary>
        /// 获取指定币种和周期的历史K线数据。
        /// </summary>
        /// <param name="symbol">币种标识符，如 "BTCUSDT"。</param>
        /// <param name="interval">K线的时间周期，如 "15m", "30m", "1h", "4h" 等。</param>
        /// <returns>返回一个JArray对象，包含K线数据。</returns>
        private async Task<JArray> GetHistoricalData(string symbol, string interval, int limit = 500)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10); // 设置超时时间
                try
                {
                    string url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var klines = JArray.Parse(content);
                        analysis_Log.Text += $"📊 已获取 {klines.Count} 根 {interval} K线数据\n";
                        return klines;
                    }
                    else
                    {
                        analysis_Log.Text += $"❌ 获取数据失败 - 状态码: {response.StatusCode}\n";
                        return null;
                    }
                }
                catch (TaskCanceledException)
                {
                    analysis_Log.Text += $"❌ 获取数据超时，请检查网络连接\n";
                    return null;
                }
                catch (Exception ex)
                {
                    analysis_Log.Text += $"❌ 获取数据时发生错误: {ex.Message}\n";
                    return null;
                }
            }
        }

        /// <summary>
        /// 计算相对强弱指数 (RSI)。
        /// </summary>
        /// <param name="prices">收盘价列表。</param>
        /// <param name="period">计算周期，默认为14。</param>
        /// <returns>返回RSI值。</returns>
        private double CalculateRSI(IList<double> prices, int period = 14)
        {
            var gains = new List<double>();
            var losses = new List<double>();

            // 遍历价格变化，计算上涨和下跌幅度
            for (int i = 1; i < prices.Count; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(-change);
            }

            // 使用索引操作获取最近 N 个周期的涨幅和跌幅
            int startIndex = Math.Max(0, gains.Count - period);
            double avgGain = gains.Skip(startIndex).Take(period).Average();

            startIndex = Math.Max(0, losses.Count - period);
            double avgLoss = losses.Skip(startIndex).Take(period).Average();

            if (avgLoss == 0) return 100;
            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }


        /// <summary>
        /// 计算MACD指标。
        /// </summary>
        /// <param name="prices">收盘价列表。</param>
        /// <returns>返回MACD线、信号线和直方图。</returns>
        private (double MACDLine, double SignalLine, double Histogram) CalculateMACD(IList<double> prices)
        {
            // 快速EMA (12周期)
            double fastEMA = CalculateEMA(prices, 12);

            // 慢速EMA (26周期)
            double slowEMA = CalculateEMA(prices, 26);

            // MACD线 = 快速EMA - 慢速EMA
            double macdLine = fastEMA - slowEMA;

            // 信号线 (9周期EMA)
            var macdLineHistory = new List<double>();
            for (int i = 0; i < prices.Count; i++)
            {
                var tempPrices = prices.Take(i + 1).ToList();
                macdLineHistory.Add(CalculateEMA(tempPrices, 12) - CalculateEMA(tempPrices, 26));
            }

            // 使用索引操作获取最近 9 个周期的 MACD 线历史
            int startIndex = Math.Max(0, macdLineHistory.Count - 9);
            double signalLine = CalculateEMA(macdLineHistory.Skip(startIndex).Take(9).ToList(), 9);

            // 直方图 = MACD线 - 信号线
            double histogram = macdLine - signalLine;

            return (macdLine, signalLine, histogram);
        }

        /// <summary>
        /// 计算指数移动平均线 (EMA)。
        /// </summary>
        /// <param name="prices">收盘价列表。</param>
        /// <param name="period">计算周期。</param>
        /// <returns>返回给定周期的 EMA 值。</returns>
        private double CalculateEMA(IList<double> prices, int period)
        {
            if (prices == null || prices.Count == 0) throw new ArgumentException("价格列表不能为空", nameof(prices));
            if (period <= 0) throw new ArgumentException("周期必须大于0", nameof(period));

            // 平滑系数
            double smoothingMultiplier = 2.0 / (period + 1);

            // 初始EMA值为第一个价格
            double ema = prices[0];

            // 迭代计算EMA
            for (int i = 1; i < prices.Count; i++)
            {
                // EMA公式：EMA(i) = Price(i) * K + EMA(i-1) * (1 - K)
                // 其中K是平滑系数
                ema = prices[i] * smoothingMultiplier + ema * (1 - smoothingMultiplier);
            }

            return ema;
        }

        /// <summary>
        /// 计算布林带。
        /// </summary>
        /// <param name="prices">收盘价列表。</param>
        /// <param name="period">计算周期，默认为20。</param>
        /// <param name="stdDev">标准差倍数，默认为2。</param>
        /// <returns>返回布林带上轨、下轨和中轨（SMA）。</returns>
        private (double UpperBand, double LowerBand, double SMA) CalculateBollingerBands(IList<double> prices, int period = 20, double stdDev = 2)
        {
            // 使用索引操作获取最近 N 个周期的价格
            int startIndex = Math.Max(0, prices.Count - period);
            var lastPrices = prices.Skip(startIndex).Take(period).ToList();

            // 计算简单移动平均线 (SMA)
            double sma = lastPrices.Average();

            // 计算方差和标准差
            double variance = lastPrices.Select(p => Math.Pow(p - sma, 2)).Average(); // 方差
            double std = Math.Sqrt(variance); // 标准差

            // 计算布林带上轨和下轨
            double upperBand = sma + stdDev * std; // 上轨
            double lowerBand = sma - stdDev * std; // 下轨

            return (upperBand, lowerBand, sma); // 返回上轨、下轨和中轨
        }

        /// <summary>
        /// 综合涨跌方向判断 - 专业量化版
        /// </summary>
        private string AnalyzeTrend(
    double currentPrice,
    double rsi,
    double ma,
    (double UpperBand, double LowerBand, double SMA) bollingerBands,
    (double MACDLine, double SignalLine, double Histogram) macdResult,
    (double K, double D) stochastic,
    double atr,
    double avgVolume,
    Dictionary<string, double> fibonacciRetracement,
    string interval,
    JArray shortKlines = null
)
        {
            // 初始化各指标得分
            double rsiScore = 0;
            double macdScore = 0;
            double bollingerScore = 0;
            double maScore = 0;
            double atrScore = 0;
            double volumeScore = 0;
            double stochasticScore = 0;
            double fibonacciScore = 0;

            // 【优化1：改进趋势判断】使用三条件趋势确认
            bool isTrendBullish = 
                currentPrice > ma && 
                macdResult.MACDLine > macdResult.SignalLine &&
                macdResult.Histogram > 0;
            
            bool isTrendBearish = 
                currentPrice < ma && 
                macdResult.MACDLine < macdResult.SignalLine &&
                macdResult.Histogram < 0;
            
            bool isSideways = !isTrendBullish && !isTrendBearish;
            
            // 【优化2：更敏感的RSI评分】基于多区间划分
            if (isTrendBullish) {
                if (rsi < 35) rsiScore = 3.5; // 超卖区间，强烈看涨，降低阈值
                else if (rsi < 50) rsiScore = 2.5; // 买入区间，看涨，扩大区间
                else if (rsi < 65) rsiScore = 1.5; // 中性偏多
                else if (rsi > 75) rsiScore = -2.5; // 超买，看跌，降低阈值
                else rsiScore = 1.0;
            } else if (isTrendBearish) {
                if (rsi > 65) rsiScore = -3.5; // 超买区间，强烈看跌，降低阈值
                else if (rsi > 50) rsiScore = -2.5; // 卖出区间，看跌，扩大区间
                else if (rsi > 35) rsiScore = -1.5; // 中性偏空
                else if (rsi < 25) rsiScore = 2.5; // 超卖，看涨
                else rsiScore = -1.0;
            } else {
                if (rsi > 70) rsiScore = -3.0; // 超买，降低阈值
                else if (rsi < 30) rsiScore = 3.0; // 超卖，提高阈值
                else if (rsi > 60) rsiScore = -1.5; // 偏多
                else if (rsi < 40) rsiScore = 1.5; // 偏空
            }

            // 【优化3：增强MACD评分】更敏感的信号检测
            if (macdResult.MACDLine > macdResult.SignalLine) {
                double macdStrength = Math.Abs(macdResult.MACDLine - macdResult.SignalLine);
                if (macdResult.MACDLine > 0 && macdResult.Histogram > 0) {
                    macdScore = macdStrength > 0.002 ? 5.0 : 4.5; // 强烈上涨趋势，降低阈值
                } else if (macdResult.MACDLine > 0) {
                    macdScore = macdStrength > 0.001 ? 4.0 : 3.5; // 中等上涨趋势，降低阈值
                } else if (macdResult.Histogram > 0) {
                    macdScore = macdStrength > 0.0005 ? 3.0 : 2.5; // 弱上涨趋势，降低阈值
                } else {
                    macdScore = 2.0; // 轻微上涨信号，提高基础分值
                }
            } else if (macdResult.MACDLine < macdResult.SignalLine) {
                double macdStrength = Math.Abs(macdResult.MACDLine - macdResult.SignalLine);
                if (macdResult.MACDLine < 0 && macdResult.Histogram < 0) {
                    macdScore = macdStrength > 0.002 ? -5.0 : -4.5; // 强烈下跌趋势，降低阈值
                } else if (macdResult.MACDLine < 0) {
                    macdScore = macdStrength > 0.001 ? -4.0 : -3.5; // 中等下跌趋势，降低阈值
                } else if (macdResult.Histogram < 0) {
                    macdScore = macdStrength > 0.0005 ? -3.0 : -2.5; // 弱下跌趋势，降低阈值
                } else {
                    macdScore = -2.0; // 轻微下跌信号，提高基础分值
                }
            }

            // 【优化4：改进布林带评分】更敏感的信号检测
            double bollingerWidth = bollingerBands.UpperBand - bollingerBands.LowerBand;
            double pricePosition = (currentPrice - bollingerBands.LowerBand) / bollingerWidth;
            
            if (isTrendBullish) {
                if (currentPrice < bollingerBands.SMA) {
                    bollingerScore = 4.5; // 价格回踩中轨 = 强烈看涨，提高分值
                } else if (currentPrice < bollingerBands.LowerBand) {
                    bollingerScore = 5.5; // 价格突破下轨 = 极度看涨，提高分值
                } else if (pricePosition > 0.8) {
                    bollingerScore = 1.5; // 价格接近上轨 = 温和看涨，降低阈值
                } else if (pricePosition > 0.6) {
                    bollingerScore = 2.5; // 价格在中上部 = 看涨，降低阈值
                } else if (pricePosition > 0.4) {
                    bollingerScore = 3.5; // 价格在中部 = 强烈看涨，降低阈值
                } else {
                    bollingerScore = 2.0; // 价格在中下部 = 温和看涨
                }
            } else if (isTrendBearish) {
                if (currentPrice > bollingerBands.SMA) {
                    bollingerScore = -4.5; // 价格反弹中轨 = 强烈看跌，提高分值
                } else if (currentPrice > bollingerBands.UpperBand) {
                    bollingerScore = -5.5; // 价格突破上轨 = 极度看跌，提高分值
                } else if (pricePosition < 0.2) {
                    bollingerScore = -1.5; // 价格接近下轨 = 温和看跌，提高阈值
                } else if (pricePosition < 0.4) {
                    bollingerScore = -2.5; // 价格在中下部 = 看跌，提高阈值
                } else if (pricePosition < 0.6) {
                    bollingerScore = -3.5; // 价格在中部 = 强烈看跌，提高阈值
                } else {
                    bollingerScore = -2.0; // 价格在中上部 = 温和看跌
                }
            }

            // 【优化5：更敏感的移动平均线评分】基于偏离百分比和趋势强度
            double maDistance = (currentPrice - ma) / ma * 100; // 价格偏离均线百分比
            
            if (isTrendBullish) {
                if (maDistance > 0.8) {
                    maScore = 4.5; // 明显高于均线 = 强上涨信号，降低阈值
                } else if (maDistance > 0.2) {
                    maScore = 3.5; // 略高于均线 = 中等上涨信号，降低阈值
                } else if (maDistance > -0.2) {
                    maScore = 2.5; // 接近均线 = 弱上涨信号，扩大区间
                } else if (maDistance > -0.8) {
                    maScore = 1.5; // 略微低于均线 = 温和看涨，新增区间
                } else {
                    maScore = 1.0; // 明显低于均线 = 中性，新增区间
                }
            } else if (isTrendBearish) {
                if (maDistance < -0.8) {
                    maScore = -4.5; // 明显低于均线 = 强下跌信号，提高阈值
                } else if (maDistance < -0.2) {
                    maScore = -3.5; // 略低于均线 = 中等下跌信号，提高阈值
                } else if (maDistance < 0.2) {
                    maScore = -2.5; // 接近均线 = 弱下跌信号，扩大区间
                } else if (maDistance < 0.8) {
                    maScore = -1.5; // 略微高于均线 = 温和看跌，新增区间
                } else {
                    maScore = -1.0; // 明显高于均线 = 中性，新增区间
                }
            }

            // 【优化6：改进成交量评分】基于量价配合和趋势强度
            if (avgVolume > 0 && atr > 0) {
                double volumeTrendRatio = avgVolume / atr;
                double volumeThreshold = atr * 2.5; // 优化成交量阈值
                
                if (isTrendBullish) {
                    if (volumeTrendRatio > volumeThreshold * 1.5) {
                        volumeScore = 5.0; // 大幅放量，强烈确认上涨
                    } else if (volumeTrendRatio > volumeThreshold) {
                        volumeScore = 4.0; // 放量，确认上涨
                    } else if (volumeTrendRatio > volumeThreshold * 0.5) {
                        volumeScore = 3.0; // 温和放量，弱确认上涨
                    } else {
                        volumeScore = 2.0; // 正常成交量，中性
                    }
                } else if (isTrendBearish) {
                    if (volumeTrendRatio > volumeThreshold * 1.5) {
                        volumeScore = -5.0; // 大幅放量，强烈确认下跌
                    } else if (volumeTrendRatio > volumeThreshold) {
                        volumeScore = -4.0; // 放量，确认下跌
                    } else if (volumeTrendRatio > volumeThreshold * 0.5) {
                        volumeScore = -3.0; // 温和放量，弱确认下跌
                    } else {
                        volumeScore = -2.0; // 正常成交量，中性
                    }
                }
            }

            // 【优化7：改进随机指标评分】基于多条件判断
            if (isTrendBullish) {
                if (stochastic.K > stochastic.D && stochastic.K < 80) {
                    stochasticScore = 3.5; // 金叉且未超买 = 强烈看涨
                } else if (stochastic.K < 20) {
                    stochasticScore = 4.0; // 超卖 = 极度看涨
                } else if (stochastic.K > stochastic.D) {
                    stochasticScore = 2.5; // 金叉 = 看涨
                }
            } else if (isTrendBearish) {
                if (stochastic.K < stochastic.D && stochastic.K > 20) {
                    stochasticScore = -3.5; // 死叉且未超卖 = 强烈看跌
                } else if (stochastic.K > 80) {
                    stochasticScore = -4.0; // 超买 = 极度看跌
                } else if (stochastic.K < stochastic.D) {
                    stochasticScore = -2.5; // 死叉 = 看跌
                }
            }

            // 【优化8：精确斐波那契回撤判断】基于支撑阻力强度
            double closestSupport = 0;
            double closestResistance = 0;
            double minDistance = double.MaxValue;
            string closestLevel = "";
            
            foreach (var kvp in fibonacciRetracement) {
                double distance = Math.Abs(currentPrice - kvp.Value);
                if (distance < minDistance) {
                    minDistance = distance;
                    closestLevel = kvp.Key;
                    if (kvp.Value < currentPrice) closestSupport = kvp.Value;
                    else closestResistance = kvp.Value;
                }
            }
            
            double priceRange = fibonacciRetracement["0%"] - fibonacciRetracement["100%"];
            double distanceThreshold = priceRange * 0.02; // 精确到2%阈值
            
            // 主要斐波那契水平有更强的支撑阻力
            bool isMajorLevel = closestLevel == "38.2%" || closestLevel == "50.0%" || closestLevel == "61.8%";
            
            if (isTrendBullish && minDistance < distanceThreshold) {
                if (closestSupport > 0) {
                    fibonacciScore = isMajorLevel ? 4.5 : 3.5; // 接近支撑位 + 上涨趋势
                }
            } else if (isTrendBearish && minDistance < distanceThreshold) {
                if (closestResistance > 0) {
                    fibonacciScore = isMajorLevel ? -4.5 : -3.5; // 接近阻力位 + 下跌趋势
                }
            }

            // 【优化9：根据不同周期动态调整权重分配】更精确的策略制定
            double macdWeight, maWeight, volumeWeight, bollingerWeight, rsiWeight, stochasticWeight, fibonacciWeight;
            
            switch (interval.ToLower())
            {
                case "15m": // 10-15分钟周期（短线，波动快）
                    macdWeight = 0.50;       // MACD 权重增加，短线趋势仍重要
                    maWeight = 0.20;         // MA 权重降低，短线均线滞后
                    volumeWeight = 0.15;     // 成交量权重增加，短线更依赖量能
                    bollingerWeight = 0.08;  // 布林带权重增加，短线波动大
                    rsiWeight = 0.04;         // RSI 权重增加，短线动量更重要
                    stochasticWeight = 0.04; // 随机指标权重增加，短线反转频繁
                    fibonacciWeight = 0.02;   // 斐波那契权重增加，短线支撑阻力更明显
                    break;
                case "30m": // 30分钟周期（中短线）
                    macdWeight = 0.50;       // MACD 权重适中
                    maWeight = 0.25;         // MA 权重保持
                    volumeWeight = 0.13;     // 成交量权重适中
                    bollingerWeight = 0.05;  // 布林带权重适中
                    rsiWeight = 0.03;         // RSI 权重适中
                    stochasticWeight = 0.03; // 随机指标权重适中
                    fibonacciWeight = 0.01;   // 斐波那契权重保持
                    break;
                case "1h": // 1小时周期（中线）
                    macdWeight = 0.55;       // MACD 权重为主
                    maWeight = 0.25;         // MA 权重保持
                    volumeWeight = 0.12;     // 成交量权重适中
                    bollingerWeight = 0.03;  // 布林带权重降低
                    rsiWeight = 0.02;         // RSI 权重降低
                    stochasticWeight = 0.02; // 随机指标权重降低
                    fibonacciWeight = 0.01;   // 斐波那契权重保持
                    break;
                case "4h": // 4小时周期（中长线）
                    macdWeight = 0.60;       // MACD 权重增加，长线趋势更重要
                    maWeight = 0.25;         // MA 权重保持
                    volumeWeight = 0.10;     // 成交量权重降低，长线更依赖趋势
                    bollingerWeight = 0.02;  // 布林带权重降低
                    rsiWeight = 0.01;         // RSI 权重降低
                    stochasticWeight = 0.01; // 随机指标权重降低
                    fibonacciWeight = 0.01;   // 斐波那契权重保持
                    break;
                case "1d": // 日线周期（长线）
                    macdWeight = 0.65;       // MACD 权重最高，长线趋势最重要
                    maWeight = 0.25;         // MA 权重保持
                    volumeWeight = 0.08;     // 成交量权重最低，长线趋势主导
                    bollingerWeight = 0.01;  // 布林带权重最低
                    rsiWeight = 0.005;       // RSI 权重最低
                    stochasticWeight = 0.005; // 随机指标权重最低
                    fibonacciWeight = 0.00;   // 斐波那契权重为0，长线不依赖短期支撑阻力
                    break;
                default: // 默认情况
                    macdWeight = 0.55;       // MACD 权重 55%（趋势核心，最重要）
                    maWeight = 0.25;         // MA 权重 25%（趋势核心）
                    volumeWeight = 0.12;     // 成交量权重 12%（趋势确认）
                    bollingerWeight = 0.03;  // 布林带权重 3%（波动率验证）
                    rsiWeight = 0.02;         // RSI 权重 2%（动量辅助）
                    stochasticWeight = 0.02; // 随机指标权重 2%（反转辅助）
                    fibonacciWeight = 0.01;   // 斐波那契权重 1%（支撑阻力辅助）
                    break;
            }
            
            double totalScore = 
                macdScore * macdWeight +       // MACD 权重（根据周期动态调整）
                maScore * maWeight +           // MA 权重（根据周期动态调整）
                volumeScore * volumeWeight +   // 成交量权重（根据周期动态调整）
                bollingerScore * bollingerWeight + // 布林带权重（根据周期动态调整）
                rsiScore * rsiWeight +         // RSI 权重（根据周期动态调整）
                stochasticScore * stochasticWeight + // 随机指标权重（根据周期动态调整）
                fibonacciScore * fibonacciWeight; // 斐波那契权重（根据周期动态调整）
            
            // 【优化11：结合更短周期K线数据辅助判断趋势】
            if (shortKlines != null && shortKlines.Count > 0)
            {
                try
                {
                    // 提取更短周期的收盘价数据
                    var shortClosingPrices = shortKlines.Select(k => (double)k[4]).ToList();
                    var shortHighPrices = shortKlines.Select(k => (double)k[2]).ToList();
                    var shortLowPrices = shortKlines.Select(k => (double)k[3]).ToList();
                    
                    if (shortClosingPrices.Count < 3) return "中性(建议观望)"; // 确保有足够的数据点
                    
                    // 计算更短周期的技术指标
                    double shortRsi = CalculateRSI(shortClosingPrices, 6); // 更短的RSI周期
                    double shortMa = CalculateMA(shortClosingPrices, 10); // 更短的MA周期
                    var shortMacd = CalculateMACD(shortClosingPrices); // 短周期MACD
                    var shortStochastic = CalculateStochasticOscillator(shortHighPrices, shortLowPrices, shortClosingPrices, 6); // 短周期随机指标
                    
                    // 更短周期趋势判断
                    double shortTrendScore = 0;
                    
                    // 1. 短周期MA趋势
                    if (currentPrice > shortMa)
                    {
                        shortTrendScore += 1.5; // 短周期价格在MA之上，看涨
                    }
                    else
                    {
                        shortTrendScore -= 1.5; // 短周期价格在MA之下，看跌
                    }
                    
                    // 2. 短周期RSI趋势
                    if (shortRsi > 60) shortTrendScore += 1.5; // 短周期RSI偏强多，看涨
                    else if (shortRsi > 50) shortTrendScore += 0.5; // 短周期RSI偏多，温和看涨
                    else if (shortRsi < 40) shortTrendScore -= 1.5; // 短周期RSI偏弱空，看跌
                    else if (shortRsi < 50) shortTrendScore -= 0.5; // 短周期RSI偏空，温和看跌
                    
                    // 3. 短周期MACD趋势
                    if (shortMacd.MACDLine > shortMacd.SignalLine && shortMacd.Histogram > 0)
                    {
                        shortTrendScore += 2.0; // 短周期MACD金叉且直方图扩大，强烈看涨
                    }
                    else if (shortMacd.MACDLine > shortMacd.SignalLine)
                    {
                        shortTrendScore += 1.0; // 短周期MACD金叉，温和看涨
                    }
                    else if (shortMacd.MACDLine < shortMacd.SignalLine && shortMacd.Histogram < 0)
                    {
                        shortTrendScore -= 2.0; // 短周期MACD死叉且直方图扩大，强烈看跌
                    }
                    else if (shortMacd.MACDLine < shortMacd.SignalLine)
                    {
                        shortTrendScore -= 1.0; // 短周期MACD死叉，温和看跌
                    }
                    
                    // 4. 短周期随机指标
                    if (shortStochastic.K > shortStochastic.D && shortStochastic.K < 80)
                    {
                        shortTrendScore += 1.5; // 短周期随机指标金叉且未超买，看涨
                    }
                    else if (shortStochastic.K < shortStochastic.D && shortStochastic.K > 20)
                    {
                        shortTrendScore -= 1.5; // 短周期随机指标死叉且未超卖，看跌
                    }
                    
                    // 5. 短周期价格动量（最近几根K线的变化）
                    double recentChange = (shortClosingPrices.Last() - shortClosingPrices[shortClosingPrices.Count - 3]) / shortClosingPrices[shortClosingPrices.Count - 3] * 100;
                    if (Math.Abs(recentChange) > 0.1) // 超过0.1%的变化才考虑
                    {
                        shortTrendScore += recentChange * 0.5; // 根据变化幅度调整分数
                    }
                    
                    // 将短周期趋势得分加入总得分，权重为0.2（增加权重，更重视短周期信号）
                    totalScore += shortTrendScore * 0.2;
                    
                    // 日志输出短周期辅助分析结果
                    analysis_Log.Text += $"📊 短周期辅助分析: 短周期RSI={shortRsi:F2}, 短周期MA={shortMa:F2}, 短周期MACD={shortMacd.MACDLine:F4}/{shortMacd.SignalLine:F4}, 短周期趋势得分={shortTrendScore:F2}\n";
                }
                catch (Exception ex)
                {
                    analysis_Log.Text += $"❌ 短周期K线分析失败: {ex.Message}\n";
                }
            }

            // 【优化10：根据不同周期调整信号过滤条件】平衡信号质量和数量，提高胜率
            // 1. 针对15m周期（10分钟）的特殊处理，降低阈值，更容易产生信号
            if (interval.ToLower() == "15m")
            {
                // 1. 强信号：核心指标共振且综合得分适中
                if (isTrendBullish && totalScore > 1.2) {
                    return "上涨";
                } else if (isTrendBearish && totalScore < -1.2) {
                    return "下跌";
                }
                // 2. 趋势信号：MACD和MA指示同一方向
                else if (Math.Abs(macdScore) > 2.0 && Math.Abs(maScore) > 1.5 && Math.Sign(macdScore) == Math.Sign(maScore)) {
                    return macdScore > 0 ? "上涨" : "下跌";
                }
                // 3. 量价共振信号：成交量确认趋势
                else if (Math.Abs(volumeScore) > 2.0 && (Math.Abs(macdScore) > 1.0 || Math.Abs(maScore) > 1.0)) {
                    return volumeScore > 0 ? "上涨" : "下跌";
                }
                // 4. 综合得分信号：综合得分适中
                else if (Math.Abs(totalScore) > 1.5) {
                    return totalScore > 0 ? "上涨" : "下跌";
                }
                // 5. 核心指标双重确认
                else if ((Math.Abs(macdScore) > 1.5 && Math.Abs(maScore) > 1.5) ||
                         (Math.Abs(macdScore) > 1.5 && Math.Abs(volumeScore) > 1.5) ||
                         (Math.Abs(maScore) > 1.5 && Math.Abs(volumeScore) > 1.5)) {
                    return totalScore > 0 ? "上涨" : "下跌";
                }
                // 6. 单个核心指标强烈指示
                else if (Math.Abs(macdScore) > 3.0 || Math.Abs(maScore) > 3.0) {
                    return macdScore > 0 ? "上涨" : "下跌";
                }
                // 7. 短线特殊信号：RSI和随机指标共振
                else if (Math.Abs(rsiScore) > 1.5 && Math.Abs(stochasticScore) > 1.5 && Math.Sign(rsiScore) == Math.Sign(stochasticScore)) {
                    return rsiScore > 0 ? "上涨" : "下跌";
                }
                // 8. 短线特殊信号：布林带突破
                else if (Math.Abs(bollingerScore) > 3.0) {
                    return bollingerScore > 0 ? "上涨" : "下跌";
                }
            }
            // 其他周期使用原有条件
            else
            {
                // 1. 强信号：核心指标共振且综合得分较高
                if (isTrendBullish && totalScore > 1.8) {
                    return "上涨";
                } else if (isTrendBearish && totalScore < -1.8) {
                    return "下跌";
                }
                // 2. 趋势强烈信号：MACD和MA强烈指示同一方向
                else if (Math.Abs(macdScore) > 3.0 && Math.Abs(maScore) > 2.5 && Math.Sign(macdScore) == Math.Sign(maScore)) {
                    return macdScore > 0 ? "上涨" : "下跌";
                }
                // 3. 量价共振信号：成交量确认趋势
                else if (Math.Abs(volumeScore) > 3.0 && (Math.Abs(macdScore) > 1.5 || Math.Abs(maScore) > 1.5)) {
                    return volumeScore > 0 ? "上涨" : "下跌";
                }
                // 4. 高综合得分信号：综合得分较高
                else if (Math.Abs(totalScore) > 2.5) {
                    return totalScore > 0 ? "上涨" : "下跌";
                }
                // 5. 核心指标双重确认
                else if ((Math.Abs(macdScore) > 2.0 && Math.Abs(maScore) > 2.0) ||
                         (Math.Abs(macdScore) > 2.0 && Math.Abs(volumeScore) > 2.0) ||
                         (Math.Abs(maScore) > 2.0 && Math.Abs(volumeScore) > 2.0)) {
                    return totalScore > 0 ? "上涨" : "下跌";
                }
                // 6. 单个核心指标强烈指示
                else if (Math.Abs(macdScore) > 4.0 || Math.Abs(maScore) > 4.0) {
                    return macdScore > 0 ? "上涨" : "下跌";
                }
            }
            // 其余情况：观望
            return "中性(建议观望)";
        }
    }
}