/*
 * Background music for invitations — shared across every template.
 *
 * Behaviour (per spec):
 *   • A small elegant icon is fixed in the bottom-left corner and stays visible while scrolling.
 *   • Clicking the icon ONLY toggles mute / unmute. It never pauses, restarts or replays.
 *   • Music autoplays when the browser allows it; otherwise it starts on the first interaction.
 *   • The icon always shows a clear muted / unmuted state.
 *   • Loops continuously.
 *
 * Sources: an uploaded audio file (URL, streamed by the API) OR a YouTube link (audio only —
 * the video surface is hidden and controls are stripped so it behaves like background music).
 *
 * Usage:  InvitationMusic.init(data.music, window.API_BASE)
 *   data.music = { enabled, url, autoplay }
 */
(function () {
  'use strict';

  function parseYouTubeId(url) {
    if (!url) return null;
    var s = String(url).trim();
    var m = s.match(/(?:youtu\.be\/|youtube\.com\/(?:watch\?v=|embed\/|shorts\/|v\/))([\w-]{11})/);
    if (m) return m[1];
    return /^[\w-]{11}$/.test(s) ? s : null;   // bare id
  }

  function resolveUrl(url, apiBase) {
    if (/^https?:\/\//i.test(url)) return url;
    var base = (apiBase || '').replace(/\/$/, '');
    return base + url;   // API-relative (e.g. /api/public/media/<id>)
  }

  // Inline SVGs so there are no extra network requests.
  var ICON_ON =
    '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
    '<path d="M11 5 6 9H3v6h3l5 4V5z"/><path d="M15.5 8.5a5 5 0 0 1 0 7"/><path d="M18.5 5.5a9 9 0 0 1 0 13"/></svg>';
  var ICON_OFF =
    '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
    '<path d="M11 5 6 9H3v6h3l5 4V5z"/><line x1="22" y1="9" x2="16" y2="15"/><line x1="16" y1="9" x2="22" y2="15"/></svg>';

  function injectStyles() {
    if (document.getElementById('inv-music-style')) return;
    var css =
      '.inv-music-btn{position:fixed;left:16px;bottom:16px;z-index:9999;width:46px;height:46px;' +
      'border-radius:50%;display:flex;align-items:center;justify-content:center;cursor:pointer;' +
      'border:1px solid rgba(255,255,255,.35);background:rgba(20,18,16,.55);color:#fff;' +
      'backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px);box-shadow:0 4px 16px rgba(0,0,0,.35);' +
      'transition:transform .2s ease,opacity .2s ease,background .2s ease;padding:0;' +
      'padding-bottom:env(safe-area-inset-bottom,0)}' +
      '.inv-music-btn:hover{transform:scale(1.06)}' +
      '.inv-music-btn:active{transform:scale(.94)}' +
      '.inv-music-btn.is-muted{opacity:.72}' +
      '.inv-music-btn .inv-music-pulse{position:absolute;inset:-1px;border-radius:50%;border:1px solid rgba(255,255,255,.35);' +
      'animation:invMusicPulse 2.4s ease-out infinite;opacity:0}' +
      '.inv-music-btn.is-playing .inv-music-pulse{opacity:1}' +
      '@keyframes invMusicPulse{0%{transform:scale(1);opacity:.5}100%{transform:scale(1.5);opacity:0}}' +
      '@media (prefers-reduced-motion: reduce){.inv-music-btn .inv-music-pulse{animation:none}}';
    var st = document.createElement('style');
    st.id = 'inv-music-style';
    st.textContent = css;
    document.head.appendChild(st);
  }

  function init(music, apiBase) {
    if (!music || music.enabled === false || !music.url) return;
    if (document.querySelector('.inv-music-btn')) return;   // guard against double init

    injectStyles();

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'inv-music-btn is-muted';
    btn.setAttribute('aria-label', 'Toggle background music');
    btn.setAttribute('aria-pressed', 'false');
    var pulse = document.createElement('span');
    pulse.className = 'inv-music-pulse';
    btn.appendChild(pulse);
    var iconWrap = document.createElement('span');
    iconWrap.style.display = 'flex';
    iconWrap.innerHTML = ICON_OFF;
    btn.appendChild(iconWrap);
    document.body.appendChild(btn);

    var ytId = parseYouTubeId(music.url);
    var engine = ytId ? youTubeEngine(ytId) : audioEngine(resolveUrl(music.url, apiBase));

    var started = false;   // has playback actually begun?
    function render() {
      var muted = engine.isMuted();
      iconWrap.innerHTML = muted ? ICON_OFF : ICON_ON;
      btn.classList.toggle('is-muted', muted);
      btn.classList.toggle('is-playing', started && !muted);
      btn.setAttribute('aria-pressed', String(!muted));
    }

    // Try to autoplay WITH sound. If the browser blocks it, arm a one-time
    // interaction listener that begins playback — without ever restarting it.
    function tryStart(unmuted) {
      engine.start(unmuted).then(function (ok) {
        started = started || ok;
        render();
      });
    }
    function armFirstInteraction() {
      var events = ['pointerdown', 'touchstart', 'keydown', 'scroll'];
      function onFirst() {
        events.forEach(function (e) { window.removeEventListener(e, onFirst, true); });
        if (!started) tryStart(true);
      }
      events.forEach(function (e) { window.addEventListener(e, onFirst, { capture: true, passive: true, once: true }); });
    }

    engine.onReady(function () {
      engine.start(true).then(function (ok) {
        if (ok) { started = true; render(); }
        else { armFirstInteraction(); render(); }
      });
    });

    // The icon toggles mute only. The very first click also doubles as the
    // interaction that starts blocked playback (still unmuted, never restarted).
    btn.addEventListener('click', function () {
      if (!started) { tryStart(true); return; }
      engine.setMuted(!engine.isMuted());
      render();
    });

    render();
  }

  // ── Uploaded-audio engine ───────────────────────────────────────────────
  function audioEngine(src) {
    var audio = new Audio();
    audio.src = src;
    audio.loop = true;
    audio.preload = 'auto';
    audio.muted = false;
    var readyCbs = [];
    // Audio is usable almost immediately; fire "ready" on next tick.
    setTimeout(function () { readyCbs.forEach(function (cb) { cb(); }); }, 0);
    return {
      onReady: function (cb) { readyCbs.push(cb); },
      start: function (unmuted) {
        audio.muted = !unmuted;
        var p = audio.play();
        if (!p || !p.then) return Promise.resolve(true);
        return p.then(function () { return true; }).catch(function () { return false; });
      },
      isMuted: function () { return audio.muted || audio.paused; },
      setMuted: function (m) { audio.muted = m; if (!m && audio.paused) audio.play().catch(function () {}); }
    };
  }

  // ── YouTube-as-audio engine (hidden surface, no controls, looped) ────────
  function youTubeEngine(videoId) {
    var yt = null, ready = false, readyCbs = [], pendingMuted = false;

    var holder = document.createElement('div');
    holder.style.cssText = 'position:fixed;width:1px;height:1px;left:-9999px;top:-9999px;opacity:0;pointer-events:none;';
    var target = document.createElement('div');
    holder.appendChild(target);
    document.body.appendChild(holder);

    function boot() {
      yt = new YT.Player(target, {
        videoId: videoId,
        playerVars: {
          autoplay: 0, controls: 0, disablekb: 1, fs: 0, modestbranding: 1,
          rel: 0, playsinline: 1, loop: 1, playlist: videoId
        },
        events: {
          onReady: function () { ready = true; readyCbs.forEach(function (cb) { cb(); }); },
          onStateChange: function (e) { if (e.data === YT.PlayerState.ENDED) yt.playVideo(); } // belt-and-braces loop
        }
      });
    }

    // Load the IFrame API once.
    if (window.YT && window.YT.Player) {
      boot();
    } else {
      var prev = window.onYouTubeIframeAPIReady;
      window.onYouTubeIframeAPIReady = function () { if (prev) prev(); boot(); };
      if (!document.getElementById('inv-yt-api')) {
        var s = document.createElement('script');
        s.id = 'inv-yt-api';
        s.src = 'https://www.youtube.com/iframe_api';
        document.head.appendChild(s);
      }
    }

    return {
      onReady: function (cb) { ready ? cb() : readyCbs.push(cb); },
      start: function (unmuted) {
        if (!yt) return Promise.resolve(false);
        try {
          if (unmuted) { yt.unMute(); pendingMuted = false; } else { yt.mute(); pendingMuted = true; }
          yt.playVideo();
          // YT gives no reliable play promise; assume success when unmuted autoplay is user-driven.
          return Promise.resolve(true);
        } catch (e) { return Promise.resolve(false); }
      },
      isMuted: function () { try { return yt ? yt.isMuted() : pendingMuted; } catch (e) { return pendingMuted; } },
      setMuted: function (m) { try { m ? yt.mute() : yt.unMute(); } catch (e) {} pendingMuted = m; }
    };
  }

  window.InvitationMusic = { init: init, _parseYouTubeId: parseYouTubeId };
})();
