namespace DriveTrack.Web.Components.Shared.DesignSystem;

/// <summary>
/// The icon vocabulary, closed at compile time.
/// <para>
/// The scaffold's navigation carried three <c>bi-*-nav-menu</c> spans with no CSS rule behind
/// them — an empty box on every screen, and nothing in the build said so. Naming the set as an
/// enum makes a typo a compile error instead, and makes "which icons does this product have?" a
/// question with one answer.
/// </para>
/// <para>
/// Every member is drawn by <c>Icon.razor</c>. Adding one without a path there leaves a blank
/// <c>&lt;svg&gt;</c>, which <c>SharedComponentTests</c> catches by counting the two against each
/// other.
/// </para>
/// </summary>
public enum IconName
{
    /// <summary>The landing page.</summary>
    Home,

    /// <summary>The signed-in caller's own profile.</summary>
    Profile,

    /// <summary>Entering the application.</summary>
    SignIn,

    /// <summary>Leaving the application.</summary>
    SignOut,

    /// <summary>Creating an account.</summary>
    Register,

    /// <summary>A create action (the palette's create colour).</summary>
    Create,

    /// <summary>An edit action (the palette's edit colour).</summary>
    Edit,

    /// <summary>A destructive action (the palette's destructive colour).</summary>
    Delete,

    /// <summary>Accepting a prompt.</summary>
    Confirm,

    /// <summary>Dismissing a dialog.</summary>
    Close,

    /// <summary>The way back from a dead end.</summary>
    Back,

    /// <summary>The way on: the next page of a list (the mirror of <see cref="Back"/>).</summary>
    Forward,

    /// <summary>A caution notice.</summary>
    Warning,

    /// <summary>Access refused.</summary>
    Forbidden,

    /// <summary>Nothing at this address.</summary>
    NotFound,

    /// <summary>A point on the map.</summary>
    Location,

    /// <summary>The client roster.</summary>
    Clients,

    /// <summary>The dispatcher roster.</summary>
    Dispatchers,
    /// <summary>The driver roster.</summary>
    Drivers,

    /// <summary>The vehicle fleet.</summary>
    Vehicles,

    /// <summary>Deliveries: the parcel that is the product's central record.</summary>
    Deliveries,

    /// <summary>A delivery's append-only history.</summary>
    Timeline,

    /// <summary>Looking a place up by its address.</summary>
    Search,

    /// <summary>The log of outbound notification attempts.</summary>
    Notifications,

    /// <summary>Capturing the hand-over: the action that takes the photographs.</summary>
    Camera,

    /// <summary>A captured proof of delivery, as something to read rather than to take.</summary>
    Proof,

    /// <summary>A driver's conversation, and the destination that opens it.</summary>
    Chat,

    /// <summary>Sending the message that has been typed.</summary>
    Send,

    /// <summary>A client's verdict on a delivery, and the destination that opens them.</summary>
    Reviews,

    /// <summary>A driver's stretch of time on duty, and the two destinations that open them.</summary>
    Shifts,

    /// <summary>The control that reveals the navigation below the phone breakpoint.</summary>
    Menu,

    /// <summary>A sortable column header, and the direction it can be taken in.</summary>
    Sort,

    /// <summary>
    /// The column the rows are actually ordered by, smallest first. The stacked pair above says
    /// "this sorts"; a single chevron says which way it went, so ascending and descending differ by
    /// shape rather than only by the <c>aria-sort</c> a sighted reader never hears.
    /// </summary>
    SortAscending,

    /// <summary>The same column ordered the other way.</summary>
    SortDescending,

    /// <summary>The reading language, and the control that changes it.</summary>
    Language,

    /// <summary>A control that opens a menu below it.</summary>
    Expand,
}
