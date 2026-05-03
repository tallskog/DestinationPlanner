using DestinationPlanner.Models;

namespace DestinationPlanner.ViewModels;

public class EditFlightViewModel : ViewModelBase
{
    private DateTime? _blockOffDate;
    private string _blockOffTime = string.Empty;
    private DateTime? _blockOnDate;
    private string _blockOnTime = string.Empty;
    private AircraftType _aircraftType;
    private string _aircraftModel = string.Empty;
    private string _departureIcao = string.Empty;
    private string _arrivalIcao = string.Empty;

    public DateTime? BlockOffDate
    {
        get => _blockOffDate;
        set { SetField(ref _blockOffDate, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public string BlockOffTime
    {
        get => _blockOffTime;
        set { SetField(ref _blockOffTime, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public DateTime? BlockOnDate
    {
        get => _blockOnDate;
        set { SetField(ref _blockOnDate, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public string BlockOnTime
    {
        get => _blockOnTime;
        set { SetField(ref _blockOnTime, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public AircraftType AircraftType
    {
        get => _aircraftType;
        set => SetField(ref _aircraftType, value);
    }

    public string AircraftModel
    {
        get => _aircraftModel;
        set => SetField(ref _aircraftModel, value);
    }

    public string DepartureIcao
    {
        get => _departureIcao;
        set { SetField(ref _departureIcao, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public string ArrivalIcao
    {
        get => _arrivalIcao;
        set { SetField(ref _arrivalIcao, value); OnPropertyChanged(nameof(IsValid)); }
    }

    public AircraftType[] AircraftTypeOptions { get; } = [AircraftType.Airplane, AircraftType.Helicopter];

    public bool IsValid =>
        BlockOffDate.HasValue &&
        BlockOnDate.HasValue &&
        TryParseTime(BlockOffTime, out _) &&
        TryParseTime(BlockOnTime, out _) &&
        !string.IsNullOrWhiteSpace(DepartureIcao) &&
        !string.IsNullOrWhiteSpace(ArrivalIcao);

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
            AircraftType  = AircraftType,
            AircraftModel = AircraftModel.Trim(),
            DepartureIcao = DepartureIcao.Trim().ToUpperInvariant(),
            ArrivalIcao   = ArrivalIcao.Trim().ToUpperInvariant(),
            BlockOffUtc   = blockOff,
            BlockOnUtc    = blockOn,
        };
    }

    public static EditFlightViewModel FromRecord(FlightRecord r) => new()
    {
        _blockOffDate  = r.BlockOffUtc.Date,
        _blockOffTime  = r.BlockOffUtc.ToString("HH:mm"),
        _blockOnDate   = r.BlockOnUtc.Date,
        _blockOnTime   = r.BlockOnUtc.ToString("HH:mm"),
        _aircraftType  = r.AircraftType,
        _aircraftModel = r.AircraftModel,
        _departureIcao = r.DepartureIcao,
        _arrivalIcao   = r.ArrivalIcao,
    };

    private static bool TryParseTime(string s, out TimeSpan result)
    {
        if (TimeSpan.TryParseExact(s, @"hh\:mm", null, out result)) return true;
        if (TimeSpan.TryParseExact(s, @"h\:mm",  null, out result)) return true;
        result = default;
        return false;
    }
}
