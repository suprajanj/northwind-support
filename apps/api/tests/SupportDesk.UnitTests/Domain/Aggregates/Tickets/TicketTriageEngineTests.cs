using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTriageEngineTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private readonly SlaOptions _slaOptions = new();

    [Fact]
    public void Triage_CategoryForcesCriticalPriority_AlwaysSetsCritical()
    {
        var category = new Category("Security Outage", requiresSpecialist: false, forcesCriticalPriority: true);
        var agents = new List<AgentCandidate>
        {
            new(1, "Agent", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 2, SpecializationCategoryIds: [category.Id])
        };

        var result = TicketTriageEngine.Triage(category, CustomerTier.Standard, requestedPriority: TicketPriority.Low, agents, _slaOptions, Now);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Contains("forces Critical", result.PriorityReason);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
    }

    [Fact]
    public void Triage_NoRequestedPriority_DefaultsToMedium()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var agents = new List<AgentCandidate>
        {
            new(1, "Agent", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 2, SpecializationCategoryIds: [category.Id])
        };

        var result = TicketTriageEngine.Triage(category, CustomerTier.Standard, requestedPriority: null, agents, _slaOptions, Now);

        Assert.Equal(TicketPriority.Medium, result.Priority);
        Assert.Contains("Defaulted to Medium", result.PriorityReason);
        Assert.Equal(Now.AddHours(24), result.DueAtUtc);
    }

    [Fact]
    public void Triage_PremiumTier_HalvesSlaWindow()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var agents = new List<AgentCandidate>();

        var result = TicketTriageEngine.Triage(category, CustomerTier.Premium, requestedPriority: TicketPriority.High, agents, _slaOptions, Now);

        Assert.Equal(TicketPriority.High, result.Priority);
        // High base is 8h -> halved to 4h
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
        Assert.Contains("Premium", result.SlaReason);
    }

    [Fact]
    public void Triage_AssignsAgentAndProvidesReasons()
    {
        var category = new Category("Technical", requiresSpecialist: true, forcesCriticalPriority: false);
        var agents = new List<AgentCandidate>
        {
            new(1, "Alex", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 3, SpecializationCategoryIds: [category.Id])
        };

        var result = TicketTriageEngine.Triage(category, CustomerTier.Standard, requestedPriority: TicketPriority.Medium, agents, _slaOptions, Now);

        Assert.Equal(1, result.AssignedAgentId);
        Assert.Contains("Alex", result.AssignmentReason);
    }
}
