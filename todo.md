# DestinationPlanner — To Do

## In progress / next up

## Map tab

- [x] **ICAO prefix filter** — filter airports by ICAO prefix (e.g. EF,ES for Finland & Sweden). Apply via the "Apply Filters" button.
- [x] **Zoom-dependent circle size** — airport markers now scale smoothly with zoom level (large at city level, small dots at continent view).
- [ ] **Highlight logbook airports differently** — logbook airports are already orange; consider adding a label or a different shape to distinguish them more clearly at low zoom.
- [ ] **Viewport-based airport count** — status bar currently shows count of all filtered airports; narrow it to only those visible in the current viewport (needs viewport-change detection; see architecture notes).

## Logbook tab

- [ ] **Edit / delete a flight** — right-click context menu on a DataGrid row.
- [ ] **Export to CSV** — useful for importing into spreadsheets or other tools.

## General

- [ ] **Persist last-used logbook path** — auto-load on startup using `Properties.Settings` or a small config file.
- [ ] **Persist airport data path** — same idea; remember the folder so the user doesn't have to browse every time.
- [ ] **About / help dialog** — links to OurAirports download page and MSFS SDK page.

## Done

- [x] **SimConnect integration** — `SimConnectService` connects to MSFS 2024 via `BRAKE PARKING INDICATOR` sim variable. Block-off (brake released) and block-on (brake set) are detected; nearest airport within 15 nm is resolved from the OurAirports data. `FlightCompleted` event flows to `LogbookService.AddFlight()`. App auto-reconnects every 10 s. Connection status shown in the window status bar.

## Bugs / known issues



## Wishlist for new features
- [ ] **Show Metar information on airport tooltip click** - show current METAR information when user clicks a airport marker in addition to ICAO, name, runway length and ILS capability
- [ ] **Weather overlay on map** - show windbarbs and rain in map if user selects to show weather overlay
- [ ] **Improve radius view** - when a radius is selected, centre should be highlited somehow and also radius circle could be drawn to map view
- [ ] **Status bar at the bottom of the scree** - In future status bar should show the connection status to simulator. Also the logbook used should be shown in status bar
- [ ] **Default logbook** - Usage of the logbook should be improved such a way that program remembers what logbook is used when the program is closed and started again. When the program is started for the first time, it should first ask the user to set the logbook to be used.
- [ ] **Wildcards support in logbook** - Wildcards should be supported in logbook view with departure and arrival