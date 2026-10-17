"""Derive a provisional DuelConfig proposal from source targets and instrumented simulation scores."""
import argparse, json, math
from pathlib import Path
from collections import Counter
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--targets', type=Path, required=True, help='Aggregate JSON from derive_wyscout_duel_targets.py')
parser.add_argument('--scores', type=Path, required=True, help='JSONL emitted by instrumented simulator lab')
parser.add_argument('--baseline-attempt-rate', type=float, required=True, help='AttemptRate used by the scored baseline')
parser.add_argument('--out', type=Path, required=True, help='Calibration proposal JSON output')
args = parser.parse_args()
groups=json.loads(args.targets.read_text(encoding='utf8'))['groups']
c=Counter(); zone=[Counter() for _ in range(3)]
for g in groups.values():
 c.update(g['counts']);c['team_matches']+=g['team_matches']
 for i,z in enumerate(g['contact_card_per_third']):
  zone[i].update({k:z.get(k,0) for k in ('fouls','cards','direct_red')})
rows=[json.loads(l) for l in args.scores.read_text(encoding='utf8').splitlines() if l.startswith('{')]
matches=[r for r in rows if r['kind']=='match']; scores=[r for r in rows if r['kind']=='foul']
if not matches or not scores: raise ValueError('Need both completed match records and foul score records')
if any(m['status'] != 'Finished' for m in matches): raise ValueError('Baseline contains unfinished or abandoned matches')
if not 0 < args.baseline_attempt_rate <= 1: raise ValueError('Baseline attempt rate must be in (0,1]')
if any(s['zone'] not in (0,1,2) or not math.isfinite(s['score']) or not 0 <= s['score'] <= 1 for s in scores):
 raise ValueError('Foul scores must be finite in [0,1], with zone 0,1,2')
if any(not any(s['zone']==i for s in scores) for i in range(3)):
 raise ValueError('Need at least one foul score in each pitch third')
if any(not math.isfinite(m[k]) or m[k] < 0 for m in matches for k in ('InsideUnscaled','OutsideUnscaled')):
 raise ValueError('Expected foul masses must be finite and nonnegative')
def threshold(s,target):
 candidates=sorted(set(s)); candidates += [min(1,math.nextafter(max(s),math.inf))]
 best=min(candidates,key=lambda t:abs(sum(v>=t for v in s)/len(s)-target))
 return {'value':best,'actual_rate':sum(v>=best for v in s)/len(s),'target_rate':target,'n':len(s)}
targets={'contact_cards_per_third':[z['cards']/z['fouls'] for z in zone],
 'contact_red_per_foul':c['contact_direct_red']/c['contact_fouls'],
 'penalty_per_contact_foul':c['penalties']/c['contact_fouls'],
 'contact_fouls_per_team':c['contact_fouls']/c['team_matches'],
 'all_fouls_per_team':c['fouls']/c['team_matches'],
 'penalties_per_team':c['penalties']/c['team_matches'],
 'direct_red_per_team':c['direct_red']/c['team_matches'],
 'second_yellow_per_team':c['second_yellow']/c['team_matches'],
 'duel_proxy_per_team':(c['ground_defending_duels']+c['contact_fouls'])/c['team_matches']}
ins=sum(r['InsideUnscaled'] for r in matches);outside=sum(r['OutsideUnscaled'] for r in matches)
if ins<=0 or outside<=0: raise ValueError('Need positive expected foul mass both inside and outside penalty areas')
r=targets['penalty_per_contact_foul']; unconstrained_f=r*outside/(ins*(1-r)); f=max(0,min(1,unconstrained_f))
expected=(outside+f*ins)/(2*len(matches))
proposal={'RecklessOwnThird':threshold([s['score'] for s in scores if s['zone']==0],targets['contact_cards_per_third'][0]),
 'RecklessMiddle':threshold([s['score'] for s in scores if s['zone']==1],targets['contact_cards_per_third'][1]),
 'RecklessAttacking':threshold([s['score'] for s in scores if s['zone']==2],targets['contact_cards_per_third'][2]),
 'ExcessiveForceScore':threshold([s['score'] for s in scores],targets['contact_red_per_foul']),
 'PenaltyAreaFoulFactor':f,
 'AttemptRate':args.baseline_attempt_rate*targets['contact_fouls_per_team']/expected}
res={'targets':targets,'source_counts':dict(c),'baseline_matches':matches,'baseline_fouls':len(scores),'proposal':proposal,
 'limitations':['Proposal only; no production edit or validation run.',
 'Quantiles are observed under old conduct defaults; new cards change later actions and score distribution.',
 'Penalty factor uses expected foul mass at attempts, not observed rare penalties. Refit after rate/shot changes.',
 'AttemptRate scaled against contact fouls; Wyscout ground defending duels cannot be treated as identical to sim challenges.',
 'Pooled seven competitions; extra time included, equal weight per team-match, not per competition.']}
res['valid_config_range'] = 0 <= f <= 1 and 0 <= proposal['AttemptRate'] <= 1
res['sample_sufficient_for_rare_red_target'] = len(scores)*targets['contact_red_per_foul'] >= 5
res['penalty_fit']={'unconstrained_factor':unconstrained_f,'bounded_factor':f,
 'expected_penalty_per_foul':f*ins/(outside+f*ins), 'target_penalty_per_foul':r,
 'foul_mass_inside_at_factor_one':ins,'foul_mass_outside':outside,
 'target_reachable_in_sample':0 <= unconstrained_f <= 1}
args.out.parent.mkdir(parents=True, exist_ok=True)
args.out.write_text(json.dumps(res,indent=2),encoding='utf8')
print(json.dumps(res,indent=2))
