/* Read-only fragments follow committed SQL state. No automatic POST retry. */
(function (root) {
    'use strict';

    class FragmentReader {
        constructor({ fetch, apply, failed, canApply, timeoutMs = 15000 }) {
            Object.assign(this, { fetch, apply, failed, canApply, timeoutMs });
            this.version = 0;
            this.pending = null;
        }
        invalidate() {
            this.version++;
            this.controller?.abort();
            this.pending = null;
        }
        refresh(key, url) {
            if (!this.canApply(key)) return Promise.resolve(false);
            if (this.pending) return this.pending;
            const version = ++this.version;
            const controller = this.controller = new AbortController();
            const timer = setTimeout(() => controller.abort(), this.timeoutMs);
            const operation = (async () => {
                try {
                    const response = await this.fetch(url, {
                        cache: 'no-store', signal: controller.signal,
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    if (!response.ok) throw new Error('HTTP ' + response.status);
                    if (!response.headers.get('content-type')?.includes('text/html'))
                        throw new Error('Unexpected fragment format');
                    const html = await response.text();
                    if (version !== this.version || !this.canApply(key)) return false;
                    this.apply(key, html);
                    return true;
                } catch (error) {
                    if (version === this.version && this.canApply(key)) this.failed(key, error);
                    return false;
                } finally {
                    clearTimeout(timer);
                }
            })();
            this.pending = operation;
            const release = () => { if (this.pending === operation) this.pending = null; };
            operation.then(release, release);
            return operation;
        }
    }

    if (typeof module !== 'undefined' && module.exports) module.exports = { FragmentReader };
    if (!root.document) return;

    document.addEventListener('DOMContentLoaded', () => {
        const config = document.getElementById('workflowRefreshConfig');
        if (!config) return;
        const tabs = ['applicant', 'reviewer', 'audit', 'dashboard'];
        const hosts = { applicant: 'resultsBody', reviewer: 'reviewerQueueHost',
            audit: 'auditTrailHost', dashboard: 'dashboardSummaryHost' };
        const intervals = { applicant: 5000, reviewer: 5000, audit: 10000, dashboard: 15000 };
        let activeTab = tabs.find(tab => document.getElementById('pane-' + tab)?.style.display !== 'none') || 'applicant';
        let mutating = false;
        let confirmed = false;
        let mutationFocus = null;
        let pollTimer;
        const stale = new Set();
        const disabledStates = new WeakMap();
        const tabPositions = new Map();
        const defaultDashboardPeriod = document.getElementById('dashboardPeriod')?.value;
        const notice = document.getElementById('workflowSyncNotice');
        const status = document.getElementById('workflowUpdateStatus');

        function updateActionAvailability() {
            tabs.forEach(tab => {
                document.querySelectorAll('#pane-' + tab + ' .js-workflow-form button, #pane-' + tab + ' button[onclick^="forward"]')
                    .forEach(button => {
                        const blocked = mutating || stale.has(tab);
                        if (blocked && !disabledStates.has(button)) disabledStates.set(button, button.disabled);
                        if (blocked) button.disabled = true;
                        else if (disabledStates.has(button)) {
                            button.disabled = disabledStates.get(button);
                            disabledStates.delete(button);
                        }
                    });
            });
        }
        function showSyncState() {
            notice.hidden = !stale.has(activeTab);
            document.getElementById('workflowSyncMessage').textContent =
                'Chưa cập nhật được dữ liệu. Đang giữ bản gần nhất; cập nhật lại trước khi thao tác.';
            document.getElementById('workflowFullRefresh').href = location.pathname + location.search;
            updateActionAvailability();
        }
        function captureView(host) {
            const focused = host.contains(document.activeElement) ? document.activeElement : null;
            const form = focused?.closest('form');
            return {
                x: window.scrollX, y: window.scrollY,
                scroll: Array.from(host.querySelectorAll('.table-scroll-area')).map(el => [el.scrollLeft, el.scrollTop]),
                focus: focused && { id: focused.id, name: focused.name,
                    rowId: focused.closest('tr[data-case-id]')?.dataset.caseId,
                    caseId: form?.querySelector('[name="id"]')?.value,
                    decision: form?.querySelector('[name="decision"]')?.value },
                from: document.getElementById('auditDateFrom')?.value || '',
                to: document.getElementById('auditDateTo')?.value || '',
                open: Array.from(host.querySelectorAll('tr[data-audit-id]')).filter(row => row.querySelector('details[open]'))
                    .map(row => row.dataset.auditId)
            };
        }
        function restoreView(host, state) {
            host.querySelectorAll('.table-scroll-area').forEach((el, index) => {
                if (state.scroll[index]) [el.scrollLeft, el.scrollTop] = state.scroll[index];
            });
            if (activeTab === 'audit') {
                const from = document.getElementById('auditDateFrom');
                const to = document.getElementById('auditDateTo');
                if (from) from.value = state.from;
                if (to) to.value = state.to;
                host.querySelectorAll('tr[data-audit-id]').forEach(row => {
                    const details = row.querySelector('details');
                    if (details) details.open = state.open.includes(row.dataset.auditId);
                });
                initializeAuditFilter();
            }
            if (state.focus) {
                let target = state.focus.id && document.getElementById(state.focus.id);
                if (!target && state.focus.caseId) {
                    const form = Array.from(host.querySelectorAll('form')).find(candidate =>
                        candidate.querySelector('[name="id"]')?.value === state.focus.caseId &&
                        candidate.querySelector('[name="decision"]')?.value === state.focus.decision);
                    target = form?.querySelector('button');
                }
                if (!target && state.focus.rowId) {
                    const row = Array.from(host.querySelectorAll('tr[data-case-id]'))
                        .find(candidate => candidate.dataset.caseId === state.focus.rowId);
                    target = row?.querySelector('button');
                }
                target ||= host.querySelector('h2,h3,h4') || document.getElementById('pane-' + activeTab);
                if (!target.matches('input,button,a,select,textarea')) target.tabIndex = -1;
                target.focus({ preventScroll: true });
            }
            window.scrollTo(state.x, state.y);
        }
        const reader = new FragmentReader({
            fetch: (...args) => fetch(...args),
            canApply: tab => tab === activeTab && !document.hidden && !mutating,
            apply: (tab, html) => {
                const probe = document.createElement(tab === 'applicant' ? 'tbody' : 'div');
                probe.innerHTML = html;
                const expected = { applicant: 'tr[data-case-id],td.empty-state', reviewer: '.escalation-queue',
                    audit: '.audit-log-queue', dashboard: '.dashboard-summary' };
                if (probe.querySelector('[data-fragment-error]') || !probe.querySelector(expected[tab]))
                    throw new Error('Server cannot load workflow data');
                const host = document.getElementById(hosts[tab]);
                const state = captureView(host);
                if (!state.focus && notice.contains(document.activeElement)) state.focus = {};
                if (!state.focus && mutationFocus?.tab === tab) state.focus = mutationFocus.focus;
                if (tab === 'applicant') root.applyEmployeeEscalationFragment(html);
                else host.innerHTML = html;
                stale.delete(tab);
                showSyncState();
                restoreView(host, state);
                status.textContent = 'Đã cập nhật dữ liệu từ máy chủ.';
            },
            failed: tab => {
                stale.add(tab);
                showSyncState();
                status.textContent = 'Chưa cập nhật được dữ liệu; đang giữ bản gần nhất.';
                console.warn('AURA: workflow data refresh unavailable; previous rows retained.');
            }
        });
        function schedule() {
            clearTimeout(pollTimer);
            if (!document.hidden && !mutating) pollTimer = setTimeout(async () => {
                await refresh();
                schedule();
            }, stale.has(activeTab) ? 30000 : intervals[activeTab]);
        }
        async function refresh(tab = activeTab) {
            if (tab !== activeTab || document.hidden || mutating) return false;
            const url = new URL(config.dataset[tab + 'Url'], location.href);
            if (tab === 'dashboard') {
                const period = new URL(location.href).searchParams.get('dashboardPeriod') ||
                    defaultDashboardPeriod;
                if (period) url.searchParams.set('dashboardPeriod', period);
            }
            return reader.refresh(tab, url.pathname + url.search);
        }
        function navigate(tab, push = true) {
            if (!tabs.includes(tab)) return;
            tabPositions.set(activeTab, [window.scrollX, window.scrollY]);
            reader.invalidate();
            activeTab = tab;
            root.switchTab(tab);
            if (push) {
                const url = new URL(location.href);
                if (tab === 'applicant') url.searchParams.delete('tab');
                else url.searchParams.set('tab', tab);
                history.pushState(null, '', url.pathname + url.search);
            }
            const position = tabPositions.get(tab) || [0, 0];
            window.scrollTo(...position);
            showSyncState();
            document.dispatchEvent(new Event('aura:tabchanged'));
            refresh().then(schedule);
        }
        root.AuraWorkflow = {
            navigate, refresh,
            rendered: updateActionAvailability,
            beginMutation() {
                if (mutating || stale.has(activeTab)) return false;
                mutationFocus = { tab: activeTab, focus: captureView(document.getElementById(hosts[activeTab])).focus };
                mutating = true;
                confirmed = false;
                clearTimeout(pollTimer);
                reader.invalidate();
                updateActionAvailability();
                return true;
            },
            confirmMutation() { confirmed = true; },
            async post(url, body) {
                const controller = new AbortController();
                const timer = setTimeout(() => controller.abort(), 20000);
                try {
                    const response = await fetch(url, { method: 'POST',
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }, body, signal: controller.signal });
                    // Keep the timeout through response-body receipt. An uncertain
                    // response is reconciled by GET, never by an automatic POST.
                    const text = await response.text();
                    return new Response(text, { status: response.status, headers: response.headers });
                } finally { clearTimeout(timer); }
            },
            async endMutation() {
                // Refresh even after response loss: SQL may already have committed.
                mutating = false;
                reader.invalidate();
                if (confirmed) {
                    try { localStorage.setItem('aura.workflow.changed', String(Date.now())); }
                    catch { /* Storage-disabled browsers still use visible-tab polling. */ }
                }
                await refresh();
                mutationFocus = null;
                updateActionAvailability();
                schedule();
            },
            async whenApplicantVisible() {
                while (document.hidden || activeTab !== 'applicant') {
                    await new Promise(resolve => {
                        const wake = () => {
                            document.removeEventListener('visibilitychange', wake);
                            document.removeEventListener('aura:tabchanged', wake);
                            resolve();
                        };
                        document.addEventListener('visibilitychange', wake);
                        document.addEventListener('aura:tabchanged', wake);
                    });
                }
            }
        };
        document.getElementById('workflowRetryRefresh').addEventListener('click', () => refresh().then(schedule));
        document.addEventListener('visibilitychange', () => {
            reader.invalidate();
            if (!document.hidden) refresh().then(schedule);
            else clearTimeout(pollTimer);
        });
        window.addEventListener('storage', event => {
            if (event.key === 'aura.workflow.changed') {
                reader.invalidate();
                refresh().then(schedule);
            }
        });
        window.addEventListener('popstate', () => {
            const tab = new URL(location.href).searchParams.get('tab');
            navigate(tabs.includes(tab) ? tab : 'applicant', false);
        });
        document.addEventListener('change', event => {
            if (event.target.id !== 'dashboardPeriod') return;
            const url = new URL(location.href);
            url.searchParams.set('dashboardPeriod', event.target.value);
            history.pushState(null, '', url.pathname + url.search);
            reader.invalidate();
            refresh().then(schedule);
        });
        document.addEventListener('submit', event => {
            if (event.target.matches('.dashboard-period-form')) {
                event.preventDefault();
                document.getElementById('dashboardPeriod').dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
        document.addEventListener('click', event => {
            if (event.target.closest('.dashboard-refresh-button')) {
                event.preventDefault();
                refresh().then(schedule);
            }
        });
        initializeAuditFilter();
        refresh().then(schedule);
        document.dispatchEvent(new Event('aura:workflowready'));
    });

    function initializeAuditFilter() {
        const from = document.getElementById('auditDateFrom');
        const to = document.getElementById('auditDateTo');
        const reset = document.getElementById('auditFilterReset');
        const result = document.getElementById('auditFilterResult');
        const empty = document.getElementById('auditNoFilterResults');
        if (!from || !to || !reset || !result) return;
        const apply = () => {
            const rows = Array.from(document.querySelectorAll('.audit-data-row'));
            let visible = 0;
            to.min = from.value;
            from.max = to.value;
            rows.forEach(row => {
                const date = row.dataset.auditDate || '';
                row.hidden = Boolean((from.value && date < from.value) || (to.value && date > to.value));
                if (!row.hidden) visible++;
            });
            if (empty) empty.hidden = visible !== 0;
            result.textContent = `${visible}/${rows.length} bản ghi`;
        };
        if (!from.dataset.filterBound) {
            from.dataset.filterBound = 'true';
            from.addEventListener('change', apply);
            to.addEventListener('change', apply);
            reset.addEventListener('click', () => { from.value = ''; to.value = ''; apply(); });
        }
        apply();
    }
})(typeof window !== 'undefined' ? window : globalThis);
