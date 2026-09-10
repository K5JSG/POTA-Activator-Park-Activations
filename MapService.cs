using System.Text.Json;
using System.Text.Json.Serialization;

namespace PotaActivatorParkActivations
{
    // One park's worth of information, packaged up exactly the way the map page needs it.
    // The [JsonPropertyName] attributes control what the field is called in the JSON/JavaScript
    // that gets embedded in the HTML file - JavaScript convention is lowerCamelCase.
    public class MapParkDto
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("county")]
        public string County { get; set; } = "";

        [JsonPropertyName("elevationFeet")]
        public double? ElevationFeet { get; set; }

        [JsonPropertyName("kff")]
        public string Kff { get; set; } = "";

        // True = you have activated this park yourself (green pin).
        // False = you have not activated it yet (yellow pin, or orange if
        // BoatAccessOnly - see that field).
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        // User-flagged (grid checkbox, not derived from any data source) as
        // only reachable by boat. Shown as an orange pin, but only while not
        // yet completed - completed status (green) still wins once you've
        // actually activated it, same priority order as the grid's row
        // coloring.
        [JsonPropertyName("boatAccessOnly")]
        public bool BoatAccessOnly { get; set; }

        [JsonPropertyName("communityCount")]
        public int CommunityCount { get; set; }

        [JsonPropertyName("communityCallsign")]
        public string CommunityCallsign { get; set; } = "";

        [JsonPropertyName("communityDate")]
        public string CommunityDate { get; set; } = "";

        [JsonPropertyName("myCount")]
        public int MyCount { get; set; }

        [JsonPropertyName("myDate")]
        public string MyDate { get; set; } = "";
    }

    // One SOTA summit, for the optional "SOTA Summits" map layer - see
    // Form1's _sotaSummits (already filtered to currently-valid US summits)
    // and FerLookupService.ComputeSotaMatches, which is what actually decides
    // which park's row gets a SOTA Ref value. This layer shows every loaded
    // summit regardless of that match, since a summit just outside every
    // park's boundary is still useful to see on the map.
    public class MapSotaSummitDto
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("altFeet")]
        public double AltFeet { get; set; }

        [JsonPropertyName("points")]
        public int Points { get; set; }

        [JsonPropertyName("activationCount")]
        public int ActivationCount { get; set; }

        [JsonPropertyName("activationCall")]
        public string ActivationCall { get; set; } = "";

        [JsonPropertyName("activationDate")]
        public string ActivationDate { get; set; } = "";
    }

    // One toggleable map layer - e.g. NY's "PAD-US" or "EC" area layers, or a
    // single named national trail. Mirrors how potamap.us (github.com/cwhelchel/
    // potamap.ol) groups its own boundary data into named, independently
    // checkbox-toggled layers - see FerLookupService.BoundaryFeature.Layer.
    public class MapBoundaryLayerDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // True for a trail (rendered as a line) rather than an area boundary
        // (rendered as a filled polygon).
        [JsonPropertyName("isLine")]
        public bool IsLine { get; set; }

        [JsonPropertyName("features")]
        public List<MapGeoFeatureDto> Features { get; set; } = new();
    }

    // One boundary polygon or trail route within a layer.
    public class MapGeoFeatureDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // Geometry[part][ring][point] = [lon, lat]. For an area layer this is
        // GeoJSON MultiPolygon coordinates (a part's first ring is its outer
        // boundary, any further rings are holes - mirrors
        // FerLookupService.BoundaryFeature.Polys). For a line layer, each part
        // holds exactly one "ring", which is really just that line's own point
        // sequence (no ring/hole concept for a line) - reusing the same shape
        // so the map's JS has a single rendering code path for both.
        [JsonPropertyName("geometry")]
        public List<List<double[][]>> Geometry { get; set; } = new();

        // Mirrors FerLookupService.BoundaryFeature/TrailRoute's own
        // Min/Max fields - a cheap reject before the real point-in-polygon
        // (or point-near-trail) math for the ""am I in/near this park right
        // now"" live GPS check (see MapService's InfoBar JS), the same
        // bounding-box-first pattern that code already uses server-side.
        // Recomputing these from Geometry in JS on every load would be
        // redundant work for data already computed once here.
        [JsonPropertyName("minLon")]
        public double MinLon { get; set; }
        [JsonPropertyName("minLat")]
        public double MinLat { get; set; }
        [JsonPropertyName("maxLon")]
        public double MaxLon { get; set; }
        [JsonPropertyName("maxLat")]
        public double MaxLat { get; set; }
    }

    public static class MapService
    {
        // Builds one complete, self-contained HTML file (map + all the data + all the
        // JavaScript needed to draw it) as a single string. This file can be opened by
        // any web browser - no server, no install, no account, no API key. It uses
        // Leaflet (a free open-source mapping library) and OpenStreetMap map tiles
        // (also free), both loaded from their public content-delivery networks.
        public static string BuildMapHtml(
            List<MapParkDto> parks, List<MapBoundaryLayerDto>? boundaryLayers = null,
            List<MapSotaSummitDto>? sotaSummits = null)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            string parkJson = JsonSerializer.Serialize(parks, jsonOptions);
            string boundaryJson = JsonSerializer.Serialize(boundaryLayers ?? new List<MapBoundaryLayerDto>(), jsonOptions);
            string sotaJson = JsonSerializer.Serialize(sotaSummits ?? new List<MapSotaSummitDto>(), jsonOptions);

            // Guard against a park/boundary/summit name that happens to contain
            // "</script>" - that would otherwise break out of our embedded
            // <script> block.
            parkJson = parkJson.Replace("</", "<\\/");
            boundaryJson = boundaryJson.Replace("</", "<\\/");
            sotaJson = sotaJson.Replace("</", "<\\/");

            string html = HtmlTemplate
                .Replace("__PARK_DATA__", parkJson)
                .Replace("__BOUNDARY_DATA__", boundaryJson)
                .Replace("__SOTA_DATA__", sotaJson);
            return html;
        }

        private const string HtmlTemplate = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"" />
