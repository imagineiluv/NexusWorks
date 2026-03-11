(function () {
    const terminals = new Map();

    function resolveTerminalCtor() {
        // CDN UMD builds may expose Terminal globally.
        if (typeof Terminal !== 'undefined') return Terminal;
        // npm-bundled build exposes window.Terminal (UMD wrapper)
        if (window && window.Terminal) return window.Terminal;
        // Some builds expose under window.xterm
        if (window && window.xterm && window.xterm.Terminal) return window.xterm.Terminal;
        return null;
    }

    function resolveFitAddonCtor() {
        // CDN addon exposes FitAddon.FitAddon
        if (typeof FitAddon !== 'undefined' && FitAddon && FitAddon.FitAddon) return FitAddon.FitAddon;
        // Some bundles attach directly to window
        if (window && window.FitAddon && window.FitAddon.FitAddon) return window.FitAddon.FitAddon;
        return null;
    }

    function normalizeElement(element) {
        return element instanceof HTMLElement
            ? element
            : (element && element.id ? document.getElementById(element.id) : null);
    }

    function sanitizeBoolean(value, fallback) {
        return typeof value === 'boolean' ? value : !!fallback;
    }

    function buildInteractionOptions(options) {
        return {
            copyOnSelect: sanitizeBoolean(options && options.copyOnSelect, true),
            rightClickSelectsWord: sanitizeBoolean(options && options.rightClickSelectsWord, true),
            rightClickPaste: sanitizeBoolean(options && options.rightClickPaste, true),
            bracketedPasteMode: sanitizeBoolean(options && options.bracketedPasteMode, true)
        };
    }

    function buildHistoryOptions(options) {
        const limit = (options && typeof options.historyLimit === 'number') ? options.historyLimit : 200;
        return {
            historyEnabled: sanitizeBoolean(options && options.historyEnabled, true),
            historyLimit: Math.max(1, limit || 1),
            persistHistory: sanitizeBoolean(options && options.persistHistory, true),
            historyKey: (options && typeof options.historyKey === 'string' && options.historyKey.trim()) ? options.historyKey.trim() : null
        };
    }

    const HISTORY_PREFIX = 'terminal.history:';

    function resolveHistoryKey(state) {
        return (state && state.historyOptions && state.historyOptions.historyKey) || (state && state.id) || 'terminal';
    }

    function loadHistoryFromStorage(key, limit) {
        try {
            const raw = localStorage.getItem(HISTORY_PREFIX + key);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) return [];
            return parsed.filter((x) => typeof x === 'string').slice(-(limit || parsed.length));
        } catch {
            return [];
        }
    }

    function persistHistory(state) {
        if (!state || !state.historyOptions || !state.historyOptions.persistHistory) return;
        const key = resolveHistoryKey(state);
        try {
            localStorage.setItem(HISTORY_PREFIX + key, JSON.stringify(state.history || []));
        } catch {
            // ignore quota or storage errors
        }
    }

    function disposeInteraction(state) {
        if (state.selectionDisposable) {
            try { state.selectionDisposable.dispose(); } catch { }
            state.selectionDisposable = null;
        }

        if (state.contextMenuHandler) {
            try { state.el.removeEventListener('contextmenu', state.contextMenuHandler); } catch { }
            state.contextMenuHandler = null;
        }
    }

    function applyInteraction(state, nextOptions) {
        disposeInteraction(state);

        state.interactionOptions = Object.assign({}, state.interactionOptions || {}, nextOptions || {});

        const interaction = state.interactionOptions;

        try { state.term.options.copyOnSelection = !!interaction.copyOnSelect; } catch { }
        try { state.term.options.rightClickSelectsWord = !!interaction.rightClickSelectsWord; } catch { }
        try { state.term.options.bracketedPasteMode = !!interaction.bracketedPasteMode; } catch { }

        if (interaction.copyOnSelect) {
            state.selectionDisposable = state.term.onSelectionChange(async () => {
                try {
                    const selected = state.term.getSelection();
                    if (selected) {
                        await navigator.clipboard.writeText(selected);
                    }
                } catch {
                    // clipboard write can fail silently in restricted contexts
                }
            });
        }

        if (interaction.rightClickPaste) {
            state.contextMenuHandler = async (event) => {
                event.preventDefault();

                try {
                    const text = await navigator.clipboard.readText();
                    if (!text) return;

                    if (state.inputApi && state.inputApi.insertText) {
                        state.inputApi.insertText(text);
                        state.inputApi.renderInputLine();
                    } else {
                        state.term.write(text);
                    }
                } catch {
                    // clipboard read can fail on insecure origins or denied permissions
                }
            };

            try { state.el.addEventListener('contextmenu', state.contextMenuHandler); } catch { }
        }
    }

    function isNearBottom(el, thresholdPx) {
        const threshold = typeof thresholdPx === 'number' ? thresholdPx : 24;
        const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
        return distanceFromBottom <= threshold;
    }

    window.terminal = {
        create: function (element, options) {
            const el = normalizeElement(element);
            if (!el) return null;

            const TerminalCtor = resolveTerminalCtor();
            if (!TerminalCtor) {
                throw new Error('xterm.js is not loaded (Terminal is undefined). Ensure vendor/xterm/xterm.js is present and loaded before js/terminal.js.');
            }

            const FitAddonCtor = resolveFitAddonCtor();
            if (!FitAddonCtor) {
                throw new Error('xterm-addon-fit is not loaded (FitAddon is undefined). Ensure vendor/xterm/xterm-addon-fit.js is present and loaded before js/terminal.js.');
            }

            const id = (options && options.id) || (crypto && crypto.randomUUID ? crypto.randomUUID() : (Date.now() + '-' + Math.random()));

            const theme = (options && options.theme) || {};
            const interactionOptions = buildInteractionOptions(options);
            const historyOptions = buildHistoryOptions(options);

            const term = new TerminalCtor({
                convertEol: options && typeof options.convertEol === 'boolean' ? options.convertEol : true,
                cursorBlink: options && typeof options.cursorBlink === 'boolean' ? options.cursorBlink : true,
                cursorStyle: (options && options.cursorStyle) || 'block',
                fontFamily: (options && options.fontFamily) || '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
                fontSize: (options && options.fontSize) || 12,
                fontWeight: (options && options.fontWeight) || 'normal',
                fontWeightBold: (options && options.fontWeightBold) || 'bold',
                lineHeight: (options && options.lineHeight) || 1.0,
                letterSpacing: (options && options.letterSpacing) || 0,
                scrollback: (options && options.scrollback) || 5000,
                allowTransparency: options && typeof options.allowTransparency === 'boolean' ? options.allowTransparency : true,
                drawBoldTextInBrightColors: options && typeof options.drawBoldTextInBrightColors === 'boolean' ? options.drawBoldTextInBrightColors : true,
                copyOnSelection: interactionOptions.copyOnSelect,
                rightClickSelectsWord: interactionOptions.rightClickSelectsWord,
                bracketedPasteMode: interactionOptions.bracketedPasteMode,
                disableStdin: !!(options && options.disableStdin),
                theme: {
                    background: theme.background || '#000000',
                    foreground: theme.foreground || '#EDEDED',
                    cursor: theme.cursor || '#EDEDED',
                    cursorAccent: theme.cursorAccent || '#000000',
                    selectionBackground: theme.selectionBackground || 'rgba(255, 255, 255, 0.25)',
                    black: theme.black || '#2E3436',
                    red: theme.red || '#CC0000',
                    green: theme.green || '#4E9A06',
                    yellow: theme.yellow || '#C4A000',
                    blue: theme.blue || '#3465A4',
                    magenta: theme.magenta || '#75507B',
                    cyan: theme.cyan || '#06989A',
                    white: theme.white || '#D3D7CF',
                    brightBlack: theme.brightBlack || '#555753',
                    brightRed: theme.brightRed || '#EF2929',
                    brightGreen: theme.brightGreen || '#8AE234',
                    brightYellow: theme.brightYellow || '#FCE94F',
                    brightBlue: theme.brightBlue || '#729FCF',
                    brightMagenta: theme.brightMagenta || '#AD7FA8',
                    brightCyan: theme.brightCyan || '#34E2E2',
                    brightWhite: theme.brightWhite || '#EEEEEC'
                }
            });

            const fitAddon = new FitAddonCtor();
            term.loadAddon(fitAddon);

            el.innerHTML = '';
            term.open(el);
            fitAddon.fit();

            const state = {
                id,
                term,
                fitAddon,
                el,
                autoScroll: true,
                prompt: '',
                interactionOptions,
                historyOptions,
                history: [],
                historyIndex: 0
            };

            terminals.set(id, state);

            const resizeObserver = new ResizeObserver(() => {
                try { fitAddon.fit(); } catch { }
            });
            resizeObserver.observe(el);
            state.resizeObserver = resizeObserver;

            applyInteraction(state, interactionOptions);

            return id;
        },

        write: function (id, text) {
            const state = terminals.get(id);
            if (!state) return;

            const shouldStick = state.autoScroll && isNearBottom(state.el, 48);
            state.term.write(text);

            if (shouldStick) {
                requestAnimationFrame(() => state.term.scrollToBottom());
            }
        },

        setAutoScroll: function (id, enabled) {
            const state = terminals.get(id);
            if (!state) return;
            state.autoScroll = !!enabled;
        },

        setOptions: function (id, options) {
            const state = terminals.get(id);
            if (!state || !options) return;

            try {
                // Apply supported options (keep it defensive)
                if (typeof options.fontSize === 'number') state.term.options.fontSize = options.fontSize;
                if (typeof options.fontFamily === 'string') state.term.options.fontFamily = options.fontFamily;
                if (typeof options.cursorBlink === 'boolean') state.term.options.cursorBlink = options.cursorBlink;
                if (typeof options.cursorStyle === 'string') state.term.options.cursorStyle = options.cursorStyle;
                if (typeof options.scrollback === 'number') state.term.options.scrollback = options.scrollback;
                if (typeof options.allowTransparency === 'boolean') state.term.options.allowTransparency = options.allowTransparency;
                if (typeof options.drawBoldTextInBrightColors === 'boolean') state.term.options.drawBoldTextInBrightColors = options.drawBoldTextInBrightColors;
                if (typeof options.lineHeight === 'number') state.term.options.lineHeight = options.lineHeight;
                if (typeof options.letterSpacing === 'number') state.term.options.letterSpacing = options.letterSpacing;
                if (typeof options.bracketedPasteMode === 'boolean') state.term.options.bracketedPasteMode = options.bracketedPasteMode;

                if (options.theme) {
                    state.term.options.theme = Object.assign({}, state.term.options.theme || {}, options.theme);
                }

                const interactionUpdates = {};
                if (typeof options.copyOnSelect === 'boolean') interactionUpdates.copyOnSelect = options.copyOnSelect;
                if (typeof options.rightClickSelectsWord === 'boolean') interactionUpdates.rightClickSelectsWord = options.rightClickSelectsWord;
                if (typeof options.rightClickPaste === 'boolean') interactionUpdates.rightClickPaste = options.rightClickPaste;
                if (typeof options.bracketedPasteMode === 'boolean') interactionUpdates.bracketedPasteMode = options.bracketedPasteMode;

                if (Object.keys(interactionUpdates).length) {
                    applyInteraction(state, interactionUpdates);
                }

                const historyUpdates = {};
                if (typeof options.historyEnabled === 'boolean') historyUpdates.historyEnabled = options.historyEnabled;
                if (typeof options.historyLimit === 'number') historyUpdates.historyLimit = Math.max(1, options.historyLimit);
                if (typeof options.persistHistory === 'boolean') historyUpdates.persistHistory = options.persistHistory;
                if (typeof options.historyKey === 'string' && options.historyKey.trim()) historyUpdates.historyKey = options.historyKey.trim();

                if (Object.keys(historyUpdates).length) {
                    state.historyOptions = Object.assign({}, state.historyOptions || {}, historyUpdates);
                    if (state.historyOptions.historyEnabled) {
                        const key = resolveHistoryKey(state);
                        state.history = loadHistoryFromStorage(key, state.historyOptions.historyLimit);
                        state.historyIndex = state.history.length;
                    } else {
                        state.history = [];
                        state.historyIndex = 0;
                    }
                }

                try { state.fitAddon.fit(); } catch { }
            } catch { }
        },

        setTheme: function (id, theme) {
            const state = terminals.get(id);
            if (!state || !theme) return;
            try {
                state.term.options.theme = Object.assign({}, state.term.options.theme || {}, theme);
            } catch { }
        },

        applyGlobalOptions: function (options, autoScrollEnabled) {
            try {
                terminals.forEach((state) => {
                    try {
                        if (typeof autoScrollEnabled === 'boolean') {
                            state.autoScroll = autoScrollEnabled;
                        }

                        if (options) {
                            // reuse the same logic as setOptions
                            if (typeof options.fontSize === 'number') state.term.options.fontSize = options.fontSize;
                            if (typeof options.fontFamily === 'string') state.term.options.fontFamily = options.fontFamily;
                            if (typeof options.cursorBlink === 'boolean') state.term.options.cursorBlink = options.cursorBlink;
                            if (typeof options.cursorStyle === 'string') state.term.options.cursorStyle = options.cursorStyle;
                            if (typeof options.scrollback === 'number') state.term.options.scrollback = options.scrollback;
                            if (typeof options.allowTransparency === 'boolean') state.term.options.allowTransparency = options.allowTransparency;
                            if (typeof options.drawBoldTextInBrightColors === 'boolean') state.term.options.drawBoldTextInBrightColors = options.drawBoldTextInBrightColors;
                            if (typeof options.lineHeight === 'number') state.term.options.lineHeight = options.lineHeight;
                            if (typeof options.letterSpacing === 'number') state.term.options.letterSpacing = options.letterSpacing;
                            if (typeof options.bracketedPasteMode === 'boolean') state.term.options.bracketedPasteMode = options.bracketedPasteMode;

                            if (options.theme) {
                                state.term.options.theme = Object.assign({}, state.term.options.theme || {}, options.theme);
                            }

                            const interactionUpdates = {};
                            if (typeof options.copyOnSelect === 'boolean') interactionUpdates.copyOnSelect = options.copyOnSelect;
                            if (typeof options.rightClickSelectsWord === 'boolean') interactionUpdates.rightClickSelectsWord = options.rightClickSelectsWord;
                            if (typeof options.rightClickPaste === 'boolean') interactionUpdates.rightClickPaste = options.rightClickPaste;
                            if (typeof options.bracketedPasteMode === 'boolean') interactionUpdates.bracketedPasteMode = options.bracketedPasteMode;

                            if (Object.keys(interactionUpdates).length) {
                                applyInteraction(state, interactionUpdates);
                            }

                            const historyUpdates = {};
                            if (typeof options.historyEnabled === 'boolean') historyUpdates.historyEnabled = options.historyEnabled;
                            if (typeof options.historyLimit === 'number') historyUpdates.historyLimit = Math.max(1, options.historyLimit);
                            if (typeof options.persistHistory === 'boolean') historyUpdates.persistHistory = options.persistHistory;
                            if (typeof options.historyKey === 'string' && options.historyKey.trim()) historyUpdates.historyKey = options.historyKey.trim();

                            if (Object.keys(historyUpdates).length) {
                                state.historyOptions = Object.assign({}, state.historyOptions || {}, historyUpdates);
                                if (state.historyOptions.historyEnabled) {
                                    const key = resolveHistoryKey(state);
                                    state.history = loadHistoryFromStorage(key, state.historyOptions.historyLimit);
                                    state.historyIndex = state.history.length;
                                } else {
                                    state.history = [];
                                    state.historyIndex = 0;
                                }
                            }
                        }

                        try { state.fitAddon.fit(); } catch { }
                    } catch { }
                });
            } catch { }
        },

        attachInput: function (id, dotNetRef) {
            const state = terminals.get(id);
            if (!state) return;

            try { state.term.focus(); } catch { }

            // Avoid double-binding
            if (state._disposeInput) {
                try { state._disposeInput(); } catch { }
                state._disposeInput = null;
            }

            let buffer = '';
            state.historyOptions = state.historyOptions || buildHistoryOptions();
            state.history = (state.historyOptions.historyEnabled)
                ? (state.history && state.history.length ? state.history : loadHistoryFromStorage(resolveHistoryKey(state), state.historyOptions.historyLimit))
                : [];
            state.historyIndex = state.history ? state.history.length : 0;

            function renderPrompt() {
                if (state.prompt) {
                    state.term.write(state.prompt);
                }
            }

            function renderInputLine() {
                try { state.term.write('\u001b[2K\r'); } catch { }
                renderPrompt();
                if (buffer) {
                    state.term.write(buffer);
                }
            }

            function insertText(text) {
                if (!text) return;
                buffer += text;
                try { state.term.write(text); } catch { }
            }

            function pushHistory(line) {
                if (!state.historyOptions.historyEnabled || !line) return;

                const last = state.history[state.history.length - 1];
                if (last !== line) {
                    state.history.push(line);
                }

                if (state.historyOptions.historyLimit && state.history.length > state.historyOptions.historyLimit) {
                    state.history = state.history.slice(-state.historyOptions.historyLimit);
                }

                state.historyIndex = state.history.length;
                persistHistory(state);
            }

            function navigateHistory(delta) {
                if (!state.historyOptions.historyEnabled || !state.history.length) return;
                const nextIndex = Math.min(Math.max(state.historyIndex + delta, 0), state.history.length - 1);
                state.historyIndex = nextIndex;
                buffer = state.history[state.historyIndex] || '';
                renderInputLine();
            }

            // Draw initial prompt in the input line.
            renderPrompt();

            state.inputApi = {
                insertText,
                renderInputLine
            };

            const disposable = state.term.onData(async (data) => {
                if (data === '\u001b[A') {
                    navigateHistory(-1);
                    return;
                }

                if (data === '\u001b[B') {
                    if (!state.historyOptions.historyEnabled || state.history.length === 0) return;
                    if (state.historyIndex >= state.history.length - 1) {
                        state.historyIndex = state.history.length;
                        buffer = '';
                        renderInputLine();
                        return;
                    }

                    navigateHistory(1);
                    return;
                }

                // Enter
                if (data === '\r') {
                    state.term.write('\r\n');
                    const line = buffer;
                    buffer = '';
                    pushHistory(line);
                    try {
                        await dotNetRef.invokeMethodAsync('OnTerminalEnter', line);
                    } catch {
                        // ignore interop errors
                    }

                    // After sending the command, show a fresh prompt.
                    renderPrompt();
                    return;
                }

                // Backspace (DEL)
                if (data === '\u007F') {
                    if (buffer.length > 0) {
                        buffer = buffer.slice(0, -1);
                        // Move left, clear char, move left
                        state.term.write('\b \b');
                    }
                    return;
                }

                // Ctrl+C: forward as empty line? (or send interrupt sequence)
                if (data === '\u0003') {
                    state.term.write('^C\r\n');
                    buffer = '';
                    state.historyIndex = state.history ? state.history.length : 0;
                    try {
                        await dotNetRef.invokeMethodAsync('OnTerminalEnter', '');
                    } catch { }

                    renderPrompt();
                    return;
                }

                // Printable input (may include multi-byte sequences; keep simple)
                buffer += data;
                state.term.write(data);
            });

            state._disposeInput = () => {
                try { disposable.dispose(); } catch { }
                state.inputApi = null;
            };
        },

        setPrompt: function (id, prompt) {
            const state = terminals.get(id);
            if (!state) return;
            state.prompt = (prompt == null) ? '' : String(prompt);
        },

        focus: function (id) {
            const state = terminals.get(id);
            if (!state) return;
            try { state.term.focus(); } catch { }
        },

        dispose: function (id) {
            const state = terminals.get(id);
            if (!state) return;

            try { state._disposeInput && state._disposeInput(); } catch { }
            try { disposeInteraction(state); } catch { }
            try { state.resizeObserver && state.resizeObserver.disconnect(); } catch { }
            try { state.term && state.term.dispose(); } catch { }
            terminals.delete(id);
        }
    };
})();
