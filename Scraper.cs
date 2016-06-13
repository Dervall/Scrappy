using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scrappy
{
    public class Scraper
    {
        // Regex is immutable and thread safe. Reuse one object for all calls across all threads
        private static readonly Regex HrefRegex;

        // Keeps the state of a current download, to make the class reentrant and thread safe
        private class DownloadState
        {
            internal readonly Uri Root;
            internal readonly HashSet<Uri> Visited = new HashSet<Uri>();
            internal readonly object SyncRoot = new object();

            public DownloadState(Uri root)
            {
                Root = root;
            }
        }

        static Scraper()
        {
            HrefRegex = new Regex(@"href\s*=\s*['""]([^""']*)['""]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        
        public async Task Download(Uri root)
        {
            await Download(root, new DownloadState(root));
        }

        private async Task Download(Uri uri, DownloadState state)
        {
            Console.WriteLine($"Downloading {uri}");

            var childTasks = new List<Task>();

            try
            {
                var request = WebRequest.Create(uri);

                using (var response = await request.GetResponseAsync())
                {
                    if (response.ContentType.StartsWith("text/html") ||
                        response.ContentType.StartsWith("application/xhtml+xml"))
                    {
                        string content;
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            content = await reader.ReadToEndAsync();
                        }

                        await WriteFile(uri, Encoding.UTF8.GetBytes(content));
                        
                        childTasks = GetLinks(uri, content, state).Select(f => Download(f, state)).ToList();
                    }
                    else
                    {
                        using (var responseStream = response.GetResponseStream())
                        {
                            await WriteFile(uri, responseStream);
                        }
                    }
                }

                Console.WriteLine($"Finished downloading {uri}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to download {uri}, {e.Message}");
            }

            await Task.WhenAll(childTasks);
        }

        private IEnumerable<Uri> GetLinks(Uri url, string content, DownloadState state)
        {
            foreach (var link in HrefRegex.Matches(content)
                .Cast<Match>()
                .Select(f => f.Groups[1].Value)
                .Where(f => !f.StartsWith("#"))             // Remove anchor links to same page
                .Where(f => !f.StartsWith("javascript:"))   // Remove javascript links
                .Where(f => !f.StartsWith("mailto:"))       // Remove mailto links
                .Where(f => f != "/"))                      // Remove links that goes to the same page
            {
                Uri absoluteUri;
                try
                {
                    absoluteUri = new Uri(url, link);
                }
                catch (UriFormatException e)
                {
                    // Catch bad URLs, so we will not stop the downloading progress
                    Console.WriteLine($"Malformed URI found at {url}. {e.Message}. Ignoring.");                        
                    continue;
                }

                // Make sure we stay in the same domain, so we're not downloading the entire internet
                if (absoluteUri.Host.EndsWith(state.Root.Host, StringComparison.InvariantCultureIgnoreCase))
                {
                    lock (state.SyncRoot)
                    {
                        if (!state.Visited.Add(absoluteUri))
                        {
                            continue;
                        }
                    }
                    yield return absoluteUri;
                }
            }
        }

        private static async Task WriteFile(Uri url, byte[] content)
        {
            using (var ms = new MemoryStream(content))
            {
                await WriteFile(url, ms);
            }
        }

        private static async Task WriteFile(Uri url, Stream content)
        {
            var safePath = new [] { '?', '&'}.Aggregate(url.PathAndQuery.TrimStart('/'), (path, c) => path.Replace(c, '_'));
            if (string.IsNullOrEmpty(safePath))
            {
                safePath = "index.html";
            }

            if (safePath.EndsWith("/"))
            {
                // Unnamed file, typically index.html. Might be anything though
                safePath = Path.Combine(safePath, "index.html");
            }

            var fileName = Path.Combine(url.Host.Replace(".", "_"), safePath);
            var directoryName = Path.GetDirectoryName(fileName);
            
            // Ensure the path exists
            Directory.CreateDirectory(directoryName);
            
            using (var fs = File.OpenWrite(fileName))
            {
                await content.CopyToAsync(fs);
            }
        }
    }
}