# DestinationPlanner — To Do

## In progress / next up

## Map tab

- [x] **ICAO prefix filter** — filter airports by ICAO prefix (e.g. EF,ES for Finland & Sweden). Apply via the "Apply Filters" button.
- [x] **Zoom-dependent circle size** — airport markers now scale smoothly with zoom level (large at city level, small dots at continent view).
- [ ] **Highlight logbook airports differently** — logbook airports are already orange; consider adding a label or a different shape to distinguish them more clearly at low zoom.
- [ ] **Viewport-based airport count** — status bar currently shows count of all filtered airports; narrow it to only those visible in the current viewport (needs viewport-change detection; see architecture notes).

## Logbook tab

- [x] **Edit / delete a flight** — right-click context menu on a DataGrid row.
- [ ] **Export to CSV** — useful for importing into spreadsheets or other tools.

## General

- [ ] **Persist last-used logbook path** — auto-load on startup using `Properties.Settings` or a small config file.
- [ ] **Persist airport data path** — same idea; remember the folder so the user doesn't have to browse every time.
- [ ] **About / help dialog** — links to OurAirports download page and MSFS SDK page.

## Done

- [x] **SimConnect integration** — `SimConnectService` connects to MSFS 2024 via `BRAKE PARKING INDICATOR` sim variable. Block-off (brake released) and block-on (brake set) are detected; nearest airport within 15 nm is resolved from the OurAirports data. `FlightCompleted` event flows to `LogbookService.AddFlight()`. App auto-reconnects every 10 s. Connection status shown in the window status bar.
- [x] **Airport popup with runways and METAR (US16)** — clicking an airport marker opens a popup showing ICAO, name, each individual runway with its length, and live METAR fetched asynchronously from aviationweather.gov.
- [x] **Two-airport selection with distance line (US17)** — left-click sets primary airport (blue popup), Ctrl+left-click sets secondary (orange popup). A dashed line with distance in nm is drawn between them. Both popups follow the map on pan/zoom.
- [x] **SimConnect bogus departure fix** — 5-second stabilization window after connection suppresses loading-state jitter (stale coordinates and spurious brake transitions). Non-ICAO identifiers (e.g. `US-12381`) are also excluded from departure/arrival resolution.

## Bugs / known issues

## Wishlist for new features
- [ ] **Weather overlay on map** — show windbarbs and rain in map if user selects to show weather overlay
- [ ] **Improve radius view** — when a radius is selected, centre should be highlighted somehow and also a radius circle could be drawn on the map
- [ ] **Status bar at the bottom of the screen** — connection status to simulator and the active logbook name
- [ ] **Default logbook** — remember which logbook was used when the program is closed; on first launch, ask the user to set the logbook to be used
- [ ] **Wildcards support in logbook** — wildcards should be supported in logbook view with departure and arrival filters
- [ ] **Highlight logbook airports differently** — logbook airports are already orange; consider adding a label or a different shape to distinguish them more clearly at low zoom
- [ ] **Viewport-based airport count** — status bar currently shows count of all filtered airports; narrow it to only those visible in the current viewport