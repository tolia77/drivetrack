using System.Globalization;
using System.Text.RegularExpressions;
using DriveTrack.Application.Common;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Account;
using DriveTrack.Web.Components.Shared;

namespace DriveTrack.Integration.Tests.Components;

/// <summary>
/// A refusal appears where the user is looking.
/// <para>
/// Two halves. A few codes the service raises with no field are really about one input - the
/// capacity refusal is about the weight box - so the Blazor adapter pins them to that input and
/// <c>DtFieldError</c> draws them under it; that is asserted through <see cref="FailureKeys"/> and
/// the rendered banner. And after a refusal the first error on the screen is scrolled into view
/// and focused, which only a browser can do - so, as for the dialog, it is asserted against the
/// banner's own module, read as source: there is no JavaScript runner in this solution.
/// </para>
/// </summary>
public class FailureRevealTests
{
    [Theory]
    [InlineData(nameof(ErrorCode.DELIVERY_EXCEEDS_VEHICLE_CAPACITY), "PackageWeightKg")]
    [InlineData(nameof(ErrorCode.AUTH_EMAIL_ALREADY_IN_USE), "Email")]
    [InlineData(nameof(ErrorCode.AUTH_CURRENT_PASSWORD_INCORRECT), "CurrentPassword")]
    [InlineData(nameof(ErrorCode.FLEET_LICENSE_PLATE_IN_USE), "LicensePlate")]
    public void A_refusal_about_one_input_is_pinned_to_that_input(string code, string field)
    {
        var failure = new ConflictException(Enum.Parse<ErrorCode>(code), "refused");

        Assert.Equal([new ScreenFailure(field, code)], FailureKeys.For(failure));
    }

    [Fact]
    public void The_capacity_refusal_as_the_service_raises_it_lands_on_the_weight_box()
    {
        // The real shape, not just the code: DeliveryCapacity throws a DomainRuleException with no
        // field, and that is what used to put it in the banner at the top of the dialog.
        var failure = new DomainRuleException(ErrorCode.DELIVERY_EXCEEDS_VEHICLE_CAPACITY, "too heavy");

        Assert.Equal("PackageWeightKg", Assert.Single(FailureKeys.For(failure)).Field);
    }

    [Fact]
    public void The_current_password_refusal_as_the_service_raises_it_lands_on_its_box()
    {
        // A ValidationException with an empty field list - the branch that is not a field error.
        var failure = new ValidationException(ErrorCode.AUTH_CURRENT_PASSWORD_INCORRECT, "wrong", []);

        Assert.Equal("CurrentPassword", Assert.Single(FailureKeys.For(failure)).Field);
    }

    [Fact]
    public void Not_found_forbidden_and_transition_refusals_stay_fieldless()
    {
        DriveTrackException[] refusals =
        [
            new NotFoundException(ErrorCode.COMMON_NOT_FOUND, "missing"),
            new ForbiddenException(ErrorCode.AUTH_FORBIDDEN, "forbidden"),
            new ConflictException(ErrorCode.DELIVERY_INVALID_STATUS_TRANSITION, "transition"),
        ];

        foreach (var refusal in refusals)
        {
            Assert.Null(Assert.Single(FailureKeys.For(refusal)).Field);
        }
    }

    [Fact]
    public async Task A_pinned_refusal_on_a_screen_without_that_box_still_reaches_the_banner()
    {
        // FR-83: the Drivers edit dialog renders no address box, so an e-mail refusal pinned to
        // Email has nothing to sit under, and the banner is what keeps it on the page.
        var failures = FailureKeys.For(new ConflictException(ErrorCode.AUTH_EMAIL_ALREADY_IN_USE, "taken"));

        var html = await ComponentRenderer.RenderAsync<DtFailureBanner>(
            new Dictionary<string, object?>
            {
                ["InlineFields"] = "FirstName,LastName,LicenseNumber",
                ["Failures"] = failures,
            });

        Assert.Equal(1, SharedMarkup.Occurrences(html, "alert alert-danger"));
        Assert.True(SharedMarkup.IsUkrainian(SharedMarkup.TextOf(html)));
    }

