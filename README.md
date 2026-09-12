# POTA Activator Park Activations

A Windows desktop tool for [Parks on the Air (POTA)](https://parksontheair.com/) activators. Load every park in a state, cross-reference it against your ADIF log, and get a ready-to-use activation planning report — plus an interactive map to help plan and navigate to your next activation.

Built by Jeremy S. Gaynor, K5JSG.

## Features

- **Load all parks for a state** directly from the POTA API, complete with county, elevation, and boundary data.
- **Import an ADIF log** and automatically mark which parks you've already activated.
- **2-fer / 3-fer detection ("Xfer's")** — flags parks whose boundaries overlap, or that share a National Scenic/Historic Trail, so you know which activations can count for multiple parks at once.
- **SOTA cross-reference** — shows any [Summits on the Air](https://www.sotamaps.org/) summit that falls within a park's real boundary, matched by geometry rather than a pre-built cross-reference list.
- **WWFF/KFF cross-reference**, auto-downloaded and kept up to date.
- **Boat Access Only** flag, auto-detected from POTA's own park data.
- **Export** your finished report to CSV or Excel.
- **Interactive map**:
  - Park pins color-coded by status (not worked / boat-access-only / worked)
  - Toggleable park boundary, National Trail, and SOTA summit layers
  - Live GPS "follow me" tracking
  - Distance measuring tool and address search
  - Save the map out to a file
- **Built for the field** — parks, boundaries, trails, elevation, and SOTA data are all cached locally, so the app keeps working with no signal once you've loaded a state at least once.

## Installation

Download the latest installer from the [Releases](https://github.com/K5JSG/POTA-Activator-Park-Activations/releases) page and run it. The app is self-contained — no separate .NET runtime install is required.

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows, with the Windows Desktop workload) and, optionally, [Inno Setup](https://jrsoftware.org/isdl.php) if you want to build the installer.

```powershell
dotnet build "POTA Activator Park Activations.csproj"
```

To produce a self-contained release build and installer:

```powershell
.\build.ps1 -Version <version>
```

This publishes a self-contained, single-file executable to `publish\` and, if Inno Setup is installed, builds the installer into `dist\`.

## License

GNU General Public License v3.0 — see [License.txt](License.txt).
