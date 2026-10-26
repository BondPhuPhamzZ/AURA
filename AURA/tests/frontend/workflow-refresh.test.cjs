const { test } = require('node:test');
const assert = require('node:assert/strict');
const { FragmentReader } = require('../../wwwroot/js/workflow-refresh.js');

const deferred = () => { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; };
const html = value => ({ ok: true, headers: new Headers({ 'Content-Type': 'text/html; charset=utf-8' }), text: async () => value });
function setup(fetch, extras = {}) {
    const applied = [], failed = [];
    const reader = new FragmentReader({ fetch, canApply: () => true,
        apply: (key, value) => applied.push([key, value]), failed: (key, error) => failed.push([key, error]), ...extras });
    return { reader, applied, failed };
}

test('overlapping polls share one request and apply one result', async () => {
    const pending = deferred(); let calls = 0;
    const { reader, applied } = setup(() => { calls++; return pending.promise; });
    const first = reader.refresh('audit', '/audit');
    const second = reader.refresh('audit', '/audit');
    assert.equal(first, second);
    pending.resolve(html('committed'));
    assert.equal(await first, true);
    assert.equal(calls, 1);
    assert.deepEqual(applied, [['audit', 'committed']]);
});
test('late pre-mutation response cannot replace a newer committed fragment even if abort is ignored', async () => {
    const old = deferred(); let calls = 0;
    const { reader, applied } = setup(() => ++calls === 1 ? old.promise : Promise.resolve(html('after-commit')));
    const first = reader.refresh('reviewer', '/queue');
    reader.invalidate();
    await reader.refresh('reviewer', '/queue');
    old.resolve(html('before-commit'));
    assert.equal(await first, false);
    assert.deepEqual(applied, [['reviewer', 'after-commit']]);
});
test('hidden or inactive views do not start requests or apply in-flight results', async () => {
    let visible = false, calls = 0;
    const pending = deferred();
    const { reader, applied } = setup(() => { calls++; return pending.promise; }, { canApply: () => visible });
    assert.equal(await reader.refresh('applicant', '/queue'), false);
    assert.equal(calls, 0);
    visible = true;
    const first = reader.refresh('applicant', '/queue');
    visible = false;
    pending.resolve(html('hidden-result'));
    assert.equal(await first, false);
    assert.deepEqual(applied, []);
});
test('HTTP and malformed responses keep existing UI and allow an explicit retry', async () => {
    let calls = 0;
    const { reader, applied, failed } = setup(async () => {
        calls++;
        if (calls === 1) return { ok: false, status: 503 };
        if (calls === 2) return { ok: true, headers: new Headers({ 'Content-Type': 'application/json' }) };
        return html('recovered');
    });
    assert.equal(await reader.refresh('audit', '/audit'), false);
    assert.equal(await reader.refresh('audit', '/audit'), false);
    assert.equal(applied.length, 0);
    assert.equal(failed.length, 2);
    assert.equal(await reader.refresh('audit', '/audit'), true);
    assert.deepEqual(applied, [['audit', 'recovered']]);
});
test('SQL error fragment rejected by renderer is surfaced rather than clearing the UI', async () => {
    const { reader, failed } = setup(async () => html('db-error'), { apply: () => { throw new Error('SQL unavailable'); } });
    assert.equal(await reader.refresh('dashboard', '/dashboard'), false);
    assert.equal(failed.length, 1);
});
test('an obsolete request failure does not mark the newly selected tab stale', async () => {
    const old = deferred(); let calls = 0;
    const { reader, failed } = setup(() => ++calls === 1 ? old.promise : Promise.resolve(html('new-tab')));
    const first = reader.refresh('audit', '/audit');
    reader.invalidate();
    await reader.refresh('reviewer', '/queue');
    old.reject(new Error('old network failure'));
    await first;
    assert.equal(failed.length, 0);
});
test('a timed-out read releases the coordinator and reports a recoverable failure', async () => {
    const { reader, failed } = setup((url, { signal }) => new Promise((resolve, reject) => {
        signal.addEventListener('abort', () => reject(new Error('aborted')), { once: true });
    }), { timeoutMs: 5 });
    assert.equal(await reader.refresh('audit', '/audit'), false);
    assert.equal(reader.pending, null);
    assert.equal(failed.length, 1);
});
test('a synchronous transport exception also releases the reader for recovery', async () => {
    let calls = 0;
    const { reader, applied, failed } = setup(() => {
        if (++calls === 1) throw new Error('transport could not start');
        return Promise.resolve(html('recovered'));
    });
    assert.equal(await reader.refresh('audit', '/audit'), false);
    assert.equal(reader.pending, null);
    assert.equal(await reader.refresh('audit', '/audit'), true);
    assert.equal(failed.length, 1);
    assert.deepEqual(applied, [['audit', 'recovered']]);
});
