namespace MovieVerse.Api.Data;

/// <summary>A fictional demo cinema; seeded, not editable through the API.</summary>
public sealed class Cinema
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Address { get; set; }

    /// <summary>IANA time zone the schedule is planned and shown in.</summary>
    public required string TimeZone { get; set; }

    public List<Hall> Halls { get; set; } = [];
}

public sealed class Hall
{
    public int Id { get; set; }

    public int CinemaId { get; set; }

    public Cinema? Cinema { get; set; }

    public required string Name { get; set; }

    public List<Seat> Seats { get; set; } = [];
}

public enum SeatType : byte
{
    Standard = 0,
    Vip = 1,
    Wheelchair = 2,
}

public sealed class Seat
{
    public int Id { get; set; }

    public int HallId { get; set; }

    /// <summary>Row letter as printed on the ticket, "A" nearest the screen.</summary>
    public required string Row { get; set; }

    /// <summary>Seat number within the row, counted left to right.</summary>
    public int Number { get; set; }

    /// <summary>Column in the hall grid, so aisles and gaps can be drawn.</summary>
    public int Column { get; set; }

    public SeatType Type { get; set; }
}
