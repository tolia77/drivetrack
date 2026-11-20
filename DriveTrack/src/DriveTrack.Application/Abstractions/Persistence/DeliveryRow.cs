using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>A delivery and the names of its two parties, as one list row.</summary>
/// <param name="Delivery">The delivery.</param>
/// <param name="DriverName">The assigned driver's name, or null when there is none.</param>
/// <param name="ClientName">The requesting client's name, or null when there is none.</param>
public sealed record DeliveryRow(Delivery Delivery, string? DriverName, string? ClientName);
