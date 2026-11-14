const { test } = require('node:test');
const assert = require('node:assert/strict');
const core = require('../../tools/evidence_annotation/annotation-core.js');

const display = { width: 500, height: 700 };

test('drags map to the 0..1000 image space, rounding outward and clamping to the image', () => {
    assert.deepEqual(core.normalizeBox({ x: 50, y: 70 }, { x: 250, y: 140 }, display), { x: 100, y: 100, width: 400, height: 100 });
    // Dragging up-left gives the same box.
    assert.deepEqual(core.normalizeBox({ x: 250, y: 140 }, { x: 50, y: 70 }, display), { x: 100, y: 100, width: 400, height: 100 });
    // Outside the image is clamped.
    assert.deepEqual(core.normalizeBox({ x: -20, y: -5 }, { x: 600, y: 900 }, display), { x: 0, y: 0, width: 1000, height: 1000 });
    // Fractional pixels round outward so the drawn area is never shrunk.
    assert.deepEqual(core.normalizeBox({ x: 10.3, y: 10.3 }, { x: 20.2, y: 20.2 }, display), { x: 20, y: 14, width: 21, height: 15 });
    // A click (or a sliver) is not a box.
    assert.equal(core.normalizeBox({ x: 10, y: 10 }, { x: 11, y: 40 }, display), null);
    assert.equal(core.normalizeBox({ x: 0, y: 0 }, { x: 10, y: 10 }, { width: 0, height: 10 }), null);
});

test('an image is complete only when every field has a status, boxes and the required facts', () => {
    const image = core.emptyImage('a.jpg', 'A'.repeat(64), 1000, 1400);
    assert.equal(core.missing(image).length, 6);

    image.facts.merchantName = 'CỬA HÀNG A';
    image.facts.totalAmount = 185000;
    image.fields.merchantName = { status: 'present', box: { x: 1, y: 1, width: 10, height: 10 } };
    image.fields.identifier = { status: 'present', box: null };
    image.fields.documentDate = { status: 'absent' };
    image.fields.totalAmount = { status: 'present', box: { x: 1, y: 800, width: 900, height: 40 } };
    assert.deepEqual(core.missing(image), ['Mã định danh: chưa vẽ khung']);

    image.fields.identifier.box = { x: 1, y: 60, width: 300, height: 20 };
    assert.deepEqual(core.missing(image), []);
});

test('exported files match the format the .NET evaluation harness reads', () => {
    const image = core.emptyImage('a.jpg', 'B'.repeat(64), 1000, 1400);
    Object.assign(image.facts, { merchantName: ' CỬA HÀNG A ', identifier: 'RCP-1', identifierField: 'receiptNumber',
        dateEvidence: '05/10/2026', totalAmount: 185000, totalEvidence: 'TỔNG THANH TOÁN 185.000 đ' });
    image.fields = {
        merchantName: { status: 'present', box: { x: 10, y: 20, width: 300, height: 30 } },
        identifier: { status: 'absent', box: { x: 9, y: 9, width: 9, height: 9 } }
    };

    const file = core.buildAnnotationFile('  reviewer-B ', '', [image], '2026-11-14');
    assert.equal(file.format, 'aura-evidence-annotations-v1');
    assert.equal(file.annotator, 'reviewer-B');
    assert.equal(file.note, null);
    const exported = file.images[0];
    assert.equal(exported.facts.merchantName, 'CỬA HÀNG A');
    assert.deepEqual(Object.keys(exported.fields), ['merchantName', 'identifier', 'documentDate', 'totalAmount']);
    // An absent field never carries a stale box; an unanswered field is "unset", never a negative control.
    assert.deepEqual(exported.fields.identifier, { status: 'absent' });
    assert.deepEqual(exported.fields.totalAmount, { status: 'unset' });
    assert.deepEqual(core.parseAnnotationFile(JSON.stringify(file)).images[0].fields.merchantName.box,
        { x: 10, y: 20, width: 300, height: 30 });
    assert.throws(() => core.parseAnnotationFile('{"format":"other","images":[]}'), /annotations\.json/);
});
