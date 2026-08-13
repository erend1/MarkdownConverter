// dom-bridge.js — minimal DOM primitives for the Blazor side to call.
// No business logic lives here. Everything stateful or branching lives in
// C# (`IEditorBridge` + presenters). Comments call out *why* each primitive
// stays in JS — usually a browser-only API with no WASM equivalent.

window.domBridge = {
    // Read the current selection range of a textarea / input.
    getSelection: function (selector) {
        var el = document.querySelector(selector);
        if (!el) return { start: 0, end: 0 };
        return { start: el.selectionStart || 0, end: el.selectionEnd || 0 };
    },

    // Write a selection range. Setting selectionStart/End on a textarea is a
    // direct DOM property assignment with no C# equivalent in WASM.
    setSelection: function (selector, start, end) {
        var el = document.querySelector(selector);
        if (!el) return;
        el.selectionStart = start;
        el.selectionEnd = end;
    },

    // Plain getter — C# already has `value=` binding but during find we
    // sometimes need a fresh snapshot uncached by Blazor's render diff.
    getValue: function (selector) {
        var el = document.querySelector(selector);
        return el ? el.value : '';
    },

    // Scroll the textarea so the current selection's first line is centred.
    // Computes line × line-height from getComputedStyle — same math as the
    // old _scrollToSelection helper. No business logic, just geometry on
    // values only the DOM knows.
    scrollSelectionIntoView: function (selector) {
        var el = document.querySelector(selector);
        if (!el) return;
        var idx = el.selectionStart;
        if (typeof idx !== 'number') return;
        var before = el.value.substring(0, idx);
        var lineNum = (before.match(/\n/g) || []).length;
        var cs = window.getComputedStyle(el);
        var lineHeight = parseFloat(cs.lineHeight);
        if (!lineHeight || isNaN(lineHeight)) {
            lineHeight = (parseFloat(cs.fontSize) || 14) * 1.4;
        }
        var paddingTop = parseFloat(cs.paddingTop) || 0;
        el.scrollTop = Math.max(0,
            paddingTop + lineNum * lineHeight - el.clientHeight / 2);
    },

    getScrollRatio: function (selector) {
        var el = document.querySelector(selector);
        if (!el) return 0;
        var max = el.scrollHeight - el.clientHeight;
        if (max <= 0) return 0;
        return Math.max(0, Math.min(1, el.scrollTop / max));
    },

    setScrollRatio: function (selector, ratio) {
        var el = document.querySelector(selector);
        if (!el) return;
        var normalized = Math.max(0, Math.min(1, ratio || 0));
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                el.scrollTop = normalized * Math.max(0, el.scrollHeight - el.clientHeight);
                el.dispatchEvent(new Event('scroll'));
            });
        });
    },

    // Insert text at the cursor via execCommand. This is the ONLY way to
    // edit a <textarea> while keeping the browser's native undo stack
    // (Ctrl+Z) intact — assigning `el.value = ...` wipes the undo history.
    // We preserve the user's previous focus so the find input keeps focus
    // when this is invoked from a find/replace context.
    insertTextAtCursor: function (selector, text) {
        var el = document.querySelector(selector);
        if (!el) return;
        var prev = document.activeElement;
        el.focus();
        document.execCommand('insertText', false, text);
        if (prev && prev !== el && typeof prev.focus === 'function') {
            prev.focus();
        }
        el.dispatchEvent(new Event('input', { bubbles: true }));
    },

    // Plain focus — single line. Kept as a primitive because Blazor cannot
    // call `el.focus()` directly from C#.
    focus: function (selector) {
        var el = document.querySelector(selector);
        if (el) el.focus();
    },

    // Captures the chords that need preventDefault() (otherwise Tab moves
    // focus / Ctrl+K opens the browser address bar) and forwards them to
    // C# for handling. Pure DOM glue — no logic about WHAT to do with the
    // chord lives here, only "is this a chord we own?".
    attachEditorKeyShim: function (selector, dotnetRef) {
        var el = document.querySelector(selector);
        if (!el) return;
        if (el._mdKeyShim) el.removeEventListener('keydown', el._mdKeyShim);
        var handler = function (e) {
            var chord = window.domBridge._classifyChord(e);
            if (chord === null) return;
            e.preventDefault();
            var selStart = el.selectionStart || 0;
            var selEnd = el.selectionEnd || 0;
            var selected = el.value.substring(selStart, selEnd);
            dotnetRef.invokeMethodAsync('OnEditorChord', chord, selected, selStart, selEnd);
        };
        el._mdKeyShim = handler;
        el.addEventListener('keydown', handler);
    },

    _classifyChord: function (e) {
        var mod = e.ctrlKey || e.metaKey;
        if (e.key === 'Tab' && !mod && !e.shiftKey && !e.altKey) return 'Tab';
        if (!mod) return null;
        if (e.key === 'b') return 'CtrlB';
        if (e.key === 'i') return 'CtrlI';
        if (e.key === 'k') return 'CtrlK';
        if (e.key === '`') return 'CtrlBacktick';
        return null;
    },

    // Document-level drag-and-drop bridge. FileReader is browser-only, so
    // the read happens in JS; the resulting (name, text) pair is forwarded
    // to a [JSInvokable] on the C# side. Idempotent — re-attaching removes
    // the previous pair.
    attachDragDrop: function (dotnetRef) {
        if (window._mdDragOver) {
            document.removeEventListener('dragover', window._mdDragOver);
            document.removeEventListener('drop', window._mdDrop);
        }
        var over = function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        };
        var drop = function (e) {
            e.preventDefault();
            var files = e.dataTransfer.files;
            if (!files || files.length === 0) return;
            var file = files[0];
            var ext = file.name.split('.').pop().toLowerCase();
            var method = null;
            if (ext === 'md' || ext === 'markdown' || ext === 'txt') method = 'OnFileDrop';
            else if (ext === 'bib') method = 'OnBibDrop';
            if (!method) return;
            var reader = new FileReader();
            reader.onload = function (ev) {
                dotnetRef.invokeMethodAsync(method, file.name, ev.target.result);
            };
            reader.readAsText(file);
        };
        window._mdDragOver = over;
        window._mdDrop = drop;
        document.addEventListener('dragover', over);
        document.addEventListener('drop', drop);
    },

    // Data-driven global shortcuts. C# supplies an array of
    // { key, shift, selector } objects; when a Ctrl/Cmd chord matches one,
    // JS clicks the element. No routing logic in JS — the mapping is
    // configuration, passed in. Bound exactly once via the sentinel.
    attachGlobalShortcuts: function (bindings) {
        if (window._mdGlobalShortcutsAttached) return;
        window._mdGlobalShortcutsAttached = true;
        document.addEventListener('keydown', function (e) {
            var mod = e.ctrlKey || e.metaKey;
            if (!mod) return;
            for (var i = 0; i < bindings.length; i++) {
                var b = bindings[i];
                if (b.key === e.key && (!!b.shift) === e.shiftKey) {
                    e.preventDefault();
                    var btn = document.querySelector(b.selector);
                    if (btn) btn.click();
                    return;
                }
            }
        });
    },

    // Ctrl+F / Ctrl+H bridge: opens the editor's find bar. Forwarded to a
    // [JSInvokable] (ShowFindBar) on the MarkdownEditor component. The
    // retained handler is replaced on attach and explicitly removed when the
    // component is disposed so it cannot retain a dead .NET reference.
    attachFindShortcut: function (dotnetRef, ownerId) {
        window.domBridge.detachFindShortcut();
        var handler = function (e) {
            if (!e.ctrlKey && !e.metaKey) return;
            if (e.key === 'f') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('ShowFindBar', false);
            } else if (e.key === 'h') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('ShowFindBar', true);
            }
        };
        window._mdFindShortcutOwnerId = ownerId;
        window._mdFindShortcutHandler = handler;
        document.addEventListener('keydown', handler);
    },

    detachFindShortcut: function (ownerId) {
        if (!window._mdFindShortcutHandler) return;
        if (ownerId && window._mdFindShortcutOwnerId !== ownerId) return;
        document.removeEventListener('keydown', window._mdFindShortcutHandler);
        window._mdFindShortcutHandler = null;
        window._mdFindShortcutOwnerId = null;
    },

    // Called once per drag, from the splitter's @onmousedown Blazor handler.
    // Forwards each subsequent mousemove (clientX + container geometry) up
    // to C# for the percentage math, then self-cleans on mouseup. No
    // resize logic lives here — only event glue.
    startSplitterDrag: function (dotnetRef) {
        var container = document.querySelector('.editor-layout');
        if (!container) return;
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        var move = function (e) {
            var rect = container.getBoundingClientRect();
            dotnetRef.invokeMethodAsync('OnSplitterDrag',
                e.clientX, rect.left, rect.width);
        };
        var end = function () {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', end);
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            dotnetRef.invokeMethodAsync('OnSplitterEnd');
        };
        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', end);
    }
};
