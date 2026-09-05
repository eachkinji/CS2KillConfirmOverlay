#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
6657 Danmaku Annotation Web GUI Backend Server
Zero external dependencies (Python standard library only).
Features:
- Fast in-memory indexing and multi-criteria querying across 23,521 annotations.
- Strict schema & taxonomy validation before writing.
- Atomic file writing and automated rolling backups.
- Reviewer anti-spoofing enforcement.
- Localhost (127.0.0.1) only.
"""

import os
import sys
import json
import shutil
import hashlib
import tempfile
import threading
from datetime import datetime
from urllib.parse import urlparse, parse_qs
from http.server import HTTPServer, BaseHTTPRequestHandler
from typing import Dict, List, Any, Optional, Tuple

sys.stdout.reconfigure(encoding='utf-8')

EXPECTED_TOTAL_ITEMS = 23521
EXPECTED_SHA256 = "9bd3ed7ae963714a34d481bde45df597e4d4db49ee23c39d67506f11b4e32183"

class AnnotationDataStore:
    def __init__(self, repo_root: str):
        self.repo_root = os.path.abspath(repo_root)
        self.annotation_dir = os.path.join(self.repo_root, "Widget", "Danmaku", "Annotation")
        self.source_path = os.path.join(self.repo_root, "Widget", "Danmaku", "6657_memes.json")
        self.taxonomy_path = os.path.join(self.annotation_dir, "taxonomy.v1.json")
        self.schema_path = os.path.join(self.annotation_dir, "schema.v1.json")
        self.manifest_path = os.path.join(self.annotation_dir, "manifest.json")
        self.aliases_path = os.path.join(self.annotation_dir, "entity_aliases.v1.json")
        self.batches_dir = os.path.join(self.annotation_dir, "batches")
        self.merged_path = os.path.join(self.annotation_dir, "6657_annotations_v1.json")
        self.backup_dir = os.path.join(self.annotation_dir, ".backups")

        self.lock = threading.RLock()
        self.source_memes: List[Dict[str, Any]] = []
        self.index_to_meme: Dict[int, Dict[str, Any]] = {}
        self.taxonomy: Dict[str, Any] = {}
        self.manifest: Dict[str, Any] = {}
        self.aliases_data: Dict[str, Any] = {}
        self.valid_enums: Dict[str, set] = {}
        self.canonical_entities: Dict[str, str] = {}
        self.alias_to_canonical: Dict[str, Tuple[str, str]] = {}

        # In-memory storage: 1-based index -> annotation entry
        self.annotations: Dict[int, Dict[str, Any]] = {}
        # index -> batch_id
        self.index_to_batch_id: Dict[int, str] = {}
        self.batch_id_to_manifest: Dict[str, Dict[str, Any]] = {}

        self.load_all()

    def verify_source_integrity(self):
        if not os.path.exists(self.source_path):
            raise FileNotFoundError(f"Source file missing: {self.source_path}")
        with open(self.source_path, "rb") as f:
            content = f.read()
            sha256 = hashlib.sha256(content).hexdigest()
        if sha256 != EXPECTED_SHA256:
            raise ValueError(f"Source SHA256 mismatch! Expected {EXPECTED_SHA256}, got {sha256}")
        self.source_memes = json.loads(content.decode("utf-8-sig"))
        if len(self.source_memes) != EXPECTED_TOTAL_ITEMS:
            raise ValueError(f"Source count mismatch! Expected {EXPECTED_TOTAL_ITEMS}, got {len(self.source_memes)}")
        self.index_to_meme = {i + 1: {"id": i + 1, "text": self.source_memes[i]} for i in range(len(self.source_memes))}

    def load_taxonomy(self):
        with open(self.taxonomy_path, "r", encoding="utf-8") as f:
            self.taxonomy = json.load(f)
        dims = self.taxonomy.get("dimensions", {})
        for dim in ["targets", "stances", "topics", "formats", "culture"]:
            self.valid_enums[dim] = set(dims.get(dim, {}).get("items", {}).get("enum", []))
        self.valid_enums["context"] = set(dims.get("context", {}).get("enum", []))
        safety_props = dims.get("safety", {}).get("properties", {})
        self.valid_enums["safety_severity"] = set(safety_props.get("severity", {}).get("enum", []))
        self.valid_enums["safety_flags"] = set(safety_props.get("flags", {}).get("items", {}).get("enum", []))
        self.valid_enums["entity_types"] = set(
            dims.get("entities", {}).get("items", {}).get("properties", {}).get("type", {}).get("enum", [])
        )
        self.valid_enums["review_status"] = set(
            dims.get("review", {}).get("properties", {}).get("status", {}).get("enum", [])
        )

    def load_aliases(self):
        if os.path.exists(self.aliases_path):
            with open(self.aliases_path, "r", encoding="utf-8") as f:
                self.aliases_data = json.load(f)
            entities_map = self.aliases_data.get("entities", {})
            for cat, cat_entries in entities_map.items():
                for cname, cinfo in cat_entries.items():
                    canonical = cinfo.get("canonical_name", cname)
                    canonical_type = cinfo.get("type")
                    self.canonical_entities[canonical] = canonical_type
                    for alias in cinfo.get("aliases", []):
                        if alias != canonical:
                            self.alias_to_canonical[alias] = (canonical, canonical_type)

    def load_manifest(self):
        with open(self.manifest_path, "r", encoding="utf-8") as f:
            self.manifest = json.load(f)
        for b in self.manifest.get("batches", []):
            bid = b["batch_id"]
            self.batch_id_to_manifest[bid] = b
            for idx in range(b["start_index"], b["end_index"] + 1):
                self.index_to_batch_id[idx] = bid

    def load_annotations(self):
        # First try loading all batch files
        loaded_count = 0
        batches = self.manifest.get("batches", [])
        for b in batches:
            rel_path = b["relative_path"]
            abs_path = os.path.join(self.annotation_dir, rel_path)
            if os.path.exists(abs_path):
                with open(abs_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    for entry in data.get("annotations", []):
                        self.annotations[entry["index"]] = entry
                        loaded_count += 1
        
        # If merged exists and has items not loaded, fallback / complement
        if loaded_count < EXPECTED_TOTAL_ITEMS and os.path.exists(self.merged_path):
            try:
                with open(self.merged_path, "r", encoding="utf-8") as f:
                    merged_data = json.load(f)
                    for entry in merged_data.get("annotations", []):
                        if entry["index"] not in self.annotations:
                            self.annotations[entry["index"]] = entry
            except Exception:
                pass

    def load_all(self):
        with self.lock:
            self.verify_source_integrity()
            self.load_taxonomy()
            self.load_aliases()
            self.load_manifest()
            self.load_annotations()

    def get_meta(self) -> Dict[str, Any]:
        with self.lock:
            # Prepare canonical entity suggestions list
            canonical_list = []
            for name, etype in self.canonical_entities.items():
                canonical_list.append({"name": name, "type": etype})
            canonical_list.sort(key=lambda x: (x["type"], x["name"]))

            return {
                "total_items": EXPECTED_TOTAL_ITEMS,
                "loaded_items": len(self.annotations),
                "taxonomy": self.taxonomy,
                "manifest_batches": self.manifest.get("batches", []),
                "canonical_entities": canonical_list,
                "entity_aliases": self.aliases_data.get("entities", {})
            }

    def get_stats(self) -> Dict[str, Any]:
        with self.lock:
            review_counts = {"pending": 0, "reviewed": 0, "approved": 0, "disputed": 0}
            safety_counts = {"safe": 0, "borderline_playful": 0, "sensitive_flame": 0, "toxic_vulgar": 0}
            target_counts: Dict[str, int] = {}
            stance_counts: Dict[str, int] = {}
            topic_counts: Dict[str, int] = {}

            flame_streamer_count = 0

            for entry in self.annotations.values():
                r_status = entry.get("review", {}).get("status", "pending")
                review_counts[r_status] = review_counts.get(r_status, 0) + 1

                s_sev = entry.get("safety", {}).get("severity", "safe")
                safety_counts[s_sev] = safety_counts.get(s_sev, 0) + 1

                for t in entry.get("targets", []):
                    target_counts[t] = target_counts.get(t, 0) + 1
                for s in entry.get("stances", []):
                    stance_counts[s] = stance_counts.get(s, 0) + 1
                    if s == "flame_streamer":
                        flame_streamer_count += 1
                for top in entry.get("topics", []):
                    topic_counts[top] = topic_counts.get(top, 0) + 1

            return {
                "total": EXPECTED_TOTAL_ITEMS,
                "annotated": len(self.annotations),
                "review_counts": review_counts,
                "safety_counts": safety_counts,
                "target_counts": target_counts,
                "stance_counts": stance_counts,
                "topic_counts": topic_counts,
                "flame_streamer_count": flame_streamer_count,
            }

    def query_items(
        self,
        page: int = 1,
        page_size: int = 50,
        query: str = "",
        target: str = "",
        stance: str = "",
        topic: str = "",
        format_: str = "",
        culture: str = "",
        severity: str = "",
        flag: str = "",
        review_status: str = "",
        batch_id: str = "",
        preset: str = ""
    ) -> Dict[str, Any]:
        with self.lock:
            q = query.strip().lower()
            matched_indices = []

            for idx in range(1, EXPECTED_TOTAL_ITEMS + 1):
                entry = self.annotations.get(idx)
                meme = self.index_to_meme.get(idx, {})
                raw_text = meme.get("text", "")
                raw_text_lower = raw_text.lower()

                if not entry:
                    if q and (q not in raw_text_lower and str(idx) != q):
                        continue
                    matched_indices.append(idx)
                    continue

                # 1. Full-text search
                if q:
                    idx_str = str(idx)
                    in_text = q in raw_text_lower
                    in_idx = (q == idx_str or idx_str.startswith(q))
                    in_ent = any(q in ent.get("name", "").lower() for ent in entry.get("entities", []))
                    in_comment = q in entry.get("review", {}).get("comments", "").lower()
                    in_tags = any(q in t.lower() for t in entry.get("targets", []) + entry.get("stances", []) + entry.get("topics", []))
                    if not (in_text or in_idx or in_ent or in_comment or in_tags):
                        continue

                # 2. Target filter
                if target and target not in entry.get("targets", []):
                    continue

                # 3. Stance filter
                if stance and stance not in entry.get("stances", []):
                    continue

                # 4. Topic filter
                if topic and topic not in entry.get("topics", []):
                    continue

                # 5. Format filter
                if format_ and format_ not in entry.get("formats", []):
                    continue

                # 6. Culture filter
                if culture and culture not in entry.get("culture", []):
                    continue

                # 7. Safety severity
                if severity and entry.get("safety", {}).get("severity") != severity:
                    continue

                # 8. Safety flags
                if flag and flag not in entry.get("safety", {}).get("flags", []):
                    continue

                # 9. Review status
                if review_status and entry.get("review", {}).get("status") != review_status:
                    continue

                # 10. Batch filter
                if batch_id:
                    cur_batch = self.index_to_batch_id.get(idx)
                    if cur_batch != batch_id:
                        continue

                # 11. Presets
                if preset == "flame_streamer":
                    if "flame_streamer" not in entry.get("stances", []):
                        continue
                elif preset in {"safe_with_flame", "safe_but_flame"}:
                    sev = entry.get("safety", {}).get("severity")
                    has_flame = any(isinstance(s, str) and s.startswith("flame_") for s in entry.get("stances", []))
                    if not (sev == "safe" and has_flame):
                        continue
                elif preset == "toxic_or_sensitive":
                    if entry.get("safety", {}).get("severity") not in {"sensitive_flame", "toxic_vulgar"}:
                        continue
                elif preset == "pending_review":
                    if entry.get("review", {}).get("status") != "pending":
                        continue
                elif preset == "has_entities":
                    if not entry.get("entities"):
                        continue
                elif preset == "low_confidence":
                    if entry.get("confidence", 1.0) >= 0.85:
                        continue

                matched_indices.append(idx)

            total_matched = len(matched_indices)
            start_offset = (page - 1) * page_size
            end_offset = start_offset + page_size
            page_indices = matched_indices[start_offset:end_offset]

            items = []
            for idx in page_indices:
                entry = self.annotations.get(idx)
                meme = self.index_to_meme.get(idx, {})
                items.append({
                    "index": idx,
                    "raw_text": meme.get("text", ""),
                    "batch_id": self.index_to_batch_id.get(idx, ""),
                    "targets": entry.get("targets", []) if entry else [],
                    "stances": entry.get("stances", []) if entry else [],
                    "topics": entry.get("topics", []) if entry else [],
                    "safety": entry.get("safety", {"severity": "safe", "flags": ["none"]}) if entry else {},
                    "confidence": entry.get("confidence", 1.0) if entry else 1.0,
                    "review": entry.get("review", {"status": "pending"}) if entry else {"status": "pending"},
                    "entity_count": len(entry.get("entities", [])) if entry else 0,
                })

            return {
                "page": page,
                "page_size": page_size,
                "total_matched": total_matched,
                "total_pages": (total_matched + page_size - 1) // page_size if page_size > 0 else 0,
                "items": items
            }

    def get_single_item(self, index: int) -> Optional[Dict[str, Any]]:
        with self.lock:
            if index < 1 or index > EXPECTED_TOTAL_ITEMS:
                return None
            entry = self.annotations.get(index)
            meme = self.index_to_meme.get(index, {})
            batch_id = self.index_to_batch_id.get(index, "")

            if not entry:
                # Default baseline entry
                entry = {
                    "index": index,
                    "targets": ["streamer"],
                    "stances": ["neutral_informative"],
                    "topics": ["misc_unclear"],
                    "formats": ["plain_statement"],
                    "culture": ["origin_6657"],
                    "entities": [],
                    "context": "standalone",
                    "safety": {"severity": "safe", "flags": ["none"]},
                    "confidence": 1.0,
                    "review": {"status": "pending"}
                }

            return {
                "index": index,
                "raw_text": meme.get("text", ""),
                "batch_id": batch_id,
                "annotation": entry
            }

    def validate_entry(self, entry: Dict[str, Any]) -> Tuple[bool, List[str]]:
        errors = []
        if not isinstance(entry, dict):
            return False, ["Entry must be a JSON object."]

        # 1. Absolute prohibition of copied/altered original text
        forbidden_keys = {"text", "raw_text", "content", "danmaku", "message", "source_text", "meme"}
        for k in forbidden_keys:
            if k in entry:
                errors.append(f"Entry contains forbidden field '{k}'. Original text MUST NOT be duplicated in annotations!")

        # 2. Allowed fields check
        allowed_entry_keys = {
            "index", "targets", "stances", "topics", "formats",
            "culture", "entities", "context", "safety", "confidence",
            "review", "source_tags"
        }
        for k in entry.keys():
            if k not in allowed_entry_keys:
                errors.append(f"Unexpected field '{k}' in entry.")

        required_fields = [
            "index", "targets", "stances", "topics", "formats",
            "culture", "entities", "context", "safety", "confidence", "review"
        ]
        for f in required_fields:
            if f not in entry:
                errors.append(f"Missing required field '{f}'.")

        if errors:
            return False, errors

        idx = entry.get("index")
        if not isinstance(idx, int) or idx < 1 or idx > EXPECTED_TOTAL_ITEMS:
            errors.append(f"Invalid index: {idx} (must be 1..{EXPECTED_TOTAL_ITEMS})")

        # 3. Array dimension enum & uniqueness validation
        for dim_name in ["targets", "stances", "topics", "formats", "culture"]:
            val = entry.get(dim_name)
            if not isinstance(val, list) or len(val) == 0:
                errors.append(f"Field '{dim_name}' must be a non-empty array.")
            else:
                if len(val) != len(set(val)):
                    errors.append(f"Field '{dim_name}' contains duplicate values: {val}")
                for item in val:
                    if item not in self.valid_enums.get(dim_name, set()):
                        errors.append(f"Invalid enum value '{item}' for dimension '{dim_name}'.")

        # Stance-Target strong coherence check: flame_* requires matching target
        targets_val = entry.get("targets", [])
        stances_val = entry.get("stances", [])
        topics_val = entry.get("topics", [])
        target_set = set(targets_val) if isinstance(targets_val, list) else set()

        flame_to_target = {
            "flame_streamer": "streamer",
            "flame_player": "pro_player",
            "flame_team": "pro_team",
            "flame_audience": "chat_audience",
            "flame_caster_host": "caster_host",
            "flame_external_figure": "external_figure",
        }
        if isinstance(stances_val, list):
            for st_item in stances_val:
                if st_item in flame_to_target:
                    req_target = flame_to_target[st_item]
                    if req_target not in target_set:
                        errors.append(f"Stance '{st_item}' requires target '{req_target}' in targets.")

        # Topic boundary check: streamer topics require 'streamer' in targets
        if isinstance(topics_val, list):
            for t in topics_val:
                if isinstance(t, str) and t.startswith("streamer_") and "streamer" not in target_set:
                    errors.append(f"Topic '{t}' is reserved for streamer and requires 'streamer' in targets.")

        # 4. Context enum validation
        ctx = entry.get("context")
        if not isinstance(ctx, str) or ctx not in self.valid_enums.get("context", set()):
            errors.append(f"Invalid 'context' value '{ctx}'.")

        # 5. Structured Safety Validation
        safety = entry.get("safety")
        if not isinstance(safety, dict):
            errors.append("'safety' must be an object with 'severity' and 'flags'.")
        else:
            allowed_safety_keys = {"severity", "flags"}
            for k in safety.keys():
                if k not in allowed_safety_keys:
                    errors.append(f"'safety' contains unexpected field '{k}'.")

            sev = safety.get("severity")
            flags = safety.get("flags")

            if sev not in self.valid_enums.get("safety_severity", set()):
                errors.append(f"Invalid safety severity '{sev}'.")

            if not isinstance(flags, list) or len(flags) == 0:
                errors.append("safety.flags must be a non-empty array.")
            else:
                if len(flags) != len(set(flags)):
                    errors.append(f"safety.flags contains duplicate items: {flags}")

                for flag in flags:
                    if flag not in self.valid_enums.get("safety_flags", set()):
                        errors.append(f"Invalid safety flag '{flag}'.")

                if "none" in flags and len(flags) > 1:
                    errors.append(f"safety.flags cannot mix 'none' with other risk flags: {flags}")

                risk_flags = {"profanity", "personal_attack", "sexual_content", "violent_imagery", "discriminatory", "self_harm", "spam_noise"}
                if any(rf in flags for rf in risk_flags) and sev == "safe":
                    errors.append(f"Safety contradiction! Severity cannot be 'safe' when risk flags are present: {flags}")

                if sev == "safe" and flags != ["none"]:
                    errors.append(f"Safety contradiction! Severity 'safe' requires flags=['none'], got {flags}")

        # 6. Entities Validation
        entities = entry.get("entities")
        if not isinstance(entities, list):
            errors.append("'entities' must be an array.")
        else:
            seen_entity_tuples = set()
            for ent in entities:
                if not isinstance(ent, dict):
                    errors.append(f"Entity is not an object: {ent}")
                    continue

                allowed_ent_keys = {"name", "type"}
                for k in ent.keys():
                    if k not in allowed_ent_keys:
                        errors.append(f"Entity contains unexpected field '{k}': {ent}")

                if "name" not in ent or "type" not in ent:
                    errors.append(f"Entity missing 'name' or 'type': {ent}")
                else:
                    name = str(ent.get("name", "")).strip()
                    etype = ent.get("type")
                    if not name:
                        errors.append("Entity 'name' cannot be empty.")
                    if etype not in self.valid_enums.get("entity_types", set()):
                        errors.append(f"Invalid entity type '{etype}' in entity {ent}")

                    entity_key = (name, etype)
                    if entity_key in seen_entity_tuples:
                        errors.append(f"Duplicate entity definition: {ent}")
                    seen_entity_tuples.add(entity_key)

                    # Alias normalization check
                    if name in self.alias_to_canonical:
                        canonical_name, canonical_type = self.alias_to_canonical[name]
                        errors.append(
                            f"Entity name '{name}' is a known alias of '{canonical_name}'. Please use canonical name '{canonical_name}'."
                        )

                    # Canonical entity type consistency check
                    if name in self.canonical_entities:
                        canonical_type = self.canonical_entities[name]
                        if etype != canonical_type:
                            errors.append(f"Entity '{name}' type mismatch! Expected '{canonical_type}', got '{etype}'.")

                    # Targets-Entities coherence check
                    if etype == "streamer" and "streamer" not in target_set:
                        errors.append(f"Entity '{name}' is of type 'streamer', but 'targets' does not contain 'streamer'.")
                    elif etype == "player" and "pro_player" not in target_set:
                        errors.append(f"Entity '{name}' is of type 'player', but 'targets' does not contain 'pro_player'.")
                    elif etype == "team" and "pro_team" not in target_set:
                        errors.append(f"Entity '{name}' is of type 'team', but 'targets' does not contain 'pro_team'.")
                    elif etype == "coach" and ("pro_player" not in target_set and "caster_host" not in target_set and "pro_team" not in target_set):
                        errors.append(f"Entity '{name}' is of type 'coach', but 'targets' does not contain 'pro_player', 'caster_host', or 'pro_team'.")
                    elif etype == "caster" and "caster_host" not in target_set:
                        errors.append(f"Entity '{name}' is of type 'caster', but 'targets' does not contain 'caster_host'.")
                    elif etype == "acg_character" and "external_figure" not in target_set:
                        errors.append(f"Entity '{name}' is of type 'acg_character', but 'targets' does not contain 'external_figure'.")

        # 7. Confidence Validation
        conf = entry.get("confidence")
        if not isinstance(conf, (int, float)) or conf < 0.0 or conf > 1.0:
            errors.append(f"'confidence' must be float between 0.0 and 1.0, got {conf}")

        # 8. Review Validation & Anti-Spoofing
        review = entry.get("review")
        if not isinstance(review, dict) or "status" not in review:
            errors.append("'review' must be object with 'status'")
        else:
            allowed_rev_keys = {"status", "reviewer", "comments"}
            for k in review.keys():
                if k not in allowed_rev_keys:
                    errors.append(f"'review' contains unexpected field '{k}'")

            status = review.get("status")
            if status not in self.valid_enums.get("review_status", set()):
                errors.append(f"Invalid review status '{status}'")

            reviewer = review.get("reviewer")
            # Anti-spoofing rule: pending review CANNOT have a reviewer assigned
            if status == "pending" and reviewer is not None and str(reviewer).strip() != "":
                errors.append(f"Spoofed review! Reviewer '{reviewer}' cannot be set when status is 'pending'.")

            if status in {"reviewed", "approved"}:
                if reviewer is None or str(reviewer).strip() == "":
                    errors.append(f"Status '{status}' requires a non-empty 'reviewer' field.")

        # 9. Optional source_tags validation
        if "source_tags" in entry:
            st = entry["source_tags"]
            if not isinstance(st, list):
                errors.append("'source_tags' must be a list of strings if provided.")
            elif len(st) != len(set(st)):
                errors.append(f"'source_tags' contains duplicate entries: {st}")

        return len(errors) == 0, errors

    def _create_backup(self, files_to_backup: List[str]) -> str:
        os.makedirs(self.backup_dir, exist_ok=True)
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        backup_sub = os.path.join(self.backup_dir, f"backup_{timestamp}")
        os.makedirs(backup_sub, exist_ok=True)
        for fpath in files_to_backup:
            if os.path.exists(fpath):
                shutil.copy2(fpath, os.path.join(backup_sub, os.path.basename(fpath)))
        return backup_sub

    def save_item(self, index: int, new_entry: Dict[str, Any]) -> Tuple[bool, List[str]]:
        with self.lock:
            entry_to_save = dict(new_entry)
            entry_to_save["index"] = index

            # Strip empty optional reviewer if pending
            if entry_to_save.get("review", {}).get("status") == "pending":
                if "reviewer" in entry_to_save["review"] and not entry_to_save["review"]["reviewer"]:
                    del entry_to_save["review"]["reviewer"]

            # Validate entry
            is_valid, errors = self.validate_entry(entry_to_save)
            if not is_valid:
                return False, errors

            # Find batch info
            bid = self.index_to_batch_id.get(index)
            if not bid or bid not in self.batch_id_to_manifest:
                return False, [f"Could not map index {index} to a valid batch."]

            batch_manifest = self.batch_id_to_manifest[bid]
            batch_rel_path = batch_manifest["relative_path"]
            batch_abs_path = os.path.join(self.annotation_dir, batch_rel_path)

            batch_start = batch_manifest["start_index"]
            batch_end = batch_manifest["end_index"]

            # 1. Build new batch structure without touching in-memory annotations
            batch_annotations = []
            for i in range(batch_start, batch_end + 1):
                if i == index:
                    batch_annotations.append(entry_to_save)
                elif i in self.annotations:
                    batch_annotations.append(self.annotations[i])
                else:
                    return False, [f"Missing annotation data for index {i} in batch {bid}"]

            batch_data = {
                "schema_version": "1.0.0",
                "batch_id": bid,
                "total_items": len(batch_annotations),
                "range": {"start_index": batch_start, "end_index": batch_end},
                "annotations": batch_annotations
            }

            # 2. Build new merged structure if full coverage exists
            merged_data = None
            if len(self.annotations) == EXPECTED_TOTAL_ITEMS or os.path.exists(self.merged_path):
                sorted_annotations = []
                for i in range(1, EXPECTED_TOTAL_ITEMS + 1):
                    if i == index:
                        sorted_annotations.append(entry_to_save)
                    elif i in self.annotations:
                        sorted_annotations.append(self.annotations[i])
                    else:
                        break
                if len(sorted_annotations) == EXPECTED_TOTAL_ITEMS:
                    merged_data = {
                        "schema_version": "1.0.0",
                        "batch_id": "full_merged",
                        "total_items": EXPECTED_TOTAL_ITEMS,
                        "range": {"start_index": 1, "end_index": EXPECTED_TOTAL_ITEMS},
                        "annotations": sorted_annotations
                    }

            # 3. Create backup copies before modifying files
            backup_dir = self._create_backup([batch_abs_path, self.merged_path])
            batch_backup_copy = os.path.join(backup_dir, os.path.basename(batch_abs_path))
            merged_backup_copy = os.path.join(backup_dir, os.path.basename(self.merged_path))

            # 4. Prepare temporary files
            tmp_batch_fd, tmp_batch_path = tempfile.mkstemp(
                dir=os.path.dirname(batch_abs_path), prefix=".tmp_batch_", suffix=".json"
            )
            tmp_merged_fd = None
            tmp_merged_path = None
            if merged_data is not None:
                tmp_merged_fd, tmp_merged_path = tempfile.mkstemp(
                    dir=os.path.dirname(self.merged_path), prefix=".tmp_merged_", suffix=".json"
                )

            batch_replaced = False
            merged_replaced = False

            try:
                # Write temp batch
                with os.fdopen(tmp_batch_fd, "w", encoding="utf-8") as f:
                    json.dump(batch_data, f, ensure_ascii=False, indent=2)

                # Write temp merged
                if merged_data is not None and tmp_merged_fd is not None:
                    with os.fdopen(tmp_merged_fd, "w", encoding="utf-8") as f:
                        json.dump(merged_data, f, ensure_ascii=False, indent=2)

                # Two-Phase atomic replacement:
                # Phase 1: replace batch file
                os.replace(tmp_batch_path, batch_abs_path)
                batch_replaced = True

                # Phase 2: replace merged file
                if merged_data is not None and tmp_merged_path is not None:
                    os.replace(tmp_merged_path, self.merged_path)
                    merged_replaced = True

                # Phase 3: Commit to memory only when all files successfully replaced
                self.annotations[index] = entry_to_save
                return True, []

            except Exception as e:
                # Transaction rollback
                rollback_errors = []
                if batch_replaced:
                    try:
                        if os.path.exists(batch_backup_copy):
                            shutil.copy2(batch_backup_copy, batch_abs_path)
                    except Exception as rb_ex:
                        rollback_errors.append(f"Failed to rollback batch: {rb_ex}")

                if merged_replaced:
                    try:
                        if os.path.exists(merged_backup_copy):
                            shutil.copy2(merged_backup_copy, self.merged_path)
                    except Exception as rb_ex:
                        rollback_errors.append(f"Failed to rollback merged: {rb_ex}")

                for tpath in [tmp_batch_path, tmp_merged_path]:
                    if tpath and os.path.exists(tpath):
                        try:
                            os.remove(tpath)
                        except Exception:
                            pass

                err_msg = f"Transactional write failed and rolled back: {e}"
                if rollback_errors:
                    err_msg += f" (Rollback warnings: {'; '.join(rollback_errors)})"
                return False, [err_msg]


class AnnotationHTTPRequestHandler(BaseHTTPRequestHandler):
    store: AnnotationDataStore = None
    gui_html_path: str = ""

    def log_message(self, format, *args):
        # Concise logging to console
        sys.stderr.write(f"[{datetime.now().strftime('%H:%M:%S')}] {args[0]} {args[1]}\n")

    def _send_json(self, status_code: int, data: Any):
        payload = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()
        self.wfile.write(payload)

    def _send_html(self, status_code: int, html_content: str):
        payload = html_content.encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        params = parse_qs(parsed.query)

        def get_param(name: str, default: str = "") -> str:
            vals = params.get(name, [])
            return vals[0] if vals else default

        try:
            if path in ["/", "/index.html"]:
                if os.path.exists(self.gui_html_path):
                    with open(self.gui_html_path, "r", encoding="utf-8") as f:
                        content = f.read()
                    self._send_html(200, content)
                else:
                    self._send_html(404, "<h1>GUI index.html not found</h1>")
                return

            if path == "/api/meta":
                data = self.store.get_meta()
                self._send_json(200, {"success": True, "data": data})
                return

            if path == "/api/stats":
                data = self.store.get_stats()
                self._send_json(200, {"success": True, "data": data})
                return

            if path == "/api/items":
                page = int(get_param("page", "1"))
                page_size = int(get_param("page_size", "50"))
                query = get_param("query", "")
                target = get_param("target", "")
                stance = get_param("stance", "")
                topic = get_param("topic", "")
                format_ = get_param("format", "")
                culture = get_param("culture", "")
                severity = get_param("severity", "")
                flag = get_param("flag", "")
                review_status = get_param("review_status", "")
                batch_id = get_param("batch_id", "")
                preset = get_param("preset", "")

                data = self.store.query_items(
                    page=page,
                    page_size=page_size,
                    query=query,
                    target=target,
                    stance=stance,
                    topic=topic,
                    format_=format_,
                    culture=culture,
                    severity=severity,
                    flag=flag,
                    review_status=review_status,
                    batch_id=batch_id,
                    preset=preset
                )
                self._send_json(200, {"success": True, "data": data})
                return

            if path.startswith("/api/item/"):
                idx_str = path[len("/api/item/"):]
                if not idx_str.isdigit():
                    self._send_json(400, {"success": False, "error": "Invalid index parameter"})
                    return
                idx = int(idx_str)
                item = self.store.get_single_item(idx)
                if item is None:
                    self._send_json(404, {"success": False, "error": f"Item index {idx} not found"})
                    return
                self._send_json(200, {"success": True, "data": item})
                return

            self._send_json(404, {"success": False, "error": "Endpoint not found"})
        except Exception as e:
            self._send_json(500, {"success": False, "error": str(e)})

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path.startswith("/api/item/"):
            idx_str = path[len("/api/item/"):]
            if not idx_str.isdigit():
                self._send_json(400, {"success": False, "error": "Invalid index parameter"})
                return
            idx = int(idx_str)

            content_length = int(self.headers.get("Content-Length", 0))
            if content_length == 0:
                self._send_json(400, {"success": False, "error": "Missing request body"})
                return

            try:
                body = self.rfile.read(content_length).decode("utf-8")
                new_entry = json.loads(body)
            except Exception as e:
                self._send_json(400, {"success": False, "error": f"Invalid JSON body: {e}"})
                return

            success, errors = self.store.save_item(idx, new_entry)
            if not success:
                self._send_json(400, {"success": False, "errors": errors})
                return

            updated = self.store.get_single_item(idx)
            self._send_json(200, {"success": True, "data": updated, "message": f"Index {idx} saved successfully."})
            return

        self._send_json(404, {"success": False, "error": "Endpoint not found"})


def create_server(repo_root: str, host: str = "127.0.0.1", port: int = 8765) -> HTTPServer:
    store = AnnotationDataStore(repo_root)
    gui_html_path = os.path.join(repo_root, "Widget", "Danmaku", "Annotation", "gui", "index.html")

    AnnotationHTTPRequestHandler.store = store
    AnnotationHTTPRequestHandler.gui_html_path = gui_html_path

    server = HTTPServer((host, port), AnnotationHTTPRequestHandler)
    return server


def main():
    import argparse
    import webbrowser

    default_repo_root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
    parser = argparse.ArgumentParser(description="6657 Danmaku Annotation Web GUI Server")
    parser.add_argument("--repo-root", default=default_repo_root, help="Repository root path")
    parser.add_argument("--host", default="127.0.0.1", help="Host address (default 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8765, help="Port to listen on (default 8765)")
    parser.add_argument("--no-browser", action="store_true", help="Do not open browser automatically")

    args = parser.parse_args()

    server = create_server(args.repo_root, host=args.host, port=args.port)
    url = f"http://{args.host}:{args.port}"
    print("=" * 60)
    print(f"6657 弹幕标注审阅系统已在本地启动:")
    print(f"  -> 访问地址: {url}")
    print(f"  -> 存储根路径: {os.path.abspath(args.repo_root)}")
    print(f"  -> 按 Ctrl+C 停止服务器")
    print("=" * 60)

    if not args.no_browser:
        threading.Timer(0.8, lambda: webbrowser.open(url)).start()

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[INFO] 服务器已优雅退出。")
        server.server_close()


if __name__ == "__main__":
    main()
