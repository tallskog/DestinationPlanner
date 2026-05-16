# DestinationPlanner — Claude Instructions

## Project overview
C# .NET 8 WPF application. MSFS 2024 flight logbook with SimConnect integration and Mapsui map.
Solution: `DestinationPlanner.sln`, project: `DestinationPlanner/DestinationPlanner.csproj`.

## Key architecture
- **SimConnect**: window-message-based, 1 Hz data via `SimConnectService`. SimData struct uses `LayoutKind.Sequential, Pack=1` — field order must match `AddToDataDefinition` call order exactly.
- **Map**: Mapsui 5.0.2 (`Mapsui.UI.Wpf`). Use `GeoHelper.LonToMercatorX/LatToMercatorY` (not `SphericalMercator`). Moving overlays (aircraft marker, selection line) go on the WPF `SelectionOverlay` Canvas, not in a Mapsui layer.
- **Logbook**: XML, namespace `urn:destination-planner:logbook:v1`. Serialized by `NativeLogbookSerializer`.

## Serializer rules — always follow these when adding logbook fields
1. New properties on `FlightRecord` must be **nullable** (`double?`, `string?`, etc.).
2. In `ToXml`: emit the element **only when the value is non-null** (omit = backward-compatible).
3. In `FromXml`: always use `?.Value` with a safe fallback — **never `!.Value`** on new fields.
4. Parse nullable numbers with the `ParseNullDouble` helper.
5. Bump the schema version attribute (`version="1.x"`) when shipping a format change.

## AppData path convention
- **Release builds**: `%LocalAppData%\DestinationPlanner\`
- **Debug builds**: `%LocalAppData%\DestinationPlanner-dev\`  (`#if DEBUG` in `AppDataHelper.cs`)

This keeps dev and installed-release logbooks completely separate.

## Requirements tracking
- When a plan is finalized and approved, update `DestinationPlanner/requirements.md` with the user stories and acceptance criteria from the plan before starting implementation.
- Plan must not break the existing requirements. If new requirement conflicts with an older requirement, it must be checked which one to follow or modify existing accordingly.

## README.md
- `DestinationPlanner/README.md` must be checked and updated if needed every time when implementing a change

## Mapsui 5.0.2 gotchas
- `SymbolStyle` is available; `VectorStyle` is an alternative.
- No public viewport-changed event — load all filtered airports into `MemoryLayer` at once.
- `Map.ViewportInitialized` event for initial centering.

## Build
`dotnet build DestinationPlanner/DestinationPlanner.csproj` — must pass with zero errors before marking any task done. The 9 NU1701 warnings about OpenTK are pre-existing and can be ignored.
