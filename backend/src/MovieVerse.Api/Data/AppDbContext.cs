using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MovieVerse.Api.Tickets;

namespace MovieVerse.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const int MaxOverviewLength = 1000;

    public DbSet<User> Users => Set<User>();

    public DbSet<ListItem> ListItems => Set<ListItem>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ReviewReport> ReviewReports => Set<ReviewReport>();

    public DbSet<Cinema> Cinemas => Set<Cinema>();

    public DbSet<Hall> Halls => Set<Hall>();

    public DbSet<Seat> Seats => Set<Seat>();

    public DbSet<Showtime> Showtimes => Set<Showtime>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingSeat> BookingSeats => Set<BookingSeat>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Times are stored in UTC; mark them as such on read, or JSON serializes them without "Z"
        // and browsers read them as local time
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.GoogleSubject).IsUnique();
            user.Property(u => u.GoogleSubject).HasMaxLength(255);
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.Name).HasMaxLength(200);
            user.Property(u => u.AvatarUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<ListItem>(item =>
        {
            item.HasKey(i => new { i.UserId, i.List, i.MovieId });
            item.HasIndex(i => new { i.UserId, i.List, i.AddedAtUtc });
            item.HasOne<User>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
            item.Property(i => i.Title).HasMaxLength(500);
            item.Property(i => i.PosterPath).HasMaxLength(200);
            item.Property(i => i.ReleaseDate).HasMaxLength(10);
            item.Property(i => i.Overview).HasMaxLength(MaxOverviewLength);
        });

        modelBuilder.Entity<Rating>(rating =>
        {
            rating.HasKey(r => new { r.UserId, r.MovieId });
            rating.HasIndex(r => new { r.UserId, r.RatedAtUtc });
            rating.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            rating.ToTable(t => t.HasCheckConstraint(
                "CK_Ratings_Score", $"[Score] BETWEEN {Rating.MinScore} AND {Rating.MaxScore}"));
            rating.Property(r => r.Title).HasMaxLength(500);
            rating.Property(r => r.PosterPath).HasMaxLength(200);
            rating.Property(r => r.ReleaseDate).HasMaxLength(10);
            rating.Property(r => r.Overview).HasMaxLength(MaxOverviewLength);
        });

        modelBuilder.Entity<Review>(review =>
        {
            review.HasIndex(r => new { r.UserId, r.MovieId }).IsUnique();
            review.HasIndex(r => new { r.MovieId, r.Status, r.CreatedAtUtc });
            review.HasIndex(r => r.Status);
            review.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            review.Property(r => r.Body).HasMaxLength(Review.MaxBodyLength);
        });

        modelBuilder.Entity<ReviewReport>(report =>
        {
            report.HasKey(r => new { r.ReviewId, r.ReporterUserId });
            report.HasOne<Review>().WithMany(r => r.Reports).HasForeignKey(r => r.ReviewId).OnDelete(DeleteBehavior.Cascade);
            // SQL Server rejects a second cascade path from Users (via Reviews and directly),
            // so a reporter's reports are deleted explicitly when the account is deleted
            report.HasOne<User>().WithMany().HasForeignKey(r => r.ReporterUserId).OnDelete(DeleteBehavior.NoAction);
            report.HasIndex(r => r.ReporterUserId);
        });

        modelBuilder.Entity<Cinema>(cinema =>
        {
            cinema.Property(c => c.Name).HasMaxLength(100);
            cinema.Property(c => c.Address).HasMaxLength(200);
            cinema.Property(c => c.TimeZone).HasMaxLength(64);
            cinema.HasData(CinemaSeed.Cinemas.Select(c => new Cinema
            {
                Id = c.Id, Name = c.Name, Address = c.Address, TimeZone = c.TimeZone,
            }));
        });

        modelBuilder.Entity<Hall>(hall =>
        {
            hall.HasOne(h => h.Cinema).WithMany(c => c.Halls).HasForeignKey(h => h.CinemaId);
            hall.Property(h => h.Name).HasMaxLength(100);
            hall.HasData(CinemaSeed.Halls.Select(h => new Hall { Id = h.Id, CinemaId = h.CinemaId, Name = h.Name }));
        });

        modelBuilder.Entity<Seat>(seat =>
        {
            seat.HasOne<Hall>().WithMany(h => h.Seats).HasForeignKey(s => s.HallId);
            seat.Property(s => s.Row).HasMaxLength(2);
            seat.HasIndex(s => new { s.HallId, s.Row, s.Number }).IsUnique();
            seat.HasData(CinemaSeed.Seats);
        });

        modelBuilder.Entity<Showtime>(showtime =>
        {
            showtime.HasOne(s => s.Hall).WithMany().HasForeignKey(s => s.HallId);
            showtime.HasIndex(s => new { s.HallId, s.StartsAtUtc }).IsUnique();
            showtime.HasIndex(s => new { s.MovieId, s.StartsAtUtc });
            showtime.HasIndex(s => s.LocalDate);
            showtime.Property(s => s.MovieTitle).HasMaxLength(500);
            showtime.Property(s => s.PosterPath).HasMaxLength(200);
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            booking.HasOne(b => b.Showtime).WithMany().HasForeignKey(b => b.ShowtimeId).OnDelete(DeleteBehavior.NoAction);
            booking.HasIndex(b => new { b.UserId, b.CreatedAtUtc });
            booking.HasIndex(b => new { b.ShowtimeId, b.Status });
            booking.HasIndex(b => b.StripeSessionId).IsUnique().HasFilter("[StripeSessionId] IS NOT NULL");
            booking.Property(b => b.SeatLabels).HasMaxLength(200);
            booking.Property(b => b.StripeSessionId).HasMaxLength(255);
            booking.Property(b => b.CheckoutUrl).HasMaxLength(2048);
            booking.Property(b => b.StripePaymentIntentId).HasMaxLength(255);
            booking.Property(b => b.TicketCode).HasMaxLength(16);
        });

        modelBuilder.Entity<BookingSeat>(seat =>
        {
            seat.HasKey(s => new { s.BookingId, s.SeatId });
            // the guarantee against double booking: a seat appears once per showtime
            seat.HasIndex(s => new { s.ShowtimeId, s.SeatId }).IsUnique();
            seat.HasOne<Booking>().WithMany(b => b.Seats).HasForeignKey(s => s.BookingId).OnDelete(DeleteBehavior.Cascade);
            seat.HasOne<Showtime>().WithMany().HasForeignKey(s => s.ShowtimeId).OnDelete(DeleteBehavior.NoAction);
            seat.HasOne(s => s.Seat).WithMany().HasForeignKey(s => s.SeatId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
