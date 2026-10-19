using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _sut;
    private readonly Customer _contoso;
    private readonly Customer _fabrikam;
    private readonly Category _technical;
    private readonly Category _billing;
    private readonly Agent _agentAlex;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _contoso = new Customer("Contoso", "it@contoso.com", "+1-555-0100", CustomerTier.Premium, FixedClock.DefaultNow);
        _fabrikam = new Customer("Fabrikam", "ops@fabrikam.com", "+1-555-0200", CustomerTier.Standard, FixedClock.DefaultNow);
        _technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _agentAlex = new Agent("Alex Turner", "alex@northwind.com", maxOpenTickets: 10, FixedClock.DefaultNow);

        _db.AddRange(_contoso, _fabrikam, _technical, _billing, _agentAlex);
        _db.SaveChanges();

        // Seed 4 distinct tickets
        var t1 = Ticket.Raise("TCK-0001", "Database timeout", "Frequent timeouts", _contoso.Id, _technical.Id, TicketPriority.Critical, FixedClock.DefaultNow);
        t1.AssignTo(_agentAlex.Id, FixedClock.DefaultNow);
        t1.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow);

        var t2 = Ticket.Raise("TCK-0002", "Invoice calculation issue", "VAT mismatch", _fabrikam.Id, _billing.Id, TicketPriority.High, FixedClock.DefaultNow);
        t2.ChangeStatus(TicketStatus.Open, FixedClock.DefaultNow);

        var t3 = Ticket.Raise("TCK-0003", "Contoso network latency", "Packet drop observed", _contoso.Id, _technical.Id, TicketPriority.High, FixedClock.DefaultNow);
        t3.AssignTo(_agentAlex.Id, FixedClock.DefaultNow);

        var t4 = Ticket.Raise("TCK-0004", "Printer setup", "Needs driver", _fabrikam.Id, _technical.Id, TicketPriority.Low, FixedClock.DefaultNow);

        _db.AddRange(t1, t2, t3, t4);
        _db.SaveChanges();

        _sut = new TicketQueries(_db, new FixedClock());
    }

    [Fact]
    public async Task GetPagedAsync_ExactMatchPriority_ReturnsOnlyMatchingPriority()
    {
        var query = new TicketQuery { Priority = TicketPriority.Critical };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-0001", result.Items[0].Reference);
        Assert.Equal(TicketPriority.Critical, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchOnCustomerName_MatchesCustomer()
    {
        var query = new TicketQuery { Search = "Contoso" };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("Contoso", item.Customer.Name));
    }

    [Fact]
    public async Task GetPagedAsync_CombinationOfFilters_CombinesWithAnd()
    {
        var query = new TicketQuery
        {
            CustomerId = _contoso.Id,
            Priority = TicketPriority.High,
            Status = TicketStatus.New
        };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-0003", result.Items[0].Reference);
        Assert.Equal(_contoso.Id, result.Items[0].Customer.Id);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(TicketStatus.New, result.Items[0].Status);
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnly_ReturnsOnlyTicketsWithoutAssignedAgent()
    {
        var query = new TicketQuery { UnassignedOnly = true };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Null(item.AssignedAgent));
        Assert.Contains(result.Items, i => i.Reference == "TCK-0002");
        Assert.Contains(result.Items, i => i.Reference == "TCK-0004");
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesTitleAndReference()
    {
        var byTitle = await _sut.GetPagedAsync(new TicketQuery { Search = "printer" }, CancellationToken.None);
        Assert.Single(byTitle.Items);
        Assert.Equal("TCK-0004", byTitle.Items[0].Reference);

        var byRef = await _sut.GetPagedAsync(new TicketQuery { Search = "TCK-0002" }, CancellationToken.None);
        Assert.Single(byRef.Items);
        Assert.Equal("TCK-0002", byRef.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersAppliedBeforeCountingAndPaging()
    {
        var query = new TicketQuery
        {
            CustomerId = _contoso.Id,
            Page = 1,
            PageSize = 1
        };

        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Contoso has 2 tickets total. With PageSize=1, TotalCount should be 2, TotalPages should be 2.
        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
