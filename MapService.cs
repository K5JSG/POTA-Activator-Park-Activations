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

    public static class MapService
    {
        // Builds one complete, self-contained HTML file (map + all the data + all the
        // JavaScript needed to draw it) as a single string. This file can be opened by
        // any web browser - no server, no install, no account, no API key. It uses
        // Leaflet (a free open-source mapping library) and OpenStreetMap map tiles
        // (also free), both loaded from their public content-delivery networks.
        public static string BuildMapHtml(List<MapParkDto> parks)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            string parkJson = JsonSerializer.Serialize(parks, jsonOptions);

            // Guard against a park name/county that happens to contain "</script>" -
            // that would otherwise break out of our embedded <script> block.
            parkJson = parkJson.Replace("</", "<\\/");

            string html = HtmlTemplate.Replace("__PARK_DATA__", parkJson);
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
  @media (prefers-color-scheme: dark) {
    body { background: #1e1e1e; }
    .legend {
      background: #2d2d30; color: #e8e8e8;
      box-shadow: 0 1px 5px rgba(0,0,0,0.6);
    }
    .legend-swatch { border-color: #999; }
    .pota-popup a { color: #6ab0f3; }
    .pota-popup .my-line { color: #6fdc6f; }
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

var map = L.map('map');

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a> contributors'
}).addTo(map);

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
