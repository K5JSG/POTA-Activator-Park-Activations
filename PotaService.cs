using ClosedXML.Excel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace POTA_Check
{
    public class RawPark
    {
        public string Reference { get; set; } = "";
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Grid { get; set; } = "";
        public List<string> States { get; set; } = new List<string>();
        public bool Active { get; set; }
    }

    public static class PotaService
    {
        public static readonly List<UsState> UsStates = new List<UsState>
        {
            new("AL","Alabama"), new("AK","Alaska"), new("AZ","Arizona"), new("AR","Arkansas"),
            new("CA","California"), new("CO","Colorado"), new("CT","Connecticut"), new("DE","Delaware"),
            new("DC","District of Columbia"), new("FL","Florida"), new("GA","Georgia"), new("HI","Hawaii"),
            new("ID","Idaho"), new("IL","Illinois"), new("IN","Indiana"), new("IA","Iowa"),
            new("KS","Kansas"), new("KY","Kentucky"), new("LA","Louisiana"), new("ME","Maine"),
            new("MD","Maryland"), new("MA","Massachusetts"), new("MI","Michigan"), new("MN","Minnesota"),
            new("MS","Mississippi"), new("MO","Missouri"), new("MT","Montana"), new("NE","Nebraska"),
            new("NV","Nevada"), new("NH","New Hampshire"), new("NJ","New Jersey"), new("NM","New Mexico"),
            new("NY","New York"), new("NC","North Carolina"), new("ND","North Dakota"), new("OH","Ohio"),
            new("OK","Oklahoma"), new("OR","Oregon"), new("PA","Pennsylvania"), new("RI","Rhode Island"),
            new("SC","South Carolina"), new("SD","South Dakota"), new("TN","Tennessee"), new("TX","Texas"),
            new("UT","Utah"), new("VT","Vermont"), new("VA","Virginia"), new("WA","Washington"),
            new("WV","West Virginia"), new("WI","Wisconsin"), new("WY","Wyoming")
        };

        private const string AllParksUrl = "https://pota.app/all_parks_ext.csv";

        public static async Task<List<RawPark>> DownloadAllParksAsync(HttpClient http)
        {
            string csv = await http.GetStringAsync(AllParksUrl);
            var lines = csv.Split('\n');
            var results = new List<RawPark>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r', '\n');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields.Count < 8) continue;

                string reference = fields[0];
                string name = fields[1];
                string active = fields[2];
                string locationDesc = fields[4];
                string latStr = fields[5];
                string lonStr = fields[6];
                string grid = fields[7];

                if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;

                results.Add(new RawPark
                {
                    Reference = reference,
                    Name = name,
                    Latitude = lat,
                    Longitude = lon,
                    Grid = grid,
                    States = locationDesc.Split(',').Select(s => s.Trim()).ToList(),
                    Active = active == "1"
                });
            }

            return results;
        }

        public static List<ParkRecord> FilterByState(List<RawPark> allParks, string stateCode)
        {
            string target = "US-" + stateCode.ToUpperInvariant();
            var results = new List<ParkRecord>();

            foreach (var p in allParks)
            {
                if (!p.Active) continue;
                bool matches = p.States.Any(s => string.Equals(s, target, StringComparison.OrdinalIgnoreCase));
                if (!matches) continue;

                results.Add(new ParkRecord
                {
                    Reference = p.Reference,
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Grid = p.Grid,
                    MultiState = p.States.Count > 1
                });
            }

            return results;
        }

        public static async Task GeocodeParksAsync(HttpClient http, List<ParkRecord> parks, string selectedStateCode, IProgress<int> progress)
        {
            int total = parks.Count;
            if (total == 0) return;
            int completed = 0;
            using var semaphore = new SemaphoreSlim(8);

            var tasks = parks.Select(async park =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (county, stateAbbrev) = await GetCountyAndStateAsync(http, park.Latitude, park.Longitude);
                    park.County = county;
                    park.State = stateAbbrev;

                    if (park.MultiState && !string.IsNullOrEmpty(stateAbbrev) &&
                        !string.Equals(stateAbbrev, selectedStateCode, StringComparison.OrdinalIgnoreCase))
                    {
                        park.Exclude = true;
                    }
                }
                catch
                {
                    park.County = "";
                }
                finally
                {
                    semaphore.Release();
                    int done = Interlocked.Increment(ref completed);
                    progress.Report((int)(done * 100.0 / total));
                }
            });

            await Task.WhenAll(tasks);
        }

        public static async Task GeocodeExtraParksAsync(HttpClient http, List<ParkRecord> parks, IProgress<int> progress)
        {
            int total = parks.Count;
            if (total == 0) return;
            int completed = 0;
            using var semaphore = new SemaphoreSlim(8);

            var tasks = parks.Select(async park =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (county, stateAbbrev) = await GetCountyAndStateAsync(http, park.Latitude, park.Longitude);
                    park.County = county;
                    park.State = stateAbbrev;
                }
                catch
                {
                    park.County = "";
                }
                finally
                {
                    semaphore.Release();
                    int done = Interlocked.Increment(ref completed);
                    progress.Report((int)(done * 100.0 / total));
                }
            });

            await Task.WhenAll(tasks);
        }

        private static async Task<(string County, string StateAbbrev)> GetCountyAndStateAsync(HttpClient http, double lat, double lon)
        {
            string url = "https://geocoding.geo.census.gov/geocoder/geographies/coordinates" +
                         $"?x={lon.ToString(CultureInfo.InvariantCulture)}" +
                         $"&y={lat.ToString(CultureInfo.InvariantCulture)}" +
                         "&benchmark=Public_AR_Current&vintage=Current_Current&layers=Counties,States&format=json";

            string json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            string county = "";
            string state = "";

            var geographies = doc.RootElement.GetProperty("result").GetProperty("geographies");

            if (geographies.TryGetProperty("Counties", out var counties) && counties.GetArrayLength() > 0)
            {
                string rawCounty = counties[0].GetProperty("NAME").GetString() ?? "";
                county = Regex.Replace(rawCounty, @"\s+County$", "", RegexOptions.IgnoreCase).Trim();
            }

            if (geographies.TryGetProperty("States", out var states) && states.GetArrayLength() > 0)
                state = states[0].GetProperty("STUSAB").GetString() ?? "";

            return (county, state);
        }

        // ---- Community activation history (used by the map) ----------------------------

        private const string ActivationsUrlBase = "https://api.pota.app/park/activations/";

        // Holds what we learned about a park's activation history from the POTA API.
        public class ActivationInfo
        {
            public int Count { get; set; }
            public string LastCallsign { get; set; } = "";
            public DateTime? LastDate { get; set; }
        }

        // Looks up ONE park's activation history from the POTA API.
        // If the lookup fails for any reason (no internet, park not found, POTA API
        // hiccup, etc.) this quietly returns an "empty" result instead of throwing,
        // so one bad park doesn't stop the whole map from being built.
        public static async Task<ActivationInfo> GetActivationInfoAsync(HttpClient http, string reference)
        {
            var info = new ActivationInfo();
            try
            {
                // We ask for up to 250 of the most recent activations. That is far more
                // than almost every park will ever have, so Count ends up being the
                // real total for the vast majority of parks. A handful of extremely
                // popular parks could have more than 250 all-time activations, in which
                // case this number would be a "250+" style undercount.
                string url = ActivationsUrlBase + Uri.EscapeDataString(reference) + "?count=250";
                string json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    info.Count = doc.RootElement.GetArrayLength();
                    if (info.Count > 0)
                    {
                        var first = doc.RootElement[0];
                        info.LastCallsign = GetStringProperty(first, "activeCallsign", "callsign", "activatorCallsign") ?? "";

                        string? dateStr = GetStringProperty(first, "date", "qsoDate", "activationDate");
                        if (dateStr != null && DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                            info.LastDate = parsed;
                    }
                }
            }
            catch
            {
                // Leave info as an "empty" result (Count = 0) if anything goes wrong.
            }
            return info;
        }

        private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
            return null;
        }

        // Looks up activation history for a whole list of parks at once, in parallel
        // (a handful of requests at a time, so we don't hammer POTA's free API).
        public static async Task<Dictionary<string, ActivationInfo>> FetchActivationInfoAsync(
            HttpClient http, List<ParkRecord> parks, IProgress<int> progress)
        {
            var results = new System.Collections.Concurrent.ConcurrentDictionary<string, ActivationInfo>(StringComparer.OrdinalIgnoreCase);
            int total = parks.Count;
            if (total == 0) return new Dictionary<string, ActivationInfo>(StringComparer.OrdinalIgnoreCase);

            int completed = 0;
            using var semaphore = new SemaphoreSlim(6);

            var tasks = parks.Select(async park =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var info = await GetActivationInfoAsync(http, park.Reference);
                    results[park.Reference] = info;
                }
                finally
                {
                    semaphore.Release();
                    int done = Interlocked.Increment(ref completed);
                    progress.Report((int)(done * 100.0 / total));
                }
            });

            await Task.WhenAll(tasks);
            return new Dictionary<string, ActivationInfo>(results, StringComparer.OrdinalIgnoreCase);
        }

        // ---- "Activated by me" history, straight from your ADIF file --------------------

        // Scans the ADIF log for every QSO record where YOU were the activator
        // (that's what the MY_SIG_INFO / MY_POTA_REF tag means - it only shows up
        // in your log when you were the one operating from that park). For each
        // park reference found this way, it collects every QSO_DATE it sees, so we
        // can later count "how many different days did I activate this park" and
        // find the most recent one.
        public static Dictionary<string, List<DateTime>> ParseMyActivationDates(string adifText)
        {
            var result = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

            // ADIF records are separated by an <EOR> tag - split the log into one
            // chunk of text per QSO so we don't mix up fields from different QSOs.
            var records = Regex.Split(adifText, "<eor>", RegexOptions.IgnoreCase);
            var tagRegex = new Regex(@"<(?<tag>[a-zA-Z_]+)(:(?<len>\d+))?(:[a-zA-Z]+)?>", RegexOptions.IgnoreCase);

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record)) continue;

                string? myParkRefs = null;
                string? qsoDate = null;

                foreach (Match m in tagRegex.Matches(record))
                {
                    string tag = m.Groups["tag"].Value;
                    if (!m.Groups["len"].Success) continue;

                    int len = int.Parse(m.Groups["len"].Value);
                    int start = m.Index + m.Length;
                    if (start + len > record.Length) continue;

                    string value = record.Substring(start, len).Trim();
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    if (tag.Equals("MY_SIG_INFO", StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("MY_POTA_REF", StringComparison.OrdinalIgnoreCase))
                    {
                        myParkRefs = value;
                    }
                    else if (tag.Equals("QSO_DATE", StringComparison.OrdinalIgnoreCase))
                    {
                        qsoDate = value;
                    }
                }

                // This QSO wasn't one where you were the activator - skip it.
                if (myParkRefs == null) continue;

                DateTime? parsedDate = null;
                if (qsoDate != null &&
                    DateTime.TryParseExact(qsoDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    parsedDate = d;
                }

                // A single QSO can list more than one park reference (a "two-fer" -
                // activating two parks at the same spot), separated by commas.
                foreach (string part in myParkRefs.Split(','))
                {
                    string cleaned = part.Trim();
                    if (string.IsNullOrWhiteSpace(cleaned)) continue;

                    if (!result.TryGetValue(cleaned, out var list))
                    {
                        list = new List<DateTime>();
                        result[cleaned] = list;
                    }
                    if (parsedDate.HasValue)
                        list.Add(parsedDate.Value);
                }
            }

            return result;
        }

        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                        inQuotes = true;
                    else if (c == ',')
                    {
                        fields.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                        sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }

        public static HashSet<string> ParseAdifCompletedRefs(string adifText)
        {
            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tagRegex = new Regex(@"<(?<tag>[a-zA-Z_]+)(:(?<len>\d+))?(:[a-zA-Z]+)?>", RegexOptions.IgnoreCase);
            var matches = tagRegex.Matches(adifText);

            var wantedTags = new[] { "MY_SIG_INFO", "MY_POTA_REF" };

            foreach (Match m in matches)
            {
                string tag = m.Groups["tag"].Value;
                if (!wantedTags.Any(t => tag.Equals(t, StringComparison.OrdinalIgnoreCase))) continue;
                if (!m.Groups["len"].Success) continue;

                int len = int.Parse(m.Groups["len"].Value);
                int start = m.Index + m.Length;
                if (start + len > adifText.Length) continue;

                string rawValue = adifText.Substring(start, len).Trim();
                if (string.IsNullOrWhiteSpace(rawValue)) continue;

                foreach (string part in rawValue.Split(','))
                {
                    string cleaned = part.Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        refs.Add(cleaned);
                }
            }

            return refs;
        }

        public static void ExportCsv(string path, List<ParkRecord> parks)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            writer.WriteLine("Reference,Name,Latitude,Longitude,Grid,County,State,Completed");

            foreach (var p in parks)
            {
                writer.WriteLine(string.Join(",",
                    Csv(p.Reference),
                    Csv(p.Name),
                    p.Latitude.ToString(CultureInfo.InvariantCulture),
                    p.Longitude.ToString(CultureInfo.InvariantCulture),
                    Csv(p.Grid),
                    Csv(p.County),
                    Csv(p.State),
                    p.Completed ? "Yes" : "No"));
            }
        }

        public static void ExportExcel(string path, List<ParkRecord> parks)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Parks");

            string[] headers = { "Reference", "Name", "Latitude", "Longitude", "Grid", "County", "State", "Completed" };
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = headers[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }

            int row = 2;
            foreach (var p in parks)
            {
                ws.Cell(row, 1).Value = p.Reference;
                ws.Cell(row, 2).Value = p.Name;
                ws.Cell(row, 3).Value = p.Latitude;
                ws.Cell(row, 4).Value = p.Longitude;
                ws.Cell(row, 5).Value = p.Grid;
                ws.Cell(row, 6).Value = p.County;
                ws.Cell(row, 7).Value = p.State;
                ws.Cell(row, 8).Value = p.Completed ? "Yes" : "No";

                if (p.Completed)
                {
                    var rowRange = ws.Range(row, 1, row, 8);
                    rowRange.Style.Fill.BackgroundColor = p.OutOfState ? XLColor.Orange : XLColor.IndianRed;
                    rowRange.Style.Font.FontColor = XLColor.Black;
                    rowRange.Style.Font.Strikethrough = true;
                }

                row++;
            }

            ws.RangeUsed()!.SetAutoFilter();
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
            workbook.SaveAs(path);
        }

        private static string Csv(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }

    public record UsState(string Code, string Name);
}