// NFR-4: a refusal is shown where the user is looking.
//
// A refused save in a long dialog used to report itself at the top of the dialog body while the
// user was scrolled down at Save, so nothing visibly happened. After a refusal arrives, the first
// error on the screen - a field error under its box, or a banner - is scrolled into view and
// focused, so both the eye and a screen reader land on it.
//
// Called from DtFailureBanner's OnAfterRenderAsync, which never runs under static rendering: the
// sign-in and register pages reload at the top after their POST and need no script at all.

// The two shapes a refusal takes on a screen: the sentence DtFieldError puts under its input, and
// the danger banner DtFailureBanner draws for what no input claimed. In one selector so the browser
// returns them in document order and the first one is genuinely the first.
const errorSelector = ".dt-field-error, .dt-banner[role=\"alert\"]";

// The control a field error is about: the input, select or textarea beside it in the shared
// wrapper, or the input inside an input group there.
const controlSelector = ":scope > :is(.form-control, .form-select, textarea), :scope > .input-group > .form-control";

// Every banner bound to one failure list asks - the page's and each dialog's. Only the banner in
// the active region reveals (see scopeOf), and requests landing before the next frame are
// collapsed into one, so two banners never fight over the scroll position or the focus.
let pending = false;

/**
 * Reveals the first refusal on the screen once the render that produced it has painted.
 * @param {HTMLElement} anchor The banner's always-rendered anchor; the region is read from it.
 */
export function reveal(anchor) {
    // Scoped before the de-duplication, so a banner that stands down cannot swallow the request
    // of the one that owns the reveal.
    const scope = scopeOf(anchor);

    if (!scope || pending) {
        return;
    }

    pending = true;

    requestAnimationFrame(() => {
        pending = false;
        revealIn(scope);
    });
}

/**
 * The region this banner reveals in: the open dialog it sits in, otherwise its page's main. Null
 * when a modal dialog is open and this banner is not inside it - the page's banner and the closed
 * dialogs' banners stand down, so only the banner in the active region reveals.
 * @param {HTMLElement} anchor The banner's anchor.
 * @returns {Element|null} The scope to search, or null when another banner owns the reveal.
 */
function scopeOf(anchor) {
    if (!anchor || !anchor.isConnected) {
        return null;
    }

    const dialog = anchor.closest("dialog[open]");

    if (dialog) {
        return dialog;
    }

    // Every DtDialog opens with showModal(), so an open dialog is a modal one - and one the user is
    // in rather than this banner's page.
    if (document.querySelector("dialog[open]")) {
        return null;
    }

    return anchor.closest("main");
}

/**
 * The first visible refusal in the scope, in document order. Errors inside a dialog that is not
 * open are skipped: the dialogs' markup is always rendered, and their boxes are bound to the same
 * failure list as the page.
 * @param {Element} scope The region to search.
 * @returns {HTMLElement|null} The error to reveal.
 */
function firstError(scope) {
    for (const element of scope.querySelectorAll(errorSelector)) {
        const dialog = element.closest("dialog");

        if (dialog && !dialog.open) {
            continue;
        }

        if (element.getClientRects().length === 0) {
            continue;
        }

        return element;
    }

    return null;
}

/**
 * Scrolls the first refusal into view and moves focus to it: a field error's control (or the
 * error itself when the box has none), or the banner itself (it carries tabindex="-1" for this).
 * @param {Element} scope The region to search.
 */
function revealIn(scope) {
    const error = firstError(scope);

    if (!error) {
        return;
    }

    const isFieldError = error.classList.contains("dt-field-error");
    const wrapper = isFieldError ? error.parentElement : null;
    const target = isFieldError ? wrapper : error;
    // A box with no input of its own - the signature pad, the photo picker - focuses the sentence
    // itself, which carries tabindex="-1" for this.
    const focusTarget = isFieldError ? (wrapper?.querySelector(controlSelector) ?? error) : error;

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const behavior = reduceMotion ? "auto" : "smooth";

    // Focus first without letting the browser jump, then scroll once in the chosen manner: a
    // focus() that scrolled on its own would make every reveal instant, reduced motion or not.
    focusTarget.focus({ preventScroll: true });
    (target ?? error).scrollIntoView({ behavior, block: "center" });
}
