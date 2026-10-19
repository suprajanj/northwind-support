using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaOptionsTests
{
    private readonly SlaOptions _options = new();

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void CalculateWindow_StandardTier_UsesBaseWindow(TicketPriority priority, double expectedHours)
    {
        var window = _options.CalculateWindow(priority, CustomerTier.Standard);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void CalculateWindow_PremiumTier_HalvesBaseWindow(TicketPriority priority, double expectedHours)
    {
        var window = _options.CalculateWindow(priority, CustomerTier.Premium);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Fact]
    public void CalculateWindow_PremiumTier_EnforcesMinimumOneHourFloor()
    {
        // Custom options where critical base window is 1.5 hours -> halved = 0.75h -> clamped to 1.0h
        var custom = new SlaOptions
        {
            BaseWindowHours = new() { ["Critical"] = 1.5 },
            MinimumWindowHours = 1.0,
            PremiumCustomerMultiplier = 0.5
        };

        var window = custom.CalculateWindow(TicketPriority.Critical, CustomerTier.Premium);
        Assert.Equal(TimeSpan.FromHours(1.0), window);
    }

    [Fact]
    public void CalculateDueDate_AddsWindowToFromTimestamp()
    {
        var start = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var due = _options.CalculateDueDate(start, TicketPriority.High, CustomerTier.Standard);

        Assert.Equal(start.AddHours(8), due);
    }
}
