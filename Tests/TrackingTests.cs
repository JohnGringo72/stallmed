using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StallmedManager.Server.Controllers;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;
using Xunit;

namespace StallmedManager.Tests
{
    public class TrackingTests
    {
        // ---- Fake Traccar: απαντά με προκαθορισμένο JSON ανά path ----
        private class FakeTraccar : HttpMessageHandler
        {
            public Dictionary<string, string> Responses = new();
            public HttpStatusCode? FailWith;
            public string? LastAuth;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                LastAuth = request.Headers.Authorization?.ToString();
                if (FailWith.HasValue) return Task.FromResult(new HttpResponseMessage(FailWith.Value));
                var path = request.RequestUri!.AbsolutePath.TrimStart('/');
                var body = Responses.TryGetValue(path, out var b) ? b : "[]";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }

        private static StallmedContext CreateContext() =>
            new(new DbContextOptionsBuilder<StallmedContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static TraccarClient CreateClient(FakeTraccar fake) =>
            new(new HttpClient(fake),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Traccar:BaseUrl"] = "http://traccar.test:8082",
                    ["Traccar:Token"] = "secret-token"
                }).Build(),
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<TraccarClient>.Instance);

        private static TrackingController CreateController(StallmedContext ctx, FakeTraccar fake, string email, string role)
        {
            var controller = new TrackingController(ctx, CreateClient(fake), NullLogger<TrackingController>.Instance);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, role)
                    }, "test"))
                }
            };
            return controller;
        }

        private const string Devices = @"[
            {""id"":1,""name"":""ST-901L"",""uniqueId"":""9171097738"",""status"":""online"",""lastUpdate"":""2026-10-15T08:00:00.000+00:00""},
            {""id"":2,""name"":""FMC130"",""uniqueId"":""864275074239800"",""status"":""offline""}]";

        private const string Positions = @"[
            {""id"":10,""deviceId"":1,""protocol"":""h02"",""valid"":true,""latitude"":40.6,""longitude"":22.9,""speed"":10,
             ""fixTime"":""2026-10-15T08:00:00.000+00:00"",""attributes"":{""ignition"":true}},
            {""id"":11,""deviceId"":2,""protocol"":""teltonika"",""valid"":true,""latitude"":37.9,""longitude"":23.7,""speed"":0,
             ""fixTime"":""2026-10-15T07:00:00.000+00:00"",""attributes"":{""temp1"":5.5,""temp2"":850,""power"":12.6,""raw"":""000F""}}]";

        [Fact]
        public void Position_MissingValues_AreNull_NotZero()
        {
            var p = TraccarMapper.Position(JsonDocument.Parse(@"{""id"":1,""deviceId"":1,""latitude"":1,""longitude"":2,""attributes"":{}}").RootElement);
            Assert.Null(p.SpeedKmh);
            Assert.Null(p.Ignition);
            Assert.Null(p.PowerV);
            Assert.Null(p.Rssi);
            Assert.Empty(p.Temperatures);
        }

        [Fact]
        public void Position_ConvertsKnots_AndFlagsBadTemperature()
        {
            var p = TraccarMapper.Position(JsonDocument.Parse(Positions).RootElement[1]);
            Assert.Equal(0, p.SpeedKmh);                       // πραγματικό 0 -- όχι null
            var t1 = p.Temperatures.Single(t => t.Key == "temp1");
            var t2 = p.Temperatures.Single(t => t.Key == "temp2");
            Assert.Equal(5.5, t1.Value);
            Assert.True(t2.Malformed);                          // 850 = αποσυνδεδεμένος αισθητήρας
            Assert.Null(t2.Value);
            Assert.False(p.Attributes.ContainsKey("raw"));      // raw μόνο στο Raw Data

            var moving = TraccarMapper.Position(JsonDocument.Parse(Positions).RootElement[0]);
            Assert.Equal(18.5, moving.SpeedKmh);                // 10 knots
        }

        [Fact]
        public async Task Admin_SeesAllDevices_WithTokenServerSide()
        {
            using var ctx = CreateContext();
            var fake = new FakeTraccar { Responses = { ["api/devices"] = Devices, ["api/positions"] = Positions } };
            var result = await CreateController(ctx, fake, "admin@test", "admin").Overview();

            var dto = Assert.IsType<TrackingOverviewDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(2, dto.Devices.Count);
            Assert.Equal("Bearer secret-token", fake.LastAuth);
        }

        [Fact]
        public async Task User_SeesOnlyDevicesOfAssignedVehicle()
        {
            using var ctx = CreateContext();
            ctx.Users.Add(new User { IdUser = 5, Email = "driver@test", Username = "d", Password = "", Firstname = "", Lastname = "", Role = "user", Active = true });
            ctx.TrackingAssets.Add(new TrackingAsset { AssetID = 100, Name = "Ψυγείο 1" });
            ctx.TrackingAssetDevices.Add(new TrackingAssetDevice { AssetID = 100, TraccarDeviceId = 2, ValidFromUtc = DateTime.UtcNow.AddDays(-1) });
            ctx.TrackingUserAccess.Add(new TrackingUserAccess { UserID = 5, AccessLevel = TrackingAccessLevels.User, AssetID = 100 });
            ctx.TrackingTempRanges.Add(new TrackingTempRange { Scope = "Default", MinC = 2, MaxC = 8 });
            await ctx.SaveChangesAsync();

            var fake = new FakeTraccar { Responses = { ["api/devices"] = Devices, ["api/positions"] = Positions } };
            var controller = CreateController(ctx, fake, "driver@test", "user");
            var dto = Assert.IsType<TrackingOverviewDto>(Assert.IsType<OkObjectResult>((await controller.Overview()).Result).Value);

            var device = Assert.Single(dto.Devices);
            Assert.Equal(2, device.Id);
            Assert.Equal("Ψυγείο 1", device.AssetName);
            Assert.Equal(8, device.TempRange!.MaxC);

            // Η άλλη συσκευή δεν φαίνεται ούτε απευθείας ούτε στο Raw Data.
            Assert.IsType<NotFoundResult>((await controller.Device(1)).Result);
            Assert.IsType<ForbidResult>((await controller.Raw(2, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow)).Result);
        }

        [Fact]
        public async Task UserWithoutAccess_IsForbidden()
        {
            using var ctx = CreateContext();
            var result = await CreateController(ctx, new FakeTraccar(), "nobody@test", "user").Overview();
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task TraccarDown_Returns503_NotEmptyList()
        {
            using var ctx = CreateContext();
            var fake = new FakeTraccar { FailWith = HttpStatusCode.BadGateway };
            var result = await CreateController(ctx, fake, "admin@test", "admin").Overview();
            Assert.Equal(503, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task Temperature_FindsExcursions_FromConfiguredRange()
        {
            using var ctx = CreateContext();
            ctx.TrackingTempRanges.Add(new TrackingTempRange { Scope = "Default", MinC = 2, MaxC = 8 });
            await ctx.SaveChangesAsync();
            string Pos(int min, double t) =>
                $@"{{""id"":{min},""deviceId"":2,""valid"":true,""latitude"":1,""longitude"":1,""fixTime"":""2026-10-15T08:{min:00}:00Z"",""attributes"":{{""temp1"":{t.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}";
            var fake = new FakeTraccar
            {
                Responses = { ["api/positions"] = $"[{Pos(0, 5)},{Pos(1, 9)},{Pos(2, 10.5)},{Pos(3, 6)},{Pos(4, 1.5)},{Pos(5, 4)}]" }
            };
            var result = await CreateController(ctx, fake, "admin@test", "admin")
                .Temperature(2, new DateTimeOffset(2026, 10, 15, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

            var dto = Assert.IsType<TrackingTemperatureDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
            var s = Assert.Single(dto.Series);
            Assert.Equal(1.5, s.Min);
            Assert.Equal(10.5, s.Max);
            Assert.Equal(2, s.Excursions.Count);
            Assert.Equal("high", s.Excursions[0].Kind);
            Assert.Equal(10.5, s.Excursions[0].Peak);
            Assert.Equal("low", s.Excursions[1].Kind);
        }

        [Fact]
        public async Task Route_RejectsTooLongRange()
        {
            using var ctx = CreateContext();
            var result = await CreateController(ctx, new FakeTraccar(), "admin@test", "admin")
                .Route(1, DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
