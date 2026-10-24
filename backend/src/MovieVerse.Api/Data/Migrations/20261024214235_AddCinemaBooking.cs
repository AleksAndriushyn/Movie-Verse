using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MovieVerse.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCinemaBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cinemas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cinemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Halls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CinemaId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Halls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Halls_Cinemas_CinemaId",
                        column: x => x.CinemaId,
                        principalTable: "Cinemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HallId = table.Column<int>(type: "int", nullable: false),
                    Row = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Column = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seats_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Showtimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HallId = table.Column<int>(type: "int", nullable: false),
                    MovieId = table.Column<int>(type: "int", nullable: false),
                    MovieTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PosterPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StandardPriceCents = table.Column<int>(type: "int", nullable: false),
                    VipPriceCents = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showtimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Showtimes_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ShowtimeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalCents = table.Column<int>(type: "int", nullable: false),
                    SeatLabels = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StripeSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TicketCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingSeats",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    ShowtimeId = table.Column<int>(type: "int", nullable: false),
                    PriceCents = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSeats", x => new { x.BookingId, x.SeatId });
                    table.ForeignKey(
                        name: "FK_BookingSeats_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingSeats_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingSeats_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Cinemas",
                columns: new[] { "Id", "Address", "Name", "TimeZone" },
                values: new object[,]
                {
                    { 1, "Gaslamp Quarter, San Diego, CA", "Flamingo Screens", "America/Los_Angeles" },
                    { 2, "North Park, San Diego, CA", "Potuzhniy Cinema", "America/Los_Angeles" }
                });

            migrationBuilder.InsertData(
                table: "Halls",
                columns: new[] { "Id", "CinemaId", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Kyiv" },
                    { 2, 1, "Lviv" },
                    { 3, 2, "Odesa" },
                    { 4, 2, "Kharkiv VIP" }
                });

            migrationBuilder.InsertData(
                table: "Seats",
                columns: new[] { "Id", "Column", "HallId", "Number", "Row", "Type" },
                values: new object[,]
                {
                    { 1001, 0, 1, 1, "A", (byte)2 },
                    { 1002, 1, 1, 2, "A", (byte)2 },
                    { 1003, 5, 1, 3, "A", (byte)0 },
                    { 1004, 6, 1, 4, "A", (byte)0 },
                    { 1005, 7, 1, 5, "A", (byte)0 },
                    { 1006, 8, 1, 6, "A", (byte)0 },
                    { 1007, 9, 1, 7, "A", (byte)0 },
                    { 1008, 10, 1, 8, "A", (byte)0 },
                    { 1009, 11, 1, 9, "A", (byte)0 },
                    { 1010, 12, 1, 10, "A", (byte)0 },
                    { 1011, 13, 1, 11, "A", (byte)0 },
                    { 1012, 14, 1, 12, "A", (byte)0 },
                    { 1013, 18, 1, 13, "A", (byte)2 },
                    { 1014, 19, 1, 14, "A", (byte)2 },
                    { 1015, 0, 1, 1, "B", (byte)0 },
                    { 1016, 1, 1, 2, "B", (byte)0 },
                    { 1017, 2, 1, 3, "B", (byte)0 },
                    { 1018, 3, 1, 4, "B", (byte)0 },
                    { 1019, 5, 1, 5, "B", (byte)0 },
                    { 1020, 6, 1, 6, "B", (byte)0 },
                    { 1021, 7, 1, 7, "B", (byte)0 },
                    { 1022, 8, 1, 8, "B", (byte)0 },
                    { 1023, 9, 1, 9, "B", (byte)0 },
                    { 1024, 10, 1, 10, "B", (byte)0 },
                    { 1025, 11, 1, 11, "B", (byte)0 },
                    { 1026, 12, 1, 12, "B", (byte)0 },
                    { 1027, 13, 1, 13, "B", (byte)0 },
                    { 1028, 14, 1, 14, "B", (byte)0 },
                    { 1029, 16, 1, 15, "B", (byte)0 },
                    { 1030, 17, 1, 16, "B", (byte)0 },
                    { 1031, 18, 1, 17, "B", (byte)0 },
                    { 1032, 19, 1, 18, "B", (byte)0 },
                    { 1033, 0, 1, 1, "C", (byte)0 },
                    { 1034, 1, 1, 2, "C", (byte)0 },
                    { 1035, 2, 1, 3, "C", (byte)0 },
                    { 1036, 3, 1, 4, "C", (byte)0 },
                    { 1037, 5, 1, 5, "C", (byte)0 },
                    { 1038, 6, 1, 6, "C", (byte)0 },
                    { 1039, 7, 1, 7, "C", (byte)0 },
                    { 1040, 8, 1, 8, "C", (byte)0 },
                    { 1041, 9, 1, 9, "C", (byte)0 },
                    { 1042, 10, 1, 10, "C", (byte)0 },
                    { 1043, 11, 1, 11, "C", (byte)0 },
                    { 1044, 12, 1, 12, "C", (byte)0 },
                    { 1045, 13, 1, 13, "C", (byte)0 },
                    { 1046, 14, 1, 14, "C", (byte)0 },
                    { 1047, 16, 1, 15, "C", (byte)0 },
                    { 1048, 17, 1, 16, "C", (byte)0 },
                    { 1049, 18, 1, 17, "C", (byte)0 },
                    { 1050, 19, 1, 18, "C", (byte)0 },
                    { 1051, 0, 1, 1, "D", (byte)0 },
                    { 1052, 1, 1, 2, "D", (byte)0 },
                    { 1053, 2, 1, 3, "D", (byte)0 },
                    { 1054, 3, 1, 4, "D", (byte)0 },
                    { 1055, 5, 1, 5, "D", (byte)0 },
                    { 1056, 6, 1, 6, "D", (byte)0 },
                    { 1057, 7, 1, 7, "D", (byte)0 },
                    { 1058, 8, 1, 8, "D", (byte)0 },
                    { 1059, 9, 1, 9, "D", (byte)0 },
                    { 1060, 10, 1, 10, "D", (byte)0 },
                    { 1061, 11, 1, 11, "D", (byte)0 },
                    { 1062, 12, 1, 12, "D", (byte)0 },
                    { 1063, 13, 1, 13, "D", (byte)0 },
                    { 1064, 14, 1, 14, "D", (byte)0 },
                    { 1065, 16, 1, 15, "D", (byte)0 },
                    { 1066, 17, 1, 16, "D", (byte)0 },
                    { 1067, 18, 1, 17, "D", (byte)0 },
                    { 1068, 19, 1, 18, "D", (byte)0 },
                    { 1069, 0, 1, 1, "E", (byte)0 },
                    { 1070, 1, 1, 2, "E", (byte)0 },
                    { 1071, 2, 1, 3, "E", (byte)0 },
                    { 1072, 3, 1, 4, "E", (byte)0 },
                    { 1073, 5, 1, 5, "E", (byte)0 },
                    { 1074, 6, 1, 6, "E", (byte)0 },
                    { 1075, 7, 1, 7, "E", (byte)0 },
                    { 1076, 8, 1, 8, "E", (byte)0 },
                    { 1077, 9, 1, 9, "E", (byte)0 },
                    { 1078, 10, 1, 10, "E", (byte)0 },
                    { 1079, 11, 1, 11, "E", (byte)0 },
                    { 1080, 12, 1, 12, "E", (byte)0 },
                    { 1081, 13, 1, 13, "E", (byte)0 },
                    { 1082, 14, 1, 14, "E", (byte)0 },
                    { 1083, 16, 1, 15, "E", (byte)0 },
                    { 1084, 17, 1, 16, "E", (byte)0 },
                    { 1085, 18, 1, 17, "E", (byte)0 },
                    { 1086, 19, 1, 18, "E", (byte)0 },
                    { 1087, 0, 1, 1, "F", (byte)0 },
                    { 1088, 1, 1, 2, "F", (byte)0 },
                    { 1089, 2, 1, 3, "F", (byte)0 },
                    { 1090, 3, 1, 4, "F", (byte)0 },
                    { 1091, 5, 1, 5, "F", (byte)0 },
                    { 1092, 6, 1, 6, "F", (byte)0 },
                    { 1093, 7, 1, 7, "F", (byte)0 },
                    { 1094, 8, 1, 8, "F", (byte)0 },
                    { 1095, 9, 1, 9, "F", (byte)0 },
                    { 1096, 10, 1, 10, "F", (byte)0 },
                    { 1097, 11, 1, 11, "F", (byte)0 },
                    { 1098, 12, 1, 12, "F", (byte)0 },
                    { 1099, 13, 1, 13, "F", (byte)0 },
                    { 1100, 14, 1, 14, "F", (byte)0 },
                    { 1101, 16, 1, 15, "F", (byte)0 },
                    { 1102, 17, 1, 16, "F", (byte)0 },
                    { 1103, 18, 1, 17, "F", (byte)0 },
                    { 1104, 19, 1, 18, "F", (byte)0 },
                    { 1105, 0, 1, 1, "G", (byte)0 },
                    { 1106, 1, 1, 2, "G", (byte)0 },
                    { 1107, 2, 1, 3, "G", (byte)0 },
                    { 1108, 3, 1, 4, "G", (byte)0 },
                    { 1109, 5, 1, 5, "G", (byte)0 },
                    { 1110, 6, 1, 6, "G", (byte)0 },
                    { 1111, 7, 1, 7, "G", (byte)0 },
                    { 1112, 8, 1, 8, "G", (byte)0 },
                    { 1113, 9, 1, 9, "G", (byte)0 },
                    { 1114, 10, 1, 10, "G", (byte)0 },
                    { 1115, 11, 1, 11, "G", (byte)0 },
                    { 1116, 12, 1, 12, "G", (byte)0 },
                    { 1117, 13, 1, 13, "G", (byte)0 },
                    { 1118, 14, 1, 14, "G", (byte)0 },
                    { 1119, 16, 1, 15, "G", (byte)0 },
                    { 1120, 17, 1, 16, "G", (byte)0 },
                    { 1121, 18, 1, 17, "G", (byte)0 },
                    { 1122, 19, 1, 18, "G", (byte)0 },
                    { 1123, 0, 1, 1, "H", (byte)0 },
                    { 1124, 1, 1, 2, "H", (byte)0 },
                    { 1125, 2, 1, 3, "H", (byte)0 },
                    { 1126, 3, 1, 4, "H", (byte)0 },
                    { 1127, 5, 1, 5, "H", (byte)0 },
                    { 1128, 6, 1, 6, "H", (byte)0 },
                    { 1129, 7, 1, 7, "H", (byte)0 },
                    { 1130, 8, 1, 8, "H", (byte)0 },
                    { 1131, 9, 1, 9, "H", (byte)0 },
                    { 1132, 10, 1, 10, "H", (byte)0 },
                    { 1133, 11, 1, 11, "H", (byte)0 },
                    { 1134, 12, 1, 12, "H", (byte)0 },
                    { 1135, 13, 1, 13, "H", (byte)0 },
                    { 1136, 14, 1, 14, "H", (byte)0 },
                    { 1137, 16, 1, 15, "H", (byte)0 },
                    { 1138, 17, 1, 16, "H", (byte)0 },
                    { 1139, 18, 1, 17, "H", (byte)0 },
                    { 1140, 19, 1, 18, "H", (byte)0 },
                    { 1141, 0, 1, 1, "I", (byte)1 },
                    { 1142, 1, 1, 2, "I", (byte)1 },
                    { 1143, 2, 1, 3, "I", (byte)1 },
                    { 1144, 3, 1, 4, "I", (byte)1 },
                    { 1145, 5, 1, 5, "I", (byte)1 },
                    { 1146, 6, 1, 6, "I", (byte)1 },
                    { 1147, 7, 1, 7, "I", (byte)1 },
                    { 1148, 8, 1, 8, "I", (byte)1 },
                    { 1149, 9, 1, 9, "I", (byte)1 },
                    { 1150, 10, 1, 10, "I", (byte)1 },
                    { 1151, 11, 1, 11, "I", (byte)1 },
                    { 1152, 12, 1, 12, "I", (byte)1 },
                    { 1153, 13, 1, 13, "I", (byte)1 },
                    { 1154, 14, 1, 14, "I", (byte)1 },
                    { 1155, 16, 1, 15, "I", (byte)1 },
                    { 1156, 17, 1, 16, "I", (byte)1 },
                    { 1157, 18, 1, 17, "I", (byte)1 },
                    { 1158, 19, 1, 18, "I", (byte)1 },
                    { 1159, 0, 1, 1, "J", (byte)1 },
                    { 1160, 1, 1, 2, "J", (byte)1 },
                    { 1161, 2, 1, 3, "J", (byte)1 },
                    { 1162, 3, 1, 4, "J", (byte)1 },
                    { 1163, 5, 1, 5, "J", (byte)1 },
                    { 1164, 6, 1, 6, "J", (byte)1 },
                    { 1165, 7, 1, 7, "J", (byte)1 },
                    { 1166, 8, 1, 8, "J", (byte)1 },
                    { 1167, 9, 1, 9, "J", (byte)1 },
                    { 1168, 10, 1, 10, "J", (byte)1 },
                    { 1169, 11, 1, 11, "J", (byte)1 },
                    { 1170, 12, 1, 12, "J", (byte)1 },
                    { 1171, 13, 1, 13, "J", (byte)1 },
                    { 1172, 14, 1, 14, "J", (byte)1 },
                    { 1173, 16, 1, 15, "J", (byte)1 },
                    { 1174, 17, 1, 16, "J", (byte)1 },
                    { 1175, 18, 1, 17, "J", (byte)1 },
                    { 1176, 19, 1, 18, "J", (byte)1 },
                    { 2001, 0, 2, 1, "A", (byte)2 },
                    { 2002, 3, 2, 2, "A", (byte)0 },
                    { 2003, 4, 2, 3, "A", (byte)0 },
                    { 2004, 5, 2, 4, "A", (byte)0 },
                    { 2005, 7, 2, 5, "A", (byte)0 },
                    { 2006, 8, 2, 6, "A", (byte)0 },
                    { 2007, 9, 2, 7, "A", (byte)0 },
                    { 2008, 12, 2, 8, "A", (byte)2 },
                    { 2009, 0, 2, 1, "B", (byte)0 },
                    { 2010, 1, 2, 2, "B", (byte)0 },
                    { 2011, 2, 2, 3, "B", (byte)0 },
                    { 2012, 3, 2, 4, "B", (byte)0 },
                    { 2013, 4, 2, 5, "B", (byte)0 },
                    { 2014, 5, 2, 6, "B", (byte)0 },
                    { 2015, 7, 2, 7, "B", (byte)0 },
                    { 2016, 8, 2, 8, "B", (byte)0 },
                    { 2017, 9, 2, 9, "B", (byte)0 },
                    { 2018, 10, 2, 10, "B", (byte)0 },
                    { 2019, 11, 2, 11, "B", (byte)0 },
                    { 2020, 12, 2, 12, "B", (byte)0 },
                    { 2021, 0, 2, 1, "C", (byte)0 },
                    { 2022, 1, 2, 2, "C", (byte)0 },
                    { 2023, 2, 2, 3, "C", (byte)0 },
                    { 2024, 3, 2, 4, "C", (byte)0 },
                    { 2025, 4, 2, 5, "C", (byte)0 },
                    { 2026, 5, 2, 6, "C", (byte)0 },
                    { 2027, 7, 2, 7, "C", (byte)0 },
                    { 2028, 8, 2, 8, "C", (byte)0 },
                    { 2029, 9, 2, 9, "C", (byte)0 },
                    { 2030, 10, 2, 10, "C", (byte)0 },
                    { 2031, 11, 2, 11, "C", (byte)0 },
                    { 2032, 12, 2, 12, "C", (byte)0 },
                    { 2033, 0, 2, 1, "D", (byte)0 },
                    { 2034, 1, 2, 2, "D", (byte)0 },
                    { 2035, 2, 2, 3, "D", (byte)0 },
                    { 2036, 3, 2, 4, "D", (byte)0 },
                    { 2037, 4, 2, 5, "D", (byte)0 },
                    { 2038, 5, 2, 6, "D", (byte)0 },
                    { 2039, 7, 2, 7, "D", (byte)0 },
                    { 2040, 8, 2, 8, "D", (byte)0 },
                    { 2041, 9, 2, 9, "D", (byte)0 },
                    { 2042, 10, 2, 10, "D", (byte)0 },
                    { 2043, 11, 2, 11, "D", (byte)0 },
                    { 2044, 12, 2, 12, "D", (byte)0 },
                    { 2045, 0, 2, 1, "E", (byte)0 },
                    { 2046, 1, 2, 2, "E", (byte)0 },
                    { 2047, 2, 2, 3, "E", (byte)0 },
                    { 2048, 3, 2, 4, "E", (byte)0 },
                    { 2049, 4, 2, 5, "E", (byte)0 },
                    { 2050, 5, 2, 6, "E", (byte)0 },
                    { 2051, 7, 2, 7, "E", (byte)0 },
                    { 2052, 8, 2, 8, "E", (byte)0 },
                    { 2053, 9, 2, 9, "E", (byte)0 },
                    { 2054, 10, 2, 10, "E", (byte)0 },
                    { 2055, 11, 2, 11, "E", (byte)0 },
                    { 2056, 12, 2, 12, "E", (byte)0 },
                    { 2057, 0, 2, 1, "F", (byte)0 },
                    { 2058, 1, 2, 2, "F", (byte)0 },
                    { 2059, 2, 2, 3, "F", (byte)0 },
                    { 2060, 3, 2, 4, "F", (byte)0 },
                    { 2061, 4, 2, 5, "F", (byte)0 },
                    { 2062, 5, 2, 6, "F", (byte)0 },
                    { 2063, 7, 2, 7, "F", (byte)0 },
                    { 2064, 8, 2, 8, "F", (byte)0 },
                    { 2065, 9, 2, 9, "F", (byte)0 },
                    { 2066, 10, 2, 10, "F", (byte)0 },
                    { 2067, 11, 2, 11, "F", (byte)0 },
                    { 2068, 12, 2, 12, "F", (byte)0 },
                    { 2069, 0, 2, 1, "G", (byte)1 },
                    { 2070, 1, 2, 2, "G", (byte)1 },
                    { 2071, 2, 2, 3, "G", (byte)1 },
                    { 2072, 3, 2, 4, "G", (byte)1 },
                    { 2073, 4, 2, 5, "G", (byte)1 },
                    { 2074, 5, 2, 6, "G", (byte)1 },
                    { 2075, 7, 2, 7, "G", (byte)1 },
                    { 2076, 8, 2, 8, "G", (byte)1 },
                    { 2077, 9, 2, 9, "G", (byte)1 },
                    { 2078, 10, 2, 10, "G", (byte)1 },
                    { 2079, 11, 2, 11, "G", (byte)1 },
                    { 2080, 12, 2, 12, "G", (byte)1 },
                    { 3001, 0, 3, 1, "A", (byte)2 },
                    { 3002, 3, 3, 2, "A", (byte)0 },
                    { 3003, 4, 3, 3, "A", (byte)0 },
                    { 3004, 5, 3, 4, "A", (byte)0 },
                    { 3005, 6, 3, 5, "A", (byte)0 },
                    { 3006, 7, 3, 6, "A", (byte)0 },
                    { 3007, 8, 3, 7, "A", (byte)0 },
                    { 3008, 9, 3, 8, "A", (byte)0 },
                    { 3009, 12, 3, 9, "A", (byte)2 },
                    { 3010, 0, 3, 1, "B", (byte)0 },
                    { 3011, 1, 3, 2, "B", (byte)0 },
                    { 3012, 2, 3, 3, "B", (byte)0 },
                    { 3013, 3, 3, 4, "B", (byte)0 },
                    { 3014, 4, 3, 5, "B", (byte)0 },
                    { 3015, 5, 3, 6, "B", (byte)0 },
                    { 3016, 6, 3, 7, "B", (byte)0 },
                    { 3017, 8, 3, 8, "B", (byte)0 },
                    { 3018, 9, 3, 9, "B", (byte)0 },
                    { 3019, 10, 3, 10, "B", (byte)0 },
                    { 3020, 11, 3, 11, "B", (byte)0 },
                    { 3021, 12, 3, 12, "B", (byte)0 },
                    { 3022, 13, 3, 13, "B", (byte)0 },
                    { 3023, 14, 3, 14, "B", (byte)0 },
                    { 3024, 0, 3, 1, "C", (byte)0 },
                    { 3025, 1, 3, 2, "C", (byte)0 },
                    { 3026, 2, 3, 3, "C", (byte)0 },
                    { 3027, 3, 3, 4, "C", (byte)0 },
                    { 3028, 4, 3, 5, "C", (byte)0 },
                    { 3029, 5, 3, 6, "C", (byte)0 },
                    { 3030, 6, 3, 7, "C", (byte)0 },
                    { 3031, 8, 3, 8, "C", (byte)0 },
                    { 3032, 9, 3, 9, "C", (byte)0 },
                    { 3033, 10, 3, 10, "C", (byte)0 },
                    { 3034, 11, 3, 11, "C", (byte)0 },
                    { 3035, 12, 3, 12, "C", (byte)0 },
                    { 3036, 13, 3, 13, "C", (byte)0 },
                    { 3037, 14, 3, 14, "C", (byte)0 },
                    { 3038, 0, 3, 1, "D", (byte)0 },
                    { 3039, 1, 3, 2, "D", (byte)0 },
                    { 3040, 2, 3, 3, "D", (byte)0 },
                    { 3041, 3, 3, 4, "D", (byte)0 },
                    { 3042, 4, 3, 5, "D", (byte)0 },
                    { 3043, 5, 3, 6, "D", (byte)0 },
                    { 3044, 6, 3, 7, "D", (byte)0 },
                    { 3045, 8, 3, 8, "D", (byte)0 },
                    { 3046, 9, 3, 9, "D", (byte)0 },
                    { 3047, 10, 3, 10, "D", (byte)0 },
                    { 3048, 11, 3, 11, "D", (byte)0 },
                    { 3049, 12, 3, 12, "D", (byte)0 },
                    { 3050, 13, 3, 13, "D", (byte)0 },
                    { 3051, 14, 3, 14, "D", (byte)0 },
                    { 3052, 0, 3, 1, "E", (byte)0 },
                    { 3053, 1, 3, 2, "E", (byte)0 },
                    { 3054, 2, 3, 3, "E", (byte)0 },
                    { 3055, 3, 3, 4, "E", (byte)0 },
                    { 3056, 4, 3, 5, "E", (byte)0 },
                    { 3057, 5, 3, 6, "E", (byte)0 },
                    { 3058, 6, 3, 7, "E", (byte)0 },
                    { 3059, 8, 3, 8, "E", (byte)0 },
                    { 3060, 9, 3, 9, "E", (byte)0 },
                    { 3061, 10, 3, 10, "E", (byte)0 },
                    { 3062, 11, 3, 11, "E", (byte)0 },
                    { 3063, 12, 3, 12, "E", (byte)0 },
                    { 3064, 13, 3, 13, "E", (byte)0 },
                    { 3065, 14, 3, 14, "E", (byte)0 },
                    { 3066, 0, 3, 1, "F", (byte)0 },
                    { 3067, 1, 3, 2, "F", (byte)0 },
                    { 3068, 2, 3, 3, "F", (byte)0 },
                    { 3069, 3, 3, 4, "F", (byte)0 },
                    { 3070, 4, 3, 5, "F", (byte)0 },
                    { 3071, 5, 3, 6, "F", (byte)0 },
                    { 3072, 6, 3, 7, "F", (byte)0 },
                    { 3073, 8, 3, 8, "F", (byte)0 },
                    { 3074, 9, 3, 9, "F", (byte)0 },
                    { 3075, 10, 3, 10, "F", (byte)0 },
                    { 3076, 11, 3, 11, "F", (byte)0 },
                    { 3077, 12, 3, 12, "F", (byte)0 },
                    { 3078, 13, 3, 13, "F", (byte)0 },
                    { 3079, 14, 3, 14, "F", (byte)0 },
                    { 3080, 0, 3, 1, "G", (byte)1 },
                    { 3081, 1, 3, 2, "G", (byte)1 },
                    { 3082, 2, 3, 3, "G", (byte)1 },
                    { 3083, 3, 3, 4, "G", (byte)1 },
                    { 3084, 5, 3, 5, "G", (byte)1 },
                    { 3085, 6, 3, 6, "G", (byte)1 },
                    { 3086, 7, 3, 7, "G", (byte)1 },
                    { 3087, 8, 3, 8, "G", (byte)1 },
                    { 4001, 0, 4, 1, "A", (byte)1 },
                    { 4002, 1, 4, 2, "A", (byte)1 },
                    { 4003, 3, 4, 3, "A", (byte)1 },
                    { 4004, 4, 4, 4, "A", (byte)1 },
                    { 4005, 6, 4, 5, "A", (byte)1 },
                    { 4006, 7, 4, 6, "A", (byte)1 },
                    { 4007, 9, 4, 7, "A", (byte)1 },
                    { 4008, 10, 4, 8, "A", (byte)1 },
                    { 4009, 0, 4, 1, "B", (byte)1 },
                    { 4010, 1, 4, 2, "B", (byte)1 },
                    { 4011, 3, 4, 3, "B", (byte)1 },
                    { 4012, 4, 4, 4, "B", (byte)1 },
                    { 4013, 6, 4, 5, "B", (byte)1 },
                    { 4014, 7, 4, 6, "B", (byte)1 },
                    { 4015, 9, 4, 7, "B", (byte)1 },
                    { 4016, 10, 4, 8, "B", (byte)1 },
                    { 4017, 0, 4, 1, "C", (byte)1 },
                    { 4018, 1, 4, 2, "C", (byte)1 },
                    { 4019, 3, 4, 3, "C", (byte)1 },
                    { 4020, 4, 4, 4, "C", (byte)1 },
                    { 4021, 6, 4, 5, "C", (byte)1 },
                    { 4022, 7, 4, 6, "C", (byte)1 },
                    { 4023, 9, 4, 7, "C", (byte)1 },
                    { 4024, 10, 4, 8, "C", (byte)1 },
                    { 4025, 0, 4, 1, "D", (byte)1 },
                    { 4026, 1, 4, 2, "D", (byte)1 },
                    { 4027, 3, 4, 3, "D", (byte)1 },
                    { 4028, 4, 4, 4, "D", (byte)1 },
                    { 4029, 6, 4, 5, "D", (byte)1 },
                    { 4030, 7, 4, 6, "D", (byte)1 },
                    { 4031, 9, 4, 7, "D", (byte)1 },
                    { 4032, 10, 4, 8, "D", (byte)1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ShowtimeId_Status",
                table: "Bookings",
                columns: new[] { "ShowtimeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StripeSessionId",
                table: "Bookings",
                column: "StripeSessionId",
                unique: true,
                filter: "[StripeSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_CreatedAtUtc",
                table: "Bookings",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingSeats_SeatId",
                table: "BookingSeats",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSeats_ShowtimeId_SeatId",
                table: "BookingSeats",
                columns: new[] { "ShowtimeId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Halls_CinemaId",
                table: "Halls",
                column: "CinemaId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_HallId_Row_Number",
                table: "Seats",
                columns: new[] { "HallId", "Row", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_HallId_StartsAtUtc",
                table: "Showtimes",
                columns: new[] { "HallId", "StartsAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_LocalDate",
                table: "Showtimes",
                column: "LocalDate");

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_MovieId_StartsAtUtc",
                table: "Showtimes",
                columns: new[] { "MovieId", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingSeats");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "Showtimes");

            migrationBuilder.DropTable(
                name: "Halls");

            migrationBuilder.DropTable(
                name: "Cinemas");
        }
    }
}
