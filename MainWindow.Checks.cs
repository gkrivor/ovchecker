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
using System.Windows.Documents;

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
            var save_state = (ListBoxItem item) =>
            {
                var panel = (item.Content as WrapPanel);
                if (panel!.Children[1] is System.Windows.Controls.CheckBox)
                    CustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.CheckBox)!.IsChecked;
                else if (panel!.Children[1] is System.Windows.Controls.TextBox)
                    CustomizationsState[panel!.Uid] = (panel!.Children[1] as System.Windows.Controls.TextBox)!.Text;
            };
            foreach (var list_item in ListChecksCustomizations.Items)
            {
                if (list_item is ListBoxItem)
                {
                    save_state((list_item as ListBoxItem)!);
                    continue;
                }
                if (list_item is Expander)
                {
                    var expander = (list_item as Expander)!;
                    CustomizationsState[expander.Header.ToString()!] = expander.IsExpanded ? "yes" : "no";
                    foreach (var list_item2 in ((list_item as Expander)!.Content as Panel)!.Children)
                    {
                        if (list_item2 is ListBoxItem)
                        {
                            save_state((list_item2 as ListBoxItem)!);
                            continue;
                        }
                    }
                    continue;
                }
            }
            ListChecksCustomizations.Items.Clear();
            Dictionary<string, Expander> groups = new();
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
                        if ((itm is ListBoxItem) && ((itm as ListBoxItem)!.Content as WrapPanel)!.Uid == customization.GUID)
                        {
                            exists = true;
                            break;
                        }
                        if (itm is Expander)
                        {
                            foreach (var itm2 in ((itm as Expander)!.Content as Panel)!.Children)
                            {
                                if (((itm2 as ListBoxItem)!.Content as WrapPanel)!.Uid == customization.GUID)
                                {
                                    exists = true;
                                    break;
                                }
                            }
                            if (exists) break;
                        }
                    }
                    if (exists) continue;
                    Expander group_panel;
                    if (groups.ContainsKey(customization.Group))
                    {
                        group_panel = groups[customization.Group];
                    }
                    else
                    {
                        group_panel = new Expander() { Header = customization.Group };
                        group_panel.Content = new StackPanel();
                        groups[customization.Group] = group_panel;
                        ListChecksCustomizations.Items.Add(group_panel);
                        if (CustomizationsState.ContainsKey(group_panel.Header.ToString()!))
                        {
                            group_panel.IsExpanded = CustomizationsState[group_panel.Header.ToString()!]!.ToString() == "yes";
                        }
                    }
                    var list_item = new System.Windows.Controls.ListViewItem();
                    var panel = new System.Windows.Controls.WrapPanel();
                    panel.Uid = customization.GUID;
                    panel.Children.Add(new System.Windows.Controls.TextBlock() { Text = customization.Name });
                    if (customization.Value is bool)
                    {
                        bool value = bool.Parse(customization.Value.ToString()!);
                        if (CustomizationsState.ContainsKey(customization.GUID))
                            value = bool.Parse(CustomizationsState[customization.GUID]!.ToString()!);
                        panel.Children.Add(new System.Windows.Controls.CheckBox() { Uid = customization.GUID, Margin = new(10, 1, 0, 0), ToolTip = customization.Name, IsChecked = value });
                    }
                    else if (customization.Value is string)
                    {
                        string value = customization.Value.ToString()!;
                        if (CustomizationsState.ContainsKey(customization.GUID))
                            value = CustomizationsState[customization.GUID]!.ToString()!;
                        panel.Children.Add(new System.Windows.Controls.TextBox() { Uid = customization.GUID, Margin = new(10, 0, 0, 0), MinWidth = 64, ToolTip = customization.Name, Text = value });
                    }
                    if (!string.IsNullOrEmpty(customization.HelpURL))
                    {
                        var btn_help = new Button() { Content = "?", Uid = customization.HelpURL, Margin = new(5, 0, 0, 0), Padding = new(3, 0, 3, 0), ToolTip = "Show help" };
                        btn_help.Click += (s, e) =>
                        {
                            System.Diagnostics.Process.Start("explorer.exe", (s as Button)!.Uid);
                        };
                        panel.Children.Add(btn_help);
                    }
                    list_item.Content = panel;
                    (group_panel.Content as StackPanel)!.Children.Add(list_item);
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
                var apply_customization = (Panel panel) =>
                {
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
                };

                foreach (var list_item in ListChecksCustomizations.Items)
                {
                    if (list_item is ListBoxItem)
                    {
                        apply_customization(((list_item as ListBoxItem)!.Content as Panel)!);
                        continue;
                    }
                    if (list_item is Expander)
                    {
                        foreach (var list_item2 in ((list_item as Expander)!.Content as Panel)!.Children)
                        {
                            apply_customization(((list_item2 as ListBoxItem)!.Content as Panel)!);
                        }
                    }
                }
            }

            System.IO.File.WriteAllText(script_path, script);

            tasks.Add(new() { Name = python_path, Args = "\"" + script_path + "\"", WorkingDir = WorkDir, CustomEnvVars = custom_env, DontStopOnError = (CBFastFail.IsChecked == false), NoWindow = true, ItemSource = item, StatusHandler = ItemStatusHandler });
        }
    }
}
