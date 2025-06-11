using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using OVChecker.Tools;

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow? instance;
        public static string python_bin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python";
        public OVFrontends ModelsFrontend { get; set; } = OVFrontends.Any;
        public SelfUpdate UpdateChecker { get; set; } = new();
        public Process? NetronServer { get; set; } = null;
        public MainWindow()
        {
            instance = this;
            SplashScreen.ShowWindow();
            InitializeComponent();
            if (Properties.Settings.Default.IsUpgraded == false)
            {
                Properties.Settings.Default.Upgrade();
            }
            Properties.Settings.Default.IsUpgraded = true;
            OVChecksRegistry.RegisterChecks();
            ListViewChecks.ItemsSource = OVChecks;
            ListViewPytests.ItemsSource = Pytests;
            if (Properties.Settings.Default.WorkDirs != null)
            {
                foreach (var item in Properties.Settings.Default.WorkDirs)
                {
                    CBoxWorkDir.Items.Add(new ComboBoxItem() { Content = item });
                }
            }
            if (Properties.Settings.Default.WorkDir != "")
            {
                SplashScreen.SetStatus("Updating available OpenVINO versions...");
                SelectOrAddComboBoxItem(CBoxWorkDir, Properties.Settings.Default.WorkDir);
                CBoxPythonPathReset();
            }
            else
            {
                string default_wd = AppDomain.CurrentDomain.BaseDirectory + "DefaultWorkDir";
                if (!System.IO.Directory.Exists(default_wd))
                {
                    System.IO.Directory.CreateDirectory(default_wd);
                }
                if (System.IO.Directory.Exists(default_wd))
                {
                    WorkDir = default_wd;
                }
            }
            if (Properties.Settings.Default.ModelPaths != null)
            {
                foreach (var item in Properties.Settings.Default.ModelPaths)
                {
                    CBoxModelPath.Items.Add(new ComboBoxItem() { Content = item });
                }
            }
            if (Properties.Settings.Default.ModelPath != "")
            {
                SelectOrAddComboBoxItem(CBoxModelPath, Properties.Settings.Default.ModelPath);
                DetectFrontend();
            }
            if (Properties.Settings.Default.PytestsPaths != null)
            {
                foreach (var item in Properties.Settings.Default.PytestsPaths)
                {
                    CBoxPytestsPath.Items.Add(new ComboBoxItem() { Content = item });
                }
            }
            /*
            // Need to think - is it necessary to choose it on run?..
            if (Properties.Settings.Default.PytestsPath != "")
            {
                SelectOrAddComboBoxItem(CBoxPytestsPath, Properties.Settings.Default.PytestsPath);
            }
            */
            ShowApplicableChecks();
            if (IsNetronAvailable())
            {
                ButtonOpenNetron.IsEnabled = true;
                ButtonUpdateNetron.Content = "Reload Netron";
            }
            ButtonUpdate.Visibility = Visibility.Hidden;
            ButtonNewVersion.Visibility = Visibility.Hidden;
            LabelUpdateProgress.Visibility = Visibility.Hidden;
            UpdateChecker.CheckUpdates();
            UpdateAboutTab();
            SplashScreen.CloseWindow();
        }
        public void UpdateAboutTab()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
            LabelVersion.Content = "Version: " + version;
            ButtonBugReport.Uid += " (" + version + ")";
            ButtonFeatureRequest.Uid += " (" + version + ")";
        }
        public void ShowButtonUpdate()
        {
            ButtonUpdate.Visibility = Visibility.Visible;
            LabelUpdateProgress.Visibility = Visibility.Hidden;
            ButtonNewVersion.Visibility = Visibility.Hidden;
        }
        public void DetectFrontend()
        {
            if (System.IO.Directory.Exists(ModelPath))
            {
                ModelsFrontend = OVFrontends.TF;
                return;
            }
            var ext = System.IO.Path.GetExtension(ModelPath)!.ToLower();
            switch (ext)
            {
                case ".onnx": ModelsFrontend = OVFrontends.ONNX; break;
                case ".pb": ModelsFrontend = OVFrontends.TF; break;
                case ".tflite": ModelsFrontend = OVFrontends.TFLite; break;
                case ".xml": ModelsFrontend = OVFrontends.IR; break;
                default: ModelsFrontend = OVFrontends.Any; break;
            }
        }
        public OVCheckItem? GetOVCheckItemByGUID(string GUID)
        {
            foreach (var item in OVChecks)
            {
                if (item.GUID == GUID) return item;
            }
            return null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TabTools.SelectedIndex = 0;
        }

        private void PrepareEnvironment(List<AppOutput.ProcessItem> tasks, string python_path, ref string? custom_env)
        {
            string pip_flags = "";
            if (CBPIPQuite.IsChecked == true)
            {
                pip_flags = " -q";
            }
            if (CBoxOpenVINOPath.SelectedItem is ComboBoxOpenVINOItem)
            {
                var item = (CBoxOpenVINOPath.SelectedItem as ComboBoxOpenVINOItem)!;
                tasks.Add(new() { Name = python_path, Args = "-m pip install --upgrade --disable-pip-version-check openvino==" + item.Version + pip_flags, WorkingDir = WorkDir, NoWindow = true });
            }
            else if (CBoxOpenVINOPath.SelectedItem != null)
            {
                tasks.Add(new() { Name = python_path, Args = "-m pip uninstall --disable-pip-version-check -y openvino" + pip_flags, WorkingDir = WorkDir, NoWindow = true });
                tasks.Add(new() { Name = python_path, Args = "-m pip install --disable-pip-version-check numpy" + pip_flags, WorkingDir = WorkDir, NoWindow = true });
                string openvino_path = (CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                if (openvino_path.ToLower().EndsWith(".whl"))
                {
                    tasks.Add(new() { Name = python_path, Args = "-m pip install --disable-pip-version-check --upgrade \"" + openvino_path + "\"" + pip_flags, WorkingDir = WorkDir, NoWindow = true });
                }
                else
                {
                    custom_env = "OPENVINO_LIB_PATHS=" + CBoxOpenVINOPath.Text + "\nPYTHONPATH=" + CBoxOpenVINOPath.Text + "python\\;" + CBoxOpenVINOPath.Text + "..\\..\\..\\tools\\ovc\\;";
                }
            }
        }
        private void ButtonRunSelected_Click(object sender, RoutedEventArgs e)
        {
            List<AppOutput.ProcessItem> tasks = new();

            string python_path = PythonPath;
            string? custom_env = null;

            PrepareEnvironment(tasks, python_path, ref custom_env);

            for (int i = 0; i < OVChecks.Count; i++)
            {
                var item = OVChecks[i]!;

                if (item.Selected == false) continue;

                PrepareCheck(item, tasks, python_path, custom_env);
            }

            if (tasks.Count <= 0) return;

            AppOutput app = new();
            app.RunProcess("Checks: " + ModelPath, tasks, WorkDir + "latest.log");
        }

        private void ItemStatusHandler(object? source, AppOutput.ProcessStatus status)
        {
            if (source is OVCheckItem)
            {
                string status_str = "unknown";
                switch (status)
                {
                    case AppOutput.ProcessStatus.OK: status_str = "ok"; break;
                    case AppOutput.ProcessStatus.ERROR: status_str = "error"; break;
                }
                (source as OVCheckItem)!.Status = status_str;
            }
        }

        private void ButtonBrowseModelPath_Click(object sender, RoutedEventArgs e)
        {
            string allKnownExts = "*.xml; *.onnx; *.pb; *.pbtxt; *.meta; *.caffee; *.tflite";
            System.Windows.Forms.OpenFileDialog openDialog = new()
            {
                Filter = "Known models|" + allKnownExts + "|All files (*.*)|*.*",
                DefaultExt = allKnownExts,
            };

            if (!string.IsNullOrEmpty(CBoxModelPath.Text))
            {
                if (System.IO.Directory.Exists(CBoxModelPath.Text))
                    openDialog.InitialDirectory = CBoxModelPath.Text;
                else
                    openDialog.InitialDirectory = System.IO.Path.GetDirectoryName(CBoxModelPath.Text);
            }

            var result = openDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;

            if (openDialog.FileName.EndsWith("saved_model.pb"))
            {
                SelectOrAddComboBoxItem(CBoxModelPath, System.IO.Path.GetDirectoryName(openDialog.FileName)!.Replace("/", "\\"));
            }
            else
            {
                SelectOrAddComboBoxItem(CBoxModelPath, openDialog.FileName.Replace("/", "\\"));
            }
            ShowApplicableChecks();
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button)
            {
                if ((sender as System.Windows.Controls.Button)!.Uid == "None")
                {
                    foreach (var item in OVChecks)
                        item.Selected = false;
                }
                else if ((sender as System.Windows.Controls.Button)!.Uid == "Status")
                {
                    foreach (var item in OVChecks)
                        item.Status = "unknown";
                }
                else
                {
                    foreach (var item in OVChecks)
                        item.Selected = true;
                }
                CollectionViewSource.GetDefaultView(OVChecks).Refresh();
            }
        }

        private void ButtonSingleCheckRun_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button)) { return; }

            List<AppOutput.ProcessItem> tasks = new();

            string python_path = PythonPath;
            string? custom_env = null;

            PrepareEnvironment(tasks, python_path, ref custom_env);
            var item = GetOVCheckItemByGUID((sender as System.Windows.Controls.Button)!.Uid);
            PrepareCheck(item!, tasks, python_path, custom_env);

            if (tasks.Count <= 0) return;

            AppOutput app = new();
            app.RunProcess("Single Check: " + ModelPath, tasks, WorkDir + "latest.log");
        }

        private void ButtonOpenModelsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ModelPath)) { return; }
            if (System.IO.File.Exists(ModelPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + ModelPath.Replace("\"", "\\\"") + "\"");
            }
            else if (System.IO.Directory.Exists(ModelPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + ModelPath.Replace("\"", "\\\"") + "\"");
            }
        }

        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Application will be closed. Do you want to install update right now?", "Update", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            string app_path = AppDomain.CurrentDomain.BaseDirectory.Replace("/", "\\");
            if (!app_path.EndsWith("\\")) app_path += "\\";

            System.IO.File.WriteAllText(app_path + "update.cmd", "@echo off\n" +
                "echo OVChecker Updater\n" +
                "echo Press Ctrl + C if you want to stop an updating process\n" +
                "pause\n" +
                "del /q \"%~dp0\\*.exe\"\n" +
                "del /q \"%~dp0\\*.dll\"\n" +
                "del /q \"%~dp0\\*.config\"\n" +
                "copy /y \"%~dp0\\Updates\\" + UpdateChecker.LatestVersion + "\\*\" \"%~dp0\\\"\n" +
                "start OVChecker.exe\n");
            System.Diagnostics.Process.Start("cmd.exe", "/c \"" + app_path + "update.cmd\"");
            Application.Current.Shutdown();
        }
        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo((sender as Button)!.Uid) { UseShellExecute = true });
            }
            catch { }
        }

        private void TabTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabTools.SelectedIndex == 1 || TabTools.SelectedIndex == 2)
            {
                if (WorkDir == "")
                {
                    MessageBox.Show("Please, set a Work Dir before continue.", "Work Dir", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TabTools.SelectedIndex = 0;
                    return;
                }
                if (PythonPath == "")
                {
                    MessageBox.Show("Please, set a Python Path before continue.", "Python Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TabTools.SelectedIndex = 0;
                    return;
                }
                if (OVPath == "")
                {
                    MessageBox.Show("Please, select an OpenVINO before continue.", "OpenVINO", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TabTools.SelectedIndex = 0;
                    return;
                }
            }
        }

        private void CBoxWorkDir_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBoxWorkDir.SelectedItem is ComboBoxItem)
            {
                WorkDir = (CBoxWorkDir.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                CBoxPythonPathReset();
            }
        }

        private void CBoxModelPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBoxModelPath.SelectedItem is ComboBoxItem)
            {
                ModelPath = (CBoxModelPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                DetectFrontend();
                ShowApplicableChecks();
            }
        }

        private void ButtonDownloadWheel_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(TextWheelURL.Text))
            {
                MessageBox.Show("Please, enter a Wheel URL");
                return;
            }
            string url = TextWheelURL.Text;
            if (url.Length < 20 || url.Substring(0, 4).ToLower() != "http" || url.Substring(url.Length - 4).ToLower() != ".whl")
            {
                MessageBox.Show("Please, check URL correctness");
                return;
            }
            string filename = System.IO.Path.GetFileName(url);
            if (System.IO.File.Exists(WorkDir + filename) && MessageBox.Show("File " + filename + " exists, overwrite?", "Downloading Wheel", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            var update_progress = (int total) =>
            {
                SplashScreen.instance!.Dispatcher.Invoke(() =>
                {
                    if (SplashScreen.instance != null)
                    {
                        SplashScreen.SetStatus("Downloading Wheel: " + ((float)total / (1024 * 1024)).ToString("0.00") + "Mb");
                    }
                });
            };
            SplashScreen.ShowWindow("Downloading Wheel...");
            try
            {
                if (!WebRequest.DownloadFile(url, WorkDir + filename, update_progress, true))
                {
                    System.Windows.MessageBox.Show("Download failed, please download file manually and store as " + filename + " in Work Dir");
                    return;
                }
                MessageBox.Show("Wheel has been downloaded!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                SelectOrAddComboBoxItem(CBoxOpenVINOPath, WorkDir + filename);
            }
            catch { }
            SplashScreen.CloseWindow();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (NetronServer != null)
            {
                NetronServer.StandardInput.Write(26);
                NetronServer.StandardInput.Flush();
                NetronServer.StandardInput.Close();
                try
                {
                    System.Threading.Thread.Sleep(200);
                    NetronServer.Kill();
                }
                catch { }
            }
            UpdateChecker.Stop();
            foreach (var wnd in Application.Current.Windows)
            {
                (wnd as Window)!.Close();
            }
        }

        private void ButtonBrowsePytestsPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openDialog = new()
            {
                Filter = "Python files with tests|*.py|All files (*.*)|*.*",
                DefaultExt = "*.py"
            };

            var result = openDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;

            SelectOrAddComboBoxItem(CBoxPytestsPath, openDialog.FileName.Replace("/", "\\"));
        }

        private void ButtonBrowsePytestsFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openDialog = new()
            {
                Description = "Select a folder for Pytests lookup",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            var result = openDialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;

            string folder = openDialog.SelectedPath.Replace("/", "\\");
            if (!folder.EndsWith("\\")) folder += "\\";
            SelectOrAddComboBoxItem(CBoxPytestsPath, folder);
        }

        private void CBoxPytestsPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBoxPytestsPath.SelectedItem is ComboBoxItem)
            {
                PytestsPath = (CBoxPytestsPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                ShowApplicablePytests();
            }
        }

        private void ButtonRefreshPytests_Click(object sender, RoutedEventArgs e)
        {
            if (CBoxPytestsPath.SelectedItem is ComboBoxItem)
            {
                PytestsPath = (CBoxPytestsPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                ShowApplicablePytests();
            }
        }

        private void ButtonSelectPytests_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button)
            {
                if ((sender as System.Windows.Controls.Button)!.Uid == "None")
                {
                    foreach (var item in Pytests)
                        item.Selected = false;
                }
                else if ((sender as System.Windows.Controls.Button)!.Uid == "Status")
                {
                    foreach (var item in Pytests)
                        item.Status = "unknown";
                }
                else
                {
                    foreach (var item in Pytests)
                        item.Selected = true;
                }
                CollectionViewSource.GetDefaultView(Pytests).Refresh();
            }
        }

        private void ButtonSinglePytestRun_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button)) { return; }

            List<AppOutput.ProcessItem> tasks = new();

            string python_path = PythonPath;
            string? custom_env = null;

            PrepareEnvironment(tasks, python_path, ref custom_env);
            var item = GetPytestItemByGUID((sender as System.Windows.Controls.Button)!.Uid);
            PreparePytest(item!, tasks, python_path, custom_env);

            if (tasks.Count <= 0) return;

            AppOutput app = new();
            app.RunProcess("Single Test: " + item!.Name, tasks, WorkDir + "latest.log");
        }

        private void ButtonRunSelectedPytests_Click(object sender, RoutedEventArgs e)
        {
            List<AppOutput.ProcessItem> tasks = new();

            string python_path = PythonPath;
            string? custom_env = null;

            PrepareEnvironment(tasks, python_path, ref custom_env);
            PreparePytest(null, tasks, python_path, custom_env);

            if (tasks.Count <= 0) return;

            AppOutput app = new();
            app.RunProcess("Pytest: " + PytestsPath, tasks, WorkDir + "latest.log");
        }

        private void ButtonPipControl_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button)) return;
            string action = (sender as Button)!.Uid;
            string python_path = PythonPath;
            string? custom_env = null;
            List<AppOutput.ProcessItem> tasks = new();

            if (action == "pip_list")
            {
                AppOutput app = new();
                tasks.Add(new() { Name = python_path, Args = "-m pip list", WorkingDir = WorkDir, CustomEnvVars = custom_env, NoWindow = true });
                app.RunProcess("pip list", tasks, WorkDir + "pip.log");
            }
            else if (action == "pip_install")
            {
                if (string.IsNullOrEmpty(TextPipInstall.Text))
                {
                    MessageBox.Show("Please, enter a list of PyPi packages into the text box", "Empty packages", MessageBoxButton.OK, MessageBoxImage.Information);
                    TextPipInstall.Focus();
                    return;
                }
                AppOutput app = new();
                tasks.Add(new() { Name = python_path, Args = "-m pip install " + TextPipInstall.Text, WorkingDir = WorkDir, CustomEnvVars = custom_env, NoWindow = true });
                app.RunProcess("pip install " + TextPipInstall.Text, tasks, WorkDir + "pip.log");
            }
        }

        private void ButtonNetron_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button)) return;
            string action = (sender as Button)!.Uid;
            List<AppOutput.ProcessItem> tasks = new();

            if (action == "open")
            {
                if(!IsNetronAvailable())
                {
                    MessageBox.Show("Netron isn\'t detected, press \"Download Netron\" button to download and install Netron for the chosen Python");
                    ButtonOpenNetron.IsEnabled = false;
                    return;
                }

                //                AppControl app = new();
                //                app.RunProcess(PythonPath, "-c \"import netron; netron.main()\"", WorkDir);
                if (NetronServer != null)
                {
                    Process.Start("explorer.exe", "http://localhost:8001");
                }
                else
                {
                    NetronServer = new Process();
                    NetronServer.StartInfo.FileName = PythonPath;
                    NetronServer.StartInfo.Arguments = "-c \"import sys; sys.path.append(\\\"netron-main/dist/pypi\\\"); import netron; netron.main()\" -p 8001";
                    NetronServer.StartInfo.WorkingDirectory = WorkDir;
                    NetronServer.StartInfo.UseShellExecute = false;
                    NetronServer.StartInfo.RedirectStandardInput = true;
                    NetronServer.StartInfo.CreateNoWindow = true;
                    NetronServer.Start();
                    System.Threading.Thread.Sleep(200);
                    if(NetronServer.HasExited)
                    {
                        MessageBox.Show("Something wrong went with starting Netron. You may need to reboot your PC.");
                        NetronServer = null;
                        return;
                    }
                    Process.Start("explorer.exe", "http://localhost:8001");
                }
            }
            else if (action == "update")
            {
                UpdateNetron();
            }
        }

        private void ButtonPassViewer_Click(object sender, RoutedEventArgs e)
        {
            PassViewer passViewer = new PassViewer();
            passViewer.Show();
        }

        private void ButtonMatchesViewer_Click(object sender, RoutedEventArgs e)
        {
            MatchesViewer matchesViewer = new MatchesViewer();
            matchesViewer.Show();
        }

        private void ButtonGPUMemPool_Click(object sender, RoutedEventArgs e)
        {
            GPUMemPool gpuMemPool = new GPUMemPool();
            gpuMemPool.Show();
        }
    }
}