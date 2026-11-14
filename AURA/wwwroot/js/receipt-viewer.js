/* In-page receipt viewer. Reads the existing NoStore receipt route; never changes facts or decisions.
   When the local evidence localizer is enabled it also draws read-only OCR evidence boxes. */
(function (root) {
    'use strict';

    const SVG_NS = 'http://www.w3.org/2000/svg';
    const SPACE = 1000;

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

    // Evidence regions use the same request ID as the receipt route.
    function evidenceUrl(source, base) {
        if (!source || !base) return null;
        const id = new URL(source).pathname.split('/').pop();
        return /^[A-Za-z0-9-]+$/.test(id || '') ? `${base.replace(/\/$/, '')}/${id}` : null;
    }

    // Fail closed: draw only well-formed boxes inside the 0..1000 space; everything else is dropped.
    function validRegions(payload) {
        const space = payload?.coordinateSpace;
        if (!payload || space?.width !== SPACE || space?.height !== SPACE || space.orientationApplied !== true)
            return { regions: [], unlocated: [] };
        const isInt = value => Number.isInteger(value) && value >= 0 && value <= SPACE;
        const regions = (Array.isArray(payload.regions) ? payload.regions : []).filter(region =>
            typeof region?.field === 'string' && typeof region.label === 'string' &&
            isInt(region.box?.x) && isInt(region.box?.y) && isInt(region.box?.width) && isInt(region.box?.height) &&
            region.box.width > 0 && region.box.height > 0 &&
            region.box.x + region.box.width <= SPACE && region.box.y + region.box.height <= SPACE);
        const unlocated = (Array.isArray(payload.unlocated) ? payload.unlocated : []).filter(item =>
            typeof item?.label === 'string' && typeof item.reason === 'string');
        return { regions, unlocated };
    }

    // Fixed card order for the four evidence fields; unknown fields go last.
    const FIELD_ORDER = ['merchantName', 'identifier', 'documentDate', 'totalAmount'];
    function fieldRank(field) {
        const index = FIELD_ORDER.indexOf(field);
        return index < 0 ? FIELD_ORDER.length : index;
    }

    function describe(link) {
        const row = link.closest('tr');
        const code = row?.querySelector('.cell-code')?.textContent.trim() || '';
        const image = link.querySelector('img');
        const name = link.getAttribute('title') || image?.getAttribute('alt') || 'Ảnh chứng từ';
        return { title: code ? `${name} · ${code}` : name, alt: image?.getAttribute('alt') || name };
    }

    if (typeof module !== 'undefined' && module.exports)
        module.exports = { isPlainActivation, receiptSource, evidenceUrl, validRegions, fieldRank, describe };
    if (!root.document) return;

    document.addEventListener('DOMContentLoaded', () => {
        const dialog = document.getElementById('receiptViewer');
        if (!dialog || typeof dialog.showModal !== 'function') return;
        const image = document.getElementById('receiptViewerImage');
        const title = document.getElementById('receiptViewerTitle');
        const status = document.getElementById('receiptViewerStatus');
        const openTab = document.getElementById('receiptViewerOpenTab');
        const zoom = document.getElementById('receiptViewerZoom');
        const overlay = document.getElementById('receiptViewerOverlay');
        const evidence = document.getElementById('receiptViewerEvidence');
        const evidenceStatus = document.getElementById('receiptViewerEvidenceStatus');
        const evidenceList = document.getElementById('receiptViewerEvidenceList');
        const evidenceToggle = document.getElementById('receiptViewerEvidenceToggle');
        const evidenceBase = dialog.dataset.evidenceUrl || '';
        dialog.classList.toggle('has-evidence', Boolean(evidenceBase && evidence));
        let returnFocus = null;
        let evidenceTarget = null;
        let evidenceRequest = 0;

        function setZoom(actualSize) {
            dialog.classList.toggle('is-actual-size', actualSize);
            zoom.setAttribute('aria-pressed', actualSize ? 'true' : 'false');
            zoom.textContent = actualSize ? 'Vừa khung' : 'Kích thước gốc';
        }

        function resetEvidence() {
            evidenceRequest++;
            evidenceTarget = null;
            overlay?.replaceChildren();
            // SVG elements have no hidden IDL property; toggle the attribute itself.
            overlay?.toggleAttribute('hidden', true);
            evidenceList?.replaceChildren();
            if (evidence) evidence.hidden = true;
        }

        function selectField(field) {
            overlay?.querySelectorAll('rect').forEach(rect =>
                rect.classList.toggle('is-active', rect.dataset.field === field));
            evidenceList?.querySelectorAll('button[data-field]').forEach(button =>
                button.setAttribute('aria-pressed', button.dataset.field === field ? 'true' : 'false'));
        }

        function renderEvidence({ regions, unlocated }) {
            for (const region of regions) {
                const rect = document.createElementNS(SVG_NS, 'rect');
                for (const key of ['x', 'y', 'width', 'height']) rect.setAttribute(key, String(region.box[key]));
                rect.dataset.field = region.field;
                rect.addEventListener('click', () => selectField(region.field));
                overlay.appendChild(rect);

            }
            // Every card is li > .receipt-evidence-item and the four fields keep one fixed order,
            // so located and unlocated cards line up the same way on every receipt.
            const cards = [
                ...regions.map(region => ({ field: region.field, located: true, region })),
                ...unlocated.map(missing => ({ field: missing.field, located: false, missing }))
            ].sort((left, right) => fieldRank(left.field) - fieldRank(right.field));
            for (const card of cards) {
                const item = document.createElement('li');
                const label = document.createElement('strong');
                const detail = document.createElement('span');
                let body;
                if (card.located) {
                    body = document.createElement('button');
                    body.type = 'button';
                    body.className = 'receipt-evidence-item is-located';
                    body.dataset.field = card.field;
                    body.setAttribute('aria-pressed', 'false');
                    body.addEventListener('click', () => selectField(card.field));
                    label.textContent = card.region.label;
                    // Raw OCR text (may lack diacritics or split digits); the AI facts are unchanged.
                    detail.textContent = card.region.text ? `OCR: “${card.region.text}”` : 'Đã định vị';
                } else {
                    body = document.createElement('div');
                    body.className = 'receipt-evidence-item is-unlocated';
                    label.textContent = card.missing.label;
                    detail.textContent = `Không định vị được: ${card.missing.reason}`;
                }
                body.append(label, detail);
                item.appendChild(body);
                evidenceList.appendChild(item);
            }
            overlay.toggleAttribute('hidden', regions.length === 0 || !evidenceToggle.checked);
            evidenceStatus.textContent = regions.length
                ? `Đã định vị ${regions.length} trường; chọn một trường để làm nổi khung. Chữ "OCR" là bản OCR đọc thô để đối chiếu vị trí; khung chỉ hỗ trợ đọc, không thay dữ kiện hay quyết định.`
                : 'Chưa định vị được trường nào; hãy đọc trực tiếp ảnh gốc.';
        }

        async function loadEvidence() {
            if (!evidenceTarget || !overlay || !evidence) return;
            const request = ++evidenceRequest;
            evidence.hidden = false;
            evidenceStatus.textContent = 'Đang chạy OCR cục bộ để định vị bằng chứng…';
            try {
                const response = await fetch(evidenceTarget, {
                    cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (request !== evidenceRequest) return;
                if (!response.ok) throw new Error('HTTP ' + response.status);
                const payload = validRegions(await response.json());
                if (request !== evidenceRequest) return;
                renderEvidence(payload);
            } catch {
                if (request !== evidenceRequest) return;
                evidenceStatus.textContent = 'Không tải được vùng bằng chứng; ảnh gốc vẫn xem bình thường.';
            }
        }

        function open(link, source) {
            const details = describe(link);
            resetEvidence();
            returnFocus = { element: link, href: link.getAttribute('href') };
            evidenceTarget = evidenceUrl(source, evidenceBase);
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
            loadEvidence();
        });
        image.addEventListener('error', () => {
            if (!dialog.open) return;
            image.hidden = true;
            status.hidden = false;
            status.classList.add('is-error');
            status.textContent = 'Không tải được ảnh chứng từ. Ảnh có thể đã hết hạn lưu hoặc không còn khớp SHA-256 đã ghi nhận.';
        });
        zoom.addEventListener('click', () => setZoom(!dialog.classList.contains('is-actual-size')));
        evidenceToggle?.addEventListener('change', () => {
            overlay.toggleAttribute('hidden', !evidenceToggle.checked || overlay.childElementCount === 0);
        });
        dialog.querySelector('[data-receipt-viewer-close]').addEventListener('click', () => dialog.close());
        // A click on the backdrop (the dialog box itself, outside its content) closes it.
        dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
        dialog.addEventListener('close', () => {
            // A queued close event can arrive after the viewer was already reopened for another
            // receipt; it must not clear the new image or its evidence.
            if (dialog.open) return;
            image.removeAttribute('src');
            resetEvidence();
            // Fragment polling may have replaced the row; fall back to the same receipt link.
            const target = returnFocus?.element?.isConnected ? returnFocus.element
                : Array.from(document.querySelectorAll('a.receipt-preview-link'))
                    .find(candidate => candidate.getAttribute('href') === returnFocus?.href);
            target?.focus({ preventScroll: true });
            returnFocus = null;
        });
    });
})(typeof window !== 'undefined' ? window : globalThis);
