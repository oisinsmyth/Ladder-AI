import sys,os,shutil,subprocess,re,json
arm=sys.argv[1]; src=sys.argv[2]
work=os.path.join(r"C:\Users\User\.claude\jobs\0755f2fb\tmp","m_"+arm)
if os.path.isdir(work): shutil.rmtree(work)
shutil.copytree(src,work)
home=os.path.join(work,"home.html")
html=open(home,encoding="utf-8",errors="replace").read()
probe="""<script>window.addEventListener('load',function(){
var R=document.body.getBoundingClientRect(),o=[];
document.querySelectorAll('body *').forEach(function(el){
 var r=el.getBoundingClientRect();if(r.width<4||r.height<4)return;
 if(el.children.length>0 && el.textContent.trim().length===0 && !el.matches('button,a')) {}
 o.push({t:el.tagName,l:Math.round(r.left),y:Math.round(r.top),w:Math.round(r.width),h:Math.round(r.height),
  txt:(el.children.length===0)?1:0});});
var p=document.createElement('pre');p.id='GEO';p.style.display='none';
p.textContent=JSON.stringify(o);document.body.appendChild(p);});</script>"""
open(home,"w",encoding="utf-8").write(html.replace("</body>",probe+"</body>"))
out=subprocess.run([r"C:\Program Files\Google\Chrome\Application\chrome.exe","--headless","--disable-gpu",
 "--window-size=1920,1080","--force-device-scale-factor=1","--virtual-time-budget=4000","--dump-dom",
 "file:///"+home.replace("\\","/")],capture_output=True,text=True,encoding="utf-8",errors="replace")
m=re.search(r'<pre id="GEO"[^>]*>(.*?)</pre>',out.stdout,re.S)
if not m: print(arm,"MEASURE FAILED"); sys.exit()
d=json.loads(m.group(1).replace('&quot;','"'))
leaves=[x for x in d if x['txt']==1]
off=[x for x in d if x['l']%8 or x['y']%8]
oc=[x for x in d if x['l']<0 or x['y']<0 or x['l']+x['w']>1920 or x['y']+x['h']>1080]
small=[x for x in d if x['t'] in ('BUTTON','A') and (x['w']<44 or x['h']<44)]
# vertical coverage: find largest empty horizontal band
rows=[0]*1080
for x in d:
    if x['txt']==1:
        for yy in range(max(0,x['y']),min(1080,x['y']+x['h'])): rows[yy]=1
gap=cur=0
for v in rows:
    cur=0 if v else cur+1
    gap=max(gap,cur)
print("%-8s elements=%-4d leaves=%-4d off8grid=%-4d(%2d%%) offcanvas=%-3d smalltargets=%-3d largest_vertical_gap=%dpx"
 %(arm,len(d),len(leaves),len(off),100*len(off)//max(1,len(d)),len(oc),len(small),gap))
