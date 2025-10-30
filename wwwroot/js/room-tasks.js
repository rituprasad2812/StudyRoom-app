(function () {
  'use strict';
  console.log('room-tasks.js loaded');

  var cfg = window.StudyRoom || {};
  if (typeof cfg.isMember === 'string') cfg.isMember = cfg.isMember.toLowerCase() === 'true';
  if (!cfg.roomId) { console.error('No roomId for tasks'); return; }
  if (!window.signalR) { console.error('SignalR not loaded'); return; }

  var roomId = String(cfg.roomId);
  var currentUserId = String(cfg.userId || '');
  var ownerId = String(cfg.ownerId || '');

  function byId(id) { return document.getElementById(id); }

  var tasksConn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tasks").withAutomaticReconnect().configureLogging(signalR.LogLevel.Information).build();

  var allTasks = []; // keep in-memory for counts

  function emitStats() {
    var open = 0, overdue = 0, now = new Date();
    for (var i = 0; i < allTasks.length; i++) {
      var t = allTasks[i];
      var status = String(t.status || t.Status || '').toLowerCase();
      if (status !== 'done') {
        open++;
        var due = t.dueAt || t.DueAt;
        if (due) {
          var d = new Date(due);
          if (!isNaN(d) && d < now) overdue++;
        }
      }
    }
    try { window.dispatchEvent(new CustomEvent('tasks-stats', { detail: { open: open, overdue: overdue } })); } catch (_) { }
  }

  tasksConn.on("TaskCreated", function (t) { upsertTask(t, true); });
  tasksConn.on("TaskUpdated", function (t) { upsertTask(t, true); });
  tasksConn.on("TaskDeleted", function (payload) {
    var id = payload && (payload.id || payload.Id);
    if (!id) return;
    allTasks = allTasks.filter(function (x) { return String(x.id || x.Id) !== String(id); });
    var el = document.querySelector('[data-task-id="' + id + '"]');
    if (el && el.parentElement) el.parentElement.removeChild(el);
    emitStats();
  });

  tasksConn.start()
    .then(function () { return tasksConn.invoke("JoinRoom", roomId); })
    .then(function () { console.log("Tasks connected"); return loadTasks(); })
    .catch(function (err) { console.error("Tasks start failed", err); });

  function loadTasks() {
    var url = '/Rooms/' + encodeURIComponent(roomId) + '/Tasks';
    return fetch(url, { credentials: 'same-origin' })
      .then(function (res) { if (!res.ok) throw new Error('Tasks list failed'); return res.json(); })
      .then(function (data) {
        clearLists();
        allTasks = (data.items || []);
        for (var i = 0; i < allTasks.length; i++) upsertTask(allTasks[i], false);
        emitStats();
      })
      .catch(function (e) { console.error('Tasks list error', e); });
  }

  function clearLists() {
    ['todo', 'doing', 'done'].forEach(function (k) {
      var c = byId('taskList-' + k); if (c) c.innerHTML = '';
    });
  }
  function canEdit(t) {
    var createdBy = String(t.createdBy || t.CreatedBy || '');
    return currentUserId && (currentUserId === createdBy || currentUserId === ownerId);
  }
  function renderDue(dueAt) {
    if (!dueAt) return '';
    var d = new Date(dueAt);
    if (isNaN(d)) return '';
    return d.toLocaleDateString();
  }
  function makeTaskElement(t) {
    var id = t.id || t.Id;
    var title = t.title || t.Title || '';
    var status = String(t.status || t.Status || 'todo').toLowerCase();
    var dueTxt = renderDue(t.dueAt || t.DueAt);
    var card = document.createElement('div');
    card.className = 'task-item card card-body py-2 px-3';
    card.setAttribute('data-task-id', id);

    var top = document.createElement('div');
    top.className = 'd-flex align-items-start justify-content-between';

    var left = document.createElement('div');
    var h = document.createElement('div'); h.textContent = title;
    var small = document.createElement('div'); small.className = 'small text-muted';
    small.textContent = dueTxt ? ('Due: ' + dueTxt) : '';
    left.appendChild(h); left.appendChild(small);

    var right = document.createElement('div'); right.className = 'd-flex align-items-center gap-2';

    var sel = document.createElement('select');
    sel.className = 'form-select form-select-sm';
    ['todo', 'doing', 'done'].forEach(function (opt) {
      var o = document.createElement('option');
      o.value = opt; o.text = opt.charAt(0).toUpperCase() + opt.slice(1);
      if (opt === status) o.selected = true;
      sel.appendChild(o);
    });
    sel.disabled = !canEdit(t);
    sel.addEventListener('change', function () { changeStatus(id, sel.value); });

    var del = document.createElement('button');
    del.type = 'button'; del.className = 'btn btn-sm btn-outline-danger'; del.textContent = 'Delete';
    del.disabled = !canEdit(t);
    del.addEventListener('click', function () { deleteTask(id); });

    right.appendChild(sel); right.appendChild(del);

    top.appendChild(left); top.appendChild(right);
    card.appendChild(top);

    return card;
  }
  function placeTaskEl(el, status) {
    var c = byId('taskList-' + status); if (!c) return; c.appendChild(el);
  }
  function upsertTask(t, updateArray) {
    if (updateArray) {
      var id = t.id || t.Id;
      var found = false;
      for (var i = 0; i < allTasks.length; i++) {
        var tid = allTasks[i].id || allTasks[i].Id;
        if (String(tid) === String(id)) { allTasks[i] = t; found = true; break; }
      }
      if (!found) allTasks.push(t);
    }
    var id2 = t.id || t.Id;
    var status = String(t.status || t.Status || 'todo').toLowerCase();
    var old = document.querySelector('[data-task-id="' + id2 + '"]');
    if (old && old.parentElement) old.parentElement.removeChild(old);
    var el = makeTaskElement(t);
    placeTaskEl(el, status);
    emitStats();
  }

  function changeStatus(id, status) {
    var fd = new FormData(); fd.append('status', status);
    fetch('/Rooms/' + encodeURIComponent(roomId) + '/Tasks/' + encodeURIComponent(id) + '/status',
      { method: 'POST', body: fd, credentials: 'same-origin' })
      .then(function (res) { if (!res.ok) throw new Error('status change failed'); })
      .then(function () { window.showToast && window.showToast('Task updated', ''); })
      .catch(function (e) { console.error(e); window.showToast && window.showToast('Update failed', ''); });
  }

  function deleteTask(id) {
    fetch('/Rooms/' + encodeURIComponent(roomId) + '/Tasks/' + encodeURIComponent(id) + '/delete',
      { method: 'POST', credentials: 'same-origin' })
      .then(function (res) { if (!res.ok) throw new Error('delete failed'); })
      .then(function () {
        window.showToast && window.showToast('Task deleted', '');
        allTasks = allTasks.filter(function (x) { return String(x.id || x.Id) !== String(id); });
        var el = document.querySelector('[data-task-id="' + id + '"]');
        if (el && el.parentElement) el.parentElement.removeChild(el);
        emitStats();
      })
      .catch(function (e) { console.error(e); window.showToast && window.showToast('Delete failed', ''); });
  }

  // Add handler for Add button
  var addBtn = byId('taskAddBtn');
  if (addBtn) addBtn.addEventListener('click', function () {
    var titleEl = byId('taskTitle');
    var dueEl = byId('taskDue');
    var title = (titleEl && titleEl.value || '').trim();
    var due = (dueEl && dueEl.value || '').trim();
    if (!title) return;
    var fd = new FormData(); fd.append('title', title); if (due) fd.append('dueDate', due);
    fetch('/Rooms/' + encodeURIComponent(roomId) + '/Tasks', { method: 'POST', body: fd, credentials: 'same-origin' })
      .then(function (res) { if (!res.ok) throw new Error('create failed'); })
      .then(function () {
        if (titleEl) titleEl.value = ''; if (dueEl) dueEl.value = '';
        window.showToast && window.showToast('Task added', '');
      })
      .catch(function (e) { console.error(e); window.showToast && window.showToast('Add failed', ''); });
  });
})();
