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

    // Select and reveal the exact C#-owned match range. A textarea does not
    // expose caret geometry, so map the explicit character offsets through a
    // short-lived, style-identical plain-text mirror with a DOM Range. The
    // measurement node is removed synchronously and never depends on which
    // asynchronously painted <mark> is current. The physical-line estimate
    // remains a safe fallback if the editor has no mirror template.
    revealSelection: function (selector, start, end) {
        var el = document.querySelector(selector);
        if (!el) return;

        var textLength = el.value.length;
        var safeStart = Math.max(0, Math.min(textLength, Number(start) || 0));
        var safeEnd = Math.max(safeStart, Math.min(textLength, Number(end) || 0));
        el.selectionStart = safeStart;
        el.selectionEnd = safeEnd;

        var wrap = el.closest('.editor-mirror-wrap');
        var mirrorTemplate = wrap && wrap.querySelector('.editor-mirror');
        if (mirrorTemplate) {
            var measurement = mirrorTemplate.cloneNode(false);
            measurement.setAttribute('aria-hidden', 'true');
            measurement.style.visibility = 'hidden';
            measurement.style.pointerEvents = 'none';
            measurement.style.zIndex = '-1';
            measurement.textContent = el.value;
            wrap.appendChild(measurement);
            try {
                var startPosition = findTextPosition(measurement, safeStart);
                var endPosition = findTextPosition(measurement, safeEnd);
                if (startPosition && endPosition) {
                    var range = document.createRange();
                    range.setStart(startPosition.node, startPosition.offset);
                    range.setEnd(endPosition.node, endPosition.offset);
                    var rangeRects = range.getClientRects();
                    if (rangeRects.length > 0) {
                        var rangeRect = rangeRects[0];
                        var measurementRect = measurement.getBoundingClientRect();
                        var rangeTop = rangeRect.top - measurementRect.top;
                        var exactTop = rangeTop + rangeRect.height / 2 - el.clientHeight / 2;
                        var maxScroll = Math.max(0, el.scrollHeight - el.clientHeight);
                        el.scrollTop = Math.max(0, Math.min(maxScroll, exactTop));
                        return;
                    }
                }
            } finally {
                measurement.remove();
            }
        }

        var before = el.value.substring(0, safeStart);
        var lineNum = (before.match(/\n/g) || []).length;
        var cs = window.getComputedStyle(el);
        var lineHeight = parseFloat(cs.lineHeight);
        if (!lineHeight || isNaN(lineHeight)) {
            lineHeight = (parseFloat(cs.fontSize) || 14) * 1.4;
        }
        var paddingTop = parseFloat(cs.paddingTop) || 0;
        el.scrollTop = Math.max(0,
            paddingTop + lineNum * lineHeight - el.clientHeight / 2);

        function findTextPosition(root, offset) {
            var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
            var remaining = offset;
            var node;
            while ((node = walker.nextNode())) {
                if (remaining <= node.data.length) {
                    return { node: node, offset: remaining };
                }
                remaining -= node.data.length;
            }
            return null;
        }
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
        window.domBridge.detachDragDrop();
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

    detachDragDrop: function () {
        if (window._mdDragOver)
            document.removeEventListener('dragover', window._mdDragOver);
        if (window._mdDrop)
            document.removeEventListener('drop', window._mdDrop);
        window._mdDragOver = null;
        window._mdDrop = null;
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

    // Pointer capture and document-level delivery are browser-native. The
    // adapter throttles move delivery to one C# call per animation frame;
    // C# remains the owner of percentage math, clamping, keyboard policy and
    // the tap-to-reset decision. Every temporary listener is removed at the
    // end of the gesture or explicitly during component disposal.
    startSplitterDrag: function (dotnetRef, pointerId, startX, startY) {
        window.domBridge.cancelSplitterDrag();

        var container = document.querySelector('.editor-layout');
        var splitter = document.querySelector('.splitter');
        if (!container || !splitter) return;

        var moved = false;
        var pendingEvent = null;
        var animationFrame = 0;
        var invocation = Promise.resolve();

        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';

        try {
            splitter.setPointerCapture(pointerId);
        } catch {
            // Document-level listeners below still provide safe delivery on
            // engines that reject late pointer capture.
        }

        var sendPosition = function (event) {
            var rect = container.getBoundingClientRect();
            invocation = invocation
                .then(function () {
                    return dotnetRef.invokeMethodAsync(
                        'OnSplitterDrag', event.clientX, rect.left, rect.width);
                })
                .catch(function () {
                    // Component/browser teardown owns cancellation; a late
                    // move must not become an unhandled browser rejection.
                });
        };

        var flushMove = function () {
            animationFrame = 0;
            if (!pendingEvent) return;
            var event = pendingEvent;
            pendingEvent = null;
            sendPosition(event);
        };

        var move = function (e) {
            if (e.pointerId !== pointerId) return;
            e.preventDefault();

            if (!moved) {
                moved = Math.abs(e.clientX - startX) >= 3
                    || Math.abs(e.clientY - startY) >= 3;
            }
            if (!moved) return;

            pendingEvent = e;
            if (!animationFrame)
                animationFrame = requestAnimationFrame(flushMove);
        };

        var cleanup = function () {
            document.removeEventListener('pointermove', move);
            document.removeEventListener('pointerup', end);
            document.removeEventListener('pointercancel', cancel);
            if (animationFrame) cancelAnimationFrame(animationFrame);
            animationFrame = 0;
            pendingEvent = null;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            try {
                if (splitter.hasPointerCapture(pointerId))
                    splitter.releasePointerCapture(pointerId);
            } catch {
            }

            if (window._mdSplitterDragCleanup === cleanup)
                window._mdSplitterDragCleanup = null;
        };

        var finish = function (e, cancelled) {
            if (e.pointerId !== pointerId) return;
            e.preventDefault();

            if (moved && !cancelled)
                sendPosition(e);

            cleanup();
            invocation
                .then(function () {
                    return dotnetRef.invokeMethodAsync(
                        'OnSplitterEnd', cancelled || moved);
                })
                .catch(function () {
                });
        };

        var end = function (e) { finish(e, false); };
        var cancel = function (e) { finish(e, true); };

        window._mdSplitterDragCleanup = cleanup;
        document.addEventListener('pointermove', move, { passive: false });
        document.addEventListener('pointerup', end, { passive: false });
        document.addEventListener('pointercancel', cancel, { passive: false });
    },

    cancelSplitterDrag: function () {
        if (window._mdSplitterDragCleanup)
            window._mdSplitterDragCleanup();
    }
};
