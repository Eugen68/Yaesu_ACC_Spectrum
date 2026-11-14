"""Lag from attenuator steps. toggle.py switched Main's ATT between 0 and 18 dB every
1.5 s; ACC Main and the RSP1B (on Main's IF) both see each step at the same instant.
For each source: the 5% of points that change most, averaged, folded over every
switch (falls flipped so all are rises), normalised 0..1. Reported: time from the
switch command to 10%, 50% and 90%, in ms, at each Refine step.
Usage: steplag.py <prefix>  -> JSON
"""
import sys,os,json,numpy as np
from an import sources, ema
from load import DATA
pre=sys.argv[1]; src=sources(pre,skip=0.5); t0=float(open(f'{DATA}/{pre}-t0.txt').read())
log=json.load(open(f'{DATA}/{pre}-log.json')); sw=[(a-t0,v) for a,b,v in log[:-1]]
DW=float(os.environ.get("DWELL","1.5")); G=np.arange(-0.3,DW-0.3,0.005); out={}
for n,lst in (('ACC Main',[('R0',1),('R1',.6),('R3',.3),('R5',.12)]),('RSP1B',[('',None)])):
    F,t=src[n]
    for lab,w in lst:
        H=F if w in (None,1) else ema(F,w)
        st=np.array([v for _,v in sw]); 
        # state at each frame: last switch value before it
        idx=np.searchsorted([s for s,_ in sw],t)-1; ok=idx>=0
        on=np.array([sw[i][1]!='00' if i>=0 else False for i in idx])
        late=np.array([t[j]-sw[idx[j]][0]>DW*0.6 if idx[j]>=0 else False for j in range(len(t))])
        d=H[ok&late&~on].mean(0)-H[ok&late&on].mean(0); k=max(3,int(.05*H.shape[1]))
        p=np.argsort(d)[-k:]; s=H[:,p].mean(1)
        curves=[]
        for s0,v in sw:
            y=np.interp(s0+G,t,s,left=np.nan,right=np.nan)
            if np.isnan(y).any(): continue
            curves.append(-y if v!='00' else y)   # going to 18 dB = fall; flip so all rise
        c=np.mean(curves,0); lo=c[G<0].mean(); hi=c[(G>DW*0.55)].mean(); c=(c-lo)/(hi-lo)
        def tx(f):
            i=np.where((G>0)&(c>=f))[0]; return round(float(G[i[0]])*1000) if len(i) else None
        out[(n+' '+lab).strip()]=dict(t10=tx(.1),t50=tx(.5),t90=tx(.9),steps=len(curves),dB=float(d[p].mean()))
print(json.dumps(out))
