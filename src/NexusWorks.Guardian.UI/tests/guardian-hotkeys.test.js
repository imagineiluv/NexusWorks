const test = require("node:test");
const assert = require("node:assert/strict");

const hotkeysModulePath = require.resolve("../wwwroot/js/guardian-hotkeys.js");

function loadHotkeysHarness({ resolveAction = () => null } = {}) {
    const listeners = new Map();
    const selectedRow = {
        scrollIntoViewCalls: [],
        scrollIntoView(options) {
            this.scrollIntoViewCalls.push(options);
        }
    };
    const selectedHistory = {
        scrollIntoViewCalls: [],
        scrollIntoView(options) {
            this.scrollIntoViewCalls.push(options);
        }
    };

    class FakeInput {
        constructor() {
            this.focusCalls = 0;
            this.selectCalls = 0;
        }

        focus() {
            this.focusCalls += 1;
        }

        select() {
            this.selectCalls += 1;
        }
    }

    const searchInput = new FakeInput();

    const document = {
        addEventListener(type, handler) {
            listeners.set(type, handler);
        },
        removeEventListener(type, handler) {
            if (listeners.get(type) === handler) {
                listeners.delete(type);
            }
        },
        querySelector(selector) {
            if (selector === "tr[data-guardian-selected='true']") {
                return selectedRow;
            }

            if (selector === "li[data-guardian-history-selected='true']") {
                return selectedHistory;
            }

            return null;
        },
        getElementById(id) {
            return id === "guardian-result-search" ? searchInput : null;
        }
    };

    globalThis.window = globalThis;
    globalThis.document = document;
    globalThis.HTMLInputElement = FakeInput;
    globalThis.guardianHotkeyMap = { resolveAction };

    delete require.cache[hotkeysModulePath];
    require(hotkeysModulePath);

    return {
        api: globalThis.guardianHotkeys,
        listeners,
        searchInput,
        selectedRow,
        selectedHistory,
        cleanup() {
            delete require.cache[hotkeysModulePath];
            delete globalThis.guardianHotkeys;
            delete globalThis.guardianHotkeyMap;
            delete globalThis.HTMLInputElement;
            delete globalThis.document;
            delete globalThis.window;
        }
    };
}

test("register dispatches resolved hotkey actions through the dotnet callback", async (t) => {
    const harness = loadHotkeysHarness({
        resolveAction: () => "focus-search"
    });
    t.after(() => harness.cleanup());

    const calls = [];
    harness.api.register({
        invokeMethodAsync(methodName, action) {
            calls.push({ methodName, action });
            return Promise.resolve();
        }
    });

    const handler = harness.listeners.get("keydown");
    assert.equal(typeof handler, "function");

    let prevented = 0;
    let stopped = 0;

    await handler({
        code: "Slash",
        key: "/",
        preventDefault() {
            prevented += 1;
        },
        stopPropagation() {
            stopped += 1;
        }
    });

    assert.deepEqual(calls, [{ methodName: "HandleHotkeyAsync", action: "focus-search" }]);
    assert.equal(prevented, 1);
    assert.equal(stopped, 1);
});

test("register ignores events when no hotkey action is resolved", async (t) => {
    const harness = loadHotkeysHarness();
    t.after(() => harness.cleanup());

    const calls = [];
    harness.api.register({
        invokeMethodAsync(methodName, action) {
            calls.push({ methodName, action });
            return Promise.resolve();
        }
    });

    const handler = harness.listeners.get("keydown");
    assert.equal(typeof handler, "function");

    let prevented = 0;

    await handler({
        code: "KeyZ",
        key: "z",
        preventDefault() {
            prevented += 1;
        },
        stopPropagation() {
        }
    });

    assert.deepEqual(calls, []);
    assert.equal(prevented, 0);
});

test("unregister removes the active keydown listener", (t) => {
    const harness = loadHotkeysHarness({
        resolveAction: () => "hide-shortcuts"
    });
    t.after(() => harness.cleanup());

    harness.api.register({
        invokeMethodAsync() {
            return Promise.resolve();
        }
    });

    assert.equal(harness.listeners.has("keydown"), true);
    harness.api.unregister();
    assert.equal(harness.listeners.has("keydown"), false);
});

test("focus and scroll helpers target the expected elements", (t) => {
    const harness = loadHotkeysHarness();
    t.after(() => harness.cleanup());

    harness.api.focusResultSearch();
    harness.api.scrollSelectedRow();
    harness.api.scrollSelectedHistory();

    assert.equal(harness.searchInput.focusCalls, 1);
    assert.equal(harness.searchInput.selectCalls, 1);
    assert.deepEqual(harness.selectedRow.scrollIntoViewCalls, [{
        behavior: "smooth",
        block: "nearest",
        inline: "nearest"
    }]);
    assert.deepEqual(harness.selectedHistory.scrollIntoViewCalls, [{
        behavior: "smooth",
        block: "nearest",
        inline: "nearest"
    }]);
});
