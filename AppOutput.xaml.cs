using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using OVChecker.Tools;

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for AppOutput.xaml
    /// </summary>
    public partial class AppOutput : Window
    {
        private System.Threading.Thread? thread2 = null;
        private Process? process = null;
        private StreamWriter? outputLog = null;
        public bool IsWorkflowFinished = true;
        public bool ThreadStop = false;
        private const int OutputBufCapacity = 128 * 1024;
        private long OutputLogLength = 0;
        private StringBuilder OutputBuf = new(OutputBufCapacity);
        private long LastOutputFlush = 0;
        private string OutputLogPath = string.Empty;

        public enum ProcessStatus
        {
            UNKNOWN = 0, OK = 1, ERROR = 2,
        }

        public delegate bool CustomProcessItemHandler();
        public delegate void ProcessItemStatusHandler(object? source, ProcessStatus status);
        public class ProcessItem
        {
            public string Name { get; set; }
            public string Args { get; set; }
            public string WorkingDir { get; set; }
            public string? CustomEnvVars { get; set; }
            public CustomProcessItemHandler? CustomHandler { get; set; }
            public ProcessItemStatusHandler? StatusHandler { get; set; }
            public bool DontStopOnError { get; set; }
            public bool NoWindow { get; set; }
            public string Uid { get; set; }
            public object? ItemSource { get; set; }
            // List of forced tool buttons delimited by ";"
            public string ForcedTools { get; set; }

            public ProcessItem() { Name = ""; Args = ""; WorkingDir = ""; DontStopOnError = false; Uid = ""; NoWindow = false; ForcedTools = ""; }
            public string toString()
            {
                return Name + Args + WorkingDir + CustomEnvVars;
            }
        }

        public AppOutput()
        {
            InitializeComponent();
            MnuDarkSkin.IsChecked = Properties.Settings.Default.DarkSkin;
            MnuDarkSkin_Click(MnuDarkSkin, null);
        }

        public void RunProcess(string windowTitle, List<ProcessItem> workflow, string? OutputFileName = null)
        {
            if (workflow.Count == 0) { return; }
            var threadParameters = new System.Threading.ThreadStart(delegate { ThreadProc(workflow, OutputFileName); });
            ThreadStop = false;
            IsWorkflowFinished = false;
            thread2 = new System.Threading.Thread(threadParameters);
            thread2.Start();
            Dispatcher.Invoke(delegate
            {
                Title = windowTitle;
                List<string> ListedFolders = new();
                foreach (var item in workflow)
                {
                    if (ListedFolders.Contains(item.WorkingDir)) continue;
                    MenuItem mnuItem = new MenuItem() { Header = item.WorkingDir };
                    mnuItem.Click += MnuFolders_Click;
                    MnuFolders.Items.Add(mnuItem);
                    ListedFolders.Add(item.WorkingDir);
                }
                if (OutputFileName != null)
                {
                    BtnOpenLogFolder.Uid = OutputFileName;
                }
                else
                {
                    BtnOpenLogFolder.Visibility = Visibility.Hidden;
                }
                Show();
            });
        }

        public void RunProcess(string windowTitle, string Name, string Args, string WorkingDir, string? CustomEnvVars = null, string? OutputFileName = null)
        {
            RunProcess(windowTitle, new List<ProcessItem> { new ProcessItem { Name = Name, Args = Args, WorkingDir = WorkingDir, CustomEnvVars = CustomEnvVars } }, OutputFileName);
        }

        private void ThreadProc(List<ProcessItem> workflow, string? OutputFileName = null)
        {
            if (OutputFileName != null)
            {
                outputLog = new StreamWriter(OutputFileName);
                OutputLogPath = OutputFileName;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            foreach (ProcessItem item in workflow)
            {
                if (ThreadStop) { break; }

                if (item.CustomHandler != null)
                {
                    OutputHandler("Custom Handler: " + item.Name);
                    if (item.CustomHandler())
                    {
                        OutputHandler("Succeeded");
                    }
                    else
                    {
                        OutputHandler("Failed");
                        if (!item.DontStopOnError)
                        {
                            break;
                        }
                    }
                    continue;
                }

                process = new Process();
                process.StartInfo.FileName = item.Name;
                if (!string.IsNullOrWhiteSpace(item.CustomEnvVars))
                {
                    EnvVarsEditor.ModifyProcessEnvVars(process, item.CustomEnvVars);
                }
                process.StartInfo.Arguments = EnvVarsEditor.ApplyProcessEnvVars(item.Args, process);
                process.StartInfo.WorkingDirectory = EnvVarsEditor.ApplyProcessEnvVars(item.WorkingDir, process);
                if (string.IsNullOrWhiteSpace(process.StartInfo.WorkingDirectory))
                {
                    process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(item.Name);
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = item.NoWindow;

                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);

                OutputHandler("CMD: " + item.Name + " " + process.StartInfo.Arguments);

                ProcessStatus status = ProcessStatus.UNKNOWN;

                try
                {
                    process.Start();
                    OutputHandler(">>> Process ID: " + process.Id);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    status = process.ExitCode == 0 ? ProcessStatus.OK : ProcessStatus.ERROR;
                    if (process.ExitCode != 0 && !item.DontStopOnError)
                    {
                        if (item.StatusHandler != null)
                        {
                            Dispatcher.Invoke(new(() =>
                            {
                                item.StatusHandler(item.ItemSource, status);
                            }), System.Windows.Threading.DispatcherPriority.Input);
                        }
                        //if(MessageBox.Show("Execution of a last command was failed. Probably, it might be fixed manually.\n\n", "Execution failure"))
                        break;
                    }
                    process.Close();
                }
                catch (Exception e)
                {
                    status = ProcessStatus.ERROR;
                    OutputHandler("Error while starting external application: " + e.Message);
                    if (!item.DontStopOnError)
                    {
                        if (item.StatusHandler != null)
                        {
                            Dispatcher.Invoke(new(() =>
                            {
                                item.StatusHandler(item.ItemSource, status);
                            }), System.Windows.Threading.DispatcherPriority.Input);
                        }
                        break;
                    }
                }

                if (item.StatusHandler != null)
                {
                    Dispatcher.Invoke(new(() =>
                    {
                        item.StatusHandler(item.ItemSource, status);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            OutputHandler("Work completed");
            ProcessLog.Dispatcher.Invoke(
                new Action(() =>
                {
                    IsWorkflowFinished = true;
                })
            );
            if (OutputBuf.Length > 0)
            {
                string show_data = OutputBuf.ToString();
                ProcessLog.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        ProcessLog.BeginChange();
                        ProcessLog.AppendText(show_data);
                        if (MnuAutoScroll.IsChecked == true)
                        {
                            try
                            {
                                ProcessLog.CaretIndex = ProcessLog.Text.Length;
                                ProcessLog.ScrollToEnd();
                            }
                            catch
                            { }
                        }
                        ProcessLog.EndChange();
                    }), System.Windows.Threading.DispatcherPriority.Input
                );
                OutputBuf.Clear();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (outputLog != null)
            {
                outputLog.Flush();
                outputLog.Close();
            }
        }
        public void ProcessKill(bool entireProcessTree = false)
        {
            if (process != null)
            {
                try
                {
                    process.StandardInput.Write(26);
                    process.StandardInput.Write("\r\n");
                    process.StandardInput.Flush();
                    process.StandardInput.Close();
                }
                catch { }
                try
                {
                    if (process != null)
                        process.Kill(entireProcessTree);
                }
                catch { }
            }
        }
        public void StopProcessing(bool entireProcessTree = false)
        {
            try
            {
                ThreadStop = true;
                OutputHandler("Requested process kill");
                ProcessKill(entireProcessTree);
                OutputHandler("Process" + (entireProcessTree ? " tree" : "") + " killed");
            }
            catch { }
        }
        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (string.IsNullOrEmpty(outLine.Data)) return;
            long cur_time = Environment.TickCount;
            long update_speed = OutputLogLength > OutputBufCapacity ? 1000 : 100;
            long diff = cur_time - LastOutputFlush;
            if (diff > update_speed && OutputBuf.Length > 0)
            {
                string show_data = OutputBuf.ToString();
                OutputBuf.Clear();
                ProcessLog.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        ProcessLog.BeginChange();
                        if (ProcessLog.Text.Length < OutputBufCapacity && OutputLogLength <= OutputBufCapacity)
                        {
                            ProcessLog.AppendText(show_data);
                        }
                        else
                        {
                            ProcessLog.Text = show_data;
                            ProcessLog.AppendText("\nTask log has " + ((OutputLogLength + 1024 * 1024 - 1) / (1024 * 1024)) + "Mb of captured data and cannot be effectively displayed\n");
                            ProcessLog.AppendText("Displayed only last " + ((ProcessLog.Text.Length + 1024 * 1024 - 1) / (1024 * 1024)) + "Mb\n");
                            if (outputLog != null)
                            {
                                ProcessLog.AppendText("Please, use an external application to view stored log:\n");
                                ProcessLog.AppendText(OutputLogPath);
                            }
                            ProcessLog.AppendText("\n\nTask continues execution...");
                        }
                        show_data = string.Empty;
                        if (MnuAutoScroll.IsChecked == true)
                        {
                            try
                            {
                                ProcessLog.CaretIndex = ProcessLog.Text.Length;
                                ProcessLog.ScrollToEnd();
                            }
                            catch
                            { }
                        }
                        ProcessLog.EndChange();
                    }), System.Windows.Threading.DispatcherPriority.Background
                );
                if (cur_time - LastOutputFlush > 10000)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                LastOutputFlush = cur_time;
            }
            string log_text = Regex.Replace(outLine.Data, @"\e\[[0-9;]*m", "");
            OutputLogLength += log_text.Length;
            if (OutputBuf.Length + log_text.Length + 1 > OutputBufCapacity)
            {
                if (log_text.Length < OutputBufCapacity)
                    OutputBuf.Remove(0, log_text.Length + 1);
                else
                    OutputBuf.Clear();
            }
            if (log_text.Length < OutputBufCapacity)
                OutputBuf.Append(log_text);
            else
                OutputBuf.Append(log_text.Substring(log_text.Length - OutputBufCapacity - 1));
            OutputBuf.Append("\n");

            if (outputLog != null)
            {
                outputLog.WriteLine(log_text);
                outputLog.Flush();
            }
        }
        private void OutputHandler(string outLine)
        {
            long cur_time = Environment.TickCount;
            if ((cur_time - LastOutputFlush > 100 || OutputBuf.Length + outLine.Length + 1 > OutputBufCapacity) && OutputBuf.Length > 0)
            {
                string show_data = OutputBuf.ToString();
                OutputLogLength += OutputBuf.Length;
                OutputBuf.Clear();
                ProcessLog.Dispatcher.Invoke(
                new Action(() =>
                {
                    ProcessLog.BeginChange();
                    if (ProcessLog.Text.Length < OutputBufCapacity)
                    {
                        ProcessLog.AppendText(show_data);
                    }
                    else
                    {
                        ProcessLog.Text = show_data;
                    }
                    if (MnuAutoScroll.IsChecked == true)
                    {
                        try
                        {
                            ProcessLog.CaretIndex = ProcessLog.Text.Length;
                            ProcessLog.ScrollToEnd();
                        }
                        catch
                        { }
                    }
                    ProcessLog.EndChange();
                }), System.Windows.Threading.DispatcherPriority.Background
                );
                LastOutputFlush = cur_time;
            }
            OutputBuf.Append(outLine);
            OutputBuf.Append("\n");
            if (outputLog != null)
            {
                outputLog.WriteLine(outLine);
                outputLog.Flush();
            }
        }
        private void MnuClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MnuSave_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files|*.txt",
                DefaultExt = "*.txt"
            };
            bool? result = saveDialog.ShowDialog();
            if (result != true) return;

            System.IO.File.WriteAllText(saveDialog.FileName, ProcessLog.Text);
        }
        private void MnuKill_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("Are you sure want to kill the process?", "Process Kill", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }
            try
            {
                switch (System.Windows.MessageBox.Show("Should stop processing further tasks?\n\nYes - stop current process and stop further processing\nNo - stop current process and continue if possible\nCancel - do not kill process", "Stopping processing...", MessageBoxButton.YesNoCancel))
                {
                    case MessageBoxResult.Yes:
                        ThreadStop = true;
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
                ProcessKill(true);
            }
            catch { }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            int pos = ProcessLog.CaretIndex, newPos = ProcessLog.Text.IndexOf(TextSearch.Text, pos, StringComparison.InvariantCultureIgnoreCase);
            if (pos == newPos && pos < ProcessLog.Text.Length)
            {
                ++pos;
                newPos = ProcessLog.Text.IndexOf(TextSearch.Text, pos, StringComparison.InvariantCultureIgnoreCase);
            }
            if (pos > 0 && newPos == -1)
            {
                pos = 0;
                newPos = ProcessLog.Text.IndexOf(TextSearch.Text, pos, StringComparison.InvariantCultureIgnoreCase);
            }
            if (newPos == -1)
            {
                SearchResults.Content = "Nothing found";
                return;
            }
            SearchResults.Content = "";
            ProcessLog.Focus();
            ProcessLog.SelectionStart = newPos;
            ProcessLog.SelectionLength = TextSearch.Text.Length;
            TextSearch.Focus();
        }

        private void TextSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnSearch_Click(sender, new());
        }

        private void MnuWordWrap_Click(object sender, RoutedEventArgs e)
        {
            ProcessLog.TextWrapping = (sender as MenuItem)!.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }

        private void MnuDarkSkin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)!.IsChecked)
            {
                ProcessLog.Background = System.Windows.Media.Brushes.Black;
                ProcessLog.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                ProcessLog.Background = System.Windows.Media.Brushes.White;
                ProcessLog.Foreground = System.Windows.Media.Brushes.Black;
            }
            if (Properties.Settings.Default.DarkSkin != (sender as MenuItem)!.IsChecked)
            {
                Properties.Settings.Default.DarkSkin = (sender as MenuItem)!.IsChecked;
                Properties.Settings.Default.Save();
            }
        }
        private void MnuFolders_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem)) { return; }
            System.Diagnostics.Process.Start("explorer.exe", (sender as MenuItem)!.Header.ToString() ?? "");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsWorkflowFinished) { return; }
            switch (System.Windows.MessageBox.Show("Processing tasks, should stop processing and kill tasks?", "Stopping processing...", MessageBoxButton.YesNo))
            {
                case MessageBoxResult.Yes:
                    ThreadStop = true;
                    ProcessKill(true);
                    e.Cancel = false;
                    return;
                case MessageBoxResult.No:
                    e.Cancel = true;
                    return;
            }
            e.Cancel = !IsWorkflowFinished;
        }
        private void ProcessLog_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (process != null)
                {
                    switch (e.Key)
                    {
                        case Key.Enter: process.StandardInput.Write("\r\n"); break;
                    }
                    process.StandardInput.Flush();
                }
            }
            catch { }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ProcessLog.Text = "";
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void BtnOpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button)) { return; }
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + ((sender as Button)!.Uid.ToString() ?? "").Replace("\"", "\\\"") + "\"");
        }

        private void MnuTools_Click(object sender, RoutedEventArgs e)
        {
            var action = (sender as Control)!.Uid;
            if(action == "matches_viewer")
            {
                MatchesViewer matchesViewer = new MatchesViewer();
                matchesViewer.Show();
                matchesViewer.ParseFile(OutputLogPath);
            }
            else if(action == "pass_viewer")
            {
                PassViewer passViewer = new PassViewer();
                passViewer.Show();
                passViewer.ParseFile(OutputLogPath);
            }
            else if (action == "gpu_mem_pool")
            {
                GPUMemPool gpuMemPool = new GPUMemPool();
                gpuMemPool.Show();
                gpuMemPool.ParseFile(OutputLogPath);
            }
        }
    }
}
