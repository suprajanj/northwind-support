using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the TicketEscalation audit entity.
/// </summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TicketId).IsRequired();

        builder.Property(e => e.FromPriority).HasConversion<int>().IsRequired();
        builder.Property(e => e.ToPriority).HasConversion<int>().IsRequired();

        builder.Property(e => e.FromAgentId).IsRequired(false);
        builder.Property(e => e.ToAgentId).IsRequired(false);

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)").IsRequired(false);
        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)").IsRequired(false);

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        // Escalation history belongs strictly to the parent ticket; cascade delete removes history with ticket.
        builder.HasOne<Ticket>()
            .WithMany(t => t.Escalations)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Historical references to agents preserve referential integrity without deleting history.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index on TicketId + EscalatedAtUtc optimizes GET /api/tickets/{id}/escalations (newest first).
        builder.HasIndex(e => e.TicketId).HasDatabaseName("IX_TicketEscalations_TicketId");
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc }).HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
