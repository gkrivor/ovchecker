using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace OVChecker
{
    public partial class MainWindow : Window
    {
        private string _PytestsPath = "";
        public string PytestsPath
        {
            get { return _PytestsPath; }
            set
            {
                string _value = value.Replace("/", "\\");
                if (System.IO.Directory.Exists(_value) && !_value.EndsWith("/"))
                {
                    _value += "/";
                }
                if (_PytestsPath == _value) return;
                _PytestsPath = _value;
                SelectOrAddComboBoxItem(CBoxPytestsPath, _value);
                Properties.Settings.Default.PytestsPath = _value;
                SaveCBoxPytestsPathsState();
            }
        }
        public void SaveCBoxPytestsPathsState()
        {
            if (Properties.Settings.Default.PytestsPaths != null)
                Properties.Settings.Default.PytestsPaths.Clear();
            else
                Properties.Settings.Default.PytestsPaths = new();
            foreach (object item in CBoxPytestsPath.Items)
            {
                Properties.Settings.Default.PytestsPaths.Add((item as ComboBoxItem)!.Content!.ToString()!);
            }
            Properties.Settings.Default.Save();
        }

        public class PytestItem : INotifyPropertyChanged
        {
            private bool _Selected;
            public bool Selected { get { return _Selected; } set { _Selected = value; if (instance != null) instance.ShowAvailablePytestsCustomizations(); } }
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
            public PytestItem()
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
        Dictionary<string, object?> PytestsCustomizationsState = new();
        Dictionary<string, bool> OVPytestsState = new();
        public ObservableCollection<PytestItem> Pytests { get; set; } = new();
        public PytestItem? GetPytestItemByGUID(string GUID)
        {
            foreach (var item in Pytests)
            {
                if (item.GUID == GUID) return item;
            }
            return null;
        }
        private void PytestItemStatusHandler(object? source, AppOutput.ProcessStatus status)
        {
            if (source is PytestItem)
            {
                string status_str = "unknown";
                switch (status)
                {
                    case AppOutput.ProcessStatus.OK: status_str = "ok"; break;
                    case AppOutput.ProcessStatus.ERROR: status_str = "error"; break;
                }
                (source as PytestItem)!.Status = status_str;
            }
        }

        private void ShowApplicablePytests()
        {
            if (PythonPath.Length <= 0) return;
            Pytests.Clear();
            string pip_flags = " -q";
            List<string> python_args = new();
            if (CBoxOpenVINOPath.SelectedItem is ComboBoxOpenVINOItem)
            {
                var item = (CBoxOpenVINOPath.SelectedItem as ComboBoxOpenVINOItem)!;
                python_args.Add("-m pip install --upgrade --disable-pip-version-check pytest openvino==" + item.Version + pip_flags);
            }
            else if (CBoxOpenVINOPath.SelectedItem != null)
            {
                python_args.Add("-m pip uninstall --disable-pip-version-check -y openvino" + pip_flags);
                python_args.Add("-m pip install --disable-pip-version-check numpy" + pip_flags);
                string openvino_path = (CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!.Content.ToString()!;
                if (openvino_path.ToLower().EndsWith(".whl"))
                {
                    python_args.Add("-m pip install --disable-pip-version-check --upgrade \"" + openvino_path + "\"" + pip_flags);
                }
            }
            string raw_result = string.Empty;
            string err = string.Empty;
            SplashScreen.ShowWindow();
            foreach (var arg in python_args)
            {
                SplashScreen.SetStatus("Running " + arg);
                System.Diagnostics.Process process = new();
                process.StartInfo.FileName = PythonPath;
                process.StartInfo.Arguments = arg;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                raw_result = process.StandardOutput.ReadToEnd();
                err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    SplashScreen.CloseWindow();
                    MessageBox.Show("Something went wrong:\n" + arg + "\nSTDOUT: " + raw_result + "\nSTDERR: " + err, "Pytest error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            SplashScreen.SetStatus("Requesting list of tests...");
            {
                System.Diagnostics.Process process = new();
                process.StartInfo.FileName = PythonPath;
                process.StartInfo.Arguments = "-m pytest \"" + PytestsPath.Replace("\"", "\\\"") + "\" --collect-only -ra";
                if (TextPytestFilter.Text.Length > 0)
                {
                    process.StartInfo.Arguments += " -k \"" + TextPytestFilter.Text.Replace("\"", "\\\"") + "\"";
                }
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                if (CBoxOpenVINOPath.SelectedItem != null && !(CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!.Content.ToString()!.ToLower().EndsWith(".whl"))
                {
                    process.StartInfo.EnvironmentVariables["OPENVINO_LIB_PATHS"] = CBoxOpenVINOPath.Text;
                    process.StartInfo.EnvironmentVariables["PYTHONPATH"] = CBoxOpenVINOPath.Text + "python\\;" + CBoxOpenVINOPath.Text + "..\\..\\..\\tools\\ovc\\;";
                }
                process.Start();
                raw_result = process.StandardOutput.ReadToEnd();
                err = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    SplashScreen.CloseWindow();
                    MessageBox.Show("Something went wrong while fetching pytest tests list:\n" + raw_result + "\n" + err, "Pytest error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                process.WaitForExit();
            }
            SplashScreen.SetStatus("Parsing list...");
            foreach (var line in raw_result.Split('\n'))
            {
                int pos = line.IndexOf("<TestCaseFunction") + 18;
                if (pos < 18) pos = line.IndexOf("<Function") + 10;
                if (pos < 10) continue; // Nothing found
                int brace_pos = line.LastIndexOf('>');
                string test_name = line.Substring(pos, brace_pos - pos);
                Pytests.Add(new() { Name = test_name, GUID = Guid.NewGuid().ToString() });
            }
            SplashScreen.CloseWindow();
        }
        private void ShowAvailablePytestsCustomizations()
        {
            /*
            foreach (var list_item in ListPytestsCustomizations.Items)
            {
                var panel = ((list_item as ListBoxItem)!.Content as WrapPanel);
                if (panel!.Children[1] is System.Windows.Controls.CheckBox)
                    PytestsCustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.CheckBox)!.IsChecked;
                else if (panel!.Children[1] is System.Windows.Controls.TextBox)
                    PytestsCustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.TextBox)!.Text;
            }
            ListPytestsCustomizations.Items.Clear();
            for (int i = 0; i < OVPytests.Count; i++)
            {
                var item = OVPytests[i]!;

                if (item.Selected == false) continue;

                var description = OVPytestsDescriptions.GetDescriptionByGUID(item.GUID);
                if (description == null) continue;

                foreach (var customization in description.Customizations)
                {
                    bool exists = false;
                    foreach (var itm in ListPytestsCustomizations.Items)
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
                        if (PytestsCustomizationsState.ContainsKey(customization.GUID))
                            value = bool.Parse(PytestsCustomizationsState[customization.GUID]!.ToString()!);
                        panel.Children.Add(new System.Windows.Controls.CheckBox() { Uid = customization.GUID, Margin = new(10, 0, 0, 0), ToolTip = customization.Name, IsChecked = value });
                    }
                    else if (customization.Value is string)
                    {
                        string value = customization.Value.ToString()!;
                        if (PytestsCustomizationsState.ContainsKey(customization.GUID))
                            value = PytestsCustomizationsState[customization.GUID]!.ToString()!;
                        panel.Children.Add(new System.Windows.Controls.TextBox() { Uid = customization.GUID, Margin = new(10, 0, 0, 0), MinWidth = 64, ToolTip = customization.Name, Text = value });
                    }
                    list_item.Content = panel;
                    ListPytestsCustomizations.Items.Add(list_item);
                }
            }
            */
        }
        private void PreparePytest(PytestItem item, List<AppOutput.ProcessItem> tasks, string python_path, string? custom_env)
        {
            /*
            if (item.Requirements != "")
            {
                string pip_flags = "";
                if (CBPIPQuite.IsChecked == true)
                {
                    pip_flags = " -q";
                }
                tasks.Add(new() { Name = python_path, Args = "-m pip install --disable-pip-version-check --upgrade " + item.Requirements + pip_flags, WorkingDir = WorkDir, NoWindow = true });
            }

            //string script_path = WorkDir + item.Name.Replace(" ", "_") + ".py";
            //string script = item.Script.Replace("\r", "");
            //script = script.Replace("%MODEL_PATH%", ModelPath.Replace("\\", "/"));
            /*
            // Apply customizations
            var description = OVPytestsDescriptions.GetDescriptionByGUID(item.GUID);
            if (description != null)
            {
                foreach (var list_item in ListPytestsCustomizations.Items)
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
            */
            //System.IO.File.WriteAllText(script_path, script);

            string args = "-m pytest \"" + PytestsPath.Replace("\"", "\\\"") + "\" --disable-warnings --no-header -v -k \"";
            if (TextPytestFilter.Text.Length > 0)
            {
                args += TextPytestFilter.Text.Replace("\"", "\\\"") + " and ";
            }

            args += item.Name + "\"";


            tasks.Add(new() { Name = python_path, Args = args, WorkingDir = WorkDir, CustomEnvVars = custom_env, DontStopOnError = (CBFastFailPytests.IsChecked == false), NoWindow = true, ItemSource = item, StatusHandler = PytestItemStatusHandler });
        }
    }
}
