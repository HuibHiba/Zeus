"""Genereert schema/speler.schema.json en squads/{amateur,halfprof,prof}.json (voorlopig, v0).

Deterministisch en reproduceert de gecommitte bestanden byte voor byte. Elk niveau is een basisprofiel van 15
attributen (schaal 1-20) met een vaste afwijking van -1..+1 per speler en positie; spitsen krijgen meer afwerken,
centrale verdedigers meer verdedigen. Amateurs zijn fysiek redelijk, maar zwak in overzicht, anticipatie,
positionering en beslissen (de systematische fouten zitten in perceptie en timing). Namen zijn fictief.
Aanpassen: wijzig `bases` of `names` en draai opnieuw:  python scripts/generate_squads.py
"""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
attrs=['passing','firstTouch','finishing','dribbling','defending','heading','speed','acceleration','strength','endurance','vision','anticipation','positioning','decisions','discipline']
positions=['GK','RB','RC','LC','LB','RWB','LWB','CB','DM','RCM','LCM','CM','AM','RM','LM','RW','LW','RS','LS','ST']
roles=['keeper','back','centrale_verdediger','wingback','controlerende_middenvelder','box_to_box','aanvallende_middenvelder','buitenspeler','spits','targetman']
def obj(properties,required=None):return dict(type='object',additionalProperties=False,properties=properties,required=required or list(properties))
text=dict(type='string',minLength=1,pattern=r'\S')
rating=dict(type='integer',minimum=1,maximum=20)
player=obj(dict(id=text,name=text,role=dict(enum=roles),preferredPosition=dict(enum=positions),attributes=obj({a:rating for a in attrs}),keeper=obj({a:rating for a in ['reflexes','positioning','rushingOut']})),['id','name','role','preferredPosition','attributes'])
player['allOf']=[{'if':{'properties':{'role':{'const':'keeper'}}},'then':{'required':['keeper']}}]
schema=obj(dict(schemaVersion={'const':'0.1'},status={'const':'voorlopig'},name=text,starters=dict(type='array',minItems=11,maxItems=11,items={'allOf':[{'$ref':'#/$defs/player'},{'properties':{'id':{'enum':positions}}}]}),bench=dict(type='array',minItems=7,maxItems=7,items={'allOf':[{'$ref':'#/$defs/player'},{'properties':{'id':{'pattern':'^B[1-7]$'}}}]})))
schema.update({'$schema':'https://json-schema.org/draft/2020-12/schema','$defs':{'player':player},'description':'Voorlopige werkhypothese v0; unieke ids, GK en tactiekslots aanvullend door SquadLoader/validate_squads.'})
(ROOT/'schema/speler.schema.json').write_text(json.dumps(schema,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
roster=json.loads((ROOT/'tactics/laag_blok_442.json').read_text(encoding='utf-8-sig'))['spelers']
roster += [{'id':'B'+str(i+1),'rol':r,'pos':pos} for i,(r,pos) in enumerate([('keeper','GK'),('centrale_verdediger','RC'),('back','LB'),('controlerende_middenvelder','RCM'),('buitenspeler','RM'),('spits','RS'),('targetman','LS')])]
names=['Jorre Veldakker','Marniek Zandhorst','Tijmen Rietweide','Wessel Brinkwaard','Nout Korenbeek','Jelmer Eikenwaard','Sietse Dalhoven','Bauke Veenakker','Rens Kiezelhorst','Koen Meerveldt','Floris Houtkampen','Jurre Wolkerveld','Fedde Zilverbeek','Ties Haverhorst','Melle Duinakker','Stijn Wilgenwaard','Bram Schaduwbeek','Daan Regenhorst']
# Physical strengths can survive at amateur level; errors are concentrated in perception, timing and spacing.
bases={'amateur':[10,9,10,11,9,11,13,12,13,11,6,6,6,7,11], 'halfprof':[14,13,14,14,13,13,15,14,14,14,12,12,12,13,14], 'prof':[18,17,17,17,17,16,17,17,16,17,17,18,18,18,17]}
(ROOT/'squads').mkdir(exist_ok=True)
for level,base in bases.items():
    players=[]
    for i,p in enumerate(roster):
        values={a:max(1,min(20,base[j]+((i*3+j*2)%3-1))) for j,a in enumerate(attrs)}
        if p['rol'] in ['spits','targetman']:values['finishing']=min(20,values['finishing']+1);values['defending']=max(1,values['defending']-2)
        if p['rol']=='centrale_verdediger':values['defending']=min(20,values['defending']+1);values['finishing']=max(1,values['finishing']-3)
        row=dict(id=p['id'],name=names[i],role=p['rol'],preferredPosition=p.get('pos',p['id']),attributes=values)
        if p['rol']=='keeper':row['keeper']={a:base[10]+(i%2) for a in ['reflexes','positioning','rushingOut']}
        players.append(row)
    data=dict(schemaVersion='0.1',status='voorlopig',name=level,starters=players[:11],bench=players[11:])
    (ROOT/'squads'/(level+'.json')).write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
