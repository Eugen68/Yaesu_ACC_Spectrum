"""Detection table with per-run ranges. Usage: dtab.py <res dir> <runs prefix> -> markdown"""
import sys,json,glob,os,numpy as np
R={}
for f in glob.glob(os.path.join(sys.argv[1],sys.argv[2]+'[123]_*.json')):
    d=json.load(open(f)); r,b,sp=d['prefix'].split('_')
    if d.get('detect'): R.setdefault((b,int(sp)),[]).append(d['detect'])
SP=['1k','2k','5k','10k','20k','50k','100k','200k','500k','1M']
K=[('ACC Main raw','Raw'),('ACC Main R1','Refine 1'),('ACC Main R3','Refine 3'),('ACC Main R5','Refine 5'),('RSP1B','RSP1B')]
def cell(L,k):
    v=[x[k]['pd'] for x in L if x.get(k) and x[k]['pd'] is not None and x[k]['n']>0]
    n=sum(x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)
    if not v: return '–'
    p=sum(x[k]['pd']*x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)/n
    return f'{100*p:.0f}' + (f' ({100*min(v):.0f}–{100*max(v):.0f})' if len(v)>1 else '')
MIN=int(os.environ.get('MINSIG','20'))
for b,name in (('160','160 m, 1840 kHz'),('bc','31 m broadcast band, 9650 kHz'),('20','20 m, 14075 kHz'),('10','10 m, 28075 kHz')):
    print(f'\n**{name}**\n\n| Span | Signals | '+' | '.join(l for _,l in K)+' |\n|---|---|'+'---|'*len(K))
    for s in range(10):
        L=R.get((b,s),[]); ns=sum(x.get('signals_Sub',0) for x in L)
        if ns<MIN: print(f'| {SP[s]} | {ns} | '+' | '.join('too few' if i==0 else '' for i in range(len(K)))+' |'); continue
        print(f'| {SP[s]} | {ns} | '+' | '.join(cell(L,k) for k,_ in K)+' |')
