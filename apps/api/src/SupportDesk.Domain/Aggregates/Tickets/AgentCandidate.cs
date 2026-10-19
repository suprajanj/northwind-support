namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Projection of agent workload and qualifications used by assignment policies.
/// </summary>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SpecializationCategoryIds);
