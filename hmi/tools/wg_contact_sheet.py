"""Build a self-contained contact sheet: every sampled symbol at placement size, labelled.

Images are embedded as data URIs so the page is one file with no external requests - it must stay
readable when moved, mailed or opened from anywhere.

Symbols are shown at 96 px, the middle of our real 48-130 px placement range, with a size toggle so
the owner can see them at 48 and 130 too. That is the whole point: a count cannot answer an
aesthetic question.
"""
import base64, collections, io, os

THUMBS = r"C:\Users\User\.claude\jobs\0755f2fb\tmp\wg_thumbs"
SRC = r"C:\Users\User\.claude\jobs\0755f2fb\tmp\wg_extract"
OUT = r"C:\Users\User\Desktop\AI Ladder Project\hmi\wincc-graphics-contact-sheet.html"

# Category -> (available in the full library, folders the sample came from)
AVAILABLE = {
    "TANKS___SILOS": ("Tanks & silos", 398),
    "PUMPS": ("Pumps", 251),
    "PIPES": ("Pipes", 332),
    "MOTORS": ("Motors", 106),
    "STATE_VARIANTS___Animate__folders_": ("State variants (\u2018Animate\u2019)", 86),
    "VALVES": ("Valves", 143),
    "CONVEYORS": ("Conveyors", 122),
    "HEAT_EXCHANGE___BOILERS": ("Heat exchange / boilers", 163),
    "INSTRUMENTATION__ISA_": ("Instrumentation (ISA)", 306),
    "SENSORS___FLOW_METERS": ("Sensors & flow meters", 110),
    "MIXERS___BLOWERS": ("Mixers & blowers", 150),
    "PROCESS_INDUSTRIES": ("Process industries", 297),
}

ORDER = ["TANKS___SILOS", "PUMPS", "PIPES", "MOTORS", "STATE_VARIANTS___Animate__folders_",
         "VALVES", "CONVEYORS", "HEAT_EXCHANGE___BOILERS", "INSTRUMENTATION__ISA_",
         "SENSORS___FLOW_METERS", "MIXERS___BLOWERS", "PROCESS_INDUSTRIES"]

CSS = """
:root{--bg:#f7f7f8;--fg:#16181d;--muted:#5c6270;--card:#fff;--line:#dfe1e6;--accent:#0b6ea8;
      --warn:#8a4b00;--warnbg:#fff4e5;--good:#0a6b3d;--goodbg:#e9f7ef;}
:root:not([data-theme="light"]){}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){
  --bg:#14161a;--fg:#e8eaee;--muted:#a3aab8;--card:#1d2026;--line:#2c313a;--accent:#57b6e8;
  --warn:#ffcf8f;--warnbg:#33240f;--good:#7ede9f;--goodbg:#10301f;}}
:root[data-theme="dark"]{--bg:#14161a;--fg:#e8eaee;--muted:#a3aab8;--card:#1d2026;--line:#2c313a;
  --accent:#57b6e8;--warn:#ffcf8f;--warnbg:#33240f;--good:#7ede9f;--goodbg:#10301f;}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);
     font:15px/1.55 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;}
.wrap{max-width:1180px;margin:0 auto;padding:32px 20px 80px}
h1{font-size:26px;margin:0 0 6px;letter-spacing:-.01em}
h2{font-size:18px;margin:38px 0 4px;padding-top:18px;border-top:1px solid var(--line)}
.sub{color:var(--muted);margin:0 0 26px}
.count{color:var(--muted);font-size:13px;margin:0 0 14px}
.panel{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:16px 18px;margin:18px 0}
.panel h3{margin:0 0 8px;font-size:15px}
.note{background:var(--warnbg);color:var(--warn);border:1px solid var(--line);
      border-radius:10px;padding:14px 16px;margin:16px 0}
.good{background:var(--goodbg);color:var(--good);border:1px solid var(--line);
      border-radius:10px;padding:14px 16px;margin:16px 0}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:14px}
.cell{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:10px 8px;
      text-align:center;overflow:hidden}
.shot{display:flex;align-items:center;justify-content:center;height:140px;
      background:repeating-conic-gradient(#e9e9ec 0 25%,#fdfdfe 0 50%) 50%/16px 16px;
      border-radius:6px;margin-bottom:8px}
.shot img{image-rendering:auto}
.nm{font-size:11px;color:var(--fg);word-break:break-word;line-height:1.3}
.mt{font-size:10px;color:var(--muted);margin-top:3px;font-variant-numeric:tabular-nums}
.tag{display:inline-block;font-size:10px;padding:1px 5px;border-radius:4px;border:1px solid var(--line);
     color:var(--muted);margin-top:3px}
.controls{position:sticky;top:0;background:var(--bg);padding:12px 0;border-bottom:1px solid var(--line);
          z-index:5;display:flex;gap:8px;align-items:center;flex-wrap:wrap}
button{font:inherit;font-size:13px;padding:5px 12px;border-radius:6px;border:1px solid var(--line);
       background:var(--card);color:var(--fg);cursor:pointer}
button[aria-pressed="true"]{background:var(--accent);color:#fff;border-color:var(--accent)}
table{border-collapse:collapse;width:100%;font-size:13px}
th,td{text-align:left;padding:6px 10px;border-bottom:1px solid var(--line)}
th{color:var(--muted);font-weight:600}
.scroll{overflow-x:auto}
code{background:var(--bg);padding:1px 5px;border-radius:4px;font-size:12px}
"""

