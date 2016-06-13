# Web scraper

This is a basic asynchronous web scraper. Program expects you to input a website address (including http/https, e.g. http://www.example.com) and will attempt to scrape as much content as it can see from that page.

# Implementation

Each page is downloaded using `WebRequest` async methods. If the content type indicates that this is a HTML page, the page is scanned through using a regular expression. This expression matches any `href="[...]"` tag. For each of these tags a new download is started asynchronously and in parallell. When there are no more links to find in any document the process will complete.

Files are saved to the output directory, in a subfolder named after the host. Scraping is limited to the host of the root URL, but may travese into subdomains of the root host. For example, entering http://example.com might follow links into http://www.example.com but not http://www.example2.com.

Files are named after the sanitized URL. If there are no page name in the URL, files are saved into index.html.

The scraper is thread-safe and reentrant. Failures when downloading are reported and then ignored, allowing the parser to complete as much work as it can.

# Known issues

* Where there are links to both `/folder` and `/folder/` in the same document, the scraper cannot distinguish between if `folder` is a directory or a file. The first ocurrence will be used.

* There are no retries of aborted downloads.

* Reporting is fairly minimal, and done directly to the console. Using callbacks or events would be preferableto aggregate the information.

