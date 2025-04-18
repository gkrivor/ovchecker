using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Xml;
using System.Net;
using System.Reflection;
using System.Windows.Threading;
using System.Security.Cryptography;

namespace OVChecker
{
    public class SelfUpdate
    {
        private System.Threading.Thread? UpdateThread = null;
        public bool ThreadStop = false;
        public bool IsCheckUpdatesCompleted = false;
        public System.Version LatestVersion = new(0, 0, 0);
        public Dictionary<System.Version, string> AvailableVersions = new();
        public Dictionary<System.Version, string> VersionsHash = new();
        public void CheckUpdates()
        {
            var threadParameters = new System.Threading.ThreadStart(CheckUpdatesThreadProc);
            ThreadStop = false;
            IsCheckUpdatesCompleted = false;
            UpdateThread = new System.Threading.Thread(threadParameters);
            UpdateThread.Start();
        }
        public void Stop()
        {
            if (UpdateThread == null) return;
            ThreadStop = true;
            UpdateThread.Interrupt();
        }
        private string GetFileHash(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var fs = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(fs);
                        return BitConverter.ToString(hash).ToLowerInvariant().Replace("-", "");
                    }
                }
            }
            catch { }
            return "";
        }
        private void CheckUpdatesThreadProc()
        {
            bool first_attempt = true;
            var httpClientHandler = new HttpClientHandler
            {
                Proxy = null,
                UseProxy = false,
            };

            if (Properties.Settings.Default.UpdateProxy != "")
            {
                httpClientHandler.Proxy = new WebProxy(Properties.Settings.Default.UpdateProxy);
                httpClientHandler.UseProxy = true;
            }

            while (!IsCheckUpdatesCompleted)
            {
                if (!first_attempt)
                    System.Threading.Thread.Sleep(30000);
                else
                    first_attempt = false;
                try
                {
                    using (var client = new HttpClient(httpClientHandler, false))
                    {
                        if (ThreadStop) return;
                        using (var s = client.GetStreamAsync(Properties.Settings.Default.UpdateURL))
                        {
                            if (ThreadStop) return;
                            using (var ms = new MemoryStream())
                            {
                                s.Result.CopyTo(ms);
                                ms.Seek(0, SeekOrigin.Begin);
                                XmlDocument xml = new();
                                xml.Load(ms);
                                var doc = xml.DocumentElement;
                                if (doc == null) continue;
                                var latest = doc.GetElementsByTagName("latest");
                                if (latest != null && latest.Count > 0)
                                {
                                    LatestVersion = new(latest[0]!.FirstChild!.Value!);
                                }
                                AvailableVersions.Clear();
                                var available = doc.GetElementsByTagName("available");
                                if (available != null && available.Count > 0)
                                {
                                    foreach (var version in available[0]!.ChildNodes)
                                    {
                                        if (!(version is XmlElement)) continue;
                                        var item = (version as XmlElement)!;
                                        AvailableVersions[new System.Version(item.Attributes[0]!.Value)] = item.Attributes[1]!.Value;
                                        if (item.Attributes.Count > 2)
                                        {
                                            VersionsHash[new System.Version(item.Attributes[0]!.Value)] = item.Attributes[2]!.Value.ToLowerInvariant();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
                if (Assembly.GetExecutingAssembly().GetName().Version < LatestVersion)
                {
                    try
                    {
                        string app_path = AppDomain.CurrentDomain.BaseDirectory.Replace("/", "\\");
                        if (!app_path.EndsWith("\\")) app_path += "\\";
                        string updates_path = app_path + "Updates\\";
                        if (!System.IO.Directory.Exists(updates_path))
                        {
                            System.IO.Directory.CreateDirectory(updates_path);
                        }
                        string update_path = updates_path + LatestVersion.ToString();
                        if (!AvailableVersions.ContainsKey(LatestVersion))
                        {
                            continue;
                        }
                        string update_archive = update_path + ".zip";
                        if (System.IO.File.Exists(update_archive))
                        {
                            if (VersionsHash.ContainsKey(LatestVersion) && VersionsHash[LatestVersion] != GetFileHash(update_archive))
                            {
                                try
                                {
                                    System.IO.File.Delete(update_archive);
                                    System.IO.Directory.Delete(update_path, true);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                        if (!System.IO.File.Exists(update_archive))
                        {
                            using (var client = new HttpClient(httpClientHandler, false))
                            {
                                if (ThreadStop) return;
                                using (var s = client.GetStreamAsync(AvailableVersions[LatestVersion]))
                                {
                                    if (ThreadStop) return;
                                    using (var fs = new FileStream(update_archive, FileMode.OpenOrCreate))
                                    {
                                        s.Result.CopyTo(fs);
                                    }
                                    if (VersionsHash.ContainsKey(LatestVersion) && VersionsHash[LatestVersion] != GetFileHash(update_archive))
                                    {
                                        try
                                        {
                                            System.IO.File.Delete(update_archive);
                                            System.IO.Directory.Delete(update_path, true);
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                        if (System.IO.File.Exists(update_archive) && !System.IO.Directory.Exists(update_path))
                        {
                            System.IO.Directory.CreateDirectory(update_path);
                            System.IO.Compression.ZipFile.ExtractToDirectory(update_archive, update_path);
                        }
                        if (System.IO.Directory.Exists(update_path) && MainWindow.instance != null)
                        {
                            MainWindow.instance.Dispatcher.Invoke(new(() => { if (MainWindow.instance != null) MainWindow.instance.ShowButtonUpdate(); }));
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                IsCheckUpdatesCompleted = true;
            }
        }
    }
}
