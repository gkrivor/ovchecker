using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public partial class PassViewer : Window
    {
        public class OVPassItem : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public int Milliseconds { get; set; }
            public string? PassName { get; set; }
            public string State { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            public OVPassItem()
            {
                Name = "";
                Milliseconds = 0;
                PassName = null;
                State = "?";
            }
            protected void OnPropertyChanged([CallerMemberName] string? name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        public ObservableCollection<OVPassItem> Passes { get; set; } = new();
        public PassViewer()
        {
            InitializeComponent();
            ListViewPasses.ItemsSource = Passes;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(Passes);
            view.Filter = PassesFilter;
            /*
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("PassName");
            view.GroupDescriptions.Add(groupDescription);
            */
        }
        private bool PassesFilter(object item)
        {
            if (string.IsNullOrEmpty(TextFilter.Text))
            {
                return true;
            }
            else
            {
                return (item as OVPassItem)!.Name.Contains(TextFilter.Text, StringComparison.OrdinalIgnoreCase);
            }
        }
        private void ParseBlock(ref StringBuilder src_text, ref string? pass_name, ref List<string> pass_path, bool is_last_block = false)
        {
            int n_pos = -1;
            do
            {
                n_pos = -1;
                // Try to find a line
                for (int i = 0; i < src_text.Length; ++i)
                {
                    if (src_text[i] == '\n')
                    {
                        n_pos = i;
                        break;
                    }
                }
                if (n_pos == -1 && is_last_block && src_text.Length > 1) n_pos = src_text.Length;
                if (n_pos == -1) break; // Read next block of text if new line isn't found
                string line = src_text.ToString(0, n_pos - 1);
                src_text.Remove(0, n_pos < src_text.Length ? n_pos + 1 : src_text.Length - 1);
                if (line.StartsWith("PassManager started:"))
                {
                    pass_name = line.Substring(25);
                    pass_path.Add(pass_name);
                }
                else if (line.StartsWith("PassManager finished:"))
                {
                    var match = Regex.Match(line.Substring(25), @"([^ ]+)\s*([0-9]+)ms\s*([+-])");
                    if (match.Success)
                    {
                        string group_name = "TOTAL";
                        if (match.Groups[1].Value != pass_name)
                        {
                            group_name += " Parsing Error";
                        }
                        Passes.Add(new() { Name = group_name, Milliseconds = int.Parse(match.Groups[2].Value), State = match.Groups[3].Value, PassName = string.Join(" \\ ", pass_path) });
                    }
                    if (pass_path.Count > 0)
                        pass_path.RemoveAt(pass_path.Count - 1);
                    pass_name = pass_path.Count > 0 ? pass_path.Last() : null;
                }
                else if (line.StartsWith("                         "))
                {
                    var match = Regex.Match(line.Substring(25), @"([^ ]+)\s*([0-9]+)ms\s*([+-])");
                    if (match.Success)
                    {
                        Passes.Add(new() { Name = match.Groups[1].Value, Milliseconds = int.Parse(match.Groups[2].Value), State = match.Groups[3].Value, PassName = string.Join(" \\ ", pass_path) });
                    }
                }
            } while (n_pos > -1);
        }
        public void ParseFile(string filename)
        {
            Passes.Clear();
            StringBuilder src_text = new();
            List<string> pass_path = new();
            SplashScreen.ShowWindow("Parsing log...");
            using (var src_file = System.IO.File.OpenRead(filename))
            {
                byte[] buffer = new byte[128 * 1024];
                string? pass_name = null;

                var file_info = new System.IO.FileInfo(filename);
                long read = 0, total = 0, file_size = file_info.Length;
                int last_percent = 0, new_percent;
                while ((read = src_file.Read(buffer, 0, buffer.Length)) > 0)
                {
                    src_text.Append(Encoding.ASCII.GetString(buffer));
                    total += read;
                    ParseBlock(ref src_text, ref pass_name, ref pass_path);
                    new_percent = (int)(total * 100 / file_size);
                    if (new_percent > last_percent)
                    {
                        last_percent = new_percent;
                        SplashScreen.SetStatus("Parsing done for " + ((float)total / (1024 * 1024)).ToString("0.00") + "Mb or " + last_percent + "%");
                    }
                }
                if (src_text.Length > 0)
                    ParseBlock(ref src_text, ref pass_name, ref pass_path, true);
            }
            SplashScreen.CloseWindow();
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

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(Passes).Refresh();
        }

        private void TextFilter_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnFilter_Click(sender, new());
        }
    }
}
