using DriveTrack.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriveTrack.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Vehicle"/>.</summary>
internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(vehicle => vehicle.Id);

        builder.Property(vehicle => vehicle.Model).HasMaxLength(100).IsRequired();
        builder.Property(vehicle => vehicle.LicensePlate).HasMaxLength(20).IsRequired();
        builder.Property(vehicle => vehicle.CapacityKg).HasPrecision(10, 2);

        // One plate, one vehicle - in the schema, because two concurrent creates defeat
        // a "does this plate already exist" query no matter where it is written.
        builder.HasIndex(vehicle => vehicle.LicensePlate).IsUnique();
    }
}
