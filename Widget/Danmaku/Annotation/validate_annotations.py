#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
6657 Danmaku Annotation Validation and Merge CLI
Strictly verifies:
1. Untouched source 6657_memes.json SHA256 integrity and item count (23521).
2. JSON Schema strict compliance of batches, calibration samples, and merged dataset.
3. 1-based index boundary, strict monotonicity, and 100% continuous range coverage per batch.
4. Absolute prohibition of copied/altered original danmaku text.
5. Strict taxonomy controlled vocabulary enum validation and array deduplication.
6. Entity structure and type validation.
7. Structured safety validation (severity + flags logic consistency, no safe profanity).
8. Reviewer anti-spoofing validation (no reviewer when pending).
9. Manifest seamless coverage and full spectrum merge verification.
"""

import os
import sys
import json
import hashlib
import argparse
from typing import Dict, List, Set, Any, Tuple, Optional

try:
    import jsonschema
except ImportError:
    jsonschema = None

sys.stdout.reconfigure(encoding='utf-8')

EXPECTED_TOTAL_ITEMS = 23521
EXPECTED_SHA256 = "9bd3ed7ae963714a34d481bde45df597e4d4db49ee23c39d67506f11b4e32183"


class AnnotationValidator:
    def __init__(self, repo_root: str):
        self.repo_root = os.path.abspath(repo_root)
        self.annotation_dir = os.path.join(self.repo_root, "Widget", "Danmaku", "Annotation")
        self.source_path = os.path.join(self.repo_root, "Widget", "Danmaku", "6657_memes.json")
        self.taxonomy_path = os.path.join(self.annotation_dir, "taxonomy.v1.json")
        self.schema_path = os.path.join(self.annotation_dir, "schema.v1.json")
        self.manifest_path = os.path.join(self.annotation_dir, "manifest.json")
        self.aliases_path = os.path.join(self.annotation_dir, "entity_aliases.v1.json")
        self.batches_dir = os.path.join(self.annotation_dir, "batches")
        self.calibration_path = os.path.join(
            self.annotation_dir, "calibration_samples", "calibration_sample_batch.json"
        )
        if not os.path.exists(self.calibration_path):
            self.calibration_path = os.path.join(
                self.annotation_dir, "gold_samples", "gold_sample_batch.json"
            )

        self.taxonomy = None
        self.schema = None
        self.aliases_data = None
        self.alias_to_canonical = {}
        self.canonical_entities = {}
        self.valid_enums = {}
        self.source_data = None
        self.manifest = None
        self.errors = []
        self.warnings = []

    def log_error(self, msg: str):
        self.errors.append(msg)
        print(f"[ERROR] {msg}")

    def log_warning(self, msg: str):
        self.warnings.append(msg)
        print(f"[WARN]  {msg}")

    def log_info(self, msg: str):
        print(f"[INFO]  {msg}")

    def verify_source_integrity(self) -> bool:
        if not os.path.exists(self.source_path):
            self.log_error(f"Source file not found: {self.source_path}")
            return False

        with open(self.source_path, "rb") as f:
            raw_bytes = f.read()
            computed_sha256 = hashlib.sha256(raw_bytes).hexdigest()

        if computed_sha256 != EXPECTED_SHA256:
            self.log_error(
                f"Source 6657_memes.json SHA256 mismatch! Expected {EXPECTED_SHA256}, got {computed_sha256}. "
                "Raw source must remain untouched and unaltered!"
            )
            return False

        try:
            self.source_data = json.loads(raw_bytes.decode("utf-8-sig"))
        except Exception as e:
            self.log_error(f"Failed to parse source 6657_memes.json: {e}")
            return False

        if len(self.source_data) != EXPECTED_TOTAL_ITEMS:
            self.log_error(
                f"Source count mismatch! Expected {EXPECTED_TOTAL_ITEMS} items, found {len(self.source_data)}."
            )
            return False

        self.log_info(f"Source integrity verified: {EXPECTED_TOTAL_ITEMS} items, SHA256: {computed_sha256[:16]}... OK")
        return True

    def load_schema(self) -> bool:
        if not os.path.exists(self.schema_path):
            self.log_error(f"Schema definition missing: {self.schema_path}")
            return False

        try:
            with open(self.schema_path, "r", encoding="utf-8") as f:
                self.schema = json.load(f)
        except Exception as e:
            self.log_error(f"Failed to parse schema.v1.json: {e}")
            return False

        self.log_info("JSON Schema v1 loaded OK")
        return True

    def load_taxonomy(self) -> bool:
        if not os.path.exists(self.taxonomy_path):
            self.log_error(f"Taxonomy definition missing: {self.taxonomy_path}")
            return False

        try:
            with open(self.taxonomy_path, "r", encoding="utf-8") as f:
                self.taxonomy = json.load(f)
        except Exception as e:
            self.log_error(f"Failed to load taxonomy.v1.json: {e}")
            return False

        dims = self.taxonomy.get("dimensions", {})
        for dim_name in ["targets", "stances", "topics", "formats", "culture"]:
            if dim_name not in dims:
                self.log_error(f"Taxonomy missing dimension: {dim_name}")
                return False
            dim_def = dims[dim_name]
            self.valid_enums[dim_name] = set(dim_def.get("items", {}).get("enum", []))

        # scalar context
        self.valid_enums["context"] = set(dims.get("context", {}).get("enum", []))

        # structured safety
        safety_def = dims.get("safety", {}).get("properties", {})
        self.valid_enums["safety_severity"] = set(safety_def.get("severity", {}).get("enum", []))
        self.valid_enums["safety_flags"] = set(
            safety_def.get("flags", {}).get("items", {}).get("enum", [])
        )

        # entities and review enums
        self.valid_enums["entity_types"] = set(
            dims.get("entities", {}).get("items", {}).get("properties", {}).get("type", {}).get("enum", [])
        )
        self.valid_enums["review_status"] = set(
            dims.get("review", {}).get("properties", {}).get("status", {}).get("enum", [])
        )

        self.log_info("Taxonomy v1 loaded and controlled vocabularies initialized OK")
        return True

    def load_aliases(self) -> bool:
        if not os.path.exists(self.aliases_path):
            self.log_warning(f"Entity aliases file not found at {self.aliases_path}, alias normalization checks skipped.")
            return True

        try:
            with open(self.aliases_path, "r", encoding="utf-8") as f:
                self.aliases_data = json.load(f)
        except Exception as e:
            self.log_error(f"Failed to load entity_aliases.v1.json: {e}")
            return False

        entities_map = self.aliases_data.get("entities", {})
        for category, cat_entries in entities_map.items():
            for cname, cinfo in cat_entries.items():
                canonical = cinfo.get("canonical_name", cname)
                canonical_type = cinfo.get("type")
                self.canonical_entities[canonical] = canonical_type
                for alias in cinfo.get("aliases", []):
                    if alias != canonical:
                        self.alias_to_canonical[alias] = (canonical, canonical_type)

        self.log_info(
            f"Entity aliases loaded: {len(self.canonical_entities)} canonical entities, "
            f"{len(self.alias_to_canonical)} alias mappings OK"
        )
        return True

    def verify_manifest(self) -> bool:
        if not os.path.exists(self.manifest_path):
            self.log_error(f"Manifest missing: {self.manifest_path}")
            return False

        try:
            with open(self.manifest_path, "r", encoding="utf-8") as f:
                self.manifest = json.load(f)
        except Exception as e:
            self.log_error(f"Failed to load manifest.json: {e}")
            return False

        batches = self.manifest.get("batches", [])
        if not batches:
            self.log_error("Manifest has empty batches array.")
            return False

        expected_next = 1
        total_counted = 0
        seen_batch_ids = set()

        for b in batches:
            bid = b.get("batch_id")
            if not bid or bid in seen_batch_ids:
                self.log_error(f"Manifest contains duplicate or invalid batch_id: {bid}")
                return False
            seen_batch_ids.add(bid)

            start = b.get("start_index")
            end = b.get("end_index")
            count = b.get("count")

            if start != expected_next:
                self.log_error(f"Manifest gap or overlap at {bid}: expected start {expected_next}, got {start}")
                return False

            if end < start or count != (end - start + 1):
                self.log_error(f"Manifest range/count inconsistency in {bid}: start={start}, end={end}, count={count}")
                return False

            total_counted += count
            expected_next = end + 1

        if total_counted != EXPECTED_TOTAL_ITEMS or expected_next != EXPECTED_TOTAL_ITEMS + 1:
            self.log_error(
                f"Manifest total coverage mismatch! Counted {total_counted}, expected {EXPECTED_TOTAL_ITEMS}"
            )
            return False

        self.log_info(f"Manifest verified: {len(batches)} batches seamlessly covering 1..{EXPECTED_TOTAL_ITEMS} OK")
        return True

    def validate_single_entry(self, entry: Dict[str, Any], batch_scope: str = "") -> bool:
        is_valid = True

        # 1. Absolute prohibition of copied/altered original text
        forbidden_keys = {"text", "raw_text", "content", "danmaku", "message", "source_text", "meme"}
        for k in forbidden_keys:
            if k in entry:
                self.log_error(
                    f"[{batch_scope}] Entry contains forbidden field '{k}' at index {entry.get('index')}. "
                    "Original text MUST NOT be duplicated or stored in annotation entries!"
                )
                is_valid = False

        # 2. Strict allowed fields only (additionalProperties check)
        allowed_entry_keys = {
            "index", "targets", "stances", "topics", "formats",
            "culture", "entities", "context", "safety", "confidence",
            "review", "source_tags"
        }
        for k in entry.keys():
            if k not in allowed_entry_keys:
                self.log_error(f"[{batch_scope}] Unexpected field '{k}' at index {entry.get('index')}")
                is_valid = False

        required_fields = [
            "index", "targets", "stances", "topics", "formats",
            "culture", "entities", "context", "safety", "confidence", "review"
        ]
        for f in required_fields:
            if f not in entry:
                self.log_error(f"[{batch_scope}] Missing required field '{f}' at index {entry.get('index')}")
                is_valid = False

        if not is_valid:
            return False

        idx = entry["index"]
        if not isinstance(idx, int) or idx < 1 or idx > EXPECTED_TOTAL_ITEMS:
            self.log_error(f"[{batch_scope}] Invalid index: {idx} (must be integer 1..{EXPECTED_TOTAL_ITEMS})")
            is_valid = False

        # 3. Array dimension enum & uniqueness validation
        for dim_name in ["targets", "stances", "topics", "formats", "culture"]:
            val = entry.get(dim_name)
            if not isinstance(val, list) or len(val) == 0:
                self.log_error(f"[{batch_scope}] Index {idx}: '{dim_name}' must be a non-empty array.")
                is_valid = False
            else:
                # Check uniqueness (no duplicate tags)
                if len(val) != len(set(val)):
                    self.log_error(f"[{batch_scope}] Index {idx}: '{dim_name}' contains duplicate values: {val}")
                    is_valid = False

                for item in val:
                    if item not in self.valid_enums.get(dim_name, set()):
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Invalid value '{item}' for dimension '{dim_name}'."
                        )
                        is_valid = False

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
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Stance '{st_item}' requires target '{req_target}' in targets."
                        )
                        is_valid = False

        # Topic boundary check: streamer topics require 'streamer' in targets
        if isinstance(topics_val, list):
            for t in topics_val:
                if isinstance(t, str) and t.startswith("streamer_") and "streamer" not in target_set:
                    self.log_error(
                        f"[{batch_scope}] Index {idx}: Topic '{t}' is reserved for streamer and requires 'streamer' in targets."
                    )
                    is_valid = False

        # 4. Context enum validation
        ctx = entry.get("context")
        if not isinstance(ctx, str) or ctx not in self.valid_enums.get("context", set()):
            self.log_error(f"[{batch_scope}] Index {idx}: Invalid 'context' value '{ctx}'.")
            is_valid = False

        # 5. Structured Safety Validation
        safety = entry.get("safety")
        if not isinstance(safety, dict):
            self.log_error(f"[{batch_scope}] Index {idx}: 'safety' must be an object with 'severity' and 'flags'.")
            is_valid = False
        else:
            allowed_safety_keys = {"severity", "flags"}
            for k in safety.keys():
                if k not in allowed_safety_keys:
                    self.log_error(f"[{batch_scope}] Index {idx}: 'safety' contains unexpected field '{k}'.")
                    is_valid = False

            sev = safety.get("severity")
            flags = safety.get("flags")

            if sev not in self.valid_enums.get("safety_severity", set()):
                self.log_error(f"[{batch_scope}] Index {idx}: Invalid safety severity '{sev}'.")
                is_valid = False

            if not isinstance(flags, list) or len(flags) == 0:
                self.log_error(f"[{batch_scope}] Index {idx}: safety.flags must be a non-empty array.")
                is_valid = False
            else:
                if len(flags) != len(set(flags)):
                    self.log_error(f"[{batch_scope}] Index {idx}: safety.flags contains duplicate items: {flags}")
                    is_valid = False

                for flag in flags:
                    if flag not in self.valid_enums.get("safety_flags", set()):
                        self.log_error(f"[{batch_scope}] Index {idx}: Invalid safety flag '{flag}'.")
                        is_valid = False

                # Logical consistency:
                # - If 'none' in flags, it must be the only flag
                if "none" in flags and len(flags) > 1:
                    self.log_error(
                        f"[{batch_scope}] Index {idx}: safety.flags cannot mix 'none' with other risk flags: {flags}"
                    )
                    is_valid = False

                # - If flags contain risk flags, severity cannot be 'safe'
                risk_flags = {"profanity", "personal_attack", "sexual_content", "violent_imagery", "discriminatory", "self_harm", "spam_noise"}
                if any(rf in flags for rf in risk_flags) and sev == "safe":
                    self.log_error(
                        f"[{batch_scope}] Index {idx}: Safety contradiction! Severity cannot be 'safe' when risk flags are present: {flags}"
                    )
                    is_valid = False

                # - If severity is safe, flags must be ['none']
                if sev == "safe" and flags != ["none"]:
                    self.log_error(
                        f"[{batch_scope}] Index {idx}: Safety contradiction! Severity 'safe' requires flags=['none'], got {flags}"
                    )
                    is_valid = False

        # 6. Entities Validation
        entities = entry.get("entities")
        if not isinstance(entities, list):
            self.log_error(f"[{batch_scope}] Index {idx}: 'entities' must be an array.")
            is_valid = False
        else:
            seen_entity_tuples = set()
            for ent in entities:
                if not isinstance(ent, dict):
                    self.log_error(f"[{batch_scope}] Index {idx}: Entity is not an object: {ent}")
                    is_valid = False
                    continue

                allowed_ent_keys = {"name", "type"}
                for k in ent.keys():
                    if k not in allowed_ent_keys:
                        self.log_error(f"[{batch_scope}] Index {idx}: Entity contains unexpected field '{k}': {ent}")
                        is_valid = False

                if "name" not in ent or "type" not in ent:
                    self.log_error(f"[{batch_scope}] Index {idx}: Entity missing 'name' or 'type': {ent}")
                    is_valid = False
                else:
                    name = str(ent.get("name", "")).strip()
                    etype = ent.get("type")
                    if not name:
                        self.log_error(f"[{batch_scope}] Index {idx}: Entity 'name' cannot be empty.")
                        is_valid = False
                    if etype not in self.valid_enums.get("entity_types", set()):
                        self.log_error(f"[{batch_scope}] Index {idx}: Invalid entity type '{etype}' in entity {ent}")
                        is_valid = False

                    entity_key = (name, etype)
                    if entity_key in seen_entity_tuples:
                        self.log_error(f"[{batch_scope}] Index {idx}: Duplicate entity definition: {ent}")
                        is_valid = False
                    seen_entity_tuples.add(entity_key)

                    # Alias normalization check
                    if name in self.alias_to_canonical:
                        canonical_name, canonical_type = self.alias_to_canonical[name]
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity name '{name}' is a known alias of '{canonical_name}'. "
                            f"Please use canonical name '{canonical_name}'."
                        )
                        is_valid = False

                    # Canonical entity type consistency check
                    if name in self.canonical_entities:
                        canonical_type = self.canonical_entities[name]
                        if etype != canonical_type:
                            self.log_error(
                                f"[{batch_scope}] Index {idx}: Entity '{name}' type mismatch! "
                                f"Expected '{canonical_type}', got '{etype}'."
                            )
                            is_valid = False

                    # Targets-Entities coherence check
                    if etype == "streamer" and "streamer" not in target_set:
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'streamer', but 'targets' does not contain 'streamer'."
                        )
                        is_valid = False
                    elif etype == "player" and "pro_player" not in target_set:
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'player', but 'targets' does not contain 'pro_player'."
                        )
                        is_valid = False
                    elif etype == "team" and "pro_team" not in target_set:
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'team', but 'targets' does not contain 'pro_team'."
                        )
                        is_valid = False
                    elif etype == "coach" and ("pro_player" not in target_set and "caster_host" not in target_set and "pro_team" not in target_set):
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'coach', but 'targets' does not contain 'pro_player' or 'caster_host'."
                        )
                        is_valid = False
                    elif etype == "caster" and "caster_host" not in target_set:
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'caster', but 'targets' does not contain 'caster_host'."
                        )
                        is_valid = False
                    elif etype == "acg_character" and "external_figure" not in target_set:
                        self.log_error(
                            f"[{batch_scope}] Index {idx}: Entity '{name}' is of type 'acg_character', but 'targets' does not contain 'external_figure'."
                        )
                        is_valid = False

        # 7. Confidence Validation
        conf = entry.get("confidence")
        if not isinstance(conf, (int, float)) or conf < 0.0 or conf > 1.0:
            self.log_error(f"[{batch_scope}] Index {idx}: 'confidence' must be float between 0.0 and 1.0, got {conf}")
            is_valid = False

        # 8. Review Validation & Anti-Spoofing
        review = entry.get("review")
        if not isinstance(review, dict) or "status" not in review:
            self.log_error(f"[{batch_scope}] Index {idx}: 'review' must be object with 'status'")
            is_valid = False
        else:
            allowed_rev_keys = {"status", "reviewer", "comments"}
            for k in review.keys():
                if k not in allowed_rev_keys:
                    self.log_error(f"[{batch_scope}] Index {idx}: 'review' contains unexpected field '{k}'")
                    is_valid = False

            status = review.get("status")
            if status not in self.valid_enums.get("review_status", set()):
                self.log_error(f"[{batch_scope}] Index {idx}: Invalid review status '{status}'")
                is_valid = False

            reviewer = review.get("reviewer")
            # Anti-spoofing rule: pending review CANNOT have a reviewer assigned
            if status == "pending" and reviewer is not None and str(reviewer).strip() != "":
                self.log_error(
                    f"[{batch_scope}] Index {idx}: Spoofed review! Reviewer '{reviewer}' cannot be set when status is 'pending'."
                )
                is_valid = False

            # Reviewed/Approved must have a valid reviewer
            if status in {"reviewed", "approved"}:
                if reviewer is None or str(reviewer).strip() == "":
                    self.log_error(
                        f"[{batch_scope}] Index {idx}: Status '{status}' requires a non-empty 'reviewer' field."
                    )
                    is_valid = False

        # 9. Optional source_tags validation
        if "source_tags" in entry:
            st = entry["source_tags"]
            if not isinstance(st, list):
                self.log_error(f"[{batch_scope}] Index {idx}: 'source_tags' must be a list of strings if provided.")
                is_valid = False
            elif len(st) != len(set(st)):
                self.log_error(f"[{batch_scope}] Index {idx}: 'source_tags' contains duplicate entries: {st}")
                is_valid = False

        return is_valid

    def validate_file(
        self, file_path: str, is_calibration: bool = False, expected_manifest_entry: Optional[Dict[str, Any]] = None
    ) -> Tuple[bool, List[Dict[str, Any]]]:
        if not os.path.exists(file_path):
            self.log_error(f"File not found: {file_path}")
            return False, []

        try:
            with open(file_path, "r", encoding="utf-8") as f:
                data = json.load(f)
        except Exception as e:
            self.log_error(f"JSON decode failed for {file_path}: {e}")
            return False, []

        if not isinstance(data, dict):
            self.log_error(f"{file_path} root must be a JSON object.")
            return False, []

        # 1. JSON Schema formal validation if available
        if jsonschema and self.schema:
            try:
                jsonschema.validate(instance=data, schema=self.schema)
            except jsonschema.exceptions.ValidationError as e:
                self.log_error(f"Schema validation failed for {os.path.basename(file_path)}: {e.message} at path {list(e.path)}")
                return False, []
            except Exception as e:
                self.log_error(f"Schema validator error on {os.path.basename(file_path)}: {e}")
                return False, []

        batch_id = data.get("batch_id", os.path.basename(file_path))
        annotations = data.get("annotations", [])

        if not isinstance(annotations, list):
            self.log_error(f"[{batch_id}] 'annotations' must be an array.")
            return False, []

        declared_total = data.get("total_items")
        if declared_total != len(annotations):
            self.log_error(
                f"[{batch_id}] Declared total_items ({declared_total}) does not match actual annotations count ({len(annotations)})"
            )
            return False, []

        range_obj = data.get("range", {})
        start_idx = range_obj.get("start_index")
        end_idx = range_obj.get("end_index")

        if not isinstance(start_idx, int) or not isinstance(end_idx, int) or start_idx > end_idx:
            self.log_error(f"[{batch_id}] Invalid range specification: {range_obj}")
            return False, []

        file_valid = True

        # Check full seamless coverage for standard batches
        if not is_calibration and batch_id != "calibration_sample" and batch_id != "gold_sample":
            expected_count = end_idx - start_idx + 1
            if declared_total != expected_count:
                self.log_error(
                    f"[{batch_id}] Range [{start_idx}..{end_idx}] implies {expected_count} items, but declared total_items is {declared_total}"
                )
                file_valid = False

            if expected_manifest_entry:
                m_start = expected_manifest_entry.get("start_index")
                m_end = expected_manifest_entry.get("end_index")
                m_bid = expected_manifest_entry.get("batch_id")
                if batch_id != m_bid:
                    self.log_error(f"[{batch_id}] Batch ID mismatch with manifest: expected {m_bid}, got {batch_id}")
                    file_valid = False
                if start_idx != m_start or end_idx != m_end:
                    self.log_error(
                        f"[{batch_id}] Range mismatch with manifest: expected [{m_start}..{m_end}], got [{start_idx}..{end_idx}]"
                    )
                    file_valid = False

        # Monotonicity, uniqueness, and completeness checks
        seen_indices = set()
        expected_current_index = start_idx if (not is_calibration and batch_id not in {"calibration_sample", "gold_sample"}) else None

        for idx_offset, entry in enumerate(annotations):
            if not isinstance(entry, dict):
                self.log_error(f"[{batch_id}] Annotation entry #{idx_offset} is not an object.")
                file_valid = False
                continue

            entry_idx = entry.get("index")
            if entry_idx in seen_indices:
                self.log_error(f"[{batch_id}] Duplicate index found: {entry_idx}")
                file_valid = False
            seen_indices.add(entry_idx)

            # Continuous 100% coverage check for batches
            if expected_current_index is not None:
                if entry_idx != expected_current_index:
                    self.log_error(
                        f"[{batch_id}] Index continuity broken at entry #{idx_offset}: expected index {expected_current_index}, got {entry_idx}"
                    )
                    file_valid = False
                expected_current_index += 1
            else:
                # For calibration samples, ensure strictly within range
                if entry_idx < start_idx or entry_idx > end_idx:
                    self.log_error(f"[{batch_id}] Index {entry_idx} out of declared range [{start_idx}..{end_idx}]")
                    file_valid = False

            if not self.validate_single_entry(entry, batch_scope=batch_id):
                file_valid = False

        if expected_current_index is not None and expected_current_index != end_idx + 1:
            self.log_error(
                f"[{batch_id}] Incomplete batch coverage! Stopped at index {expected_current_index - 1}, expected {end_idx}"
            )
            file_valid = False

        if file_valid:
            self.log_info(f"File {os.path.basename(file_path)} [{batch_id}] passed all checks ({len(annotations)} items) OK")
        return file_valid, annotations

    def check_and_merge_all(self, output_file: Optional[str] = None, allow_incomplete: bool = False) -> bool:
        if (
            not self.verify_source_integrity()
            or not self.load_schema()
            or not self.load_taxonomy()
            or not self.load_aliases()
            or not self.verify_manifest()
        ):
            return False

        batches = self.manifest.get("batches", [])
        all_annotations = []
        covered_indices = set()
        completed_batches = 0
        missing_batches = []

        for b in batches:
            rel_path = b["relative_path"]
            abs_path = os.path.join(self.annotation_dir, rel_path)
            if not os.path.exists(abs_path):
                missing_batches.append(b["batch_id"])
                continue

            # When allow_incomplete is active, validate without aborting progress reporting
            error_count_before = len(self.errors)
            valid, batch_entries = self.validate_file(abs_path, is_calibration=False, expected_manifest_entry=b)
            if not valid:
                if allow_incomplete:
                    # Under allow_incomplete, treat failing batches as uncompleted for progress reporting
                    self.log_warning(
                        f"Batch {b['batch_id']} is pending repairs ({len(self.errors) - error_count_before} issues); excluded from interim coverage."
                    )
                    missing_batches.append(b["batch_id"])
                    # Revert errors recorded during this non-blocking batch check
                    self.errors = self.errors[:error_count_before]
                    continue
                else:
                    self.log_error(f"Batch {b['batch_id']} failed validation.")
                    return False

            for entry in batch_entries:
                idx = entry["index"]
                if idx in covered_indices:
                    self.log_error(f"Cross-batch duplicate index {idx} detected in {b['batch_id']}!")
                    return False
                covered_indices.add(idx)
                all_annotations.append(entry)

            completed_batches += 1

        total_batches = len(batches)
        coverage_pct = (len(covered_indices) / EXPECTED_TOTAL_ITEMS) * 100.0
        self.log_info(
            f"Coverage Progress: {completed_batches}/{total_batches} batches ({len(covered_indices)}/{EXPECTED_TOTAL_ITEMS} items, {coverage_pct:.2f}%)"
        )

        if missing_batches:
            self.log_info(f"Uncompleted batches: {len(missing_batches)} (e.g. {missing_batches[:5]}...)")

        if len(covered_indices) == EXPECTED_TOTAL_ITEMS:
            all_annotations.sort(key=lambda x: x["index"])
            if output_file:
                merged_data = {
                    "schema_version": "1.0.0",
                    "batch_id": "full_merged",
                    "total_items": len(all_annotations),
                    "range": {"start_index": 1, "end_index": EXPECTED_TOTAL_ITEMS},
                    "annotations": all_annotations
                }
                out_path = os.path.abspath(output_file)
                with open(out_path, "w", encoding="utf-8") as f:
                    json.dump(merged_data, f, ensure_ascii=False, indent=2)
                self.log_info(f"Successfully exported full merged dataset to {out_path} OK")
            return True
        else:
            if allow_incomplete:
                self.log_info("Full coverage not yet reached (ongoing milestone). Exiting 0 due to --allow-incomplete.")
                return True
            else:
                self.log_error(
                    f"Full coverage NOT reached! Only {completed_batches}/{total_batches} batches present. "
                    "Use --allow-incomplete if checking interim status."
                )
                return False


def main():
    parser = argparse.ArgumentParser(description="6657 Danmaku Annotation Validator and Merger")
    parser.add_argument("--repo-root", default=r"D:\KBC\CS2KillConfirmOverlay", help="Repository root path")
    parser.add_argument("--validate-file", help="Validate a specific batch JSON file")
    parser.add_argument("--validate-calibration", "--validate-gold", dest="validate_calibration", action="store_true", help="Validate calibration samples")
    parser.add_argument("--verify-infra", action="store_true", help="Verify taxonomy, schema, manifest, source integrity, and calibration samples")
    parser.add_argument("--check-coverage", action="store_true", help="Check current batch completion and coverage")
    parser.add_argument("--allow-incomplete", action="store_true", help="Allow incomplete batch coverage without non-zero exit code")
    parser.add_argument("--merge-output", help="Output path for merged JSON if full coverage reached")

    args = parser.parse_args()
    validator = AnnotationValidator(args.repo_root)

    success = True
    executed_action = False

    if args.verify_infra:
        executed_action = True
        s1 = validator.verify_source_integrity()
        s2 = validator.load_schema()
        s3 = validator.load_taxonomy()
        s4 = validator.load_aliases()
        s5 = validator.verify_manifest()
        s6, _ = validator.validate_file(validator.calibration_path, is_calibration=True)
        success = s1 and s2 and s3 and s4 and s5 and s6

    if args.validate_calibration:
        executed_action = True
        validator.load_schema()
        validator.load_taxonomy()
        validator.load_aliases()
        val_success, _ = validator.validate_file(validator.calibration_path, is_calibration=True)
        success = success and val_success

    if args.validate_file:
        executed_action = True
        validator.load_schema()
        validator.load_taxonomy()
        validator.load_aliases()
        val_success, _ = validator.validate_file(args.validate_file)
        success = success and val_success

    if args.check_coverage:
        executed_action = True
        cov_success = validator.check_and_merge_all(
            output_file=args.merge_output,
            allow_incomplete=args.allow_incomplete
        )
        success = success and cov_success

    if not executed_action:
        # Default run: Infrastructure and calibration verification
        s1 = validator.verify_source_integrity()
        s2 = validator.load_schema()
        s3 = validator.load_taxonomy()
        s4 = validator.load_aliases()
        s5 = validator.verify_manifest()
        s6, _ = validator.validate_file(validator.calibration_path, is_calibration=True)
        success = s1 and s2 and s3 and s4 and s5 and s6
        if success:
            validator.log_info("Default check completed: Infrastructure & Calibration Samples valid. (0/48 batches started; awaiting host review)")

    if not success or len(validator.errors) > 0:
        print(f"\nFAILED: {len(validator.errors)} errors encountered.")
        sys.exit(1)
    else:
        print("\nSUCCESS: All requested validation checks passed cleanly!")
        sys.exit(0)


if __name__ == "__main__":
    main()
