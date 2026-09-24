// OpenStreetMap-Carto-like style for the Protomaps basemap (v4) vector
// tiles the Offline Map menu downloads, drawn by protomaps-leaflet. Colors,
// label sizes and zoom thresholds follow openstreetmap-carto (CC0), the
// style behind the standard map on openstreetmap.org, so the offline map
// looks like the online Street layer. Exposes window.osmStyle =
// { paintRules, labelRules, ready } - wait on ready before adding a layer
// (see the wetland pattern below).
(function () {
  var P = protomapsL;

  function lin(stops) { return P.linear(stops); }
  function kind(f) { return f.props.kind; }
  function detail(f) { return f.props.kind_detail; }
  function isPoly(f) { return f.geomType === P.GeomType.Polygon; }
  function isLine(f) { return f.geomType === P.GeomType.Line; }
  function isPoint(f) { return f.geomType === P.GeomType.Point; }

  // ---- Colors (openstreetmap-carto) ----
  var C = {
    land: '#f2efe9', water: '#aad3df', waterText: '#4d80b3',
    residential: '#e0dfdf', commercial: '#f2dad9', retail: '#ffd6d1', industrial: '#ebdbe8',
    forest: '#add19e', park: '#c8facc', grass: '#cdebb0', scrub: '#c8d7ab', farmland: '#eef0d5',
    cemetery: '#aacbaf', school: '#ffffe5', hospital: '#ffffe5', pitch: '#aae0cb', playground: '#dffce2',
    pedestrian: '#dddde8', parking: '#eeeeee', golf: '#def6c0', beach: '#fff1ba', aerodrome: '#e9e7e2',
    military: '#f3e3dd', sand: '#f5e9c6', glacier: '#ddecec', allotments: '#c9e1bf',
    building: '#d9d0c9', buildingLine: '#c4b6ab',
    protectedLine: '#008000', parkText: '#237c2b',
    motorway: '#e892a2', motorwayCase: '#dc2a67',
    trunk: '#f9b29c', trunkCase: '#c84e2f',
    primary: '#fcd6a4', primaryCase: '#a06b00',
    secondary: '#f7fabf', secondaryCase: '#707d05',
    tertiary: '#ffffff', tertiaryCase: '#8f8f8f',
    minor: '#ffffff', minorCase: '#bbbbbb', minorLowZoom: '#c3c3c3',
    footway: '#fa8072', cycleway: '#0000ff', bridleway: '#008000', track: '#996600',
    rail: '#707070', runway: '#bbbbcc', boundary: '#8d618b',
    text: '#222222', halo: '#ffffff', houseNumber: '#666666', placeText: '#000000'
  };

  // ---- Road classes: fill width by zoom, min zoom, colors ----
  var W = {
    motorway: lin([[5, 0.6], [8, 1.2], [10, 2], [12, 3.5], [13, 6], [15, 9], [16, 11], [18, 18]]),
    primary: lin([[7, 0.6], [9, 1.2], [11, 2], [12, 3.5], [13, 5.5], [15, 9], [16, 11], [18, 18]]),
    secondary: lin([[9, 0.8], [11, 1.5], [12, 3], [13, 5], [15, 8], [16, 10], [18, 17]]),
    tertiary: lin([[10, 0.8], [12, 2.5], [13, 4], [15, 7], [16, 9], [18, 15]]),
    minor: lin([[12, 0.8], [13, 2.5], [14, 4], [15, 6], [16, 8], [18, 13]]),
    service: lin([[13, 0.8], [14, 2], [16, 4], [18, 7]]),
    link: lin([[12, 1.5], [13, 3], [15, 5], [18, 10]])
  };

  // [kind_detail, fill color, casing color, width fn, fill min zoom, casing min zoom]
  var ROADS = [
    ['service', C.minor, C.minorCase, W.service, 14, 14],
    ['unclassified', C.minor, C.minorCase, W.minor, 12, 13],
    ['residential', C.minor, C.minorCase, W.minor, 12, 13],
    ['living_street', C.minor, C.minorCase, W.minor, 12, 13],
    ['road', C.minor, C.minorCase, W.minor, 12, 13],
    ['tertiary_link', C.tertiary, C.tertiaryCase, W.link, 12, 12],
    ['tertiary', C.tertiary, C.tertiaryCase, W.tertiary, 10, 12],
    ['secondary_link', C.secondary, C.secondaryCase, W.link, 12, 12],
    ['secondary', C.secondary, C.secondaryCase, W.secondary, 9, 11],
    ['primary_link', C.primary, C.primaryCase, W.link, 12, 12],
    ['primary', C.primary, C.primaryCase, W.primary, 7, 11],
    ['trunk_link', C.trunk, C.trunkCase, W.link, 12, 12],
    ['trunk', C.trunk, C.trunkCase, W.motorway, 5, 11],
    ['motorway_link', C.motorway, C.motorwayCase, W.link, 12, 12],
    ['motorway', C.motorway, C.motorwayCase, W.motorway, 5, 11]
  ];

  function roadFilter(d, minZoom) {
    return function (z, f) { return z >= minZoom && detail(f) === d; };
  }

  // ---- Paint rules (drawn in order, first = bottom) ----
  var paint = [];

  paint.push({ dataLayer: 'earth', symbolizer: new P.PolygonSymbolizer({ fill: C.land }) });

  function landuse(kinds, fill, minzoom, opts) {
    paint.push({
      dataLayer: 'landuse', minzoom: minzoom || 0,
      symbolizer: new P.PolygonSymbolizer(Object.assign({ fill: fill }, opts || {})),
      filter: function (z, f) { return kinds.indexOf(kind(f)) >= 0; }
    });
  }
  landuse(['residential'], C.residential, 10);
  landuse(['commercial'], C.commercial, 10);
  landuse(['retail'], C.retail, 10);
  landuse(['industrial', 'railway', 'quarry', 'landfill', 'brownfield', 'construction'], C.industrial, 10);
  landuse(['farmland', 'farmyard', 'orchard', 'vineyard'], C.farmland, 10);
  landuse(['grass', 'grassland', 'meadow', 'village_green', 'recreation_ground', 'garden'], C.grass, 10);
  landuse(['scrub', 'heath'], C.scrub, 8);
  landuse(['forest', 'wood'], C.forest, 7);
  landuse(['park', 'dog_park'], C.park, 10);
  landuse(['cemetery', 'grave_yard'], C.cemetery, 12);
  landuse(['school', 'university', 'college', 'kindergarten'], C.school, 12);
  landuse(['hospital', 'clinic'], C.hospital, 12);
  landuse(['golf_course'], C.golf, 11);
  landuse(['pitch', 'stadium', 'track', 'sports_centre'], C.pitch, 13);
  landuse(['playground'], C.playground, 14);
  landuse(['allotments'], C.allotments, 12);
  landuse(['beach'], C.beach, 11);
  landuse(['sand', 'bare_rock'], C.sand, 10);
  landuse(['glacier'], C.glacier, 5);
  landuse(['aerodrome', 'airfield'], C.aerodrome, 10);
  landuse(['military', 'naval_base'], C.military, 10);
  landuse(['pedestrian', 'platform'], C.pedestrian, 13);
  landuse(['parking'], C.parking, 14);

  // Wetland: Carto's marsh look - @grass fill with its own wetland.png
  // symbol pattern on top from z10 (landcover.mss). The tiles don't say
  // which kind of wetland, so marsh (the common case here) is used for all.
  // The pattern image loads asynchronously; osmStyle.ready resolves once
  // it has, and the page waits on that before adding the layer, since a
  // canvas pattern can't be made from an image that hasn't loaded yet.
  var wetlandImage = new Image();
  var patternsReady = new Promise(function (resolve) {
    wetlandImage.onload = resolve;
    wetlandImage.onerror = resolve; // falls back to the plain fill below
  });
  // openstreetmap-carto's symbols/wetland.png (CC0), inlined so the style
  // has no separate file to fetch.
  wetlandImage.src = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAAsTAAALEwEAmpwYAAAAB3RJTUUH3wQWEw81YAVvHAAAChdJREFUeNrt3UmWIzcMBNDmxofTFXw+XcGH00beuPu1bVW3Bg4YfmzrlZIEAgGAmSTHtxdwud7uX/3trz//GN8WY+Xzs/52xnFAHHA6EI4D849iUwIABC2xoH06l3BluzIVsUELkCpgBJ7EkJUXI/sksgdfNfEghnHs+cz/cojgYp/Gdh5ICfxsDQBxBTS7NLTheDSJrgR4x6HvkkAACtqT4/r+vBFtkgKjbibiWy0AEJ0WwnFqzK8+N/0ioKwCMLECEFBzRWaGPfmEjbQACLLdDjtsprw/+/whYEHV0Ld9tRcdQAsAVTLPSaGNJvKSzhMCcLne7u/2ficMHHFRDdHW+/HUhzKneLaLo8ipYiBeWgAQJEAABBjwEwFA0Dzj7hiUn8759P9H4vn/dgN2+45bVmPTbvb5+fmDkxHOuLQAgOhQLLs/8/yye8W96xeI/PVmBcB4EIUXnbm4Y+6DMXrPm9j3ts+PtwCc3av34y+vT7dVAF4FIiEbxJx32csSBJfqZ6evsyYRawAEBRoDcWXqhyJG8JoIwM+OftexLseAyBVU9m//V9piIGEMR9oQVHd+kX0ryyItWAOITWYLgIQODgoAstQONAKft/36+DXgjEVAANVIjnG2XgRUiQAsaAGiBFTHI8F22YBoNhGAiI7O9KmnQFnjQ9eRrxkz5dYzE6tA9tztE6/QQHA25uGIZsTKd9tV/tpPgDUQAGoPndcGKrYJ46sfEygCERdq2+tyvd05Fimhse28lgricCcuCchSAtB1Dzbi8sXp57/y3IHUdQOb36BNC+BQDJCArAFAwvbt5JFy3bdCDxck9HM68OX0CmCXigsegHkxKGAaKP6sMRPfeuA0sB7RWPSWLpS4c2/eeLyGJH4r7OdMQISFRvxyJiAAQYkpAB3L3Cjf1Gdt107+f4WKMW0FoFwHOCgAdq/1nQ/xrWPLcbne7r5+A6jN9a/GJPA+dNxsZxNKfXqIFsD38sBv9e08GAVBTtouks868ociAxwUi3Dbgd99qOzLDpCPo0OQnB9jlr0Wrt/WAgBsDUjBvNYmbY5NQiTIwLVyl4MCQFwhIwBBe80Vvx19YxAkXQNQovedX4TdkCdsW8WfD08FznS54aksSVwIcQU7eD8vSEALIAABInA+9FsAQezjm6zrBDh5uAIArQ8bBm4BMk8y+tgrf0Jr0dQaQOrXgMgAnQUIwSF1IBDwz2xCAIIod5fyG2JVCqleAyK80jXLXLPY6seXgNUDqLt42MsPj2yrtERsvmjsC8RDqLRzsAkooABku0q8gmPdEiTJqAA4F/hzjQDIjsA/Oef/u9/XnyJK6THjjhbgGIGikK/CO21rRGvGNjINFgAWVQBZV++JCuCOFoDjoSyvVnIV0Q87O3PlpVdXAcjWwPeJ5zg4VxDxUV++vnU9eBdSVLkDXjDV9cmn50jIsoIFrAEICOLyvi35Iy+HW22ZRFQBBb8RAIaEzsEcdd3n1d9zPXhAIhJXsAYgILbZyEq7+S0RAPfjERzzUQGkdrbVbyBWC+8GFBwA8cVEIBZT9si70aLfftQxCRIApSC7WQMAgI4il64k3KH07sB7b6627ia8GETZhBSd7OYSFS2AQGEXWCkAvkCrFyRsSgAEBkGCJHyc/h1A96weMYgrCsupOUX/RmHV82Qf/azxawEQhgDxR0f7jioTRGCBxe8fCABDQvXAdTy6NYCpzs1ECgKP04/8PRA03/x8zSagZ9mdwyAFeSsIT+pPgbM5ICNhZFcoswZQ4RPUrgFJiPrYyFsAaB2YkXh/ImkK7CYZh8CrDra2ANGNdOLVX+YgZC9rANQWZPJi3HLkE+JCY//+60iwmeRAQiB48W3FOIHI2JnIVeaezf+jE0lkivm2cZdgozUAQGA2KSYA341zYkOKz4sFIvudtb+ydxMhtS/5haTioTdUV1YgLI1tPxgIwBrAUwHmFRexmj1/bxHOPfdyvd2HgFNOskvjCgApnlfL2URypqBgJgAgOza0WRTbD44xP4Gcz56zxv/SIiBCAtQSwdBn6hGZfhnW2wRrAKlLq9MEFgh5+WM3oB4vha2st9QZ/9EyXJACaAFkiMaCmWlDFgEQwMB/R9d5Zs99XK63O9ILGELbkwOhP+HsQCBBAmFbAOSMHcj8g0/t1wAEwTnbrbb9yXfqXXg1Ogah994gQf0jAO/uBZDFzY8dcts19YEgnJv3IEq+SdgCIAJUCkQHnExcA6DQADkxMgVg5IDOtGId1Y4Eez8/ZEEQuCoAQG7o6PtxcpJdn92Z/KdaJdvaJ1YAgoe94Cwfph0KyriyLyGyBiCgCR4/EQCkAKLZaa6DwcyXH+KMYffzkJmoEMXG9htZDWOTTM5xV8+o2fw5uqq7AOY7/phcASAnQC64GajgmH1hicPpBcCimbHDel9yJrQQIgeMLqoAfFQhM0LiNYCO/aLjoM6N2a68YAIgI/ecs2CJYYvT7QVVBGichAiA7AhagNhlCgHpU9LikQoAADYJX4irwTpmg+r3Kaqa9j7v3ctRRtdAyN7WKIf7tD7hWwBkBLAGACApEIA6BOz4gYjArWOPZ8Y+Ik8eGeGkGHYY+8jmTNkEIEgL0J3QnV/lRb2mC14UALvSkJAI902CiI7cbJxYQOwGBMIDOSqAncSyb77n+gK8Zk+LgAmFjt2tFVgDgJSk7Hxce8QxjM7GrZgVVAdQvgJAcsCHBQLAkCAIa9nhyMUg1cjgSDTtYNW5+/Ye2FALkIME7gF4fs4CF//CVgAVDSngCOHKMZR9DQi1xYowxvbx6OjoSGMVINCuBQCQDAILgNtzEWTn3B0uogIoHbSnCb7DHt7QPD/nSLYal+vt7uQUgNrC86v/AQAtAEXNXJ34tqJOib7z+R9tBrKfnAiCCkCABBHHTDZSdRGA9qvTsq4kctrfP+4FQDjkIVS1efDof8N/9418vYOW8FkDQFDgIwKAIIBPs59b7h49wdzPZpV8vnsug9PyzDuDzWZ/pqrPX9wCPDKATIkw5t2DxynftSMjm6nmkrcAIFDhPG8QShCyb2O0OFa6A+GqzvHE/oJOJ2KFPVrL9tY8Y5LRE1cA/3Wez0RlXe1HHxsMBgXYw/fZsTLj9wSobLVtHJJFwBaACWqSVLABARCAbPfNVWgfC8CpwxlPPxuIbIV5/ur/BQDyuoJcCyAIsxBY0NWxZwRfjggDyUpqwcheKgACIuDY86g9PxkXEiAZWzb2JUIJQkGjBYjrVIQASecznnsNGMBRqor5c5cctABE6EniZ/66LrowZbbFqJLZZAOQFF7n/8g8+GrCYQGuX39++tkyIwjCxnMgADImGzbGWO2MCItP9vgTFXZSAbQn5u6t1e6ULCwAVBYglhgtux2YwWtnduhr68GwIHj73kI8KgVg5jmoIiSI1hUAcelnIzY9z70haAgPO9X206/G9jfwTkI4NIG6mQAAAABJRU5ErkJggg==';
  landuse(['wetland'], C.grass, 5);
  var wetlandPattern = new P.PolygonSymbolizer({ pattern: wetlandImage });
  paint.push({
    dataLayer: 'landuse', minzoom: 10,
    symbolizer: wetlandPattern,
    filter: function (z, f) { return kind(f) === 'wetland' && wetlandImage.complete && wetlandImage.naturalWidth > 0; }
  });

  // Water
  paint.push({
    dataLayer: 'water', symbolizer: new P.PolygonSymbolizer({ fill: C.water }),
    filter: function (z, f) { return isPoly(f) && kind(f) !== 'swimming_pool'; }
  });
  paint.push({
    dataLayer: 'water', minzoom: 15, symbolizer: new P.PolygonSymbolizer({ fill: C.water, stroke: '#78bed2', width: 0.5 }),
    filter: function (z, f) { return isPoly(f) && kind(f) === 'swimming_pool'; }
  });
  paint.push({
    dataLayer: 'water', minzoom: 8,
    symbolizer: new P.LineSymbolizer({ color: C.water, width: lin([[8, 0.7], [12, 1.5], [14, 3], [18, 12]]), lineCap: 'round', lineJoin: 'round' }),
    filter: function (z, f) { return isLine(f) && (kind(f) === 'river' || kind(f) === 'canal'); }
  });
  paint.push({
    // Carto's @stream-width-z12..z16 (water.mss).
    dataLayer: 'water', minzoom: 12,
    symbolizer: new P.LineSymbolizer({ color: C.water, width: lin([[12, 0.8], [13, 1.4], [14, 2], [15, 2.5], [16, 3], [18, 4]]), lineCap: 'round', lineJoin: 'round' }),
    filter: function (z, f) { return isLine(f) && (kind(f) === 'stream' || kind(f) === 'ditch' || kind(f) === 'drain'); }
  });

  // Protected areas (nature reserves, wildlife refuges/management areas,
  // national parks): OSM Carto's green border with a translucent inner band.
  var protectedKinds = ['nature_reserve', 'protected_area', 'national_park'];
  paint.push({
    dataLayer: 'landuse', minzoom: 8,
    symbolizer: new P.PolygonSymbolizer({ fill: 'rgba(0,0,0,0)', stroke: 'rgba(0,128,0,0.15)', width: lin([[8, 2], [12, 6], [16, 10]]) }),
    filter: function (z, f) { return protectedKinds.indexOf(kind(f)) >= 0; }
  });
  paint.push({
    dataLayer: 'landuse', minzoom: 8,
    symbolizer: new P.PolygonSymbolizer({ fill: 'rgba(0,0,0,0)', stroke: 'rgba(0,128,0,0.55)', width: lin([[8, 0.6], [12, 1], [16, 1.5]]) }),
    filter: function (z, f) { return protectedKinds.indexOf(kind(f)) >= 0; }
  });

  // Buildings
  paint.push({
    dataLayer: 'buildings', minzoom: 13,
    symbolizer: new P.PolygonSymbolizer({ fill: C.building, stroke: C.buildingLine, width: lin([[13, 0], [15, 0.5], [18, 1]]) }),
    filter: function (z, f) { return isPoly(f); }
  });

  // Runways/taxiways
  paint.push({
    dataLayer: 'roads', minzoom: 11,
    symbolizer: new P.LineSymbolizer({ color: C.runway, width: lin([[11, 2], [13, 6], [16, 20], [18, 40]]) }),
    filter: function (z, f) { return detail(f) === 'runway'; }
  });
  paint.push({
    dataLayer: 'roads', minzoom: 13,
    symbolizer: new P.LineSymbolizer({ color: C.runway, width: lin([[13, 1], [16, 5], [18, 12]]) }),
    filter: function (z, f) { return detail(f) === 'taxiway'; }
  });

  // Paths, tracks, cycleways, footways: dashed lines like OSM Carto, with
  // a faint white halo so they stay visible over green areas.
  function path(details, color, width, dash, minzoom) {
    paint.push({
      dataLayer: 'roads', minzoom: minzoom,
      symbolizer: new P.LineSymbolizer({ color: 'rgba(255,255,255,0.6)', width: function (z) { return width(z) + 2; } }),
      filter: function (z, f) { return kind(f) === 'path' && details.indexOf(detail(f)) >= 0; }
    });
    paint.push({
      dataLayer: 'roads', minzoom: minzoom,
      symbolizer: new P.LineSymbolizer({ color: color, width: width, dash: dash }),
      filter: function (z, f) { return kind(f) === 'path' && details.indexOf(detail(f)) >= 0; }
    });
  }
  path(['footway', 'path', 'sidewalk', 'crossing', 'pedestrian', 'corridor'], C.footway, lin([[13, 0.8], [15, 1.2], [18, 2]]), [3, 2], 13);
  path(['steps'], C.footway, lin([[15, 3], [18, 5]]), [1, 1.5], 15);
  path(['cycleway'], C.cycleway, lin([[13, 0.8], [15, 1.2], [18, 2]]), [3, 2], 13);
  path(['bridleway'], C.bridleway, lin([[13, 0.8], [15, 1.2], [18, 2]]), [4, 2], 13);
  path(['track'], C.track, lin([[13, 1], [15, 1.5], [18, 2.5]]), [6, 3], 13);

  // Road casings first (all classes), then fills - so a major road's fill
  // is always drawn over any minor road's casing at an intersection.
  ROADS.forEach(function (r) {
    var width = r[3], casingMin = r[5];
    paint.push({
      dataLayer: 'roads', minzoom: casingMin,
      symbolizer: new P.LineSymbolizer({ color: r[2], width: function (z) { return width(z) + (z >= 16 ? 2 : 1.4); }, lineCap: 'round', lineJoin: 'round' }),
      filter: roadFilter(r[0], casingMin)
    });
  });
  ROADS.forEach(function (r) {
    var fillMin = r[4], casingMin = r[5], fill = r[1], lowZoomFill = r[1] === C.minor ? C.minorLowZoom : r[1];
    paint.push({
      dataLayer: 'roads', minzoom: fillMin,
      symbolizer: new P.LineSymbolizer({
        // Below its casing zoom a white road would vanish on the light
        // background, so OSM Carto draws it gray there instead.
        color: function (z) { return z < casingMin && fill === C.minor ? lowZoomFill : fill; },
        width: r[3], lineCap: 'round', lineJoin: 'round'
      }),
      filter: roadFilter(r[0], fillMin)
    });
  });

  // Railways: gray line with white dashes.
  paint.push({
    dataLayer: 'roads', minzoom: 9,
    symbolizer: new P.LineSymbolizer({ color: C.rail, width: lin([[9, 0.8], [13, 2], [16, 3], [18, 4]]) }),
    filter: function (z, f) { return kind(f) === 'rail' && detail(f) === 'rail'; }
  });
  paint.push({
    dataLayer: 'roads', minzoom: 13,
    symbolizer: new P.LineSymbolizer({ color: '#ffffff', width: lin([[13, 0.8], [16, 1.5], [18, 2]]), dash: [8, 8] }),
    filter: function (z, f) { return kind(f) === 'rail' && detail(f) === 'rail'; }
  });

  // Administrative boundaries (admin.mss): state lines are a wide
  // translucent purple band with a thin dashed line down the middle from
  // z10; county lines a thin dashed purple line on a white backing from z10.
  function isState(f) { return kind(f) === 'region' || (typeof detail(f) === 'number' && detail(f) <= 4); }
  var stateWidth = function (z) { return byZoom(z, [[4, 0.4], [5, 0.5], [6, 0.6], [7, 0.8], [8, 1], [9, 1.5], [10, 2], [11, 2.8], [12, 3], [13, 3.5], [14, 4]]); };
  paint.push({
    dataLayer: 'boundaries', minzoom: 4,
    symbolizer: new P.LineSymbolizer({ color: C.boundary, width: function (z) { return stateWidth(z) * 2; }, opacity: 0.35 }),
    filter: function (z, f) { return isState(f); }
  });
  paint.push({
    dataLayer: 'boundaries', minzoom: 10,
    symbolizer: new P.LineSymbolizer({ color: C.boundary, width: function (z) { return z >= 11 ? 0.8 : 0.6; }, dash: [8, 2, 1.5, 2, 1.5, 2] }),
    filter: function (z, f) { return isState(f); }
  });
  paint.push({
    dataLayer: 'boundaries', minzoom: 10,
    symbolizer: new P.LineSymbolizer({ color: '#ffffff', width: function (z) { return z >= 11 ? 1.4 : 1; } }),
    filter: function (z, f) { return kind(f) === 'county'; }
  });
  paint.push({
    dataLayer: 'boundaries', minzoom: 10,
    symbolizer: new P.LineSymbolizer({ color: C.boundary, width: function (z) { return z >= 11 ? 1.4 : 1; }, dash: [8, 1.5, 1.5, 1.5] }),
    filter: function (z, f) { return kind(f) === 'county'; }
  });

  // ---- Label rules (earlier = higher priority when labels collide) ----
  // Sizes, colors and zoom thresholds below are taken from
  // openstreetmap-carto's own style sheets (placenames.mss, roads.mss,
  // amenity-points.mss, water.mss, addressing.mss) - Carto sizes are in
  // the same CSS pixels used here. A Carto halo radius r is a canvas
  // stroke width of 2r.
  var names = ['name:en', 'name'];
  var labels = [];
  var HALO = 'rgba(255,255,255,0.6)';   // @standard-halo-fill
  var PLACE = '#222222';                // @placenames
  var PLACE_LIGHT = '#777777';          // @placenames-light
  var FONT = 'sans-serif';

  function font(size, italic) { return (italic ? 'italic ' : '') + size + 'px ' + FONT; }
  // Picks the value for the highest threshold <= z from [[zoom, value], ...].
  function byZoom(z, steps) {
    var v = steps[0][1];
    for (var i = 0; i < steps.length; i++) if (z >= steps[i][0]) v = steps[i][1];
    return v;
  }
  function minZoom(f, fallback) { return typeof f.props.min_zoom === 'number' ? f.props.min_zoom : fallback; }
  // Carto shows an area's name once it covers 3000 pixels on screen and
  // steps its size up at 12000 / 48000 / 192000 (way_pixels). Tiles don't
  // carry area; the closest thing is Protomaps' min_zoom, which its
  // basemap build grades from the area's bounding box in coarse bands
  // (Pois.java namedPolygonZoomsIndex). Compared side by side with
  // openstreetmap.org, areas first get labeled there about one zoom after
  // min_zoom. Each zoom after that is 4x the pixels, i.e. one step up
  // Carto's size ladder. Nature reserves/protected areas start one step
  // up: their bounding boxes are usually much bigger than a park's, so
  // they're typically well past 3000 pixels by the time they show.
  var RESERVE_KINDS = ['nature_reserve', 'protected_area', 'national_park'];
  function areaFirstZoom(f) { return minZoom(f, 14) + 1; }
  function areaSize(z, f, sizes, first) {
    var step = Math.max(0, Math.floor(z - first) + (RESERVE_KINDS.indexOf(kind(f)) >= 0 ? 1 : 0));
    return sizes[Math.min(step, sizes.length - 1)];
  }

  // Place names (placenames.mss). [first zoom, last zoom (exclusive), size steps, zoom it turns light gray]
  var PLACES = {
    city: [8, 15, [[8, 13], [10, 14], [11, 15]], 99],
    town: [9, 16, [[9, 10], [11, 11], [12, 13], [14, 15]], 99],
    suburb: [12, 17, [[12, 11], [13, 12], [14, 14], [16, 15]], 14],
    village: [12, 17, [[12, 10], [13, 11], [14, 13], [15, 14], [16, 15]], 14],
    quarter: [14, 17, [[14, 11], [15, 12], [16, 14]], 15],
    hamlet: [14, 18, [[14, 10], [15, 11], [16, 12]], 15],
    neighbourhood: [15, 20, [[15, 10], [16, 12]], 16],
    isolated_dwelling: [15, 20, [[15, 10], [16, 12]], 16],
    farm: [15, 20, [[15, 10], [16, 12]], 16],
    locality: [16, 20, [[16, 10], [17, 12]], 17]
  };
  function placeRule(f) { return PLACES[detail(f)] || (kind(f) === 'neighbourhood' ? PLACES.neighbourhood : null); }

  // Big cities below zoom 8: Carto draws a dot with the name beside it.
  labels.push({
    dataLayer: 'places', maxzoom: 7,
    symbolizer: new P.GroupSymbolizer([
      new P.CircleSymbolizer({ radius: 2.5, fill: '#222222', stroke: '#ffffff', width: 1 }),
      new P.OffsetTextSymbolizer({ labelProps: names, fill: PLACE, stroke: HALO, width: 3, offsetX: 5, offsetY: 5, font: function (z) { return font(z >= 6 ? 12 : 11); } })
    ]),
    filter: function (z, f) { return kind(f) === 'locality' && (detail(f) === 'city' || detail(f) === 'town') && z >= minZoom(f, 99); }
  });
  labels.push({
    dataLayer: 'places', minzoom: 8,
    symbolizer: new P.Padding(4, new P.CenteredTextSymbolizer({
      labelProps: names, stroke: HALO, width: 3,
      fill: function (z, f) { var r = placeRule(f); return r && z >= r[3] ? PLACE_LIGHT : PLACE; },
      font: function (z, f) { var r = placeRule(f); return font(r ? byZoom(z, r[2]) : 10); }
    })),
    filter: function (z, f) { var r = placeRule(f); return !!r && z >= r[0] && z < r[1]; }
  });

  // Route shields (roads.mss #roads-text-ref): motorway from z10, trunk and
  // primary z11, secondary z12, tertiary z13; 10px, 11 at z16, 12 at z18.
  var SHIELD_TEXT = { motorway: '#620728', trunk: '#5f1c0c', primary: '#503000', secondary: '#363b02', tertiary: '#3b3b3b' };
  var SHIELD_FILL = { motorway: C.motorway, trunk: C.trunk, primary: C.primary, secondary: C.secondary, tertiary: '#ffffff' };
  var SHIELD_MINZOOM = { motorway: 10, trunk: 11, primary: 11, secondary: 12, tertiary: 13 };
  function roadClass(f) { return String(detail(f) || '').replace('_link', ''); }
  labels.push({
    dataLayer: 'roads', minzoom: 10,
    symbolizer: new P.Padding(20, new P.ShieldSymbolizer({
      labelProps: ['osm_shield'], padding: 2,
      fill: function (z, f) { return SHIELD_TEXT[roadClass(f)] || '#3b3b3b'; },
      background: function (z, f) { return SHIELD_FILL[roadClass(f)] || '#ffffff'; },
      font: function (z) { return font(byZoom(z, [[10, 10], [16, 11], [18, 12]])); }
    })),
    filter: function (z, f) {
      var cls = roadClass(f);
      if (!f.props.shield_text || !SHIELD_MINZOOM[cls] || z < SHIELD_MINZOOM[cls] || String(detail(f)).indexOf('_link') >= 0) return false;
      // Carto shows the full ref ("NY 31", "I 390"), and only the first
      // route where several share a road. Labels can't compute text, so
      // it's derived once here and cached on the feature's own props.
      if (f.props.osm_shield === undefined) f.props.osm_shield = String(f.props.ref || f.props.shield_text).split(';')[0];
      return true;
    }
  });

  // Area names: protected areas/nature reserves/national parks (#008000),
  // parks and other leisure areas, woods, wetlands - oblique, 10/12/15px
  // by size on screen (amenity-points.mss).
  var AREA_TEXT = {
    nature_reserve: '#008000', protected_area: '#008000', national_park: '#008000',
    park: '#0c8416', garden: '#0c8416', golf_course: '#0c8416', recreation_ground: '#0c8416', playground: '#0c8416', pitch: '#0c8416', dog_park: '#0c8416',
    forest: '#46673b', wood: '#46673b',
    wetland: '#0566bf', marsh: '#0566bf', swamp: '#0566bf', bog: '#0566bf'
  };
  labels.push({
    dataLayer: 'pois',
    symbolizer: new P.CenteredTextSymbolizer({
      labelProps: names, stroke: HALO, width: 3, lineHeight: 1.1, maxLineChars: 14,
      fill: function (z, f) { return AREA_TEXT[kind(f)]; },
      font: function (z, f) { return font(areaSize(z, f, [10, 12, 15], areaFirstZoom(f)), true); }
    }),
    filter: function (z, f) { return !!AREA_TEXT[kind(f)] && z >= areaFirstZoom(f); }
  });

  // Road names, centered on the road with a halo in the road's own color
  // (roads.mss #roads-text-name).
  var ROAD_NAME = {
    motorway: [13, [[13, 8], [14, 9], [15, 10], [17, 11], [19, 12]], C.motorway],
    trunk: [13, [[13, 8], [14, 9], [15, 10], [17, 11], [19, 12]], C.trunk],
    primary: [13, [[13, 8], [14, 9], [15, 10], [17, 11], [19, 12]], C.primary],
    secondary: [13, [[13, 8], [14, 9], [15, 10], [17, 11], [19, 12]], C.secondary],
    tertiary: [14, [[14, 9], [17, 11], [19, 12]], '#ffffff'],
    residential: [15, [[15, 8], [16, 9], [17, 11], [19, 12]], '#ffffff'],
    unclassified: [15, [[15, 8], [16, 9], [17, 11], [19, 12]], '#ffffff'],
    road: [15, [[15, 8], [16, 9], [17, 11], [19, 12]], '#ffffff'],
    living_street: [15, [[15, 8], [16, 9], [17, 11], [19, 12]], '#ffffff'],
    service: [16, [[16, 9], [17, 11], [19, 12]], '#ffffff'],
    footway: [16, [[16, 9], [17, 10]], HALO], path: [16, [[16, 9], [17, 10]], HALO],
    cycleway: [16, [[16, 9], [17, 10]], HALO], bridleway: [16, [[16, 9], [17, 10]], HALO],
    track: [16, [[16, 9], [17, 10]], HALO], pedestrian: [16, [[16, 9], [17, 10]], HALO]
  };
  labels.push({
    dataLayer: 'roads', minzoom: 13,
    symbolizer: new P.LineLabelSymbolizer({
      labelProps: names, fill: '#000000', width: 2, position: P.LineLabelPlacement.Center,
      stroke: function (z, f) { return ROAD_NAME[detail(f)][2]; },
      font: function (z, f) { return font(byZoom(z, ROAD_NAME[detail(f)][1])); }
    }),
    filter: function (z, f) { var r = ROAD_NAME[detail(f)]; return !!r && z >= r[0]; }
  });

  // Water names (water.mss): lakes by size on screen, rivers/canals from
  // z13 (12px from z14), streams from z15.
  labels.push({
    dataLayer: 'water',
    symbolizer: new P.CenteredTextSymbolizer({
      labelProps: names, fill: C.waterText, stroke: HALO, width: 3, maxLineChars: 14,
      font: function (z, f) { return font(areaSize(z, f, [10, 12, 15, 19], minZoom(f, 12)), true); }
    }),
    filter: function (z, f) { return isPoint(f) && z >= minZoom(f, 12); }
  });
  labels.push({
    dataLayer: 'water', minzoom: 13,
    symbolizer: new P.LineLabelSymbolizer({
      labelProps: names, fill: C.waterText, stroke: HALO, width: 3, position: P.LineLabelPlacement.Center,
      font: function (z, f) { return font(kind(f) === 'river' && z >= 14 ? 12 : 10, true); }
    }),
    filter: function (z, f) {
      if (!isLine(f)) return false;
      if (kind(f) === 'river' || kind(f) === 'canal') return z >= 13;
      return z >= 15;
    }
  });

  // Schools, colleges, universities, hospitals: Carto labels these by
  // area like parks (amenity-points.mss), in dark text.
  var CAMPUS_KINDS = ['school', 'college', 'university', 'hospital', 'kindergarten'];
  labels.push({
    dataLayer: 'pois',
    symbolizer: new P.CenteredTextSymbolizer({
      labelProps: names, fill: '#000000', stroke: HALO, width: 3, maxLineChars: 14,
      font: function (z, f) { return font(areaSize(z, f, [10, 12, 15], minZoom(f, 15) + 1), true); }
    }),
    filter: function (z, f) { return CAMPUS_KINDS.indexOf(kind(f)) >= 0 && z >= minZoom(f, 15) + 1; }
  });

  // Other named places (shops, schools, buildings, subdivisions like
  // "Creekside of Hamlin"): Carto shows most of these from z17, in its
  // brown amenity color.
  labels.push({
    dataLayer: 'pois', minzoom: 17,
    symbolizer: new P.CenteredTextSymbolizer({ labelProps: names, fill: '#734a08', stroke: HALO, width: 3, font: font(10), maxLineChars: 14 }),
    filter: function (z, f) { return !AREA_TEXT[kind(f)] && CAMPUS_KINDS.indexOf(kind(f)) < 0 && kind(f) !== 'residential'; }
  });

  // House numbers (addressing.mss): z17+, 10px, #666.
  labels.push({
    dataLayer: 'buildings', minzoom: 17,
    symbolizer: new P.CenteredTextSymbolizer({ labelProps: ['addr_housenumber'], fill: '#666666', stroke: HALO, width: function (z) { return z >= 18 ? 2.5 : 2; }, font: font(10) }),
    filter: function (z, f) { return !!f.props.addr_housenumber; }
  });

  // No backgroundColor: oceans come from the water layer's own polygons,
  // and anything past the downloaded state's edge (no tile at all) is left
  // to the map container's neutral gray instead of looking like open water.
  window.osmStyle = { paintRules: paint, labelRules: labels, ready: patternsReady };
})();
