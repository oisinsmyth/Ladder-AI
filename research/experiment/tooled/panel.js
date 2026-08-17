/* Shared panel clock. Panel time runs from the shift time the alarm list was
   captured, so every timestamp on screen stays consistent with the others. */
(function () {
  var clock = document.getElementById('clock');
  var date  = document.getElementById('date');
  if (!clock || !date) { return; }

  var days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  var mons = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
              'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

  function pad(n) { return (n < 10 ? '0' : '') + n; }

  var t = new Date(2026, 7, 14, 14, 26, 41);

  function tick() {
    clock.textContent = pad(t.getHours()) + ':' + pad(t.getMinutes()) + ':' + pad(t.getSeconds());
    date.textContent  = days[t.getDay()] + ' ' + t.getDate() + ' ' +
                        mons[t.getMonth()] + ' ' + t.getFullYear();
    t = new Date(t.getTime() + 1000);
  }

  tick();
  setInterval(tick, 1000);
})();
