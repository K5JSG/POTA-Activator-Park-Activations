using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PotaActivatorParkActivations
{
    // Detects candidate "Xfer" groups (a "2fer", "3fer", etc.) - parks whose boundaries overlap,
    // so one activation counts for all of them at once. Unlike KFF or county
    // lookups, POTA's own API exposes no boundary geometry, so this reads real
    // land-unit boundary polygons published per-state by the potamap.us project
    // (github.com/cwhelchel/potamap.ol, public/data/US-XX/*.geojson) - mostly
    // PAD-US (Protected Areas Database of the United States) extracts, plus a
    // handful of purpose-built extra layers for things PAD-US doesn't cover
    // (e.g. NY's Erie Canalway corridor) - then matches those polygons to POTA
    // parks by name and does a point-in-polygon test - exactly what POTA's own
    // n-fer rule is actually asking ("is my activation point inside more than
    // one park's boundary?").
    //
    // This is a best-effort heuristic, not authoritative: it depends on a
    // community-maintained boundary extract and on matching those units' names
    // to POTA park names (exactly - see NormalizeName below), so a boundary
    // whose name isn't an exact word-for-word match to any POTA park in the
    // state - or that simply doesn't exist in this data at all - won't be found.
    // Callers should present results as candidates to verify, matching POTA's
    // own rule that an activator must confirm an overlap before claiming it.
    public static class FerLookupService
    {
        private const string TreeApiUrl = "https://api.github.com/repos/cwhelchel/potamap.ol/git/trees/main?recursive=1";
        private const string RawBaseUrl = "https://raw.githubusercontent.com/cwhelchel/potamap.ol/main/";
        private const string SourceIndexFileName = "FerSourceIndex.cache.json";

        // A single land-unit polygon (or multi-part unit, e.g. a forest split
        // into several pieces), reduced to the same compact shape
        // CountyLookupService uses for counties.json: a bounding box for a cheap
        // pre-filter, plus Polys[part][ring] = flat [lon,lat,lon,lat,...]. The
        // first ring in a part is its outer boundary; any further rings are holes.
        public class BoundaryFeature
        {
            public string Name { get; set; } = "";
            public double MinLon { get; set; }
            public double MinLat { get; set; }
            public double MaxLon { get; set; }
            public double MaxLat { get; set; }
            public List<List<double[]>> Polys { get; set; } = new();
        }

        // Bumped whenever DownloadSourceIndexAsync's file-selection logic changes
        // (which files count as boundary data) or ComputeFers' matching logic
        // changes (how names are compared) - an on-disk cache written under an
        // older version is treated as absent, regardless of how recently it was
        // written, so a code change here always takes effect on the next load
        // instead of silently being masked by a still-"fresh" cache from before
        // the change. Confirmed this was a real gap, not just a theoretical one:
        // broadening file discovery to include NY's eriecanalway_sim.geojson had
        // no effect on an already-cached FerBoundaries_NY.cache.json until this
        // version check was added.
        private const int CacheSchemaVersion = 2;

        private class BoundaryCache
        {
            public int SchemaVersion { get; set; }
            public DateTime CachedUtc { get; set; }
            public List<BoundaryFeature> Features { get; set; } = new();
        }

        private class SourceIndexCache
        {
            public int SchemaVersion { get; set; }
            public DateTime CachedUtc { get; set; }
            public Dictionary<string, List<string>> StateUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        // A trail's real path, for the separate "within 100 feet of the trail"
        // rule POTA states for trail-type parks (distinct from the full-
        // containment rule area parks use - see IsPointNearTrail/TrailToleranceKm
        // below). Lines[part] = flat [lon,lat,lon,lat,...] - one entry per
        // disconnected segment (a MultiLineString part, or a GPX <trkseg>) - with
        // no closure/ring semantics, unlike BoundaryFeature.Polys.
        public class TrailRoute
        {
            public string Name { get; set; } = "";
            public double MinLon { get; set; }
            public double MinLat { get; set; }
            public double MaxLon { get; set; }
            public double MaxLat { get; set; }
            public List<double[]> Lines { get; set; } = new();
        }

        private class TrailRouteCache
        {
            public int SchemaVersion { get; set; }
            public DateTime CachedUtc { get; set; }
            public List<TrailRoute> Routes { get; set; } = new();
        }

        public class FerResult
        {
            // Reference -> other references it may overlap with.
            public Dictionary<string, List<string>> Fers { get; } = new(StringComparer.OrdinalIgnoreCase);
            // How many loaded parks were matched to a boundary polygon by name -
            // used to tell the user how complete this state's coverage is.
            public int MatchedBoundaryCount { get; set; }
        }

        public class TrailFerResult
        {
            // Reference -> other references within 100 ft of the same trail.
            public Dictionary<string, List<string>> Fers { get; } = new(StringComparer.OrdinalIgnoreCase);
            // Trail parks that matched a route AND had at least one park within
            // 100 ft of it - callers add these to the displayed list even when
            // the trail's own POTA point isn't in the loaded state, since that's
            // the only way the row (and its Fers) becomes visible.
            public List<ParkRecord> RelevantTrailParks { get; } = new();
        }

        // ---- Step 1: find out which boundary files exist for a state --------------------

        // Boundary filenames aren't consistent across states (PAD-US version
        // number and layer name vary, and a state can also carry one-off extra
        // files with an arbitrary name), so rather than guessing a pattern this
        // reads the repo's full file tree once and filters it - cheap (~180KB)
        // and cached like everything else here.
        private static async Task<Dictionary<string, List<string>>> DownloadSourceIndexAsync(HttpClient http)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, TreeApiUrl);
            // The GitHub API rejects requests with no User-Agent header.
            request.Headers.UserAgent.ParseAdd("POTA-Activator-Park-Activations");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await http.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cts.Token);

            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tree", out var tree)) return result;

            const string prefix = "public/data/US-";
            foreach (var entry in tree.EnumerateArray())
            {
                if (!entry.TryGetProperty("path", out var pathEl)) continue;
                string path = pathEl.GetString() ?? "";
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!path.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase)) continue;

                string remainder = path.Substring(prefix.Length);
                int slash = remainder.IndexOf('/');
                if (slash < 0) continue;

                string stateCode = remainder.Substring(0, slash);
                string fileName = remainder.Substring(slash + 1);

                // Most states only carry a PADUS*.geojson file, but a few also
                // have a purpose-built extra layer for something PAD-US doesn't
                // cover well - e.g. NY's eriecanalway_sim.geojson (from
                // nycanalmap.com) or new_york_state_parks.geojson. Rather than
                // only ever looking for "PADUS" in the name, pull in every real
                // boundary file and skip only the ones known not to be one:
                // county outlines, the point-marker parks-US-XX.geojson (POTA's
                // own pins, not polygons), and SOTA summit-association region
                // files (named like "W2--EH.geojson", "W0C--FR.geojson").
                if (fileName.Equals("counties.geojson", StringComparison.OrdinalIgnoreCase)) continue;
                if (fileName.StartsWith("parks-US-", StringComparison.OrdinalIgnoreCase)) continue;
                if (fileName.Contains("--")) continue;

                if (!result.TryGetValue(stateCode, out var urls))
                {
                    urls = new List<string>();
                    result[stateCode] = urls;
                }
                urls.Add(RawBaseUrl + path);
            }

            return result;
        }

        private static async Task<SourceIndexCache?> TryReadValidSourceIndexAsync(string path)
        {
            var cached = await TryReadJsonAsync<SourceIndexCache>(path);
            return cached != null && cached.SchemaVersion == CacheSchemaVersion ? cached : null;
        }

        private static async Task<BoundaryCache?> TryReadValidBoundaryCacheAsync(string path)
        {
            var cached = await TryReadJsonAsync<BoundaryCache>(path);
            return cached != null && cached.SchemaVersion == CacheSchemaVersion ? cached : null;
        }

        private static async Task<Dictionary<string, List<string>>> EnsureSourceIndexAsync(HttpClient http, string cacheFolder, TimeSpan maxAge)
        {
            string cachePath = Path.Combine(cacheFolder, SourceIndexFileName);

            if (File.Exists(cachePath))
            {
                var cached = await TryReadValidSourceIndexAsync(cachePath);
                if (cached != null && DateTime.UtcNow - cached.CachedUtc < maxAge)
                    return cached.StateUrls;
            }

            try
            {
                var fresh = await DownloadSourceIndexAsync(http);
                await TryWriteJsonAsync(cachePath, new SourceIndexCache
                {
                    SchemaVersion = CacheSchemaVersion,
                    CachedUtc = DateTime.UtcNow,
                    StateUrls = fresh
                });
                return fresh;
            }
            catch
            {
                var cached = await TryReadValidSourceIndexAsync(cachePath);
                return cached?.StateUrls ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // ---- Step 2: download + parse a state's boundary polygons (cached) ---------------

        public static async Task<List<BoundaryFeature>> EnsureBoundariesAsync(
            HttpClient http, string cacheFolder, string stateCode, TimeSpan maxAge, Action<string>? statusCallback = null)
        {
            string cachePath = Path.Combine(cacheFolder, $"FerBoundaries_{stateCode.ToUpperInvariant()}.cache.json");

            if (File.Exists(cachePath))
            {
                var cached = await TryReadValidBoundaryCacheAsync(cachePath);
                if (cached != null && DateTime.UtcNow - cached.CachedUtc < maxAge)
                    return cached.Features;
            }

            try
            {
                var index = await EnsureSourceIndexAsync(http, cacheFolder, maxAge);
                if (!index.TryGetValue(stateCode, out var urls) || urls.Count == 0)
                {
                    // No known PAD-US source for this state - use whatever we have
                    // cached (even if stale) rather than nothing.
                    var stale = await TryReadValidBoundaryCacheAsync(cachePath);
                    return stale?.Features ?? new List<BoundaryFeature>();
                }

                statusCallback?.Invoke("Downloading park boundary data for overlap detection (one-time per state)...");

                var allFeatures = new List<BoundaryFeature>();
                foreach (var url in urls)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                        string json = await http.GetStringAsync(url, cts.Token);
                        allFeatures.AddRange(ParseGeoJsonFeatures(json));
                    }
                    catch
                    {
                        // Skip this one file - partial boundary data (e.g. just the
                        // Designation layer, missing Fee) is still useful.
                    }
                }

                if (allFeatures.Count == 0)
                {
                    var stale = await TryReadValidBoundaryCacheAsync(cachePath);
                    return stale?.Features ?? new List<BoundaryFeature>();
                }

                await TryWriteJsonAsync(cachePath, new BoundaryCache
                {
                    SchemaVersion = CacheSchemaVersion,
                    CachedUtc = DateTime.UtcNow,
                    Features = allFeatures
                });
                return allFeatures;
            }
            catch
            {
                var stale = await TryReadValidBoundaryCacheAsync(cachePath);
                return stale?.Features ?? new List<BoundaryFeature>();
            }
        }

        private static List<BoundaryFeature> ParseGeoJsonFeatures(string json)
        {
            var results = new List<BoundaryFeature>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("features", out var features)) return results;

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("properties", out var props)) continue;
                if (!props.TryGetProperty("NAME", out var nameEl) || nameEl.ValueKind != JsonValueKind.String) continue;
                string name = nameEl.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!feature.TryGetProperty("geometry", out var geom)) continue;
                if (!geom.TryGetProperty("type", out var typeEl)) continue;
                if (!geom.TryGetProperty("coordinates", out var coords)) continue;

                var parts = new List<List<double[]>>();
                string geomType = typeEl.GetString() ?? "";
                if (geomType == "Polygon")
                {
                    parts.Add(ParsePolygonRings(coords));
                }
                else if (geomType == "MultiPolygon")
                {
                    foreach (var poly in coords.EnumerateArray())
                        parts.Add(ParsePolygonRings(poly));
                }
                else
                {
                    continue;
                }

                parts.RemoveAll(p => p.Count == 0);
                if (parts.Count == 0) continue;

                double minLon = double.MaxValue, minLat = double.MaxValue;
                double maxLon = double.MinValue, maxLat = double.MinValue;
                foreach (var ring in parts.SelectMany(p => p))
                {
                    for (int i = 0; i < ring.Length; i += 2)
                    {
                        double lon = ring[i], lat = ring[i + 1];
                        if (lon < minLon) minLon = lon;
                        if (lon > maxLon) maxLon = lon;
                        if (lat < minLat) minLat = lat;
                        if (lat > maxLat) maxLat = lat;
                    }
                }

                results.Add(new BoundaryFeature
                {
                    Name = name,
                    MinLon = minLon,
                    MinLat = minLat,
                    MaxLon = maxLon,
                    MaxLat = maxLat,
                    Polys = parts
                });
            }

            return results;
        }

        private static List<double[]> ParsePolygonRings(JsonElement polygonCoords)
        {
            var rings = new List<double[]>();
            foreach (var ringEl in polygonCoords.EnumerateArray())
            {
                var points = new List<double>();
                foreach (var ptEl in ringEl.EnumerateArray())
                {
                    var coords = ptEl.EnumerateArray();
                    if (!coords.MoveNext()) continue;
                    double lon = coords.Current.GetDouble();
                    if (!coords.MoveNext()) continue;
                    double lat = coords.Current.GetDouble();
                    points.Add(lon);
                    points.Add(lat);
                }
                // A real ring needs at least 3 distinct points (6 numbers).
                if (points.Count >= 6) rings.Add(points.ToArray());
            }
            return rings;
        }

        // ---- National trail routes (shared across all states, cached once) --------------

        private const string TrailRoutesFileName = "FerTrailRoutes.cache.json";
        private const string TrailCommonBaseUrl = "https://raw.githubusercontent.com/cwhelchel/potamap.ol/main/public/data/US-common/";

        // The ~15 congressionally-designated National Historic/Scenic Trails
        // that have real MultiLineString route data in potamap.ol's shared (not
        // per-state) US-common folder. Hardcoded rather than discovered via the
        // tree API the way per-state boundary files are: this is one small,
        // stable folder, not 57 inconsistently-named per-state ones. Excludes
        // US-common/FFMA_combined.geojson, which is Maidenhead grid squares, not
        // a trail.
        private static readonly string[] NationalTrailFileKeys =
        {
            "Mormon_Pioneer_NHT", "Pony_Express_NHT", "at", "butterfield_ovrlnd_nht",
            "cali_nht", "ice_age_trail2", "lc_nht", "los_tejas", "nct_nst",
            "old_spanish_nht", "or_nht", "safe", "tierra_adentro", "tot", "waro"
        };

        // New York's own official GPX download for the Empire State Trail
        // (empiretrail.ny.gov/trip-planning) - a ZIP of 3 GPX files, one per leg
        // (Hudson Valley, Champlain Valley, Erie Canal Buffalo-Albany). The URL
        // has a date component NY may change on a future refresh (e.g. a
        // "2026-xx" path replacing "2025-10") - if it 404s, this is skipped like
        // any other unreachable source below, not fatal.
        private const string EmpireTrailZipUrl = "https://empiretrail.ny.gov/sites/default/files/2025-10/GPX%20Files.zip";

        private static async Task<TrailRouteCache?> TryReadValidTrailCacheAsync(string path)
        {
            var cached = await TryReadJsonAsync<TrailRouteCache>(path);
            return cached != null && cached.SchemaVersion == CacheSchemaVersion ? cached : null;
        }

        public static async Task<List<TrailRoute>> EnsureTrailRoutesAsync(
            HttpClient http, string cacheFolder, TimeSpan maxAge, Action<string>? statusCallback = null)
        {
            string cachePath = Path.Combine(cacheFolder, TrailRoutesFileName);

            if (File.Exists(cachePath))
            {
                var cached = await TryReadValidTrailCacheAsync(cachePath);
                if (cached != null && DateTime.UtcNow - cached.CachedUtc < maxAge)
                    return cached.Routes;
            }

            statusCallback?.Invoke("Downloading national trail route data (one-time)...");

            var routes = new List<TrailRoute>();

            foreach (var fileKey in NationalTrailFileKeys)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    string json = await http.GetStringAsync(TrailCommonBaseUrl + fileKey + ".geojson", cts.Token);
                    routes.AddRange(ParseGeoJsonTrailFeatures(json));
                }
                catch
                {
                    // Skip this one trail - the rest are still useful.
                }
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                byte[] zipBytes = await http.GetByteArrayAsync(EmpireTrailZipUrl, cts.Token);
                var empireLines = ParseGpxZipLines(zipBytes);
                if (empireLines.Count > 0)
                    routes.Add(BuildTrailRoute("Empire State Trail", empireLines));
            }
            catch
            {
                // See EmpireTrailZipUrl's comment - a state-government URL with a
                // date in the path is expected to eventually go stale.
            }

            if (routes.Count == 0)
            {
                var stale = await TryReadValidTrailCacheAsync(cachePath);
                return stale?.Routes ?? new List<TrailRoute>();
            }

            await TryWriteJsonAsync(cachePath, new TrailRouteCache
            {
                SchemaVersion = CacheSchemaVersion,
                CachedUtc = DateTime.UtcNow,
                Routes = routes
            });
            return routes;
        }

        private static List<TrailRoute> ParseGeoJsonTrailFeatures(string json)
        {
            var results = new List<TrailRoute>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("features", out var features)) return results;

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("properties", out var props)) continue;

                // NAME_EXT (when present) is the fuller of the two - e.g. "North
                // Country Trail NST" vs. a bare NAME of "NCT_NST" - but neither
                // property is guaranteed, so fall back from one to the other.
                string? name = GetStringProp(props, "NAME_EXT") ?? GetStringProp(props, "NAME");
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!feature.TryGetProperty("geometry", out var geom)) continue;
                if (!geom.TryGetProperty("type", out var typeEl)) continue;
                if (!geom.TryGetProperty("coordinates", out var coords)) continue;

                var lines = new List<double[]>();
                string geomType = typeEl.GetString() ?? "";
                if (geomType == "LineString")
                {
                    var line = ParseLineCoordinates(coords);
                    if (line.Length >= 4) lines.Add(line);
                }
                else if (geomType == "MultiLineString")
                {
                    foreach (var part in coords.EnumerateArray())
                    {
                        var line = ParseLineCoordinates(part);
                        if (line.Length >= 4) lines.Add(line);
                    }
                }
                else
                {
                    continue;
                }

                if (lines.Count == 0) continue;
                results.Add(BuildTrailRoute(name!, lines));
            }

            return results;
        }

        private static string? GetStringProp(JsonElement props, string propertyName)
        {
            if (props.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
            return null;
        }

        private static double[] ParseLineCoordinates(JsonElement lineCoords)
        {
            var points = new List<double>();
            foreach (var ptEl in lineCoords.EnumerateArray())
            {
                var coords = ptEl.EnumerateArray();
                if (!coords.MoveNext()) continue;
                double lon = coords.Current.GetDouble();
                if (!coords.MoveNext()) continue;
                double lat = coords.Current.GetDouble();
                points.Add(lon);
                points.Add(lat);
            }
            return points.ToArray();
        }

        // Reads every <trkseg>'s <trkpt lat lon> points out of every .gpx file in
        // the zip (one flat polyline per segment) - standard GPX 1.1 structure,
        // same one every consumer-grade GPS/mapping tool produces and reads.
        private static List<double[]> ParseGpxZipLines(byte[] zipBytes)
        {
            var lines = new List<double[]>();
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.FullName.Contains("__MACOSX")) continue;

                try
                {
                    using var entryStream = entry.Open();
                    var xdoc = XDocument.Load(entryStream);
                    XNamespace ns = xdoc.Root!.GetDefaultNamespace();

                    foreach (var trkseg in xdoc.Descendants(ns + "trkseg"))
                    {
                        var points = new List<double>();
                        foreach (var trkpt in trkseg.Elements(ns + "trkpt"))
                        {
                            string? latStr = trkpt.Attribute("lat")?.Value;
                            string? lonStr = trkpt.Attribute("lon")?.Value;
                            if (latStr == null || lonStr == null) continue;
                            if (!double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;
                            if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                            points.Add(lon);
                            points.Add(lat);
                        }
                        if (points.Count >= 4) lines.Add(points.ToArray());
                    }
                }
                catch
                {
                    // Skip this one leg file if it's malformed - the others are
                    // still useful.
                }
            }

            return lines;
        }

        private static TrailRoute BuildTrailRoute(string name, List<double[]> lines)
        {
            double minLon = double.MaxValue, minLat = double.MaxValue;
            double maxLon = double.MinValue, maxLat = double.MinValue;
            foreach (var line in lines)
            {
                for (int i = 0; i < line.Length; i += 2)
                {
                    double lon = line[i], lat = line[i + 1];
                    if (lon < minLon) minLon = lon;
                    if (lon > maxLon) maxLon = lon;
                    if (lat < minLat) minLat = lat;
                    if (lat > maxLat) maxLat = lat;
                }
            }

            return new TrailRoute { Name = name, MinLon = minLon, MinLat = minLat, MaxLon = maxLon, MaxLat = maxLat, Lines = lines };
        }

        private static async Task<T?> TryReadJsonAsync<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<T>(text);
            }
            catch
            {
                return null;
            }
        }

        private static async Task TryWriteJsonAsync<T>(string path, T value)
        {
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value));
            }
            catch
            {
                // Not being able to write the cache isn't fatal - just means this
                // state's boundary data gets re-downloaded next time.
            }
        }

        // ---- Steps 3-4: match parks to boundaries, then test containment ----------------

        private static readonly Regex NonAlphaNumRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);

        // Normalizes to a word-order-independent key: lowercase, punctuation
        // collapsed to spaces, then the words sorted. Confirmed necessary, not
        // just theoretical - POTA's own bulk park list has US-2097 as "Lock 32
        // Canal State Park", while the matching PAD-US polygon is named "Lock 32
        // State Canal Park" (same words, "Canal"/"State" transposed). Sorting
        // the words still requires an exact set match (every word present, none
        // extra) - it's order-independence, not fuzzy/substring matching, so it
        // doesn't loosen how easily two genuinely different names can collide.
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string collapsed = NonAlphaNumRegex.Replace(name.ToLowerInvariant(), " ").Trim();
            if (collapsed.Length == 0) return "";

            var words = collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(words, StringComparer.Ordinal);
            return string.Join(" ", words);
        }

        private static HashSet<string> NormalizeWordSet(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new HashSet<string>();
            string collapsed = NonAlphaNumRegex.Replace(name.ToLowerInvariant(), " ").Trim();
            if (collapsed.Length == 0) return new HashSet<string>();
            return new HashSet<string>(collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // Words that only ever describe jurisdiction/scope, never what kind of
        // place something is - confirmed safe to add on one side of a name
        // without risking a different real place: "X Refuge" becoming "X
        // National Wildlife Refuge" is still the same refuge, but "X Park"
        // becoming "X Forest" (or vice versa) might well be two different
        // places, so type-of-place words are deliberately NOT on this list.
        // "Corridor", "Heritage", and "Area" are borderline - they can act as a
        // designation's type-noun (e.g. "National Heritage Corridor" the way
        // "Park" is the type-noun in "National Park") - but are included here as
        // a deliberate, narrow exception: confirmed against a real case (POTA's
        // "Erie Canalway Corridor National Heritage Area" vs. the boundary
        // data's plain "Erie Canalway").
        private static readonly HashSet<string> GenericDescriptorWords = new(StringComparer.Ordinal)
        {
            "state", "national", "federal", "county", "municipal", "local", "city", "town",
            "area", "corridor", "heritage"
        };

        // A looser fallback for when no boundary's name exactly matches a park's
        // (word-for-word, any order): true if one name's words are a complete
        // subset of the other's, and every extra word on the larger side is a
        // safe, jurisdiction-only word from GenericDescriptorWords above. Unlike
        // a plain subset test, this rejects a "substitution" - a word swapped
        // for a different one on each side - even if both individual words look
        // harmless, since that pattern (e.g. "X Park" vs "X Forest") is exactly
        // the shape a genuinely different place would take.
        private static bool IsSafeSubsetMatch(HashSet<string> a, HashSet<string> b)
        {
            var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
            if (smaller.Count == 0) return false;
            if (!smaller.IsSubsetOf(larger)) return false;

            foreach (var word in larger)
            {
                if (smaller.Contains(word)) continue;
                if (!GenericDescriptorWords.Contains(word)) return false;
            }

            return true;
        }

        // How far outside a boundary's (possibly simplified/generalized) edge a
        // park's own point can fall and still count as "inside" it. Sized from a
        // real validated case: Mount Rushmore's POTA point sits ~0.54 km outside
        // Black Hills National Forest's PAD-US polygon, purely from boundary
        // simplification - not a real gap. 0.75 km covers that with some margin
        // while staying far tighter than the distance between genuinely separate,
        // merely-nearby parks.
        private const double ToleranceKm = 0.75;
        private const double KmPerDegreeLat = 111.32;

        // A land unit can be split into several polygon parts that all share the
        // same NAME - grouped here so they're tested together. Shared by
        // ComputeFers and ComputeTrailFers (the latter needs a test park's own
        // matched boundary, not just its point, to check against a trail route).
        private static (Dictionary<string, List<BoundaryFeature>> Groups, List<(string Key, HashSet<string> Words)> WordSets) BuildBoundaryGroups(
            List<BoundaryFeature> boundaries)
        {
            var boundaryGroups = new Dictionary<string, List<BoundaryFeature>>();
            foreach (var b in boundaries)
            {
                string key = NormalizeName(b.Name);
                if (key.Length == 0) continue;
                if (!boundaryGroups.TryGetValue(key, out var list))
                {
                    list = new List<BoundaryFeature>();
                    boundaryGroups[key] = list;
                }
                list.Add(b);
            }

            // Precomputed word sets for the safe-subset fallback, one per
            // distinct boundary name - reusing the already-sorted key instead of
            // re-normalizing every boundary name from scratch.
            var boundaryWordSets = boundaryGroups.Keys
                .Select(key => (Key: key, Words: new HashSet<string>(key.Split(' ', StringSplitOptions.RemoveEmptyEntries))))
                .ToList();

            return (boundaryGroups, boundaryWordSets);
        }

        // Finds the one boundary group matching parkName - exact word-for-word
        // first, then the safe-subset fallback (see IsSafeSubsetMatch). More
        // than one safe-subset candidate is ambiguous and treated as no match,
        // same as zero.
        private static List<BoundaryFeature>? FindOwnedBoundaries(
            string parkName,
            Dictionary<string, List<BoundaryFeature>> boundaryGroups,
            List<(string Key, HashSet<string> Words)> boundaryWordSets)
        {
            string key = NormalizeName(parkName);
            if (key.Length == 0) return null;

            if (boundaryGroups.TryGetValue(key, out var exact)) return exact;

            var ownerWords = NormalizeWordSet(parkName);
            List<BoundaryFeature>? fallback = null;
            int candidateCount = 0;
            foreach (var (boundaryKey, boundaryWords) in boundaryWordSets)
            {
                if (!IsSafeSubsetMatch(ownerWords, boundaryWords)) continue;
                candidateCount++;
                fallback = boundaryGroups[boundaryKey];
                if (candidateCount > 1) break;
            }

            return candidateCount == 1 ? fallback : null;
        }

        public static FerResult ComputeFers(List<ParkRecord> parks, List<BoundaryFeature> boundaries)
        {
            var result = new FerResult();
            if (parks.Count == 0 || boundaries.Count == 0) return result;

            var (boundaryGroups, boundaryWordSets) = BuildBoundaryGroups(boundaries);

            var accum = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            void AddPair(string a, string b)
            {
                if (!accum.TryGetValue(a, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    accum[a] = set;
                }
                set.Add(b);
            }

            foreach (var owner in parks)
            {
                var ownedBoundaries = FindOwnedBoundaries(owner.Name, boundaryGroups, boundaryWordSets);
                if (ownedBoundaries == null) continue;

                result.MatchedBoundaryCount++;

                foreach (var other in parks)
                {
                    if (string.Equals(other.Reference, owner.Reference, StringComparison.OrdinalIgnoreCase)) continue;

                    if (IsPointInBoundaries(other.Longitude, other.Latitude, ownedBoundaries))
                    {
                        AddPair(owner.Reference, other.Reference);
                        AddPair(other.Reference, owner.Reference);
                    }
                }
            }

            foreach (var kvp in accum)
            {
                if (kvp.Value.Count == 0) continue;
                result.Fers[kvp.Key] = kvp.Value.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
            }

            return result;
        }

        // ---- Trail routes: POTA's separate "within 100 ft of the trail" rule ------------

        // How close a park's point must be to a trail's actual path to count as
        // "at" that trail - not a data-precision fudge factor like the area
        // ToleranceKm above, but a direct implementation of POTA's own stated
        // rule for trail-type parks ("Your station must be located entirely
        // within 100 feet (30.5m) of the trail" - activator guide). Verified
        // against real official route data: Lock 32 Canal State Park (US-2097)
        // sits ~248 ft from the Empire State Trail's actual GPS track at its
        // official coordinate - genuinely over this limit, not a rounding
        // artifact, so this constant is left at the real 100 ft rather than
        // loosened to make that specific case pass.
        private const double TrailToleranceKm = 0.03048;

        // Qualifier words safe to differ by when matching a trail's route name
        // to its POTA park name - kept separate from GenericDescriptorWords
        // (which was calibrated specifically for area parks) so this can't
        // loosen area matching as a side effect. "NST"/"NHT" (the two official
        // National Trails System designation abbreviations these route files
        // use, e.g. "North Country Trail NST") are expanded to their full words
        // before this list is applied - see ExpandTrailAbbreviations.
        private static readonly HashSet<string> TrailQualifierWords = new(StringComparer.Ordinal)
        {
            "national", "historic", "historical", "scenic", "trail", "of", "the", "and"
        };

        private static HashSet<string> ExpandTrailAbbreviations(HashSet<string> words)
        {
            var result = new HashSet<string>(words, StringComparer.Ordinal);
            if (result.Remove("nst")) { result.Add("national"); result.Add("scenic"); result.Add("trail"); }
            if (result.Remove("nht")) { result.Add("national"); result.Add("historic"); result.Add("trail"); }
            return result;
        }

        private static bool IsSafeTrailSubsetMatch(HashSet<string> a, HashSet<string> b)
        {
            var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
            if (smaller.Count == 0) return false;
            if (!smaller.IsSubsetOf(larger)) return false;

            foreach (var word in larger)
            {
                if (smaller.Contains(word)) continue;
                if (!TrailQualifierWords.Contains(word)) return false;
            }

            return true;
        }

        // Matches each trail route to its one POTA park by name (searching
        // ownerCandidates - typically the state's pre-exclusion candidate list,
        // which already includes multi-state trails even though geocoding later
        // excludes them for having an anchor point outside the state), then
        // tests every park in testParks (the state's real, already-geocoded
        // list) against that trail's actual path within TrailToleranceKm.
        // Symmetric pairing, same accumulation pattern as ComputeFers. A trail
        // with no unique name match, or that comes up empty, contributes
        // nothing - same ambiguity-averse behavior as the area matcher.
        //
        // A test park's own reported point is often not the right thing to test
        // - it's frequently just a parking-lot/trailhead pin, not the park's
        // real extent. Confirmed on a real case: Lock 32 Canal State Park's POTA
        // point sits ~248 ft from the Empire State Trail's actual GPS route
        // (over the 100 ft rule), but its real PAD-US boundary polygon
        // *directly intersects* that same route (0 ft) - the trail runs right
        // through the park. So whenever a test park also has its own matched
        // area boundary (via boundaries/FindOwnedBoundaries, the same lookup
        // ComputeFers uses), that boundary is checked against the trail too,
        // not just the single point - a hit on either counts.
        public static TrailFerResult ComputeTrailFers(
            List<ParkRecord> ownerCandidates, List<ParkRecord> testParks, List<TrailRoute> trails, List<BoundaryFeature> boundaries)
        {
            var result = new TrailFerResult();
            if (trails.Count == 0 || testParks.Count == 0) return result;

            var (boundaryGroups, boundaryWordSets) = boundaries.Count > 0
                ? BuildBoundaryGroups(boundaries)
                : (new Dictionary<string, List<BoundaryFeature>>(), new List<(string Key, HashSet<string> Words)>());

            var accum = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            void AddPair(string a, string b)
            {
                if (!accum.TryGetValue(a, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    accum[a] = set;
                }
                set.Add(b);
            }

            var relevant = new Dictionary<string, ParkRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (var trail in trails)
            {
                var trailWords = ExpandTrailAbbreviations(NormalizeWordSet(trail.Name));
                if (trailWords.Count == 0) continue;

                ParkRecord? owner = null;
                int candidateCount = 0;
                foreach (var candidate in ownerCandidates)
                {
                    var candidateWords = ExpandTrailAbbreviations(NormalizeWordSet(candidate.Name));
                    if (candidateWords.Count == 0) continue;
                    if (!candidateWords.SetEquals(trailWords) && !IsSafeTrailSubsetMatch(candidateWords, trailWords)) continue;

                    candidateCount++;
                    owner = candidate;
                    if (candidateCount > 1) break;
                }
                if (candidateCount != 1 || owner == null) continue;

                bool anyHit = false;
                foreach (var other in testParks)
                {
                    if (string.Equals(other.Reference, owner.Reference, StringComparison.OrdinalIgnoreCase)) continue;

                    bool near = IsPointNearTrail(other.Longitude, other.Latitude, trail);
                    if (!near)
                    {
                        var otherBoundaries = FindOwnedBoundaries(other.Name, boundaryGroups, boundaryWordSets);
                        if (otherBoundaries != null)
                            near = IsTrailNearBoundary(trail, otherBoundaries);
                    }

                    if (near)
                    {
                        AddPair(owner.Reference, other.Reference);
                        AddPair(other.Reference, owner.Reference);
                        anyHit = true;
                    }
                }

                if (anyHit) relevant[owner.Reference] = owner;
            }

            foreach (var kvp in accum)
            {
                if (kvp.Value.Count == 0) continue;
                result.Fers[kvp.Key] = kvp.Value.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
            }
            result.RelevantTrailParks.AddRange(relevant.Values);

            return result;
        }

        private static bool IsPointNearTrail(double lon, double lat, TrailRoute trail)
        {
            double latPad = TrailToleranceKm / KmPerDegreeLat;
            double lonScale = Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0));
            double lonPad = TrailToleranceKm / (KmPerDegreeLat * lonScale);

            if (lon < trail.MinLon - lonPad || lon > trail.MaxLon + lonPad ||
                lat < trail.MinLat - latPad || lat > trail.MaxLat + latPad)
                return false;

            foreach (var line in trail.Lines)
            {
                int n = line.Length / 2;
                for (int i = 0; i < n - 1; i++)
                {
                    double d = PointToSegmentDistanceKm(
                        lon, lat, line[2 * i], line[2 * i + 1], line[2 * (i + 1)], line[2 * (i + 1) + 1], lonScale);
                    if (d <= TrailToleranceKm) return true;
                }
            }

            return false;
        }

        // Whether a trail's route passes within TrailToleranceKm of a park's own
        // boundary polygon (not just a single point) - checked by walking every
        // vertex of the trail's real GPS track/route against that polygon
        // (containment first via the existing exact ray-cast test, then
        // distance-to-edge). Trail source data is dense enough for this
        // (GPX/NHT tracks run one point every ~20-40m, finer than the ~30m/100ft
        // tolerance being tested) that sampling at vertices, rather than doing
        // full segment-to-polygon geometry, doesn't miss a real crossing.
        private static bool IsTrailNearBoundary(TrailRoute trail, List<BoundaryFeature> ownedBoundaries)
        {
            double latPad = TrailToleranceKm / KmPerDegreeLat;

            foreach (var line in trail.Lines)
            {
                int n = line.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    double lon = line[2 * i], lat = line[2 * i + 1];
                    double lonScale = Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0));
                    double lonPad = TrailToleranceKm / (KmPerDegreeLat * lonScale);

                    foreach (var b in ownedBoundaries)
                    {
                        if (lon < b.MinLon - lonPad || lon > b.MaxLon + lonPad ||
                            lat < b.MinLat - latPad || lat > b.MaxLat + latPad) continue;

                        if (PointInParts(lon, lat, b.Polys)) return true;
                        if (DistanceKmToParts(lon, lat, b.Polys, lonScale) <= TrailToleranceKm) return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPointInBoundaries(double lon, double lat, List<BoundaryFeature> boundaries)
        {
            foreach (var b in boundaries)
            {
                if (lon < b.MinLon || lon > b.MaxLon || lat < b.MinLat || lat > b.MaxLat) continue;
                if (PointInParts(lon, lat, b.Polys)) return true;
            }

            // Precision fallback for a point that misses a real overlap only
            // because the boundary was simplified for file size - see ToleranceKm.
            double latPad = ToleranceKm / KmPerDegreeLat;
            double lonScale = Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0));
            double lonPad = ToleranceKm / (KmPerDegreeLat * lonScale);

            foreach (var b in boundaries)
            {
                if (lon < b.MinLon - lonPad || lon > b.MaxLon + lonPad ||
                    lat < b.MinLat - latPad || lat > b.MaxLat + latPad) continue;

                if (DistanceKmToParts(lon, lat, b.Polys, lonScale) <= ToleranceKm) return true;
            }

            return false;
        }

        // Standard point-in-polygon parity test applied across every ring of a
        // part (outer boundary plus any holes) - a point crosses an odd number of
        // ring boundaries only when it's genuinely inside the shape. Mirrors
        // CountyLookupService's PointInCounty/PointInRing.
        private static bool PointInParts(double lon, double lat, List<List<double[]>> parts)
        {
            foreach (var part in parts)
            {
                int ringsContainingPoint = 0;
                foreach (var ring in part)
                {
                    if (PointInRing(lon, lat, ring)) ringsContainingPoint++;
                }
                if (ringsContainingPoint % 2 == 1) return true;
            }
            return false;
        }

        private static bool PointInRing(double lon, double lat, double[] ring)
        {
            int n = ring.Length / 2;
            bool inside = false;
            int j = n - 1;
            for (int i = 0; i < n; i++)
            {
                double xi = ring[2 * i], yi = ring[2 * i + 1];
                double xj = ring[2 * j], yj = ring[2 * j + 1];

                if (((yi > lat) != (yj > lat)) &&
                    (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        private static double DistanceKmToParts(double lon, double lat, List<List<double[]>> parts, double lonScale)
        {
            double best = double.MaxValue;
            foreach (var ring in parts.SelectMany(p => p))
            {
                int n = ring.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    double d = PointToSegmentDistanceKm(
                        lon, lat, ring[2 * i], ring[2 * i + 1], ring[2 * next], ring[2 * next + 1], lonScale);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        // Distance from a point to a segment, computed in a locally-scaled degree
        // space (longitude scaled by cos(latitude) so 1 unit is ~equally far in
        // both directions) and converted to kilometers - accurate enough at the
        // sub-kilometer scale ToleranceKm operates at. Mirrors
        // CountyLookupService's PointToSegmentDistanceSquared.
        private static double PointToSegmentDistanceKm(
            double px, double py, double ax, double ay, double bx, double by, double lonScale)
        {
            double axs = ax * lonScale, bxs = bx * lonScale, pxs = px * lonScale;
            double dx = bxs - axs, dy = by - ay;

            double t;
            if (dx == 0 && dy == 0)
                t = 0;
            else
                t = Math.Max(0, Math.Min(1, ((pxs - axs) * dx + (py - ay) * dy) / (dx * dx + dy * dy)));

            double cx = axs + t * dx, cy = ay + t * dy;
            double ex = pxs - cx, ey = py - cy;
            return Math.Sqrt(ex * ex + ey * ey) * KmPerDegreeLat;
        }
    }
}
