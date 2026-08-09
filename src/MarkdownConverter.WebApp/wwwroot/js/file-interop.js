window.desktopCapabilities = {
    read: function () {
        var marker = window.__markdownConverterDesktopCapabilities;
        if (!marker || typeof marker !== 'object') return null;

        return {
            canCompilePdf: marker.canCompilePdf === true,
            canReceivePendingFiles: marker.canReceivePendingFiles === true
        };
    }
};

window.fileInterop = {
    // Save with a "Save As" dialog when the host browser supports the
    // File System Access API; otherwise fall back to a legacy <a download>
    // trigger (file lands in Downloads). Returns one of:
    //   "saved"     — user picked a destination and the file was written
    //   "cancelled" — user dismissed the dialog
    //   "fallback"  — File System Access API unavailable; legacy download fired
    saveFile: async function (fileName, byteArray, mimeType) {
        var blob = new Blob([byteArray], { type: mimeType });

        if (window.showSaveFilePicker) {
            try {
                var extMatch = fileName && fileName.match(/\.[^.\\/]+$/);
                var ext = extMatch ? extMatch[0] : '';
                var pickerOpts = { suggestedName: fileName };
                if (ext) {
                    var accept = {};
                    accept[mimeType || 'application/octet-stream'] = [ext];
                    pickerOpts.types = [{
                        description: mimeType || 'File',
                        accept: accept
                    }];
                }

                var handle = await window.showSaveFilePicker(pickerOpts);
                var writable = await handle.createWritable();
                await writable.write(blob);
                await writable.close();
                return 'saved';
            } catch (e) {
                // AbortError = user clicked Cancel in the picker. Treat as a
                // first-class outcome so callers don't show a fake success toast.
                if (e && (e.name === 'AbortError' || e.code === 20)) {
                    return 'cancelled';
                }
                // Any other failure (SecurityError on expired user gesture,
                // NotAllowedError, etc.) — fall through to the legacy path so
                // the user still gets their file.
                console.warn('showSaveFilePicker failed, falling back:', e);
            }
        }

        // Legacy fallback — same behaviour the app had before.
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        return 'fallback';
    },

    // Desktop-exclusive: compile markdown to PDF via local API.
    // Returns a typed { success, error } shape so the C# side can deserialise
    // unambiguously — the prior contract returned `true` on success and a
    // string on failure, which System.Text.Json could not distinguish when
    // bound to `object`, causing successful compiles to be reported as errors.
    compilePdf: async function (markdown) {
        try {
            var resp = await fetch('/api/preview-pdf', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ markdown: markdown })
            });
            if (resp.ok) {
                var blob = await resp.blob();
                var url = URL.createObjectURL(blob);
                window.open(url, '_blank');
                return { success: true };
            }
            var text = await resp.text();
            return { success: false, error: text || 'PDF compilation failed' };
        } catch (e) {
            return { success: false, error: 'Error: ' + e.message };
        }
    },

    copyToClipboard: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            console.warn('Clipboard write failed:', e);
            return false;
        }
    },

    // When launched from Explorer ("Open with...", double-click,
    // or a default-app association), the Desktop wrapper stores the path
    // and exposes it once via /api/pending-file. Returns the {name, content}
    // payload on first call, or null when there is no pending file (Web
    // builds, ordinary launches, or a Blazor reload after the file was
    // already consumed).
    fetchPendingFile: async function () {
        try {
            var resp = await fetch('/api/pending-file', { method: 'GET' });
            if (resp.status === 204) return null;
            if (!resp.ok) return null;
            return await resp.json();
        } catch {
            return null;
        }
    },

    fetchPendingFiles: async function () {
        try {
            var resp = await fetch('/api/pending-files', { method: 'GET' });
            if (resp.status === 204) return null;
            if (!resp.ok) return null;
            return await resp.json();
        } catch {
            return null;
        }
    },

    printPreviewAsPdf: function (previewContainerId) {
        var container = document.getElementById(previewContainerId);
        if (!container) return;

        var printWindow = window.open('', '_blank', 'width=800,height=600');
        if (!printWindow) return;

        // Build print document with KaTeX CSS + preview styles
        var katexCss = document.querySelector('link[href*="katex"]');
        var katexLink = katexCss ? '<link rel="stylesheet" href="' + katexCss.href + '">' : '';

        printWindow.document.write([
            '<!DOCTYPE html><html><head>',
            '<meta charset="utf-8">',
            '<title>Print Preview</title>',
            katexLink,
            '<style>',
            'body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif; ',
            '  font-size: 14px; line-height: 1.6; color: #1f2328; max-width: 800px; margin: 0 auto; padding: 40px; }',
            'h1 { font-size: 2em; border-bottom: 1px solid #d1d9e0; padding-bottom: 0.3em; }',
            'h2 { font-size: 1.5em; border-bottom: 1px solid #d1d9e0; padding-bottom: 0.3em; }',
            'h3 { font-size: 1.25em; }',
            'code { font-family: Consolas, monospace; font-size: 85%; padding: 0.2em 0.4em; background: #f6f8fa; border-radius: 4px; }',
            'pre { padding: 16px; background: #f6f8fa; border-radius: 6px; border: 1px solid #d1d9e0; overflow: auto; }',
            'pre code { padding: 0; background: none; }',
            'blockquote { padding: 0 1em; border-left: 3px solid #d1d9e0; color: #656d76; }',
            'table { border-collapse: collapse; width: 100%; }',
            'th, td { padding: 6px 13px; border: 1px solid #d1d9e0; }',
            'th { font-weight: 600; background: #f6f8fa; }',
            'hr { border: 0; border-top: 1px solid #d1d9e0; }',
            'img { max-width: 100%; }',
            '.markdown-alert { padding: 8px 16px; margin-bottom: 16px; border-left: 4px solid; border-radius: 4px; }',
            '.markdown-alert-note { border-color: #0969da; background: rgba(9,105,218,0.05); }',
            '.markdown-alert-tip { border-color: #1a7f37; background: rgba(26,127,55,0.05); }',
            '.markdown-alert-important { border-color: #8250df; background: rgba(130,80,223,0.05); }',
            '.markdown-alert-warning { border-color: #9a6700; background: rgba(154,103,0,0.05); }',
            '.markdown-alert-caution { border-color: #cf222e; background: rgba(207,34,46,0.05); }',
            '@media print { body { padding: 0; } }',
            '</style>',
            '</head><body>',
            container.innerHTML,
            '</body></html>'
        ].join('\n'));
        printWindow.document.close();

        // Wait for KaTeX CSS to load, then print
        printWindow.onload = function () {
            setTimeout(function () {
                printWindow.print();
                printWindow.close();
            }, 300);
        };
    }
};
