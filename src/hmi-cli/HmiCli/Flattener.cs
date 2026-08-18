using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HmiCli;

/// <summary>
/// T1 - resolve CSS layout to absolute integer rects by letting Chrome do the layout and reading it
/// off. Proven twice in the rev-7 research: 55 items with faceplate refs and bindings at 1920x1080,
/// and across four independent screens at 1280x800.
/// </summary>
public static class Flattener
{
    // The probe runs in the page, walks the DOM on load, and stashes JSON in a hidden <pre> which
    // --dump-dom then hands back. No automation framework, no MCP server, no dependency beyond the
    // Chrome already installed. That minimalism is the point - it is also what makes it portable.
    private const string ProbeScript = """
<script id="__hmi_probe">
window.addEventListener('load', function () {
  var root = document.body.getBoundingClientRect();
  var out = [];
  var all = document.querySelectorAll('body *');
  for (var i = 0; i < all.length; i++) {
    var el = all[i];
    if (el.id === '__hmi_probe' || el.tagName === 'SCRIPT' || el.tagName === 'STYLE') continue;
    var r = el.getBoundingClientRect();
    var c = getComputedStyle(el);
    if (c.display === 'none' || c.visibility === 'hidden') continue;
    var declared = el.getAttribute('data-hmi');
    var geometryless = el.hasAttribute('data-hmi-geometryless');
    if (!geometryless && r.width < 1 && r.height < 1) continue;
    var z = c.zIndex === 'auto' ? 0 : parseInt(c.zIndex, 10);
    out.push({
      index: out.length,
      tag: el.tagName,
      type: declared,
      left: r.left - root.left, top: r.top - root.top,
      width: r.width, height: r.height,
      zTier: isNaN(z) ? 0 : z,
      // A WRITABLE IOField IS INTERACTIVE EVEN WHEN IT IS AUTHORED AS A <div>.
      // Interactivity was derived from the HTML element alone, so an operator-writable setpoint
      // written as <div data-hmi="IOField" data-hmi-mode="Input"> escaped H-401's 9 mm touch floor
      // entirely - it is a touch target on the panel and was not one to the checker. What the PANEL
      // does decides this, not what the markup happens to be made of.
      // 🔴 A DECLARED BUTTON IS INTERACTIVE WHATEVER ELEMENT IT IS MADE OF.
      //
      // This keyed on the HTML element, so `<div data-hmi="Button">` - which is how every screen in
      // this project authors its buttons - was NOT interactive to the checker. H-401 (touch size),
      // H-404 (separation) and H-503 (overlap) therefore never ran on a single button.
      //
      // Measured on a real job: a screen reported "0 errors over 62 items" having examined NONE of
      // its own touch targets, and the navigation bar copied from it turned out to be 8 px apart
      // against a 3 mm floor. A green over the wrong denominator, in the checker whose whole purpose
      // is the physical-safety rules.
      //
      // What the PANEL does decides this, not what the markup is made of - the same correction
      // already applied to a writable IOField.
      interactive: !!(el.matches('button,a,input,select,textarea,[role=button]')
                      || el.hasAttribute('data-hmi-interactive')
                      || el.getAttribute('data-hmi') === 'Button'
                      || ['Input', 'InOutput'].includes(el.getAttribute('data-hmi-mode'))),
      leaf: el.children.length === 0,
      geometryless: geometryless,
      ignored: el.hasAttribute('data-hmi-ignore'),
      overrideSpec: el.getAttribute('data-hmi-override'),
      zone: el.getAttribute('data-hmi-zone'),
      accentRole: el.getAttribute('data-hmi-accent'),
      accentFor: el.getAttribute('data-hmi-accent-for'),
      elementId: el.id || null,
      lineDirection: el.getAttribute('data-hmi-line'),
      safetyCritical: el.hasAttribute('data-hmi-safety'),
      alarmFlash: el.hasAttribute('data-hmi-alarm'),
      bind: el.getAttribute('data-hmi-bind'),
      goto: el.getAttribute('data-hmi-goto'),
      mode: el.getAttribute('data-hmi-mode'),
      format: el.getAttribute('data-hmi-format'),
      unit: el.getAttribute('data-hmi-unit'),
      // 🔴 TEXT WAS SILENTLY TRUNCATED AT 80 CHARACTERS.
      //
      // `.slice(0, 80)` was an arbitrary cap, and it CUT REAL CAPTIONS MID-WORD with no warning
      // from check, from emit or from the coherence gate. Measured on a real job: a 90-character
      // safety statement reached the SimaticML as "...is a different number and is on the si" and
      // was found only because the author read the emitted XML.
      //
      // A truncated caption is worse than a refused one: it renders, it looks deliberate, and the
      // half that carried the warning is the half that went. Only LEAF elements contribute text
      // (see the ternary), so there was never a runaway-container risk for the cap to guard against.
      //
      // Full text is captured now, and `textTruncated` marks anything past a sane ceiling so emit
      // can refuse rather than quietly shorten it.
      text: el.children.length === 0 ? (el.textContent || '').trim().slice(0, 1000) : null,
      textTruncated: el.children.length === 0 && (el.textContent || '').trim().length > 1000,
      backColor: c.backgroundColor, foreColor: c.color, borderColor: c.borderTopColor,
      backgroundImage: c.backgroundImage, boxShadow: c.boxShadow, textShadow: c.textShadow,
      borderRadius: c.borderTopLeftRadius, animationName: c.animationName,
      fontVariantNumeric: c.fontVariantNumeric,
      fontSizePx: parseFloat(c.fontSize) || 0,
      fontWeight: parseInt(c.fontWeight, 10) || 400,
      fontStyleCss: c.fontStyle,
      textTransform: c.textTransform,
      letterSpacing: c.letterSpacing,
      fontFamilyCss: c.fontFamily,
      fontFamilyUsed: (function () {
        // The RESOLVED family, not the CSS stack. getComputedStyle returns the whole list, so the
        // first entry is only a request - measuring which face actually rendered needs a width
        // comparison against a known-absent family.
        var probe = document.createElement('span');
        probe.textContent = 'HMImmmiii';
        probe.style.cssText = 'position:absolute;visibility:hidden;white-space:pre;font-size:40px;';
        document.body.appendChild(probe);
        var fallbackWidth = {};
        ['monospace', 'serif', 'sans-serif'].forEach(function (g) {
          probe.style.fontFamily = g;
          fallbackWidth[g] = probe.offsetWidth;
        });
        var list = c.fontFamily.split(',').map(function (x) { return x.trim().replace(/^[\'\"]|[\'\"]$/g, ''); });
        var used = null;
        for (var k = 0; k < list.length && !used; k++) {
          var name = list[k];
          if (['monospace', 'serif', 'sans-serif', 'system-ui'].indexOf(name) >= 0) { used = name; break; }
          var hit = false;
          ['monospace', 'serif', 'sans-serif'].forEach(function (g) {
            probe.style.fontFamily = '"' + name + '",' + g;
            if (probe.offsetWidth !== fallbackWidth[g]) { hit = true; }
          });
          if (hit) { used = name; }
        }
        document.body.removeChild(probe);
        return used || list[list.length - 1] || '';
      })()
    });
  }
  var pre = document.createElement('pre');
  pre.id = 'HMI_IR';
  pre.style.display = 'none';
  pre.textContent = JSON.stringify(out);
  document.body.appendChild(pre);
});
</script>
""";

