using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OVChecker.Tools
{
    /// <summary>
    /// Interaction logic for PassViewer.xaml
    /// </summary>
    public partial class GPUMemPool : Window
    {
        private System.Threading.Thread? ParsingThread = null;
        public bool StopThread = false;
        public string source_file = string.Empty;
        public GPUMemPool()
        {
            InitializeComponent();
        }
        public void ParseFile(string filename)
        {
            source_file = filename;
            var thread_parameters = new System.Threading.ThreadStart(delegate { ParseFileInternal(filename); });
            ParsingThread = new System.Threading.Thread(thread_parameters);
            ParsingThread.Start();
        }
        public static void CopyResToFile(string resourceName, string outputPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    throw new FileNotFoundException("Resource not found: " + resourceName);

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
        }
        public void ParseFileInternal(string filename)
        {
            Dispatcher.Invoke(() => { SplashScreen.ShowWindow("Parsing log..."); });

            string work_dir = MainWindow.instance!.WorkDir;
            string script_file = work_dir + "gpu_mem_pool.py";
            string output_file = work_dir + "gpu_mem_pool.jpg";

            if (System.IO.File.Exists(script_file))
            {
                System.IO.File.Delete(script_file);
            }
            CopyResToFile("OVChecker.Tools.GpuMemPool.py", script_file);

            Process process = new Process();
            process.StartInfo.FileName = MainWindow.instance!.PythonPath;
            process.StartInfo.WorkingDirectory = MainWindow.instance!.WorkDir;
            process.StartInfo.Arguments = "-m pip install pillow";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string err = process.StandardError.ReadToEnd();
            process.WaitForExit();

            process = new Process();
            process.StartInfo.FileName = MainWindow.instance!.PythonPath;
            process.StartInfo.WorkingDirectory = MainWindow.instance!.WorkDir;
            process.StartInfo.Arguments = "\"" + script_file + "\" \"" + filename + "\" \"" + output_file + "\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
//            process.StartInfo.RedirectStandardOutput = false;
//            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            err = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Dispatcher.Invoke(() =>
            {
                SplashScreen.CloseWindow();
                TextLog.AppendText(output);
                TextLog.AppendText(err);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(output_file, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                Img.Source = bitmap;
            });
        }

        private void MnuOpen_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openDialog = new()
            {
                Filter = "Log and text files|*.log;*.txt|All files (*.*)|*.*",
                DefaultExt = "*.log;*.txt"
            };

            var result = openDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;

            ParseFile(openDialog.FileName);
        }
        private void MnuClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
