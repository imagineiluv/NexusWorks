(function () {
    let handler = null;
    let dotNetRef = null;

    function resolveAction(event) {
        if (!globalThis.guardianHotkeyMap || typeof globalThis.guardianHotkeyMap.resolveAction !== "function") {
            return null;
        }

        return globalThis.guardianHotkeyMap.resolveAction(event);
    }

    window.guardianHotkeys = {
        register(reference) {
            dotNetRef = reference;

            if (handler) {
                document.removeEventListener("keydown", handler, true);
            }

            handler = async function (event) {
                const action = resolveAction(event);
                if (!action || !dotNetRef) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();

                try {
                    await dotNetRef.invokeMethodAsync("HandleHotkeyAsync", action);
                } catch (error) {
                    console.error("Guardian hotkey dispatch failed.", error);
                }
            };

            document.addEventListener("keydown", handler, true);
        },

        unregister() {
            if (handler) {
                document.removeEventListener("keydown", handler, true);
                handler = null;
            }

            dotNetRef = null;
        },

        scrollSelectedRow() {
            const row = document.querySelector("tr[data-guardian-selected='true']");
            if (!row) {
                return;
            }

            row.scrollIntoView({
                behavior: "smooth",
                block: "nearest",
                inline: "nearest"
            });
        },

        scrollSelectedHistory() {
            const item = document.querySelector("li[data-guardian-history-selected='true']");
            if (!item) {
                return;
            }

            item.scrollIntoView({
                behavior: "smooth",
                block: "nearest",
                inline: "nearest"
            });
        },

        focusResultSearch() {
            const input = document.getElementById("guardian-result-search");
            if (!input || !(input instanceof HTMLInputElement)) {
                return;
            }

            input.focus();
            input.select();
        }
    };
})();
