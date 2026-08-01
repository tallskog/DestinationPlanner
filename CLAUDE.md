# DestinationPlanner — Claude Instructions

## Project overview
C# .NET 8 WPF application. MSFS 2024 flight logbook with SimConnect integration and Mapsui map.
Solution: `DestinationPlanner.slnx`, project: `DestinationPlanner/DestinationPlanner.csproj`, test project: `DestinationPlanner.Tests/DestinationPlanner.Tests.csproj`.

## Key architecture
- **SimConnect**: window-message-based, 1 Hz data via `SimConnectService`. SimData struct uses `LayoutKind.Sequential, Pack=1` — field order must match `AddToDataDefinition` call order exactly.
- **Map**: Mapsui 5.0.2 (`Mapsui.UI.Wpf`). Use `GeoHelper.LonToMercatorX/LatToMercatorY` (not `SphericalMercator`). Moving overlays (aircraft marker, selection line) go on the WPF `SelectionOverlay` Canvas, not in a Mapsui layer.
- **Logbook**: XML, namespace `urn:destination-planner:logbook:v1`. Serialized by `NativeLogbookSerializer`.

## Map info box parity (MapView, TripMapWindow, CandidateMapWindow, FlightLegMapWindow)
The Map tab (`MapView`, embedded `UserControl`), the Trip Plan map (`TripMapWindow`, standalone `Window`), the candidate-review map (`CandidateMapWindow`, standalone `Window`, US43), and the single-leg map opened from the Logbook tab's "Show on Map" (`FlightLegMapWindow`, standalone `Window`, US46) are four separate, independent map surfaces, but their airport-info-box interaction — clicking an airport, dragging a box, and how it behaves while panning/zooming — must behave identically. All four share `Helpers/DraggableInfoBox` for this (position tracking, drag handling, the leader line shown once a box is dragged from its default spot); a plain `Border` on the map's own `Canvas` overlay, not a WPF `Popup` (a `Popup` is a separate top-level window, which is what used to require manual workarounds for minimize/window-move/tab-switch — see `requirements.md` US42/BUG-12). `CandidateMapWindow` only has a primary box (no Ctrl+click/secondary) since a flat candidate list has no "pair" concept the way a `TripMapWindow` leg's two endpoints do — that's one deliberate interaction difference. `FlightLegMapWindow` always has exactly a pair (its one fixed leg's departure/arrival) and opens both boxes automatically on load instead of requiring a click first — the other deliberate difference; every actual click/drag/zoom mechanism is still the same shared behavior.
- If you change one window's click/drag/zoom behavior for info boxes, mirror it in the others, or extend `DraggableInfoBox` itself so all four pick it up for free — don't let them drift apart.
- Any `Canvas` overlay hosting a `DraggableInfoBox` must **not** itself have `IsHitTestVisible="False"` — that suppresses hit-testing for the entire subtree, including the box (verified empirically; see BUG-12). Mark every other, purely-decorative child on that Canvas `IsHitTestVisible="False"` individually instead.

## Serializer rules — always follow these when adding logbook fields
1. New properties on `FlightRecord` must be **nullable** (`double?`, `string?`, etc.).
2. In `ToXml`: emit the element **only when the value is non-null** (omit = backward-compatible).
3. In `FromXml`: always use `?.Value` with a safe fallback — **never `!.Value`** on new fields.
4. Parse nullable numbers with the `ParseNullDouble` helper.
5. Bump the schema version attribute (`version="1.x"`) when shipping a format change.

## Backwards compatibility & data safety
Before marking any task done, check whether it touches persisted user data — logbook XML, `settings.json`, or any cached file under the AppData folder (e.g. `openaip.local.json`, `openaip-airport-types.json`). If it does, verify explicitly (not just "should be fine") that a file written by the previous version still loads correctly and that no existing value is silently reset, dropped, or overwritten with a default. This is not optional — a user upgrading must never lose a logbook entry, a remembered setting, or a cached credential.
- New fields added to `AppSettings` or any other JSON-serialized settings/cache object must deserialize safely from an older file that lacks them (missing → type default, never an exception). Apply the same nullable/safe-fallback discipline as the logbook Serializer rules below to *any* persisted format, not just the logbook XML.
- Never let test code or throwaway tooling write to the real AppData path (`AppDataHelper.AppDataPath` / the real `settings.json`) — Debug builds and the test project resolve to the *same* `DestinationPlanner-dev` folder a real dev build uses, so an unguarded `AppSettingsService.Save` call in a test silently overwrites the developer's actual settings (this happened once — see `BUG-06` in `requirements.md`). Use `AppSettingsService.TestOverridePath` (or an equivalent isolated path) whenever a test exercises a code path that persists settings.
- When changing a ViewModel or service that calls `AppSettingsService.Save`/`Load`, `NativeLogbookSerializer`, or any other persistence code, ask directly: "if the user's existing file already has data in the old shape, does this change preserve it?" — and verify by inspecting an existing real file's contents before/after, not just by reasoning about the code.

## AppData path convention
- **Release builds**: `%LocalAppData%\DestinationPlanner\`
- **Debug builds**: `%LocalAppData%\DestinationPlanner-dev\`  (`#if DEBUG` in `AppDataHelper.cs`)

This keeps dev and installed-release logbooks completely separate.

## Requirements tracking
- When a plan is finalized and approved, update `DestinationPlanner/requirements.md` with the user stories and acceptance criteria from the plan before starting implementation.
- Plan must not break the existing requirements. If new requirement conflicts with an older requirement, it must be checked which one to follow or modify existing accordingly.
- `requirements.md` should also reflect what automated test coverage each requirement needs (or already has) — see Testing section below.

## README.md
- `README.md` (repo root) must be checked and updated if needed every time when implementing a change

## Committing
- When the user explicitly asks to commit (e.g. "commit the code", "commit this"), just create the commit directly — no need to ask for confirmation first. The request to commit already is the authorization, same as "push" below already is for the push sequence. This still follows the general git safety rules (new commit, not amend; no `--no-verify`; only files relevant to the change get staged, never a blanket `git add -A`).

## Versioning
- `DestinationPlanner/DestinationPlanner.csproj`'s `<Version>` must match the latest git tag (tag `vX.Y.Z` → `<Version>X.Y.Z</Version>`, no leading `v`).
- When asked to tag a release (e.g. "tag this as vX.Y.Z"), update `<Version>` and commit that change *before* creating the tag, so the tagged commit already carries the matching version — don't tag first and fix the csproj after.
- **Whenever the user says "push"**, this means the whole sequence end-to-end, not just the version bump: classify the change(s) since the last tag under semver, bump `<Version>` accordingly, commit that bump, create/move the git tag to match, then push both the commits and the tag to GitHub (`origin`) — all without waiting for a separate "yes, actually push" confirmation, since saying "push" already is that confirmation.
  - **Patch** (`X.Y.Z+1`) — bug fix only, no new capability (e.g. BUG-06/BUG-07-style postmortems).
  - **Minor** (`X.Y+1.0`) — a new feature or capability added, backward-compatible (e.g. US36 filter persistence, US38 precipitation overlay).
  - **Major** (`X+1.0.0`) — a breaking/incompatible change: old logbook/settings files would no longer load correctly, or a documented behavior a user could depend on is removed/changed incompatibly. Rare for this app given the Serializer rules and Backwards compatibility section above are designed to avoid exactly this.
  - Bumping a higher component resets the lower ones to `0` (standard semver — e.g. a minor bump is `X.(Y+1).0`, not `X.(Y+1).Z`).
  - If a batch of unpushed commits mixes categories, use the highest-precedence one (a feature + a fix together is still a minor bump).
  - If it's genuinely ambiguous whether something is a fix vs. a feature vs. breaking, ask rather than guessing — don't silently pick one.

## Testing
- Test project: `DestinationPlanner.Tests` (xUnit), run with `dotnet test DestinationPlanner.slnx`.
- Whenever you make a code change, run `dotnet test` automatically and verify all tests pass before considering the task done — do not wait to be asked.
- **If any unit test fails, or a new requirement conflicts with an existing one in `requirements.md`, stop and consult the user before proceeding.** Do not silently weaken/delete a test or unilaterally pick which requirement "wins" — surface the conflict and let the user decide.
- Prefer testing pure logic and ViewModels via fakes (see `DestinationPlanner.Tests/Fakes`) over real I/O, SimConnect, or network calls. UI rendering and live SimConnect/MSFS behavior are verified manually, not by automated tests.

## Mapsui 5.0.2 gotchas
- `SymbolStyle` is available; `VectorStyle` is an alternative.
- No public viewport-changed event — load all filtered airports into `MemoryLayer` at once.
- `Map.ViewportInitialized` event for initial centering.

## Build
`dotnet build DestinationPlanner/DestinationPlanner.csproj` (or `dotnet build DestinationPlanner.slnx` to include the test project) — must pass with zero errors before marking any task done. The NU1701 warnings about OpenTK are pre-existing and can be ignored.
