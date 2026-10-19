using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Configurable SLA parameters loaded from application settings.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, double> BaseWindowHours { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TicketPriority.Low)] = 72,
        [nameof(TicketPriority.Medium)] = 24,
        [nameof(TicketPriority.High)] = 8,
        [nameof(TicketPriority.Critical)] = 4
    };

    public double PremiumCustomerMultiplier { get; init; } = 0.5;

    public double MinimumWindowHours { get; init; } = 1.0;

    public double AtRiskThresholdPercent { get; init; } = 25.0;

    public TimeSpan GetBaseWindow(TicketPriority priority)
    {
        var key = priority.ToString();
        if (BaseWindowHours != null && BaseWindowHours.TryGetValue(key, out var hours) && hours > 0)
        {
            return TimeSpan.FromHours(hours);
        }

        return priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72)
        };
    }

    public TimeSpan CalculateWindow(TicketPriority priority, CustomerTier tier)
    {
        var baseWindow = GetBaseWindow(priority);
        if (tier == CustomerTier.Premium)
        {
            var calculatedHours = baseWindow.TotalHours * PremiumCustomerMultiplier;
            var clampedHours = Math.Max(calculatedHours, MinimumWindowHours);
            return TimeSpan.FromHours(clampedHours);
        }

        return baseWindow;
    }

    public DateTime CalculateDueDate(DateTime fromUtc, TicketPriority priority, CustomerTier tier) =>
        fromUtc.Add(CalculateWindow(priority, tier));
}
