using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PotaActivatorParkActivations
{
    public partial class Form1 : Form
    {
        private readonly HttpClient _http = new HttpClient();
        private List<ParkRecord> _parks = new List<ParkRecord>();
        private List<RawPark> _allRawParks = new List<RawPark>();

        // The current state's own boundary polygons and national trail
        // routes - set alongside _parks in buttonLoadParks_Click (originally
        // just local variables there, used only for Xfer detection). Kept
        // here too so buttonShowMap_Click can offer them as toggleable map
        // layers without re-downloading anything.
        private List<FerLookupService.BoundaryFeature> _boundaries = new List<FerLookupService.BoundaryFeature>();
        private List<FerLookupService.TrailRoute> _trails = new List<FerLookupService.TrailRoute>();

        // The HTML from the most recent buttonShowMap_Click, kept so
        // buttonSaveMap_Click can write out a copy without re-fetching
        // activation info. Cleared (and buttonSaveMap disabled - see
        // UpdateButtonStates) whenever _adifLoaded resets to false, since
        // that's exactly when the underlying data a map would show goes
        // stale.
        private string? _lastMapHtml;

        // Files this program writes under %TEMP% during a session (the map
        // HTML buttonShowMap_Click opens in the browser, and the WWFF .xls
        // download in TryAutoDownloadWwffFileAsync) - deleted on close, since
        // nothing here needs to outlive the run that created it. Distinct
        // from GetWritableAppDataFolder()'s caches, which are deliberately
        // kept between runs.
        private readonly List<string> _tempFilesToCleanUp = new List<string>();
        private Font? _strikeFont;
        // Shared by the Xfer's and KFF Ref columns' hover tooltips - only one
        // can be showing at a time anyway, so one ToolTip/key pair covers
        // both. The key distinguishes what's currently shown (which specific
        // Xfer's reference, or which KFF cell by row) so it's only
        // re-shown when the mouse actually moves to something different.
        private readonly ToolTip _gridToolTip = new ToolTip();
        private string? _gridTooltipKey;

        // Reference -> list of dates you activated that park, built from the ADIF file.
        private Dictionary<string, List<DateTime>> _myActivations =
            new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        // Reference -> KFF reference, loaded from a user-maintained CSV file next to
        // the .exe. See LoadKffLookup() below.
        private const string KffFileName = "KffCrossReference.csv";

        // Windows won't let a normal (non-admin) user write into Program Files,
        // which is where this app is usually installed - so all locally-cached
        // data (the KFF cross reference, its date-tracking file, and the cached
        // POTA park list) lives in %LocalAppData% instead, which is always
        // writable by whoever's running the app. This folder is created
        // automatically the first time it's needed.
        private static string GetWritableAppDataFolder()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "POTA Activator Park Activations");
            Directory.CreateDirectory(folder);
            return folder;
        }

        // A dedicated, permanent subfolder under %TEMP% (rather than writing
        // loose files straight into %TEMP% itself) so the whole thing can be
        // pointed at once as a trusted location in antivirus software (e.g.
        // Norton 360), instead of the user having to trust all of %TEMP%.
        // The folder itself is left in place between runs - only the files
        // this session wrote to it are deleted on close, via
        // CleanUpTempFiles() below.
        private static string GetAppTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "POTA Activator Park Activations");
            Directory.CreateDirectory(folder);
            return folder;
        }

        // Where the KFF CSV is actually read from: prefers the writable
        // %LocalAppData% copy (which is what auto-update writes to), but falls
        // back to a copy sitting next to the .exe, in case one was placed there
        // manually before this app updated to writing into %LocalAppData%.
        private static string GetKffCsvReadPath()
        {
            string localAppDataPath = Path.Combine(GetWritableAppDataFolder(), KffFileName);
            if (File.Exists(localAppDataPath)) return localAppDataPath;

            string installFolderPath = Path.Combine(Application.StartupPath, KffFileName);
            return File.Exists(installFolderPath) ? installFolderPath : localAppDataPath;
        }
        private Dictionary<string, string> _kffLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // KFF reference -> its own name from the WWFF cross-reference
        // spreadsheet (e.g. "Cumberland Gap (KY)") - separate from
        // KffCrossReference.csv itself since a park's KFF field there is a
        // reference number, not a name, and a multi-state park's several KFF
        // numbers don't all share one name. Same writable-folder-first,
        // install-folder-fallback read path as the KFF file above.
        private const string KffNamesFileName = "KffNames.csv";

        private static string GetKffNamesCsvReadPath()
        {
            string localAppDataPath = Path.Combine(GetWritableAppDataFolder(), KffNamesFileName);
            if (File.Exists(localAppDataPath)) return localAppDataPath;

            string installFolderPath = Path.Combine(Application.StartupPath, KffNamesFileName);
            return File.Exists(installFolderPath) ? installFolderPath : localAppDataPath;
        }
        private Dictionary<string, string> _kffNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ParkElevations.csv - generated by the GenerateParkElevations tool - follows
        // the same "prefer %LocalAppData%, fall back to install folder" pattern
        // as the KFF file above.
        private const string ElevationFileName = "ParkElevations.csv";

        private static string GetElevationCsvReadPath()
        {
            string localAppDataPath = Path.Combine(GetWritableAppDataFolder(), ElevationFileName);
            if (File.Exists(localAppDataPath)) return localAppDataPath;

            string installFolderPath = Path.Combine(Application.StartupPath, ElevationFileName);
            return File.Exists(installFolderPath) ? installFolderPath : localAppDataPath;
        }

        private void LoadElevationLookup()
        {
            try
            {
                ElevationLookupService.Load(GetElevationCsvReadPath());
            }
            catch
            {
                // ElevationLookupService.Load already fails safe internally -
                // this catch is just extra insurance.
            }
        }

        // How often to check for updates to the WWFF/KFF data and the POTA park
        // list. Both barely change day to day, so there's no reason to hit the
        // network every single time - once a week keeps things current without
        // being chatty.
        private static readonly TimeSpan DataRefreshInterval = TimeSpan.FromDays(7);

        // Park boundary data (used for Xfer's detection) changes far less
        // often than the KFF/park-list data above, and the download is much
        // larger, so it's refreshed on a longer cycle.
        private static readonly TimeSpan BoundaryRefreshInterval = TimeSpan.FromDays(30);

        // National trail route data (Appalachian Trail, Empire State Trail,
        // etc.) is shared across every state and changes even less often than
        // the per-state boundary data above.
        private static readonly TimeSpan TrailRefreshInterval = TimeSpan.FromDays(30);

        // Kelly Green used to highlight the button that's the natural "next step."
        private static readonly Color NextStepColor = ColorTranslator.FromHtml("#4CBB17");
        private bool _parksLoaded;
        private bool _adifLoaded;

        public Form1()
        {
            InitializeComponent();
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) POTAActivatorParkActivations/{Application.ProductVersion}");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxState.DataSource = PotaService.UsStates;
            comboBoxState.DisplayMember = "Name";
            comboBoxState.ValueMember = "Code";

            dataGridView1.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "colRef",
                HeaderText = "Reference",
                DataPropertyName = "Reference",
                Width = 90,
                SortMode = DataGridViewColumnSortMode.Automatic,
                UseColumnTextForLinkValue = false // each cell shows its own Reference value as the link text
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Park Name", DataPropertyName = "Name", Width = 220, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLat", HeaderText = "Latitude", DataPropertyName = "Latitude", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLon", HeaderText = "Longitude", DataPropertyName = "Longitude", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colGrid", HeaderText = "Grid Square", DataPropertyName = "Grid", Width = 90, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colElevation",
                HeaderText = "Elevation (ft)",
                DataPropertyName = "ElevationFeet",
                Width = 100,
                SortMode = DataGridViewColumnSortMode.Automatic,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCounty", HeaderText = "County", DataPropertyName = "County", Width = 140, SortMode = DataGridViewColumnSortMode.Automatic });
            // Sized to fit two full 5-digit references ("US-12345, US-67890")
            // plus enough room for the start of a third ("US") to peek through
            // as a hint there's more - measured at 124px in this app's font
            // (Segoe UI 9pt; digits are fixed-width in it, so that's already
            // the worst case) plus normal cell padding.
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFers", HeaderText = "Xfer's", DataPropertyName = "Fers", Width = 140, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colKff", HeaderText = "KFF Ref", DataPropertyName = "Kff", Width = 90, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colState", HeaderText = "State", DataPropertyName = "State", Width = 60, SortMode = DataGridViewColumnSortMode.Automatic, Visible = false });
            dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colCompleted", HeaderText = "Completed", DataPropertyName = "Completed", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });

            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.CellContentClick += DataGridView1_CellContentClick;
            dataGridView1.CellMouseClick += DataGridView1_CellMouseClick;
            dataGridView1.CellMouseMove += DataGridView1_CellMouseMove;
            dataGridView1.CellMouseLeave += DataGridView1_CellMouseLeave;
            // DataGridView's own built-in per-cell tooltip mechanism (unused
            // otherwise - nothing here sets a cell's ToolTipText) would compete
            // with the manual _fersToolTip shown above for the same control.
            dataGridView1.ShowCellToolTips = false;
            _strikeFont = new Font(dataGridView1.Font, FontStyle.Strikeout);

            ApplyDataGridViewTheme();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            FormClosed += (s, e) => SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            FormClosed += (s, e) => CleanUpTempFiles();

            comboBoxState.SelectedIndexChanged += ComboBoxState_SelectedIndexChanged;
            UpdateButtonStates();
            UpdateWwffDateLabel(WwffUpdateService.LoadInfoFile(GetWritableAppDataFolder()));
        }

        // DataGridView's column/row headers don't automatically follow the
        // light/dark mode the rest of the app does - EnableHeadersVisualStyles
        // (on by default) hands header painting to the OS's classic themed
        // header renderer, which doesn't know about the app's dark mode setting.
        // Pointing the headers at SystemColors instead - the same colors every
        // other themed control here already uses - fixes that, with one code
        // path that's correct for both light and dark.
        //
        // The Reference column's link colors have the same problem for a
        // different reason: DataGridViewLinkColumn defaults to a fixed blue/
        // purple/red (LinkColor/VisitedLinkColor/ActiveLinkColor) meant for a
        // white background, not a SystemColors value, so it doesn't adapt on
        // its own either. Worse, a link cell always paints in its LinkColor
        // even while selected - it never switches to SelectionForeColor - so
        // a fixed color that happens to be close to SystemColors.Highlight
        // (the selected-row background in dark mode) goes low-contrast the
        // moment you click the row. CellFormatting below sets the per-row
        // link color explicitly based on selection state instead, so this
        // just needs a readable, theme-neutral default for the moment before
        // the grid is populated. CellFormatting also still overrides these
        // for completed rows, where the link needs to match that row's own
        // text color instead.
        private void ApplyDataGridViewTheme()
        {
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dataGridView1.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dataGridView1.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dataGridView1.BackgroundColor = SystemColors.Window;
            dataGridView1.GridColor = SystemColors.ControlDark;

            if (dataGridView1.Columns["colRef"] is DataGridViewLinkColumn refColumn)
            {
                refColumn.LinkColor = Color.White;
                refColumn.VisitedLinkColor = Color.White;
                refColumn.ActiveLinkColor = Color.White;
            }

            dataGridView1.Invalidate();
        }

        // SystemColors values update live the moment Windows' theme changes,
        // but a color already assigned to a control doesn't repaint on its own -
        // this re-applies the grid's theme colors so switching Windows between
        // light and dark while the app is already running takes effect
        // immediately instead of only on the next launch.
        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
                ApplyDataGridViewTheme();
        }

        // Reads KffCrossReference.csv fresh every time it's called, so if you edit
        // that file (or the app auto-updates it), your next "Load Parks for State"
        // click picks up the changes - no rebuild needed.
        private void LoadKffLookup()
        {
            try
            {
                string path = GetKffCsvReadPath();
                _kffLookup = PotaService.LoadKffCrossReference(path);
            }
            catch
            {
                _kffLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // Same idea as LoadKffLookup, for the separate KFF-reference -> name
        // file (see KffNamesFileName above).
        private void LoadKffNamesLookup()
        {
            try
            {
                string path = GetKffNamesCsvReadPath();
                _kffNames = PotaService.LoadKffNames(path);
            }
            catch
            {
                _kffNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void UpdateWwffDateLabel(DateTime? date)
        {
            labelWwffDate.Text = date.HasValue
                ? $"WWFF data as of: {date.Value:MMM d, yyyy}"
                : "WWFF data: not loaded";
        }

        // Tries to find and download the current KFF-POTA cross reference workbook
        // on its own. This is a best-effort attempt only: it works by scanning the
        // page for a link to an .xls/.xlsx file that looks like the cross reference
        // (not a hardcoded, one-time URL, since WWFF renames the file - and can
        // restructure the page - with every update). A short timeout keeps a
        // slow/unreachable site from stalling the "Load Parks for State" flow this
        // now runs inside of. Returns a diagnostic reason alongside the result so a
        // failure can be shown instead of silently ignored.
        private async Task<(string? Path, string Reason)> TryAutoDownloadWwffFileAsync()
        {
            try
            {
                // This page (not wwff.us) hosts the actual KFF-POTA cross reference
                // file as a plain, ordinary link - wwff.us links to the same data
                // through a download-button widget that isn't a normal <a href>,
                // so it can't be found this way.
                string html;
                try
                {
                    using var pageCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    html = await _http.GetStringAsync("https://wwffkff.wordpress.com/kff-references/", pageCts.Token);
                }
                catch (Exception ex)
                {
                    return (null, $"Couldn't reach wwffkff.wordpress.com: {ex.Message}");
                }

                var linkRegex = new Regex("href=[\"'](?<url>[^\"']+\\.xlsx?)[\"']", RegexOptions.IgnoreCase);
                var allXlsLinks = new List<string>();

                string? bestUrl = null;
                foreach (Match m in linkRegex.Matches(html))
                {
                    string url = m.Groups["url"].Value;
                    allXlsLinks.Add(url);
                    string lower = url.ToLowerInvariant();

                    // This page also links to a plain "KFF Reference List" file,
                    // whose filename also contains "kff" - matching on "kff" alone
                    // would grab the wrong file. The cross reference file's name
                    // (kff_pota_cross_reference_...) always has "pota" together
                    // with "cross", so require both.
                    if (lower.Contains("pota") && (lower.Contains("cross") || lower.Contains("x-ref")))
                    {
                        bestUrl = url;
                        break;
                    }
                }

                if (bestUrl == null)
                {
                    string linkList = allXlsLinks.Count > 0 ? string.Join(", ", allXlsLinks) : "(no .xls/.xlsx links found at all)";
                    return (null, $"Page loaded, but no link matched. .xls/.xlsx links found on the page: {linkList}");
                }

                if (!bestUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    bestUrl = new Uri(new Uri("https://wwffkff.wordpress.com/"), bestUrl).ToString();

                // A fresh timeout window for the actual file download, rather than
                // sharing one budget with the page fetch above - that file can be
                // several MB, and a slow page load would otherwise eat into the
                // time left for downloading it.
                byte[] fileBytes;
                try
                {
                    using var downloadCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
                    fileBytes = await _http.GetByteArrayAsync(bestUrl, downloadCts.Token);
                }
                catch (Exception ex)
                {
                    return (null, $"Found the link ({bestUrl}) but couldn't download it: {ex.Message}");
                }

                string fileName = Path.GetFileName(new Uri(bestUrl).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "wwff_download.xls";

                string tempPath = Path.Combine(GetAppTempFolder(), fileName);
                await File.WriteAllBytesAsync(tempPath, fileBytes);
                _tempFilesToCleanUp.Add(tempPath);
                return (tempPath, "OK");
            }
            catch (Exception ex)
            {
                return (null, "Unexpected error: " + ex.Message);
            }
        }

        // Checks for a newer KFF-POTA cross reference file at most once every
        // DataRefreshInterval (currently 7 days), tracked persistently across app
        // restarts - not just once per session - so switching states repeatedly
        // in one sitting, or reopening the app daily, doesn't cause repeat network
        // checks for data that essentially never changes that often. If found, it
        // converts the new file into KffCrossReference.csv automatically. Called
        // from buttonLoadParks_Click, so this happens as part of the normal
        // "Load Parks for State" step - no separate button, no manual download.
        // This is best-effort and silent by design: if the site can't be reached,
        // the page format changed, or anything else goes wrong, it just leaves
        // whatever KFF data is already on disk untouched and Load Parks for State
        // continues normally.
        private async Task EnsureWwffDataAsync()
        {
            var lastChecked = WwffUpdateService.LoadLastCheckedTime(GetWritableAppDataFolder());
            if (lastChecked.HasValue && DateTime.UtcNow - lastChecked.Value < DataRefreshInterval)
                return;

            try
            {
                var (filePath, _) = await TryAutoDownloadWwffFileAsync();
                if (filePath == null) return;

                string outputPath = Path.Combine(GetWritableAppDataFolder(), KffFileName);
                string outputNamesPath = Path.Combine(GetWritableAppDataFolder(), KffNamesFileName);
                var result = await Task.Run(() => WwffUpdateService.ConvertXlsToCsv(filePath, outputPath, outputNamesPath));

                if (result.Success)
                {
                    WwffUpdateService.SaveInfoFile(GetWritableAppDataFolder(), result.SourceDate);
                    // Only recorded on genuine success, same as the park
                    // list/boundary/trail lookups below - a failed attempt
                    // (offline, site unreachable, page format changed, etc.)
                    // doesn't count as a check, so the very next load tries
                    // again immediately instead of waiting out the rest of
                    // DataRefreshInterval.
                    WwffUpdateService.SaveLastCheckedTime(GetWritableAppDataFolder(), DateTime.UtcNow);
                    UpdateWwffDateLabel(result.SourceDate);
                }
            }
            catch
            {
                // Best effort only - Load Parks for State continues regardless.
            }
        }

        private void ComboBoxState_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _parksLoaded = false;
            _adifLoaded = false;
            _lastMapHtml = null;
            UpdateButtonStates();
        }

        // Best-effort delete of every temp file this session wrote (see
        // _tempFilesToCleanUp) - run once, on close, rather than after each
        // individual use, since the map file in particular needs to survive
        // at least as long as the browser tab showing it stays open.
        private void CleanUpTempFiles()
        {
            foreach (var path in _tempFilesToCleanUp)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch
                {
                    // Not worth bothering the user with on their way out -
                    // e.g. a browser still has the map file open/locked.
                }
            }
        }

        // Colors a button Kelly Green with white text when it's the natural next
        // step, or resets it to the normal system look otherwise. Disabled buttons
        // (during a busy operation) are always left in the normal look regardless.
        private static void SetButtonHighlight(Button button, bool highlight)
        {
            if (highlight)
            {
                button.BackColor = NextStepColor;
                button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = NextStepColor;
            }
            else
            {
                button.UseVisualStyleBackColor = true;
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatStyle = FlatStyle.Standard;
            }
        }

        // Figures out where the user is in the Load Parks -> Load ADIF -> Export/Map
        // workflow and highlights whichever button is the sensible next click.
        private void UpdateButtonStates()
        {
            bool stateSelected = comboBoxState.SelectedValue != null;

            buttonLoadParks.Enabled = stateSelected;
            SetButtonHighlight(buttonLoadParks, stateSelected && !_parksLoaded);

            buttonLoadAdif.Enabled = _parksLoaded;
            SetButtonHighlight(buttonLoadAdif, _parksLoaded && !_adifLoaded);

            buttonExportCsv.Enabled = _adifLoaded;
            buttonExportExcel.Enabled = _adifLoaded;
            buttonShowMap.Enabled = _adifLoaded;
            SetButtonHighlight(buttonExportCsv, _adifLoaded);
            SetButtonHighlight(buttonExportExcel, _adifLoaded);
            SetButtonHighlight(buttonShowMap, _adifLoaded);

            // Not part of the highlighted "next step" chain above - it's an
            // optional follow-up to Show Map, not something the workflow
            // pushes the user toward.
            buttonSaveMap.Enabled = _lastMapHtml != null;
        }

        private void DataGridView1_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView1.Rows.Count) return;

            var park = dataGridView1.Rows[e.RowIndex].DataBoundItem as ParkRecord;
            if (park == null) return;

            if (park.Completed)
            {
                if (park.OutOfState)
                {
                    e.CellStyle.BackColor = Color.DarkOrange;
                    e.CellStyle.SelectionBackColor = Color.Chocolate;
                    e.CellStyle.ForeColor = Color.Black;
                    e.CellStyle.SelectionForeColor = Color.Black;
                }
                else
                {
                    e.CellStyle.BackColor = Color.IndianRed;
                    e.CellStyle.SelectionBackColor = Color.Firebrick;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionForeColor = Color.White;
                }
                if (dataGridView1.Columns[e.ColumnIndex].Name != "colCompleted")
                    e.CellStyle.Font = _strikeFont;

                // The Reference column is a hyperlink, which normally renders in
                // its own link colors rather than the cell's ForeColor - without
                // this, a completed park's reference link would stay blue and be
                // hard to read against the red/orange completed-row background.
                if (dataGridView1.Columns[e.ColumnIndex].Name == "colRef" &&
                    dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewLinkCell linkCell)
                {
                    linkCell.LinkColor = e.CellStyle.ForeColor;
                    linkCell.ActiveLinkColor = e.CellStyle.ForeColor;
                    linkCell.VisitedLinkColor = e.CellStyle.ForeColor;
                }
            }
            else if (dataGridView1.Columns[e.ColumnIndex].Name == "colRef" &&
                dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewLinkCell refLinkCell)
            {
                // DataGridViewLinkCell always paints in LinkColor, even when the
                // row is selected - it doesn't switch to SelectionForeColor the
                // way normal cells do. So the selected/unselected swap has to be
                // done by hand here rather than relying on cell style colors.
                Color linkColor = dataGridView1.Rows[e.RowIndex].Selected ? Color.Black : Color.White;
                refLinkCell.LinkColor = linkColor;
                refLinkCell.ActiveLinkColor = linkColor;
                refLinkCell.VisitedLinkColor = linkColor;
            }
        }

        // Opens the park's page on the POTA website when its Reference link is
        // clicked, e.g. https://pota.app/#/park/US-2001 - the same URL pattern
        // already used for the reference links in the map popups.
        private void DataGridView1_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dataGridView1.Columns[e.ColumnIndex].Name != "colRef") return;

            var park = dataGridView1.Rows[e.RowIndex].DataBoundItem as ParkRecord;
            if (park == null || string.IsNullOrWhiteSpace(park.Reference)) return;

            string url = "https://pota.app/#/park/" + Uri.EscapeDataString(park.Reference);
            try
            {
                var psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open that link: " + ex.Message);
            }
        }

        // The Xfer's column shows a comma-separated list of references (e.g.
        // "US-6532, US-8098") as plain text, not individual link cells - there's
        // no built-in DataGridView cell type for several independent links in
        // one cell. Figures out which single reference (if any) an X position
        // within the cell falls on, by measuring each ", "-joined piece with the
        // same renderer/font DataGridView itself uses, so hit-testing lines up
        // with what's actually painted.
        private string? HitTestFersToken(string fersText, int xInCell, Font font)
        {
            if (string.IsNullOrWhiteSpace(fersText)) return null;

            // DataGridView insets cell text by a couple of pixels even with no
            // explicit cell padding set - approximate, but close enough that a
            // click/hover anywhere on a reference's own digits lands correctly.
            const int cellTextInset = 2;
            int x = xInCell - cellTextInset;
            if (x < 0) return null;

            var tokens = fersText.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
            using var g = dataGridView1.CreateGraphics();

            int pos = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (i > 0)
                {
                    int sepWidth = TextRenderer.MeasureText(g, ", ", font, Size.Empty, TextFormatFlags.NoPadding).Width;
                    if (x < pos + sepWidth) return null; // click landed on the ", " separator itself
                    pos += sepWidth;
                }

                int tokenWidth = TextRenderer.MeasureText(g, tokens[i], font, Size.Empty, TextFormatFlags.NoPadding).Width;
                if (x < pos + tokenWidth) return tokens[i];
                pos += tokenWidth;
            }

            return null;
        }

        private bool TryJumpToParkInGrid(string reference)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.DataBoundItem is ParkRecord p && string.Equals(p.Reference, reference, StringComparison.OrdinalIgnoreCase))
                {
                    dataGridView1.ClearSelection();
                    row.Selected = true;
                    dataGridView1.CurrentCell = row.Cells["colRef"];
                    dataGridView1.FirstDisplayedScrollingRowIndex = row.Index;
                    return true;
                }
            }
            return false;
        }

        // Clicking a reference inside the Xfer's column jumps to that park's own
        // row, so you don't have to scroll/search for it by hand. If the search
        // box is currently filtering it out, the search is cleared first so the
        // jump always lands somewhere rather than silently doing nothing.
        private void DataGridView1_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dataGridView1.Columns[e.ColumnIndex].Name != "colFers") return;

            string fersText = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string ?? "";
            string? reference = HitTestFersToken(fersText, e.Location.X, dataGridView1.Font);
            if (reference == null) return;

            if (!TryJumpToParkInGrid(reference) && textBoxSearch.Text.Length > 0)
            {
                textBoxSearch.Text = "";
                TryJumpToParkInGrid(reference);
            }
        }

        // A KFF cell is almost always just one bare reference ("KFF-2097"),
        // but PotaService.SelectKffForState leaves the full combined,
        // semicolon-separated field ("KFF-0019 (KY); KFF-4586 (TN)") in place
        // for the rare case it couldn't narrow to one state - so this looks up
        // and joins the name for every "KFF-XXXX" piece present, stripping
        // each one's own "(LABEL)" suffix (that's not itself a lookup key).
        private string? GetKffTooltipText(string kffText)
        {
            if (string.IsNullOrWhiteSpace(kffText)) return null;

            var names = new List<string>();
            foreach (var rawSegment in kffText.Split(';'))
            {
                string segment = rawSegment.Trim();
                int paren = segment.IndexOf('(');
                string code = (paren > 0 ? segment.Substring(0, paren) : segment).Trim();
                if (_kffNames.TryGetValue(code, out var name))
                    names.Add(name);
            }

            return names.Count > 0 ? string.Join("; ", names) : null;
        }

        // Hovering a reference inside the Xfer's column shows that park's name;
        // hovering a KFF Ref cell shows its KFF-side name (see
        // GetKffTooltipText). Either also switches to a hand/default cursor as
        // a "this is clickable" cue for Xfer's specifically - both are plain
        // text columns, not link columns, so none of this happens
        // automatically the way it does for the Reference column.
        private void DataGridView1_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            string? tooltipText = null;
            string? tooltipKey = null;
            bool clickable = false;

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                string columnName = dataGridView1.Columns[e.ColumnIndex].Name;
                string cellText = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string ?? "";

                if (columnName == "colFers")
                {
                    string? reference = HitTestFersToken(cellText, e.Location.X, dataGridView1.Font);
                    if (reference != null)
                    {
                        var target = _parks.FirstOrDefault(p => string.Equals(p.Reference, reference, StringComparison.OrdinalIgnoreCase));
                        tooltipText = target?.Name ?? reference;
                        tooltipKey = "Fers:" + reference;
                        clickable = true;
                    }
                }
                else if (columnName == "colKff")
                {
                    tooltipText = GetKffTooltipText(cellText);
                    if (tooltipText != null) tooltipKey = "Kff:" + e.RowIndex;
                }
            }

            dataGridView1.Cursor = clickable ? Cursors.Hand : Cursors.Default;

            if (tooltipKey == _gridTooltipKey) return;
            _gridTooltipKey = tooltipKey;

            if (tooltipText == null)
            {
                _gridToolTip.Hide(dataGridView1);
                return;
            }

            // e.Location is relative to the CELL (that's what the hit-testing
            // above needs), but ToolTip.Show's x/y are relative to the control
            // passed in (dataGridView1 itself) - without converting, the tooltip
            // was being told to show at the wrong position for every cell except
            // one sitting exactly at the grid's own origin, which is why it
            // never visibly appeared.
            var cellRect = dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, cutOverflow: false);
            _gridToolTip.Show(tooltipText, dataGridView1, cellRect.X + e.Location.X + 12, cellRect.Y + e.Location.Y + 20);
        }

        private void DataGridView1_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            dataGridView1.Cursor = Cursors.Default;
            if (_gridTooltipKey == null) return;
            _gridTooltipKey = null;
            _gridToolTip.Hide(dataGridView1);
        }

        private async void buttonLoadParks_Click(object sender, EventArgs e)
        {
            if (comboBoxState.SelectedValue == null)
            {
                MessageBox.Show("Please choose a state first.");
                return;
            }
            string stateCode = comboBoxState.SelectedValue.ToString()!;
            SetBusy(true);
            try
            {
                progressBar1.Value = 0;
                labelStatus.Text = "Checking for KFF-POTA cross reference updates...";
                await EnsureWwffDataAsync();
                LoadKffLookup();
                LoadKffNamesLookup();
                LoadElevationLookup();

                _myActivations = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
                _allRawParks = await PotaService.GetAllParksAsync(
                    _http, GetWritableAppDataFolder(), DataRefreshInterval,
                    msg => labelStatus.Text = msg);
                var candidates = PotaService.FilterByState(_allRawParks, stateCode);
                labelStatus.Text = $"Looking up counties for {candidates.Count} parks...";
                var progress = new Progress<int>(pct => progressBar1.Value = Math.Min(pct, 100));
                await PotaService.GeocodeParksAsync(candidates, stateCode, progress);
                _parks = candidates.Where(p => !p.Exclude).OrderBy(p => p.Name).ToList();
                foreach (var park in _parks)
                {
                    // A park that spans multiple states can have a combined KFF
                    // field like "KFF-0019 (KY); KFF-4586 (TN)" (see
                    // WwffUpdateService) - narrow that down to just this state's
                    // KFF number, since that's the only one relevant here.
                    if (_kffLookup.TryGetValue(park.Reference, out var kff))
                        park.Kff = PotaService.SelectKffForState(kff, stateCode);
                }

                labelStatus.Text = "Checking park boundaries for Xfer's...";
                var boundaries = await FerLookupService.EnsureBoundariesAsync(
                    _http, GetWritableAppDataFolder(), stateCode, BoundaryRefreshInterval,
                    msg => labelStatus.Text = msg);
                _boundaries = boundaries;
                // ComputeFers is given only this state's own parks and only this
                // state's own boundary polygons, so every reference it returns is
                // guaranteed to already be one of _parks - never a park from a
                // neighboring state, even a bordering one.
                var ferResult = await Task.Run(() => FerLookupService.ComputeFers(_parks, boundaries));
                foreach (var park in _parks)
                {
                    if (ferResult.Fers.TryGetValue(park.Reference, out var others))
                        park.Fers = string.Join(", ", others);
                }

                labelStatus.Text = "Checking national trail routes for Xfer's...";
                var trails = await FerLookupService.EnsureTrailRoutesAsync(
                    _http, GetWritableAppDataFolder(), TrailRefreshInterval,
                    msg => labelStatus.Text = msg);
                _trails = trails;
                // candidates (not _parks) is the owner search list here - it's
                // the pre-exclusion set, which still includes a multi-state
                // national trail like the Appalachian Trail even though
                // GeocodeParksAsync above excluded it from _parks for having its
                // one POTA point outside this state. testParks is still _parks:
                // the real, in-state points to check against each trail's route.
                var trailResult = await Task.Run(() => FerLookupService.ComputeTrailFers(candidates, _parks, trails, boundaries));
                foreach (var park in _parks)
                {
                    if (!trailResult.Fers.TryGetValue(park.Reference, out var trailOthers)) continue;

                    // A reference can legitimately turn up from both the area
                    // match and the trail match (e.g. a corridor's own boundary
                    // also happens to run near a trail's route) - dedupe rather
                    // than showing it twice.
                    var combined = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(park.Fers))
                    {
                        foreach (var r in park.Fers.Split(','))
                            combined.Add(r.Trim());
                    }
                    foreach (var r in trailOthers)
                        combined.Add(r);
                    park.Fers = string.Join(", ", combined);
                }

                // A relevant trail may not be in _parks yet - its own POTA point
                // is often in a completely different state (the Appalachian
                // Trail's is in Georgia), which is exactly why GeocodeParksAsync
                // excluded it above. Add it as its own row so it's visible and
                // carries its own Fers - candidates already has real
                // County/State/ElevationFeet for it from that same earlier
                // geocoding pass (it ran before exclusion was applied), so no
                // re-geocoding is needed here.
                var newTrailRows = trailResult.RelevantTrailParks
                    .Where(t => !_parks.Any(p => string.Equals(p.Reference, t.Reference, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                foreach (var t in newTrailRows)
                {
                    t.AnchorOutOfState = true;
                    if (trailResult.Fers.TryGetValue(t.Reference, out var tOthers))
                        t.Fers = string.Join(", ", tOthers);
                    if (_kffLookup.TryGetValue(t.Reference, out var tKff))
                        t.Kff = tKff;
                }
                if (newTrailRows.Count > 0)
                {
                    _parks.AddRange(newTrailRows);
                    _parks = _parks.OrderBy(p => p.AnchorOutOfState).ThenBy(p => p.Name).ToList();
                }

                dataGridView1.Columns["colState"]!.Visible = newTrailRows.Count > 0;
                BindGrid();
                _parksLoaded = true;
                _adifLoaded = false;
                _lastMapHtml = null;

                // The Xfer's column is a candidate list derived from real boundary
                // and trail-route data matched to parks by name, not authoritative
                // - POTA itself requires an activator to verify an overlap (or,
                // for a trail, being within 100 ft of it) before claiming it. This
                // status note surfaces how much coverage this state actually had,
                // right where the user is already looking.
                string ferNote = boundaries.Count == 0
                    ? " Park boundary data wasn't available this time, so Xfer's detection was skipped."
                    : ferResult.Fers.Count > 0
                        ? $" Matched boundary data for {ferResult.MatchedBoundaryCount} of {_parks.Count} parks; " +
                          $"{ferResult.Fers.Count} may be part of an Xfer - verify overlap before claiming."
                        : $" Matched boundary data for {ferResult.MatchedBoundaryCount} of {_parks.Count} parks; no overlaps found.";
                string trailNote = newTrailRows.Count > 0
                    ? $" Also found {newTrailRows.Count} national trail(s) whose route crosses this state (shown with their own state) - verify you're within 100 ft of the trail before claiming."
                    : "";
                labelStatus.Text = $"Loaded {_parks.Count} parks for {stateCode}.{ferNote}{trailNote}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading parks: " + ex.Message);
                labelStatus.Text = "Error loading parks.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void buttonLoadAdif_Click(object sender, EventArgs e)
        {
            if (_parks.Count == 0)
            {
                MessageBox.Show("Load the park list for a state first.");
                return;
            }
            if (_allRawParks.Count == 0)
            {
                MessageBox.Show("The full park list isn't loaded yet. Click \"Load Parks for State\" again, then try the ADIF file once more.");
                return;
            }
            using var dlg = new OpenFileDialog
            {
                Filter = "ADIF files (*.adi;*.adif)|*.adi;*.adif|All files (*.*)|*.*",
                Title = "Select your ADIF log file"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            SetBusy(true);
            try
            {
                string text = File.ReadAllText(dlg.FileName);
                _myActivations = PotaService.ParseMyActivationDates(text);
                var completedRefs = new HashSet<string>(_myActivations.Keys, StringComparer.OrdinalIgnoreCase);
                _parks.RemoveAll(p => p.OutOfState);
                var inStateRefs = new HashSet<string>(_parks.Select(p => p.Reference), StringComparer.OrdinalIgnoreCase);
                int matchedInState = 0;
                foreach (var park in _parks)
                {
                    park.Completed = completedRefs.Contains(park.Reference);
                    if (park.Completed) matchedInState++;
                }
                var outOfStateRefs = completedRefs.Where(r => !inStateRefs.Contains(r)).ToList();
                var extraParks = new List<ParkRecord>();
                if (outOfStateRefs.Count > 0)
                {
                    var lookup = _allRawParks
                        .GroupBy(rp => rp.Reference, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                    foreach (var reference in outOfStateRefs)
                    {
                        if (lookup.TryGetValue(reference, out var raw))
                        {
                            extraParks.Add(new ParkRecord
                            {
                                Reference = raw.Reference,
                                Name = raw.Name,
                                Latitude = raw.Latitude,
                                Longitude = raw.Longitude,
                                Grid = raw.Grid,
                                // Xfer's detection needs per-state boundary data (see
                                // FerLookupService) - not worth downloading a whole
                                // extra state's worth just for a handful of
                                // out-of-state ADIF entries, so Fers stays blank here.
                                // Kff is left as the raw (possibly multi-state)
                                // value for now - narrowed to this park's own state
                                // once geocoding below knows what that is.
                                Kff = _kffLookup.TryGetValue(raw.Reference, out var kff) ? kff : "",
                                Completed = true,
                                OutOfState = true
                            });
                        }
                    }
                }
                if (extraParks.Count > 0)
                {
                    labelStatus.Text = $"Looking up {extraParks.Count} out-of-state park(s)...";
                    progressBar1.Value = 0;
                    var progress = new Progress<int>(pct => progressBar1.Value = Math.Min(pct, 100));
                    await PotaService.GeocodeExtraParksAsync(extraParks, progress);
                    foreach (var park in extraParks)
                    {
                        if (!string.IsNullOrEmpty(park.State))
                            park.Kff = PotaService.SelectKffForState(park.Kff, park.State);
                    }
                    _parks.AddRange(extraParks);
                    _parks = _parks.OrderBy(p => p.OutOfState || p.AnchorOutOfState).ThenBy(p => p.Name).ToList();
                }
                // Also stays visible if a national trail row (AnchorOutOfState)
                // was already added by the parks load - this ADIF pass shouldn't
                // hide a State column that row still needs.
                dataGridView1.Columns["colState"]!.Visible = extraParks.Count > 0 || _parks.Any(p => p.AnchorOutOfState);
                BindGrid();
                _adifLoaded = true;
                int unresolvedCount = outOfStateRefs.Count - extraParks.Count;
                if (extraParks.Count > 0 && unresolvedCount == 0)
                {
                    labelStatus.Text = $"Marked {matchedInState} in-state park(s) complete, plus {extraParks.Count} out-of-state park(s) found.";
                }
                else if (extraParks.Count > 0 && unresolvedCount > 0)
                {
                    labelStatus.Text = $"Marked {matchedInState} in-state park(s) complete, plus {extraParks.Count} out-of-state park(s) found. " +
                        $"({unresolvedCount} other reference(s) in the ADIF didn't match any known park.)";
                }
                else if (unresolvedCount > 0)
                {
                    labelStatus.Text = $"Marked {matchedInState} in-state park(s) complete. Found {unresolvedCount} out-of-state reference(s) " +
                        "in the ADIF, but none matched a park in the POTA master list.";
                }
                else
                {
                    labelStatus.Text = $"Marked {matchedInState} of {_parks.Count} parks as completed from ADIF.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading ADIF file: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void buttonShowMap_Click(object sender, EventArgs e)
        {
            if (_parks.Count == 0)
            {
                MessageBox.Show("Load the park list for a state first.");
                return;
            }
            if (_myActivations.Count == 0)
            {
                MessageBox.Show("Load your ADIF file first, so the map can show which parks you've activated.");
                return;
            }

            SetBusy(true);
            try
            {
                labelStatus.Text = "Looking up activation history from POTA (this can take a little while)...";
                progressBar1.Value = 0;
                var progress = new Progress<int>(pct => progressBar1.Value = Math.Min(pct, 100));

                var activationInfo = await PotaService.FetchActivationInfoAsync(_http, _parks, progress);

                var mapParks = new List<MapParkDto>();
                foreach (var park in _parks)
                {
                    var dto = new MapParkDto
                    {
                        Reference = park.Reference,
                        Name = park.Name,
                        Lat = park.Latitude,
                        Lon = park.Longitude,
                        County = park.County,
                        ElevationFeet = park.ElevationFeet,
                        Kff = park.Kff,
                        Completed = park.Completed
                    };

                    if (activationInfo.TryGetValue(park.Reference, out var info))
                    {
                        dto.CommunityCount = info.Count;
                        dto.CommunityCallsign = info.LastCallsign;
                        dto.CommunityDate = info.LastDate.HasValue ? info.LastDate.Value.ToString("dd MMM yyyy") : "";
                    }

                    if (_myActivations.TryGetValue(park.Reference, out var myDates) && myDates.Count > 0)
                    {
                        var distinctDates = myDates.Distinct().OrderByDescending(d => d).ToList();
                        dto.MyCount = distinctDates.Count;
                        dto.MyDate = distinctDates[0].ToString("dd MMM yyyy");
                    }

                    mapParks.Add(dto);
                }

                string html = MapService.BuildMapHtml(mapParks, BuildBoundaryLayerDtos());
                string tempPath = Path.Combine(GetAppTempFolder(), "POTAActivatorParkActivations_Map_" + Guid.NewGuid().ToString("N") + ".html");
                File.WriteAllText(tempPath, html, Encoding.UTF8);
                _tempFilesToCleanUp.Add(tempPath);
                _lastMapHtml = html;

                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                };
                Process.Start(psi);

                labelStatus.Text = "Map opened in your default browser.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error building the map: " + ex.Message);
                labelStatus.Text = "Error building the map.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        // A few national-trail names come through from potamap.ol's source
        // data looking rougher than the rest (see FerLookupService -
        // ComputeTrailFers relies on the exact original values for matching,
        // so this cleanup is applied here, purely for map display, rather
        // than to TrailRoute.Name itself).
        private static readonly Dictionary<string, string> TrailDisplayNameOverrides =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["ICE_AGE"] = "Ice Age Trail",
            ["Appalachian trail"] = "Appalachian Trail",
            ["Sante Fe NHT"] = "Santa Fe NHT",
        };

        private static string PrettifyTrailName(string name) =>
            TrailDisplayNameOverrides.TryGetValue(name, out var pretty) ? pretty : name;

        // Groups _boundaries by their source layer (PAD-US, Community, EC,
        // ...) and adds each of _trails as its own layer, for the map's
        // toggleable boundary/trail overlays - see MapBoundaryLayerDto.
        // Ordered alphabetically (areas first, then trails) so a layer sits
        // in the same spot in the checkbox list every time, rather than
        // shuffling with download order.
        // Layers left out of the map's checkbox list specifically (not out of
        // n-fer detection - _boundaries still includes these for ComputeFers,
        // they're just not offered as a toggleable overlay). FFMA and
        // Counties don't need an entry here since EnsureBoundariesAsync never
        // downloads them as boundaries in the first place - see
        // FerLookupService.DownloadSourceIndexAsync.
        private static readonly HashSet<string> ExcludedMapLayerNames =
            new(StringComparer.OrdinalIgnoreCase) { "Community" };

        private List<MapBoundaryLayerDto> BuildBoundaryLayerDtos()
        {
            var layers = new List<MapBoundaryLayerDto>();

            var areaGroups = _boundaries
                .Where(b => !string.IsNullOrWhiteSpace(b.Layer) && !ExcludedMapLayerNames.Contains(b.Layer))
                .GroupBy(b => b.Layer, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var group in areaGroups)
            {
                var layerDto = new MapBoundaryLayerDto { Name = group.Key, IsLine = false };
                foreach (var boundary in group)
                    layerDto.Features.Add(BuildAreaFeatureDto(boundary));
                layers.Add(layerDto);
            }

            var trailsByName = GetTrailsNearLoadedState()
                .OrderBy(t => PrettifyTrailName(t.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var trail in trailsByName)
            {
                var layerDto = new MapBoundaryLayerDto { Name = PrettifyTrailName(trail.Name), IsLine = true };
                layerDto.Features.Add(BuildTrailFeatureDto(trail));
                layers.Add(layerDto);
            }

            return layers;
        }

        // _trails (unlike _boundaries) isn't already scoped to the loaded
        // state - EnsureTrailRoutesAsync downloads every national trail
        // nationwide once, shared across all states, since a single route
        // can cross many of them (see FerLookupService). Narrowed here to
        // just the trails that actually pass near the loaded state's parks,
        // padded by ~1 degree (roughly 50-70 miles) so a trail that only
        // clips a corner - where no park happens to sit right at that edge -
        // still shows up. Otherwise every state's map would offer the same
        // ~16 nationwide trail checkboxes regardless of relevance (e.g.
        // California NHT showing up while viewing Alabama).
        //
        // Tests each trail's actual vertices against proximity to an actual
        // loaded park, not against a single bounding rectangle spanning all
        // of them - confirmed necessary, not just theoretical: New York's
        // parks range from NYC (~lat 40.5) to the western Southern Tier
        // (~lat 42) to the Adirondacks (~lat 45), so the rectangle spanning
        // all of them has a whole southwestern corner - roughly Pittsburgh -
        // that isn't really near any of them. A padded-rectangle-only test
        // let Lewis and Clark NHT's real Pittsburgh-departure segment (which
        // has nothing to do with NY) fall inside that corner and pass.
        private List<FerLookupService.TrailRoute> GetTrailsNearLoadedState()
        {
            const double padDegrees = 0.5;

            var parkPoints = _parks
                // (0, 0) is what an ungeocoded park looks like - see MapService.
                .Where(p => p.Latitude != 0 || p.Longitude != 0)
                .Select(p => (Lon: p.Longitude, Lat: p.Latitude))
                .ToList();
            if (parkPoints.Count == 0) return new List<FerLookupService.TrailRoute>();

            // A cheap per-trail reject (checked before the precise, per-park
            // scan below) using the same padded rectangle spanning every
            // park - still safe as a reject, since a trail whose bounding
            // box doesn't even reach that rectangle can't be near any
            // individual park inside it either.
            double minLon = parkPoints.Min(p => p.Lon) - padDegrees;
            double maxLon = parkPoints.Max(p => p.Lon) + padDegrees;
            double minLat = parkPoints.Min(p => p.Lat) - padDegrees;
            double maxLat = parkPoints.Max(p => p.Lat) + padDegrees;

            return _trails
                .Where(t => TrailPassesNearAnyPark(t, parkPoints, minLon, minLat, maxLon, maxLat, padDegrees))
                .ToList();
        }

        private static bool TrailPassesNearAnyPark(
            FerLookupService.TrailRoute trail, List<(double Lon, double Lat)> parkPoints,
            double minLon, double minLat, double maxLon, double maxLat, double padDegrees)
        {
            if (trail.MaxLon < minLon || trail.MinLon > maxLon ||
                trail.MaxLat < minLat || trail.MinLat > maxLat)
                return false;

            foreach (var line in trail.Lines)
            {
                for (int i = 0; i < line.Length; i += 2)
                {
                    double lon = line[i], lat = line[i + 1];
                    if (lon < minLon || lon > maxLon || lat < minLat || lat > maxLat) continue;

                    foreach (var park in parkPoints)
                    {
                        if (Math.Abs(lon - park.Lon) <= padDegrees && Math.Abs(lat - park.Lat) <= padDegrees)
                            return true;
                    }
                }
            }
            return false;
        }

        private static MapGeoFeatureDto BuildAreaFeatureDto(FerLookupService.BoundaryFeature boundary)
        {
            var dto = new MapGeoFeatureDto { Name = boundary.Name };
            foreach (var part in boundary.Polys)
            {
                var rings = new List<double[][]>();
                foreach (var flatRing in part)
                    rings.Add(FlatToPoints(flatRing));
                dto.Geometry.Add(rings);
            }
            return dto;
        }

        private static MapGeoFeatureDto BuildTrailFeatureDto(FerLookupService.TrailRoute trail)
        {
            var dto = new MapGeoFeatureDto { Name = trail.Name };
            foreach (var flatLine in trail.Lines)
                dto.Geometry.Add(new List<double[][]> { FlatToPoints(flatLine) });
            return dto;
        }

        // BoundaryFeature/TrailRoute store each ring/line as a flat
        // [lon,lat,lon,lat,...] array (see FerLookupService); the map DTOs
        // use one [lon,lat] pair per point instead, matching GeoJSON
        // coordinate shape so the map's JS can treat both the same way.
        private static double[][] FlatToPoints(double[] flat)
        {
            int n = flat.Length / 2;
            var points = new double[n][];
            for (int i = 0; i < n; i++)
                points[i] = new double[] { flat[2 * i], flat[2 * i + 1] };
            return points;
        }

        // Writes out a permanent copy of the map buttonShowMap_Click last
        // opened - the browser was only ever shown a %TEMP% copy, which
        // CleanUpTempFiles deletes when the program closes.
        private void buttonSaveMap_Click(object sender, EventArgs e)
        {
            if (_lastMapHtml == null)
            {
                MessageBox.Show("Show the map first, then you can save a copy of it.");
                return;
            }
            string stateCode = comboBoxState.SelectedValue?.ToString() ?? "Parks";
            using var dlg = new SaveFileDialog
            {
                Filter = "HTML files (*.html)|*.html",
                FileName = $"POTA_{stateCode}_Map.html"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dlg.FileName, _lastMapHtml, Encoding.UTF8);
                MessageBox.Show("Map file saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving map: " + ex.Message);
            }
        }

        private void buttonExportCsv_Click(object sender, EventArgs e)
        {
            if (_parks.Count == 0)
            {
                MessageBox.Show("Load the park list for a state first.");
                return;
            }
            string stateCode = comboBoxState.SelectedValue?.ToString() ?? "Parks";
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"POTA_{stateCode}_Parks.csv"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                PotaService.ExportCsv(dlg.FileName, _parks);
                MessageBox.Show("CSV file saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving CSV: " + ex.Message);
            }
        }

        private void buttonExportExcel_Click(object sender, EventArgs e)
        {
            if (_parks.Count == 0)
            {
                MessageBox.Show("Load the park list for a state first.");
                return;
            }
            string stateCode = comboBoxState.SelectedValue?.ToString() ?? "Parks";
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = $"POTA_{stateCode}_Parks.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                PotaService.ExportExcel(dlg.FileName, _parks);
                MessageBox.Show("Excel file saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving Excel file: " + ex.Message);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var help = new HelpForm();
            help.ShowDialog(this);
        }

        private void BindGrid()
        {
            dataGridView1.DataSource = null;
            dataGridView1.AutoGenerateColumns = false;
            string searchText = textBoxSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                dataGridView1.DataSource = new SortableBindingList<ParkRecord>(_parks);
            }
            else
            {
                var filtered = new List<ParkRecord>();
                foreach (var p in _parks)
                {
                    bool matchRef = p.Reference != null && p.Reference.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchName = p.Name != null && p.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchCounty = p.County != null && p.County.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchGrid = p.Grid != null && p.Grid.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchFers = p.Fers != null && p.Fers.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                    // Added criteria rule to scan state code values
                    bool matchState = p.State != null && p.State.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (matchRef || matchName || matchCounty || matchGrid || matchFers || matchState)
                    {
                        filtered.Add(p);
                    }
                }
                dataGridView1.DataSource = new SortableBindingList<ParkRecord>(filtered);
            }
        }

        private void SetBusy(bool busy)
        {
            comboBoxState.Enabled = !busy;
            textBoxSearch.Enabled = !busy && _parks.Count > 0;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (busy)
            {
                buttonLoadParks.Enabled = false;
                buttonLoadAdif.Enabled = false;
                buttonExportCsv.Enabled = false;
                buttonExportExcel.Enabled = false;
                buttonShowMap.Enabled = false;
                buttonSaveMap.Enabled = false;
                SetButtonHighlight(buttonLoadParks, false);
                SetButtonHighlight(buttonLoadAdif, false);
                SetButtonHighlight(buttonExportCsv, false);
                SetButtonHighlight(buttonExportExcel, false);
                SetButtonHighlight(buttonShowMap, false);
            }
            else
            {
                UpdateButtonStates();
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            BindGrid();
        }
    }
}
