/* Pure helpers for the evidence annotation tool. Shared by the page and node tests; no DOM, no network. */
(function (root) {
    'use strict';

    const FORMAT = 'aura-evidence-annotations-v1';
    const SPACE = 1000;
    const FIELDS = [
        { key: 'merchantName', label: 'Đơn vị bán', hint: 'Khung bao cả dòng tên đơn vị bán (nếu xuống dòng thì bao cả hai dòng).' },
        { key: 'identifier', label: 'Mã định danh', hint: 'Khung bao nhãn và mã trên cùng dòng, ví dụ "Số hóa đơn: 000142".' },
        { key: 'documentDate', label: 'Ngày in trên chứng từ', hint: 'Khung bao nhãn và ngày, ví dụ "Ngày hóa đơn: 05/10/2026".' },
        { key: 'totalAmount', label: 'Tổng thanh toán', hint: 'Khung bao cả hàng: nhãn tổng cuối cùng và số tiền cần thanh toán (không phải tạm tính).' }
    ];
    const IDENTIFIER_FIELDS = ['invoiceNumber', 'receiptNumber', 'transactionReference', 'orderId', 'bookingId', 'documentNumber'];

    // Converts a drag (in displayed CSS pixels) to the 0..1000 space of the image, rounding outward.
    function normalizeBox(start, end, display) {
        if (!display || display.width <= 0 || display.height <= 0) return null;
        const clamp = (value, max) => Math.min(Math.max(value, 0), max);
        const x1 = clamp(Math.min(start.x, end.x), display.width);
        const x2 = clamp(Math.max(start.x, end.x), display.width);
        const y1 = clamp(Math.min(start.y, end.y), display.height);
        const y2 = clamp(Math.max(start.y, end.y), display.height);
        const x = Math.floor(x1 * SPACE / display.width);
        const y = Math.floor(y1 * SPACE / display.height);
        const right = Math.min(SPACE, Math.ceil(x2 * SPACE / display.width));
        const bottom = Math.min(SPACE, Math.ceil(y2 * SPACE / display.height));
        // Ignore accidental clicks: a box must span at least 3 units each way.
        if (right - x < 3 || bottom - y < 3) return null;
        return { x, y, width: right - x, height: bottom - y };
    }

    function emptyImage(file, sha256, width, height) {
        return {
            file, sha256, width, height,
            facts: { merchantName: '', identifier: '', identifierField: 'invoiceNumber', dateEvidence: '', totalAmount: null, totalEvidence: '' },
            fields: {}
        };
    }

    // Lists what is still missing for one image; an empty list means the image is complete.
    function missing(image) {
        const problems = [];
        for (const field of FIELDS) {
            const truth = image.fields[field.key];
            if (!truth || (truth.status !== 'present' && truth.status !== 'absent')) problems.push(`${field.label}: chưa chọn trạng thái`);
            else if (truth.status === 'present' && !truth.box) problems.push(`${field.label}: chưa vẽ khung`);
        }
        if (!String(image.facts.merchantName || '').trim()) problems.push('Chưa nhập tên đơn vị bán');
        if (!(Number(image.facts.totalAmount) > 0)) problems.push('Chưa nhập tổng tiền (số)');
        if (!IDENTIFIER_FIELDS.includes(image.facts.identifierField)) problems.push('Loại mã định danh không hợp lệ');
        return problems;
    }

    function buildAnnotationFile(annotator, note, images, createdAt) {
        return {
            format: FORMAT,
            annotator: String(annotator || '').trim() || null,
            createdAt,
            note: String(note || '').trim() || null,
            images: images.map(image => ({
                file: image.file,
                sha256: image.sha256,
                width: image.width,
                height: image.height,
                facts: {
                    merchantName: String(image.facts.merchantName || '').trim() || null,
                    identifier: String(image.facts.identifier || '').trim() || null,
                    identifierField: image.facts.identifierField,
                    dateEvidence: String(image.facts.dateEvidence || '').trim() || null,
                    totalAmount: Number(image.facts.totalAmount) > 0 ? Number(image.facts.totalAmount) : null,
                    totalEvidence: String(image.facts.totalEvidence || '').trim() || null
                },
                fields: Object.fromEntries(FIELDS.map(field => {
                    const truth = image.fields[field.key];
                    // An unanswered field must not look like a negative control; the harness rejects "unset".
                    return [field.key, truth?.status === 'present' ? { status: 'present', box: truth.box ?? null }
                        : truth?.status === 'absent' ? { status: 'absent' } : { status: 'unset' }];
                }))
            }))
        };
    }

    // Reads an exported file back; images are re-attached by file name and SHA-256.
    function parseAnnotationFile(text) {
        const parsed = JSON.parse(text);
        if (parsed?.format !== FORMAT || !Array.isArray(parsed.images))
            throw new Error('Không phải file annotations.json của AURA.');
        return parsed;
    }

    const api = { FORMAT, SPACE, FIELDS, IDENTIFIER_FIELDS, normalizeBox, emptyImage, missing, buildAnnotationFile, parseAnnotationFile };
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    else root.AuraAnnotation = api;
})(typeof window !== 'undefined' ? window : globalThis);
