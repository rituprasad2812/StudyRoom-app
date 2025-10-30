(function () {
'use strict';
console.log('room-polls.js loaded');

function byId(id) { return document.getElementById(id); }

var cfg = window.StudyRoom || {};
var isMember = (typeof cfg.isMember === 'string') ? (cfg.isMember.toLowerCase() === 'true') : !!cfg.isMember;
if (!cfg.roomId) { console.error('No roomId'); return; }
if (!window.signalR) { console.error('SignalR not loaded'); return; }

var roomId = String(cfg.roomId);
var userId = String(cfg.userId || '');

var pollsConn = new signalR.HubConnectionBuilder()
.withUrl("/hubs/polls").withAutomaticReconnect().configureLogging(signalR.LogLevel.Information).build();

function emitStatsFromList(arr) {
var now = new Date();
var open = 0;
for (var i = 0; i < (arr || []).length; i++) {
var p = arr[i];
var isClosed = !!(p.isClosed || p.IsClosed);
var exp = p.expiresAt || p.ExpiresAt;
var future = true;
if (exp) { var d = new Date(exp); future = !isNaN(d) ? (d > now) : true; }
if (!isClosed && future) open++;
}
try { window.dispatchEvent(new CustomEvent('polls-stats', { detail: { open: open } })); } catch (_) {}
}

pollsConn.on("PollCreated", function () { loadPolls(); });
pollsConn.on("PollVoted", function () { loadPolls(); });
pollsConn.on("PollClosed", function () { loadPolls(); });
pollsConn.on("PollDeleted", function () { removeDeleted(); loadPolls(); });

function removeDeleted() { /* UI removal handled in room-polls.js earlier; safe no-op here */ }

pollsConn.start()
.then(function () { return pollsConn.invoke("JoinRoom", roomId); })
.then(function () { console.log("Polls connected"); return loadPolls(); })
.catch(function (e) { console.error("Polls start failed", e); });

function loadPolls() {
var url = '/Rooms/' + encodeURIComponent(roomId) + '/Polls?_=' + Date.now();
return fetch(url, { credentials: 'same-origin', cache: 'no-store' })
.then(function (res) { if (!res.ok && res.status !== 403) throw new Error('list failed (' + res.status + ')'); return res.ok ? res.json() : { polls: [] }; })
.then(function (data) {
var list = byId('pollsList'); if (!list) return;
list.innerHTML = '';
var arr = data && data.polls ? data.polls : [];
for (var i = 0; i < arr.length; i++) list.appendChild(makePollEl(arr[i]));
emitStatsFromList(arr);
})
.catch(function (e) { console.error('loadPolls error', e); });
}

function makePollEl(p) {
var id = p.id || p.Id;
var wrap = document.createElement('div');
wrap.className = 'card border';
wrap.setAttribute('data-poll-id', id);
var body = document.createElement('div'); body.className = 'card-body';
var q = document.createElement('div'); q.className = 'fw-semibold mb-1'; q.textContent = p.question || p.Question || '';
body.appendChild(q);

var by = document.createElement('div'); by.className = 'small text-muted mb-1';
var creatorName = p.creatorName || p.CreatorName || '';
if (creatorName) by.textContent = 'by ' + creatorName;
body.appendChild(by);

var meta = document.createElement('div'); meta.className = 'small text-muted mb-2';
var isClosed = !!(p.isClosed || p.IsClosed);
var exp = p.expiresAt || p.ExpiresAt;
meta.textContent = isClosed ? 'Closed' : (exp ? ('Closes: ' + new Date(exp).toLocaleString()) : '');
body.appendChild(meta);

var opts = p.options || p.Options || [];
var total = 0; for (var i = 0; i < opts.length; i++) total += Number(opts[i].count || opts[i].Count || 0);

var ul = document.createElement('div'); ul.className = 'vstack gap-2 mb-2';
for (var j = 0; j < opts.length; j++) {
  var o = opts[j];
  var item = document.createElement('div');

  var line = document.createElement('div'); line.className = 'd-flex justify-content-between';
  var left = document.createElement('div'); left.className = 'form-check d-flex align-items-center';

  var radio = document.createElement('input');
  radio.type = 'radio'; radio.name = 'poll_' + id; radio.value = o.id || o.Id; radio.className = 'form-check-input me-2';
  radio.disabled = isClosed || !isMember;
  radio.addEventListener('change', function () { if (this.checked) submitVote(id, this.value); });

  var lab = document.createElement('label'); lab.className = 'form-check-label'; lab.textContent = o.text || o.Text || '';
  left.appendChild(radio); left.appendChild(lab);

  var cnt = Number(o.count || o.Count || 0);
  var right = document.createElement('div'); right.className = 'small text-muted'; right.textContent = String(cnt);
  line.appendChild(left); line.appendChild(right);

  var prog = document.createElement('div'); prog.className = 'progress';
  var bar = document.createElement('div'); bar.className = 'progress-bar';
  bar.style.width = total > 0 ? (Math.round(cnt * 100 / total) + '%') : '0%';
  bar.setAttribute('aria-valuenow', String(cnt));
  bar.setAttribute('aria-valuemin', '0');
  bar.setAttribute('aria-valuemax', String(total));
  prog.appendChild(bar);

  item.appendChild(line); item.appendChild(prog); ul.appendChild(item);
}
body.appendChild(ul);

// Close button for creator handled in your existing controller; optional here

wrap.appendChild(body);
return wrap;
}

function submitVote(pollId, optionId) {
if (!isMember) { window.showToast && window.showToast('Join the room to vote', ''); return; }
var fd = new FormData(); fd.append('optionId', optionId);
fetch('/Rooms/' + encodeURIComponent(roomId) + '/Polls/' + encodeURIComponent(pollId) + '/vote',
{ method: 'POST', body: fd, credentials: 'same-origin' })
.then(function (res) { if (!res.ok) return res.text().then(function (t) { throw new Error((t || 'vote failed') + ' (' + res.status + ')'); }); })
.then(function () { window.showToast && window.showToast('Vote recorded', ''); return loadPolls(); })
.catch(function (e) { console.error(e); window.showToast && window.showToast('Vote failed', e.message || ''); });
}
})();