<title>POTA Activator Park Activations - Park Map</title>
<meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
<style>
  html, body { margin: 0; padding: 0; height: 100%; font-family: Segoe UI, Arial, sans-serif; }
  #map { position: absolute; top: 0; left: 0; right: 0; bottom: 0; }
  .gps-status {
    position: absolute; bottom: 24px; left: 12px; z-index: 1000;
    background: white; padding: 8px 12px; border-radius: 6px;
    box-shadow: 0 1px 5px rgba(0,0,0,0.4); font-size: 12px;
  }
  .legend-swatch {
    display: inline-block; width: 12px; height: 12px; border-radius: 50%;
    margin-right: 6px; border: 1px solid #333; vertical-align: middle;
  }
  .pota-popup a { color: #1a5fb4; text-decoration: none; font-weight: bold; }
  .pota-popup a:hover { text-decoration: underline; }
  .pota-popup .my-line { margin-top: 6px; color: #2e8b22; font-weight: bold; }
  .pota-popup .boat-line { margin-top: 6px; color: #d45500; font-weight: bold; }
  .boundary-popup-layer { margin-top: 2px; font-size: 11px; opacity: 0.7; }
  .layer-swatch {
    display: inline-block; width: 11px; height: 11px;
    margin-right: 6px; vertical-align: middle; border: 1px solid rgba(0,0,0,0.35);
  }
  .layer-count { opacity: 0.6; }
  .layer-tree-row {
    display: flex; align-items: center; cursor: pointer;
    padding: 2px 0; white-space: nowrap;
  }
  .layer-tree-group-header .layer-tree-row { padding: 0; flex: 1 1 auto; }
  .layer-tree-toggle {
    display: inline-block; width: 14px; flex: 0 0 auto;
    text-align: center; cursor: pointer; user-select: none; font-size: 10px;
  }
  .layer-tree-children { margin-left: 16px; }
  .recenter-control a { color: #1a73e8; }
  .recenter-control a svg { vertical-align: -4px; }
  .recenter-control a.waiting { opacity: 0.5; cursor: wait; }
  .sidebar-toggle {
    position: absolute; top: 90px; left: 12px; z-index: 1001;
    background: white; border: none; border-radius: 4px;
    padding: 8px 10px; font-size: 16px; line-height: 1; cursor: pointer;
    box-shadow: 0 1px 5px rgba(0,0,0,0.4);
  }
  .sidebar {
    position: absolute; top: 90px; left: 12px; bottom: 24px; z-index: 1000;
    width: 240px; max-width: calc(100vw - 24px);
    background: white; border-radius: 6px; box-shadow: 0 1px 5px rgba(0,0,0,0.4);
    overflow-y: auto; font-size: 13px;
  }
  .sidebar-header {
    display: flex; align-items: center; justify-content: space-between;
    padding: 8px 10px; font-weight: bold;
    border-bottom: 1px solid rgba(0,0,0,0.1);
    position: sticky; top: 0; background: inherit;
  }
  .sidebar-header button {
    background: none; border: none; font-size: 14px; cursor: pointer; color: inherit;
  }
  .sidebar-section { padding: 8px 10px; border-bottom: 1px solid rgba(0,0,0,0.08); }
  .sidebar-section:last-child { border-bottom: none; }
  .sidebar-section[hidden] { display: none; }
  .sidebar-heading {
    font-weight: bold; margin-bottom: 4px; font-size: 11px; opacity: 0.65;
    text-transform: uppercase; letter-spacing: .04em;
  }
  .sidebar-row { display: flex; align-items: center; padding: 3px 0; cursor: pointer; white-space: nowrap; }
  .sidebar-row input { margin: 0 6px 0 0; }
  .info-bar {
    position: absolute; top: 12px; left: 50%; transform: translateX(-50%); z-index: 1002;
    max-width: min(600px, calc(100vw - 24px));
    background: white; padding: 8px 16px; border-radius: 6px;
    box-shadow: 0 1px 5px rgba(0,0,0,0.4); font-size: 13px; line-height: 1.5;
    text-align: center;
  }
  .info-bar.info-bar-active { background: #2e8b22; color: white; font-weight: bold; }
  .context-menu {
    position: fixed; z-index: 2000;
    background: white; border-radius: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.35);
    padding: 4px 0; font-size: 13px; min-width: 170px;
  }
  .context-menu-item { padding: 8px 14px; cursor: pointer; white-space: nowrap; }
  .context-menu-item:hover { background: #f0f0f0; }
  .measure-panel {
    position: absolute; bottom: 24px; left: 50%; transform: translateX(-50%); z-index: 1002;
    background: white; padding: 8px 14px; border-radius: 6px;
    box-shadow: 0 1px 5px rgba(0,0,0,0.4); font-size: 13px;
    display: flex; align-items: center; gap: 10px;
  }
  .measure-panel[hidden] { display: none; }
  .measure-panel button { background: none; border: none; font-size: 14px; cursor: pointer; color: inherit; padding: 0; }
  #map.measuring-cursor { cursor: crosshair; }
  @media (prefers-color-scheme: dark) {
    body { background: #1e1e1e; }
    .gps-status { background: #2d2d30; color: #e8e8e8; box-shadow: 0 1px 5px rgba(0,0,0,0.6); }
    .info-bar { background: #2d2d30; color: #e8e8e8; box-shadow: 0 1px 5px rgba(0,0,0,0.6); }
    .info-bar.info-bar-active { background: #2e8b22; color: white; }
    .legend-swatch { border-color: #999; }
    .pota-popup a { color: #6ab0f3; }
    .pota-popup .my-line { color: #6fdc6f; }
    .pota-popup .boat-line { color: #ff9a4d; }
    .layer-swatch { border-color: rgba(255,255,255,0.4); }
    .sidebar-toggle, .sidebar {
      background: #2d2d30; color: #e8e8e8;
      box-shadow: 0 1px 5px rgba(0,0,0,0.6);
    }
    .sidebar-header { border-bottom-color: rgba(255,255,255,0.15); }
    .sidebar-section { border-bottom-color: rgba(255,255,255,0.12); }
    .context-menu { background: #2d2d30; color: #e8e8e8; box-shadow: 0 2px 8px rgba(0,0,0,0.6); }
    .context-menu-item:hover { background: #3f3f42; }
    .measure-panel { background: #2d2d30; color: #e8e8e8; box-shadow: 0 1px 5px rgba(0,0,0,0.6); }
  }
</style>
</head>
<body>
<div id=""map""></div>
<div class=""gps-status"" id=""gpsStatus"" hidden></div>
<div class=""info-bar"" id=""infoBar"" hidden></div>
<div class=""measure-panel"" id=""measurePanel"" hidden>
  <span id=""measureDistanceText"">Distance: 0 ft</span>
  <button id=""measureCloseBtn"" title=""Clear measurement"">✕</button>
</div>
<div class=""context-menu"" id=""mapContextMenu"" hidden></div>

<button id=""sidebarToggle"" class=""sidebar-toggle"" title=""Show/hide layers panel"">☰</button>
<div id=""sidebar"" class=""sidebar"">
  <div class=""sidebar-header"">
    <span>Layers</span>
    <button id=""sidebarClose"" title=""Hide layers panel"">✕</button>
  </div>
  <div class=""sidebar-section"">
    <div class=""sidebar-heading"">Base Map</div>
    <label class=""sidebar-row""><input type=""radio"" name=""baseLayer"" id=""baseLayerStreet"" checked /> Street</label>
    <label class=""sidebar-row""><input type=""radio"" name=""baseLayer"" id=""baseLayerSatellite"" /> Satellite</label>
  </div>
  <div class=""sidebar-section"">
    <div class=""sidebar-heading"">Show</div>
    <label class=""sidebar-row""><input type=""checkbox"" id=""overlayWorked"" checked /> Worked</label>
    <label class=""sidebar-row""><input type=""checkbox"" id=""overlayNotWorked"" checked /> Not worked</label>
    <label class=""sidebar-row""><input type=""checkbox"" id=""overlaySota"" /> SOTA Summits</label>
  </div>
  <div class=""sidebar-section"" id=""boundaryLayerSection"" hidden></div>
  <div class=""sidebar-section"">
    <div class=""sidebar-heading"">Legend</div>
    <div><span class=""legend-swatch"" style=""background:#FFD500;""></span>Not yet activated by me</div>
    <div><span class=""legend-swatch"" style=""background:#FF6700;""></span>Boat access only, not yet activated</div>
    <div><span class=""legend-swatch"" style=""background:#2E8B22;""></span>Activated by me</div>
    <div><span class=""legend-swatch"" style=""background:#1a73e8;""></span>Your location</div>
    <div><span class=""legend-swatch"" style=""background:#8B4513;""></span>SOTA summit</div>
  </div>
</div>

<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
<script>
var parkData = __PARK_DATA__;
var boundaryLayers = __BOUNDARY_DATA__;
var sotaData = __SOTA_DATA__;

function escapeHtml(text) {
  if (!text) return '';
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/""/g, '&quot;');
}

// A plain filled circle, not a teardrop - matches the real pota.app map's
// own park markers (confirmed against the live site), not this app's
// earlier custom pin shape. Fixed pixel dimensions throughout (SVG
// width/height/viewBox and Leaflet's iconSize are all plain numbers, not
// percentages or viewport units), so this renders at the same physical size
// on any screen/DPI - Leaflet divIcon markers also don't scale with map zoom.
function makeCircleIcon(color, showCheck) {
  var checkMark = showCheck
    ? '<path d=""M4 7.3l2 2 4.3-5"" fill=""none"" stroke=""white"" stroke-width=""1.8"" stroke-linecap=""round"" stroke-linejoin=""round""/>'
    : '';
  var svg =
    '<svg width=""14"" height=""14"" viewBox=""0 0 14 14"" xmlns=""http://www.w3.org/2000/svg"">' +
    '<circle cx=""7"" cy=""7"" r=""6"" fill=""' + color + '"" stroke=""#333333"" stroke-width=""1.2""/>' +
    checkMark +
    '</svg>';
  return L.divIcon({
    html: svg,
    className: '',
    iconSize: [14, 14],
    iconAnchor: [7, 7],
    popupAnchor: [0, -7]
  });
}

var yellowIcon = makeCircleIcon('#FFD500', false);
var orangeIcon = makeCircleIcon('#FF6700', false);
var greenIcon = makeCircleIcon('#2E8B22', true);

function buildPopupHtml(p) {
  var link = 'https://pota.app/#/park/' + encodeURIComponent(p.reference);
  var html = '<div class=""pota-popup"" style=""min-width:220px;"">';
  html += '<div style=""margin-bottom:4px;""><a href=""' + link + '"" target=""_blank"" rel=""noopener"">' +
          escapeHtml(p.reference) + ' - ' + escapeHtml(p.name) + '</a></div>';

  if (p.elevationFeet !== null && p.elevationFeet !== undefined) {
    html += '<div>Elevation: ' + Math.round(p.elevationFeet).toLocaleString() + ' ft</div>';
  }

  if (p.county) {
    html += '<div>County: ' + escapeHtml(p.county) + '</div>';
  }

  if (p.kff) {
    html += '<div>KFF: ' + escapeHtml(p.kff) + '</div>';
  }

  if (p.boatAccessOnly) {
    html += '<div class=""boat-line"">Boat access only</div>';
  }

  if (p.communityCount > 0) {
    var plural = p.communityCount === 1 ? 'time' : 'times';
    html += '<div>Activated ' + p.communityCount + ' ' + plural + ', most recently by ' +
            escapeHtml(p.communityCallsign) + ' on ' + escapeHtml(p.communityDate) + '.</div>';
  } else {
    html += '<div>No activations found on record.</div>';
  }

  if (p.myCount > 0) {
    var myPlural = p.myCount === 1 ? 'time' : 'times';
    html += '<div class=""my-line"">You have activated ' + p.myCount + ' ' + myPlural +
            ', most recently on ' + escapeHtml(p.myDate) + '.</div>';
  }

  html += '</div>';
  return html;
}

// A brown mountain-peak glyph (with a snow-cap highlight) - one fixed icon
// for every summit, not color-coded by activation status.
function makeSotaIcon() {
  var svg =
    '<svg width=""22"" height=""22"" viewBox=""0 0 22 22"" xmlns=""http://www.w3.org/2000/svg"">' +
    '<path d=""M11 2 L20 19 L2 19 Z"" fill=""#8B4513"" stroke=""#3a2312"" stroke-width=""1.3"" stroke-linejoin=""round""/>' +
    '<path d=""M11 2 L14.5 9 L7.5 9 Z"" fill=""#ffffff"" opacity=""0.85""/>' +
    '</svg>';
  return L.divIcon({
    html: svg,
    className: '',
    iconSize: [22, 22],
    iconAnchor: [11, 19],
    popupAnchor: [0, -17]
  });
}

var sotaIcon = makeSotaIcon();

// No outbound link here (unlike buildPopupHtml's pota.app one) - SOTA doesn't
// publish a simple per-summit URL pattern the way POTA does, so this only
// shows the summit's own published data.
function buildSotaPopupHtml(s) {
  var html = '<div class=""pota-popup"" style=""min-width:200px;"">';
  html += '<div style=""margin-bottom:4px;font-weight:bold;"">' + escapeHtml(s.reference) + ' - ' + escapeHtml(s.name) + '</div>';
  html += '<div>Elevation: ' + Math.round(s.altFeet).toLocaleString() + ' ft</div>';
  html += '<div>Points: ' + s.points + '</div>';

  if (s.activationCount > 0) {
    var plural = s.activationCount === 1 ? 'time' : 'times';
    html += '<div>Activated ' + s.activationCount + ' ' + plural;
    if (s.activationCall) html += ', most recently by ' + escapeHtml(s.activationCall);
    if (s.activationDate) html += ' on ' + escapeHtml(s.activationDate);
    html += '.</div>';
  } else {
    html += '<div>No activations found on record.</div>';
  }

  html += '</div>';
  return html;
}

// Trails are always purple, EC (Erie Canalway) stays the red it landed on
// under the old per-index palette, and every other area/park layer (PAD-US
// and friends) matches the blue already used for park reference links in
// popups (.pota-popup a) elsewhere on this page.
function getLayerColor(layer) {
  if (layer.isLine) return '#8e24aa';
  if (layer.name.toLowerCase() === 'ec') return '#e6194b';
  return '#1a5fb4';
}

// GeoJSON/BoundaryFeature order is [lon, lat] - Leaflet wants [lat, lng].
function pointToLatLng(pt) { return [pt[1], pt[0]]; }

function buildBoundaryPopupHtml(featureName, layerName) {
  return '<div class=""pota-popup"">' + escapeHtml(featureName) +
    '<div class=""boundary-popup-layer"">' + escapeHtml(layerName) + '</div></div>';
}

function makeSwatch(color, isLine) {
  var swatch = document.createElement('span');
  swatch.className = 'layer-swatch';
  swatch.style.background = color;
  swatch.style.borderRadius = isLine ? '0' : '2px';
  return swatch;
}

// One flat checkbox row - used for every area layer, and for each trail
// once its group is expanded. Built with DOM calls (not an HTML string)
// so a park/boundary name never needs manual escaping here.
function buildLeafRow(entry, map) {
  var row = document.createElement('label');
  row.className = 'layer-tree-row';

  var checkbox = document.createElement('input');
  checkbox.type = 'checkbox';
  checkbox.addEventListener('change', function () {
    if (checkbox.checked) map.addLayer(entry.group); else map.removeLayer(entry.group);
    if (entry.onChange) entry.onChange();
  });
  entry.checkbox = checkbox;

  row.appendChild(checkbox);
  row.appendChild(makeSwatch(entry.color, entry.isLine));
  row.appendChild(document.createTextNode(' ' + entry.name + ' '));

  // A trail entry is always exactly one route, so its own count is always
  // ""(1)"" - not informative the way an area layer's unit count is, so it's
  // left off leaf rows for trails (the group header shows the count that
  // actually means something: how many trails there are).
  if (!entry.isLine) {
    var count = document.createElement('span');
    count.className = 'layer-count';
    count.textContent = '(' + entry.count + ')';
    row.appendChild(count);
  }

  return row;
}

// The ""Trails"" node: a header (expand arrow + a select-all checkbox showing
// checked/unchecked/indeterminate depending on its children) plus a
// collapsible list of the individual trails. The arrow sits outside the
// header's <label> specifically so clicking it only expands/collapses -
// were it inside the label, a native click-to-toggle-the-checkbox would
// fire too.
function buildTrailGroup(trailEntries, map) {
  var wrapper = document.createElement('div');
  wrapper.className = 'layer-tree-group';

  var header = document.createElement('div');
  header.className = 'layer-tree-row layer-tree-group-header';

  var toggle = document.createElement('span');
  toggle.className = 'layer-tree-toggle';
  toggle.textContent = '▶';

  var innerLabel = document.createElement('label');
  innerLabel.className = 'layer-tree-row';

  var groupCheckbox = document.createElement('input');
  groupCheckbox.type = 'checkbox';

  var groupName = document.createElement('span');
  groupName.textContent = ' Trails ';

  var groupCount = document.createElement('span');
  groupCount.className = 'layer-count';
  groupCount.textContent = '(' + trailEntries.length + ')';

  innerLabel.appendChild(groupCheckbox);
  innerLabel.appendChild(makeSwatch('#8e24aa', true));
  innerLabel.appendChild(groupName);
  innerLabel.appendChild(groupCount);
  header.appendChild(toggle);
  header.appendChild(innerLabel);

  var children = document.createElement('div');
  children.className = 'layer-tree-children';
  children.hidden = true;

  function updateGroupCheckboxState() {
    var checkedCount = trailEntries.filter(function (t) { return t.checkbox.checked; }).length;
    groupCheckbox.checked = checkedCount > 0;
    groupCheckbox.indeterminate = checkedCount > 0 && checkedCount < trailEntries.length;
  }

  trailEntries.forEach(function (entry) {
    entry.onChange = updateGroupCheckboxState;
    children.appendChild(buildLeafRow(entry, map));
  });

  // Ticking the group box sets every trail to the same new state - the
  // standard ""select all"" pattern. Programmatically setting .checked
  // doesn't fire 'change' on its own, so each affected child's is
  // dispatched by hand to actually add/remove its layer.
  groupCheckbox.addEventListener('change', function () {
    var newState = groupCheckbox.checked;
    groupCheckbox.indeterminate = false;
    trailEntries.forEach(function (entry) {
      if (entry.checkbox.checked !== newState) {
        entry.checkbox.checked = newState;
        entry.checkbox.dispatchEvent(new Event('change'));
      }
    });
  });

  toggle.addEventListener('click', function () {
    children.hidden = !children.hidden;
    toggle.textContent = children.hidden ? '▶' : '▼';
  });

  wrapper.appendChild(header);
  wrapper.appendChild(children);
  return wrapper;
}

// Builds every layer's Leaflet objects (area boundaries as filled
// multi-polygons, trails as multi-line routes - see
// MapBoundaryLayerDto/MapGeoFeatureDto), then fills in the sidebar's own
// boundary section: area layers as flat checkboxes, trails grouped under
// one collapsible ""Trails"" node whose own checkbox turns all of them
// on/off together, expandable for individual control. Everything starts
// unchecked/off, same as potamap.us's own default, so a state's full
// boundary data can be embedded without slowing down or cluttering the
// initial view - nothing is drawn until its box (or the group box) is
// checked.
function addBoundaryLayers(map) {
  var areaEntries = [];
  var trailEntries = [];

  boundaryLayers.forEach(function (layer) {
    var color = getLayerColor(layer);
    var group = L.layerGroup();

    layer.features.forEach(function (feature) {
      var popupHtml = buildBoundaryPopupHtml(feature.name, layer.name);
      var shape;

      if (layer.isLine) {
        // Multi-line: one array entry per disconnected segment. Bold: thick,
        // fully-opaque stroke so a trail reads clearly against the tiles.
        var lines = feature.geometry.map(function (part) { return part[0].map(pointToLatLng); });
        shape = L.polyline(lines, { color: color, weight: 5, opacity: 1 });
      } else {
        // Multi-polygon-with-holes: part[0] is a piece's outer ring, any
        // further rings in that part are holes cut out of it.
        var parts = feature.geometry.map(function (part) {
          return part.map(function (ring) { return ring.map(pointToLatLng); });
        });
        shape = L.polygon(parts, { color: color, weight: 1.5, fillColor: color, fillOpacity: 0.18 });
      }

      shape.bindPopup(popupHtml).addTo(group);
    });

    var entry = { name: layer.name, color: color, isLine: layer.isLine, group: group, count: layer.features.length };
    (layer.isLine ? trailEntries : areaEntries).push(entry);
  });

  // Left hidden (see its markup) when there's nothing to show - a state
  // with no matched boundary/trail data shouldn't leave an empty,
  // pointlessly-bordered section sitting in the sidebar.
  if (areaEntries.length === 0 && trailEntries.length === 0) return;

  var section = document.getElementById('boundaryLayerSection');
  section.hidden = false;

  var heading = document.createElement('div');
  heading.className = 'sidebar-heading';
  heading.textContent = 'Park Boundaries';
  section.appendChild(heading);

  areaEntries.forEach(function (entry) { section.appendChild(buildLeafRow(entry, map)); });
  if (trailEntries.length > 0) section.appendChild(buildTrailGroup(trailEntries, map));
}

var map = L.map('map');

var streetLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a> contributors'
}).addTo(map);

// Esri's World Imagery - free, no API key/account needed, same as the
// OpenStreetMap tiles above.
var satelliteLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
  maxZoom: 19,
  attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community'
});

var workedLayer = L.layerGroup().addTo(map);
var notWorkedLayer = L.layerGroup().addTo(map);
// Not added to the map here (unlike workedLayer/notWorkedLayer above) - a
// state can have many summits, so this starts unchecked/off, same convention
// as the boundary/trail layers below (addBoundaryLayers).
var sotaLayer = L.layerGroup();

// Base map + overlay toggles, and (below) the sidebar show/hide button -
// plain HTML in the #sidebar panel instead of Leaflet's own
// L.control.layers, so it can live alongside the legend and the boundary
// layer checkboxes (addBoundaryLayers below) in one collapsible place
// rather than as several separate floating boxes around the map.
function wireBaseLayerRadio(id, layerToShow, layerToHide) {
  document.getElementById(id).addEventListener('change', function (e) {
    if (!e.target.checked) return;
    map.removeLayer(layerToHide);
    map.addLayer(layerToShow);
  });
}
wireBaseLayerRadio('baseLayerStreet', streetLayer, satelliteLayer);
wireBaseLayerRadio('baseLayerSatellite', satelliteLayer, streetLayer);

function wireOverlayCheckbox(id, layer) {
  document.getElementById(id).addEventListener('change', function (e) {
    if (e.target.checked) map.addLayer(layer); else map.removeLayer(layer);
  });
}
wireOverlayCheckbox('overlayWorked', workedLayer);
wireOverlayCheckbox('overlayNotWorked', notWorkedLayer);
wireOverlayCheckbox('overlaySota', sotaLayer);

var sidebarEl = document.getElementById('sidebar');
document.getElementById('sidebarToggle').addEventListener('click', function () {
  sidebarEl.hidden = !sidebarEl.hidden;
});
document.getElementById('sidebarClose').addEventListener('click', function () {
  sidebarEl.hidden = true;
});

addBoundaryLayers(map);

sotaData.forEach(function (s) {
  var marker = L.marker([s.lat, s.lon], { icon: sotaIcon });
  marker.bindPopup(buildSotaPopupHtml(s));
  marker.addTo(sotaLayer);
});

var bounds = [];
parkData.forEach(function (p) {
  // (0, 0) is what an ungeocoded park looks like here - it's out in the Gulf
  // of Guinea, nowhere near a real US park, so this only filters those out.
  if (!p.lat && !p.lon) return;
  var icon = p.completed ? greenIcon : (p.boatAccessOnly ? orangeIcon : yellowIcon);
  var marker = L.marker([p.lat, p.lon], { icon: icon });
  marker.bindPopup(buildPopupHtml(p));
  marker.addTo(p.completed ? workedLayer : notWorkedLayer);
  bounds.push([p.lat, p.lon]);
});

if (bounds.length > 0) {
  map.fitBounds(bounds, { padding: [40, 40] });
} else {
  map.setView([39.8, -98.6], 4); // fallback: center of the continental US
}

// ---- Right-click context menu: ""Measure distance"" (Google Maps-style
// click-to-extend polyline with a running total) and ""What's here?""
// (reverse geocode via Nominatim, the same free OpenStreetMap service the
// tile layers above already depend on - no API key). Both are purely
// best-effort, same philosophy as the GPS features below: ""What's here?""
// needs a network connection and just falls back to showing the raw
// coordinates if that fetch fails (e.g. no signal out in the field), and
// neither feature needs a secure context the way navigator.geolocation
// does, so both still work in a saved, standalone file opened later.
var contextMenuEl = document.getElementById('mapContextMenu');

function hideContextMenu() {
  contextMenuEl.hidden = true;
}

function addContextMenuItem(label, onClick) {
  var item = document.createElement('div');
  item.className = 'context-menu-item';
  item.textContent = label;
  item.addEventListener('click', function () {
    hideContextMenu();
    onClick();
  });
  contextMenuEl.appendChild(item);
}

function showContextMenu(latlng, clientX, clientY) {
  contextMenuEl.innerHTML = '';
  addContextMenuItem('Measure distance', function () { startMeasuring(latlng); });
  addContextMenuItem(""What's here?"", function () { showWhatsHere(latlng); });
  if (measurePoints.length > 0) {
    addContextMenuItem('Clear measurement', clearMeasuring);
  }

  // Positioned at the click, then clamped so it can't run off the
  // right/bottom edge on a narrow window - has to be shown (off-screen)
  // first to get its real offsetWidth/Height to clamp against.
  contextMenuEl.style.left = '-1000px';
  contextMenuEl.style.top = '0';
  contextMenuEl.hidden = false;
  var menuWidth = contextMenuEl.offsetWidth;
  var menuHeight = contextMenuEl.offsetHeight;
  contextMenuEl.style.left = Math.max(0, Math.min(clientX, window.innerWidth - menuWidth - 4)) + 'px';
  contextMenuEl.style.top = Math.max(0, Math.min(clientY, window.innerHeight - menuHeight - 4)) + 'px';
}

map.on('contextmenu', function (e) {
  e.originalEvent.preventDefault();
  showContextMenu(e.latlng, e.originalEvent.clientX, e.originalEvent.clientY);
});

// A left click anywhere on the map background both dismisses an open
// context menu and, while measuring, places the next point - see
// startMeasuring/addMeasurePoint below. Clicks on markers/boundary shapes
// never reach here: Leaflet already stops those from bubbling up to the
// map's own click handler, which is what lets a park popup still open
// normally mid-measurement instead of also dropping a stray point on it.
map.on('click', function (e) {
  hideContextMenu();
  if (measureActive) addMeasurePoint(e.latlng);
});
map.on('movestart zoomstart', hideContextMenu);
document.addEventListener('keydown', function (e) {
  if (e.key === 'Escape') { hideContextMenu(); clearMeasuring(); }
});

function showWhatsHere(latlng) {
  var coordsLine = latlng.lat.toFixed(5) + ', ' + latlng.lng.toFixed(5);
  var popup = L.popup()
    .setLatLng(latlng)
    .setContent('<div class=""pota-popup"">Looking up address&hellip;</div>')
    .openOn(map);

  fetch('https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=' + latlng.lat + '&lon=' + latlng.lng + '&zoom=18&addressdetails=1')
    .then(function (resp) { if (!resp.ok) throw new Error('reverse geocode failed'); return resp.json(); })
    .then(function (data) {
      var address = data && data.display_name ? escapeHtml(data.display_name) : 'No address found for this location.';
      popup.setContent('<div class=""pota-popup"" style=""min-width:200px;"">' +
        '<div style=""margin-bottom:4px;"">' + address + '</div>' +
        '<div style=""opacity:0.7;font-size:11px;"">' + coordsLine + '</div></div>');
    })
    .catch(function () {
      popup.setContent('<div class=""pota-popup"" style=""min-width:200px;"">' +
        '<div>Address lookup unavailable (no signal?)</div>' +
        '<div style=""opacity:0.7;font-size:11px;"">' + coordsLine + '</div></div>');
    });
}

// ---- Measuring tool: click to drop points, with a running total shown
// both in the bottom panel and as a live tentative-distance tooltip that
// follows the cursor ahead of the next click (the ""rubber band"" line) -
// the same interaction Google Maps' own ""Measure distance"" tool uses.
// Distances use Leaflet's own LatLng.distanceTo (great-circle, via the
// Haversine formula) rather than the flat-degree approximation the
// park-boundary checks above use - those trade accuracy for speed over
// many repeated checks, but this tool only computes a handful of
// distances per click, so the more accurate spherical formula costs
// nothing here.
var measureActive = false;
var measurePoints = [];
var measureMarkers = [];
var measureLine = null;
var measureRubberBand = null;
var measureTotalMeters = 0;

var measureVertexIcon = L.divIcon({
  html: '<div style=""width:10px;height:10px;border-radius:50%;background:#ffffff;border:2px solid #1a73e8;box-shadow:0 0 2px rgba(0,0,0,0.6);""></div>',
  className: '',
  iconSize: [10, 10],
  iconAnchor: [5, 5]
});

var measurePanelEl = document.getElementById('measurePanel');
var measureDistanceTextEl = document.getElementById('measureDistanceText');

// Feet under half a mile, miles beyond that - mirrors Google Maps' own
// US-unit switchover rather than always showing one unit.
function formatDistance(meters) {
  var feet = meters * 3.28084;
  if (feet < 2640) return Math.round(feet).toLocaleString() + ' ft';
  return (feet / 5280).toFixed(2) + ' mi';
}

function recalcMeasureTotal() {
  measureTotalMeters = 0;
  for (var i = 1; i < measurePoints.length; i++) {
    measureTotalMeters += measurePoints[i - 1].distanceTo(measurePoints[i]);
  }
  measureDistanceTextEl.textContent = 'Distance: ' + formatDistance(measureTotalMeters);
}

function rebuildMeasureLine() {
  if (measureLine) { map.removeLayer(measureLine); measureLine = null; }
  if (measurePoints.length > 1) {
    measureLine = L.polyline(measurePoints, { color: '#1a73e8', weight: 3 }).addTo(map);
  }
}

function discardRubberBand() {
  if (measureRubberBand) { map.removeLayer(measureRubberBand); measureRubberBand = null; }
}

function removeMeasureVertexAt(index) {
  measurePoints.splice(index, 1);
  var marker = measureMarkers.splice(index, 1)[0];
  map.removeLayer(marker);
  rebuildMeasureLine();
  recalcMeasureTotal();
  discardRubberBand();
  if (measurePoints.length === 0) measurePanelEl.hidden = true;
}

// Clicking a placed point removes it (and reconnects its neighbors) -
// matches Google Maps' own measuring tool. stopPropagation keeps that
// click from also bubbling up to the map's own click handler, which would
// otherwise immediately re-add a point at the same spot.
function addMeasureVertexMarker(latlng) {
  var marker = L.marker(latlng, { icon: measureVertexIcon }).addTo(map);
  marker.on('click', function (e) {
    L.DomEvent.stopPropagation(e);
    var idx = measureMarkers.indexOf(marker);
    if (idx !== -1) removeMeasureVertexAt(idx);
  });
  measureMarkers.push(marker);
}

function startMeasuring(latlng) {
  clearMeasuring();
  measureActive = true;
  L.DomUtil.addClass(map.getContainer(), 'measuring-cursor');
  measurePoints.push(latlng);
  addMeasureVertexMarker(latlng);
  recalcMeasureTotal();
  measurePanelEl.hidden = false;
}

function addMeasurePoint(latlng) {
  measurePoints.push(latlng);
  addMeasureVertexMarker(latlng);
  rebuildMeasureLine();
  recalcMeasureTotal();
  discardRubberBand(); // next mousemove rebuilds it from the new last point
}

function clearMeasuring() {
  measureActive = false;
  L.DomUtil.removeClass(map.getContainer(), 'measuring-cursor');
  measurePoints = [];
  measureMarkers.forEach(function (m) { map.removeLayer(m); });
  measureMarkers = [];
  if (measureLine) { map.removeLayer(measureLine); measureLine = null; }
  discardRubberBand();
  measureTotalMeters = 0;
  measurePanelEl.hidden = true;
}

map.on('mousemove', function (e) {
  if (!measureActive || measurePoints.length === 0) return;
  var last = measurePoints[measurePoints.length - 1];
  var tentative = measureTotalMeters + last.distanceTo(e.latlng);
  if (!measureRubberBand) {
    measureRubberBand = L.polyline([last, e.latlng], { color: '#1a73e8', weight: 2, dashArray: '6,6' })
      .addTo(map)
      .bindTooltip('', { direction: 'right', offset: [12, 0] });
  }
  measureRubberBand.setLatLngs([last, e.latlng]);
  measureRubberBand.setTooltipContent(formatDistance(tentative));
  measureRubberBand.openTooltip(e.latlng);
});

document.getElementById('measureCloseBtn').addEventListener('click', clearMeasuring);

// Live ""you are here"" marker from the browser's Geolocation API, plus a
// bottom-right button that flies back to it on demand. Both purely
// best-effort - if there's no location hardware, the browser/OS location
// permission is denied, or this is opened in a context that doesn't allow
// it, locationerror just fires and the map works exactly as it did before,
// with no marker and a button that quietly does nothing.
var youMarker = null;
var youAccuracyCircle = null;
var youLocatedOnce = false;
var recenterButton = null;

// A saved, standalone copy of this file (Save Map, opened later via
// file://) can never get live location, no matter what this script does -
// browsers only allow navigator.geolocation from a secure context
// (https:, or the loopback exception the live map's own local server
// relies on - see Form1.cs's MapServerPort), and file:// doesn't qualify.
// Checked once up front so that case can show one clear explanation
// instead of every poll attempt silently failing with a cryptic browser
// permission error every 10 seconds for no benefit.
var geoAvailable = window.isSecureContext;

// Bottom-right readout showing whether the browser is actually receiving
// fresh position fixes and how far off they're expected to be - the ""you
// are here"" dot alone can't distinguish a genuinely stalled GPS from one
// that's just not moving because a real device isn't moving. This ticks
// once a second so a fix that stops arriving (e.g. Windows/the browser
// falls back to a static Wi-Fi-based location) shows growing as ""updated
// Xs ago"" instead of silently looking current forever.
var gpsStatusEl = document.getElementById('gpsStatus');
var lastFixTimestamp = null;
var lastFixAccuracy = null;

// Shown alongside the fix info so a permission problem (blocked, or never
// answered) is visible on sight instead of looking identical to ""just
// hasn't gotten a fix yet"" - the two look the same from lastFixTimestamp
// alone. Queried once up front and kept live via onchange, since Chrome
// remembers a per-site grant/block permanently once set (via the address
// bar's padlock, or a past prompt response) - unlike a fix, this can be
// checked immediately, with no location request needed at all.
var geoPermissionState = null;
if (navigator.permissions && navigator.permissions.query) {
  navigator.permissions.query({ name: 'geolocation' }).then(function (result) {
    geoPermissionState = result.state;
    updateGpsStatusText();
    result.onchange = function () {
      geoPermissionState = result.state;
      updateGpsStatusText();
    };
  }).catch(function () { /* Permissions API unsupported here - just omitted below. */ });
}

function updateGpsStatusText() {
  if (!gpsStatusEl) return;

  if (!geoAvailable) {
    gpsStatusEl.hidden = false;
    gpsStatusEl.textContent = 'GPS: not available in a saved file - use Show Map in the app for live tracking.';
    return;
  }

  var parts = [];
  if (geoPermissionState) parts.push('permission: ' + geoPermissionState);
  if (lastFixTimestamp !== null) {
    var seconds = Math.max(0, Math.round((Date.now() - lastFixTimestamp) / 1000));
    var ago = seconds < 60 ? (seconds + 's ago') : (Math.round(seconds / 60) + 'm ago');
    parts.push('±' + Math.round(lastFixAccuracy) + ' m, updated ' + ago);
  }
  if (parts.length === 0) return;

  gpsStatusEl.hidden = false;
  gpsStatusEl.textContent = 'GPS: ' + parts.join(', ');
}
setInterval(updateGpsStatusText, 1000);

// Set when the button is clicked before any fix has arrived yet - resolved
// (flown to) by the next onLocationFound instead of requesting a second,
// one-off fix that would race the poll running below.
var pendingRecenter = false;

// ""Follow me"" mode: once you've pressed the recenter button, later fixes
// keep the map centered on the dot automatically (like a normal nav app),
// right up until you drag or zoom the map yourself - at which point it
// backs off instead of constantly fighting you while you're looking
// around, matching the same ""don't yank the view around"" philosophy
// onLocationFound already applies elsewhere. programmaticMove distinguishes
// ""the view moved because this file just called flyTo/panTo/fitBounds""
// from ""the view moved because you dragged or zoomed it"" - without it,
// recenterOnMe's own flyTo (which can include a zoom change, to at least
// zoom level 14) would immediately flip followMode back off the moment it
// started, since Leaflet fires the same zoomstart event either way.
var followMode = false;
var programmaticMove = false;
map.on('moveend', function () { programmaticMove = false; });
map.on('dragstart zoomstart', function () {
  if (!programmaticMove) followMode = false;
});

function recenterOnMe() {
  if (!geoAvailable) {
    updateGpsStatusText(); // surfaces the ""not available in a saved file"" explanation right away
    return;
  }
  followMode = true;
  if (youMarker) {
    programmaticMove = true;
    map.flyTo(youMarker.getLatLng(), Math.max(map.getZoom(), 14));
  } else {
    pendingRecenter = true;
    recenterButton.classList.add('waiting');
    recenterButton.title = 'Waiting for your location...';
  }
}

var RecenterControl = L.Control.extend({
  options: { position: 'bottomright' },
  onAdd: function () {
    var container = L.DomUtil.create('div', 'leaflet-bar recenter-control');
    L.DomEvent.disableClickPropagation(container);

    var button = L.DomUtil.create('a', '', container);
    button.href = '#';
    button.title = 'Center on my location';
    button.innerHTML =
      '<svg width=""18"" height=""18"" viewBox=""0 0 18 18"" xmlns=""http://www.w3.org/2000/svg"">' +
      '<circle cx=""9"" cy=""9"" r=""2.5"" fill=""currentColor""/>' +
      '<path d=""M9 1v3M9 14v3M1 9h3M14 9h3"" stroke=""currentColor"" stroke-width=""1.6"" stroke-linecap=""round""/>' +
      '</svg>';

    L.DomEvent.on(button, 'click', function (e) {
      L.DomEvent.preventDefault(e);
      recenterOnMe();
    });

    recenterButton = button;
    return container;
  }
});

map.addControl(new RecenterControl());

// ---- Live ""am I in/near a park right now"" check, per POTA's own
// activation rules - not the same question the Xfer's grid column
// answers (whether a PARK'S OWN reported point overlaps another park's
// boundary); this is about where YOU actually are right now, tested
// against the real boundary/trail geometry already embedded above
// (boundaryLayers) - exactly what POTA requires an activator to verify
// before claiming an overlap or a trail activation, rather than trusting
// a park's single reported coordinate. Runs on every GPS fix regardless
// of which sidebar layer checkboxes are currently ticked - this is about
// accuracy, not what's currently drawn.
var infoBarEl = document.getElementById('infoBar');
var TrailCheckToleranceKm = 0.03048; // 100 ft (30.5 m) - POTA's own trail-activation rule, same constant FerLookupService.TrailToleranceKm uses server-side.
var KmPerDegreeLatCheck = 111.32;

// Same ray-casting parity test as FerLookupService.PointInRing/CountyLookupService.PointInRing.
function pointInRingCheck(lon, lat, ring) {
  var inside = false;
  for (var i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    var xi = ring[i][0], yi = ring[i][1];
    var xj = ring[j][0], yj = ring[j][1];
    if (((yi > lat) !== (yj > lat)) && (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi)) inside = !inside;
  }
  return inside;
}

function isPointInAreaFeature(lon, lat, feature) {
  if (lon < feature.minLon || lon > feature.maxLon || lat < feature.minLat || lat > feature.maxLat) return false;
  for (var p = 0; p < feature.geometry.length; p++) {
    var ringsContaining = 0;
    var part = feature.geometry[p];
    for (var r = 0; r < part.length; r++) {
      if (pointInRingCheck(lon, lat, part[r])) ringsContaining++;
    }
    if (ringsContaining % 2 === 1) return true;
  }
  return false;
}

// Same locally-scaled-degree distance math as FerLookupService.PointToSegmentDistanceKm.
function distancePointToSegmentKm(px, py, ax, ay, bx, by, lonScale) {
  var axs = ax * lonScale, bxs = bx * lonScale, pxs = px * lonScale;
  var dx = bxs - axs, dy = by - ay;
  var t = (dx === 0 && dy === 0) ? 0 : Math.max(0, Math.min(1, ((pxs - axs) * dx + (py - ay) * dy) / (dx * dx + dy * dy)));
  var cx = axs + t * dx, cy = ay + t * dy;
  var ex = pxs - cx, ey = py - cy;
  return Math.sqrt(ex * ex + ey * ey) * KmPerDegreeLatCheck;
}

function isPointNearTrailFeature(lon, lat, feature) {
  var latPad = TrailCheckToleranceKm / KmPerDegreeLatCheck;
  var lonScale = Math.max(0.1, Math.cos(lat * Math.PI / 180));
  var lonPad = TrailCheckToleranceKm / (KmPerDegreeLatCheck * lonScale);
  if (lon < feature.minLon - lonPad || lon > feature.maxLon + lonPad ||
      lat < feature.minLat - latPad || lat > feature.maxLat + latPad) return false;

  for (var p = 0; p < feature.geometry.length; p++) {
    var line = feature.geometry[p][0]; // a trail feature's ""ring"" is really just its own point sequence - see MapGeoFeatureDto.
    for (var i = 0; i < line.length - 1; i++) {
      var d = distancePointToSegmentKm(lon, lat, line[i][0], line[i][1], line[i + 1][0], line[i + 1][1], lonScale);
      if (d <= TrailCheckToleranceKm) return true;
    }
  }
  return false;
}

function normalizeNameWords(name) {
  if (!name) return [];
  var collapsed = name.toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
  return collapsed.length === 0 ? [] : collapsed.split(' ').sort();
}

// Best-effort cross-reference from a boundary/trail feature's own name to
// its POTA reference, for display only - same word-set-equality idea as
// FerLookupService.NormalizeName server-side (minus its safe-subset
// fallback, which isn't worth the false-positive risk here: showing the
// raw boundary/trail name on a near-miss is still useful, unlike
// server-side where a wrong Xfer match matters more). Returns null - and
// callers fall back to the raw feature name - when nothing matches, or
// more than one park name matches equally (ambiguous).
function findParkByFeatureName(featureName) {
  var target = normalizeNameWords(featureName).join(' ');
  if (!target) return null;
  var match = null;
  for (var i = 0; i < parkData.length; i++) {
    if (normalizeNameWords(parkData[i].name).join(' ') === target) {
      if (match) return null;
      match = parkData[i];
    }
  }
  return match;
}

function formatFeatureLabel(featureName) {
  var park = findParkByFeatureName(featureName);
  return park ? (park.reference + ' - ' + featureName) : featureName;
}

function checkNearbyParks(lat, lon) {
  if (!infoBarEl) return;

  var insideNames = {};
  var nearTrailNames = {};
  boundaryLayers.forEach(function (layer) {
    layer.features.forEach(function (feature) {
      if (layer.isLine) {
        if (isPointNearTrailFeature(lon, lat, feature)) nearTrailNames[feature.name] = true;
      } else if (isPointInAreaFeature(lon, lat, feature)) {
        insideNames[feature.name] = true;
      }
    });
  });

  var lines = [];
  Object.keys(insideNames).forEach(function (n) { lines.push('Inside: ' + formatFeatureLabel(n)); });
  Object.keys(nearTrailNames).forEach(function (n) { lines.push('Within 100 ft of: ' + formatFeatureLabel(n)); });

  infoBarEl.hidden = false;
  if (lines.length === 0) {
    infoBarEl.classList.remove('info-bar-active');
    infoBarEl.textContent = 'Not currently within any known park boundary or trail.';
  } else {
    infoBarEl.classList.add('info-bar-active');
    infoBarEl.innerHTML = lines.map(escapeHtml).join('<br>');
  }
}

function onLocationFound(e) {
  clearLocatePending();
  lastFixTimestamp = e.timestamp || Date.now();
  lastFixAccuracy = e.accuracy;
  updateGpsStatusText();
  checkNearbyParks(e.latlng.lat, e.latlng.lng);

  var popupHtml = 'Your location (±' + Math.round(e.accuracy) + ' m)';

  if (!youMarker) {
    youMarker = L.circleMarker(e.latlng, {
      radius: 8, weight: 3, color: '#ffffff', opacity: 1,
      fillColor: '#1a73e8', fillOpacity: 1
    }).addTo(map).bindPopup(popupHtml);
    youAccuracyCircle = L.circle(e.latlng, {
      radius: e.accuracy, weight: 1, color: '#1a73e8', fillColor: '#1a73e8', fillOpacity: 0.1
    }).addTo(map);
  } else {
    youMarker.setLatLng(e.latlng).setPopupContent(popupHtml);
    youAccuracyCircle.setLatLng(e.latlng).setRadius(e.accuracy);
  }

  if (pendingRecenter) {
    pendingRecenter = false;
    recenterButton.classList.remove('waiting');
    recenterButton.title = 'Center on my location';
    programmaticMove = true;
    map.flyTo(e.latlng, Math.max(map.getZoom(), 14));
  } else if (followMode) {
    // Keep the dot centered on every later fix too, not just the one right
    // after pressing the button - panTo (not flyTo) so this never touches
    // zoom, only position: a real user zoom is still the only thing that
    // should change zoom, and panTo alone never fires zoomstart, so it
    // can't spuriously trip the followMode-cancelling listener above.
    programmaticMove = true;
    map.panTo(e.latlng);
  }

  // Only nudge the view on the very first fix, and only if it's not already
  // visible - e.g. the parks loaded are for a state you're not currently
  // standing in. After that, leave the view alone (beyond followMode above)
  // so a later GPS update never yanks the map out from under you while
  // you're panning or zooming it.
  if (!youLocatedOnce) {
    youLocatedOnce = true;
    if (!map.getBounds().contains(e.latlng)) {
      programmaticMove = true;
      map.fitBounds(map.getBounds().extend(e.latlng), { padding: [40, 40] });
    }
  }
}

function onLocationError(e) {
  clearLocatePending();
  // Not worth interrupting the user with a popup about, but shown in the
  // gpsStatus readout (see its declaration above) so a permission-denied /
  // unavailable / timed-out failure is visible instead of silently looking
  // like the map is just waiting on a first fix forever. A pending button
  // click also shouldn't be left waiting forever on a fix that isn't
  // coming, so it's cleared here too.
  if (gpsStatusEl) {
    gpsStatusEl.hidden = false;
    gpsStatusEl.textContent = 'GPS: ' + (e && e.message ? e.message : 'location unavailable');
  }
  if (recenterButton) {
    recenterButton.classList.remove('waiting');
    recenterButton.title = 'Center on my location';
  }
  pendingRecenter = false;
}

// Everything below actually calls navigator.geolocation - skipped
// entirely when geoAvailable is false (see its declaration above) so a
// saved file doesn't spend forever retrying a request the browser will
// never allow to succeed; updateGpsStatusText's own geoAvailable check
// already keeps the readout showing a clear explanation the whole time.
if (geoAvailable) {

map.on('locationfound', onLocationFound);
map.on('locationerror', onLocationError);

// Deliberately NOT also running map.locate({watch:true}) (a persistent
// navigator.geolocation.watchPosition subscription) alongside the poll
// below - confirmed directly, by comparing this page's own repeated
// one-shot requests against the same test run on a page with no
// competing geolocation activity at all: this origin, with only the poll
// below active, resolved fine, while adding a concurrent watchPosition
// subscription (as this app used to run alongside the poll) made
// requests here take dramatically longer than the very same code on a
// page with nothing else calling the geolocation API. Whatever the exact
// mechanism, running two overlapping geolocation subscriptions on one
// page was the actual source of the stalls, not the poll's cadence or
// this origin itself. A single one-shot request every tick - never a
// second, independent subscription running at the same time - keeps
// exactly one request outstanding, period, which is what actually
// resolved reliably.
//
// locatePending guards against firing a new request while one's still
// outstanding - confirmed necessary, not just theoretical: at a tight 1s
// tick with no guard, a real fix here takes longer than that to resolve
// (this OS/browser combination is evidently going through a several-second
// Wi-Fi-based lookup, not an instant GPS read), so a new locate() request
// was starting on top of the previous unfinished one every tick, and they
// started timing each other out (""GPS: Geolocation error: Timeout
// expired"") instead of resolving. Checking on every tick but only
// actually starting a request when the last one has finished means this
// still can't pile up requests even at a much shorter interval than the
// 10s below - it's a correctness guard, not just a battery-saving one.
//
// locatePendingTimeoutId is a second, independent safety net on top of
// that: confirmed directly (via the page's own live state, not just
// theory) that a locate() call here can occasionally call back neither
// onLocationFound nor onLocationError at all - it just never resolves -
// even with an explicit timeout passed below. Without this, that single
// hung request would leave locatePending stuck true forever, permanently
// blocking every future tick and silently stopping the readout from ever
// updating again. Whichever handler does eventually fire clears this
// timer; if neither ever does, it fires on its own and forces the next
// tick to try again anyway - a hung request degrades to ""one skipped
// update"", never ""polling stops for good"".
var locatePending = false;
var locatePendingTimeoutId = null;

function clearLocatePending() {
  locatePending = false;
  if (locatePendingTimeoutId !== null) {
    clearTimeout(locatePendingTimeoutId);
    locatePendingTimeoutId = null;
  }
}

setInterval(function () {
  if (locatePending) return;
  locatePending = true;
  locatePendingTimeoutId = setTimeout(clearLocatePending, 15000);
  map.locate({ setView: false, enableHighAccuracy: true, maximumAge: 0, timeout: 10000 });
}, 10000);

} // if (geoAvailable)
</script>
</body>
</html>";
    }
}
