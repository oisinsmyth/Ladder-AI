"""Verify the contact sheet is a working page: every embedded image must actually decode.

A data URI that is truncated or mis-encoded renders as a broken-image box, and a page of 366 broken
boxes would look exactly like "the library is bad" - the wrong conclusion from a build error.
"""
import base64, collections, io, re, struct

PAGE = r"C:\Users\<user>\Desktop\AI Ladder Project\hmi\wincc-graphics-contact-sheet.html"

if __name__ == "__main__":
    html = io.open(PAGE, encoding="utf-8").read()
    print("page bytes: %d" % len(html.encode("utf-8")))

    imgs = re.findall(r'src="data:image/png;base64,([^"]+)"', html)
    print("embedded images: %d" % len(imgs))

    bad = 0
    dims = []
    for i, b64 in enumerate(imgs):
        try:
            raw = base64.b64decode(b64)
        except Exception as e:
            print("  [%d] BASE64 FAILED: %s" % (i, e))
            bad += 1
            continue
        if not raw.startswith(b"\x89PNG\r\n\x1a\n"):
            print("  [%d] NOT A PNG (starts %r)" % (i, raw[:8]))
            bad += 1
            continue
        w, h = struct.unpack(">II", raw[16:24])
        dims.append((w, h))
        if w == 0 or h == 0:
            print("  [%d] ZERO DIMENSION" % i)
            bad += 1

    print("decoded OK: %d   BAD: %d" % (len(imgs) - bad, bad))
    if dims:
        ws = sorted(d[0] for d in dims)
        hs = sorted(d[1] for d in dims)
        print("thumbnail px  width min/med/max = %d/%d/%d   height = %d/%d/%d"
              % (ws[0], ws[len(ws) // 2], ws[-1], hs[0], hs[len(hs) // 2], hs[-1]))

    print()
    print("structure:")
    for tag in ("<title>", "<style>", "<script>", 'class="grid"', 'class="cell"',
                'data-size="96"', "<table>"):
        print("  %-18s %d" % (tag, html.count(tag)))
    print("  <h2> sections    %d" % html.count("<h2>"))

    print()
    print("no-project-data check (must all be zero):")
    for term in ("JOB9003", "K150", "Ulster", "Shredder", "PusherArm", "EStop", "ap20"):
        n = html.count(term)
        print("  %-12s %d%s" % (term, n, "   <-- LEAK" if n else ""))
