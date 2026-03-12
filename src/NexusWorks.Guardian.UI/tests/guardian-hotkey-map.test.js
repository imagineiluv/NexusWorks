const test = require("node:test");
const assert = require("node:assert/strict");

const hotkeyMap = require("../wwwroot/js/guardian-hotkey-map.js");

function createEvent(overrides = {}) {
    return {
        code: "",
        key: "",
        ctrlKey: false,
        metaKey: false,
        altKey: false,
        shiftKey: false,
        repeat: false,
        isComposing: false,
        target: null,
        ...overrides
    };
}

test("maps core execution shortcuts", () => {
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Enter", ctrlKey: true })), "run");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Enter", metaKey: true, shiftKey: true })), "rerun");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyH", altKey: true, shiftKey: true })), "refresh-history");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyS", altKey: true, shiftKey: true })), "load-sample");
});

test("maps history shortcuts", () => {
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "BracketLeft" })), "select-history-previous");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "BracketRight" })), "select-history-next");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyM" })), "load-selected-history");
});

test("maps filter and report shortcuts", () => {
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Digit1" })), "select-filter-all");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Digit5" })), "select-filter-ok");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyH" })), "open-report-html");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyE" })), "open-report-excel");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyD" })), "open-report-json");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyL" })), "open-report-log");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyU" })), "open-report-output");
});

test("maps navigation and row actions", () => {
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyJ" })), "select-next");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyK" })), "select-previous");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyN" })), "select-next");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyP" })), "select-previous");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyX" })), "toggle-selected-row");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyO" })), "open-selected-current");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyO", shiftKey: true })), "open-selected-patch");
});

test("maps bulk actions and help shortcuts", () => {
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyA", altKey: true, shiftKey: true })), "select-visible");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyR", altKey: true, shiftKey: true })), "select-review-set");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyC", altKey: true, shiftKey: true })), "clear-bulk-selection");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyO", altKey: true, shiftKey: true })), "open-bulk-current");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyP", altKey: true, shiftKey: true })), "open-bulk-patch");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Slash", key: "/" })), "focus-search");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Slash", key: "?" })), "toggle-shortcuts");
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Escape" })), "hide-shortcuts");
});

test("suppresses typing shortcuts when an editable target is focused", () => {
    const inputTarget = { tagName: "input", isContentEditable: false };

    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Slash", key: "/", target: inputTarget })), null);
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Slash", key: "?", target: inputTarget })), null);
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyJ", target: inputTarget })), null);
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "KeyO", target: inputTarget })), null);
    assert.equal(hotkeyMap.resolveAction(createEvent({ code: "Digit1", target: inputTarget })), null);
});