JS = """
document.querySelectorAll('[data-size]').forEach(function(b){
  b.addEventListener('click', function(){
    var px = b.getAttribute('data-size');
    document.querySelectorAll('[data-size]').forEach(function(o){
      o.setAttribute('aria-pressed', o === b ? 'true' : 'false');
    });
    document.querySelectorAll('.shot img').forEach(function(img){
      img.style.maxWidth = px + 'px'; img.style.maxHeight = px + 'px';
    });
  });
});
"""


def load_sizes():
    native = {}
    tsv = os.path.join(THUMBS, "sizes.tsv")
    if os.path.isfile(tsv):
        for line in io.open(tsv, encoding="utf-8-sig"):
            p = line.rstrip("\n").split("\t")
            if len(p) >= 6:
                try:
                    native[p[0].replace("/", os.sep)] = (int(p[2]), int(p[3]), p[4], int(p[5]))
                except ValueError:
                    pass
    return native


def esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


if __name__ == "__main__":
    native = load_sizes()

    # A hardcoded key that does not match a real directory drops that whole category SILENTLY - it
    # already happened once here, and the category it swallowed (state variants) was the most
    # decision-relevant one on the page. Reconcile both directions before rendering anything.
    on_disk = {d for d in os.listdir(THUMBS) if os.path.isdir(os.path.join(THUMBS, d))}
    unknown = on_disk - set(ORDER)
    dangling = set(ORDER) - on_disk
    if unknown or dangling:
        raise SystemExit("CATEGORY MISMATCH - refusing to build a page that hides a category.\n"
                         "  on disk but not in ORDER: %s\n"
                         "  in ORDER but not on disk: %s" % (sorted(unknown), sorted(dangling)))
    expected = sum(len([f for f in os.listdir(os.path.join(THUMBS, d))
                        if f.lower().endswith(".png")]) for d in on_disk)

    out = []
    out.append("<title>WinCC Shipped Symbols</title>")
    out.append("<style>%s</style>" % CSS)
    out.append('<div class="wrap">')
    out.append("<h1>WinCC shipped graphics &mdash; contact sheet</h1>")
    out.append('<p class="sub">A visual audit of <code>Graphics_All.zip</code>, installed with '
               'TIA Portal V20 at <code>\\Portal V20\\Lib\\Graphics\\</code>. '
               '<strong>7,997 files</strong> across 180 folders. Shown here: a 366-symbol even-spread '
               'sample, rendered at the sizes we actually place things.</p>')

    out.append('<div class="good"><strong>The two formats are not equivalent, and this is the '
               'finding that matters.</strong> The <strong>3,008 WMF</strong> files are '
               '<strong>true vector</strong> &mdash; every one of the 205 sampled contained drawing '
               'geometry and no bitmap record, so they rescale cleanly to any placement size. The '
               '<strong>1,778 EMF</strong> files are the glossy 3-D looking set, and '
               '<strong>124 of 129 sampled carry an embedded bitmap</strong> (median native size '
               '87&times;87&nbsp;px, median 55&nbsp;KB, up to 1.9&nbsp;MB). Those are effectively '
               'fixed-size rasters: fine at 48&ndash;96&nbsp;px, soft above it.</div>')

    out.append('<div class="controls"><span style="color:var(--muted);font-size:13px">'
               'Display size:</span>'
               '<button data-size="48">48 px</button>'
               '<button data-size="96" aria-pressed="true">96 px</button>'
               '<button data-size="130">130 px</button>'
               '<span style="color:var(--muted);font-size:12px">'
               '&nbsp;our real placement range is 48&ndash;130 px</span></div>')

    total = 0
    for key in ORDER:
        d = os.path.join(THUMBS, key)
        if not os.path.isdir(d):
            continue
        label, avail = AVAILABLE.get(key, (key, 0))
        files = sorted(f for f in os.listdir(d) if f.lower().endswith(".png"))
        if not files:
            continue
        out.append("<h2>%s</h2>" % esc(label))
        out.append('<p class="count"><strong>%d</strong> available in the library &mdash; '
                   'showing %d, spread evenly across the source folders.</p>' % (avail, len(files)))
        out.append('<div class="grid">')
        for f in files:
            p = os.path.join(d, f)
            b64 = base64.b64encode(io.open(p, "rb").read()).decode("ascii")
            relkey = os.path.join(key, f)
            meta = native.get(relkey)
            if not meta:
                meta = native.get(os.path.splitext(relkey)[0] + ".svg")
            pretty = f.split("_", 1)[-1].rsplit(".", 1)[0]
            if meta and meta[0]:
                mt = "%d&times;%d native" % (meta[0], meta[1])
                fmt = meta[2].lstrip(".").upper()
            else:
                mt = "vector"
                fmt = "SVG"
            out.append('<div class="cell">'
                       '<div class="shot"><img src="data:image/png;base64,%s" '
                       'style="max-width:96px;max-height:96px" alt="%s"></div>'
                       '<div class="nm">%s</div>'
                       '<div class="mt">%s</div>'
                       '<span class="tag">%s</span>'
                       "</div>" % (b64, esc(pretty), esc(pretty), mt, fmt))
            total += 1
        out.append("</div>")

    out.append('<h2>Library structure &mdash; the full count, not the sample</h2>')
    out.append('<div class="scroll"><table><tr><th>Class</th><th>Files available</th>'
               '<th>Where</th></tr>')
    rows = [
        ("Tanks &amp; silos", 398, "Automation [EMF]/Tanks (+Parts, +Animate), Other [WMF]/Tanks (+Cutaways), [SVG]/Tanks"),
        ("Pipes", 393, "Automation [EMF]/Pipes (+Flange, +Animate), Other [WMF]/Pipes (+Segmented, +Misc), [SVG]/Pipes"),
        ("Pumps", 263, "Automation [EMF]/Pumps (+Animate), Other [WMF]/Pumps, [SVG]/Pumps"),
        ("Motors", 130, "Automation [EMF]/Motors (+Animate), Other [WMF]/Motors, [SVG]/Motors"),
        ("Valves", 149, "Automation [EMF]/Valves (+Animate), Other [WMF]/Valves, [SVG]/Valves"),
        ("Conveyors", 124, "Other [WMF]/Conveyors/Belt, /Simple, /Miscellaneous, [SVG]/Conveyors"),
        ("Instrumentation (ISA)", 306, "Technology/Standardized symbols [WMF]/ISA Symbols, ISA Symbols 3D"),
        ("Heat exchange / boilers", 163, "Automation [EMF]/Heating &amp; boilers, Other [WMF]/Boilers, /Heating, Industries/Process Cooling"),
        ("Mixers &amp; blowers", 150, "Automation [EMF]/Mixer, /Blowers, Other [WMF]/Mixers"),
        ("Sensors &amp; flow meters", 110, "Automation [EMF]/Sensor, Other [WMF]/Sensors, /Flow Meters"),
        ("State variants (&lsquo;Animate&rsquo;)", 86, "Automation [EMF]/*/Animate &mdash; running/stopped pairs"),
        ("Water &amp; wastewater", 112, "Equipment/Industries [WMF]/Water &amp; Wastewater"),
        ("Chemical", 50, "Equipment/Industries [WMF]/Chemical"),
        ("Mining", 63, "Equipment/Industries [WMF]/Mining"),
        ("Food", 72, "Equipment/Industries [WMF]/Food"),
    ]
    for name, n, where in rows:
        rows_html = "<tr><td>%s</td><td><strong>%d</strong></td><td style='color:var(--muted);font-size:12px'>%s</td></tr>"
        out.append(rows_html % (name, n, where))
    out.append("</table></div>")

    out.append('<div class="panel"><h3>What is NOT here</h3>'
               '<p style="color:var(--muted);margin:0">The <code>IndustryGraphicLibrary</code> under '
               '<code>Data\\Hmi\\SvgControls\\</code> (463 <code>.svghmi</code> files, 37 categories) '
               'is a <strong>different, WinCC&nbsp;Unified-only</strong> library: its files are SVG '
               'widgets carrying <code>hmi-bind:</code> parameter bindings, not plain pictures. '
               '<code>PTSymLib</code> (633 <code>.ctx</code>/<code>.cat</code> files) is the legacy '
               'ProTool <em>Symbol library</em> object &mdash; the one Siemens documents as '
               '<strong>not available on Basic Panels</strong>. Neither is the folder audited here.</p></div>')

    out.append('<p class="sub" style="margin-top:30px;font-size:12px">Rendered from the installed '
               'library via GDI+ (metafiles) and Inkscape (SVG). Checkerboard indicates transparency. '
               'No project data appears on this page.</p>')
    out.append("</div>")
    out.append("<script>%s</script>" % JS)

    html = "\n".join(out)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(html)
    if total != expected:
        raise SystemExit("EMBEDDED %d BUT %d THUMBNAILS EXIST - the page is incomplete." % (total, expected))
    print("symbols embedded: %d of %d thumbnails (reconciled)" % (total, expected))
    print("written: %s  (%.1f MB)" % (OUT, os.path.getsize(OUT) / 1024.0 / 1024.0))
