namespace Harness.MirrorView;

/// <summary>
/// The one page. Static HTML with its CSS and JS inline — no build step, no CDN, nothing to install.
///
/// <para><b>Every number on it comes from <c>/api/mirror</c>.</b> The markup carries no values and no
/// map: a page with a fallback table baked into it would render something plausible when the API was
/// refusing, which is the exact failure this whole lane exists to prevent.</para>
///
/// <para><b>The age counts up in the browser between fetches</b>, from the server's own
/// <c>ageSeconds</c> plus the local elapsed time since that response arrived. Local elapsed time, never
/// the browser's wall clock against the server's — a client whose clock is minutes out would otherwise
/// render a healthy reading as ancient or a dead one as fresh.</para>
///
/// <para><b>A failed FETCH is its own red state.</b> If the browser cannot reach the viewer, the numbers
/// on screen are frozen — and they must not go on looking like a live view of a controller because the
/// page happens to still be open.</para>
/// </summary>
public static class MirrorPage
{
    public const string ContentType = "text/html; charset=utf-8";

    public static string Html => Content;

    private const string Content = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Modbus mirror — live register view</title>
<style>
  :root { color-scheme: light dark; }
  body { font-family: Consolas, "Cascadia Mono", ui-monospace, monospace; margin: 0; padding: 0 1rem 3rem;
         background: #12141a; color: #dfe3ea; }
  h1 { font-size: 1.15rem; margin: 1rem 0 0.25rem; }
  a { color: #8ab4f8; }
  .sub { color: #98a0ad; font-size: 0.8rem; margin: 0 0 0.75rem; }

  #banner { padding: 0.7rem 0.9rem; border-radius: 6px; font-weight: bold; margin: 0.5rem 0;
            border-left: 8px solid #666; background: #1c1f27; }
  #banner .why { display: block; font-weight: normal; margin-top: 0.35rem; color: #c3c9d4; }
  .live      { border-left-color: #35b073 !important; background: #10261c !important; color: #7ff0b4; }
  .stale     { border-left-color: #d2a017 !important; background: #2a2410 !important; color: #ffd873; }
  .bad       { border-left-color: #d64545 !important; background: #2b1414 !important; color: #ff9c9c; }
  .unknown   { border-left-color: #6b7280 !important; background: #1c1f27 !important; color: #b6bcc7; }

  .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 0.6rem; margin: 0.6rem 0 1rem; }
  .card { background: #1a1d25; border: 1px solid #2b303b; border-radius: 6px; padding: 0.55rem 0.7rem; }
  .card .k { color: #98a0ad; font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.04em; }
  .card .v { font-size: 1.05rem; margin-top: 0.15rem; word-break: break-all; }
  .card .n { color: #98a0ad; font-size: 0.72rem; margin-top: 0.3rem; }

  table { border-collapse: collapse; width: 100%; font-size: 0.78rem; }
  th, td { border-bottom: 1px solid #262a34; padding: 0.28rem 0.45rem; text-align: left; vertical-align: top; }
  th { position: sticky; top: 0; background: #12141a; color: #98a0ad; font-weight: normal;
       text-transform: uppercase; font-size: 0.68rem; letter-spacing: 0.04em; }
  td.idx { text-align: right; color: #98a0ad; }
  td.raw { color: #f0c674; white-space: nowrap; }
  td.val { font-weight: bold; }
  td.cmt { color: #98a0ad; font-size: 0.72rem; }
  tr.continuation td { color: #6f7683; }
  tr.unmapped td { color: #7c5a5a; font-style: italic; }
  .true  { color: #7ff0b4; }
  .false { color: #8b93a1; }

  /* *** THE STALE TREATMENT. Values are greyed and struck so they cannot be mistaken for a reading
     taken now, and the header row says so on every screen-width. *** */
  #values.notcurrent td.val, #values.notcurrent td.raw { opacity: 0.32; text-decoration: line-through; }
  #values.notcurrent { filter: grayscale(1); }
  .stamp { color: #98a0ad; font-size: 0.72rem; margin: 0.4rem 0 1rem; }
  footer { margin-top: 2rem; color: #98a0ad; font-size: 0.74rem; border-top: 1px solid #262a34; padding-top: 0.8rem; }
  footer b { color: #c3c9d4; }
</style>
</head>
<body>

<h1>Modbus mirror — live register view</h1>
<p class="sub" id="subtitle">connecting…</p>

<div id="banner" class="unknown">STARTING<span class="why">no response from the viewer yet.</span></div>

<div class="grid">
  <div class="card"><div class="k">data age</div><div class="v" id="age">—</div><div class="n" id="ageNote"></div></div>
  <div class="card"><div class="k">scan counter</div><div class="v" id="scan">—</div><div class="n" id="scanNote"></div></div>
  <div class="card"><div class="k">build stamp</div><div class="v" id="stamp">—</div><div class="n">static — it names the program, it cannot say the program is running.</div></div>
  <div class="card"><div class="k">last attempt</div><div class="v" id="attempt">—</div><div class="n" id="attemptNote"></div></div>
</div>

<p class="stamp" id="mapline"></p>

<table>
  <thead>
    <tr>
      <th>reg</th><th>%M address</th><th>tag</th><th>type</th>
      <th>raw</th><th id="valueHead">decoded</th><th>comment / meaning</th>
    </tr>
  </thead>
  <tbody id="values"><tr><td colspan="7">waiting for the first response…</td></tr></tbody>
</table>

<footer>
  <p><b>What this shows.</b> The holding-register mirror the PLC publishes over Modbus, read whole in one
  FC03 per poll. The map — names, addresses, types, comments — is parsed from the committed IR artifacts
  named above; there is no table built into this program.</p>
  <p><b>What it deliberately does NOT show.</b> It is not a debugger and not an online-monitoring tool.
  It cannot see any tag that is not in the mirror, it cannot see DB contents, it cannot step, force,
  breakpoint or write anything at all, and it says nothing about the block logic that produced these
  values. A register it does not name is a register the map does not name.</p>
  <p><b>Raw beside every decode, always.</b> A decode that hides its bytes hides a wrong assumption.
  32-bit values are reassembled HIGH-WORD-FIRST — measured on this rig, not assumed.</p>
</footer>

<script>
"use strict";

var lastPayload = null;
var lastLocalMs = 0;

function el(id) { return document.getElementById(id); }

function setBanner(cls, headline, why) {
  var b = el("banner");
  b.className = cls;
  b.innerHTML = "";
  b.appendChild(document.createTextNode(headline));
  var span = document.createElement("span");
  span.className = "why";
  span.textContent = why;
  b.appendChild(span);
}

function bannerClassFor(status) {
  if (status === "Live") return "live";
  if (status === "Stale") return "stale";
  if (status === "NeverRead") return "unknown";
  return "bad";
}

function render(d) {
  el("subtitle").textContent =
    d.target + "  ·  " + d.declaredRegisters + " registers from %M" + d.baseByte +
    "  ·  polled every " + d.pollIntervalSeconds.toFixed(1) + " s  ·  stale after " +
    d.staleAfterSeconds.toFixed(1) + " s";

  el("mapline").textContent =
    "map: " + d.mapSource + "   ·   area pointer: " + d.areaSource + "   ·   allowlist: " + d.allowlistPath;

  setBanner(bannerClassFor(d.status), d.status.toUpperCase(), d.statusText);

  el("scan").textContent = (d.scan.value === null || d.scan.value === undefined) ? "not read" :
    (d.scan.value + (d.scan.advancing === true ? "  ▲" : d.scan.advancing === false ? "  ■" : "  ?"));
  el("scanNote").textContent = d.scan.note;

  el("stamp").textContent = d.buildStamp || "not read";

  el("attempt").textContent = d.lastAttemptOutcome + " (" + d.lastAttemptElapsedMs + " ms)";
  el("attemptNote").textContent = d.lastAttemptDetail;

  var body = el("values");
  body.className = d.valuesAreCurrent ? "" : "notcurrent";
  el("valueHead").textContent = d.valuesAreCurrent ? "decoded" : "decoded — NOT CURRENT";

  var html = "";
  for (var i = 0; i < d.rows.length; i++) {
    var r = d.rows[i];
    var cls = r.role === "continuation" ? "continuation" : (r.role === "unmapped" ? "unmapped" : "");
    var valCls = "val";
    if (r.decoded === "TRUE") { valCls += " true"; }
    if (r.decoded === "FALSE") { valCls += " false"; }
    html += "<tr class='" + cls + "'>" +
      "<td class='idx'>" + r.index + "</td>" +
      "<td>" + esc(r.mAddress) + "</td>" +
      "<td>" + esc(r.tag || "—") + "</td>" +
      "<td>" + esc(r.typeName || "—") + "</td>" +
      "<td class='raw'>" + esc(r.raw || "—") + "</td>" +
      "<td class='" + valCls + "' title='" + esc(r.basis || "") + "'>" + esc(r.decoded || "—") + "</td>" +
      "<td class='cmt'>" + esc(r.comment || "") + "</td></tr>";
  }
  body.innerHTML = html;
}

function esc(s) {
  return String(s).replace(/[&<>"']/g, function (c) {
    return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
  });
}

/* The age ticks locally from the server's own figure. Local elapsed time, never a wall-clock
   subtraction across two machines — a skewed browser clock must not be able to make a dead reading
   look fresh or a fresh one look ancient. */
function tickAge() {
  if (!lastPayload) { return; }
  var d = lastPayload;
  if (d.ageSeconds === null || d.ageSeconds === undefined) {
    el("age").textContent = "never read";
    el("ageNote").textContent = "no poll has ever produced a reading.";
    return;
  }
  var local = (Date.now() - lastLocalMs) / 1000;
  var shown = d.ageSeconds + local;
  el("age").textContent = shown.toFixed(1) + " s ago";
  el("ageNote").textContent = "observed " + d.observedUtc + " (UTC). Stale after " +
    d.staleAfterSeconds.toFixed(1) + " s.";

  /* If the page itself has gone past the window since the last response, do not wait for a fetch to
     say so — the numbers are already not current. */
  if (shown > d.staleAfterSeconds && d.status === "Live") {
    setBanner("stale", "STALE",
      "the last response said LIVE, but " + shown.toFixed(1) + " s have passed in this browser since it " +
      "arrived, past the " + d.staleAfterSeconds.toFixed(1) + " s window. These numbers are not now.");
    el("values").className = "notcurrent";
    el("valueHead").textContent = "decoded — NOT CURRENT";
  }
}

function poll() {
  fetch("/api/mirror", { cache: "no-store" })
    .then(function (r) {
      if (!r.ok) { throw new Error("HTTP " + r.status); }
      return r.json();
    })
    .then(function (d) {
      lastPayload = d;
      lastLocalMs = Date.now();
      render(d);
      tickAge();
    })
    .catch(function (e) {
      /* THE VIEWER ITSELF IS UNREACHABLE. Everything on screen is frozen and must say so. */
      setBanner("bad", "CANNOT REACH THE VIEWER",
        "the browser could not fetch /api/mirror (" + e.message + "). Every value below is frozen at " +
        "whatever it was when the last response arrived, and none of it is current.");
      el("values").className = "notcurrent";
      el("valueHead").textContent = "decoded — NOT CURRENT";
    });
}

poll();
setInterval(poll, 1000);
setInterval(tickAge, 200);
</script>
</body>
</html>
""";
}