    [Fact]
    public async Task A_claimed_capacity_refusal_leaves_the_banner_empty()
    {
        // The delivery dialog claims the weight box, so the capacity refusal is drawn there and the
        // banner has nothing to say about it.
        var failures = FailureKeys.For(
            new DomainRuleException(ErrorCode.DELIVERY_EXCEEDS_VEHICLE_CAPACITY, "too heavy"));

        var html = await ComponentRenderer.RenderAsync<DtFailureBanner>(
            new Dictionary<string, object?>
            {
                ["InlineFields"] = "Pickup,Dropoff,PackageDetails,PackageWeightKg,DeliveryNotes,WindowLatestAt",
                ["Failures"] = failures,
            });

        Assert.DoesNotContain("alert-danger", html, StringComparison.Ordinal);

        var field = await ComponentRenderer.RenderAsync<DtFieldError>(
            new Dictionary<string, object?>
            {
                ["Field"] = "PackageWeightKg",
                ["Failures"] = failures,
            });

        Assert.Contains("dt-field-error", field, StringComparison.Ordinal);
        Assert.True(SharedMarkup.IsUkrainian(SharedMarkup.TextOf(field)));
    }

    [Fact]
    public async Task The_refusal_banner_can_take_focus_and_the_anchor_is_always_there()
    {
        var empty = await ComponentRenderer.RenderAsync<DtFailureBanner>(new Dictionary<string, object?>());

        // Something to scope the reveal from even when every refusal was claimed by a box.
        Assert.Matches(@"<span[^>]*class=""dt-failure-anchor""[^>]*hidden", empty);

        var html = await ComponentRenderer.RenderAsync<DtFailureBanner>(
            new Dictionary<string, object?>
            {
                ["Failures"] = (IReadOnlyList<ScreenFailure>)
                [
                    new ScreenFailure(null, nameof(ErrorCode.FLEET_VEHICLE_IN_USE)),
                ],
            });

        var banner = Regex.Match(html, @"<div[^>]*alert-danger[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.True(banner.Success, html);
        Assert.Contains(@"tabindex=""-1""", banner.Value, StringComparison.Ordinal);

        // The screen's own danger sentence matches the same selector, so it must take focus too.
        var message = await ComponentRenderer.RenderAsync<DtFailureBanner>(
            new Dictionary<string, object?>
            {
                ["Message"] = "Відмовлено",
                ["Variant"] = DtBannerVariant.Danger,
            });

        Assert.Matches(@"<div[^>]*alert-danger[^>]*tabindex=""-1""", message);

        // And a field error, for the box with no input of its own.
        var field = await ComponentRenderer.RenderAsync<DtFieldError>(
            new Dictionary<string, object?>
            {
                ["Field"] = "Signature",
                ["Failures"] = (IReadOnlyList<ScreenFailure>)
                [
                    new ScreenFailure("Signature", nameof(ErrorCode.COMMON_FIELD_REQUIRED)),
                ],
            });

        Assert.Matches(@"<div[^>]*dt-field-error[^>]*tabindex=""-1""", field);
    }

    [Fact]
    public async Task A_pinned_refusal_in_the_banner_is_not_labelled_with_a_box_the_screen_lacks()
    {
        // The vehicle screen can raise the capacity refusal but renders no weight box; a label
        // there would name an input the form does not have.
        var failures = FailureKeys.For(
            new DomainRuleException(ErrorCode.DELIVERY_EXCEEDS_VEHICLE_CAPACITY, "too heavy"));

        var html = await ComponentRenderer.RenderAsync<DtFailureBanner>(
            new Dictionary<string, object?>
            {
                ["InlineFields"] = "Model,LicensePlate,CapacityKg,Mileage",
                ["Failures"] = failures,
            },
            culture: CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(1, SharedMarkup.Occurrences(html, "alert alert-danger"));
        Assert.DoesNotContain("<strong", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Package weight, kg:", SharedMarkup.TextOf(html), StringComparison.Ordinal);
    }

    [Fact]
    public void The_banner_reveals_only_when_a_new_non_empty_list_arrives()
    {
        // Typing after a refusal re-renders with the same list; that must not scroll or steal focus.
        var source = SharedMarkup.ReadShared("DtFailureBanner.razor");

        Assert.Contains("ReferenceEquals(Failures, _seen)", source, StringComparison.Ordinal);
        Assert.Contains("_revealPending = Failures is { Count: > 0 };", source, StringComparison.Ordinal);

        // And it is spent by the render that reveals, so the next unrelated render does nothing.
        var afterRender = source[source.IndexOf("OnAfterRenderAsync", StringComparison.Ordinal)..];

        Assert.Matches(@"if \(!_revealPending\)\s*\{\s*return;", afterRender);
        Assert.Contains("_revealPending = false;", afterRender, StringComparison.Ordinal);
        Assert.Contains(@"InvokeVoidAsync(""reveal"", _anchor)", afterRender, StringComparison.Ordinal);

        // Best-effort: a module that fails to load must not take the circuit down after a refusal.
        Assert.Contains("catch (JSException)", afterRender, StringComparison.Ordinal);

        // OnAfterRender never runs under static rendering, which is what keeps sign-in and register
        // script-free; a reveal started anywhere else would reach the page during prerender too.
        Assert.Equal(1, SharedMarkup.Occurrences(source, "\"reveal\""));
    }

    [Fact]
    public void The_reveal_is_scoped_to_the_open_dialog_else_the_page()
    {
        var module = SharedMarkup.ReadShared("DtFailureBanner.razor.js");

        var scope = FunctionBody(module, "scopeOf");

        // Read from the banner's own anchor, so stacked or unrelated dialogs cannot pick the region.
        Assert.Contains(@"anchor.closest(""dialog[open]"")", scope, StringComparison.Ordinal);
        Assert.Contains(@"anchor.closest(""main"")", scope, StringComparison.Ordinal);

        // A page banner stands down while a dialog is open, so only the active region reveals.
        Assert.Matches(
            @"if \(document\.querySelector\(""dialog\[open\]""\)\)\s*\{\s*return null;",
            scope);

        // The dialogs' markup is always rendered and bound to the same list as the page, so an
        // error inside a closed one is not the first error the user can see.
        Assert.Matches(@"dialog\s*&&\s*!dialog\.open", FunctionBody(module, "firstError"));
    }

    [Fact]
    public void Two_banners_on_one_list_reveal_once()
    {
        // The page's banner and each dialog's are bound to one failure list, and each asks.
        var module = SharedMarkup.ReadShared("DtFailureBanner.razor.js");
        var reveal = FunctionBody(module, "reveal");

        // Scoped before de-duplicating, so a banner that stands down cannot swallow the request.
        Assert.True(
            reveal.IndexOf("scopeOf(anchor)", StringComparison.Ordinal)
                < reveal.IndexOf("pending = true;", StringComparison.Ordinal),
            reveal);
        Assert.Matches(@"if \(!scope \|\| pending\)\s*\{\s*return;", reveal);
        Assert.Contains("pending = true;", reveal, StringComparison.Ordinal);
        Assert.Contains("requestAnimationFrame", reveal, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_error_is_found_in_document_order_and_its_control_is_focused()
    {
        var module = SharedMarkup.ReadShared("DtFailureBanner.razor.js");

        // One selector covering both shapes, so querySelectorAll returns them in document order.
        Assert.Contains(
            @"const errorSelector = "".dt-field-error, .dt-banner[role=\""alert\""]"";",
            module,
            StringComparison.Ordinal);

        // A field error hands focus to its control; a banner takes it itself.
        Assert.Contains(".form-control, .form-select, textarea", module, StringComparison.Ordinal);
        Assert.Contains(".input-group > .form-control", module, StringComparison.Ordinal);

        var now = FunctionBody(module, "revealIn");

        // A box with no input of its own (signature pad, photo picker) focuses the sentence instead.
        Assert.Contains("wrapper?.querySelector(controlSelector) ?? error) : error", now, StringComparison.Ordinal);
        Assert.Contains("focus({ preventScroll: true })", now, StringComparison.Ordinal);
        Assert.Contains("scrollIntoView(", now, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scroll_is_instant_under_reduced_motion()
    {
        var now = FunctionBody(SharedMarkup.ReadShared("DtFailureBanner.razor.js"), "revealIn");

        Assert.Contains(@"matchMedia(""(prefers-reduced-motion: reduce)"")", now, StringComparison.Ordinal);
        Assert.Matches(@"reduceMotion\s*\?\s*""auto""\s*:\s*""smooth""", now);
    }

    /// <summary>The body of a top-level function in the module, up to its closing brace.</summary>
    private static string FunctionBody(string module, string name)
    {
        var start = Regex.Match(
            module,
            $@"^(export )?function {Regex.Escape(name)}\(",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        Assert.True(start.Success, $"The module defines no {name} function.");

        var end = module.IndexOf("\n}", start.Index, StringComparison.Ordinal);

        Assert.True(end > start.Index, $"{name} is never closed.");

        return module[start.Index..end];
    }
}
