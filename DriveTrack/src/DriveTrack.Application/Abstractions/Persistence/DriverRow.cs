using DriveTrack.Application.Common;
using DriveTrack.Application.Drivers;
using DriveTrack.Domain.Drivers;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>A driver, with the vehicle they hold, and the account fields a roster row shows.</summary>
/// <param name="Driver">The driver row, with its vehicle loaded.</param>
/// <param name="FirstName">The account's given name.</param>
/// <param name="LastName">The account's family name.</param>
/// <param name="Email">The account's sign-in address.</param>
public sealed record DriverRow(Driver Driver, string FirstName, string LastName, string Email);
