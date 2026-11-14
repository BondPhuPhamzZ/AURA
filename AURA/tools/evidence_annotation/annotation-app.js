/* DOM wiring for the evidence annotation tool. Everything stays in this tab: no fetch, no storage. */
(function () {
    'use strict';
    const core = window.AuraAnnotation;
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const $ = id => document.getElementById(id);
    const state = { images: [], current: -1, activeField: null, imported: new Map(), drag: null };

    function setStatus(text) { $('status').textContent = text; }

    async function sha256Hex(buffer) {
        const digest = await crypto.subtle.digest('SHA-256', buffer);
        return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('').toUpperCase();
    }

    function naturalSize(url) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight });
            image.onerror = () => reject(new Error('Không đọc được ảnh.'));
            image.src = url;
        });
    }

    async function addFiles(files) {
        for (const file of files) {
            if (!/^image\/(jpeg|png)$/.test(file.type)) continue;
            if (state.images.some(image => image.file === file.name)) continue;
            const buffer = await file.arrayBuffer();
            const sha256 = await sha256Hex(buffer);
            const url = URL.createObjectURL(file);
            const size = await naturalSize(url);
            const previous = state.imported.get(file.name);
            let image = core.emptyImage(file.name, sha256, size.width, size.height);
            if (previous) {
                if (previous.sha256 !== sha256) {
                    setStatus(`${file.name}: nội dung ảnh khác với annotation đã mở (SHA-256 không khớp); tạo bản đánh dấu mới.`);
                } else {
                    image = { ...image, facts: { ...image.facts, ...previous.facts }, fields: previous.fields || {} };
                    for (const key of ['merchantName', 'identifier', 'dateEvidence', 'totalEvidence'])
                        image.facts[key] = image.facts[key] ?? '';
                }
            }
            state.images.push({ ...image, url });
        }
        if (state.current < 0 && state.images.length) state.current = 0;
        render();
    }

    function currentImage() { return state.images[state.current] || null; }

    function renderList() {
        const list = $('imageList');
        list.replaceChildren(...state.images.map((image, index) => {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = image.file;
            button.classList.toggle('done', core.missing(image).length === 0);
            if (index === state.current) button.setAttribute('aria-current', 'true');
            button.addEventListener('click', () => { state.current = index; state.activeField = null; render(); });
            item.appendChild(button);
            return item;
        }));
    }

    function renderOverlay() {
        const image = currentImage();
        const overlay = $('overlay');
        overlay.replaceChildren();
        if (!image) return;
        for (const field of core.FIELDS) {
            const box = image.fields[field.key]?.status === 'present' ? image.fields[field.key].box : null;
            if (!box) continue;
            const rect = document.createElementNS(SVG_NS, 'rect');
            for (const key of ['x', 'y', 'width', 'height']) rect.setAttribute(key, String(box[key]));
            if (field.key === state.activeField) rect.classList.add('active');
            overlay.appendChild(rect);
        }
        if (state.drag?.box) {
            const rect = document.createElementNS(SVG_NS, 'rect');
            for (const key of ['x', 'y', 'width', 'height']) rect.setAttribute(key, String(state.drag.box[key]));
            rect.classList.add('draft');
            overlay.appendChild(rect);
        }
    }

    function renderFields() {
        const image = currentImage();
        const container = $('fields');
        container.replaceChildren();
        if (!image) return;
        for (const field of core.FIELDS) {
            const truth = image.fields[field.key] || {};
            const set = document.createElement('fieldset');
            const legend = document.createElement('legend');
            legend.textContent = field.label;
            const row = document.createElement('div');
            row.className = 'field-row';
            for (const [value, text] of [['present', 'Có trên ảnh'], ['absent', 'Không có / không đọc được']]) {
                const label = document.createElement('label');
                const radio = document.createElement('input');
                radio.type = 'radio';
                radio.name = 'status-' + field.key;
                radio.value = value;
                radio.checked = truth.status === value;
                radio.addEventListener('change', () => {
                    image.fields[field.key] = value === 'present' ? { status: 'present', box: truth.box || null } : { status: 'absent' };
                    state.activeField = value === 'present' ? field.key : null;
                    render();
                });
                label.append(radio, ' ' + text);
                row.appendChild(label);
            }
            const draw = document.createElement('button');
            draw.type = 'button';
            draw.className = 'secondary';
            draw.textContent = 'Vẽ khung';
            draw.disabled = truth.status !== 'present';
            draw.setAttribute('aria-pressed', state.activeField === field.key ? 'true' : 'false');
            draw.addEventListener('click', () => { state.activeField = field.key; render(); });
            row.appendChild(draw);
            const hint = document.createElement('p');
            hint.className = 'field-hint';
            hint.textContent = field.hint;
            const boxText = document.createElement('p');
            boxText.className = 'box-text';
            boxText.textContent = truth.status === 'present'
                ? (truth.box ? `Khung: x=${truth.box.x} y=${truth.box.y} rộng=${truth.box.width} cao=${truth.box.height} (thang 0..1000)` : 'Chưa vẽ khung.')
                : truth.status === 'absent' ? 'Không vẽ khung cho trường này.' : 'Chưa chọn trạng thái.';
            set.append(legend, row, hint, boxText);
            container.appendChild(set);
        }
    }

    function renderFacts() {
        const image = currentImage();
        for (const input of document.querySelectorAll('[data-fact]')) {
            const key = input.dataset.fact;
            input.value = image ? (image.facts[key] ?? '') : '';
        }
    }

    function render() {
        const image = currentImage();
        $('stage').hidden = !image;
        $('panel').hidden = !image;
        if (image && $('photo').getAttribute('src') !== image.url) $('photo').src = image.url;
        $('drawHint').textContent = image
            ? (state.activeField ? `Kéo chuột trên ảnh để vẽ khung cho "${core.FIELDS.find(f => f.key === state.activeField).label}".` : 'Chọn trạng thái và bấm "Vẽ khung" cho từng trường.')
            : '';
        renderList();
        renderOverlay();
        renderFields();
        renderFacts();
        const problems = image ? core.missing(image) : [];
        $('missing').textContent = image ? (problems.length ? 'Còn thiếu:\n' + problems.join('\n') : 'Ảnh này đã đủ thông tin.') : '';
        $('preview').value = JSON.stringify(buildFile(), null, 2);
    }

    function buildFile() {
        return core.buildAnnotationFile($('annotator').value, $('note').value,
            state.images.map(({ url, ...image }) => image), new Date().toISOString().slice(0, 10));
    }

    function pointFrom(event) {
        const rect = $('photo').getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top, display: { width: rect.width, height: rect.height } };
    }

    $('stage').addEventListener('pointerdown', event => {
        const image = currentImage();
        if (!image || !state.activeField || image.fields[state.activeField]?.status !== 'present') return;
        const start = pointFrom(event);
        state.drag = { start, box: null };
        $('stage').setPointerCapture(event.pointerId);
        event.preventDefault();
    });
    $('stage').addEventListener('pointermove', event => {
        if (!state.drag) return;
        const point = pointFrom(event);
        state.drag.box = core.normalizeBox(state.drag.start, point, point.display);
        renderOverlay();
    });
    $('stage').addEventListener('pointerup', event => {
        if (!state.drag) return;
        const point = pointFrom(event);
        const box = core.normalizeBox(state.drag.start, point, point.display);
        state.drag = null;
        const image = currentImage();
        if (box && image) image.fields[state.activeField] = { status: 'present', box };
        render();
    });

    for (const input of document.querySelectorAll('[data-fact]')) {
        input.addEventListener('input', () => {
            const image = currentImage();
            if (!image) return;
            image.facts[input.dataset.fact] = input.type === 'number' ? (input.value === '' ? null : Number(input.value)) : input.value;
            renderList();
            const problems = core.missing(image);
            $('missing').textContent = problems.length ? 'Còn thiếu:\n' + problems.join('\n') : 'Ảnh này đã đủ thông tin.';
            $('preview').value = JSON.stringify(buildFile(), null, 2);
        });
    }
    $('annotator').addEventListener('input', render);
    $('note').addEventListener('input', render);
    $('images').addEventListener('change', event => addFiles(Array.from(event.target.files || [])));
    $('import').addEventListener('change', async event => {
        const file = event.target.files?.[0];
        if (!file) return;
        try {
            const parsed = core.parseAnnotationFile(await file.text());
            state.imported = new Map(parsed.images.map(image => [image.file, image]));
            $('annotator').value = parsed.annotator || '';
            $('note').value = parsed.note || '';
            setStatus(`Đã mở ${parsed.images.length} ảnh đã đánh dấu. Chọn lại đúng các file ảnh để tiếp tục (đối chiếu theo tên và SHA-256).`);
        } catch (error) {
            setStatus(error.message);
        }
    });
    $('export').addEventListener('click', async () => {
        if (!state.images.length) { setStatus('Chưa có ảnh nào.'); return; }
        const text = JSON.stringify(buildFile(), null, 2) + '\n';
        const incomplete = state.images.filter(image => core.missing(image).length).length;
        const link = document.createElement('a');
        link.href = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
        link.download = 'annotations.json';
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        const hash = await sha256Hex(new TextEncoder().encode(text));
        setStatus((incomplete ? `Lưu ý: còn ${incomplete} ảnh chưa đủ thông tin; bộ chạy sẽ từ chối file này. ` : '') +
            `Đã xuất annotations.json. SHA-256 để khóa trước khi chạy: ${hash}`);
    });
    window.addEventListener('beforeunload', event => {
        if (state.images.length) { event.preventDefault(); event.returnValue = ''; }
    });
    render();
})();