    private static readonly string[] ChromeCandidates =
    {
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    };

    public static string? FindBrowser(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.Exists(overridePath) ? overridePath : null;
        }

        return ChromeCandidates.FirstOrDefault(File.Exists);
    }

    public static ScreenIr Flatten(string htmlPath, Panel panel, string browserPath)
    {
        var work = Path.Combine(Path.GetTempPath(), "hmi-flatten-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);

        try
        {
            // Copy the whole source directory so relative CSS/image references still resolve. The
            // probe is injected into the COPY, never into the authored file - the research session
            // recorded a converter silently overwriting hand-authored input twice in one day.
            var srcDir = Path.GetDirectoryName(Path.GetFullPath(htmlPath))!;
            foreach (var file in Directory.GetFiles(srcDir))
            {
                File.Copy(file, Path.Combine(work, Path.GetFileName(file)), overwrite: true);
            }

            var target = Path.Combine(work, Path.GetFileName(htmlPath));
            var html = File.ReadAllText(target);
            html = html.Contains("</body>", StringComparison.OrdinalIgnoreCase)
                ? Regex.Replace(html, "</body>", ProbeScript + "</body>", RegexOptions.IgnoreCase)
                : html + ProbeScript;
            File.WriteAllText(target, html);

            var psi = new ProcessStartInfo(browserPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in new[]
                     {
                         "--headless", "--disable-gpu", "--no-sandbox",
                         $"--window-size={panel.WidthPx},{panel.HeightPx}",
                         "--force-device-scale-factor=1", "--virtual-time-budget=5000", "--dump-dom",
                         "file:///" + target.Replace('\\', '/'),
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("could not start the browser");
            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(60000);

            var m = Regex.Match(stdout, "<pre id=\"HMI_IR\"[^>]*>(.*?)</pre>", RegexOptions.Singleline);
            if (!m.Success)
            {
                throw new InvalidOperationException(
                    "the probe produced no IR - the page may not have loaded. NOT treated as an empty screen: "
                    + "empty is not clean, and a silent zero here would pass every downstream check.");
            }

            var json = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
            var items = JsonSerializer.Deserialize<List<IrItem>>(json) ?? new List<IrItem>();

            return new ScreenIr
            {
                Panel = panel.Name,
                Family = "Classic",
                CanvasWidth = panel.WidthPx,
                CanvasHeight = panel.HeightPx,
                Source = Path.GetFullPath(htmlPath),
                ChromeVersion = BrowserVersion(browserPath),
                Items = items,
            };
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // A locked temp file is not a reason to fail a completed flatten.
            }
        }
    }

    // Pinned and recorded: a Chrome version change moves the flattened integers, which makes it
    // build-breaking rather than cosmetic. Recording it in the IR is what lets a golden-test failure
    // be diagnosed instead of merely observed.
    private static string BrowserVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown";
        }
        catch (FileNotFoundException)
        {
            return "unknown";
        }
    }
}
