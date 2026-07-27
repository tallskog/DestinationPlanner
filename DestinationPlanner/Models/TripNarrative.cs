namespace DestinationPlanner.Models;

// Structured output of IAiTripPlanningService.NarrateAsync (US41 follow-up) — used when the
// leg sequence is already fixed by TripRouteBuilder (a per-leg distance constraint was given),
// so the AI's only job left is title + narrative text, not sequencing.
public record TripNarrative(string Title, string Narrative);
