# DestinationPlanner — Requirements

This file consolidates two requirement documents that had diverged (a flat list at the repo root and this structured file). Section 1 preserves the original flat list verbatim — its story numbers (`US1`–`US25.4`) are referenced directly in code comments (e.g. `US13`, `US15.1`–`US15.3`), so they are **not** renumbered. Section 2 preserves the original structured write-ups, renumbered to continue after `US25.4` (their original numbers were never referenced in code) so nothing collides.

---

## 1. Core requirements (US1–US25.4)

US1: Application shall be built using C#
US2: WPF shall be used as a UI framework
US3: Application shall have a map display that is possible to zoom in/out with mouse wheel, and drag around
US4: Map shall show (higlighted) airports in the visible area
US5: User is able to filter the airports
US5.1: Based on runway length (can set min and max length)
US5.1.1: User can select whether feets or meters is used when giving min and max runway length
US5.2: Based on instrument approach capabilites of the airport
US5.3: By first selecting certain airport and then giving a radius in nm
US6: Application shall be able to connect to msfs2024 and collect information from a flight with following information
US6.1: Date of flight
US6.2: Type of the aircraft (aircraft, helicopter)
US6.2: Time or releasing of the handbrake
US6.3: Departure airport
US6.4: Time of setting the handbrake after arrival
US6.5: Arrival airport
US6.6: Tracking of the flight is not needed
US7: If depature and arrival are the same airport, the flight shall not be collected
US8: All flights shall be stored to an external file (it shall be decided whether json or xml format shall be used), which will work as flight logbook for this application
US9: User might have already lots of flights done, it shall be possible to import a logbook file (either in json or xml format) created by another application
US9.1: When importing logbook, it shall be checked that no duplicates exists
US10: User shall be able to investigate the logbook in text format by setting different kind of filters, se US6.1 - US6.5. This investigation shall be in another tab from the map
US11: The airports found from the logbook shall be also indicated in map. The filters that can be used to investigate the logbook, shall also be available in map view
US12: The application shall use appData/Local/DestinationPlanner folder to persistently store configuration, logbook and necessary airport information files. The folder shall be created if it doesn't exist when the app is started
US13: When user ask app to load airport data, the files shall be copied to appdata folder and shall be used from there by default. If user wants to update the airport files, user will again ask the app to load airport data and app fill copy the files to appdata folder and overwrite the existing airport files
US14: The logbook shall be written to appdata folder. Flight information shall be automatically stored to the file, user doesn't have to remember to save it. If no logbook exist in appdata folder, application shall create a default empty logbook. The default logbook name shall be logbook-<dd>-<mm>-<yyyy>.xml (current date shall be used in filename).
US15: User shall have an option to import and export logbook in format known to application.
US15.1: When importing, a new logbook shall be created. The filename format shall follow the same rules as with default logbook. If file already exists, a running number shall be inserted between the date and word "logbook"
US15.2: When exporting, user shall be given an option to select where the logbook shall be written and possibly change the logbook file name
US15.3: When there are more than one logbook file in appdata folder, application shall ask which one to use when starting up. If only one logbook file exists, app will use that automatically
US16: When clicking an airport from a map, a popup window shall appear anchored near the airport marker. The popup shall show:
US16.1: The ICAO code and name of the airport
US16.2: Each individual runway with its length in feet (sorted longest first). If no runway data is available, "N/A" is shown.
US16.3: The current METAR fetched from an external source (aviationweather.gov). While loading, "METAR: Loading…" is shown. If no METAR is available, "METAR: Not available" is shown.
US17: It shall be possible to select two airports simultaneously.
US17.1: Clicking the left mouse button on an airport opens a single primary popup (blue border). Any previously selected secondary airport is cleared.
US17.2: Clicking Ctrl+left mouse button on an airport opens a secondary popup (orange border) alongside the primary. If no primary is selected yet, the clicked airport becomes the primary.
US17.3: Clicking Ctrl+left mouse again replaces the secondary airport with the newly clicked one.
US17.4: When two airports are selected, a dashed line is drawn between them on the map with the distance in nautical miles shown at the midpoint.
US17.5: Both popups are anchored near their respective airport markers and move with the map when the user pans or zooms.
US17.6: Clicking an empty area on the map closes both popups and clears the selection.
US17.7: [DONE] Popups should follow the the window focus. If program is minimized, popup should follow. Also if another window is switched on top of this app, popups should not stay on top 
US17.8: [DONE] If the main app window is moved, popups should follow and be anchored to airport
US18: [DONE] I want to user to have a possibility to use search in map screen
US18.1: [DONE] User could use ICAO code or airport name as search key
US18.2: [DONE] Once user starts to enter search key, a list of airports found so far has been shown. List shall be updated after every key entered
US18.3: [DONE] User will select the airport from the drop down list
US18.4: [DONE] Once airport is selected, map will center and zoom in for the selected airport
US19: [DONE] Application shall support importing logbooks exported from Little Navmap (CSV format) via the existing "Import Foreign Logbook" menu. Format is auto-detected from file extension.
US20: [DONE] AircraftType (Airplane/Helicopter) shall be removed from the logbook data model. Existing logbook files that contain the element shall load without errors (element silently ignored).
US21: [DONE] The logbook view shall display the most recent flight at the top (sorted by block-off time descending).
US22: [DONE] After importing a foreign logbook, newly added rows shall be highlighted in light green in the logbook view. The highlight is session-only (not persisted) and is cleared when the user clicks "Clear Filters".
US23: [DONE] Duplicate detection during import shall handle the case where the same flight exists with non-overlapping but similar block times (same date, same route, duration within 3 minutes). The original internal logbook entry shall be kept.
US24: [DONE] User shall be able to switch the active logbook at runtime via File → Open Logbook… without restarting the application.
US25: [DONE] User shall be able to download airport data directly from within the application without manually downloading and selecting CSV files.
US25.1: [DONE] The application shall provide a "Download Airport Data" menu item under File that fetches airports.csv, runways.csv, and airport-frequencies.csv from the OurAirports GitHub repository (https://github.com/davidmegginson/ourairports-data).
US25.2: [DONE] The downloaded files shall be saved to the AppData folder, overwriting any existing files, and loaded immediately after download.
US25.3: [DONE] If optional files (runways.csv, airport-frequencies.csv) fail to download, the error shall be silently ignored and only airports.csv is required to succeed.
US25.4: [DONE] The application window shall be disabled during the download to prevent concurrent operations.

---

## 2. Detailed feature specs & bug postmortems (US26–US34, BUG-01–BUG-05)

## US-BC1: Dev/Release logbook isolation
**As a developer**, I want the debug build to use a separate AppData folder (`DestinationPlanner-dev\`) so that running the dev version never corrupts or interferes with the logbook used by the installed release version.

**Acceptance criteria:**
- Debug builds read/write `%LocalAppData%\DestinationPlanner-dev\`
- Release builds read/write `%LocalAppData%\DestinationPlanner\`
- Starting the debug build does not modify any files in the release folder

---

## US-BC2: Graceful logbook load failure
**As a user**, I want the application to show a clear error message if the logbook file cannot be loaded (e.g. it was saved by a newer version), instead of crashing silently on startup.

**Acceptance criteria:**
- If `NativeLogbookSerializer.Load` throws, a `MessageBox` is shown explaining the failure
- The app starts with an empty logbook rather than crashing
- The error message tells the user the file may have been written by a newer version

---

## US-BC3: Forward-compatible XML serializer
**As a developer**, I want new logbook fields to be optional in the XML format so that older versions of the app can open logbooks saved by newer versions without crashing.

**Acceptance criteria:**
- New `FlightRecord` properties are nullable
- New XML elements are omitted when the value is null (old files stay valid)
- `FromXml` uses `?.Value` with safe defaults for all new fields — never `!.Value`
- A per-record try-catch in `Load` skips malformed records instead of aborting the whole file
- Schema version attribute is bumped when a format change ships

---

## US26: Live aircraft position marker on map
**As a pilot**, I want to see a live airplane symbol (✈) on the map that tracks my aircraft's real-world position from the flight simulator, so I can see where I am at a glance.

**Acceptance criteria:**
- When MSFS is connected via SimConnect, a ✈ symbol appears on the map at the aircraft's current lat/lon
- The symbol updates every second as the aircraft moves
- The symbol rotates to match the aircraft's true heading
- When the map is panned or zoomed, the marker repositions correctly
- When SimConnect disconnects, the marker disappears

---

## US27: Landing statistics capture
**As a pilot**, I want the app to automatically record my landing quality (vertical speed, G-force, airspeed, and wind) at the moment of touchdown, so I can track my landings over time.

**Acceptance criteria:**
- At the moment `SIM ON GROUND` transitions from false to true during a flight, the app captures: vertical speed (ft/min), G-force (g), indicated airspeed (kts), wind velocity (kts), wind direction (°)
- These values are stored on the `FlightRecord` that is saved when the flight completes
- Values are persisted to and loaded from the logbook XML file
- Older logbook files that do not have these fields open without error; the columns display "—"
- Manually entered flights display "—" in all landing stat columns

---

## US28: Landing statistics in logbook view
**As a pilot**, I want to see landing statistics as columns in the logbook flight list so I can compare landings across flights at a glance.

**Acceptance criteria:**
- Four new columns appear in the logbook DataGrid after "Duration": FPM, G, Spd, Wind
- FPM shows the vertical speed at touchdown formatted as e.g. `-320` (negative = descent)
- G shows G-force formatted as e.g. `1.2g`
- Spd shows indicated airspeed as e.g. `134kt`
- Wind shows wind as e.g. `12kt/270°`
- When a flight has no landing data, all four columns show `—`

---

## US29: Landing quality rating
**As a pilot**, I want each flight to display a 1–5 star landing quality rating so I can quickly assess and compare my landings.

**Acceptance criteria:**
- After a flight completes, the logbook shows a star rating (★★★★☆ style) in a "Rating" column
- The rating is a composite of six factors: vertical speed, G-force, bank angle at touchdown, pitch at touchdown, centerline deviation, and touchdown zone accuracy (first third of runway)
- Bank angle and pitch are captured via two new SimConnect variables (`PLANE BANK DEGREES`, `PLANE PITCH DEGREES`)
- Centerline deviation is computed from the touchdown lat/lon vs. the nearest runway centerline using OurAirports `runways.csv` endpoint data
- Touchdown zone accuracy is the along-track distance from the threshold expressed as a percentage of runway length; landing in the first 33% scores highest
- Score → stars: ≥85→5, ≥70→4, ≥50→3, ≥30→2, else 1
- If a component is unavailable (e.g. no runway data), its weight is redistributed among available components
- All new fields (`LandingHeadingDeg`, `LandingBankAngleDeg`, `LandingPitchAngleDeg`, `LandingCrosswindKts`, `LandingCenterlineDeviationFt`, `LandingTouchdownZonePct`, `LandingStars`) are persisted in the XML logbook (schema v1.2) using the omit-if-null pattern
- Pre-v1.2 logbook files open without error; the Rating column shows `—` for those flights
- The Rating column supports sorting by star count

---

## US30: Landing rating detail popup
**As a pilot**, I want to click on a flight's star rating in the logbook to see a detailed breakdown of how the rating was calculated, so I can understand which aspects of the landing were good or needed improvement.

**Acceptance criteria:**
- Clicking the star rating cell for a flight with rating data opens a modal dialog titled "Landing Rating — {FROM} → {TO} — {date}"
- The dialog shows a table with one row per scoring component (Vertical Speed, G-Force, Bank Angle, Pitch Angle, Centerline Dev., Touchdown Zone), each with its weight percentage, measured value, and individual score (0–100)
- Individual scores are colour-coded: green (≥80), orange (≥60), red (<60); components with no data show "N/A" in grey
- The dialog shows the overall weighted score and the final star display (e.g. `★★★★☆`)
- Flights with no rating data show `—`; clicking `—` does nothing (button disabled)
- No new logbook fields are required — all data is already stored by US29

---

## US31: Configurable SimConnect data sampling rate
**As a developer/user**, I want to control how frequently SimConnect data is sampled so that landing detection accuracy can be tuned without recompiling the application.

**Acceptance criteria:**
- A `settings.json` file in the AppData directory controls `SimDataRateHz` (default: 60)
- Values > 1 Hz use `SIMCONNECT_PERIOD.VISUAL_FRAME`; the interval is computed as `max(1, round(60 / Hz))`
- A value of 1 (or less) falls back to `SIMCONNECT_PERIOD.SECOND`
- The file is created automatically with defaults on first run
- Changing the value takes effect on the next application start

---

## US32: Persist last-used logbook across sessions
**As a user**, I want the application to remember which logbook I was using so that I do not have to select it again every time I start the app.

**Acceptance criteria:**
- On startup the app reads `LastLogbookPath` from `settings.json`; if the file exists on disk it is opened directly without showing any dialog
- If the saved path no longer exists (file deleted/moved), the app falls back to the normal selection logic (auto-select if only one exists; show dialog if multiple exist)
- After selecting a logbook via **File → Open Logbook…**, the new path is saved to `settings.json` immediately
- The same `settings.json` file is used for all settings (US31)

---

## BUG-02: Aircraft map marker heading rotation
**Root cause:** The ✈ glyph (U+2708) renders pointing to the right (East) in most Windows fonts. Applying `RotateTransform.Angle = headingDegrees` directly meant a heading of 0° (North) left the nose pointing East; the marker appeared rotated 90° counter-clockwise from the correct orientation.

**Fix:** Applied a −90° base offset: `Angle = headingDegrees − 90`, so the glyph's nose points North at heading 0 and rotates correctly for all headings.

---

## BUG-03: Landing vertical speed captures approach rate instead of touchdown rate
**Root cause:** The rolling window searched the last 5 seconds for the most negative FPM and used that as the touchdown rate. For landings with a proper flare the steepest descent occurs during the approach, not at contact, so the window captured the approach rate (~370 fpm) rather than the actual touchdown rate (~87 fpm).

**Fix:** Track `_lastAirborneVerticalSpeed` — updated every frame while the aircraft is not on the ground. At touchdown this value holds the descent rate from the final airborne frame, which is the actual contact velocity. The landing window is now G-force-only (2 s, down from 5 s) so the impact G-force peak is still captured accurately.

---

## BUG-04: Landing pitch angle sign convention
**Root cause:** MSFS SimConnect `PLANE PITCH DEGREES` returns negative values for nose-up attitudes and positive for nose-down. The `ScorePitch` function and the logbook display both assumed the opposite (standard aviation convention: positive = nose-up), causing flared landings to score 0 for pitch and nose-down touchdowns to score 100.

**Fix:** Negate `sd.PitchDegrees` when storing the landing stats so the persisted value follows aviation convention (positive = nose-up). No changes to the scorer or display were required.

---

## US33: Departure / landing status rings on the map
**As a pilot**, I want visited airports on the map to visually distinguish whether I departed from them, landed at them, or both, so I can see my flight history at a glance without opening the logbook.

**Acceptance criteria:**
- A visited airport from which I departed has a green ring drawn around the orange dot
- A visited airport at which I landed has a red ring drawn around the orange dot
- An airport where I both departed and landed shows both rings; the green ring is inner (closer to the dot) and the red ring is outer, with a small gap between the two rings
- All active map filters (runway, ILS, ATIS, radius, ICAO prefix) apply equally to the ring layers and the orange dot layer
- A "Map Legend" panel in the sidebar explains all five symbol states: unvisited (blue dot), visited (orange dot), departed (green ring), landed (red ring), and departed & landed (both rings)
- Clicking anywhere within a ring (not just the central dot) opens the airport info popup

---

## BUG-05: Map filters not applied to visited-airport layers
**Root cause:** `GetLogbookAirports()` applied only the radius and ICAO-prefix filters to visited airports. The runway length, ILS, and ATIS filters were intentionally skipped (with a comment calling them "destination-search criteria"). This meant setting a minimum runway length still left short-runway airports visible on the map as orange dots.

**Fix:** All filter criteria (radius, runway min/max, ILS, ATIS, ICAO prefix) are now applied uniformly via a shared `ApplySharedFilters` helper used by `GetLogbookAirports`, `GetDepartedAirports`, and `GetLandedAirports`.

---

## BUG-01: Runway CSV column mapping
**Root cause:** `AirportDataService.ApplyRunwayData` read columns 9/10/11 for LE heading/lat/lon and 15/16/17 for HE heading/lat/lon. The actual OurAirports runways.csv header places coordinates at 9/10 and headings at 12/18 (with elevation fields at 11/17 between them). The code treated latitude as heading and elevation as longitude, placing every runway in the wrong location (e.g. EFTU appeared near Japan, producing 14 million ft centerline deviation).

**Fix:** Column indices corrected to: LE lat=9, lon=10, heading=12; HE lat=15, lon=16, heading=18. Existing logbook records that captured wrong geometry values are unaffected (stored values are not recomputed on load).

---

## US34: Airport type classification via OpenAIP
**As a pilot**, I want to see whether an airport is civil, military, a heliport, or privately operated, and filter the map by that classification, so I can plan flights around appropriate airport types.

**Background:** originally specified against Navigraph's Navigation Data API, but Navigraph denied the developer access request (submitted 2026-07-22). An alternative of reading LittleNavMap's local `little_navmap_navigraph.sqlite` cache was investigated and rejected: Navigraph's own `cycle_info.txt` license text explicitly forbids 3rd-party apps from reading that file, and independent of licensing, the compiled schema has no reliable civil/military/private signal (`is_military` misses known military fields; there is no "private" field at all). OpenAIP (openaip.net) was chosen instead — confirmed against their live OpenAPI spec (`api.core.openaip.net`), it offers an equivalent-or-richer `type`/`private` classification via simple API-key auth.

**Acceptance criteria:**
- Classification data is sourced from OpenAIP's `GET /airports` endpoint (`api.core.openaip.net`), matched to existing airports by ICAO code (`icaoCode` field)
- Mapping from OpenAIP data to `AirportType`, in priority order:
  1. `private == true` → `Private` (wins regardless of `type`)
  2. `type` 4 or 7 (Heliport Military / Heliport Civil) → `Heliport`
  3. `type` 5 (Military Aerodrome) → `Military`
  4. `type` 0, 2, 3, or 9 (Airport civil/mil, Airfield Civil, International Airport, Airfield IFR) → `Civil`
  5. `type` 1, 6, 8, 10, 11, 12, or 13 (Glider Site, Ultra Light, Aerodrome Closed, Airfield Water, Landing Strip, Agricultural Landing Strip, Altiport) → `Other`
  6. `type` null/missing (OpenAIP has the airport but no type on record) → `Unknown`
- Airports with no OpenAIP record at all show as "Unclassified" and remain visible by default — the app behaves exactly as before for users who never configure an OpenAIP API key
- The "Airport Type" filter group in the map sidebar offers seven checkboxes — Civil / Military / Heliport / Private / Other (Special-Use) / Unknown / Unclassified — all checked by default
- The Airport Type filter applies uniformly to all map layers (all airports, logbook, departed, landed) via the same shared filter logic used by the runway/ILS/ATIS/radius filters
- Authentication is a single OpenAIP API key, sent as the `x-openaip-api-key` request header — no OAuth flow, no sign-in dialog, no stored session
- The API key is never committed to source control; it is read at runtime from a local, un-committed file (`openaip.local.json`) in the AppData folder — if absent, the "Update Airport Type Data (OpenAIP)…" menu action prompts the user for the key in-app (with a link to `accounts.openaip.net` to get one) and saves it to that file so future syncs don't require re-entering it; cancelling the prompt aborts the sync without error
- The most recently fetched OpenAIP airport classification data is cached locally (`openaip-airport-types.json` in AppData) and re-applied automatically on the next launch, without requiring a network call
- OpenAIP's data is licensed CC BY-NC 4.0 (Attribution-NonCommercial): the app displays an in-app attribution link to https://www.openaip.net near the Airport Type filter, and the feature is only used for DestinationPlanner's non-commercial, free distribution

**Known gaps:**
- Real-world OpenAIP rate limits under a free-tier API key have not been measured against a full worldwide `/airports` pagination pull; if this becomes slow or throttled, the fetch may need to be scoped (e.g. per-country) rather than global.

---

## US35: Automated test coverage for core logic
**As a developer**, I want automated unit tests for the app's non-UI logic so that regressions are caught before they reach a manual test pass.

**Acceptance criteria:**
- An xUnit test project (`DestinationPlanner.Tests`) exists alongside the main project, referenced from `DestinationPlanner.slnx`, and runs via `dotnet test`
- Tests exercise pure logic and ViewModels via fakes for `IAirportDataService`, `ILogbookService`, and `ISimConnectService` — no real file I/O, network calls, or SimConnect/MSFS dependency
- `dotnet test` is run automatically whenever code changes are made (see CLAUDE.md Testing section) and must pass before a task is considered done
- If a test fails, or a new requirement would conflict with an existing one here, the user is consulted before proceeding — never silently resolved

**Coverage — implemented (101 tests as of the US35 backlog pass):**
- `AirportDataService.ApplyAirportTypes` — merge-by-ICAO, unmatched ICAOs stay Unclassified, case-insensitive lookup
- `Airport.Type` defaults to `Unclassified`
- `AirportDataService` CSV parsing (`ParseAirports`, `ApplyRunwayData`, `ApplyFrequencyData`) — instrument-approach heuristic per airport type/scheduled-service, malformed/short rows skipped, closed runways excluded, runway sort/longest-runway, ATIS frequency detection, and a regression guard for BUG-01's runway endpoint column mapping — US13, BUG-01
- `OpenAipDataService.MapType` — the full OpenAIP `type`/`private` → `AirportType` mapping table (private-override, Heliport, Military, Civil, Other, and null-type → Unknown), case-insensitive ICAO keys
- `OpenAipDataService.FetchAirportTypesAsync` — API-key header sent correctly, pagination stops at `totalPages`, items with no `icaoCode` skipped — made testable via `HttpClient` injection (defaults to a real client in production) with a fake `HttpMessageHandler` in tests
- `OpenAipCredentials.ParseJson` — valid/missing/malformed/whitespace-only JSON handling
- `MapViewModel` airport-type filter — default-all-visible across all seven categories, per-type exclusion (including Heliport/Other/Unknown), `ClearFiltersCommand` reset, and parity between `GetAllFilteredAirports()` and the shared `ApplySharedFilters()` path used by logbook/departed/landed layers (guards against a BUG-05-style regression)
- `GeoHelper` — `DistanceNm` (zero/symmetry/1-degree sanity range), `FeetToMeters`/`MetersToFeet` rounding, Mercator round-trip and the standard EPSG:3857 bound — US3, US5.1
- `LandingRatingHelper` — `ComputeBreakdown`/`ComputeStars` for each of the six scoring components at their flat (perfect/zero) regions plus one linear-interpolation region, missing-component display, and `Enrich`'s crosswind/runway-geometry/star assignment — US29
- `NativeLogbookSerializer` — full round-trip, omit-if-null on unset landing fields, loading a pre-v1.2 file with none of the extended landing elements, silently ignoring a legacy `AircraftType` element, and skipping one malformed `<Flight>` record without losing the rest of the file — US8, US-BC2, US-BC3
- `ForeignLogbookImporter` — date/time parsing, same-airport skip (US7), missing-field skip, and midnight-crossing arrival time — US9
- `LittleNavmapCsvImporter` — offset-aware UTC conversion, coordinate-style waypoint skip, same-airport skip, missing-required-column error, quoted-CSV-field parsing, and the `$$:`/`ATCCOM` aircraft-name cleanup rules — US19

**Coverage — not yet implemented (backlog):**
- UI rendering (XAML bindings, Mapsui layers, popups) and live SimConnect/MSFS behavior — verified manually, not by automated tests (see CLAUDE.md Testing section)

---

## US36: Persist map filter selections across sessions
**As a user**, I want the map filters I usually use to still be set the next time I open the app, so I don't have to re-enter the same runway length, airport type, and region criteria every session.

**Acceptance criteria:**
- Clicking **Apply Filters** saves the current filter values (min/max runway, unit, instrument approach, ATIS, centre ICAO + radius, show visited/not-visited, all seven airport-type checkboxes, ICAO prefixes) to `settings.json`
- On the next app launch, the map filters are pre-populated from the saved values instead of the hardcoded defaults
- Clicking **Clear** resets all filters to their original defaults (as before US36) and also saves that reset state to `settings.json`, so a cleared state stays cleared on next launch
- The same `settings.json` file is used for all settings (US31, US32)

**Note:** The "reset filters to defaults" button (**Clear**) already existed prior to this requirement (`ClearFiltersCommand`); US36 only adds persistence on top of the existing apply/clear behavior.

---

## US37: Scrollable map filter sidebar
**As a user**, I want to be able to apply map filters without first having to resize the window, so that growing the number of filter groups doesn't force a bigger window just to reach the Apply/Clear buttons.

**Acceptance criteria:**
- The **Apply Filters** / **Clear** buttons are pinned directly under the airport-data status line, at the top of the sidebar, and are always visible regardless of window height
- The filter groups (Runway Length, Capabilities, Airport Type, Visit Status, Radius Filter, ICAO Filter) and the Map Legend scroll independently inside a vertical `ScrollViewer` that fills the remaining sidebar height between the pinned buttons and the bottom status text
- The main window has `MinWidth="820"` / `MinHeight="480"` so it cannot be resized below a usable size; below that, the sidebar scrollbar (not further shrinking) is how additional filters are reached
- Verified manually: resizing the window down to 820×480 keeps the buttons, map, and status bar visible, with the filter list scrollable via the sidebar's vertical scrollbar

---

## BUG-06: Tests overwrote the real dev settings.json, wiping the remembered logbook path
**Root cause:** `AppDataHelper.AppDataPath` resolves to `%LocalAppData%\DestinationPlanner-dev\` under `#if DEBUG` — the same folder a real Debug build of the app uses. `DestinationPlanner.Tests` also builds in Debug, so `AppSettingsService.Save`, called from `MapViewModel.SaveFiltersToSettings()` on Apply/Clear, wrote directly to that shared real file with no test isolation. The `ClearFiltersCommand_ResetsAirportTypeFiltersToAllVisible` test built its `MapViewModel` with a blank `new AppSettings()`, so running `dotnet test` overwrote the developer's real `settings.json`, nulling out `LastLogbookPath` and resetting every filter to default — surfacing as the app re-prompting for a logbook on next launch, and would have looked like a lost preference (not lost logbook data — the XML files themselves were never touched).

**Note:** installed/production (Release) builds were never at risk — Release resolves to a separate `DestinationPlanner` folder that `dotnet test` never touches. This only affected the shared dev/test environment.

**Fix:** Added an `internal` test seam, `AppSettingsService.TestOverridePath`, that redirects `Save`/`Load` to a caller-supplied path instead of the real AppData settings file. `MapViewModelAirportTypeFilterTests` sets it to a per-run temp file in its constructor and clears/deletes it in `Dispose`, so the test suite can exercise `AppSettingsService.Save` without ever touching a real settings.json.

---

## US38: Precipitation radar overlay on the map
**As a pilot**, I want to see current precipitation (rain, snow, sleet, or hail) on the map when I'm planning a flight, so I can factor weather into airport/destination choices at the moment I'm planning — not as a continuously-updating live feed.

**Acceptance criteria:**
- A **🌧 Precip** toggle button and a refresh (⟳) button are shown as an overlay on top of the map itself (top-left corner), not in the filter sidebar — this is a map layer toggle, not an airport filter
- Checking the toggle fetches the current radar frame from RainViewer's public API (`https://api.rainviewer.com/public/weather-maps.json`, no API key required) and adds it as a Mapsui tile layer directly above the OpenStreetMap base layer (so airport markers/rings/logbook layers still render on top of it)
- No background polling: the radar frame is fetched once when the toggle is checked, and only re-fetched when the user clicks refresh — matching the app's flight-planning use case (a snapshot of current conditions), not a live weather feed
- Unchecking the toggle removes the layer and clears the status/attribution text
- A small status text next to the buttons shows the observed time of the currently-displayed frame (local time, `HH:mm`); shows "unavailable" if the fetch fails (network error, malformed response, etc. — never throws)
- An attribution line ("Radar: RainViewer", linking to rainviewer.com) is shown whenever the overlay is active, per RainViewer's free-tier attribution requirement — RainViewer is the data provider's name (a proper noun, left as-is) even though the app's own UI/code calls the feature "precipitation" since the radar covers rain, snow, sleet, and hail, not just rain
- RainViewer's public API is free for personal/non-commercial use (same license class as the OpenAIP integration, US34) with no signup/API key needed

**Coverage:** `PrecipitationRadarService.ParseLatestFrame` unit-tested for the happy path (tile URL template built from the last/most-recent `radar.past` frame) and failure modes (missing host/radar/past, empty past array, missing path/time fields, malformed JSON) — all return `null` rather than throwing. `GetLatestFrameAsync` tested end-to-end with a fake `HttpMessageHandler` for both an HTTP error status and a valid response. UI wiring (the toggle/refresh buttons and the Mapsui layer add/remove) is verified manually, not by automated tests — see CLAUDE.md Testing section.

---

## BUG-07: "Zoom Level Not Supported" placeholder tiles at deep map zoom
**Root cause:** RainViewer's radar tiles only exist up to zoom level 7. Requesting a deeper zoom doesn't 404 — their server returns HTTP 200 with a literal placeholder PNG containing the text "Zoom Level Not Supported", which BruTile/Mapsui renders like any other tile. Confirmed directly: fetching the real tile coordinates for a location with live rain (over the UK) at zoom 5–7 returned genuine varying radar image data, while zoom 8 and above returned the identical 1370-byte placeholder image regardless of location. Our original `HttpTileSource` used a default-range `GlobalSphericalMercator()` schema (effectively up to zoom 18, matching the OSM base layer), so zooming in past 7 filled the visible area with that placeholder text instead of radar or a blank tile.

**Fix:** Capped the precipitation layer's tile schema to `GlobalSphericalMercator(0, 7)` in `MapView.xaml.cs` (`LoadPrecipitationOverlayAsync`). With the schema itself reporting a max of 7 resolution levels, BruTile can never request a deeper level — it stretches (over-zooms) the deepest real tile to fill the view instead, which is the standard, expected behavior for any raster tile layer zoomed past its source's native resolution (the same mechanism used implicitly by the OSM base layer, which just happens to support deeper zoom than this app ever needs).

**Note:** `ToggleButton` automation events are `Checked`/`Unchecked`, not `Click` — `Click` does not fire when a ToggleButton's state is changed programmatically (e.g. via UI Automation's `IToggleProvider.Toggle()`), only `Checked`/`Unchecked` do. The rain toggle handlers use `Checked`/`Unchecked` for this reason.
