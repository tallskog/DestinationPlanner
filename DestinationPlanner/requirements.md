# DestinationPlanner — Requirements

---

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

## US19: Live aircraft position marker on map
**As a pilot**, I want to see a live airplane symbol (✈) on the map that tracks my aircraft's real-world position from the flight simulator, so I can see where I am at a glance.

**Acceptance criteria:**
- When MSFS is connected via SimConnect, a ✈ symbol appears on the map at the aircraft's current lat/lon
- The symbol updates every second as the aircraft moves
- The symbol rotates to match the aircraft's true heading
- When the map is panned or zoomed, the marker repositions correctly
- When SimConnect disconnects, the marker disappears

---

## US20: Landing statistics capture
**As a pilot**, I want the app to automatically record my landing quality (vertical speed, G-force, airspeed, and wind) at the moment of touchdown, so I can track my landings over time.

**Acceptance criteria:**
- At the moment `SIM ON GROUND` transitions from false to true during a flight, the app captures: vertical speed (ft/min), G-force (g), indicated airspeed (kts), wind velocity (kts), wind direction (°)
- These values are stored on the `FlightRecord` that is saved when the flight completes
- Values are persisted to and loaded from the logbook XML file
- Older logbook files that do not have these fields open without error; the columns display "—"
- Manually entered flights display "—" in all landing stat columns

---

## US21: Landing statistics in logbook view
**As a pilot**, I want to see landing statistics as columns in the logbook flight list so I can compare landings across flights at a glance.

**Acceptance criteria:**
- Four new columns appear in the logbook DataGrid after "Duration": FPM, G, Spd, Wind
- FPM shows the vertical speed at touchdown formatted as e.g. `-320` (negative = descent)
- G shows G-force formatted as e.g. `1.2g`
- Spd shows indicated airspeed as e.g. `134kt`
- Wind shows wind as e.g. `12kt/270°`
- When a flight has no landing data, all four columns show `—`

---

## US22: Landing quality rating
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

## US23: Landing rating detail popup
**As a pilot**, I want to click on a flight's star rating in the logbook to see a detailed breakdown of how the rating was calculated, so I can understand which aspects of the landing were good or needed improvement.

**Acceptance criteria:**
- Clicking the star rating cell for a flight with rating data opens a modal dialog titled "Landing Rating — {FROM} → {TO} — {date}"
- The dialog shows a table with one row per scoring component (Vertical Speed, G-Force, Bank Angle, Pitch Angle, Centerline Dev., Touchdown Zone), each with its weight percentage, measured value, and individual score (0–100)
- Individual scores are colour-coded: green (≥80), orange (≥60), red (<60); components with no data show "N/A" in grey
- The dialog shows the overall weighted score and the final star display (e.g. `★★★★☆`)
- Flights with no rating data show `—`; clicking `—` does nothing (button disabled)
- No new logbook fields are required — all data is already stored by US22

---

## BUG-01: Runway CSV column mapping
**Root cause:** `AirportDataService.ApplyRunwayData` read columns 9/10/11 for LE heading/lat/lon and 15/16/17 for HE heading/lat/lon. The actual OurAirports runways.csv header places coordinates at 9/10 and headings at 12/18 (with elevation fields at 11/17 between them). The code treated latitude as heading and elevation as longitude, placing every runway in the wrong location (e.g. EFTU appeared near Japan, producing 14 million ft centerline deviation).

**Fix:** Column indices corrected to: LE lat=9, lon=10, heading=12; HE lat=15, lon=16, heading=18. Existing logbook records that captured wrong geometry values are unaffected (stored values are not recomputed on load).
