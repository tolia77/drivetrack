using DriveTrack.Application.Authorization;
using DriveTrack.Application.Common;
using DriveTrack.Application.Deliveries;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>One status and how many deliveries currently hold it.</summary>
public sealed record DeliveryStatusCount(DeliveryStatus Status, int Count);
