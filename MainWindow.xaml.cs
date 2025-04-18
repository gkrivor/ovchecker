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

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow? instance;
        public class ComboBoxBrowseItem : ComboBoxItem
        {
        }
        public class ComboBoxDownloadItem : ComboBoxItem
        {
            public string URL { get; set; }
            public string Path { get; set; }
            public ComboBoxDownloadItem() { URL = ""; Path = ""; }
        }
        public class ComboBoxOpenVINOItem : ComboBoxItem
        {
            public string Version { get; set; }
            public ComboBoxOpenVINOItem() { Version = ""; }
        }
        public class ComboBoxBrowseWheelItem : ComboBoxItem
        {
        }
        private string _WorkDir = "";
        public string WorkDir
        {
            get { return _WorkDir; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (!_value.EndsWith("\\")) _value += "\\";
                if (_WorkDir == _value) return;
                _WorkDir = _value;
                SelectOrAddComboBoxItem(CBoxWorkDir, _value);
                CBoxPythonPathReset();
                Properties.Settings.Default.WorkDir = _value;
                SaveCBoxWorkDirState();
            }
        }
        private string _PythonPath = "";
        public string PythonPath
        {
            get { return _PythonPath; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (!_value.EndsWith("python.exe"))
                {
                    if (!_value.EndsWith("\\")) _value += "\\";
                    _value += "python.exe";
                }
                if (_PythonPath == _value) return;
                _PythonPath = _value;
                SelectOrAddComboBoxItem(CBoxPythonPath, _value);
                CBoxOpenVINOPathReset();
                Properties.Settings.Default.PythonPath = _value;
                SaveCBoxPythonPathsState();
            }
        }
        private string _OVPath = "";
        public string OVPath
        {
            get { return _OVPath; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (!_value.StartsWith("OpenVINO "))
                {
                    if (!_value.EndsWith("\\") && !_value.ToLower().EndsWith(".whl")) _value += "\\";
                }
                if (_OVPath == _value) return;
                _OVPath = _value;
                SelectOrAddComboBoxItem(CBoxOpenVINOPath, _value);
                Properties.Settings.Default.OpenVINOPath = _value;
                SaveCBoxOpenVINOPathsState();
            }
        }
        private string _ModelPath = "";
        public string ModelPath
        {
            get { return _ModelPath; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (_ModelPath == _value) return;
                _ModelPath = _value;
                SelectOrAddComboBoxItem(CBoxModelPath, _value);
                Properties.Settings.Default.ModelPath = _value;
                SaveCBoxModelPathsState();
            }
        }

        public class OVCheckItem : INotifyPropertyChanged
        {
            private bool _Selected;
            public bool Selected { get { return _Selected; } set { _Selected = value; if (instance != null) instance.ShowAvailableChecksCustomizations(); } }
            public string Name { get; set; }
            public string Script { get; set; }
            private string _Status;
            public string Status { get { return _Status; } set { _Status = value; OnPropertyChanged("StatusIcon"); } }
            public string StatusIcon
            {
                get
                {
                    return "pack://application:,,,/OVChecker;component/Resources/status_" + Status + ".bmp";
                }
            }
            public string Requirements { get; set; }
            public string GUID { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            public OVCheckItem()
            {
                _Selected = false;
                Name = "";
                Script = "";
                _Status = "unknown";
                Requirements = "";
                GUID = Guid.NewGuid().ToString();
            }
            protected void OnPropertyChanged([CallerMemberName] string? name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        Dictionary<string, object?> CustomizationsState = new();
        Dictionary<string, bool> OVChecksState = new();
        public ObservableCollection<OVCheckItem> OVChecks { get; set; } = new();
        public OVFrontends ModelsFrontend { get; set; } = OVFrontends.Any;
        public SelfUpdate UpdateChecker { get; set; } = new();
        public MainWindow()
        {
            instance = this;
            SplashScreen.ShowWindow();
            InitializeComponent();
            OVChecksRegistry.RegisterChecks();
            ListViewChecks.ItemsSource = OVChecks;
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
            } else
            {
                string default_wd = AppDomain.CurrentDomain.BaseDirectory + "DefaultWorkDir";
                if(!System.IO.Directory.Exists(default_wd))
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
            ShowApplicableChecks();
            UpdateChecker.CheckUpdates();
            UpdateAboutTab();
            SplashScreen.CloseWindow();
        }
        public void SaveCBoxWorkDirState()
        {
            if (Properties.Settings.Default.WorkDirs != null)
                Properties.Settings.Default.WorkDirs.Clear();
            else
                Properties.Settings.Default.WorkDirs = new();
            foreach (object item in CBoxWorkDir.Items)
            {
                if (item is ComboBoxBrowseItem) continue;
                Properties.Settings.Default.WorkDirs.Add((item as ComboBoxItem)!.Content!.ToString()!);
            }
            Properties.Settings.Default.Save();
        }
        public void SaveCBoxPythonPathsState()
        {

            if (Properties.Settings.Default.PythonPaths != null)
                Properties.Settings.Default.PythonPaths.Clear();
            else
                Properties.Settings.Default.PythonPaths = new();
            foreach (object item in CBoxPythonPath.Items)
            {
                if (item is ComboBoxBrowseItem || item is ComboBoxDownloadItem) continue;
                Properties.Settings.Default.PythonPaths.Add((item as ComboBoxItem)!.Content!.ToString()!);
            }
            Properties.Settings.Default.Save();
        }
        public void SaveCBoxOpenVINOPathsState()
        {
            if (Properties.Settings.Default.OpenVINOPaths != null)
                Properties.Settings.Default.OpenVINOPaths.Clear();
            else
                Properties.Settings.Default.OpenVINOPaths = new();
            foreach (object item in CBoxOpenVINOPath.Items)
            {
                if (item is ComboBoxBrowseItem || item is ComboBoxBrowseWheelItem || item is ComboBoxOpenVINOItem) continue;
                Properties.Settings.Default.OpenVINOPaths.Add((item as ComboBoxItem)!.Content!.ToString()!);
            }
            Properties.Settings.Default.Save();
        }
        public void SaveCBoxModelPathsState()
        {
            if (Properties.Settings.Default.ModelPaths != null)
                Properties.Settings.Default.ModelPaths.Clear();
            else
                Properties.Settings.Default.ModelPaths = new();
            foreach (object item in CBoxModelPath.Items)
            {
                Properties.Settings.Default.ModelPaths.Add((item as ComboBoxItem)!.Content!.ToString()!);
            }
            Properties.Settings.Default.Save();
        }
        public int ComboBoxItemIndex(ComboBox Target, string? Value)
        {
            if (String.IsNullOrEmpty(Value)) return -1;
            int idx = 0;
            foreach (object item in Target.Items)
            {
                if (!(item is ComboBoxItem)) continue;
                if ((item as ComboBoxItem)!.Content.ToString() == Value) return idx;
                ++idx;
            }
            return -1;
        }
        public void SelectOrAddComboBoxItem(ComboBox Target, string Value)
        {
            int idx = ComboBoxItemIndex(Target, Value);
            if (idx < 0)
            {
                Target.Items.Insert(0, new ComboBoxItem() { Content = Value });
                Target.SelectedIndex = 0;
            }
            else
            {
                Target.SelectedIndex = idx;
            }
        }
        public void InsertMissingComboBoxItem(ComboBox Target, string Value)
        {
            int idx = ComboBoxItemIndex(Target, Value);
            if (idx < 0)
            {
                Target.Items.Insert(0, new ComboBoxItem() { Content = Value });
            }
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
        }
        private void ShowApplicableChecks()
        {
            foreach (var check in OVChecks)
            {
                OVChecksState[check.GUID] = check.Selected;
            }
            OVChecks.Clear();
            // Do not list anything in case frontend isn't detected
            if (ModelsFrontend == OVFrontends.Any) return;
            foreach (var check in OVChecksDescriptions.GetOVCheckDescriptions())
            {
                if (check.Frontend != OVFrontends.Any && check.Frontend != ModelsFrontend)
                    continue;
                bool value = false;
                if (OVChecksState.ContainsKey(check.GUID))
                {
                    value = OVChecksState[check.GUID];
                }
                OVChecks.Add(new() { Name = check.Name, Script = check.Script, Selected = value, Requirements = check.Requirements, GUID = check.GUID });
            }
        }
        private void ShowAvailableChecksCustomizations()
        {
            foreach (var list_item in ListChecksCustomizations.Items)
            {
                var panel = ((list_item as ListBoxItem)!.Content as WrapPanel);
                if (panel!.Children[1] is System.Windows.Controls.CheckBox)
                    CustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.CheckBox)!.IsChecked;
                else if (panel!.Children[1] is System.Windows.Controls.TextBox)
                    CustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.TextBox)!.Text;
            }
            ListChecksCustomizations.Items.Clear();
            for (int i = 0; i < OVChecks.Count; i++)
            {
                var item = OVChecks[i]!;

                if (item.Selected == false) continue;

                var description = OVChecksDescriptions.GetDescriptionByGUID(item.GUID);
                if (description == null) continue;

                foreach (var customization in description.Customizations)
                {
                    bool exists = false;
                    foreach (var itm in ListChecksCustomizations.Items)
                    {
                        if (((itm as System.Windows.Controls.ListBoxItem)!.Content as WrapPanel)!.Uid == customization.GUID)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (exists) continue;
                    var list_item = new System.Windows.Controls.ListViewItem();
                    var panel = new System.Windows.Controls.WrapPanel();
                    panel.Uid = customization.GUID;
                    panel.Children.Add(new System.Windows.Controls.TextBlock() { Text = customization.Name });
                    if (customization.Value is bool)
                    {
                        bool value = bool.Parse(customization.Value.ToString()!);
                        if (CustomizationsState.ContainsKey(customization.GUID))
                            value = bool.Parse(CustomizationsState[customization.GUID]!.ToString()!);
                        panel.Children.Add(new System.Windows.Controls.CheckBox() { Uid = customization.GUID, Margin = new(10, 0, 0, 0), ToolTip = customization.Name, IsChecked = value });
                    }
                    else if (customization.Value is string)
                    {
                        string value = customization.Value.ToString()!;
                        if (CustomizationsState.ContainsKey(customization.GUID))
                            value = CustomizationsState[customization.GUID]!.ToString()!;
                        panel.Children.Add(new System.Windows.Controls.TextBox() { Uid = customization.GUID, Margin = new(10, 0, 0, 0), MinWidth = 64, ToolTip = customization.Name, Text = value });
                    }
                    list_item.Content = panel;
                    ListChecksCustomizations.Items.Add(list_item);
                }
            }
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
        /// <summary>
        /// This method resets list of available python versions in ComboBox CBoxPythonPath
        /// </summary>
        private void CBoxPythonPathReset()
        {
            CBoxPythonPath.Items.Clear();
            CBoxPythonPath.Items.Add(new ComboBoxBrowseItem() { Content = "Browse..." });
            if (CBoxWorkDir.Text.Length > 0)
            {
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.13.3", URL = "https://www.python.org/ftp/python/3.13.3/python-3.13.3-embed-amd64.zip", Path = "Python313" });
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.12.10", URL = "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip", Path = "Python312" });
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.11.11", URL = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip", Path = "Python311" });
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.10.11", URL = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip", Path = "Python310" });
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.9.13", URL = "https://www.python.org/ftp/python/3.9.13/python-3.9.13-embed-amd64.zip", Path = "Python39" });
                CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.8.10", URL = "https://www.python.org/ftp/python/3.8.10/python-3.8.10-embed-amd64.zip", Path = "Python38" });
            }
            string stored_path = "";
            if (Properties.Settings.Default.PythonPath != "")
            {
                stored_path = Properties.Settings.Default.PythonPath;
            }
            foreach (ComboBoxItem item in CBoxPythonPath.Items)
            {
                if (item is ComboBoxDownloadItem && System.IO.Directory.Exists(WorkDir + "\\" + (item as ComboBoxDownloadItem)!.Path))
                {
                    item.Content = item.Content.ToString()!.Replace("Download ", "");
                    if (WorkDir + (item as ComboBoxDownloadItem)!.Path + "\\python.exe" == stored_path)
                    {
                        CBoxPythonPath.SelectedItem = item;
                        stored_path = "";
                    }
                }
            }
            if (Properties.Settings.Default.PythonPaths != null)
            {
                foreach (var item in Properties.Settings.Default.PythonPaths)
                {
                    InsertMissingComboBoxItem(CBoxPythonPath, item!);
                }
            }
            if (stored_path != "" && System.IO.File.Exists(stored_path))
            {
                SelectOrAddComboBoxItem(CBoxPythonPath, stored_path);
                PythonPath = stored_path;
            }
        }
        /// <summary>
        /// This method resets list of available python versions in ComboBox CBoxOpenVINOPath
        /// </summary>
        private void CBoxOpenVINOPathReset()
        {
            CBoxOpenVINOPath.Items.Clear();
            CBoxOpenVINOPath.Items.Add(new ComboBoxBrowseItem() { Content = "Browse for a Bin folder..." });
            CBoxOpenVINOPath.Items.Add(new ComboBoxBrowseWheelItem() { Content = "Browse for a Wheel..." });
            if (Properties.Settings.Default.OpenVINOPaths != null)
            {
                foreach (var item in Properties.Settings.Default.OpenVINOPaths)
                {
                    if (ComboBoxItemIndex(CBoxOpenVINOPath, item) >= 0) continue;
                    CBoxOpenVINOPath.Items.Add(new ComboBoxItem() { Content = item });
                }
            }
            if (PythonPath.Length > 0)
            {
                Process process = new Process();
                process.StartInfo.FileName = PythonPath;
                process.StartInfo.Arguments = "-m pip index versions openvino";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                string versions = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!versions.Contains("Available versions:"))
                {
                    System.Windows.MessageBox.Show("Cannot list OpenVINO versions using PIP. Format might be changed.");
                    return;
                }
                string stored_path = "";
                if (Properties.Settings.Default.OpenVINOPath != "")
                {
                    stored_path = Properties.Settings.Default.OpenVINOPath;
                }
                versions = versions.Substring(versions.IndexOf("Available versions:") + 20);
                var nl_pos = versions.IndexOf("\n");
                if (nl_pos > 0)
                {
                    versions = versions.Substring(0, nl_pos).Replace("\r", "").Replace("\n", "");
                }
                foreach (string version in versions.Split(", "))
                {
                    int idx = CBoxOpenVINOPath.Items.Add(new ComboBoxOpenVINOItem() { Content = "OpenVINO " + version, Version = version });
                    if (stored_path == "OpenVINO " + version)
                    {
                        CBoxOpenVINOPath.SelectedIndex = idx;
                        OVPath = "OpenVINO " + version;
                        stored_path = "";
                    }
                }
                if (stored_path != "")
                {
                    OVPath = stored_path;
                }
            }
        }
        /// <summary>
        /// Method handles CBoxPythonPath selection change. CBoxPythonPath may contain different types of COmboBoxItems.
        /// - ComboBoxBrowseItem - it shows a dialog for choosing Python's folder, and adds it/select it in ComboBox
        /// - ComboBoxDownloadItem - it tries to download an embedded version of Python and instally PyPi as package manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CBoxPythonPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBoxPythonPath.SelectedItem is ComboBoxBrowseItem)
            {
                System.Windows.Forms.FolderBrowserDialog openDialog = new System.Windows.Forms.FolderBrowserDialog()
                {
                    ShowNewFolderButton = true,
                    RootFolder = Environment.SpecialFolder.MyComputer,
                    SelectedPath = CBoxWorkDir.Text
                };

                System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;

                if ((result = openDialog.ShowDialog()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(openDialog.SelectedPath))
                {
                    if (!System.IO.File.Exists(openDialog.SelectedPath + "\\python.exe"))
                    {
                        System.Windows.MessageBox.Show("Folder doesn't contain python.exe");
                        return;
                    }
                    CBoxPythonPath.SelectedIndex = CBoxPythonPath.Items.Add(new ComboBoxItem() { Content = openDialog.SelectedPath });
                    PythonPath = openDialog.SelectedPath + "\\python.exe";
                }
            }
            else if (CBoxPythonPath.SelectedItem is ComboBoxDownloadItem)
            {
                ComboBoxDownloadItem item = (CBoxPythonPath.SelectedItem as ComboBoxDownloadItem)!;
                string python_path = WorkDir + item.Path;
                if (item.Content.ToString()!.StartsWith("Download "))
                {
                    string python_achive = python_path + ".zip";
                    if (!System.IO.File.Exists(python_achive))
                    {
                        try
                        {
                            using (var client = new HttpClient())
                            {
                                using (var s = client.GetStreamAsync(item.URL))
                                {
                                    using (var fs = new FileStream(python_achive, FileMode.OpenOrCreate))
                                    {
                                        s.Result.CopyTo(fs);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            System.Windows.MessageBox.Show("Download failed, please download file " + item.URL + " manually and store as " + item.Path + ".zip in Work Dir");
                            return;
                        }
                    }
                    if (!System.IO.Directory.Exists(python_path))
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(python_achive, python_path);
                    }
                    item.Content = item.Content.ToString()!.Replace("Download ", "");
                    string pip_archive = WorkDir + "pip.pyz";
                    if (!System.IO.File.Exists(pip_archive))
                    {
                        string pip_installer = "https://bootstrap.pypa.io/pip/pip.pyz";
                        try
                        {
                            using (var client = new HttpClient())
                            {
                                using (var s = client.GetStreamAsync(pip_installer))
                                {
                                    using (var fs = new FileStream(pip_archive, FileMode.OpenOrCreate))
                                    {
                                        s.Result.CopyTo(fs);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            System.Windows.MessageBox.Show("Download failed, please download file " + pip_installer + " manually and store as pip.pyz in Work Dir");
                            return;
                        }
                    }
                    if (System.IO.File.Exists(pip_archive) && !System.IO.Directory.Exists(python_path + "\\Lib\\site-packages\\pip"))
                    {
                        AppOutput appOutput = new();
                        string ExeName = python_path + "\\python.exe";
                        appOutput.RunProcess("PIP Install", ExeName, pip_archive + " install pip", python_path);
                        while (!appOutput.IsWorkflowFinished)
                        {
                            await System.Threading.Tasks.Task.Delay(300);
                        }
                        string python_pth = python_path + "\\" + item.Path + "._pth";
                        if (System.IO.File.Exists(python_pth))
                        {
                            System.IO.File.Move(python_pth, python_pth + ".bak");
                        }
                        //System.IO.File.AppendAllText(python_path + "\\" + item.Path + "._pth", "Lib\nLib\\site-packages");
                    }
                }
                PythonPath = python_path;
            }
        }
        /// <summary>
        /// Method opens a dialog for choosing a Work Dir
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonWorkDirBrowse_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openDialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = CBoxWorkDir.Text
            };

            System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;

            if ((result = openDialog.ShowDialog()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(openDialog.SelectedPath))
            {
                WorkDir = openDialog.SelectedPath;
            }
        }
        /// <summary>
        /// Method opens current Work Dir in Windows Explorer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonWorkDirOpen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CBoxWorkDir.Text)) { return; }
            System.Diagnostics.Process.Start("explorer.exe", CBoxWorkDir.Text);
        }

        private void CBoxOpenVINOPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBoxOpenVINOPath.SelectedItem is ComboBoxBrowseItem)
            {
                System.Windows.Forms.FolderBrowserDialog openDialog = new System.Windows.Forms.FolderBrowserDialog()
                {
                    ShowNewFolderButton = true,
                    RootFolder = Environment.SpecialFolder.MyComputer,
                    SelectedPath = CBoxWorkDir.Text
                };

                System.Windows.Forms.DialogResult result = System.Windows.Forms.DialogResult.None;

                if ((result = openDialog.ShowDialog()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(openDialog.SelectedPath))
                {
                    if (!System.IO.File.Exists(openDialog.SelectedPath + "\\openvino.dll"))
                    {
                        System.Windows.MessageBox.Show("Folder doesn't contain openvino.dll");
                        return;
                    }
                    OVPath = openDialog.SelectedPath;
                }
            }
            else if (CBoxOpenVINOPath.SelectedItem is ComboBoxOpenVINOItem)
            {
                ComboBoxOpenVINOItem item = (CBoxOpenVINOPath.SelectedItem as ComboBoxOpenVINOItem)!;
                OVPath = item.Content.ToString()!;
            }
            else if (CBoxOpenVINOPath.SelectedItem is ComboBoxBrowseWheelItem)
            {
                string allKnownExts = "*.whl";
                System.Windows.Forms.OpenFileDialog openDialog = new()
                {
                    Filter = "Wheel files|" + allKnownExts + "|All files (*.*)|*.*",
                    DefaultExt = allKnownExts
                };

                var result = openDialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK) return;

                if (!openDialog.FileName.ToLower().EndsWith(".whl"))
                {
                    MessageBox.Show("Chosen wrong file");
                    CBoxOpenVINOPath.SelectedIndex = -1;
                    return;
                }
                OVPath = openDialog.FileName;
            }
            else
            {
                ComboBoxItem item = (CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!;
                OVPath = item.Content.ToString()!;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TabTools.SelectedIndex = 0;
        }

        private void PrepareEnvironment(List<AppOutput.ProcessItem> tasks, string python_path, ref string? custom_env)
        {
            if (CBoxOpenVINOPath.SelectedItem is ComboBoxOpenVINOItem)
            {
                var item = (CBoxOpenVINOPath.SelectedItem as ComboBoxOpenVINOItem)!;
                tasks.Add(new() { Name = python_path, Args = "-m pip install --upgrade openvino==" + item.Version, WorkingDir = WorkDir, NoWindow = true });
            }
            else// if(CBoxOpenVINOPath.SelectedItem is ComboBoxItem)
            {
                tasks.Add(new() { Name = python_path, Args = "-m pip uninstall -y openvino", WorkingDir = WorkDir, NoWindow = true });
                tasks.Add(new() { Name = python_path, Args = "-m pip install numpy", WorkingDir = WorkDir, NoWindow = true });
                string openvino_path = (CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                if (openvino_path.ToLower().EndsWith(".whl"))
                {
                    tasks.Add(new() { Name = python_path, Args = "-m pip install --upgrade \"" + openvino_path + "\"", WorkingDir = WorkDir, NoWindow = true });
                }
                else
                {
                    custom_env = "OPENVINO_LIB_PATHS=" + CBoxOpenVINOPath.Text + "\nPYTHONPATH=" + CBoxOpenVINOPath.Text + "python\\;" + CBoxOpenVINOPath.Text + "..\\..\\..\\tools\\ovc\\;";
                }
            }
        }
        private void PrepareCheck(OVCheckItem item, List<AppOutput.ProcessItem> tasks, string python_path, string? custom_env)
        {
            if (item.Requirements != "")
            {
                tasks.Add(new() { Name = python_path, Args = "-m pip install --upgrade " + item.Requirements, WorkingDir = WorkDir, NoWindow = true });
            }

            string script_path = WorkDir + item.Name.Replace(" ", "_") + ".py";
            string script = item.Script.Replace("\r", "");
            script = script.Replace("%MODEL_PATH%", ModelPath.Replace("\\", "/"));

            // Apply customizations
            var description = OVChecksDescriptions.GetDescriptionByGUID(item.GUID);
            if (description != null)
            {
                foreach (var list_item in ListChecksCustomizations.Items)
                {
                    var panel = ((list_item as ListBoxItem)!.Content as WrapPanel);
                    foreach (var custom in description.Customizations)
                    {
                        if (custom.GUID != panel!.Uid) continue;
                        if (custom.Handler == null) continue;
                        if (custom_env == null) custom_env = "";
                        if (custom.Value is bool)
                        {
                            var cb = panel.Children[1] as System.Windows.Controls.CheckBox;
                            custom.Handler(custom, cb!.IsChecked, ref script, ref custom_env);
                        }
                        else if (custom.Value is string)
                        {
                            var tb = panel.Children[1] as System.Windows.Controls.TextBox;
                            custom.Handler(custom, tb!.Text, ref script, ref custom_env);
                        }
                    }
                }
            }

            System.IO.File.WriteAllText(script_path, script);

            tasks.Add(new() { Name = python_path, Args = "\"" + script_path + "\"", WorkingDir = WorkDir, CustomEnvVars = custom_env, DontStopOnError = (CBFastFail.IsChecked == false), NoWindow = true, ItemSource = item, StatusHandler = ItemStatusHandler });
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
            app.RunProcess("Checks", tasks, WorkDir + "latest.log");
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
                DefaultExt = allKnownExts
            };

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
            app.RunProcess("Single Check", tasks, WorkDir + "latest.log");
        }

        private void ButtonOpenModelsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ModelPath)) { return; }
            if (System.IO.File.Exists(ModelPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", System.IO.Path.GetDirectoryName(ModelPath)!);
            }
            else if (System.IO.Directory.Exists(ModelPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", ModelPath);
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
                System.Diagnostics.Process.Start((sender as Button)!.Uid);
            }
            catch { }
        }

        private void TabTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabTools.SelectedIndex == 1)
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
            try
            {
                var httpClientHandler = new HttpClientHandler
                {
                    Proxy = null,
                    UseProxy = false,
                };
                using (var client = new HttpClient(httpClientHandler, false))
                {
                    using (var s = client.GetStreamAsync(url))
                    {
                        using (var fs = new FileStream(WorkDir + filename, FileMode.OpenOrCreate))
                        {
                            s.Result.CopyTo(fs);
                        }
                    }
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("Download failed, please download file manually and store as " + filename + " in Work Dir");
                return;
            }
            MessageBox.Show("Wheel has been downloaded!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            SelectOrAddComboBoxItem(CBoxOpenVINOPath, WorkDir + filename);
        }
    }
}