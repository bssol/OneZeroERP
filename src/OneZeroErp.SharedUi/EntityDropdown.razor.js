const outsideClickHandlers = new WeakMap();

export function registerOutsideClick(element, dotNetReference) {
    unregisterOutsideClick(element);

    const outsideHandler = event => {
        if (!element.contains(event.target)) {
            dotNetReference.invokeMethodAsync("CloseFromOutsideAsync");
        }
    };
    const positionHandler = () => positionPanel(element);

    outsideClickHandlers.set(element, { outsideHandler, positionHandler });
    document.addEventListener("pointerdown", outsideHandler, true);
    document.addEventListener("scroll", positionHandler, true);
    window.addEventListener("resize", positionHandler);
    positionPanel(element);
    element.querySelector(".entity-dropdown__search")?.focus();
}

export function unregisterOutsideClick(element) {
    const handlers = outsideClickHandlers.get(element);
    if (!handlers) {
        return;
    }

    document.removeEventListener("pointerdown", handlers.outsideHandler, true);
    document.removeEventListener("scroll", handlers.positionHandler, true);
    window.removeEventListener("resize", handlers.positionHandler);
    outsideClickHandlers.delete(element);
}

function positionPanel(element) {
    const trigger = element.querySelector(".entity-dropdown__trigger");
    const panel = element.querySelector(".entity-dropdown__panel");
    if (!trigger || !panel) {
        return;
    }

    const triggerRect = trigger.getBoundingClientRect();
    const viewportPadding = 10;
    const panelGap = 6;
    const panelWidth = Math.min(Math.max(triggerRect.width, 280), window.innerWidth - viewportPadding * 2);
    const left = Math.min(
        Math.max(triggerRect.left, viewportPadding),
        window.innerWidth - panelWidth - viewportPadding);
    const panelHeight = Math.min(panel.scrollHeight, 330);
    const roomBelow = window.innerHeight - triggerRect.bottom - viewportPadding;
    const openAbove = roomBelow < panelHeight + panelGap && triggerRect.top > roomBelow;
    const top = openAbove
        ? Math.max(viewportPadding, triggerRect.top - panelHeight - panelGap)
        : Math.min(triggerRect.bottom + panelGap, window.innerHeight - panelHeight - viewportPadding);

    panel.style.width = `${panelWidth}px`;
    panel.style.left = `${left}px`;
    panel.style.top = `${top}px`;
}
