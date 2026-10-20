using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates ticket priority, recalculates SLA due date, re-evaluates owner, and records escalation audit history.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    SlaOptions slaOptions,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(int ticketId, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException("Ticket is already at Critical priority and cannot be escalated.");
        }

        if (!ticket.IsOpen)
        {
            throw new ConflictException($"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var nextPriority = Ticket.NextPriority(ticket.Priority);
        var now = clock.UtcNow;
        var newDueAtUtc = slaOptions.CalculateDueDate(now, nextPriority, customer.Tier);

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

        int? newAgentId;
        var currentAgent = ticket.AssignedAgentId.HasValue
            ? candidates.FirstOrDefault(a => a.Id == ticket.AssignedAgentId.Value)
            : null;

        if (currentAgent is not null && AgentAssignmentPolicy.IsAgentStillEligible(currentAgent, category.Id, category.RequiresSpecialist))
        {
            newAgentId = currentAgent.Id;
        }
        else
        {
            var (reassignedAgent, _) = AgentAssignmentPolicy.SelectAgent(candidates, category.Id, category.RequiresSpecialist);
            newAgentId = reassignedAgent?.Id;
        }

        ticket.Escalate(
            nextPriority,
            newDueAtUtc,
            newAgentId,
            request.Reason,
            request.EscalatedBy ?? "Unknown",
            now);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated to {Priority} by {EscalatedBy}.",
            ticket.Reference, ticket.Priority, request.EscalatedBy);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}
