namespace DriveTrack.Web.Components.Shared;

/// <summary>The surface a <c>DtLanguageSwitch</c> sits on, which decides how its select is painted.</summary>
public enum DtLanguageTone
{
    /// <summary>On <c>nav-bg</c>: the navigation's foot and the landing page's footer.</summary>
    /// <remarks>
    /// The one surface the select is drawn on. There was a light face for the landing page's header
    /// until that header took a language menu of its own; the enum stays so a second surface is an
    /// arm to add rather than a parameter to reintroduce.
    /// </remarks>
    Dark,
}
