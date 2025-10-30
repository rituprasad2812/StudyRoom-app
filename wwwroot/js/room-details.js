(function () {
  'use strict';
  console.log('room-details.js loaded');

  function byId(id) { return document.getElementById(id); }
  function pad2(n) { return n < 10 ? ('0' + n) : String(n); }
  function renderTime(sec) { sec = sec | 0; return pad2(Math.floor(sec / 60)) + ':' + pad2(sec % 60); }

  var cfg = window.StudyRoom || {};
  var isMember = (typeof cfg.isMember === 'string') ? (cfg.isMember.toLowerCase() === 'true') : !!cfg.isMember;
  if (!isMember && byId('sendBtn')) { isMember = true; }

  if (!window.signalR) { console.error('SignalR not loaded'); return; }

  var roomId = (cfg.roomId || '').toString().trim();
  if (roomId.startsWith('{') && roomId.endsWith('}')) roomId = roomId.slice(1, -1);
  if (!roomId) { console.error('Missing roomId'); return; }
  var currentUserId = (cfg.userId || '').toString();

  var chatConn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat").withAutomaticReconnect().configureLogging(signalR.LogLevel.Information).build();
  var timerConn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/timer").withAutomaticReconnect().configureLogging(signalR.LogLevel.Information).build();

  // Helper to check connection state safely (works with SignalR v3+)
  function isChatConnected() {
    try {
      if (!chatConn) return false;
      // HubConnectionState object might exist on signalR; fallback to string
      if (signalR && signalR.HubConnectionState) return chatConn.state === signalR.HubConnectionState.Connected;
      return String(chatConn.state).toLowerCase() === 'connected';
    } catch (e) {
      return false;
    }
  }

  // Chat bubbles
  function normalizeMessage(m) {
    var created = m && (m.createdAt || m.CreatedAt);
    var createdAt = created ? new Date(created) : new Date();
    return {
      id: (m && (m.id || m.Id)) || null,
      userId: (m && (m.userId || m.UserId)) || '',
      displayName: (m && (m.displayName || m.DisplayName)) || '',
      content: (m && (m.content || m.Content)) || '',
      createdAt: createdAt
    };
  }

  function makeBubble(nm) {
    var mine = String(nm.userId) === String(currentUserId);
    var wrap = document.createElement('div');
    wrap.className = 'msg ' + (mine ? 'my' : 'other');
    var author = document.createElement('div');
    author.className = 'fw-semibold small';
    author.textContent = nm.displayName ? nm.displayName : (nm.userId ? nm.userId.substring(0, 6) : 'User');

    var text = document.createElement('div');
    text.textContent = nm.content || '';

    var meta = document.createElement('div');
    meta.className = 'meta';
    try { meta.textContent = nm.createdAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); } catch (_) { meta.textContent = ''; }

    wrap.appendChild(author);
    wrap.appendChild(text);
    wrap.appendChild(meta);
    return wrap;
  }

  function appendMessage(m) {
    var nm = normalizeMessage(m);
    var div = byId('chatMessages'); if (!div) return;
    div.appendChild(makeBubble(nm));
    div.scrollTop = div.scrollHeight;
  }

  function prependMessages(arr) {
    var div = byId('chatMessages'); if (!div) return;
    var top = div.firstChild, frag = document.createDocumentFragment();
    for (var i = 0; i < (arr || []).length; i++) frag.appendChild(makeBubble(normalizeMessage(arr[i])));
    div.insertBefore(frag, top);
  }

  // Online users
  function renderOnline(list) {
    list = list || [];
    var names = [];
    for (var i = 0; i < list.length; i++) {
      var u = list[i];
      var dn = (u && (u.displayName || u.DisplayName)) || '';
      var uid = (u && (u.userId || u.UserId)) || '';
      names.push(dn || (uid ? uid.substring(0, 6) : 'User'));
    }
    var el = byId("onlineUsers");
    if (el) el.textContent = names.length ? ("Online: " + names.join(", ")) : "Online: (none)";
    try { window.dispatchEvent(new CustomEvent('presence-changed', { detail: { count: names.length } })); } catch (_) { }
  }

  // History
  if (Array.isArray(cfg.history) && cfg.history.length) { prependMessages(cfg.history); }

  // Chat events
  chatConn.on("MessageReceived", function (m) {
    appendMessage(m);
    try { window.dispatchEvent(new CustomEvent('chat-message', { detail: {} })); } catch (_) { }
  });
  chatConn.on("OnlineUsers", function (l) { renderOnline(l); });
  chatConn.on("UserJoined", function () { chatConn.invoke("GetOnlineUsers", roomId).then(renderOnline).catch(function () { }); });
  chatConn.on("UserLeft", function () { chatConn.invoke("GetOnlineUsers", roomId).then(renderOnline).catch(function () { }); });

  // Timer logic
  var lastState = null, tickHandle = null, lastStartSeconds = 0;
  function updateTimerUI(s) {
    if (!s) return;
    lastState = s;
    var sec = (s.secondsRemaining != null) ? s.secondsRemaining : (s.SecondsRemaining || 0);
    var phase = s.phase || s.Phase || "idle";
    var running = (s.running != null ? s.running : s.Running) || false;
    var disp = byId("timerDisplay"); if (disp) disp.textContent = renderTime(sec);
    var pb = byId("phaseBadge"); if (pb) pb.textContent = phase;
    var sb = byId("statusBadge"); if (sb) sb.textContent = running ? "running" : "paused";

    try { window.dispatchEvent(new CustomEvent('timer-status', { detail: { running: running, seconds: sec } })); } catch (_) { }
  }

  function startLocalTick() {
    if (tickHandle) clearInterval(tickHandle);
    tickHandle = setInterval(function () {
      if (!lastState) return;
      var running = (lastState.running != null ? lastState.running : lastState.Running) || false;
      if (!running) return;
      var sec = (lastState.secondsRemaining != null ? lastState.secondsRemaining : lastState.SecondsRemaining) - 1;
      if (sec < 0) sec = 0;
      lastState.secondsRemaining = sec; lastState.SecondsRemaining = sec;
      var disp = byId("timerDisplay"); if (disp) disp.textContent = renderTime(sec);
      try { window.dispatchEvent(new CustomEvent('timer-status', { detail: { running: running, seconds: sec } })); } catch (_) { }
      if (sec === 0) { clearInterval(tickHandle); tickHandle = null; }
    }, 1000);
  }

  // Hub events for timer
  timerConn.on("TimerUpdated", function (s) { updateTimerUI(s); startLocalTick(); });
  timerConn.on("TimerEnded", function (s) {
    updateTimerUI(s);
    if (tickHandle) { clearInterval(tickHandle); tickHandle = null; }
    window.showToast && window.showToast("Timer ended", "Phase: " + (s.phase || s.Phase || ""));
    var phase = s.phase || s.Phase;
    var total = (s.totalSeconds || s.TotalSeconds) || lastStartSeconds || 0;
    if (phase === "focus" && total > 0) { timerConn.invoke("LogFocusSession", roomId, total).catch(function () { }); }
  });

  // Start hubs
  chatConn.start()
    .then(function () { return chatConn.invoke("JoinRoom", roomId); })
    .then(function () { return chatConn.invoke("GetOnlineUsers", roomId); })
    .then(function (l) { renderOnline(l || []); console.log("Chat connected"); })
    .catch(function (e) { console.error("Chat start failed", e); });

  timerConn.start()
    .then(function () { return timerConn.invoke("JoinRoom", roomId); })
    .then(function () { return timerConn.invoke("Sync", roomId); })
    .then(function (s) { updateTimerUI(s); console.log("Timer connected"); })
    .catch(function (e) { console.error("Timer start failed", e); });

  // Buttons - timer controls
  var startBtn = byId("startBtn");
  if (startBtn) startBtn.addEventListener("click", function (ev) {
    ev && ev.preventDefault && ev.preventDefault();
    if (!isMember) { window.showToast && window.showToast("Join the room to control timer", ""); return; }
    var miEl = byId("minutesInput");
    var mins = parseInt(miEl && miEl.value ? miEl.value : "25", 10);
    if (isNaN(mins) || mins < 1) mins = 1;
    var seconds = mins * 60;
    var phaseRadio = document.querySelector('input[name="phase"]:checked');
    var phase = (phaseRadio && phaseRadio.value) || "focus";
    lastStartSeconds = seconds;
    timerConn.invoke("Start", roomId, seconds, phase)
      .then(function () { console.log("Timer start ok", seconds, phase); })
      .catch(function (e) { console.error("Start failed", e); window.showToast && window.showToast("Start failed", ""); });
  });

  var pauseBtn = byId("pauseBtn");
  if (pauseBtn) pauseBtn.addEventListener("click", function (ev) {
    ev && ev.preventDefault && ev.preventDefault();
    if (!isMember) { window.showToast && window.showToast("Join the room to control timer", ""); return; }
    timerConn.invoke("Pause", roomId)
      .then(function () { console.log("Timer pause ok"); })
      .catch(function (e) { console.error("Pause failed", e); window.showToast && window.showToast("Pause failed", ""); });
  });

  var resumeBtn = byId("resumeBtn");
  if (resumeBtn) resumeBtn.addEventListener("click", function (ev) {
    ev && ev.preventDefault && ev.preventDefault();
    if (!isMember) { window.showToast && window.showToast("Join the room to control timer", ""); return; }
    timerConn.invoke("Resume", roomId)
      .then(function () { console.log("Timer resume ok"); })
      .catch(function (e) { console.error("Resume failed", e); window.showToast && window.showToast("Resume failed", ""); });
  });

  // ✅ Send button + Enter key handling (robust)
  var sendBtn = byId("sendBtn");
  var messageInput = byId("messageInput");

  function doSendMessage() {
    try {
      if (!messageInput) { console.error("messageInput element not found (expected id='messageInput')"); return; }
      var raw = messageInput.value || '';
      var text = raw.trim();
      if (!text) { /* nothing to send */ return; }

      if (!isChatConnected()) {
        console.error("Chat connection not ready. state=", chatConn && chatConn.state);
        window.showToast && window.showToast("Not connected", "Try again in a moment");
        return;
      }

      // invoke SendMessage on hub
      chatConn.invoke("SendMessage", roomId, text)
        .then(function () {
          console.log("Message sent:", text);
          // clear input only after send success
          messageInput.value = "";
          // optionally append local message immediately (since server will broadcast it back too)
          // appendMessage({ userId: currentUserId, displayName: null, content: text, createdAt: new Date() });
        })
        .catch(function (err) {
          console.error("SendMessage failed", err);
          window.showToast && window.showToast("Message failed to send", "");
        });
    } catch (e) {
      console.error("Error in doSendMessage", e);
    }
  }

  if (sendBtn) {
    sendBtn.addEventListener("click", function (ev) {
      ev && ev.preventDefault && ev.preventDefault(); // prevents form submit if inside a <form>
      doSendMessage();
    });
  } else {
    console.warn("sendBtn not found (expected id='sendBtn')");
  }

  // Enter to send (inside the input) — prevents form submit as well
  if (messageInput) {
    messageInput.addEventListener("keydown", function (ev) {
      if (!ev) ev = window.event;
      if (ev.key === 'Enter' || ev.keyCode === 13) {
        ev.preventDefault && ev.preventDefault();
        doSendMessage();
      }
    });
  }

  // Sync when switching to Timer tab
  window.addEventListener('tab-changed', function (e) {
    var key = e && e.detail && e.detail.key;
    if (key === 'timer') {
      timerConn.invoke("Sync", roomId)
        .then(function (s) { updateTimerUI(s); console.log("Timer synced on tab switch"); })
        .catch(function (err) { console.error("Sync failed", err); });
    }
  });

  // Extra: log when connection closes (helps debug)
  chatConn.onclose(function (err) {
    console.warn('Chat connection closed', err);
    window.showToast && window.showToast('Chat disconnected', '');
  });

})();
