const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

// The dashboard script is inline Razor; load just the two pure helpers from it.
const view = fs.readFileSync(path.join(__dirname, '..', '..', 'Views', 'Home', 'Index.cshtml'), 'utf8');
function extract(name) {
    const start = view.indexOf(`function ${name}(`);
    assert.ok(start >= 0, `${name} is missing from Index.cshtml`);
    const end = view.indexOf('\n    }', start);
    return view.slice(start, end + '\n    }'.length);
}
const { escapeHtml, escapeJsString } = new Function(
    `${extract('escapeHtml')}\n${extract('escapeJsString')}\nreturn { escapeHtml, escapeJsString };`)();

test('escapeHtml is safe inside quoted attribute values', () => {
    const fileName = `a" onerror="alert(1)' x='<b>&.jpg`;
    const escaped = escapeHtml(fileName);
    assert.equal(escaped, 'a&quot; onerror=&quot;alert(1)&#39; x=&#39;&lt;b&gt;&amp;.jpg');
    assert.doesNotMatch(escaped, /["'<>]/);
    assert.equal(escapeHtml(null), '');
    assert.equal(escapeHtml(42), '42');
});

test('inline handler arguments survive JS and HTML escaping', () => {
    const caseId = `x'); alert("1`;
    const attribute = escapeHtml(escapeJsString(caseId));
    assert.doesNotMatch(attribute, /["<>]/);
    // The browser HTML-decodes the attribute before parsing the JavaScript string literal.
    const decoded = attribute.replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&amp;/g, '&');
    assert.equal(new Function(`return '${decoded}';`)(), caseId);
});
