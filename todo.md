# DestinationPlanner — To Do

## In progress / next up

- [ ] **SimConnect integration** — wire up `SimConnectService` to receive parking-brake events from MSFS 2024.
  - Reference `SimConnect.dll` from the MSFS SDK (`SDK\SimConnect SDK\lib\static\SimConnect.dll`)
  - Subscribe to `PARKING_BRAKE` sim variable (on/off transitions)
  - Capture departure ICAO, arrival ICAO, block-off time (brake released) and block-on time (brake set)
  - Fire `FlightCompleted` event → `LogbookService.AddFlight()`
  - Filter out same-airport flights (already handled in `LogbookService`)

## Map tab

- [ ] **Airport tooltip on click** — show ICAO, name, runway length and ILS capability when the user clicks a marker. Use `MapControl.Info` event.
- [ ] **Highlight logbook airports differently** — logbook airports are already orange; consider adding a label or a different shape to distinguish them more clearly at low zoom.
- [ ] **Airport count in sidebar** — update status text to show how many airports are currently visible in the viewport (needs viewport-change detection; see architecture notes).

## Logbook tab

- [ ] **Edit / delete a flight** — right-click context menu on a DataGrid row.
- [ ] **Export to CSV** — useful for importing into spreadsheets or other tools.

## General

- [ ] **Persist last-used logbook path** — auto-load on startup using `Properties.Settings` or a small config file.
- [ ] **Persist airport data path** — same idea; remember the folder so the user doesn't have to browse every time.
- [ ] **About / help dialog** — links to OurAirports download page and MSFS SDK page.

## Bugs / known issues



## Wishlist for new features
- [ ] **Show Metar information on airport tooltip click** - show current METAR information when user clicks a airport marker in addition to ICAO, name, runway length and ILS capability
- [ ] **Weather overlay on map** - show windbarbs and rain in map if user selects to show weather overlay
- [ ] **Improve radius view** - when a radius is selected, centre should be highlited somehow and also radius circle could be drawn to map view
- [ ] **Status bar at the bottom of the scree** - In future status bar should show the connection status to simulator. Also the logbook used should be shown in status bar
- [ ] **Default logbook** - Usage of the logbook should be improved such a way that program remembers what logbook is used when the program is closed and started again. When the program is started for the first time, it should first ask the user to set the logbook to be used.
- [ ] **Wildcards support in logbook** - Wildcards should be supported in logbook view with departure and arrival