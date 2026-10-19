using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private static readonly DateTime NewDue = Now.AddHours(4);

    [Fact]
    public void Escalate_RaisesPriorityOneLevel_AndRecordsHistory()
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(1)
            .Build();

        var initialDue = Now.AddHours(24);
        ticket.SetDueAt(initialDue, Now);

        var escalation = ticket.Escalate(
            TicketPriority.High,
            NewDue,
            newAgentId: 2,
            reason: "Customer reported escalation due to upcoming release.",
            escalatedBy: "Team Lead",
            nowUtc: Now);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(NewDue, ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(Now, ticket.UpdatedAtUtc);

        Assert.Single(ticket.Escalations);
        Assert.Same(escalation, ticket.Escalations.First());

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Equal(initialDue, escalation.FromDueAtUtc);
        Assert.Equal(NewDue, escalation.ToDueAtUtc);
        Assert.Equal("Customer reported escalation due to upcoming release.", escalation.Reason);
        Assert.Equal("Team Lead", escalation.EscalatedBy);
        Assert.Equal(Now, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void NextPriority_StepsUpThroughTiers()
    {
        Assert.Equal(TicketPriority.Medium, Ticket.NextPriority(TicketPriority.Low));
        Assert.Equal(TicketPriority.High, Ticket.NextPriority(TicketPriority.Medium));
        Assert.Equal(TicketPriority.Critical, Ticket.NextPriority(TicketPriority.High));
        Assert.Throws<BusinessRuleViolationException>(() => Ticket.NextPriority(TicketPriority.Critical));
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_ThrowsBusinessRuleViolation()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.Critical, NewDue, null, "A valid reason for escalation.", "Lead", Now));

        Assert.Contains("Critical", ex.Message);
    }

    [Fact]
    public void Escalate_WhenResolved_ThrowsBusinessRuleViolation()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Resolved).Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.High, NewDue, null, "A valid reason for escalation.", "Lead", Now));

        Assert.Contains("Resolved", ex.Message);
    }

    [Fact]
    public void Escalate_WhenClosed_ThrowsBusinessRuleViolation()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Closed).Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.High, NewDue, null, "A valid reason for escalation.", "Lead", Now));

        Assert.Contains("Closed", ex.Message);
    }

    [Theory]
    [InlineData("1234")] // Too short (< 5 chars)
    [InlineData("")]     // Empty
    public void Escalate_InvalidReason_Throws(string reason)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.ThrowsAny<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.Medium, NewDue, null, reason, "Lead", Now));
    }
}
