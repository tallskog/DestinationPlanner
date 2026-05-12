# MSFS 2024 Flight Logbook & Destination Planner

A Windows desktop application for Microsoft Flight Simulator 2024 that combines a flight logbook with an interactive world map. It records flights automatically via SimConnect and lets you explore airports filtered by runway length, instrument approach capability, and distance from a chosen airport.

## Features

- **Live flight recording** — connects to MSFS 2024 via SimConnect and captures each flight (parking brake release → set) automatically
- **Flight logbook** — view, filter and import your flight history; supports a native XML format and a foreign XML import format
- **Interactive map** — OpenStreetMap tiles with zoom and pan; airport markers filtered by runway length, ILS capability, or radius from a centre airport
- **Logbook airports on map** — airports you have flown to/from are highlighted in a different colour

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

## Airport data setup

The map tab requires airport data from [OurAirports](https://ourairports.com/data/):

1. Download **airports.csv** from `https://ourairports.com/data/airports.csv`
2. Download **runways.csv** from `https://ourairports.com/data/runways.csv` (needed for runway-length filtering)
3. Place both files in the same folder
4. In the app: **File → Load Airport Data…** → select `airports.csv`

The app auto-detects `runways.csv` next to `airports.csv`. Without runway data, the runway-length filter has no effect.

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

### Importing an existing logbook
- **File → Open Logbook…** — open a previously saved native logbook (`.xml`)
- **File → Save Logbook…** — save the current logbook
- **File → Import Foreign Logbook…** — import the `ArrayOfFlightRecord` XML format produced by other flight logbook applications (duplicates are filtered automatically)

### Map filters
| Filter | Description |
|---|---|
| Min / Max runway | Minimum and maximum runway length (ft or m) |
| Instrument approach | Show only airports with ILS or equivalent |
| Centre ICAO + Radius | Show only airports within N nm of the given airport |

Click **Apply Filters** to update the map. **Clear** resets all filters.

### Airport search
A search box is shown in the top-right corner of the map. Type an ICAO code or any part of an airport name — a live dropdown updates after every keystroke. Click a result (or press Down / Enter) to zoom the map to that airport and open its info popup. Press Escape to clear the search.

## Project structure

```
DestinationPlanner/
├── Models/          FlightRecord, Airport, AircraftType
├── ViewModels/      MainViewModel, MapViewModel, LogbookViewModel
├── Views/           MapView.xaml, LogbookView.xaml
├── Services/        LogbookService, AirportDataService, SimConnectService
├── Serialization/   NativeLogbookSerializer, ForeignLogbookImporter
├── Schemas/         NativeLogbook.xsd, ForeignLogbook.xsd
└── Helpers/         GeoHelper, RelayCommand
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
      <AircraftType>Airplane</AircraftType>
      <DepartureIcao>EGLL</DepartureIcao>
      <ArrivalIcao>EGCC</ArrivalIcao>
      <BlockOffUtc>2026-04-26T10:30:00Z</BlockOffUtc>
      <BlockOnUtc>2026-04-26T11:45:00Z</BlockOnUtc>
    </Flight>
  </Flights>
</FlightLogbook>
```
