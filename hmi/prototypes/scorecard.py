import sys,os,shutil,subprocess,re,json,colorsys
CHROME=r"C:\Program Files\Google\Chrome\Application\chrome.exe"
PROBE=r"""<script>window.addEventListener('load',function(){
var o=[];document.querySelectorAll('body *').forEach(function(el){
 var r=el.getBoundingClientRect();if(r.width<2||r.height<2)return;var c=getComputedStyle(el);
 o.push({t:el.tagName,l:r.left,y:r.top,w:r.width,h:r.height,
 leaf:el.children.length===0?1:0,
 act:(el.matches('button,a,[role=button]')?1:0),
 bg:c.backgroundColor,fg:c.color,bi:c.backgroundImage,
 bs:c.boxShadow,ts:c.textShadow,br:c.borderRadius,
 an:c.animationName,tr:c.transitionProperty,bc:c.borderTopColor,bw:c.borderTopWidth});});
var p=document.createElement('pre');p.id='GEO';p.style.display='none';
p.textContent=JSON.stringify({body:getComputedStyle(document.body).backgroundColor,els:o});
document.body.appendChild(p);});</script>"""
def hsl(cs):
    m=re.match(r'rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)',cs or '')
    if not m: return None
    r,g,b=[float(m.group(i))/255 for i in (1,2,3)]; a=float(m.group(4) or 1)
    if a<0.05: return None
    h,l,s=colorsys.rgb_to_hls(r,g,b)
    return (h*360,s,l,a)
def run(arm,src):
    work=os.path.join(os.getcwd(),"w_"+arm)
    if os.path.isdir(work): shutil.rmtree(work)
    shutil.copytree(src,work); home=os.path.join(work,"home.html")
    h=open(home,encoding="utf-8",errors="replace").read()
    open(home,"w",encoding="utf-8").write(h.replace("</body>",PROBE+"</body>"))
    r=subprocess.run([CHROME,"--headless","--disable-gpu","--window-size=1280,800",
      "--force-device-scale-factor=1","--virtual-time-budget=4000","--dump-dom",
      "file:///"+home.replace("\\","/")],capture_output=True,text=True,encoding="utf-8",errors="replace")
    m=re.search(r'<pre id="GEO"[^>]*>(.*?)</pre>',r.stdout,re.S)
    if not m: return print(arm,"MEASURE FAILED")
    d=json.loads(m.group(1).replace('&quot;','"')); els=d['els']
    subpx=[e for e in els if any(abs(e[k]-round(e[k]))>0.01 for k in ('l','y','w','h'))]
    oc=[e for e in els if e['l']<-0.5 or e['y']<-0.5 or e['l']+e['w']>1280.5 or e['y']+e['h']>800.5]
    small=[e for e in els if e['act'] and min(e['w'],e['h'])<48]
    # alignment near-miss
    lefts={}
    for e in els: lefts.setdefault(round(e['l']),0); lefts[round(e['l'])]+=1
    clusters=[k for k,v in lefts.items() if v>=2]
    near=sum(1 for e in els if round(e['l']) not in clusters and any(0<abs(round(e['l'])-c)<=3 for c in clusters))
    # palette
    greens=set(); accents=set()
    for e in els:
        for key in ('bg','fg','bc'):
            if key=='bc' and e['bw'] in ('0px',''): continue
            v=hsl(e[key])
            if not v: continue
            hh,s,l,a=v
            if s<0.18 or l<0.05 or l>0.97: continue
            if 80<=hh<=165: greens.add(e[key])
            elif not (hh<=25 or hh>=335 or 25<hh<=55): accents.add(e[key])
    grad=[e for e in els if 'gradient' in (e['bi'] or '')]
    shad=[e for e in els if (e['bs'] not in ('none','')) or (e['ts'] not in ('none',''))]
    rad=[e for e in els if any(float(x[:-2])>2 for x in re.findall(r'[\d.]+px',e['br'] or '') )]
    anim=[e for e in els if e['an'] not in ('none','')]
    bodyhsl=hsl(d['body']); 
    h1 = bodyhsl and bodyhsl[1]<0.15 and 0.55<bodyhsl[2]<0.85
    # text-leaf overlap (Arm A's defect: colliding labels)
    tl=[e for e in els if e['leaf']]
    ov=0
    for i in range(len(tl)):
        for j in range(i+1,len(tl)):
            a,b=tl[i],tl[j]
            x=min(a['l']+a['w'],b['l']+b['w'])-max(a['l'],b['l'])
            y=min(a['y']+a['h'],b['y']+b['h'])-max(a['y'],b['y'])
            if x>2 and y>2: ov+=1
    rows=[0]*800
    for e in els:
        if e['leaf']:
            for yy in range(max(0,int(e['y'])),min(800,int(e['y']+e['h']))): rows[yy]=1
    gap=cur=0
    for v in rows:
        cur=0 if v else cur+1; gap=max(gap,cur)
    print("%-10s els=%-4d subpx=%-4d nearmiss=%-3d offcanvas=%-2d small=%-3d overlap=%-3d gap=%-4d | H1grey=%-3s H2green=%-2d H8accent=%-2d H4grad=%-2d H4shadow=%-3d H4radius=%-3d H5anim=%-2d"
      %(arm,len(els),len(subpx),near,len(oc),len(small),ov,gap,
        "OK" if h1 else "FAIL",len(greens),len(accents),len(grad),len(shad),len(rad),len(anim)))
for a in sys.argv[1:]:
    p=r"C:\Users\<user>\Desktop\AI Ladder Project\research\experiment2"+"\\"+a
    if os.path.isdir(p): run(a,p)
    else: print(a,"not ready")
