using DestinationPlanner.Models;

namespace DestinationPlanner.ViewModels;

// Display-only wrapper around a persisted TripLeg, adding the great-circle distance between
// its airports. Kept out of the TripLeg model itself since distance isn't persisted data —
// it's derived from whichever airport dataset happens to be loaded, so it's recomputed
// whenever TripPlanViewModel rebuilds the bound leg list rather than stored on the model.
public class TripLegRow(TripLeg leg, double? distanceNm)
{
    public TripLeg Leg { get; } = leg;
    public int Order => Leg.Order;
    public string DepartureIcao => Leg.DepartureIcao;
    public string ArrivalIcao => Leg.ArrivalIcao;
    public TripLegStatus Status => Leg.Status;
    public double? DistanceNm { get; } = distanceNm;
}
