using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// Deterministic airport candidate lookup for AI-assisted trip planning (US41). No AI
// involvement here — resolves filter criteria + visited-status purely from data the app
// already has (IAirportDataService, ILogbookService).
public interface ITripCandidateService
{
    IReadOnlyList<Airport> GetCandidates(AirportFilterCriteria criteria, bool excludeVisited);
}
