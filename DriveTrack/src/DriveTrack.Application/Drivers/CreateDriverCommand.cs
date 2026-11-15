namespace DriveTrack.Application.Drivers;

/// <summary>
/// What a dispatcher sends to take on a driver (FR-35): an account to sign in with, the licence
/// number, and optionally the vehicle they start out holding.
/// <para>
/// The account fields are here because FR-35 creates the account as part of taking on the driver.
/// AD-24 keeps that narrow — <c>DriverService</c> writes through the Identity port only for this
/// create (after an email-in-use check), FR-37's rename and FR-39's delete, and otherwise only
/// reads the account behind a driver — and the alternative costs the single transaction AD-5
/// requires.
/// </para>
/// </summary>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">The address the account will sign in with.</param>
/// <param name="Password">The chosen password; hashed by Identity and never stored as sent (FR-8).</param>
/// <param name="LicenseNumber">Driving licence number.</param>
/// <param name="VehicleId">The vehicle to assign, or null to start holding none.</param>
public sealed record CreateDriverCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? LicenseNumber,
    int? VehicleId);
