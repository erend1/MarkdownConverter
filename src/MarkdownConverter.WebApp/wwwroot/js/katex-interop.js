window.katexInterop = {
    renderMath: function (containerId) {
        var container = document.getElementById(containerId);
        if (!container || typeof katex === 'undefined') return;

        // Inline math: <span class="math"> — Markdig wraps content in \(...\)
        container.querySelectorAll('span.math').forEach(function (el) {
            if (el.dataset.rendered) return;
            try {
                var text = el.textContent.trim();
                // Strip Markdig's \( and \) delimiters (may have whitespace around them)
                text = text.replace(/^\\\(\s*/, '').replace(/\s*\\\)$/, '');
                if (!text) return;
                katex.render(text, el, {
                    throwOnError: false,
                    trust: false,
                    maxExpand: 1000,
                    displayMode: false
                });
                el.dataset.rendered = 'true';
            } catch (e) {
                // Show error inline instead of raw delimiters
                el.style.color = '#cf222e';
                el.style.fontSize = '0.85em';
                el.textContent = 'Math error: ' + e.message;
                el.dataset.rendered = 'true';
            }
        });

        // Display math: <div class="math"> — Markdig wraps content in \[...\]
        // Multi-line $$...$$ produces a MathBlock → <div class="math">\[\n content \n\]</div>
        container.querySelectorAll('div.math').forEach(function (el) {
            if (el.dataset.rendered) return;
            try {
                var text = el.textContent.trim();
                // Strip Markdig's \[ and \] delimiters — handle newlines and whitespace
                text = text.replace(/^\\\[\s*/, '').replace(/\s*\\\]$/, '');
                if (!text) return;
                katex.render(text.trim(), el, {
                    throwOnError: false,
                    trust: false,
                    maxExpand: 1000,
                    displayMode: true
                });
                el.dataset.rendered = 'true';
            } catch (e) {
                el.style.color = '#cf222e';
                el.style.fontSize = '0.85em';
                el.textContent = 'Math error: ' + e.message;
                el.dataset.rendered = 'true';
            }
        });
    }
};
