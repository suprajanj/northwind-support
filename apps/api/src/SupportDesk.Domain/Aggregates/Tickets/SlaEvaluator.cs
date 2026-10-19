namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public const double DefaultAtRiskThresholdPercent = 25.0;

    /// <summary>
    /// Evaluates SLA status taking the creation timestamp into account to evaluate the AtRisk state.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? createdAtUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent = DefaultAtRiskThresholdPercent)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (createdAtUtc.HasValue && dueAtUtc.Value > createdAtUtc.Value)
        {
            var totalWindowTicks = (dueAtUtc.Value - createdAtUtc.Value).Ticks;
            var remainingTicks = (dueAtUtc.Value - nowUtc).Ticks;
            var thresholdTicks = totalWindowTicks * (atRiskThresholdPercent / 100.0);

            if (remainingTicks <= thresholdTicks)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }

    /// <summary>
    /// Legacy 3-parameter overload without created timestamp; evaluates NotApplicable, Met, Breached, or WithinSla.
    /// </summary>
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        Evaluate(null, dueAtUtc, resolvedAtUtc, nowUtc);
}
