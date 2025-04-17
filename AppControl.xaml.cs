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

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for AppControl.xaml
    /// </summary>
    public partial class AppControl : Window
    {
        private System.Threading.Thread? thread2 = null;
        private Process process = new();
        public AppControl()
        {
            InitializeComponent();
        }

        public void RunProcess(string fileName, string args, string workingDir, string customEnvVars = "")
        {
            var threadParameters = new System.Threading.ThreadStart(delegate { ThreadProc(fileName, args, workingDir, customEnvVars); });
            thread2 = new System.Threading.Thread(threadParameters);
            thread2.Start();
            Title = fileName;
            LabelExecutable.Content = "Executable: " + fileName;
            LabelWorkingDir.Content = "Initial Working dir: " + workingDir;
            LabelWorkingDir.Uid = workingDir;
            ExecutableArgs.Text = args;
            Show();
        }

        private void ThreadProc(string fileName, string args, string workingDir, string customEnvVars = "")
        {
            process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.UseShellExecute = true;

            if (!string.IsNullOrWhiteSpace(customEnvVars))
            {
                EnvVarsEditor.ModifyProcessEnvVars(process, customEnvVars);
                process.StartInfo.UseShellExecute = false;
            }
            process.StartInfo.Arguments = EnvVarsEditor.ApplyProcessEnvVars(args, process);
            process.StartInfo.WorkingDirectory = EnvVarsEditor.ApplyProcessEnvVars(workingDir, process);
            if (string.IsNullOrWhiteSpace(process.StartInfo.WorkingDirectory))
            {
                process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(fileName);
            }

            try
            {
                process.Start();
                this.Dispatcher.Invoke(
                    new Action(() => { LabelProcessInfo.Content = "Process Info, ID: " + process.Id; })
                );
                process.WaitForExit();
            }
            catch { }
            this.Dispatcher.Invoke(
                new Action(() => { this.Close(); })
            );
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("Are you sure want to kill the process?", "Process Kill", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }
            try
            {
                process.Kill(true);
            }
            catch { }
        }

        private void LabelWorkingDir_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", LabelWorkingDir.Uid);
        }
    }
}
