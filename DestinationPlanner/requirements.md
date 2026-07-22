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

## US34: Airport civil/military/private classification via Navigraph
**As a pilot**, I want to see whether an airport is civil, military, or privately operated, and filter the map by that classification, so I can plan flights around appropriate airport types.

**Acceptance criteria:**
- Classification data is sourced from Navigraph's Navigation Data API (DFD v2 format), matched to existing airports by ICAO code (ARINC 424 field 5.177, "Public/Military Indicator")
- Airports with no Navigraph classification data available show as "Unclassified" and remain visible by default — the app behaves exactly as before for users who never sign in to Navigraph
- A new "Airport Type" filter group in the map sidebar offers Civil / Military / Private / Unclassified checkboxes, all checked by default
- The Airport Type filter applies uniformly to all map layers (all airports, logbook, departed, landed) via the same shared filter logic used by the runway/ILS/ATIS/radius filters
- Navigraph sign-in uses OAuth 2.0 Device Authorization Flow with PKCE — a code is displayed in-app and the user approves it in their own browser; the app never asks for a Navigraph password directly
- The sign-in dialog shows the user code, a button to open the browser, and a cancel option; on success it closes automatically, on denial/expiry/cancellation a clear message is shown
- The user is not required to sign in every launch: the OAuth refresh token is stored encrypted at rest (Windows DPAPI, current-user scope); the access token itself is never persisted, only kept in memory for the session
- If a stored refresh token fails to refresh (revoked/expired), the app clears it and only re-prompts sign-in when the user explicitly requests a Navigraph sync — it does not interrupt normal use
- The most recently downloaded Navigraph airport classification data is cached locally and re-applied automatically on the next launch, without requiring re-authentication
- Navigraph developer credentials (client ID/secret) are never committed to source control; they are read at runtime from a local file in the AppData folder — if absent, the Navigraph menu action shows a clear "not configured" message instead of failing

**Known gaps — pending Navigraph API access approval** (developer access request submitted 2026-07-22, awaiting approval):
- The exact `format` query value for DFD v2 packages in `GET /v1/navdata/packages`, and the exact JSON field names for the signed file URL / AIRAC cycle in that response, are unconfirmed. Currently guessed in `NavigraphDataService.DownloadCurrentPackageAsync` as `format=dfdv2`, `packages[0].cycle`, `packages[0].files[0].signed_url` — will need a fix-up once real responses are seen.
- Whether `tbl_pa_airports.airport_type` ever contains codes other than `C`/`M`/`P` in real data is unconfirmed (the `Unknown` fallback in `NavigraphDataService.ParseAirportTypes` should never trigger under normal use, but this is unverified).
- End-to-end device-flow sign-in, package download, and `.3sdb` parsing have not been tested against genuine Navigraph data.
- Multi-day refresh-token rotation behavior has not been verified under real usage.

---

## US35: Automated test coverage for core logic
**As a developer**, I want automated unit tests for the app's non-UI logic so that regressions are caught before they reach a manual test pass.

**Acceptance criteria:**
- An xUnit test project (`DestinationPlanner.Tests`) exists alongside the main project, referenced from `DestinationPlanner.slnx`, and runs via `dotnet test`
- Tests exercise pure logic and ViewModels via fakes for `IAirportDataService`, `ILogbookService`, `ISimConnectService`, and `INavigraphAuthService` — no real file I/O, network calls, or SimConnect/MSFS dependency
- `dotnet test` is run automatically whenever code changes are made (see CLAUDE.md Testing section) and must pass before a task is considered done
- If a test fails, or a new requirement would conflict with an existing one here, the user is consulted before proceeding — never silently resolved

**Coverage — implemented (101 tests as of the US35 backlog pass):**
- `AirportDataService.ApplyAirportTypes` — merge-by-ICAO, unmatched ICAOs stay Unclassified, case-insensitive lookup
- `Airport.Type` defaults to `Unclassified`
- `AirportDataService` CSV parsing (`ParseAirports`, `ApplyRunwayData`, `ApplyFrequencyData`) — instrument-approach heuristic per airport type/scheduled-service, malformed/short rows skipped, closed runways excluded, runway sort/longest-runway, ATIS frequency detection, and a regression guard for BUG-01's runway endpoint column mapping — US13, BUG-01
- `NavigraphDataService.ParseAirportTypes` — ARINC code → `AirportType` mapping (C/M/P/unrecognized/null), case-insensitive ICAO keys
- `NavigraphCredentials.ParseJson` — valid/missing/malformed/whitespace-only JSON handling
- `NavigraphTokenStore.Protect`/`Unprotect` — DPAPI round-trip, invalid input returns null instead of throwing
- `NavigraphAuthService` — device-flow field/endpoint correctness, a genuine PKCE round-trip check (SHA256(verifier) recomputed and compared against the code_challenge actually sent), authorization_pending retry, access_denied/expired_token/unrecognized error mapping, local device-code-expiry without contacting the server, cancellation, and refresh-token grant — made testable via a small refactor: `HttpClient` is now injected (defaults to a real client in production) instead of a hardcoded static instance, using a fake `HttpMessageHandler` in tests
- `MapViewModel` airport-type filter — default-all-visible, per-type exclusion, `ClearFiltersCommand` reset, and parity between `GetAllFilteredAirports()` and the shared `ApplySharedFilters()` path used by logbook/departed/landed layers (guards against a BUG-05-style regression)
- `NavigraphSignInViewModel` state machine — Success/Denied/Expired/Error/Cancelled transitions, `Completed` event firing once
- `GeoHelper` — `DistanceNm` (zero/symmetry/1-degree sanity range), `FeetToMeters`/`MetersToFeet` rounding, Mercator round-trip and the standard EPSG:3857 bound — US3, US5.1
- `LandingRatingHelper` — `ComputeBreakdown`/`ComputeStars` for each of the six scoring components at their flat (perfect/zero) regions plus one linear-interpolation region, missing-component display, and `Enrich`'s crosswind/runway-geometry/star assignment — US29
- `NativeLogbookSerializer` — full round-trip, omit-if-null on unset landing fields, loading a pre-v1.2 file with none of the extended landing elements, silently ignoring a legacy `AircraftType` element, and skipping one malformed `<Flight>` record without losing the rest of the file — US8, US-BC2, US-BC3
- `ForeignLogbookImporter` — date/time parsing, same-airport skip (US7), missing-field skip, and midnight-crossing arrival time — US9
- `LittleNavmapCsvImporter` — offset-aware UTC conversion, coordinate-style waypoint skip, same-airport skip, missing-required-column error, quoted-CSV-field parsing, and the `$$:`/`ATCCOM` aircraft-name cleanup rules — US19

**Coverage — not yet implemented (backlog):**
- UI rendering (XAML bindings, Mapsui layers, popups) and live SimConnect/MSFS behavior — verified manually, not by automated tests (see CLAUDE.md Testing section)
- `NavigraphDataService.DownloadCurrentPackageAsync` (package discovery + download) — same `HttpClient`-injection pattern as `NavigraphAuthService` would apply if this becomes worth testing before real Navigraph API access is confirmed
