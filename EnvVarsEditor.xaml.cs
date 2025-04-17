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
using System.Collections;
using System.Text.RegularExpressions;

namespace OVChecker
{
    /// <summary>
    /// Interaction logic for EnvVarsEditor.xaml
    /// </summary>
    public partial class EnvVarsEditor : Window
    {
        private static string variableRegEx = "%([a-z_-]+)%";
        public EnvVarsEditor()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ResEnvVars.Text = GetModifiedEnvVars(CustomEnvVars.Text);
        }

        public static string GetModifiedEnvVars(string CustomEnvVars)
        {
            Process process = new();

            string[] customEnvVars = CustomEnvVars.Split('\n');
            string result = "";
            Regex regEx = new Regex(variableRegEx, RegexOptions.IgnoreCase);
            Dictionary<string, string> CustomKeyVal = new();

            foreach (string line in customEnvVars)
            {
                string[] keyValue = line.Split('=');
                if (keyValue.Length < 2)
                {
                    result += "REM Invalid format of setting Environment Variable, should be key=value.\nREM " + line;
                    continue;
                }
                keyValue[0] = keyValue[0].Trim();
                keyValue[1] = keyValue[1].Trim();
                string value = regEx.Replace(keyValue[1], new MatchEvaluator((match) =>
                {
                    string envKey = match.Groups[1].Value.ToUpper(); ;
                    if (process.StartInfo.EnvironmentVariables.ContainsKey(envKey))
                    {
                        return process.StartInfo.EnvironmentVariables[envKey]!;
                    }
                    if (CustomKeyVal.ContainsKey(envKey))
                    {
                        return CustomKeyVal[envKey]!;
                    }
                    switch (envKey.ToUpper())
                    {
                        case "ATTEMPT_DIR": return "A:\\Work\\Dir\\Attempt\\0000";
                        case "ATTEMPT_ROOT": return "A:\\Work\\Dir\\CommitSha";
                        case "ATTEMPT_MODEL": return "A:\\Model\\Path\\model.ext";
                    }

                    return "";
                }));
                result += keyValue[0] + "=" + value + "\n";
                CustomKeyVal[keyValue[0]] = value;
            }

            return result;
        }
        public static string ModifyProcessEnvVars(Process CustomProcess, string CustomEnvVars)
        {
            Process process = new Process();

            string[] customEnvVars = CustomEnvVars.Split('\n');
            string result = "";
            Regex regEx = new Regex(variableRegEx, RegexOptions.IgnoreCase);

            foreach (string line in customEnvVars)
            {
                string[] keyValue = line.Split('=');
                if (keyValue.Length < 2)
                {
                    continue;
                }
                keyValue[0] = keyValue[0].Trim();
                keyValue[1] = keyValue[1].Trim();
                string value = regEx.Replace(keyValue[1], new MatchEvaluator((match) =>
                {
                    string envKey = match.Groups[1].Value.ToUpper(); ;
                    if (process.StartInfo.EnvironmentVariables.ContainsKey(envKey))
                    {
                        return process.StartInfo.EnvironmentVariables[envKey]!;
                    }
                    if (CustomProcess.StartInfo.EnvironmentVariables.ContainsKey(envKey))
                    {
                        return CustomProcess.StartInfo.EnvironmentVariables[envKey]!;
                    }

                    return "";
                }));
                CustomProcess.StartInfo.EnvironmentVariables[keyValue[0]] = value;
            }

            return result;
        }

        public static string ApplyProcessEnvVars(string input, Process? process = null)
        {
            Regex regEx = new Regex(variableRegEx, RegexOptions.IgnoreCase);
            if (process == null) process = new Process();

            return regEx.Replace(input, new MatchEvaluator((match) =>
            {
                string envKey = match.Groups[1].Value.ToUpper(); ;
                if (process.StartInfo.EnvironmentVariables.ContainsKey(envKey))
                {
                    return process.StartInfo.EnvironmentVariables[envKey]!;
                }

                return "";
            }));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Process process = new Process();
            List<string> envVars = new List<string>();
            foreach (DictionaryEntry entry in process.StartInfo.EnvironmentVariables)
            {
                envVars.Add(entry.Key + "=" + entry.Value);
            }
            envVars.Sort();
            SysEnvVars.Text = string.Join("\n", envVars);
            //CustomEnvVars.Text = Properties.Settings.Default.ExecutableEnvVars;
            if (!string.IsNullOrWhiteSpace(CustomEnvVars.Text))
            {
                ResEnvVars.Text = GetModifiedEnvVars(CustomEnvVars.Text);
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            //Properties.Settings.Default.ExecutableEnvVars = CustomEnvVars.Text;
            //Properties.Settings.Default.Save();
            System.Windows.MessageBox.Show("Changes has been saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            //if (CustomEnvVars.Text != Properties.Settings.Default.ExecutableEnvVars)
            {
                switch (System.Windows.MessageBox.Show("Custom Environment variables are changed. Save changes?", "Env Vars Changed", MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
                {
                    case MessageBoxResult.Yes:
                        //Properties.Settings.Default.ExecutableEnvVars = CustomEnvVars.Text;
                        //Properties.Settings.Default.Save();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private void BtnPythonEnv_Click(object sender, RoutedEventArgs e)
        {
            string[] customEnvVars = CustomEnvVars.Text.Split('\n');
            Dictionary<string, bool> VariableExists = new();
            const string DEFAULT_OPENVINO_LIB_PATHS = "OPENVINO_LIB_PATHS=%ATTEMPT_ROOT%\\bin\\intel64\\Release",
                DEFAULT_PYTHONPATH = "PYTHONPATH=%ATTEMPT_ROOT%\\bin\\intel64\\Release\\python\\;%ATTEMPT_ROOT%\\mo\\;%ATTEMPT_ROOT%\\ovc\\";

            CustomEnvVars.Text = string.Empty;
            foreach (string line in customEnvVars)
            {
                string[] keyValue = line.Split('=');
                if (keyValue.Length < 2)
                {
                    continue;
                }
                keyValue[0] = keyValue[0].Trim();
                keyValue[1] = keyValue[1].Trim();
                VariableExists[keyValue[0]] = true;

                if (CustomEnvVars.Text != string.Empty) CustomEnvVars.Text += '\n';

                switch (keyValue[0])
                {
                    case "OPENVINO_LIB_PATHS": CustomEnvVars.Text += DEFAULT_OPENVINO_LIB_PATHS; break;
                    case "PYTHONPATH": CustomEnvVars.Text += DEFAULT_PYTHONPATH; break;
                    default: CustomEnvVars.Text += line; break;
                }
            }

            if (!VariableExists.ContainsKey("OPENVINO_LIB_PATHS"))
                CustomEnvVars.Text += (CustomEnvVars.Text != string.Empty ? '\n' : string.Empty) + DEFAULT_OPENVINO_LIB_PATHS;
            if (!VariableExists.ContainsKey("PYTHONPATH"))
                CustomEnvVars.Text += (CustomEnvVars.Text != string.Empty ? '\n' : string.Empty) + DEFAULT_PYTHONPATH;
        }
    }
}
