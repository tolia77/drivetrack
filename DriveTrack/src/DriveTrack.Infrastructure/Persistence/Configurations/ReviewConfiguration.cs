using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriveTrack.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Review"/>.</summary>
internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        // The rating bound is a constraint, so a race cannot slip a 0 or a 6 past it.
        builder.ToTable(
            "Reviews",
            table => table.HasCheckConstraint("CK_Reviews_Rating", "rating >= 1 AND rating <= 5"));

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Text).HasMaxLength(2000).IsRequired();

        // One review per delivery. The original enforced this with an application
        // check, which is the worked example of an invariant concurrency defeats; declaring the
        // relationship one-to-one is what produces the unique index that actually holds.
        builder.HasOne<Delivery>()
            .WithOne(delivery => delivery.Review)
            .HasForeignKey<Review>(review => review.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        // A deleted client takes its reviews with it.
        builder.HasOne<Client>()
            .WithMany(client => client.Reviews)
            .HasForeignKey(review => review.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(review => review.ClientId);
    }
}
