/* In-page receipt viewer. Reads the existing NoStore receipt route; never changes facts or decisions. */
(function (root) {
    'use strict';

    // Only a plain primary click opens the dialog; Ctrl/Cmd/Shift/Alt or middle click keep the
    // browser's normal "open in new tab" behaviour of the underlying link.
    function isPlainActivation(event) {
        return event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
    }

    // The receipt route is same-origin and keyed by request ID; refuse anything else so a
    // crafted href cannot turn the viewer into an external image loader.
    function receiptSource(href, origin) {
        try {
            const url = new URL(href, origin);
            // End-anchored so a hosting path base (e.g. /app/Applicant/Receipt/{id}) still matches.
            return url.origin === origin && !url.search && !url.hash &&
                /(?:^|\/)Applicant\/Receipt\/[A-Za-z0-9-]+$/.test(url.pathname) ? url.href : null;
        } catch {
            return null;
        }
    }

    function describe(link) {
        const row = link.closest('tr');
        const code = row?.querySelector('.cell-code')?.textContent.trim() || '';
        const image = link.querySelector('img');
        const name = link.getAttribute('title') || image?.getAttribute('alt') || 'Ảnh chứng từ';
        return { title: code ? `${name} · ${code}` : name, alt: image?.getAttribute('alt') || name };
    }

    if (typeof module !== 'undefined' && module.exports) module.exports = { isPlainActivation, receiptSource, describe };
    if (!root.document) return;

    document.addEventListener('DOMContentLoaded', () => {
        const dialog = document.getElementById('receiptViewer');
        if (!dialog || typeof dialog.showModal !== 'function') return;
        const image = document.getElementById('receiptViewerImage');
        const title = document.getElementById('receiptViewerTitle');
        const status = document.getElementById('receiptViewerStatus');
        const openTab = document.getElementById('receiptViewerOpenTab');
        const zoom = document.getElementById('receiptViewerZoom');
        let returnFocus = null;

        function setZoom(actualSize) {
            dialog.classList.toggle('is-actual-size', actualSize);
            zoom.setAttribute('aria-pressed', actualSize ? 'true' : 'false');
            zoom.textContent = actualSize ? 'Vừa khung' : 'Kích thước gốc';
        }

        function open(link, source) {
            const details = describe(link);
            returnFocus = { element: link, href: link.getAttribute('href') };
            title.textContent = details.title;
            image.alt = details.alt;
            openTab.href = source;
            status.hidden = false;
            status.textContent = 'Đang tải ảnh…';
            status.classList.remove('is-error');
            image.hidden = true;
            setZoom(false);
            zoom.disabled = true;
            image.src = source;
            dialog.showModal();
        }

        document.addEventListener('click', event => {
            const link = event.target.closest?.('a.receipt-preview-link');
            if (!link || !isPlainActivation(event)) return;
            const source = receiptSource(link.href, root.location.origin);
            if (!source) return;
            event.preventDefault();
            open(link, source);
        });

        image.addEventListener('load', () => {
            status.hidden = true;
            image.hidden = false;
            zoom.disabled = false;
        });
        image.addEventListener('error', () => {
            if (!dialog.open) return;
            image.hidden = true;
            status.hidden = false;
            status.classList.add('is-error');
            status.textContent = 'Không tải được ảnh chứng từ. Ảnh có thể đã hết hạn lưu hoặc không còn khớp SHA-256 đã ghi nhận.';
        });
        zoom.addEventListener('click', () => setZoom(!dialog.classList.contains('is-actual-size')));
        dialog.querySelector('[data-receipt-viewer-close]').addEventListener('click', () => dialog.close());
        // A click on the backdrop (the dialog box itself, outside its content) closes it.
        dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
        dialog.addEventListener('close', () => {
            image.removeAttribute('src');
            // Fragment polling may have replaced the row; fall back to the same receipt link.
            const target = returnFocus?.element?.isConnected ? returnFocus.element
                : Array.from(document.querySelectorAll('a.receipt-preview-link'))
                    .find(candidate => candidate.getAttribute('href') === returnFocus?.href);
            target?.focus({ preventScroll: true });
            returnFocus = null;
        });
    });
})(typeof window !== 'undefined' ? window : globalThis);
