using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class AgentAssignmentPolicyTests
{
    private const int CategoryGeneral = 1;
    private const int CategoryBilling = 2;

    [Fact]
    public void SelectAgent_ChoosesAgentWithFewestOpenTickets()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 1, FullName: "Agent One", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 5, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 2, FullName: "Agent Two", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 2, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 3, FullName: "Agent Three", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 4, SpecializationCategoryIds: [CategoryGeneral])
        };

        var (chosen, reason) = AgentAssignmentPolicy.SelectAgent(agents, CategoryGeneral, requiresSpecialist: false);

        Assert.NotNull(chosen);
        Assert.Equal(2, chosen.Id);
        Assert.Contains("Agent Two", reason);
    }

    [Fact]
    public void SelectAgent_BreaksTiesDeterministicallyByAgentId()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 10, FullName: "Agent Ten", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 5, FullName: "Agent Five", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, SpecializationCategoryIds: [CategoryGeneral])
        };

        var (chosen, _) = AgentAssignmentPolicy.SelectAgent(agents, CategoryGeneral, requiresSpecialist: false);

        Assert.NotNull(chosen);
        Assert.Equal(5, chosen.Id); // Deterministic tie-break picks lower ID
    }

    [Fact]
    public void SelectAgent_RequiresSpecialist_OnlySelectsQualifiedAgent()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 1, FullName: "General Agent", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 0, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 2, FullName: "Billing Specialist", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 3, SpecializationCategoryIds: [CategoryBilling])
        };

        var (chosen, _) = AgentAssignmentPolicy.SelectAgent(agents, CategoryBilling, requiresSpecialist: true);

        Assert.NotNull(chosen);
        Assert.Equal(2, chosen.Id);
    }

    [Fact]
    public void SelectAgent_IgnoresAgentsAtCapacity()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 1, FullName: "Full Agent", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 5, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 2, FullName: "Available Agent", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 4, SpecializationCategoryIds: [CategoryGeneral])
        };

        var (chosen, _) = AgentAssignmentPolicy.SelectAgent(agents, CategoryGeneral, requiresSpecialist: false);

        Assert.NotNull(chosen);
        Assert.Equal(2, chosen.Id);
    }

    [Fact]
    public void SelectAgent_IgnoresInactiveAgents()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 1, FullName: "Inactive Agent", IsActive: false, MaxOpenTickets: 10, OpenTicketCount: 0, SpecializationCategoryIds: [CategoryGeneral]),
            new(Id: 2, FullName: "Active Agent", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 3, SpecializationCategoryIds: [CategoryGeneral])
        };

        var (chosen, _) = AgentAssignmentPolicy.SelectAgent(agents, CategoryGeneral, requiresSpecialist: false);

        Assert.NotNull(chosen);
        Assert.Equal(2, chosen.Id);
    }

    [Fact]
    public void SelectAgent_WhenNobodyIsEligible_ReturnsNullAndClearReason()
    {
        var agents = new List<AgentCandidate>
        {
            new(Id: 1, FullName: "Full Agent", IsActive: true, MaxOpenTickets: 3, OpenTicketCount: 3, SpecializationCategoryIds: [CategoryGeneral])
        };

        var (chosen, reason) = AgentAssignmentPolicy.SelectAgent(agents, CategoryGeneral, requiresSpecialist: false);

        Assert.Null(chosen);
        Assert.Contains("limit", reason);
    }

    [Fact]
    public void IsAgentStillEligible_ValidatesCapacitySpecializationAndActiveStatus()
    {
        var activeSpecialist = new AgentCandidate(1, "Specialist", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 5, SpecializationCategoryIds: [CategoryBilling]);
        Assert.True(AgentAssignmentPolicy.IsAgentStillEligible(activeSpecialist, CategoryBilling, requiresSpecialist: true));

        var inactive = new AgentCandidate(2, "Inactive", IsActive: false, MaxOpenTickets: 5, OpenTicketCount: 1, SpecializationCategoryIds: [CategoryBilling]);
        Assert.False(AgentAssignmentPolicy.IsAgentStillEligible(inactive, CategoryBilling, requiresSpecialist: true));

        var nonSpecialist = new AgentCandidate(3, "General", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 1, SpecializationCategoryIds: [CategoryGeneral]);
        Assert.False(AgentAssignmentPolicy.IsAgentStillEligible(nonSpecialist, CategoryBilling, requiresSpecialist: true));
    }
}
