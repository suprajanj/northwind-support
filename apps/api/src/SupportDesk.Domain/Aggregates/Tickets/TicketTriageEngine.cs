using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed record TriageDecision(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AssignedAgentId,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);

/// <summary>
/// Domain engine that performs automatic triage on ticket creation or escalation.
/// </summary>
public static class TicketTriageEngine
{
    public static TriageDecision Triage(
        Category category,
        CustomerTier customerTier,
        TicketPriority? requestedPriority,
        IReadOnlyList<AgentCandidate> agents,
        SlaOptions slaOptions,
        DateTime nowUtc)
    {
        // BR-1: Priority selection
        TicketPriority priority;
        string priorityReason;
        if (category.ForcesCriticalPriority)
        {
            priority = TicketPriority.Critical;
            priorityReason = $"Category '{category.Name}' forces Critical priority regardless of requested priority.";
        }
        else if (requestedPriority.HasValue)
        {
            priority = requestedPriority.Value;
            priorityReason = $"Assigned requested priority {priority}.";
        }
        else
        {
            priority = TicketPriority.Medium;
            priorityReason = "Defaulted to Medium priority because no priority was requested.";
        }

        // BR-2 & BR-3 & BR-6: SLA window & due date
        var window = slaOptions.CalculateWindow(priority, customerTier);
        var dueAtUtc = nowUtc.Add(window);
        var baseHours = slaOptions.GetBaseWindow(priority).TotalHours;

        string slaReason;
        if (customerTier == CustomerTier.Premium)
        {
            slaReason = $"Base SLA window for {priority} is {baseHours}h; halved to {window.TotalHours}h for Premium customer tier (min floor {slaOptions.MinimumWindowHours}h).";
        }
        else
        {
            slaReason = $"Standard SLA window for {priority} is {baseHours}h.";
        }

        // BR-4 & BR-5: Agent assignment
        var (chosenAgent, assignmentReason) = AgentAssignmentPolicy.SelectAgent(
            agents, category.Id, category.RequiresSpecialist);

        return new TriageDecision(
            priority,
            dueAtUtc,
            chosenAgent?.Id,
            priorityReason,
            slaReason,
            assignmentReason);
    }
}
