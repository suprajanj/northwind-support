using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, applying automatic triage rules for priority, SLA, and assignment.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    SlaOptions slaOptions,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var agentDtos = await agentQueries.GetAllAsync(ct);
        var candidates = agentDtos
            .Select(a => new AgentCandidate(
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                a.OpenTicketCount,
                a.Specializations.Select(s => s.Id).ToList()))
            .ToList();

        var triage = TicketTriageEngine.Triage(
            category,
            customer.Tier,
            request.RequestedPriority,
            candidates,
            slaOptions,
            now);

        var reference = await tickets.NextReferenceAsync(ct);

        var ticket = Ticket.Raise(
            reference,
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            triage.Priority,
            now,
            dueAtUtc: triage.DueAtUtc,
            assignedAgentId: triage.AssignedAgentId);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} ({CustomerTier}) in category {CategoryId}. Priority: {Priority}, Assignee: {AgentId}.",
            ticket.Reference, ticket.CustomerId, customer.Tier, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);
        result.Triage = new TriageSummaryDto(
            triage.PriorityReason,
            triage.SlaReason,
            triage.AssignmentReason);

        return result;
    }
}
