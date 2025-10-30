(function(){
'use strict';
var PREF_KEY = 'theme'; // 'auto' | 'light' | 'dark'
var prefers = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)');

function computeTheme(mode){
if (mode === 'dark' || mode === 'light') return mode;
return prefers && prefers.matches ? 'dark' : 'light';
}

function titleCase(s){ return s ? s.charAt(0).toUpperCase() + s.slice(1) : s; }

function updateUI(mode){
var label = document.getElementById('themeLabel');
if (label) label.textContent = titleCase(mode);
document.querySelectorAll('[data-theme-value]').forEach(function(btn){
btn.classList.toggle('active', btn.getAttribute('data-theme-value') === mode);
});
}

function apply(mode, save){
var theme = computeTheme(mode);
document.documentElement.setAttribute('data-bs-theme', theme);
if (save) localStorage.setItem(PREF_KEY, mode);
updateUI(mode);
}

function init(){
var mode = localStorage.getItem(PREF_KEY) || 'auto';
apply(mode, false);
document.querySelectorAll('[data-theme-value]').forEach(function(btn){
  btn.addEventListener('click', function(){
    var val = btn.getAttribute('data-theme-value');
    apply(val, true);
  });
});

if (prefers) {
  // Re-apply when OS theme changes and mode is auto
  var onChange = function(){
    var saved = localStorage.getItem(PREF_KEY) || 'auto';
    if (saved === 'auto') apply('auto', false);
  };
  if (prefers.addEventListener) prefers.addEventListener('change', onChange);
  else if (prefers.addListener) prefers.addListener(onChange);
}
}

if (document.readyState === 'loading') {
document.addEventListener('DOMContentLoaded', init);
} else {
init();
}
})();
