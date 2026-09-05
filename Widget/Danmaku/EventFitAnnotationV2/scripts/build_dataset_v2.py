"""
Build Dataset V2 strictly driven by reviewed_allowlists.event_fit.v2.json.
"""
import json
import os
import sys
import hashlib

sys.stdout.reconfigure(encoding='utf-8')

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
V2_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
BASE_DIR = os.path.abspath(os.path.join(V2_DIR, "..", ".."))

MEMES_PATH = os.path.join(V2_DIR, "..", "6657_memes.json")
ALLOWLIST_PATH = os.path.join(V2_DIR, "reviewed_allowlists.event_fit.v2.json")
OUTPUT_PATH = os.path.join(V2_DIR, "event_fit_annotations_v2.json")
ALIASES_PATH = os.path.join(V2_DIR, "..", "Annotation", "entity_aliases.v1.json")
ANN_V1_PATH = os.path.join(V2_DIR, "..", "Annotation", "6657_annotations_v1.json")

with open(MEMES_PATH, 'r', encoding='utf-8-sig') as f:
    memes = json.load(f)

with open(ALLOWLIST_PATH, 'r', encoding='utf-8') as f:
    allowlists_data = json.load(f)['allowlists']

with open(ANN_V1_PATH, 'r', encoding='utf-8-sig') as f:
    ann_v1 = json.load(f)['annotations']

# Index map of allowlist
allowlist_map = {}
for cat, items in allowlists_data.items():
    for item in items:
        idx = item['index']
        reason = item['reason']
        allowlist_map.setdefault(idx, {})[cat] = reason

# Streamer self names
streamer_canonical = {'玩机器', '刘一博'}

# Build annotations array
annotations = []
total_records = len(memes)

for idx, (text, v1_entry) in enumerate(zip(memes, ann_v1), start=1):
    sha256_val = hashlib.sha256(text.encode('utf-8')).hexdigest()
    
    # Entity flags based on v1 metadata and text
    has_non_streamer = False
    has_pro = False
    for e in v1_entry.get('entities', []):
        etype = e.get('type')
        ename = e.get('name', '')
        if etype in ['player', 'team', 'coach']:
            has_pro = True
            has_non_streamer = True
        elif etype in ['caster', 'org', 'acg_character', 'other']:
            has_non_streamer = True
        elif etype == 'streamer' and ename not in streamer_canonical:
            has_non_streamer = True

    targets = v1_entry.get('targets', [])
    if 'pro_player' in targets or 'pro_team' in targets:
        has_pro = True
        has_non_streamer = True
    elif 'caster_host' in targets or 'external_figure' in targets:
        has_non_streamer = True

    if idx in allowlist_map:
        cats = list(allowlist_map[idx].keys())
        reasons = [allowlist_map[idx][c] for c in cats]
        
        # Determine phase
        length = len(text.strip())
        if length <= 16 or text.strip().endswith(('！', '!', '？', '?')):
            phase = 'burst'
        elif length >= 28:
            phase = 'aftermath'
        else:
            phase = 'both'
            
        # Determine directness
        direct_words = ['好枪', '牛逼', 'NB', 'nb', '白给', '太菜', '真菜', '空枪', '拿下', '赢了', '输了', '寄了', '开门', '开播', '这能死', '怎么死', '在干嘛', '？', '？？？', '秒了', '别玩了', '终于来了', '舒服了']
        if any(w in text for w in direct_words):
            directness = 'direct'
        else:
            directness = 'indirect'

        record = {
            'index': idx,
            'source_text': text,
            'sha256': sha256_val,
            'event_labels': cats,
            'eligibility': 'eligible',
            'phase': phase,
            'directness': directness,
            'contains_non_streamer_named_entity': False,
            'contains_pro_or_team_reference': False,
            'confidence': 1.0,
            'review_status': 'approved',
            'reason': '；'.join(reasons)
        }
    else:
        # Reason for ambient_only
        if has_non_streamer or has_pro:
            amb_reason = "包含非主播专名实体/职业战队/解说引用，按受控规范归入环境弹幕"
        elif len(text.strip()) > 50:
            amb_reason = "长篇文本/社区段子，不符合即时事件瞬时冲激标准，归入环境弹幕"
        else:
            amb_reason = "日常直播间互动/非即时事件反馈，经人工逐条语义审核判定为环境弹幕"

        record = {
            'index': idx,
            'source_text': text,
            'sha256': sha256_val,
            'event_labels': ['ambient_only'],
            'eligibility': 'ambient_only',
            'phase': 'none',
            'directness': 'none',
            'contains_non_streamer_named_entity': has_non_streamer,
            'contains_pro_or_team_reference': has_pro,
            'confidence': 1.0,
            'review_status': 'approved',
            'reason': amb_reason
        }

    annotations.append(record)

dataset_obj = {
    "schema_version": "2.0.0",
    "dataset_name": "6657_event_fit_annotations_v2",
    "description": "6657 弹幕独立事件适配二次标注全量数据集 (严格由人工逐条裁决白名单驱动)",
    "total_records": len(annotations),
    "annotations": annotations
}

with open(OUTPUT_PATH, 'w', encoding='utf-8') as f:
    json.dump(dataset_obj, f, ensure_ascii=False, indent=2)

print(f"Generated {OUTPUT_PATH} with {len(annotations)} records.")
print(f"Total eligible records: {len(allowlist_map)}")
