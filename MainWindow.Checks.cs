using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OVChecker
{
    public partial class MainWindow : Window
    {
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
        private void PrepareCheck(OVCheckItem item, List<AppOutput.ProcessItem> tasks, string python_path, string? custom_env)
        {
            if (item.Requirements != "")
            {
                string pip_flags = "";
                if (CBPIPQuite.IsChecked == true)
                {
                    pip_flags = " -q";
                }
                tasks.Add(new() { Name = python_path, Args = "-m pip install --disable-pip-version-check --upgrade " + item.Requirements + pip_flags, WorkingDir = WorkDir, NoWindow = true });
            }

            string script_path = WorkDir + item.Name.Replace(" ", "_") + ".py";
            string script = item.Script.Replace("\r", "");
            script = script.Replace("%MODEL_PATH%", ModelPath.Replace("\\", "/"));

            {
                string openvino_path = CBoxOpenVINOPath.Text.Replace("\"", "\\\"");
                StringBuilder exec_info = new();
                exec_info.AppendLine("# Python: " + python_path);
                if (CBoxOpenVINOPath.SelectedItem != null && !(CBoxOpenVINOPath.SelectedItem is ComboBoxOpenVINOItem) && !(CBoxOpenVINOPath.SelectedItem as ComboBoxItem)!.Content.ToString()!.ToLower().EndsWith(".whl"))
                {
                    exec_info.AppendLine("os.environ[\"OPENVINO_LIB_PATHS\"] = \"" + openvino_path.Replace("\\", "\\\\") + "\"");
                    exec_info.AppendLine("sys.path.extend(\"" + (openvino_path + "python\\;" + openvino_path + "..\\..\\..\\tools\\ovc\\;").Replace("\\", "\\\\") + "\".split(\";\"))");
                }
                else
                {
                    exec_info.AppendLine("# OpenVINO: " + openvino_path);
                }
                const string keyword = "import os\n";
                var pos = script.IndexOf(keyword);
                if (pos != -1)
                {
                    if (pos + keyword.Length < script.Length)
                        script = script.Insert(pos + keyword.Length, exec_info.ToString());
                    else
                        script += exec_info.ToString();
                }
            }

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
    }
}
