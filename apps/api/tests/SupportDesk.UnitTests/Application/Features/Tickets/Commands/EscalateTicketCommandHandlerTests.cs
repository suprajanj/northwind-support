using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(string reason = "Customer escalates issue due to business impact") =>
        new(reason, "lead@northwind.com");

    [Fact]
    public async Task HandleAsync_IncreasesPriorityByOneLevel_AndRecalculatesDueAt()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .Build();
        ticket.SetDueAt(context.Clock.UtcNow.AddHours(24), context.Clock.UtcNow);

        context.Tickets.Setup(t => t.GetByIdAsync(41, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        await context.EscalateTicket.HandleAsync(41, Request(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        // Base High window is 8h, Customer is Premium (multiplier 0.5) => 4h from now
        Assert.Equal(context.Clock.UtcNow.AddHours(4), ticket.DueAtUtc);
        Assert.Single(ticket.Escalations);
        var escalation = ticket.Escalations.First();
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal("lead@northwind.com", escalation.EscalatedBy);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsConflictException()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .Build();

        context.Tickets.Setup(t => t.GetByIdAsync(41, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(41, Request(), CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsNotOpen_ThrowsConflictException(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .WithStatus(status)
            .Build();

        context.Tickets.Setup(t => t.GetByIdAsync(41, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(41, Request(), CancellationToken.None));

        Assert.Contains(status.ToString(), ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketNotFound_ThrowsNotFoundException()
    {
        var context = new TicketCommandTestContext();
        context.Tickets
            .Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
