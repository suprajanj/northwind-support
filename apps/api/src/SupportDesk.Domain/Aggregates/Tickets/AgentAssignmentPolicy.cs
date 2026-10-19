namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Evaluates agent eligibility and assigns tickets based on capacity, specialization, and workload.
/// </summary>
public static class AgentAssignmentPolicy
{
    /// <summary>
    /// Selects an eligible agent per BR-4 and BR-5: active, specialized where required, strictly below MaxOpenTickets,
    /// fewest open tickets, with deterministic tie-break by Agent Id.
    /// </summary>
    public static (AgentCandidate? Agent, string Reason) SelectAgent(
        IReadOnlyList<AgentCandidate> agents,
        int categoryId,
        bool requiresSpecialist)
    {
        if (agents.Count == 0)
        {
            return (null, "No agents configured in the system.");
        }

        var activeAgents = agents.Where(a => a.IsActive).ToList();
        if (activeAgents.Count == 0)
        {
            return (null, "No active agents available.");
        }

        var qualifiedAgents = requiresSpecialist
            ? activeAgents.Where(a => a.SpecializationCategoryIds.Contains(categoryId)).ToList()
            : activeAgents;

        if (qualifiedAgents.Count == 0)
        {
            return (null, "No active specialist agent available for this category.");
        }

        var availableAgents = qualifiedAgents
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .ToList();

        if (availableAgents.Count == 0)
        {
            return (null, requiresSpecialist
                ? "All specialist agents for this category are at their open ticket limit."
                : "All active agents are at their open ticket limit.");
        }

        var chosen = availableAgents
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .First();

        var specialistSuffix = requiresSpecialist ? " (specialist)" : string.Empty;
        var reason = $"Assigned to {chosen.FullName}{specialistSuffix} with {chosen.OpenTicketCount}/{chosen.MaxOpenTickets} open tickets.";

        return (chosen, reason);
    }

    /// <summary>
    /// Checks whether an existing assigned agent remains eligible to retain a ticket during escalation.
    /// </summary>
    public static bool IsAgentStillEligible(
        AgentCandidate agent,
        int categoryId,
        bool requiresSpecialist)
    {
        if (!agent.IsActive)
        {
            return false;
        }

        if (requiresSpecialist && !agent.SpecializationCategoryIds.Contains(categoryId))
        {
            return false;
        }

        // When evaluating the current assignee for an existing open ticket, their current open count
        // already includes this ticket and must not exceed MaxOpenTickets.
        return agent.OpenTicketCount <= agent.MaxOpenTickets;
    }
}
