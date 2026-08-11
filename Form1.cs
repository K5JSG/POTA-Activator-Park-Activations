using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows.Forms;

namespace POTA_Check
{
    public partial class Form1 : Form
    {
        private readonly HttpClient _http = new HttpClient();
        private List<ParkRecord> _parks = new List<ParkRecord>();
        private List<RawPark> _allRawParks = new List<RawPark>();
        private Font? _strikeFont;

        // Reference -> list of dates you activated that park, built from the ADIF file.
        private Dictionary<string, List<DateTime>> _myActivations =
            new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

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
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) POTACheckApp/1.0");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxState.DataSource = PotaService.UsStates;
            comboBoxState.DisplayMember = "Name";
            comboBoxState.ValueMember = "Code";

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRef", HeaderText = "Reference", DataPropertyName = "Reference", Width = 90, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Park Name", DataPropertyName = "Name", Width = 220, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLat", HeaderText = "Latitude", DataPropertyName = "Latitude", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLon", HeaderText = "Longitude", DataPropertyName = "Longitude", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colGrid", HeaderText = "Grid Square", DataPropertyName = "Grid", Width = 90, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCounty", HeaderText = "County", DataPropertyName = "County", Width = 140, SortMode = DataGridViewColumnSortMode.Automatic });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "colState", HeaderText = "State", DataPropertyName = "State", Width = 60, SortMode = DataGridViewColumnSortMode.Automatic, Visible = false });
            dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colCompleted", HeaderText = "Completed", DataPropertyName = "Completed", Width = 80, SortMode = DataGridViewColumnSortMode.Automatic });

            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            _strikeFont = new Font(dataGridView1.Font, FontStyle.Strikeout);

            comboBoxState.SelectedIndexChanged += ComboBoxState_SelectedIndexChanged;
            UpdateButtonStates();
        }

        private void ComboBoxState_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _parksLoaded = false;
            _adifLoaded = false;
            UpdateButtonStates();
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

            buttonExportCsv.Enabled = _parksLoaded;
            buttonExportExcel.Enabled = _parksLoaded;
            buttonShowMap.Enabled = _parksLoaded;
            SetButtonHighlight(buttonExportCsv, _adifLoaded);
            SetButtonHighlight(buttonExportExcel, _adifLoaded);
            SetButtonHighlight(buttonShowMap, _adifLoaded);
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
                }
                else
                {
                    e.CellStyle.BackColor = Color.IndianRed;
                    e.CellStyle.SelectionBackColor = Color.Firebrick;
                    e.CellStyle.ForeColor = Color.White;
                }
                if (dataGridView1.Columns[e.ColumnIndex].Name != "colCompleted")
                    e.CellStyle.Font = _strikeFont;
            }
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
                labelStatus.Text = "Downloading park list from POTA...";
                progressBar1.Value = 0;
                _myActivations = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
                _allRawParks = await PotaService.DownloadAllParksAsync(_http);
                var candidates = PotaService.FilterByState(_allRawParks, stateCode);
                labelStatus.Text = $"Looking up counties for {candidates.Count} parks...";
                var progress = new Progress<int>(pct => progressBar1.Value = Math.Min(pct, 100));
                await PotaService.GeocodeParksAsync(_http, candidates, stateCode, progress);
                _parks = candidates.Where(p => !p.Exclude).OrderBy(p => p.Name).ToList();
                dataGridView1.Columns["colState"]!.Visible = false;
                BindGrid();
                _parksLoaded = true;
                _adifLoaded = false;
                labelStatus.Text = $"Loaded {_parks.Count} parks for {stateCode}.";
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
                var completedRefs = PotaService.ParseAdifCompletedRefs(text);
                _myActivations = PotaService.ParseMyActivationDates(text);
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
                    await PotaService.GeocodeExtraParksAsync(_http, extraParks, progress);
                    _parks.AddRange(extraParks);
                    _parks = _parks.OrderBy(p => p.OutOfState).ThenBy(p => p.Name).ToList();
                }
                dataGridView1.Columns["colState"]!.Visible = extraParks.Count > 0;
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

                string html = MapService.BuildMapHtml(mapParks);
                string tempPath = Path.Combine(Path.GetTempPath(), "POTACheck_Map_" + Guid.NewGuid().ToString("N") + ".html");
                File.WriteAllText(tempPath, html, Encoding.UTF8);

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

                    // Added criteria rule to scan state code values
                    bool matchState = p.State != null && p.State.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (matchRef || matchName || matchCounty || matchGrid || matchState)
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
