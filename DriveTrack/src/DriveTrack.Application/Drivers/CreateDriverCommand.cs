namespace DriveTrack.Application.Drivers;

/// <summary>
/// What a dispatcher sends to take on a driver: an account to sign in with, the licence
/// number, and optionally the vehicle they start out holding.
/// <para>
/// The account fields are here because taking on a driver creates their account too. The
/// single-owner rule keeps that narrow — <c>DriverService</c> writes through the Identity port only
/// for this create (after an email-in-use check), the rename and the delete, and otherwise only
/// reads the account behind a driver — and the alternative costs the single transaction the unit of
/// work requires.
/// </para>
/// </summary>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account will sign in with.</param>
/// <param name="Password">The chosen password; hashed by Identity and never stored as sent.</param>
/// <param name="LicenseNumber">Driving licence number.</param>
/// <param name="VehicleId">The vehicle to assign, or null to start holding none.</param>
public sealed record CreateDriverCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? LicenseNumber,
    int? VehicleId);
