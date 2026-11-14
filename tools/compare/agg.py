"""Per-capture figures for the write-up, cached as res/<prefix>.json.
Usage: agg.py <prefix> <span Hz>
Per source and Refine step: floor spread, clarity, peak; lag against the raw ACC Main
rows; frame rate. Per capture: how strong the strongest steady signal is in the
long-term mean (so clarity can be flagged where nothing is there), and the
detection rates from detect.py.
"""
import sys,json,subprocess,os,numpy as np
from an import sources, ema, metrics
pre=sys.argv[1]; span=sys.argv[2]; src=sources(pre)
out={'prefix':pre,'span':float(span)}
def series(F,t):
    s=F.mean(1); s=s-s.mean(); return s,t
def lag(a,b):                      # how far b runs behind a, seconds; and peak correlation
    (sa,ta),(sb,tb)=a,b; g=np.arange(max(ta[0],tb[0])+1,min(ta[-1],tb[-1])-1,0.005)
    x=np.interp(g,ta,sa); y=np.interp(g,tb,sb); x=(x-x.mean())/(x.std()+1e-12); y=(y-y.mean())/(y.std()+1e-12)
    L=200; c=np.array([np.mean(x[L:len(x)-L]*y[L+l:len(y)-L+l]) for l in range(-L,L+1)])
    i=int(np.argmax(c)); return (i-L)*0.005, float(c[i])
F0,t0=src['ACC Main']; ref=series(F0,t0)
steps={'ACC Main':[('R0',1),('R1',.6),('R3',.3),('R5',.12)],'ACC Sub':[('R0',1),('R3',.3)],'RSP1B':[('',None)],'RSP1':[('',None)]}
for n,(F,t) in src.items():
    for lab,w in steps[n]:
        G=F if w in (None,1) else ema(F,w); m=metrics(G); l,c=lag(ref,series(G,t))
        out[(n+' '+lab).strip()]=dict(spread=float(m['spread']),clarity=float(m['clarity']),peak=float(m['peak']),
            lag=l,corr=c,fps=float(len(t)/(t[-1]-t[0])),pts=int(F.shape[1]))
# strongest steady signal: long-term mean, dB above its median, on each SDR
for n in ('RSP1B','RSP1','ACC Main'):
    lt=src[n][0].mean(0); out['ltpeak_'+n]=float(lt.max()-np.median(lt))
d=subprocess.run([sys.executable,'detect.py',pre,span],capture_output=True,text=True)
try: out['detect']=json.loads(d.stdout.strip().splitlines()[-1])
except Exception: out['detect']=None; out['detect_err']=d.stderr[-300:]
json.dump(out,open(os.path.join(os.environ.get('RES','../res'),pre+'.json'),'w'))
print(pre,'ok')
