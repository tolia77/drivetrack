using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriveTrack.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Shift"/>.</summary>
internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");

        builder.HasKey(shift => shift.Id);

        // One open shift per driver, as a partial unique index rather than a lookup
        // two concurrent requests can both pass. The filter is raw SQL and so names the column
        // rather than the property; it is also the exact, immutable definition of open
        // which is why nothing time-dependent may appear in it.
        builder.HasIndex(shift => shift.DriverId)
            .IsUnique()
            .HasFilter("ended_at IS NULL")
            .HasDatabaseName("IX_Shifts_DriverId_Open");

        // A shift is meaningless without its driver, and references no delivery,
        // so nothing is orphaned by taking it along.
        builder.HasOne<Driver>()
            .WithMany(driver => driver.Shifts)
            .HasForeignKey(shift => shift.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
