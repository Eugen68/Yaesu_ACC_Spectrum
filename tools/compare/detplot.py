"""Detection against span, one panel per band, from agg.py results.
Usage: detplot.py <res folder> <run prefix> <out.png>"""
import sys,json,glob,os,numpy as np, matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
res,runs,out=sys.argv[1:4]; MIN=20
R={}
for f in glob.glob(os.path.join(res,runs+'[123]_*.json')):
    d=json.load(open(f)); _,b,sp=d['prefix'].split('_')
    if d.get('detect'): R.setdefault((b,int(sp)),[]).append(d['detect'])
SP=['1k','2k','5k','10k','20k','50k','100k','200k','500k','1M']
S=[('ACC Main raw','ACC raw','#eda100','v'),('ACC Main R3','ACC Refine 3','#2a78d6','o'),
   ('ACC Main R5','ACC Refine 5','#1baf7a','s'),('RSP1B','RSP1B','#eb6834','D')]
B=[('160','160 m'),('bc','31 m broadcast'),('20','20 m'),('10','10 m')]
fig,axs=plt.subplots(1,4,figsize=(13,4.2),sharey=True,facecolor='#fcfcfb')
for ax,(b,name) in zip(axs,B):
    ax.set_facecolor('#fcfcfb')
    for k,lab,c,mk in S:
        xs,ys=[],[]
        for s in range(10):
            L=R.get((b,s),[])
            if sum(x.get('signals_Sub',0) for x in L)<MIN: continue
            n=sum(x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)
            if not n: continue
            xs.append(s); ys.append(100*sum(x[k]['pd']*x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)/n)
        ax.plot(xs,ys,color=c,lw=2,marker=mk,ms=6,mec='#fcfcfb',mew=1.2,label=lab)
        if b=='20' and xs: ax.annotate(lab,(xs[-1],ys[-1]),xytext=(6,0),textcoords='offset points',va='center',fontsize=8,color='#52514e')
    ax.set_title(name,loc='left',fontsize=10,color='#0b0b0b')
    ax.set_xticks(range(10)); ax.set_xticklabels(SP,fontsize=7.5,rotation=45); ax.set_xlim(-0.5,9.5)
    ax.set_ylim(30,101); ax.grid(axis='y',color='#e4e3df',lw=0.6); ax.set_axisbelow(True)
    for s_ in ('top','right'): ax.spines[s_].set_visible(False)
    for s_ in ('left','bottom'): ax.spines[s_].set_color('#b8b7b2')
    ax.tick_params(colors='#52514e',labelsize=8)
axs[0].set_ylabel('Weak signals shown, %',fontsize=9,color='#52514e')
axs[2].set_xlim(-0.5,11.2)
fig.legend(*axs[0].get_legend_handles_labels(),loc='upper right',ncol=4,frameon=False,fontsize=8.5)
fig.suptitle('Weak signals shown at 5% false alarms, by span and band',x=0.01,ha='left',fontsize=11.5,color='#0b0b0b')
fig.text(0.01,0.005,'Pooled over three 30 s runs, 2026-11-14. Spans with fewer than 20 signals are left out (10 m had closed below 100 kHz).',fontsize=7.5,color='#52514e')
fig.tight_layout(rect=(0,0.03,1,0.93)); fig.savefig(out,dpi=110,facecolor='#fcfcfb')
