using MovieVerse.Api.Data;

namespace MovieVerse.Api.Tickets;

/// <summary>
/// The demo cinemas. They are fictional venues in San Diego with Ukrainian-named halls, seeded
/// through migrations. Hall layouts use one string per row, nearest the screen first:
/// 's' standard seat, 'v' VIP recliner, 'w' wheelchair space, '.' aisle or gap.
/// </summary>
public static class CinemaSeed
{
    public const string TimeZone = "America/Los_Angeles";

    public static readonly Cinema[] Cinemas =
    [
        new() { Id = 1, Name = "Flamingo Screens", Address = "Gaslamp Quarter, San Diego, CA", TimeZone = TimeZone },
        new() { Id = 2, Name = "Potuzhniy Cinema", Address = "North Park, San Diego, CA", TimeZone = TimeZone },
    ];

    private static readonly (int Id, int CinemaId, string Name, string[] Rows)[] HallLayouts =
    [
        (1, 1, "Kyiv",
        [
            "ww...ssssssssss...ww",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "ssss.ssssssssss.ssss",
            "vvvv.vvvvvvvvvv.vvvv",
            "vvvv.vvvvvvvvvv.vvvv",
        ]),
        (2, 1, "Lviv",
        [
            "w..sss.sss..w",
            "ssssss.ssssss",
            "ssssss.ssssss",
            "ssssss.ssssss",
            "ssssss.ssssss",
            "ssssss.ssssss",
            "vvvvvv.vvvvvv",
        ]),
        (3, 2, "Odesa",
        [
            "w..sssssss..w",
            "sssssss.sssssss",
            "sssssss.sssssss",
            "sssssss.sssssss",
            "sssssss.sssssss",
            "sssssss.sssssss",
            "vvvv.vvvv",
        ]),
        (4, 2, "Kharkiv VIP",
        [
            "vv.vv.vv.vv",
            "vv.vv.vv.vv",
            "vv.vv.vv.vv",
            "vv.vv.vv.vv",
        ]),
    ];

    public static readonly Hall[] Halls = HallLayouts
        .Select(h => new Hall { Id = h.Id, CinemaId = h.CinemaId, Name = h.Name })
        .ToArray();

    /// <summary>Seats with stable ids (hall id * 1000 + position), so migrations stay deterministic.</summary>
    public static readonly Seat[] Seats = HallLayouts.SelectMany(BuildSeats).ToArray();

    private static IEnumerable<Seat> BuildSeats((int Id, int CinemaId, string Name, string[] Rows) hall)
    {
        var position = 0;
        for (var rowIndex = 0; rowIndex < hall.Rows.Length; rowIndex++)
        {
            var row = ((char)('A' + rowIndex)).ToString();
            var number = 0;
            for (var column = 0; column < hall.Rows[rowIndex].Length; column++)
            {
                var type = hall.Rows[rowIndex][column] switch
                {
                    's' => SeatType.Standard,
                    'v' => SeatType.Vip,
                    'w' => SeatType.Wheelchair,
                    _ => (SeatType?)null,
                };
                if (type is null)
                {
                    continue;
                }

                yield return new Seat
                {
                    Id = hall.Id * 1000 + ++position,
                    HallId = hall.Id,
                    Row = row,
                    Number = ++number,
                    Column = column,
                    Type = type.Value,
                };
            }
        }
    }
}
