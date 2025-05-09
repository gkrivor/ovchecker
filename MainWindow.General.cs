using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OVChecker
{
    public partial class MainWindow: Window
    {
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
        private bool IsCBoxPythonPathHasPath(string Path)
        {
            foreach (var cbitem in CBoxPythonPath.Items)
            {
                if (cbitem is ComboBoxDownloadItem && WorkDir + (cbitem as ComboBoxDownloadItem)!.Path + "\\" + python_bin == Path)
                {
                    return true;
                }
            }
            return false;
        }
        private string _PythonPath = "";
        public string PythonPath
        {
            get { return _PythonPath; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (!_value.EndsWith(python_bin))
                {
                    if (!_value.EndsWith("\\")) _value += "\\";
                    _value += python_bin;
                }
                if (_PythonPath == _value) return;
                _PythonPath = _value;
                if (!IsCBoxPythonPathHasPath(_value))
                {
                    SelectOrAddComboBoxItem(CBoxPythonPath, _value);
                }
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
        /// <summary>
        /// This method resets list of available python versions in ComboBox CBoxPythonPath
        /// </summary>
        private void CBoxPythonPathReset()
        {
            CBoxPythonPath.Items.Clear();
            CBoxPythonPath.Items.Add(new ComboBoxBrowseItem() { Content = "Browse..." });
            if (CBoxWorkDir.Text.Length > 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.13.3", URL = "https://www.python.org/ftp/python/3.13.3/python-3.13.3-embed-amd64.zip", Path = "Python313" });
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.12.10", URL = "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip", Path = "Python312" });
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.11.11", URL = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip", Path = "Python311" });
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.10.11", URL = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip", Path = "Python310" });
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.9.13", URL = "https://www.python.org/ftp/python/3.9.13/python-3.9.13-embed-amd64.zip", Path = "Python39" });
                    CBoxPythonPath.Items.Add(new ComboBoxDownloadItem() { Content = "Download Python 3.8.10", URL = "https://www.python.org/ftp/python/3.8.10/python-3.8.10-embed-amd64.zip", Path = "Python38" });
                }
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
                    if (WorkDir + (item as ComboBoxDownloadItem)!.Path + "\\" + python_bin == stored_path)
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
                    if (IsCBoxPythonPathHasPath(item!)) continue;
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
                process.StartInfo.Arguments = "-m pip --disable-pip-version-check index versions openvino";
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
                    if (!System.IO.File.Exists(openDialog.SelectedPath + "\\" + python_bin))
                    {
                        System.Windows.MessageBox.Show("Folder doesn't contain " + python_bin);
                        return;
                    }
                    CBoxPythonPath.SelectedIndex = CBoxPythonPath.Items.Add(new ComboBoxItem() { Content = openDialog.SelectedPath });
                    PythonPath = openDialog.SelectedPath + "\\" + python_bin;
                }
            }
            else if (CBoxPythonPath.SelectedItem is ComboBoxDownloadItem)
            {
                ComboBoxDownloadItem item = (CBoxPythonPath.SelectedItem as ComboBoxDownloadItem)!;
                string python_path = WorkDir + item.Path;
                if (item.Content.ToString()!.StartsWith("Download "))
                {
                    string python_archive = python_path + ".zip";
                    if (!System.IO.File.Exists(python_archive))
                    {
                        if (!WebRequest.DownloadFile(item.URL, python_archive, true))
                        {
                            System.Windows.MessageBox.Show("Download failed, please download file " + item.URL + " manually and store as " + item.Path + ".zip in Work Dir");
                            return;
                        }
                    }
                    if (!System.IO.Directory.Exists(python_path))
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(python_archive, python_path);
                    }
                    item.Content = item.Content.ToString()!.Replace("Download ", "");
                    string pip_archive = WorkDir + "pip.pyz";
                    if (!System.IO.File.Exists(pip_archive))
                    {
                        string pip_installer = "https://bootstrap.pypa.io/pip/pip.pyz";
                        if (!WebRequest.DownloadFile(pip_installer, pip_archive, true))
                        {
                            System.Windows.MessageBox.Show("Download failed, please download file " + pip_installer + " manually and store as pip.pyz in Work Dir");
                            return;
                        }
                    }
                    if (System.IO.File.Exists(pip_archive) && !System.IO.Directory.Exists(python_path + "\\Lib\\site-packages\\pip"))
                    {
                        AppOutput appOutput = new();
                        string ExeName = python_path + "\\" + python_bin;
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
            else if (CBoxOpenVINOPath.SelectedItem != null)
            {
                ComboBoxItem item = (CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!;
                OVPath = item.Content.ToString()!;
            }
            else
            {
                CBoxOpenVINOPath.SelectedIndex = -1;
            }
        }
    }
}
