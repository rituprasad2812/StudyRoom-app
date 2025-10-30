(function () {
    'use strict';

    var cfg = window.StudyRoom || {};
    var roomId = (cfg.roomId || '').toString();
    var storageKey = 'room:' + roomId + ':tab';

    // Shared state (badges/glance)
    var state = window.RoomState || {
        chatUnread: 0,
        onlineCount: 0,
        taskOpen: 0,
        taskOverdue: 0,
        pollOpen: 0,
        timerRunning: false,
        timerRemain: 0
    };
    window.RoomState = state;

    function qs(sel) { return document.querySelector(sel); }
    function qsa(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }
    function show(id) { var el = qs(id); if (el) el.classList.remove('d-none'); }
    function hide(id) { var el = qs(id); if (el) el.classList.add('d-none'); }

    var activeTab = (localStorage.getItem(storageKey) || 'chat');

    function updateBadges() {
        var bc = qs('#tab-badge-chat');
        if (bc) { if (state.chatUnread > 0) { bc.textContent = state.chatUnread; show('#tab-badge-chat'); } else { hide('#tab-badge-chat'); } }
        var bt = qs('#tab-badge-tasks');
        if (bt) { if (state.taskOpen > 0) { bt.textContent = state.taskOpen; show('#tab-badge-tasks'); } else hide('#tab-badge-tasks'); }

        var bp = qs('#tab-badge-polls');
        if (bp) { if (state.pollOpen > 0) { bp.textContent = state.pollOpen; show('#tab-badge-polls'); } else hide('#tab-badge-polls'); }

        var dot = qs('#tab-dot-timer');
        var tb = qs('#tab-badge-timer');
        if (dot) { if (state.timerRunning) show('#tab-dot-timer'); else hide('#tab-dot-timer'); }
        if (tb) {
            if (state.timerRemain > 0) {
                var m = Math.floor(state.timerRemain / 60), s = state.timerRemain % 60;
                tb.textContent = (m < 10 ? '0' + m : m) + ':' + (s < 10 ? '0' + s : s);
                show('#tab-badge-timer');
            } else hide('#tab-badge-timer');
        }

        var online = qs('#glance-online strong'); if (online) online.textContent = state.onlineCount;
        var openT = qs('#glance-tasks strong'); if (openT) openT.textContent = state.taskOpen;
        var overT = qs('#glance-overdue strong'); if (overT) overT.textContent = state.taskOverdue;
        var openP = qs('#glance-polls strong'); if (openP) openP.textContent = state.pollOpen;

        var gTimer = qs('#glance-timer strong');
        if (gTimer) {
            gTimer.textContent = state.timerRunning
                ? ((Math.floor(state.timerRemain / 60)).toString().padStart(2, '0') + ':' + (state.timerRemain % 60).toString().padStart(2, '0'))
                : 'stopped';
        }
    }

    function showSection(key) {
        var map = { chat: '#section-chat', tasks: '#section-tasks', polls: '#section-polls', timer: '#section-timer' };
        Object.keys(map).forEach(function (k) {
            var el = qs(map[k]); if (!el) return;
            if (k === key) el.classList.remove('d-none'); else el.classList.add('d-none');
        });
        qsa('#roomTabs .nav-link').forEach(function (btn) {
            var isActive = btn.getAttribute('data-section') === key;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-current', isActive ? 'page' : 'false');
        });

        if (key === 'chat') { state.chatUnread = 0; updateBadges(); }

        activeTab = key;
        localStorage.setItem(storageKey, activeTab);

        // Notify others (e.g., timer sync)
        try { window.dispatchEvent(new CustomEvent('tab-changed', { detail: { key: key } })); } catch (_) { }
    }

    function initTabs() {
        var tabs = qsa('#roomTabs .nav-link');
        if (!tabs.length) return;
        tabs.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var key = btn.getAttribute('data-section') || 'chat';
                showSection(key);
            });
        });

        showSection(activeTab || 'chat');
        updateBadges();
    }

    // Stats events
    window.addEventListener('chat-message', function () {
        if (activeTab !== 'chat') { state.chatUnread++; updateBadges(); }
    });
    window.addEventListener('presence-changed', function (e) {
        var c = (e && e.detail && e.detail.count) || 0;
        state.onlineCount = c; updateBadges();
    });
    window.addEventListener('tasks-stats', function (e) {
        var d = e && e.detail || {};
        state.taskOpen = d.open || 0;
        state.taskOverdue = d.overdue || 0;
        updateBadges();
    });
    window.addEventListener('polls-stats', function (e) {
        var d = e && e.detail || {};
        state.pollOpen = d.open || 0;
        updateBadges();
    });
    window.addEventListener('timer-status', function (e) {
        var d = e && e.detail || {};
        state.timerRunning = !!d.running;
        state.timerRemain = Math.max(0, parseInt(d.seconds || 0, 10) || 0);
        updateBadges();
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initTabs);
    else initTabs();
})();