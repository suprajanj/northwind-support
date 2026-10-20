namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains the automatic triage decisions (priority, SLA window, and assignment) reached upon ticket creation.
/// </summary>
public sealed record TriageSummaryDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
