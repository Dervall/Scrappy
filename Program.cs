using System;

namespace Scrappy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter URL:");
            var url = Console.ReadLine();
            if (!string.IsNullOrEmpty(url))
            {
                var scraper = new Scraper();
                scraper.Download(new Uri(url)).Wait();
                Console.WriteLine("Download completed!");
                Console.ReadKey();
            }
        }
    }
}
