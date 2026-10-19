using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedExactDueMoment_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_AtRiskBoundary_BehavesCorrectly()
    {
        // 4 hour window: 10:00 to 14:00 (240 minutes).
        // 25% threshold = 60 minutes remaining (13:00).
        var created = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        // More than 25% remaining (e.g. 61 minutes left at 12:59) -> WithinSla
        var beforeThreshold = new DateTime(2026, 1, 1, 12, 59, 0, DateTimeKind.Utc);
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(created, due, null, beforeThreshold));

        // Exactly 25% remaining (60 minutes left at 13:00) -> AtRisk
        var exactThreshold = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc);
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(created, due, null, exactThreshold));

        // Less than 25% remaining (30 minutes left at 13:30) -> AtRisk
        var insideThreshold = new DateTime(2026, 1, 1, 13, 30, 0, DateTimeKind.Utc);
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(created, due, null, insideThreshold));

        // At exact due moment -> AtRisk (not yet past due)
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(created, due, null, due));

        // 1 second past due -> Breached
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(created, due, null, due.AddSeconds(1)));
    }
}
