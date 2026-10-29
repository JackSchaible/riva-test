using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoomBooking.Exceptions;
using RoomBooking.IntegrationTests.Infrastructure;
using RoomBooking.Models.Data;
using RoomBooking.Services;
using RoomBooking.Services.DbConnectionFactory;

namespace RoomBooking.IntegrationTests;

/// <summary>
/// Drives <see cref="BookingService"/>'s PostgreSQL error handling deterministically by injecting
/// errors into its insert attempts. The real insert and conflict lookup still run against the database.
/// </summary>
public class BookingServiceErrorHandlingTests(BookingApiFactory factory) : ApiTestBase(factory)
{
    private IDbConnectionFactory ConnectionFactory => Factory.Services.GetRequiredService<IDbConnectionFactory>();

    [Fact]
    public async Task Deadlock_IsRetried_AndTheBookingIsSaved()
    {
        FaultInjectingBookingService service = new(ConnectionFactory, PostgresErrorCodes.DeadlockDetected);

        Booking booking = await service.CreateAsync(await RoomIdAsync("Room A"), "Alice",
            At(10).UtcDateTime, At(11).UtcDateTime);

        Assert.Equal(2, service.Attempts);
        Assert.Equal(booking.Id, Assert.Single(await GetBookingRowsAsync()).Id);
    }

    [Fact]
    public async Task Deadlock_ThenRealOverlap_ReportsTheConflict()
    {
        int roomA = await RoomIdAsync("Room A");
        await CreateBookingAsync(roomA, At(10), At(11));
        FaultInjectingBookingService service = new(ConnectionFactory, PostgresErrorCodes.DeadlockDetected);

        BookingConflictException ex = await Assert.ThrowsAsync<BookingConflictException>(() =>
            service.CreateAsync(roomA, "Bob", At(10, 30).UtcDateTime, At(11, 30).UtcDateTime));

        Assert.Equal("Room A is already booked 10:00-11:00 UTC", ex.Message);
        Assert.Equal(2, service.Attempts);
        Assert.Single(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task Deadlock_OnEveryAttempt_GivesUpAfterTheMaximumAttempts()
    {
        FaultInjectingBookingService service = new(ConnectionFactory, PostgresErrorCodes.DeadlockDetected)
        {
            RepeatLastError = true
        };

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() =>
            service.CreateAsync(1, "Alice", At(10).UtcDateTime, At(11).UtcDateTime));

        Assert.Equal(PostgresErrorCodes.DeadlockDetected, ex.SqlState);
        Assert.Equal(BookingService.MaxDeadlockAttempts, service.Attempts);
        Assert.Empty(await GetBookingRowsAsync());
    }

    [Fact]
    public async Task Overlap_WhoseConflictWasCancelledBeforeTheLookup_UsesTheFallbackMessage()
    {
        // The exclusion violation is reported, but no active conflicting booking exists any more,
        // as if it had been cancelled between the failed insert and the lookup.
        FaultInjectingBookingService service = new(ConnectionFactory, PostgresErrorCodes.ExclusionViolation);

        BookingConflictException ex = await Assert.ThrowsAsync<BookingConflictException>(async () =>
            await service.CreateAsync(await RoomIdAsync("Room A"), "Alice", At(10).UtcDateTime, At(11).UtcDateTime));

        Assert.Equal("Room A is already booked for part of that time", ex.Message);
        Assert.Equal(1, service.Attempts);
    }
}
