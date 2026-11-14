# scale.py <prefix> <span Hz>: is each SDR's frequency axis right? Fits the SDR's
# labelled axis to the ACC's (which is right by construction) as f_true = s*f + o,
# on peak-only versions of the long-term spectra (running median removed).
import sys,numpy as np
from an import sources
pre,span=sys.argv[1],float(sys.argv[2]); sspan=float(sys.argv[3]) if len(sys.argv)>3 else span; src=sources(pre)
def peaks(F,k):
    from numpy.lib.stride_tricks import sliding_window_view as sw
    pad=np.pad(F,k//2,mode='edge'); med=np.median(sw(pad,k),1)[:len(F)]
    return np.clip(F-med,0,None)
acc=src['ACC Main'][0].mean(0); fa=np.linspace(-span/2,span/2,len(acc)); A=peaks(acc,41)
for n in ('RSP1B','RSP1'):
    F=src[n][0].mean(0); f=np.linspace(-sspan/2,sspan/2,len(F)); P=peaks(F,max(5,int(41*len(F)/len(acc))|1))
    best=(-1,0,0)
    for s in np.arange(0.45,2.2,0.005):
        for o in np.linspace(-0.02*span,0.02*span,41):
            g=f*s+o; m=(g>-span/2)&(g<span/2)
            if m.sum()<len(F)*0.3*span/sspan: continue
            c=np.corrcoef(np.interp(g[m],fa,A),P[m])[0,1]
            if c>best[0]: best=(c,s,o)
    print(f'{n}: true span / labelled = {best[1]:.3f}, offset {best[2]:+.0f} Hz, match r={best[0]:.2f}')
