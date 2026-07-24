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

- [x] **Persist last-used logbook path** — remember which logbook was last active so it reopens automatically on next launch. Implemented via `AppSettings.LastLogbookPath` (`App.xaml.cs`); first launch with multiple logbook files prompts via `LogbookSelectionDialog`.
- [ ] **Persist airport data path** — same idea; remember the folder so the user doesn't have to browse every time.
- [ ] **About / help dialog** — links to OurAirports download page and MSFS SDK page.

## Done

- [x] **Little Navmap CSV import** — "Import Foreign Logbook" auto-detects `.csv` files as Little Navmap exports; parses ISO 8601 timestamps with timezone offset, filters coordinate-only waypoints, cleans raw ATC aircraft names.
- [x] **AircraftType removed** — `AircraftType` field removed from `FlightRecord` and all UI; old logbook XML files that contain `<AircraftType>` still load correctly (element silently ignored).
- [x] **Logbook sorted latest-first** — flight list always shows the most recent flight at the top.
- [x] **Import highlighting** — newly imported rows are highlighted in light green; clears on "Clear Filters" or next launch (session-only, never persisted).
- [x] **Improved duplicate detection** — catches near-duplicates where time intervals don't overlap but same-day same-route flights have durations within 3 minutes (e.g. backup logbooks with local-time-as-UTC storage).
- [x] **Runtime logbook selection** — File → Open Logbook… lets users switch the active logbook without restarting.
- [x] **SimConnect integration** — `SimConnectService` connects to MSFS 2024 via `BRAKE PARKING INDICATOR` sim variable. Block-off (brake released) and block-on (brake set) are detected; nearest airport within 15 nm is resolved from the OurAirports data. `FlightCompleted` event flows to `LogbookService.AddFlight()`. App auto-reconnects every 10 s. Connection status shown in the window status bar.
- [x] **Airport popup with runways and METAR (US16)** — clicking an airport marker opens a popup showing ICAO, name, each individual runway with its length, and live METAR fetched asynchronously from aviationweather.gov.
- [x] **Two-airport selection with distance line (US17)** — left-click sets primary airport (blue popup), Ctrl+left-click sets secondary (orange popup). A dashed line with distance in nm is drawn between them. Both popups follow the map on pan/zoom.
- [x] **Popup window focus behaviour (US17.7)** — popups are stripped of `WS_EX_TOPMOST` on open so other windows can cover them; popups hide when the main window minimizes and restore when it is un-minimized.
- [x] **Popup follows window move (US17.8)** — subscribes to `Window.LocationChanged` and nudges popup offsets to force WPF to reposition the layered popup HWND when the main window is dragged.
- [x] **Airport search on map (US18)** — search box overlay (top-right of map) filters all loaded airports by ICAO prefix or name substring on every keystroke, shows a live dropdown of up to 20 results. Selecting an airport zooms the map to a ~3 km wide view and opens the primary popup (ICAO, runways, live METAR) identical to a map click. Keyboard-navigable: Down arrow enters the list, Enter selects, Escape clears.
- [x] **SimConnect bogus departure fix** — 5-second stabilization window after connection suppresses loading-state jitter (stale coordinates and spurious brake transitions). Non-ICAO identifiers (e.g. `US-12381`) are also excluded from departure/arrival resolution.
- [x] **Default logbook** — last-used logbook path is remembered across restarts (`AppSettings.LastLogbookPath`, set/read in `App.xaml.cs`); on first launch with multiple logbook files, `LogbookSelectionDialog` prompts the user to pick one.
- [x] **Persist map filter selections** — filter values (runway length/unit, ILS, ATIS, radius, visited/not-visited, airport type, ICAO prefixes) are saved to `settings.json` on **Apply Filters** or **Clear** and restored on next launch. **Clear** still resets to the original defaults, as before — it now also persists that reset. See US36 in requirements.md.
- [x] **Scrollable filter sidebar** — Apply Filters/Clear are now pinned at the top of the sidebar (always visible without resizing); the filter groups + Map Legend below scroll independently. Window MinWidth/MinHeight (820x480) prevents shrinking below a usable size. See US37 in requirements.md.
- [x] **Precipitation radar overlay** — 🌧 Precip toggle + refresh button, top-left of the map (not the filter sidebar). Fetches the current frame from RainViewer's free public API on toggle/refresh only — no background polling, matching the flight-planning use case rather than a live weather feed. Named "precipitation" rather than "rain" since the radar also covers snow/sleet/hail. See US38 in requirements.md. Wind overlay (barbs) is still open, see wishlist below.

## Bugs / known issues

## Wishlist for new features
- [ ] **Wind overlay on map** — show wind barbs on the map (rain radar overlay already done — see US38 in requirements.md)
- [ ] **Improve radius view** — radius filtering itself works, but the centre airport still isn't visually highlighted and no radius circle is drawn on the map
- [ ] **Status bar at the bottom of the screen** — connection status to simulator already shown; still missing the active logbook name
- [ ] **Wildcards support in logbook** — wildcards should be supported in logbook view with departure and arrival filters (currently plain substring match)