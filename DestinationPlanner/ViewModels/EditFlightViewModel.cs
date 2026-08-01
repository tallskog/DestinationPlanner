using DestinationPlanner.Models;

namespace DestinationPlanner.ViewModels;

public class EditFlightViewModel : ViewModelBase
{
    private DateTime? _blockOffDate;
    private string _blockOffTime = string.Empty;
    private DateTime? _blockOnDate;
    private string _blockOnTime = string.Empty;
    private string _aircraftModel = string.Empty;
    private string _departureIcao = string.Empty;
    private string _arrivalIcao = string.Empty;

    // Landing stats aren't editable in this dialog — preserved verbatim from the original
    // record (FromRecord) and carried through unchanged (ToRecord) so editing a flight's
    // date/route/aircraft doesn't silently wipe its recorded landing rating/stats.
    private double? _landingFpm;
    private double? _landingGForce;
    private double? _landingAirspeedKts;
    private double? _landingWindKts;
    private double? _landingWindDirection;
    private double? _landingHeadingDeg;
    private double? _landingBankAngleDeg;
    private double? _landingPitchAngleDeg;
    private double? _landingCrosswindKts;
    private double? _landingCenterlineDeviationFt;
    private double? _landingTouchdownZonePct;
    private int?    _landingStars;

    public DateTime? BlockOffDate
    {
        get => _blockOffDate;
        set { SetField(ref _blockOffDate, value); RaiseValidationChanged(); }
    }

    public string BlockOffTime
    {
        get => _blockOffTime;
        set { SetField(ref _blockOffTime, value); RaiseValidationChanged(); }
    }

    public DateTime? BlockOnDate
    {
        get => _blockOnDate;
        set { SetField(ref _blockOnDate, value); RaiseValidationChanged(); }
    }

    public string BlockOnTime
    {
        get => _blockOnTime;
        set { SetField(ref _blockOnTime, value); RaiseValidationChanged(); }
    }

    public string AircraftModel
    {
        get => _aircraftModel;
        set => SetField(ref _aircraftModel, value);
    }

    public string DepartureIcao
    {
        get => _departureIcao;
        set { SetField(ref _departureIcao, value); RaiseValidationChanged(); }
    }

    public string ArrivalIcao
    {
        get => _arrivalIcao;
        set { SetField(ref _arrivalIcao, value); RaiseValidationChanged(); }
    }

    public bool IsValid => ValidationMessage.Length == 0;

    public string ValidationMessage
    {
        get
        {
            if (!BlockOffDate.HasValue) return "Block Off date is required.";
            if (!TryParseTime(BlockOffTime, out _)) return "Block Off time must be in h:mm or hh:mm format (e.g. 9:30).";
            if (!BlockOnDate.HasValue) return "Block On date is required.";
            if (!TryParseTime(BlockOnTime, out _)) return "Block On time must be in h:mm or hh:mm format (e.g. 9:30).";
            if (string.IsNullOrWhiteSpace(DepartureIcao)) return "Departure ICAO is required.";
            if (string.IsNullOrWhiteSpace(ArrivalIcao)) return "Arrival ICAO is required.";
            return string.Empty;
        }
    }

    private void RaiseValidationChanged()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    public FlightRecord ToRecord(Guid id)
    {
        TryParseTime(BlockOffTime, out var offTime);
        TryParseTime(BlockOnTime, out var onTime);

        var blockOff = DateTime.SpecifyKind(BlockOffDate!.Value.Date + offTime, DateTimeKind.Utc);
        var blockOn  = DateTime.SpecifyKind(BlockOnDate!.Value.Date  + onTime,  DateTimeKind.Utc);

        return new FlightRecord
        {
            Id            = id,
            Date          = DateOnly.FromDateTime(blockOff),
            AircraftModel = AircraftModel.Trim(),
            DepartureIcao = DepartureIcao.Trim().ToUpperInvariant(),
            ArrivalIcao   = ArrivalIcao.Trim().ToUpperInvariant(),
            BlockOffUtc   = blockOff,
            BlockOnUtc    = blockOn,
            LandingFpm                   = _landingFpm,
            LandingGForce                = _landingGForce,
            LandingAirspeedKts           = _landingAirspeedKts,
            LandingWindKts               = _landingWindKts,
            LandingWindDirection         = _landingWindDirection,
            LandingHeadingDeg            = _landingHeadingDeg,
            LandingBankAngleDeg          = _landingBankAngleDeg,
            LandingPitchAngleDeg         = _landingPitchAngleDeg,
            LandingCrosswindKts          = _landingCrosswindKts,
            LandingCenterlineDeviationFt = _landingCenterlineDeviationFt,
            LandingTouchdownZonePct      = _landingTouchdownZonePct,
            LandingStars                 = _landingStars,
        };
    }

    public static EditFlightViewModel FromRecord(FlightRecord r) => new()
    {
        _blockOffDate  = r.BlockOffUtc.Date,
        _blockOffTime  = r.BlockOffUtc.ToString(@"HH\:mm"),
        _blockOnDate   = r.BlockOnUtc.Date,
        _blockOnTime   = r.BlockOnUtc.ToString(@"HH\:mm"),
        _aircraftModel = r.AircraftModel,
        _departureIcao = r.DepartureIcao,
        _arrivalIcao   = r.ArrivalIcao,
        _landingFpm                   = r.LandingFpm,
        _landingGForce                = r.LandingGForce,
        _landingAirspeedKts           = r.LandingAirspeedKts,
        _landingWindKts               = r.LandingWindKts,
        _landingWindDirection         = r.LandingWindDirection,
        _landingHeadingDeg            = r.LandingHeadingDeg,
        _landingBankAngleDeg          = r.LandingBankAngleDeg,
        _landingPitchAngleDeg         = r.LandingPitchAngleDeg,
        _landingCrosswindKts          = r.LandingCrosswindKts,
        _landingCenterlineDeviationFt = r.LandingCenterlineDeviationFt,
        _landingTouchdownZonePct      = r.LandingTouchdownZonePct,
        _landingStars                 = r.LandingStars,
    };

    private static bool TryParseTime(string s, out TimeSpan result)
    {
        if (TimeSpan.TryParseExact(s, @"hh\:mm", null, out result)) return true;
        if (TimeSpan.TryParseExact(s, @"h\:mm",  null, out result)) return true;
        result = default;
        return false;
    }
}
