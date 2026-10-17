"""Reproduce Wyscout duel/card/penalty targets from original events, never emit raw data."""
import argparse, json, zipfile, hashlib
from pathlib import Path
from collections import Counter

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source-dir', type=Path, required=True, help='Directory containing original events.zip and matches.zip')
parser.add_argument('--out', type=Path, required=True, help='Aggregate JSON output path')
args = parser.parse_args()
SRC = args.source_dir
OUT = args.out
if OUT.resolve() in {(SRC/'events.zip').resolve(), (SRC/'matches.zip').resolve()}:
 raise ValueError('Output must not replace a source archive')
groups = ['England','Spain','Italy','Germany','France','World_Cup','European_Championship']
result = {'source':'Pappalardo and Massucco, Events, https://doi.org/10.6084/m9.figshare.7770599.v1; Matches, https://doi.org/10.6084/m9.figshare.7770422.v1; CC BY 4.0',
          'definitions':{'periods':'Exclude P (penalty shootouts); include extra time.',
           'duels':'Wyscout ground defending duels (sub12) + contact fouls (sub20); excludes air/attacking/loose-ball events. Proxy, not identical to sim attempt.',
           'cards':'Foul events with tags 1701/1702/1703; direct-red excludes second-yellow.',
           'penalty':'Free-kick sub35 events, divided by all foul events; taken penalties, not awards.'}, 'groups':{}}
with zipfile.ZipFile(SRC/'events.zip') as z, zipfile.ZipFile(SRC/'matches.zip') as mz:
 for group in groups:
  matches=json.loads(mz.read('matches_'+group+'.json'))
  c=Counter(); zones=[Counter() for _ in range(3)]; contact_zones=[Counter() for _ in range(3)]
  for e in json.loads(z.read('events_'+group+'.json')):
   if e.get('matchPeriod')=='P': continue
   ev,sub=e['eventId'],e['subEventId']; tags={t['id'] for t in e.get('tags',[])}
   if ev==1:
    c['all_duels']+=1
    if sub==12:c['ground_defending_duels']+=1
   if ev==3 and sub==35:
    c['penalties']+=1; c['penalties_scored']+=101 in tags
   if ev!=2:continue
   c['fouls']+=1; c['contact_fouls']+=sub==20
   card=bool(tags & {1701,1702,1703}); red=1701 in tags and 1703 not in tags
   c['cards']+=card; c['direct_red']+=red; c['second_yellow']+=1703 in tags
   if sub==20:
    c['contact_cards']+=card; c['contact_direct_red']+=red; c['contact_second_yellow']+=1703 in tags
   if e.get('positions'):
    x=e['positions'][0]['x']; zone=min(2,int(x/ (100/3)))
    zones[zone]['fouls']+=1; zones[zone]['cards']+=card; zones[zone]['direct_red']+=red
    if sub==20:
     contact_zones[zone]['fouls']+=1; contact_zones[zone]['cards']+=card; contact_zones[zone]['direct_red']+=red
  n=2*len(matches)
  result['groups'][group]={'team_matches':n,'counts':dict(c),
   'per_team':{k:v/n for k,v in c.items()},
   'penalty_per_foul':c['penalties']/c['fouls'],
   'direct_red_per_foul':c['direct_red']/c['fouls'],
   'duel_attempt_proxy_per_team':(c['ground_defending_duels']+c['contact_fouls'])/n,
   'card_per_third':[dict(z)|{'card_rate':z['cards']/z['fouls'],'direct_red_rate':z['direct_red']/z['fouls']} for z in zones],
   'contact_card_per_third':[dict(z)|{'card_rate':z['cards']/z['fouls'],'direct_red_rate':z['direct_red']/z['fouls']} for z in contact_zones]}
  print(group, json.dumps(result['groups'][group]),flush=True)
result['input_sha256']={f:hashlib.sha256((SRC/f).read_bytes()).hexdigest() for f in ['events.zip','matches.zip']}
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(json.dumps(result,indent=2),encoding='utf8')
