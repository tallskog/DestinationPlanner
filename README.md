# MSFS 2024 Flight Logbook & Destination Planner

**Where should I fly next?** This app answers that question visually. It plots every airport in the world on an interactive map alongside your personal flight history, so you can see at a glance where you've already been and where you haven't — then filter the map down to airports that actually make sense for the aircraft you fly.

It grew out of a personal project: systematically flying to every civil airport in Europe suitable for an A320, one region at a time, then moving on to the next continent. The map makes it obvious which nearby airports are still unvisited, and filters (runway length, instrument approach, airport type) narrow the field down to realistic candidates instead of every dot on the map. The same filters work just as well the other way around — if your interest is bush flying into short strips in a Piper Cub, or working through every military field in a region, set the filters to match and the map adapts to your goals instead of the developer's.

Flights are logged automatically via SimConnect while you fly — no manual entry — so the "visited" picture on the map stays current on its own.

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
- **Precipitation radar overlay** — a **🌧 Precip** toggle in the top-left corner of the map shows current precipitation (rain, snow, sleet, or hail) via [RainViewer](https://www.rainviewer.com), no signup required; a refresh button updates it on demand — see [Weather overlay](#weather-overlay)
- **Wind barb overlay** — a **🎏 Wind** toggle in the top-left corner of the map shows wind speed/direction as barbs at a flight level you choose (Surface up to FL390), via [Open-Meteo](https://open-meteo.com), no signup required — see [Weather overlay](#weather-overlay)
- **About window** — **Help → About** lists every external data source the app uses (OurAirports, OpenAIP, Open-Meteo, RainViewer, Aviation Weather Center, OpenStreetMap), each linking to the provider's site
- **AI-assisted trip planning (optional, Claude)** — describe a trip in plain English (e.g. "airports in northern Europe with a 9000ft+ runway I haven't visited") and get back a candidate airport list plus a sequenced multi-leg trip with a short narrative; requires a free Anthropic API key — see [Trip planning](#trip-planning-optional)

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

Tests live in `DestinationPlanner.Tests` (xUnit) and cover pure logic and ViewModel behavior — filter logic, CSV parsing, the OpenAIP type-mapping/pagination logic, and the AI trip-planning prompt-building/response-parsing/candidate-filtering logic — using fakes for `IAirportDataService`/`ILogbookService`/`ISimConnectService`/`IAiTripPlanningService` rather than real I/O, SimConnect, or network calls. UI rendering, live SimConnect/MSFS behavior, and the live Claude API call aren't covered by automated tests and should be verified manually.

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

Click **Apply Filters** to update the map. **Clear** resets all filters to their defaults. Filter selections are remembered across app restarts — whatever was active the last time you clicked **Apply Filters** (or **Clear**) is restored automatically on the next launch, so recurring searches don't need to be re-entered every session.

### Airport search
A search box is shown in the top-right corner of the map. Type an ICAO code or any part of an airport name — a live dropdown updates after every keystroke. Click a result (or press Down / Enter) to zoom the map to that airport and open its info popup. Press Escape to clear the search.

### Airport info boxes
Click an airport marker to open its info box (ICAO, name, runways, METAR) with a blue border. Ctrl+click a second airport to open a second info box (orange border) alongside it, with a dashed line and the distance in nautical miles between them. Click either box and drag it to a new spot — handy when two selected airports are close together and their boxes overlap, or one is covering the distance line; a thin line keeps a moved box connected to its airport so you don't lose track of which is which. Clicking empty map area closes both boxes.

### Statistics
**Statistics** (top-level menu) opens a summary of your entire logbook — independent of any filters active elsewhere in the app. Shows total flights, distinct airports visited, logbook date span, top 3 most visited/landed-at/departed-from airports, top 3 most common routes, longest leg by distance and by time, average and total leg distance/time, and a per-aircraft-type breakdown of legs and hours flown.

### About / data sources
**Help → About** opens a window showing the app's version and every external data source it pulls from (OurAirports, OpenAIP, Open-Meteo, RainViewer, Aviation Weather Center, OpenStreetMap), each a clickable link to the provider's site.

## OpenAIP integration (optional)

Airport type classification (civil / military / heliport / private / other-special-use / unknown) is sourced from [OpenAIP](https://www.openaip.net)'s public `/airports` API. This is entirely optional — without it, every airport shows as "Unclassified" and the app behaves exactly as it does today.

OpenAIP's data is licensed [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) (Attribution-NonCommercial) — free for non-commercial use like this project; the app credits OpenAIP with a link in the Airport Type sidebar panel.

To enable it:

1. Create a free account at [openaip.net](https://www.openaip.net) (top-right corner), then open your profile icon → **API Clients** and generate an API key there.
2. In the app: **File → Update Airport Type Data (OpenAIP)…** — the first time, you'll be prompted to paste your API key; the app saves it to `openaip.local.json` in the AppData folder (`%LocalAppData%\DestinationPlanner\`, or `%LocalAppData%\DestinationPlanner-dev\` for Debug builds) and immediately fetches and applies the classification. This file is never committed to source control. Subsequent syncs reuse the saved key without prompting again. The result is also cached locally and reapplied automatically on the next launch.

## Trip planning (optional)

Ask an AI ([Claude Sonnet 5](https://www.anthropic.com)) to suggest where to fly next, in plain English. This is entirely optional — without an API key configured, the rest of the app works exactly as it does today and the Trip Plans tab stays disabled.

The AI never picks airports itself — that stays deterministic, reusing the same runway/region/visited-status filtering as the Map tab, so it can never suggest an airport that doesn't exist in your loaded data. The AI's job is narrower: (1) turn your free-text request into filter parameters (using a curated table of aviation region names so it never has to guess ICAO prefixes from memory), and (2) once you've reviewed the resulting candidate airport list, sequence it into an ordered multi-leg trip with a short narrative.

To enable it:

1. Create an API key at [console.anthropic.com](https://console.anthropic.com/settings/keys).
2. In the app: **File → Configure AI…** — paste the key; it's saved to `anthropic.local.json` in the AppData folder, never committed to source control.
3. Open the **Trip Plans** tab (or **File → Plan a Trip…**), describe what you're looking for, click **Generate Candidates** to see the matching airports, then **Confirm & Plan Trip** to get a sequenced itinerary. Saved trip plans persist independently of which logbook is currently open. A leg can be marked flown manually, and is also marked flown automatically as soon as a matching flight (same departure/arrival airports) is logged — including live, the moment you land it in MSFS.

Candidate airports you don't want can be removed before confirming (**Remove Selected**), and a saved plan can be deleted entirely (**Delete Plan**). Each leg shows the distance between its airports in nautical miles. **View on Map** opens a small map window with every airport in the plan and a line per leg — green for legs already flown, orange for the rest, with the distance in nautical miles labeled at each leg's midpoint. Click an airport marker there for the same draggable info box (ICAO, name, runways, METAR) as the Map tab, or click the line between two airports to see both of their info boxes at once. Click anywhere else on the map to dismiss an open box. Selecting one or more legs in the Legs list (Ctrl/Shift-click for several) highlights those legs' lines on an already-open map window.

Selecting a saved plan shows the exact query that generated it in a read-only, copyable box. **Reuse Query** copies it back into the query field so you can tweak it and generate a new plan without retyping the original request from scratch.

If you ask for a per-leg distance (e.g. "legs around 200nm -50/+100"), that constraint is **not** left to the AI to eyeball — Claude only ever sees bare ICAO codes, so it has no way to actually know how far apart two airports are. Instead, the app computes real distances between your confirmed candidates and builds the route itself, guaranteeing every leg falls inside the requested window; the AI's job then shrinks to writing a title and narrative for that fixed route. A candidate that can't be reached within the distance window is simply left out of the plan (and called out in the status message) rather than forcing a leg that breaks the rule.

## Weather overlay

Both weather overlays live as buttons in the top-left corner of the map — not in the filter sidebar, since they toggle map layers rather than filter airports — and both follow the same philosophy: **no automatic/background refresh.** This app is for planning a flight, not for tracking weather live. Each overlay shows a snapshot of conditions at the moment you check it (or last refreshed), and stays as-is — including through panning/zooming — until you click refresh again or toggle it off.

### Precipitation

The **🌧 Precip** / **⟳** buttons show current precipitation — rain, snow, sleet, or hail — sourced from [RainViewer](https://www.rainviewer.com)'s public radar API. No signup or API key is required.

- Click **🌧 Precip** to fetch and show the most recent radar frame as an overlay on the map, drawn above the base map but below airport markers. The time shown next to the buttons is when that frame was observed.
- Click **⟳** to refresh to the latest frame at any time.
- RainViewer's radar API is free for personal/non-commercial use; the app shows a "Radar: RainViewer" attribution link whenever the overlay is active, per their terms.

### Wind barbs

The **🎏 Wind** / **⟳** buttons and the flight-level dropdown show wind speed and direction as barbs (shaft + tick marks/pennants — standard 5/10/50 kt increments), sourced from [Open-Meteo](https://open-meteo.com)'s free public forecast API. No signup or API key is required, and coverage is global.

- Choose a flight level from the dropdown — Surface, 3,000 ft, 6,000 ft, 9,000 ft, 12,000 ft, 18,000 ft (FL180), 24,000 ft (FL240), 30,000 ft (FL300), 34,000 ft (FL340), or 39,000 ft (FL390) — the same standard levels used on aviation winds-aloft charts.
- Click **🎏 Wind** to sample a grid of points across the *currently visible* map area and draw a barb at each one. Click **⟳** to resample after panning/zooming, or after changing the flight level while the overlay is already on (changing the level also resamples automatically).
- Barbs point toward the direction the wind is blowing *from* (standard meteorological convention); more/longer tick marks and filled triangles mean stronger wind (each full tick = 10 kt, each half tick = 5 kt, each filled triangle = 50 kt).
- Because the sample grid is tied to the visible area rather than the whole world, it does not follow you as you pan — refresh again once you've moved to where you want to check.
- Zoomed out further than a normal continental view (e.g. the whole world), the overlay shows "zoom in to see wind barbs" instead of fetching — at that scale the grid would be too sparse to be readable anyway.

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
| `MinRunway`, `MaxRunway`, `UseMeters`, `RequireInstrumentApproach`, `RequireAtis`, `FilterCenterIcao`, `FilterRadiusNm`, `ShowVisited`, `ShowNotVisited`, `ShowCivilAirports`, `ShowMilitaryAirports`, `ShowHeliportAirports`, `ShowPrivateAirports`, `ShowOtherAirports`, `ShowUnknownAirports`, `ShowUnclassifiedAirports`, `IcaoPrefixes` | *(auto-set)* | The Map tab's filter selections, saved automatically whenever **Apply Filters** or **Clear** is clicked and restored on the next launch. |

## Project structure

```
DestinationPlanner/
├── Converters/      WPF value converters (SetContainsConverter)
├── Models/          FlightRecord, Airport, AirportType, AirportFilterCriteria, TripPlan, TripQueryFilters, TripNarrative
├── ViewModels/      MainViewModel, MapViewModel, LogbookViewModel, TripPlanViewModel, TripLegRow
├── Views/           MapView.xaml, LogbookView.xaml, LogbookSelectionDialog.xaml, TripPlanView.xaml,
│                    AnthropicApiKeyDialog.xaml, TripMapWindow.xaml
├── Services/        LogbookService, AirportDataService, SimConnectService, OpenAipDataService, OpenAipCredentials,
│                    AirportFilterService, TripCandidateService, AnthropicTripPlanningService, AnthropicCredentials,
│                    TripRouteBuilder
├── Serialization/   NativeLogbookSerializer, ForeignLogbookImporter, LittleNavmapCsvImporter
├── Schemas/         NativeLogbook.xsd, ForeignLogbook.xsd
└── Helpers/         GeoHelper, RelayCommand, AppDataHelper, LandingRatingHelper, AppSettings, AppSettingsService,
                     RegionLookup, TripPlanStore

DestinationPlanner.Tests/
├── Services/        AirportDataService, OpenAipDataService, OpenAipCredentials, AirportFilterService,
│                    TripCandidateService, AnthropicCredentials, AnthropicTripPlanningService, TripRouteBuilder tests
├── ViewModels/      MapViewModel filter tests, TripPlanViewModel tests
└── Fakes/           Hand-rolled fakes for IAirportDataService, ILogbookService, ISimConnectService, IAiTripPlanningService
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
