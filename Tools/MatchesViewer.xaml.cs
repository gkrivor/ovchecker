using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
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
    public partial class MatchesViewer : Window
    {
        private System.Threading.Thread? ParsingThread = null;
        public bool StopThread = false;
        public string source_file = string.Empty;
        public class ItemsDictionary
        {
            private Dictionary<string, int> MatcherName = new();
            private Dictionary<string, int> NodeType = new();
            private Dictionary<string, int> NodeName = new();

            public int GetMatcherId(string name, bool create = false)
            {
                int id = -1;
                if (string.IsNullOrEmpty(name)) return -1;
                if (MatcherName.TryGetValue(name, out id)) return id;
                if (create == true)
                {
                    id = MatcherName.Count;
                    MatcherName[name] = id;
                }
                return id;
            }
            public string GetMatcherName(int id)
            {
                if (id < 0) return string.Empty;
                return MatcherName.FirstOrDefault(x => x.Value == id).Key;
            }
            public int GetTypeId(string name, bool create = false)
            {
                int id = -1;
                if (string.IsNullOrEmpty(name)) return -1;
                if (NodeType.TryGetValue(name, out id)) return id;
                if (create == true)
                {
                    id = NodeType.Count;
                    NodeType[name] = id;
                }
                return id;
            }
            public string GetTypeName(int id)
            {
                if (id < 0) return string.Empty;
                return NodeType.FirstOrDefault(x => x.Value == id).Key;
            }
            public int GetNameId(string name, bool create = false)
            {
                int id = -1;
                if (string.IsNullOrEmpty(name)) return -1;
                if (NodeName.TryGetValue(name, out id)) return id;
                if (create == true)
                {
                    id = NodeName.Count;
                    NodeName[name] = id;
                }
                return id;
            }
            public string GetName(int id)
            {
                if (id < 0) return string.Empty;
                return NodeName.FirstOrDefault(x => x.Value == id).Key;
            }
            private void FindApplicableItems(string text, ref Dictionary<string, int> dict, ref List<int> output)
            {
                foreach(var item in dict.Keys)
                {
                    if(item.Contains(text, StringComparison.InvariantCultureIgnoreCase))
                        output.Add(dict[item]);
                }
            }
            public void FindMatches(string text, ref List<int> output)
            {
                FindApplicableItems(text, ref MatcherName, ref output);
            }
            public void FindTypes(string text, ref List<int> output)
            {
                FindApplicableItems(text, ref NodeType, ref output);
            }
            public void FindNodes(string text, ref List<int> output)
            {
                FindApplicableItems(text, ref NodeName, ref output);
            }

            public ItemsDictionary() { }
            public void Clear()
            {
                MatcherName.Clear();
                NodeType.Clear();
                NodeName.Clear();
            }
        }
        public class DataOffsetItem
        {
            private long offset_start, offset_end;
            public long OffsetStart { get { return offset_start; } set { offset_start = value; } }
            public long OffsetEnd { get { return offset_end; } set { offset_end = value; } }
            public long DataLength { get { return offset_end - offset_start; } set { offset_end = offset_start + value; } }

            private int _MatcherName = -1;
            private int _NodeType = -1;
            private int _NodeName = -1;
            public int GetMatcherId() { return _MatcherName; }
            public int GetTypeId() { return _NodeType; }
            public int GetNodeId() { return _NodeName; }
            public string MatcherName
            {
                get { return Dictionary.GetMatcherName(_MatcherName); }
                set { _MatcherName = Dictionary.GetMatcherId(value, true); }
            }
            public string NodeType
            {
                get { return Dictionary.GetTypeName(_NodeType); }
                set { _NodeType = Dictionary.GetTypeId(value, true); }
            }
            public string NodeName
            {
                get { return Dictionary.GetName(_NodeName); }
                set { _NodeName = Dictionary.GetNameId(value, true); }
            }

            private bool _IsDidntMatch = true;
            public bool IsDidntMatch { get { return _IsDidntMatch; } }
            private string _ResultString = string.Empty;
            public string ResultString
            {
                get { return _ResultString; }
                set { _IsDidntMatch = value.Contains("DIDN'T"); _ResultString = value; }
            }
            public ItemsDictionary Dictionary { get; set; }

            public DataOffsetItem(ItemsDictionary dictionary) : base()
            {
                Dictionary = dictionary;
            }
        }
        public class DOTreeViewItem : TreeViewItem
        {
            public DataOffsetItem? Item { get; set; } = null;
            public DOTreeViewItem(bool expandable = true) : base()
            {
                if (!expandable) return;
                Items.Add(new TreeViewItem { Header = "Loading..." });
            }
        }
        private List<DataOffsetItem>? RootItems { get; set; } = null;
        private ItemsDictionary CurrentDictionary { get; set; } = new();
        private BitmapImage ImageOKSource { get; set; } = new();

        private List<int> MatcherIDs = new(), TypeIDs = new(), NodeIDs = new();

        public MatchesViewer()
        {
            InitializeComponent();
            ImageOKSource.BeginInit();
            ImageOKSource.UriSource = new Uri("pack://application:,,,/OVChecker;component/Resources/status_ok.bmp");
            ImageOKSource.EndInit();
        }
        private void RenderGroups()
        {
            TreeViewMatches.Items.Clear();
            if (RootItems == null) return;
            List<string> groups = new();
            FillGroups(ref groups);
            groups.Sort();
            foreach (var item in groups)
            {
                var root_item = new TreeViewItem() { Header = item };
                root_item.Items.Add(new TreeViewItem() { Header = "Loading..." });
                root_item.Expanded += RootItem_Expanded;
                TreeViewMatches.Items.Add(root_item);
            }
        }
        private void FillGroups(ref List<string> groups)
        {
            if (IsFilterApplied())
            {
                foreach (var item in RootItems!)
                {
                    if (IsItemFiltered(item)) continue;
                    if (!groups.Contains(item.MatcherName))
                    {
                        groups.Add(item.MatcherName);
                    }
                }
            }
            else
            {
                foreach (var item in RootItems!)
                {
                    if (!groups.Contains(item.MatcherName))
                    {
                        groups.Add(item.MatcherName);
                    }
                }
            }
        }
        private bool IsFilterApplied()
        {
            return !string.IsNullOrEmpty(TextFilter.Text) || CheckOnlyMatched.IsChecked == true;
        }
        private bool IsItemFiltered(DataOffsetItem item)
        {
            bool isnt_filtered = true;
            if (!string.IsNullOrEmpty(TextFilter.Text))
            {
                //isnt_filtered &= item.MatcherName.Contains(TextFilter.Text, StringComparison.InvariantCultureIgnoreCase) || item.NodeName.Contains(TextFilter.Text, StringComparison.InvariantCultureIgnoreCase) || item.NodeType.Contains(TextFilter.Text, StringComparison.InvariantCultureIgnoreCase);
                isnt_filtered &= MatcherIDs.Contains(item.GetMatcherId()) || TypeIDs.Contains(item.GetTypeId()) || NodeIDs.Contains(item.GetNodeId());
            }
            if (CheckOnlyMatched.IsChecked == true)
                isnt_filtered &= item.IsDidntMatch == false;
            return !isnt_filtered;
        }
        private void RootItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem root_item = (TreeViewItem)sender;
            if (root_item == null || (root_item.Items.Count > 0 && root_item.Items[0] is TreeViewItem && (root_item.Items[0] as TreeViewItem)!.Header.ToString() != "Loading...")) return;
            List<string> groups = new();
            string? matcher_name = root_item.Header.ToString();
            foreach (var item in RootItems!)
            {
                if (item.MatcherName != matcher_name) continue;
                if (IsFilterApplied() && IsItemFiltered(item)) continue;
                if (!groups.Contains(item.NodeType))
                {
                    groups.Add(item.NodeType);
                }
            }
            groups.Sort();
            root_item.Items.Clear();
            foreach (var item in groups)
            {
                var node_item = new TreeViewItem() { Header = item };
                node_item.Items.Add(new TreeViewItem() { Header = "Loading..." });
                node_item.Expanded += NodeItem_Expanded;
                root_item.Items.Add(node_item);
            }
        }
        private void NodeItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem node_item = (TreeViewItem)sender;
            if (node_item == null || (node_item.Items.Count > 0 && node_item.Items[0] is TreeViewItem && (node_item.Items[0] as TreeViewItem)!.Header.ToString() != "Loading...")) return;
            TreeViewItem root_item = (node_item.Parent as TreeViewItem)!;
            string? matcher_name = root_item.Header.ToString();
            string? node_type = node_item.Header.ToString();
            node_item.Items.Clear();
            foreach (var item in RootItems!)
            {
                if (item.MatcherName != matcher_name || item.NodeType != node_type) continue;
                if (IsFilterApplied() && IsItemFiltered(item)) continue;
                var match_node = new DOTreeViewItem(false) { Item = item };
                if (item.IsDidntMatch == false)
                {
                    var panel = new WrapPanel();
                    panel.Children.Add(new Image() { Source = ImageOKSource, Width = 16, Height = 16, Margin = new(0, 0, 5, 0) });
                    panel.Children.Add(new TextBlock() { Text = item.NodeName });
                    match_node.Header = panel;
                }
                else
                {
                    match_node.Header = item.NodeName;
                }
                match_node.Selected += MatchItem_Selected;
                node_item.Items.Add(match_node);
            }
        }
        private void MatchItem_Selected(object sender, RoutedEventArgs e)
        {
            DOTreeViewItem? match_item = sender as DOTreeViewItem;
            if (match_item == null) return;
            DataOffsetItem do_item = match_item.Item!;
            using (var src_file = System.IO.File.OpenRead(source_file))
            {
                src_file.Position = do_item.OffsetStart;
                byte[] bytes = new byte[do_item.DataLength];
                src_file.Read(bytes, 0, bytes.Length);
                TextSource.Text = Encoding.ASCII.GetString(bytes);
                ColorizeOutput(TextSource.Text);
            }
        }
        private void ColorizeOutput(string text)
        {
            Regex reMatchName = new Regex(@"\[[^\]]+\]", RegexOptions.Compiled);

            var doc = new FlowDocument();
            doc.LineHeight = 1.0;
            foreach (var line in text.Split("\n"))
            {
                var paragraph = new Paragraph();
                bool is_skip_line = false;
                var rline = line.TrimEnd().Replace("  ", " ").Replace("?", "");
                if (rline.Trim() == "") continue;
                if (rline.StartsWith("[DEBUG]")) continue;
                foreach (var txt in rline.Split(" "))
                {
                    if (txt == "{" || txt == "}")
                    {
                        paragraph.Inlines.Add(new Run(txt + " ") { Foreground = Brushes.Green });
                    }
                    else if (txt == "MATCHED")
                    {
                        paragraph.Inlines.Add(new Run(txt + " ") { Foreground = Brushes.Green, FontWeight = FontWeights.Bold });
                    }
                    else if (txt == "DIDN'T")
                    {
                        paragraph.Inlines.Add(new Run(txt + " ") { Foreground = Brushes.Maroon, FontWeight = FontWeights.Bold });
                    }
                    else if (txt == "ARGUMENT" || txt == "BRANCH" || txt == "PERMUTATION")
                    {
                        paragraph.Inlines.Add(new Run(txt + " ") { Foreground = Brushes.Navy });
                    }
                    else if (txt.Contains("NODE"))
                    {
                        paragraph.Inlines.Add(new Run(txt + " ") { Foreground = Brushes.Navy, FontWeight = FontWeights.Bold });
                    }
                    else if (reMatchName.IsMatch(txt))
                    {
                        paragraph.Inlines.Add(new Run("["));
                        paragraph.Inlines.Add(new Run(txt.Substring(1, txt.Length - 2)) { Foreground = Brushes.Gold, FontWeight = FontWeights.Bold });
                        paragraph.Inlines.Add(new Run("] "));
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run(txt + " "));
                    }
                }
                if (!is_skip_line)
                {
                    doc.Blocks.Add(paragraph);
                }
            }
            TextHighlight.Document = doc;
        }
        private void ParseBlock(ref StringBuilder src_text, ref DataOffsetItem? current, ref long offset, bool is_last_block = false)
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
                int offset_change = n_pos < src_text.Length ? n_pos + 1 : src_text.Length - 1;
                src_text.Remove(0, offset_change);
                if (line.StartsWith("{"))
                {
                    //{  [MarkDequantization] START: trying to start pattern matching with Multiply Multiply_4636
                    var match = Regex.Match(line, @"\{  \[([^\]]+)\] START: [a-z ]+([A-Za-z]+)\s*([A-Za-z_0-9]+)");
                    if (match.Success)
                    {
                        DataOffsetItem item = new(CurrentDictionary);
                        item.MatcherName = match.Groups[1].Value;
                        item.NodeType = match.Groups[2].Value;
                        item.NodeName = match.Groups[3].Value;
                        item.OffsetStart = offset;
                        RootItems!.Add(item);
                        current = RootItems!.Last();
                    }
                }
                else if (line.StartsWith("}"))
                {
                    //}  [MarkDequantization] END: PATTERN MATCHED, CALLBACK FAILED
                    var match = Regex.Match(line, @"\}  \[([^\]]+)\] END: (.*)");
                    if (match.Success && current != null)
                    {
                        current.ResultString = match.Groups[2].Value;
                        current.OffsetEnd = offset + n_pos + 1;
                    }
                    current = null;
                }
                offset += offset_change;
            } while (n_pos > -1);
        }
        public void ParseFile(string filename)
        {
            source_file = filename;
            var thread_parameters = new System.Threading.ThreadStart(delegate { ParseFileInternal(filename); });
            CurrentDictionary.Clear();
            TreeViewMatches.Items.Clear();
            ParsingThread = new System.Threading.Thread(thread_parameters);
            ParsingThread.Start();
        }
        private void ParseFileInternal(string filename)
        {
            Dispatcher.Invoke(() => { SplashScreen.ShowWindow("Parsing log..."); });

            StringBuilder src_text = new();
            DataOffsetItem? current = null;

            using (var src_file = System.IO.File.OpenRead(filename))
            {
                byte[] buffer = new byte[32 * 1024];

                var file_info = new System.IO.FileInfo(filename);
                RootItems = new List<DataOffsetItem>((int)(file_info.Length / 768));

                long read = 0, total = 0, file_size = file_info.Length, offset = 0;
                int last_percent = 0, new_percent;
                while ((read = src_file.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (StopThread) { break; }
                    src_text.Append(Encoding.ASCII.GetString(buffer));
                    ParseBlock(ref src_text, ref current, ref offset);
                    total += read;
                    new_percent = (int)(total * 100 / file_size);
                    if (new_percent > last_percent)
                    {
                        last_percent = new_percent;
                        SplashScreen.instance!.Dispatcher.Invoke(() =>
                        {
                            SplashScreen.SetStatus("Parsing done for " + ((float)total / (1024 * 1024)).ToString("0.00") + "Mb or " + last_percent + "%");
                        });
                    }
                }
                if (src_text.Length > 0)
                    ParseBlock(ref src_text, ref current, ref offset, true);
            }

            Dispatcher.Invoke(() => { RenderGroups(); SplashScreen.CloseWindow(); });
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
            /* Preparing list of applicable IDs for faster filtering */
            MatcherIDs.Clear();
            TypeIDs.Clear();
            NodeIDs.Clear();
            CurrentDictionary.FindMatches(TextFilter.Text, ref MatcherIDs);
            CurrentDictionary.FindTypes(TextFilter.Text, ref TypeIDs);
            CurrentDictionary.FindNodes(TextFilter.Text, ref NodeIDs);
            RenderGroups();
        }
        private void TextFilter_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnFilter_Click(sender, new());
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            RootItems = null;
        }
    }
}
