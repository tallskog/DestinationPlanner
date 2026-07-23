# MSFS 2024 Flight Logbook & Destination Planner

A Windows desktop application for Microsoft Flight Simulator 2024 that combines a flight logbook with an interactive world map. It records flights automatically via SimConnect and lets you explore airports filtered by runway length, instrument approach capability, and distance from a chosen airport.

## Features

- **Live flight recording** — connects to MSFS 2024 via SimConnect and captures each flight (parking brake release → set) automatically
- **Landing quality rating** — automatically rates each landing 1–5 stars based on vertical speed, G-force, bank angle, pitch attitude, centerline deviation, and touchdown zone accuracy; displayed as ★★★★☆ in the logbook; click the stars to open a per-component breakdown with colour-coded scores
- **Flight logbook** — view, filter, edit, and manage your flight history; supports a native XML format, a foreign XML import format, and Little Navmap CSV export files
- **Import highlighting** — newly imported flights are highlighted in light green; clears on "Clear Filters" or next launch
- **Smart duplicate detection** — prevents double entries even when times differ slightly (minute-precision imports vs. second-precision live recordings) or when the same flight appears with a time offset but matching duration
- **Interactive map** — OpenStreetMap tiles with zoom and pan; airport markers filtered by runway length, ILS capability, or radius from a centre airport
- **Logbook airports on map** — visited airports are shown as orange dots; a green ring indicates you departed from there, a red ring that you landed there; both rings appear when you have done both, with a small gap between them; a map legend in the sidebar explains all symbols
- **Multiple logbooks** — create multiple logbook files; switch between them at any time via **File → Open Logbook…**; the last-used logbook is remembered across sessions so you are not prompted on every start
- **Airport type filter (optional, OpenAIP)** — classify airports as civil, military, heliport, private, other/special-use, or unknown, and filter the map by type; requires a free OpenAIP API key — see [OpenAIP integration](#openaip-integration-optional)

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10/11 (64-bit) | WPF application |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | `dotnet --version` should show `8.x.x` |
| Microsoft Flight Simulator 2024 | Required only for live flight recording |
| MSFS SDK (SimConnect) | Required only for live flight recording — see [SimConnect setup](#simconnect-setup) |

## Build

```bash
# From the repository root
dotnet build
```

Or open `DestinationPlanner.sln` in Visual Studio 2022 / Rider and press **Build Solution**.

## Run

```bash
dotnet run --project DestinationPlanner/DestinationPlanner.csproj
```

Or press **F5** in Visual Studio / Rider.

### Publish a self-contained executable

```bash
dotnet publish DestinationPlanner/DestinationPlanner.csproj \
  -c Release -r win-x64 --self-contained \
  -o publish/
```

The output in `publish/` can be run on any Windows 10/11 machine without the .NET runtime installed.

## Running tests

```bash
dotnet test DestinationPlanner.slnx
```

Tests live in `DestinationPlanner.Tests` (xUnit) and cover pure logic and ViewModel behavior — filter logic, CSV parsing, and the OpenAIP type-mapping/pagination logic — using fakes for `IAirportDataService`/`ILogbookService`/`ISimConnectService` rather than real I/O or SimConnect. UI rendering and live SimConnect/MSFS behavior aren't covered by automated tests and should be verified manually.

## Airport data setup

The map tab requires airport data from [OurAirports](https://ourairports.com/data/). There are two ways to load it:

### Option A — Download in-app (recommended)

**File → Download Airport Data** — downloads `airports.csv`, `runways.csv`, and `airport-frequencies.csv` directly from the [OurAirports GitHub repository](https://github.com/davidmegginson/ourairports-data) and loads them automatically. Requires an internet connection.

### Option B — Manual file selection

1. Download **airports.csv** from `https://ourairports.com/data/airports.csv`
2. Download **runways.csv** from `https://ourairports.com/data/runways.csv` (needed for runway-length filtering)
3. Place both files in the same folder
4. In the app: **File → Load Airport Data…** → select `airports.csv`

The app auto-detects `runways.csv` next to `airports.csv`. Without runway data, the runway-length filter has no effect and the centerline deviation / touchdown zone components of the landing rating are skipped (the rating is computed from the remaining factors).

## SimConnect setup

> SimConnect is only needed if you want live flight recording from MSFS 2024. The rest of the app works without it.

1. Install the **MSFS 2024 SDK** from the in-sim Marketplace or the MSFS developer tools page
2. Locate `SimConnect.dll` — typically at:
   ```
   C:\MSFS SDK\SimConnect SDK\lib\static\SimConnect.dll
   ```
3. In Visual Studio: right-click the `DestinationPlanner` project → **Add → Project Reference → Browse** → select `SimConnect.dll`
4. Implement the TODOs in [Services/SimConnectService.cs](DestinationPlanner/Services/SimConnectService.cs)

## Using the app

### Recording flights
1. Start MSFS 2024 and load a flight
2. Open the app — it will connect automatically once you are on the apron
3. Releasing the parking brake starts the recording; setting it again on arrival saves the flight

### Managing logbooks
- **File → Open Logbook…** — switch to a different logbook file at any time without restarting
- **File → Import Logbook…** — import a native logbook into a new file in AppData
- **File → Export Logbook…** — save the current logbook to a user-chosen location
- **File → Import Foreign Logbook…** — merge flights from an external source into the active logbook; supports:
  - `ArrayOfFlightRecord` XML (produced by other flight logbook applications)
  - Little Navmap CSV logbook export (`.csv`)
  
  Duplicates are filtered automatically. The file format is detected from the extension. Newly added flights are highlighted in light green; click **Clear Filters** to remove the highlight.

### Map filters
| Filter | Description |
|---|---|
| Min / Max runway | Minimum and maximum runway length (ft or m) |
| Instrument approach | Show only airports with ILS or equivalent |
| ATIS | Show only airports that have ATIS |
| Show visited / not-visited | Toggle airports you have or have not flown to/from |
| Centre ICAO + Radius | Show only airports within N nm of the given airport |
| ICAO prefixes | Comma-separated prefixes to restrict by country or region (e.g. `EF,ES`) |
| Airport Type | Civil / Military / Heliport / Private / Other (Special-Use) / Unknown / Unclassified — all checked by default. Unclassified covers every airport until you sync OpenAIP data (see below) |

Click **Apply Filters** to update the map. **Clear** resets all filters to their defaults.

### Airport search
A search box is shown in the top-right corner of the map. Type an ICAO code or any part of an airport name — a live dropdown updates after every keystroke. Click a result (or press Down / Enter) to zoom the map to that airport and open its info popup. Press Escape to clear the search.

## OpenAIP integration (optional)

Airport type classification (civil / military / heliport / private / other-special-use / unknown) is sourced from [OpenAIP](https://www.openaip.net)'s public `/airports` API. This is entirely optional — without it, every airport shows as "Unclassified" and the app behaves exactly as it does today.

OpenAIP's data is licensed [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) (Attribution-NonCommercial) — free for non-commercial use like this project; the app credits OpenAIP with a link in the Airport Type sidebar panel.

To enable it:

1. Create a free account at [accounts.openaip.net](https://accounts.openaip.net) and generate an API key on the **API Clients** page.
2. In the app: **File → Update Airport Type Data (OpenAIP)…** — the first time, you'll be prompted to paste your API key; the app saves it to `openaip.local.json` in the AppData folder (`%LocalAppData%\DestinationPlanner\`, or `%LocalAppData%\DestinationPlanner-dev\` for Debug builds) and immediately fetches and applies the classification. This file is never committed to source control. Subsequent syncs reuse the saved key without prompting again. The result is also cached locally and reapplied automatically on the next launch.

## Configuration

A `settings.json` file is created automatically in the AppData directory (`%LocalAppData%\DestinationPlanner\`) on first run. You can edit it with any text editor:

```json
{
  "SimDataRateHz": 60,
  "LastLogbookPath": "C:\\Users\\...\\AppData\\Local\\DestinationPlanner\\logbook-01-01-2026.xml"
}
```

| Key | Default | Description |
|---|---|---|
| `SimDataRateHz` | `60` | SimConnect sampling rate. Values > 1 use visual-frame sampling; at 60 fps sim framerate, 60 = every frame, 10 = every 6th frame. A value of 1 uses once-per-second sampling. |
| `LastLogbookPath` | *(auto-set)* | Path of the logbook opened in the previous session. Set automatically; edit to override. |

## Project structure

```
DestinationPlanner/
├── Converters/      WPF value converters (SetContainsConverter)
├── Models/          FlightRecord, Airport, AirportType
├── ViewModels/      MainViewModel, MapViewModel, LogbookViewModel
├── Views/           MapView.xaml, LogbookView.xaml, LogbookSelectionDialog.xaml
├── Services/        LogbookService, AirportDataService, SimConnectService, OpenAipDataService, OpenAipCredentials
├── Serialization/   NativeLogbookSerializer, ForeignLogbookImporter, LittleNavmapCsvImporter
├── Schemas/         NativeLogbook.xsd, ForeignLogbook.xsd
└── Helpers/         GeoHelper, RelayCommand, AppDataHelper, LandingRatingHelper, AppSettings, AppSettingsService

DestinationPlanner.Tests/
├── Services/        AirportDataService, OpenAipDataService, OpenAipCredentials tests
├── ViewModels/      MapViewModel filter tests
└── Fakes/           Hand-rolled fakes for IAirportDataService, ILogbookService, ISimConnectService
```

## Native logbook XML format

Namespace: `urn:destination-planner:logbook:v1`

```xml
<?xml version="1.0" encoding="utf-8"?>
<FlightLogbook xmlns="urn:destination-planner:logbook:v1" version="1.0">
  <Flights>
    <Flight>
      <Id>3fa85f64-5717-4562-b3fc-2c963f66afa6</Id>
      <Date>2026-04-26</Date>
      <AircraftModel>Airbus A320</AircraftModel>
      <DepartureIcao>EGLL</DepartureIcao>
      <ArrivalIcao>EGCC</ArrivalIcao>
      <BlockOffUtc>2026-04-26T10:30:00Z</BlockOffUtc>
      <BlockOnUtc>2026-04-26T11:45:00Z</BlockOnUtc>
    </Flight>
  </Flights>
</FlightLogbook>
```
