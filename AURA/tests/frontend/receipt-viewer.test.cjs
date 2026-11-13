const { test } = require('node:test');
const assert = require('node:assert/strict');
const { isPlainActivation, receiptSource, describe } = require('../../wwwroot/js/receipt-viewer.js');

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
