using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

// Generates ParkElevations.csv - a lookup table of elevation (in feet) for every
// active US POTA park, built by querying the free Open-Elevation API.
//
// This only needs to be run once to get started, and re-run occasionally (say,
// every few months) to pick up newly-added parks. It can take a while (POTA has
// tens of thousands of US parks) - just let it run. Progress is printed as it
// goes, and results are written to disk incrementally, so if it gets
// interrupted partway through you won't lose everything that's already been
// done - just re-run it and it will pick up where it left off.

const string ParksUrl = "https://pota.app/all_parks_ext.csv";
const string ElevationApiUrl = "https://api.open-elevation.com/api/v1/lookup";
const int BatchSize = 100;
const int MinBatchSize = 5;
const int DelayBetweenBatchesMs = 1000;
const int MaxRetries = 3;

string outputFile = Path.Combine(GetRepoRoot(), "ParkElevations.csv");

using var http = new HttpClient();

// ---- Step 1: figure out what's already done, so re-runs can resume ----
var alreadyDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (File.Exists(outputFile))
{
    Console.WriteLine($"Found existing {outputFile} - will skip parks already in it.");
    foreach (var line in File.ReadAllLines(outputFile).Skip(1))
    {
        int comma = line.IndexOf(',');
        if (comma > 0) alreadyDone.Add(line[..comma].Trim());
    }
    Console.WriteLine($"Already have {alreadyDone.Count} parks.");
}
else
{
    await File.WriteAllTextAsync(outputFile, "Reference,ElevationFeet" + Environment.NewLine, Encoding.UTF8);
}

// ---- Step 2: download and parse the POTA master park list ----
Console.WriteLine("Downloading POTA park list...");
string csvText = await http.GetStringAsync(ParksUrl);
var records = SplitCsvRecords(csvText);

var parks = new List<(string Reference, double Lat, double Lon)>();
for (int i = 1; i < records.Count; i++)
{
    string line = records[i].Trim('\r', '\n');
    if (string.IsNullOrWhiteSpace(line)) continue;

    var fields = ParseCsvLine(line);
    if (fields.Count < 8) continue;

    string reference = fields[0];
    string active = fields[2];
    string latStr = fields[5];
    string lonStr = fields[6];

    if (active != "1") continue;
    if (!reference.StartsWith("US-", StringComparison.Ordinal)) continue;
    if (alreadyDone.Contains(reference)) continue;

    if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
    if (!double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;

    parks.Add((reference, lat, lon));
}

Console.WriteLine($"Found {parks.Count} active US parks still needing elevation.");

if (parks.Count == 0)
{
    Console.WriteLine("Nothing to do - ParkElevations.csv is already up to date.");
    return 0;
}

// ---- Step 3: query Open-Elevation in batches, appending results as we go ----
// Open-Elevation is a free, shared public server - a batch that's too large can
// time out on their end (504 Gateway Timeout) even when nothing is wrong on
// this end. Rather than just giving up after a few retries, a batch that fails
// gets split in half and each half is tried separately, continuing to shrink
// down to MinBatchSize before finally giving up on that chunk. This means
// occasional server overload gets worked around automatically instead of
// requiring a full repeated re-run.

async Task<bool> SendBatchAsync(List<(string Reference, double Lat, double Lon)> batch)
{
    var payload = new { locations = batch.Select(p => new { latitude = p.Lat, longitude = p.Lon }) };
    string body = JsonSerializer.Serialize(payload);

    for (int attempt = 1; attempt <= MaxRetries; attempt++)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(ElevationApiUrl, content, cts.Token);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            var rows = new List<string>();
            for (int j = 0; j < batch.Count; j++)
            {
                double meters = results[j].GetProperty("elevation").GetDouble();
                long feet = (long)Math.Round(meters * 3.28084);
                rows.Add($"{batch[j].Reference},{feet}");
            }

            await File.AppendAllTextAsync(outputFile, string.Join("\r\n", rows) + "\r\n", Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Attempt {attempt} (batch size {batch.Count}) failed: {ex.Message}");
            if (attempt < MaxRetries) await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
        }
    }
    return false;
}

async Task<int> ProcessChunkAsync(List<(string Reference, double Lat, double Lon)> chunk)
{
    if (await SendBatchAsync(chunk))
        return chunk.Count;

    if (chunk.Count <= MinBatchSize)
    {
        Console.WriteLine($"  Giving up on {chunk.Count} park(s) after repeated failures at minimum batch size - re-run later to retry them: {string.Join(", ", chunk.Select(p => p.Reference))}");
        return 0;
    }

    Console.WriteLine($"  Splitting batch of {chunk.Count} in half and retrying each half...");
    int half = (int)Math.Ceiling(chunk.Count / 2.0);
    var firstHalf = chunk.Take(half).ToList();
    var secondHalf = chunk.Skip(half).ToList();

    int completed = await ProcessChunkAsync(firstHalf);
    await Task.Delay(DelayBetweenBatchesMs);
    completed += await ProcessChunkAsync(secondHalf);
    return completed;
}

int total = parks.Count;
int done = 0;
int succeeded = 0;
int batchNum = 0;
int totalBatches = (int)Math.Ceiling(total / (double)BatchSize);

for (int start = 0; start < total; start += BatchSize)
{
    batchNum++;
    int count = Math.Min(BatchSize, total - start);
    var batch = parks.GetRange(start, count);

    succeeded += await ProcessChunkAsync(batch);

    done += batch.Count;
    int pct = (int)Math.Round(done * 100.0 / total);
    Console.WriteLine($"Batch {batchNum} / {totalBatches} - {done} / {total} parks processed ({pct}%), {succeeded} succeeded so far");

    await Task.Delay(DelayBetweenBatchesMs);
}

Console.WriteLine();
Console.WriteLine($"Done. Results are in {outputFile}.");
Console.WriteLine("Copy this file into your project (same place as KffCrossReference.csv) and it'll pick it up automatically.");

return 0;

// ---- Helpers ----------------------------------------------------------------

// Resolves the repo root from this source file's own compiled-in path, so the
// output file always lands in the right place regardless of the working
// directory this tool happens to be launched from.
static string GetRepoRoot([CallerFilePath] string sourceFilePath = "") =>
    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));

// Splits raw CSV text into logical records (one per row) the same way
// csv.Split('\n') does for the common case, except a '\n' that falls inside a
// quoted field (a field value that itself contains a literal newline) is
// treated as part of that field's value instead of ending the record early -
// matching the same fix in the main app's own CSV parsing for this same file.
static List<string> SplitCsvRecords(string csv)
{
    var records = new List<string>();
    var rawLines = csv.Split('\n');
    string? pending = null;

    foreach (var rawLine in rawLines)
    {
        pending = pending == null ? rawLine : pending + "\n" + rawLine;

        int quoteCount = pending.Length - pending.Replace("\"", "").Length;
        if (quoteCount % 2 == 0)
        {
            records.Add(pending);
            pending = null;
        }
    }

    if (pending != null) records.Add(pending);
    return records;
}

// Simple CSV line parser that handles quoted fields with embedded commas,
// matching the same parsing the main app itself uses on this same file.
static List<string> ParseCsvLine(string line)
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
                if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        else
        {
            if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
    }
    fields.Add(sb.ToString());
    return fields;
}
