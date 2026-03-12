(function (root, factory) {
    const api = factory();

    if (typeof module === "object" && module.exports) {
        module.exports = api;
    }

    root.guardianHotkeyMap = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
    function isEditableTarget(target) {
        if (!target || typeof target !== "object") {
            return false;
        }

        const tagName = typeof target.tagName === "string"
            ? target.tagName.toUpperCase()
            : "";

        return target.isContentEditable === true
            || tagName === "INPUT"
            || tagName === "TEXTAREA"
            || tagName === "SELECT";
    }

    function resolveAction(event) {
        if (!event || event.isComposing || event.repeat) {
            return null;
        }

        const ctrlOrMeta = event.ctrlKey || event.metaKey;
        const code = event.code;
        const key = event.key;

        if (!ctrlOrMeta && !event.altKey && !event.metaKey && code === "Slash" && key === "?") {
            if (isEditableTarget(event.target)) {
                return null;
            }

            return "toggle-shortcuts";
        }

        if (!ctrlOrMeta && !event.altKey && !event.shiftKey && !event.metaKey && code === "Slash") {
            if (isEditableTarget(event.target)) {
                return null;
            }

            return "focus-search";
        }

        if (code === "Escape") {
            return "hide-shortcuts";
        }

        if (ctrlOrMeta && !event.altKey && !event.shiftKey && code === "Enter") {
            return "run";
        }

        if (ctrlOrMeta && event.shiftKey && !event.altKey && code === "Enter") {
            return "rerun";
        }

        if (event.altKey && event.shiftKey && !event.ctrlKey && !event.metaKey && code === "KeyH") {
            return "refresh-history";
        }

        if (event.altKey && event.shiftKey && !event.ctrlKey && !event.metaKey && code === "KeyS") {
            return "load-sample";
        }

        if (!ctrlOrMeta && !event.altKey && !event.shiftKey && !event.metaKey) {
            if (isEditableTarget(event.target)) {
                return null;
            }

            if (code === "BracketLeft") {
                return "select-history-previous";
            }

            if (code === "BracketRight") {
                return "select-history-next";
            }

            if (code === "KeyM") {
                return "load-selected-history";
            }
        }

        if (event.altKey && event.shiftKey && !event.ctrlKey && !event.metaKey) {
            switch (code) {
                case "KeyA":
                    return "select-visible";
                case "KeyR":
                    return "select-review-set";
                case "KeyC":
                    return "clear-bulk-selection";
                case "KeyO":
                    return "open-bulk-current";
                case "KeyP":
                    return "open-bulk-patch";
            }
        }

        if (!ctrlOrMeta && !event.altKey && !event.shiftKey && !event.metaKey) {
            if (isEditableTarget(event.target)) {
                return null;
            }

            switch (code) {
                case "Digit1":
                    return "select-filter-all";
                case "Digit2":
                    return "select-filter-changed";
                case "Digit3":
                    return "select-filter-missing-required";
                case "Digit4":
                    return "select-filter-error";
                case "Digit5":
                    return "select-filter-ok";
                case "KeyH":
                    return "open-report-html";
                case "KeyE":
                    return "open-report-excel";
                case "KeyD":
                    return "open-report-json";
                case "KeyL":
                    return "open-report-log";
                case "KeyU":
                    return "open-report-output";
                case "KeyX":
                    return "toggle-selected-row";
            }
        }

        if (!ctrlOrMeta && !event.altKey && !event.metaKey && code === "KeyO") {
            if (isEditableTarget(event.target)) {
                return null;
            }

            return event.shiftKey ? "open-selected-patch" : "open-selected-current";
        }

        if (!ctrlOrMeta && !event.altKey && !event.shiftKey && !event.metaKey) {
            if (isEditableTarget(event.target)) {
                return null;
            }

            if (code === "KeyJ" || code === "KeyN") {
                return "select-next";
            }

            if (code === "KeyK" || code === "KeyP") {
                return "select-previous";
            }
        }

        return null;
    }

    return {
        isEditableTarget,
        resolveAction
    };
});
