// dom-events.js — high-frequency local-only DOM event handlers.
//
// Why these stay in JS rather than going through C# interop like the
// rest of the bridge: every `scroll` tick and every `dblclick` here
// would otherwise generate a JS→WASM round-trip. Even with WASM's
// in-process JS interop, routing tens of events per second through
// Blazor produces visible lag in the preview pane. The math is local
// to two DOM nodes (editor textarea ↔ preview), so there's no business
// logic to be tested elsewhere — it's purely geometry on values only
// the DOM knows.

window.domEvents = {
    // Editor scroll → preview proportional scroll (and vice versa).
    // Idempotent: re-binding after a tab change replaces the previous
    // listeners instead of stacking them.
    attachScrollSync: function (editorSelector, previewSelector) {
        var editor = document.querySelector(editorSelector);
        var preview = document.querySelector(previewSelector);
        if (!editor || !preview) return;
        if (editor._mdScrollSync) editor.removeEventListener('scroll', editor._mdScrollSync);
        if (preview._mdScrollSync) preview.removeEventListener('scroll', preview._mdScrollSync);

        var syncing = false;
        var editorHandler = function () {
            if (syncing) return;
            syncing = true;
            var ratio = editor.scrollTop / (editor.scrollHeight - editor.clientHeight || 1);
            preview.scrollTop = ratio * (preview.scrollHeight - preview.clientHeight);
            requestAnimationFrame(function () { syncing = false; });
        };
        var previewHandler = function () {
            if (syncing) return;
            syncing = true;
            var ratio = preview.scrollTop / (preview.scrollHeight - preview.clientHeight || 1);
            editor.scrollTop = ratio * (editor.scrollHeight - editor.clientHeight);
            requestAnimationFrame(function () { syncing = false; });
        };
        editor._mdScrollSync = editorHandler;
        preview._mdScrollSync = previewHandler;
        editor.addEventListener('scroll', editorHandler);
        preview.addEventListener('scroll', previewHandler);
    },

    // Renders an all-matches overlay onto the editor's mirror <div>.
    // The C# side passes the current textarea text plus the [{start,end}]
    // ranges; this function builds the corresponding <mark>-wrapped HTML
    // and assigns it to the mirror's innerHTML. The mirror sits behind
    // the textarea (see CSS) so the highlights show through wherever
    // the textarea's background is transparent.
    //
    // Performance: this runs only when the find state changes (not on
    // every keystroke), so an O(matches+text) string build per call is
    // fine. HTML-escaping is unrolled into a small loop rather than a
    // regex replace to keep allocations down on long documents.
    renderHighlights: function (mirrorSelector, text, ranges, currentIndex) {
        var mirror = document.querySelector(mirrorSelector);
        if (!mirror) return;
        if (!text) { mirror.innerHTML = ''; return; }
        if (!ranges || ranges.length === 0) {
            mirror.textContent = text; // textContent escapes implicitly
            return;
        }
        var html = '';
        var cursor = 0;
        for (var i = 0; i < ranges.length; i++) {
            var r = ranges[i];
            if (r.start > cursor) html += escapeHtml(text.substring(cursor, r.start));
            var cls = (i === currentIndex) ? 'match-current' : '';
            html += '<mark' + (cls ? ' class="' + cls + '"' : '') + '>' +
                    escapeHtml(text.substring(r.start, r.end)) + '</mark>';
            cursor = r.end;
        }
        if (cursor < text.length) html += escapeHtml(text.substring(cursor));
        mirror.innerHTML = html;

        function escapeHtml(s) {
            var out = '';
            for (var j = 0; j < s.length; j++) {
                var c = s.charCodeAt(j);
                if (c === 38) out += '&amp;';        // &
                else if (c === 60) out += '&lt;';    // <
                else if (c === 62) out += '&gt;';    // >
                else if (c === 34) out += '&quot;'; // "
                else out += s.charAt(j);
            }
            return out;
        }
    },

    // Mirror the textarea's scrollTop/scrollLeft onto the highlight div
    // so the marks stay aligned with the text the user is editing.
    // Bound once per textarea instance.
    attachHighlightScrollSync: function (textareaSelector, mirrorSelector) {
        var ta = document.querySelector(textareaSelector);
        var mirror = document.querySelector(mirrorSelector);
        if (!ta || !mirror) return;
        if (ta._mdHighlightSync) ta.removeEventListener('scroll', ta._mdHighlightSync);
        var handler = function () {
            mirror.scrollTop = ta.scrollTop;
            mirror.scrollLeft = ta.scrollLeft;
        };
        ta._mdHighlightSync = handler;
        ta.addEventListener('scroll', handler);
        // Prime: align once on attach so the mirror starts at the right offset.
        handler();
    },

    // Double-click in editor jumps the preview to the proportional
    // vertical position of the click, and vice versa.
    attachDoubleClickJump: function (editorSelector, previewSelector) {
        var editor = document.querySelector(editorSelector);
        var preview = document.querySelector(previewSelector);
        if (!editor || !preview) return;
        if (editor._mdDblJump) editor.removeEventListener('dblclick', editor._mdDblJump);
        if (preview._mdDblJump) preview.removeEventListener('dblclick', preview._mdDblJump);

        var editorHandler = function (e) {
            var rect = editor.getBoundingClientRect();
            var clickY = e.clientY - rect.top + editor.scrollTop;
            var ratio = clickY / editor.scrollHeight;
            preview.scrollTop = ratio * preview.scrollHeight - preview.clientHeight / 2;
        };
        var previewHandler = function (e) {
            var rect = preview.getBoundingClientRect();
            var clickY = e.clientY - rect.top + preview.scrollTop;
            var ratio = clickY / preview.scrollHeight;
            editor.scrollTop = ratio * editor.scrollHeight - editor.clientHeight / 2;
            editor.focus();
        };
        editor._mdDblJump = editorHandler;
        preview._mdDblJump = previewHandler;
        editor.addEventListener('dblclick', editorHandler);
        preview.addEventListener('dblclick', previewHandler);
    }
};
