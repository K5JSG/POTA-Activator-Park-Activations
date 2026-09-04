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
        // False = you have not activated it yet (yellow pin).
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

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
    }

    public static class MapService
    {
        // Builds one complete, self-contained HTML file (map + all the data + all the
        // JavaScript needed to draw it) as a single string. This file can be opened by
        // any web browser - no server, no install, no account, no API key. It uses
        // Leaflet (a free open-source mapping library) and OpenStreetMap map tiles
        // (also free), both loaded from their public content-delivery networks.
        public static string BuildMapHtml(List<MapParkDto> parks, List<MapBoundaryLayerDto>? boundaryLayers = null)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            string parkJson = JsonSerializer.Serialize(parks, jsonOptions);
            string boundaryJson = JsonSerializer.Serialize(boundaryLayers ?? new List<MapBoundaryLayerDto>(), jsonOptions);

            // Guard against a park/boundary name that happens to contain
            // "</script>" - that would otherwise break out of our embedded
            // <script> block.
            parkJson = parkJson.Replace("</", "<\\/");
            boundaryJson = boundaryJson.Replace("</", "<\\/");

            string html = HtmlTemplate
                .Replace("__PARK_DATA__", parkJson)
                .Replace("__BOUNDARY_DATA__", boundaryJson);
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
  .legend {
    position: absolute; bottom: 24px; left: 12px; z-index: 1000;
    background: white; padding: 10px 14px; border-radius: 6px;
    box-shadow: 0 1px 5px rgba(0,0,0,0.4); font-size: 13px; line-height: 1.6;
  }
  .legend-swatch {
    display: inline-block; width: 12px; height: 12px; border-radius: 50%;
    margin-right: 6px; border: 1px solid #333; vertical-align: middle;
  }
  .pota-popup a { color: #1a5fb4; text-decoration: none; font-weight: bold; }
  .pota-popup a:hover { text-decoration: underline; }
  .pota-popup .my-line { margin-top: 6px; color: #2e8b22; font-weight: bold; }
  .boundary-popup-layer { margin-top: 2px; font-size: 11px; opacity: 0.7; }
  .leaflet-control-layers { font-size: 13px; }
  .leaflet-control-layers-overlays { max-height: 55vh; overflow-y: auto; }
  .layer-swatch {
    display: inline-block; width: 11px; height: 11px;
    margin-right: 6px; vertical-align: middle; border: 1px solid rgba(0,0,0,0.35);
  }
  .layer-count { opacity: 0.6; }
  .layer-tree-control { padding: 6px 10px; }
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
  @media (prefers-color-scheme: dark) {
    body { background: #1e1e1e; }
    .legend {
      background: #2d2d30; color: #e8e8e8;
      box-shadow: 0 1px 5px rgba(0,0,0,0.6);
    }
    .legend-swatch { border-color: #999; }
    .pota-popup a { color: #6ab0f3; }
    .pota-popup .my-line { color: #6fdc6f; }
    .leaflet-control-layers {
      background: #2d2d30; color: #e8e8e8;
      box-shadow: 0 1px 5px rgba(0,0,0,0.6);
    }
    .leaflet-control-layers-separator { border-color: #555; }
    .layer-swatch { border-color: rgba(255,255,255,0.4); }
  }
</style>
</head>
<body>
<div id=""map""></div>
<div class=""legend"">
  <div><span class=""legend-swatch"" style=""background:#FFD500;""></span>Not yet activated by me</div>
  <div><span class=""legend-swatch"" style=""background:#2E8B22;""></span>Activated by me</div>
</div>

<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
<script>
var parkData = __PARK_DATA__;
var boundaryLayers = __BOUNDARY_DATA__;

function escapeHtml(text) {
  if (!text) return '';
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/""/g, '&quot;');
}

function makePinIcon(color, showCheck) {
  var checkMark = showCheck
    ? '<path d=""M8 15.5l4.2 4.2L22 9.5"" fill=""none"" stroke=""white"" stroke-width=""3"" stroke-linecap=""round"" stroke-linejoin=""round""/>'
    : '';
  var svg =
    '<svg width=""28"" height=""40"" viewBox=""0 0 30 42"" xmlns=""http://www.w3.org/2000/svg"">' +
    '<path d=""M15 0C6.7 0 0 6.7 0 15c0 11 15 27 15 27s15-16 15-27C30 6.7 23.3 0 15 0z"" ' +
    'fill=""' + color + '"" stroke=""#333333"" stroke-width=""1.5""/>' +
    checkMark +
    '</svg>';
  return L.divIcon({
    html: svg,
    className: '',
    iconSize: [28, 40],
    iconAnchor: [14, 40],
    popupAnchor: [0, -36]
  });
}

var yellowIcon = makePinIcon('#FFD500', false);
var greenIcon = makePinIcon('#2E8B22', true);

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
// MapBoundaryLayerDto/MapGeoFeatureDto), then adds a custom control: area
// layers as flat checkboxes, trails grouped under one collapsible ""Trails""
// node whose own checkbox turns all of them on/off together, expandable
// for individual control. Everything starts unchecked/off, same as
// potamap.us's own default, so a state's full boundary data can be
// embedded without slowing down or cluttering the initial view - nothing
// is drawn until its box (or the group box) is checked.
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

  if (areaEntries.length === 0 && trailEntries.length === 0) return;

  var TreeControl = L.Control.extend({
    options: { position: 'topright' },
    onAdd: function () {
      var container = L.DomUtil.create('div', 'leaflet-control-layers layer-tree-control');
      L.DomEvent.disableClickPropagation(container);
      L.DomEvent.disableScrollPropagation(container);

      var list = L.DomUtil.create('div', 'leaflet-control-layers-overlays', container);
      areaEntries.forEach(function (entry) { list.appendChild(buildLeafRow(entry, map)); });
      if (trailEntries.length > 0) list.appendChild(buildTrailGroup(trailEntries, map));

      return container;
    }
  });

  map.addControl(new TreeControl());
}

var map = L.map('map');

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a> contributors'
}).addTo(map);

addBoundaryLayers(map);

var bounds = [];
parkData.forEach(function (p) {
  // (0, 0) is what an ungeocoded park looks like here - it's out in the Gulf
  // of Guinea, nowhere near a real US park, so this only filters those out.
  if (!p.lat && !p.lon) return;
  var icon = p.completed ? greenIcon : yellowIcon;
  var marker = L.marker([p.lat, p.lon], { icon: icon });
  marker.bindPopup(buildPopupHtml(p));
  marker.addTo(map);
  bounds.push([p.lat, p.lon]);
});

if (bounds.length > 0) {
  map.fitBounds(bounds, { padding: [40, 40] });
} else {
  map.setView([39.8, -98.6], 4); // fallback: center of the continental US
}
</script>
</body>
</html>";
    }
}
