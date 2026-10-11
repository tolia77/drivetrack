// NFR-19: the dialog's behaviour, confined to the component's own module.
//
// showModal() supplies focus trapping, Escape dismissal and focus restoration; the only thing the
// platform does not give us is backdrop dismissal, and that is the guard below. Everything
// else here exists so that EVERY dismissal - Escape, the backdrop, the close action - arrives in
// .NET the same way: through the element's own `close` event, invoked back onto the component.
//
// Relying on Blazor's @onclose handler instead would put the same guarantee on the event-handler
// registry; a DotNetObjectReference call is deterministic and reads the same in a test.

const listeners = new WeakMap();

/**
 * Opens the dialog modally and wires its dismissal paths.
 * @param {HTMLDialogElement} element The dialog element.
 * @param {object} dotNetRef A reference to the owning DtDialog component.
 */
export function open(element, dotNetRef) {
    if (!element || element.open) {
        return;
    }

    // Re-registering on a second open would notify .NET twice for one dismissal, so the handlers
    // are attached once and remembered against the element.
    if (!listeners.has(element)) {
        const onClose = () => dotNetRef.invokeMethodAsync("NotifyClosedAsync");

        // The backdrop is not a child element: a press on it reports the dialog itself as the
        // target, while a press anywhere inside the panel reports a descendant. That identity
        // check is the guard - without it, clicking the panel would close the dialog.
        //
        // It is checked where the press BEGAN as well as where it ended. A click whose pointerdown
        // lands in the panel and whose pointerup lands over the backdrop - selecting text in a field
        // and overshooting the edge - is dispatched to the dialog itself, and judged on the click
        // alone it would throw away a half-filled form.
        //
        // And where it was released. A press that starts on the backdrop and ends inside the panel
        // also dispatches its click to the dialog - the nearest common ancestor of the two targets -
        // so the release is recorded too, and the dialog closes only when the press, the release and
        // the click are all on the backdrop.
        let pressedOnBackdrop = false;
        let releasedOnBackdrop = false;

        const onPointerDown = (event) => {
            pressedOnBackdrop = event.target === element;
        };

        const onPointerUp = (event) => {
            releasedOnBackdrop = event.target === element;
        };

        const onClick = (event) => {
            const backdropPress = pressedOnBackdrop && releasedOnBackdrop;

            pressedOnBackdrop = false;
            releasedOnBackdrop = false;

            if (backdropPress && event.target === element) {
                element.close();
            }
        };

        element.addEventListener("close", onClose);
        element.addEventListener("pointerdown", onPointerDown);
        element.addEventListener("pointerup", onPointerUp);
        element.addEventListener("click", onClick);
        listeners.set(element, { onClose, onPointerDown, onPointerUp, onClick });
    }

    element.showModal();
}

/**
 * Closes the dialog. The `close` event this raises is what notifies .NET.
 * @param {HTMLDialogElement} element The dialog element.
 */
export function close(element) {
    if (element && element.open) {
        element.close();
    }
}
