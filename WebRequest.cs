using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.ComponentModel;
using System.Diagnostics;

namespace OVChecker
{
    public class WebRequest
    {
        public static void LogNetwork(string Text)
        {
            System.IO.File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "network.log", "[" + System.DateTime.Now.ToString() + "] " + Text + "\n");
        }
        private static string? GetProxyForURL(string URL)
        {
            string url_lower = URL.ToLower();
            if (url_lower.StartsWith("http:"))
            {
                return Environment.GetEnvironmentVariable("HTTP_PROXY");
            }
            else if (url_lower.StartsWith("https:"))
            {
                return Environment.GetEnvironmentVariable("HTTPS_PROXY");
            }
            return null;
        }
        /// <summary>
        /// Method tries to download file from specified URL and returns contents as a MemoryStream.
        /// Method gets a proxy config from environment variables:
        /// HTTP_PROXY - for urls started with http:
        /// HTTPS_PROXY - for urls started with https:
        /// It tries to achieve file with or without proxy (how file should be achieved at first call -
        /// specified by a corresponding argument).
        /// </summary>
        /// <param name="SourceURL">URL to a source file</param>
        /// <param name="CallWithProxy">Indicates how should be made a request - with or without proxy, if applicable</param>
        /// <returns></returns>
        public static MemoryStream? GetMemoryStreamSimple(string SourceURL, bool CallWithProxy = false)
        {
            var http_client_handler = new HttpClientHandler
            {
                Proxy = null,
                UseProxy = false,
            };
            string? proxy_url = GetProxyForURL(SourceURL);

            if (proxy_url != null && CallWithProxy == true)
            {
                http_client_handler.Proxy = new WebProxy(proxy_url);
                http_client_handler.UseProxy = true;
            }
            var ms = new MemoryStream();
            try
            {
                using (var client = new HttpClient(http_client_handler, false))
                {
                    using (var s = client.GetStreamAsync(SourceURL))
                    {
                        s.Result.CopyTo(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        return ms;
                    }
                }
            }
            catch (Exception ex)
            {
                if (http_client_handler.UseProxy)
                    LogNetwork("Using proxy: " + proxy_url);
                LogNetwork("Cannot get a memory stream for " + SourceURL + ". Exception: " + ex.Message);
            }
            ms.Dispose();
            return null;
        }
        /// <summary>
        /// Method tries to download file from specified URL and returns contents as a MemoryStream.
        /// Method gets a proxy config from environment variables:
        /// HTTP_PROXY - for urls started with http:
        /// HTTPS_PROXY - for urls started with https:
        /// It tries to achieve file with or without proxy (how file should be achieved at first call -
        /// specified by a corresponding argument).
        /// </summary>
        /// <param name="SourceURL">URL to a source file</param>
        /// <param name="FirstCallWithProxy">Indicates how should be made a first request - with or without proxy, if applicable</param>
        /// <returns></returns>
        public static MemoryStream? GetMemoryStream(string SourceURL, bool FirstCallWithProxy = false)
        {
            MemoryStream? memory_stream = null;
            memory_stream = GetMemoryStreamSimple(SourceURL, FirstCallWithProxy);
            if (memory_stream != null)
            {
                return memory_stream;
            }
            return GetMemoryStreamSimple(SourceURL, !FirstCallWithProxy);

        }
        /// <summary>
        /// Method tries to download file from specified URL and returns true if file saved to DestPath.
        /// Method gets a proxy config from environment variables:
        /// HTTP_PROXY - for urls started with http:
        /// HTTPS_PROXY - for urls started with https:
        /// It tries to achieve file with or without proxy (how file should be achieved at first call -
        /// specified by a corresponding argument).
        /// </summary>
        /// <param name="SourceURL">URL to a source file</param>
        /// <param name="DestPath">Path to a file where content should be stored</param>
        /// <param name="CallWithProxy">Indicates how should be made a request - with or without proxy, if applicable</param>
        /// <returns></returns>
        public static bool DownloadFileSimple(string SourceURL, string DestPath, Action<int>? Progress = null, bool CallWithProxy = false)
        {
            var http_client_handler = new HttpClientHandler
            {
                Proxy = null,
                UseProxy = false,
            };

            string? proxy_url = GetProxyForURL(SourceURL);

            if (proxy_url != null && CallWithProxy == true)
            {
                http_client_handler.Proxy = new WebProxy(proxy_url);
                http_client_handler.UseProxy = true;
            }

            try
            {
                using (var client = new HttpClient(http_client_handler, false))
                {
                    using (var s = client.GetStreamAsync(SourceURL))
                    {
                        using (var fs = new FileStream(DestPath, FileMode.OpenOrCreate))
                        {
                            if (Progress == null)
                            {
                                s.Result.CopyTo(fs);
                            }
                            else
                            {
                                using (var ds = s.Result)
                                {
                                    byte[] buffer = new byte[2 * 1024];

                                    int read, total = 0;
                                    while ((read = ds.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        fs.Write(buffer, 0, read);
                                        total += read;
                                        Progress(total);
                                    }
                                }
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (http_client_handler.UseProxy)
                    LogNetwork("Using proxy: " + proxy_url);
                LogNetwork("Cannot download file for " + SourceURL + " and store it as " + DestPath + ". Exception: " + ex.Message);
            }
            return false;
        }
        /// <summary>
        /// Method tries to download file from specified URL and returns true if file saved to DestPath.
        /// Method gets a proxy config from environment variables:
        /// HTTP_PROXY - for urls started with http:
        /// HTTPS_PROXY - for urls started with https:
        /// It tries to achieve file with or without proxy (how file should be achieved at first call -
        /// specified by a corresponding argument).
        /// </summary>
        /// <param name="SourceURL">URL to a source file</param>
        /// <param name="DestPath">Path to a file where content should be stored</param>
        /// <param name="Progress">Callback called when next portion of data has been received</param>
        /// <param name="FirstCallWithProxy">Indicates how should be made a first request - with or without proxy, if applicable</param>
        /// <returns></returns>
        public static bool DownloadFile(string SourceURL, string DestPath, Action<int>? Progress = null, bool FirstCallWithProxy = false)
        {
            if (DownloadFileSimple(SourceURL, DestPath, Progress, FirstCallWithProxy))
                return true;
            return DownloadFileSimple(SourceURL, DestPath, Progress, !FirstCallWithProxy);
        }
        /// <summary>
        /// Method tries to download file from specified URL and returns true if file saved to DestPath.
        /// Method gets a proxy config from environment variables:
        /// HTTP_PROXY - for urls started with http:
        /// HTTPS_PROXY - for urls started with https:
        /// It tries to achieve file with or without proxy (how file should be achieved at first call -
        /// specified by a corresponding argument).
        /// </summary>
        /// <param name="SourceURL">URL to a source file</param>
        /// <param name="DestPath">Path to a file where content should be stored</param>
        /// <param name="FirstCallWithProxy">Indicates how should be made a first request - with or without proxy, if applicable</param>
        /// <returns></returns>
        public static bool DownloadFile(string SourceURL, string DestPath, bool FirstCallWithProxy = false)
        {
            if (DownloadFileSimple(SourceURL, DestPath, null, FirstCallWithProxy))
                return true;
            return DownloadFileSimple(SourceURL, DestPath, null, !FirstCallWithProxy);
        }
    }
}
