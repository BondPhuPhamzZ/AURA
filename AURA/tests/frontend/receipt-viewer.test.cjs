const { test } = require('node:test');
const assert = require('node:assert/strict');
const { isPlainActivation, receiptSource, evidenceUrl, validRegions, fieldRank, describe } = require('../../wwwroot/js/receipt-viewer.js');

const origin = 'http://127.0.0.1:5000';

test('only the same-origin receipt route can be shown in the viewer', () => {
    assert.equal(receiptSource('/Applicant/Receipt/0f3a9c1e-77aa-4b1d-9e21-5c2d8f1b0a11', origin),
        `${origin}/Applicant/Receipt/0f3a9c1e-77aa-4b1d-9e21-5c2d8f1b0a11`);
    assert.equal(receiptSource('/app/Applicant/Receipt/abc123', origin), `${origin}/app/Applicant/Receipt/abc123`);
    for (const href of [
        'https://example.com/Applicant/Receipt/abc',
        '//example.com/Applicant/Receipt/abc',
        '/Applicant/Receipt/abc?download=1',
        '/Applicant/Receipt/abc#x',
        '/Applicant/Receipt/../Status/abc',
        '/Applicant/Receipt/',
        '/Reports/CurrentMonth',
        'javascript:alert(1)'
    ]) assert.equal(receiptSource(href, origin), null, href);
});

test('modified or non-primary clicks keep the native new-tab link', () => {
    const click = overrides => ({ button: 0, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, ...overrides });
    assert.equal(isPlainActivation(click()), true);
    for (const overrides of [{ button: 1 }, { ctrlKey: true }, { metaKey: true }, { shiftKey: true }, { altKey: true }])
        assert.equal(isPlainActivation(click(overrides)), false, JSON.stringify(overrides));
});

test('the dialog title names the receipt and the short case code', () => {
    const link = (title, alt, code) => ({
        getAttribute: name => (name === 'title' ? title : null),
        querySelector: () => (alt == null ? null : { getAttribute: () => alt }),
        closest: () => (code == null ? null : { querySelector: () => ({ textContent: ` ${code} ` }) })
    });
    assert.deepEqual(describe(link('R3-04.jpg', 'Ảnh chứng từ R3-04.jpg', 'aaaa1111')),
        { title: 'R3-04.jpg · aaaa1111', alt: 'Ảnh chứng từ R3-04.jpg' });
    assert.deepEqual(describe(link(null, 'Ảnh chứng từ x.jpg', null)),
        { title: 'Ảnh chứng từ x.jpg', alt: 'Ảnh chứng từ x.jpg' });
    assert.deepEqual(describe(link(null, null, null)), { title: 'Ảnh chứng từ', alt: 'Ảnh chứng từ' });
});

test('evidence regions are requested only for a receipt ID and only when the feature is on', () => {
    const source = `${origin}/Applicant/Receipt/0f3a9c1e-77aa-4b1d-9e21-5c2d8f1b0a11`;
    assert.equal(evidenceUrl(source, '/Evidence/Regions'), '/Evidence/Regions/0f3a9c1e-77aa-4b1d-9e21-5c2d8f1b0a11');
    assert.equal(evidenceUrl(source, '/Evidence/Regions/'), '/Evidence/Regions/0f3a9c1e-77aa-4b1d-9e21-5c2d8f1b0a11');
    assert.equal(evidenceUrl(source, ''), null);
    assert.equal(evidenceUrl(null, '/Evidence/Regions'), null);
});

test('malformed evidence payloads draw nothing (fail closed)', () => {
    const space = { width: 1000, height: 1000, orientationApplied: true };
    const good = { field: 'totalAmount', label: 'Tổng thanh toán', text: '241.920 đ', box: { x: 10, y: 790, width: 800, height: 40 } };
    const ok = validRegions({ coordinateSpace: space, regions: [good], unlocated: [{ field: 'identifier', label: 'Mã', reason: 'r' }] });
    assert.equal(ok.regions.length, 1);
    assert.equal(ok.unlocated.length, 1);

    for (const box of [
        { x: -1, y: 0, width: 10, height: 10 }, { x: 0, y: 0, width: 0, height: 10 },
        { x: 995, y: 0, width: 10, height: 10 }, { x: 0, y: 0, width: 10.5, height: 10 }, { x: '1', y: 0, width: 10, height: 10 }
    ]) assert.equal(validRegions({ coordinateSpace: space, regions: [{ ...good, box }] }).regions.length, 0, JSON.stringify(box));

    for (const coordinateSpace of [{ ...space, width: 100 }, { ...space, orientationApplied: false }, undefined])
        assert.deepEqual(validRegions({ coordinateSpace, regions: [good] }), { regions: [], unlocated: [] });
    assert.deepEqual(validRegions(null), { regions: [], unlocated: [] });
});

test('evidence cards keep one fixed field order whether located or not', () => {
    const order = ['totalAmount', 'merchantName', 'unknownField', 'documentDate', 'identifier']
        .sort((left, right) => fieldRank(left) - fieldRank(right));
    assert.deepEqual(order, ['merchantName', 'identifier', 'documentDate', 'totalAmount', 'unknownField']);
});
