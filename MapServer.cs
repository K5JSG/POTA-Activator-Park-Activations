using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PotaActivatorParkActivations
{
    // Request handling for the park map's loopback-only HTTP server (see
    // Form1.StartMapServer for why the map is served rather than opened as
    // a file). Routes:
    //   /gps                 - current GPS mode/fix as JSON (Form1's GPS dropdown)
    //   /assets/...          - Leaflet, protomaps-leaflet and the offline map
    //                          style, built into the exe (MapAssets/ in the
    //                          project) so the map opens with no internet
    //   /offline-map/info    - whether the loaded state's offline map is
    //                          downloaded, as JSON
    //   /offline-map.pmtiles - that map file, served in byte ranges (the
    //                          map reads only the parts it needs to draw)
    //   anything else        - the map page itself
    public static class MapServer
    {
        public const string AssetPrefix = "/assets/";

        // Embedded resource name -> resource, keyed by the path under
        // MapAssets/ with forward slashes. The .csproj names resources after
        // their path, but MSBuild's RecursiveDir keeps Windows backslashes
        // for subfolders, so both are normalized here.
        private static readonly Lazy<Dictionary<string, string>> AssetResourceNames = new(() =>
            typeof(MapServer).Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("MapAssets/", StringComparison.Ordinal))
                .ToDictionary(n => n.Substring("MapAssets/".Length).Replace('\\', '/'), n => n, StringComparer.OrdinalIgnoreCase));

        private static readonly Regex RangeHeader = new(@"^bytes=(\d*)-(\d*)$", RegexOptions.Compiled);

        // Reads one of the built-in map assets as raw bytes.
        public static byte[] ReadAssetBytes(string path)
        {
            if (!AssetResourceNames.Value.TryGetValue(path, out string? name)) return Array.Empty<byte>();
            using var stream = typeof(MapServer).Assembly.GetManifestResourceStream(name);
            if (stream == null) return Array.Empty<byte>();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        // Reads one of the built-in map assets as text (the map page inlines
        // the offline style this way - see MapService.BuildMapHtml).
        public static string ReadAssetText(string path)
        {
            if (!AssetResourceNames.Value.TryGetValue(path, out string? name)) return "";
            using var stream = typeof(MapServer).Assembly.GetManifestResourceStream(name);
            if (stream == null) return "";
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Runs until listener.Stop() makes GetContext() throw. Each request
        // is handled on the thread pool: the offline map fires off many
        // small range reads at once while drawing, and serving them one at a
        // time would leave the map filling in noticeably slower.
        public static void Run(HttpListener listener, string html, Func<string> getGpsJson, Func<OfflineMapInfo> getOfflineMap)
        {
            byte[] htmlBody = Encoding.UTF8.GetBytes(html);
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch
                {
                    return;
                }
                ThreadPool.QueueUserWorkItem(_ => Handle(context, htmlBody, getGpsJson, getOfflineMap));
            }
        }

        public readonly record struct OfflineMapInfo(string StateCode, string? FilePath);

        private static void Handle(HttpListenerContext context, byte[] htmlBody, Func<string> getGpsJson, Func<OfflineMapInfo> getOfflineMap)
        {
            var response = context.Response;
            try
            {
                string path = context.Request.Url?.AbsolutePath ?? "/";
                if (path == "/gps")
                {
                    WriteJson(response, getGpsJson());
                }
                else if (path == "/offline-map/info")
                {
                    var info = getOfflineMap();
                    WriteJson(response, JsonSerializer.Serialize(new { state = info.StateCode, available = info.FilePath != null }));
                }
                else if (path == "/offline-map.pmtiles")
                {
                    ServeFileRange(context, getOfflineMap().FilePath);
                }
                else if (path.StartsWith(AssetPrefix, StringComparison.Ordinal))
                {
                    ServeAsset(response, path.Substring(AssetPrefix.Length));
                }
                else
                {
                    response.ContentType = "text/html; charset=utf-8";
                    Write(response, htmlBody);
                }
            }
            catch
            {
                // Best-effort - a browser tab closing mid-request isn't
                // worth reporting.
            }
            finally
            {
                try { response.OutputStream.Close(); } catch { /* already gone */ }
            }
        }

        private static void Write(HttpListenerResponse response, byte[] body)
        {
            response.ContentLength64 = body.Length;
            response.OutputStream.Write(body, 0, body.Length);
        }

        private static void WriteJson(HttpListenerResponse response, string json)
        {
            response.ContentType = "application/json; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            Write(response, Encoding.UTF8.GetBytes(json));
        }

        private static void ServeAsset(HttpListenerResponse response, string assetPath)
        {
            if (!AssetResourceNames.Value.TryGetValue(assetPath, out string? name))
            {
                response.StatusCode = 404;
                return;
            }
            using var stream = typeof(MapServer).Assembly.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            response.ContentType = Path.GetExtension(assetPath).ToLowerInvariant() switch
            {
                ".js" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
            Write(response, ms.ToArray());
        }

        // Serves filePath honoring a single "Range: bytes=a-b" request header
        // (206 Partial Content) - all the PMTiles reader in the map page ever
        // asks for. Opened with FileShare.ReadWrite | Delete so an Offline
        // Map update or delete in the app is never blocked by the map
        // reading the same file at that moment.
        private static void ServeFileRange(HttpListenerContext context, string? filePath)
        {
            var response = context.Response;
            if (filePath == null || !File.Exists(filePath))
            {
                response.StatusCode = 404;
                return;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = fs.Length;
            long start = 0, end = length - 1;

            string? range = context.Request.Headers["Range"];
            if (range != null)
            {
                var m = RangeHeader.Match(range.Trim());
                if (!m.Success)
                {
                    response.StatusCode = 416;
                    response.Headers["Content-Range"] = $"bytes */{length}";
                    return;
                }
                if (m.Groups[1].Value.Length == 0)
                {
                    // "bytes=-N": the last N bytes.
                    long suffix = long.Parse(m.Groups[2].Value);
                    start = Math.Max(0, length - suffix);
                }
                else
                {
                    start = long.Parse(m.Groups[1].Value);
                    if (m.Groups[2].Value.Length > 0) end = Math.Min(long.Parse(m.Groups[2].Value), length - 1);
                }
                if (start > end || start >= length)
                {
                    response.StatusCode = 416;
                    response.Headers["Content-Range"] = $"bytes */{length}";
                    return;
                }
                response.StatusCode = 206;
                response.Headers["Content-Range"] = $"bytes {start}-{end}/{length}";
            }

            // Lets the page's PMTiles reader notice if the file is replaced
            // by an update while the map is open.
            var modified = File.GetLastWriteTimeUtc(filePath);
            response.Headers["ETag"] = "\"" + length.ToString("x") + "-" + modified.Ticks.ToString("x") + "\"";
            response.Headers["Accept-Ranges"] = "bytes";
            response.Headers["Cache-Control"] = "no-cache";
            response.ContentType = "application/octet-stream";

            long count = end - start + 1;
            response.ContentLength64 = count;
            fs.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[(int)Math.Min(count, 1 << 16)];
            while (count > 0)
            {
                int read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (read <= 0) break;
                response.OutputStream.Write(buffer, 0, read);
                count -= read;
            }
        }
    }
}
