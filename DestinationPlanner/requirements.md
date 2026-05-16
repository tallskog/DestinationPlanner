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
