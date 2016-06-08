using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scrappy
{
    class Program
    {
        static void Main(string[] args)
        {

            var scraper = new Scraper(new Uri("http://tretton37.com"));
            scraper.Download().Wait();
            Console.ReadKey();
        }
    }

    public class Scraper
    {
        private readonly Uri _root;
        private readonly HashSet<Uri> _visited;
        private readonly Regex _hrefRegex;
        private readonly object _syncRoot = new object();

        public Scraper(Uri root)
        {
            _root = root;   
            _visited = new HashSet<Uri>();;   
            _hrefRegex = new Regex(@"href\s*=\s*['""]([^""']*)['""]", RegexOptions.Compiled | RegexOptions.IgnoreCase);  
        }

        public async Task Download()
        {
            await Download(_root);
        }

        public async Task Download(Uri url)
        {
            Console.WriteLine($"Downloading {url}");

            var childTasks = new List<Task>();

            try
            {
                var request = WebRequest.Create(url);

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

                        await WriteFile(url, Encoding.UTF8.GetBytes(content));
                        
                        childTasks = GetLinks(url, content).Select(Download).ToList();
                    }
                    else
                    {
                        using (var responseStream = response.GetResponseStream())
                        {
                            await WriteFile(url, responseStream);
                        }
                        
                    }
                }

                Console.WriteLine($"Finished downloading {url}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to download {url}, {e.Message}");
            }

            await Task.WhenAll(childTasks);
        }

        private IEnumerable<Uri> GetLinks(Uri url, string content)
        {
            foreach (var link in _hrefRegex.Matches(content)
                .Cast<Match>()
                .Select(f => f.Groups[1].Value)
                .Where(f => !f.StartsWith("#")) // Remove anchor links to same page
                .Where(f => !f.StartsWith("javascript:")) // Remove javascript links
                .Where(f => f != "/")) // Remove links that goes to the same page
            {
                var absoluteUri = new Uri(url, link);

                // Make sure we stay in the same domain, so we're not downloading the entire internet
                if (absoluteUri.Host.EndsWith(_root.Host))
                {
                    lock (_syncRoot)
                    {
                        if (!_visited.Add(absoluteUri))
                        {
                            continue;
                        }
                    }
                    yield return absoluteUri;
                }
            }
        }

        private async Task WriteFile(Uri url, byte[] content)
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
