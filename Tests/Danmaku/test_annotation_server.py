#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Automated Unit, Integration, and Fault-Injection Tests for 6657 Danmaku Annotation Server & GUI API.

All mutation and read-write tests run inside an isolated TemporaryDirectory sandbox copy of the repository.
The real workspace data files and .backups are NEVER modified.
Supports concurrent test suite executions.
"""

import os
import sys
import json
import socket
import shutil
import tempfile
import unittest
import threading
import urllib.request
import urllib.error
from unittest.mock import patch
from typing import Dict, Any

# Locate real repo root
REAL_REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REAL_ANNOTATION_DIR = os.path.join(REAL_REPO_ROOT, "Widget", "Danmaku", "Annotation")
sys.path.insert(0, REAL_ANNOTATION_DIR)

from server import AnnotationDataStore, create_server, EXPECTED_TOTAL_ITEMS, EXPECTED_SHA256


def get_free_port() -> int:
    """Find a dynamically available free TCP port on localhost."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(("127.0.0.1", 0))
        s.listen(1)
        port = s.getsockname()[1]
    return port


class TestAnnotationDataStoreIsolated(unittest.TestCase):
    temp_dir: tempfile.TemporaryDirectory = None
    temp_repo: str = ""

    @classmethod
    def setUpClass(cls):
        # Create an isolated temporary directory for test repository
        cls.temp_dir = tempfile.TemporaryDirectory(prefix="test_danmaku_repo_")
        cls.temp_repo = cls.temp_dir.name

        # Copy only the required Danmaku files into isolated sandbox
        src_danmaku = os.path.join(REAL_REPO_ROOT, "Widget", "Danmaku")
        dst_danmaku = os.path.join(cls.temp_repo, "Widget", "Danmaku")
        os.makedirs(os.path.dirname(dst_danmaku), exist_ok=True)
        shutil.copytree(src_danmaku, dst_danmaku, ignore=shutil.ignore_patterns("__pycache__", ".backups"))

        cls.store = AnnotationDataStore(cls.temp_repo)

    @classmethod
    def tearDownClass(cls):
        if cls.temp_dir:
            cls.temp_dir.cleanup()

    def test_01_store_integrity_and_isolation(self):
        # Ensure tests are running in isolated temp repo
        self.assertTrue(self.store.repo_root.startswith(self.temp_repo))
        self.assertEqual(len(self.store.source_memes), EXPECTED_TOTAL_ITEMS)
        self.assertEqual(len(self.store.annotations), EXPECTED_TOTAL_ITEMS)
        self.assertEqual(len(self.store.index_to_batch_id), EXPECTED_TOTAL_ITEMS)
        self.assertTrue(os.path.exists(self.store.source_path))

    def test_02_meta_endpoint(self):
        meta = self.store.get_meta()
        self.assertEqual(meta["total_items"], EXPECTED_TOTAL_ITEMS)
        self.assertEqual(meta["loaded_items"], EXPECTED_TOTAL_ITEMS)
        self.assertIn("dimensions", meta["taxonomy"])
        self.assertGreater(len(meta["manifest_batches"]), 0)
        self.assertGreater(len(meta["canonical_entities"]), 0)

    def test_03_stats_endpoint(self):
        stats = self.store.get_stats()
        self.assertEqual(stats["total"], EXPECTED_TOTAL_ITEMS)
        self.assertEqual(stats["annotated"], EXPECTED_TOTAL_ITEMS)
        self.assertIn("pending", stats["review_counts"])
        self.assertIn("safe", stats["safety_counts"])
        self.assertIn("streamer", stats["target_counts"])

    def test_04_query_pagination_and_filters(self):
        # Default page
        res = self.store.query_items(page=1, page_size=20)
        self.assertEqual(res["page"], 1)
        self.assertEqual(res["page_size"], 20)
        self.assertEqual(len(res["items"]), 20)
        self.assertEqual(res["total_matched"], EXPECTED_TOTAL_ITEMS)

        # Stance filter
        res_flame = self.store.query_items(page=1, page_size=10, stance="flame_streamer")
        self.assertGreater(res_flame["total_matched"], 0)
        for item in res_flame["items"]:
            self.assertIn("flame_streamer", item["stances"])

        # Preset filter: flame_streamer
        res_preset = self.store.query_items(page=1, page_size=10, preset="flame_streamer")
        self.assertEqual(res_preset["total_matched"], res_flame["total_matched"])

        # Preset filter: safe_with_flame (severity == safe and contains flame_*)
        res_safe_flame = self.store.query_items(page=1, page_size=50, preset="safe_with_flame")
        self.assertGreater(res_safe_flame["total_matched"], 0)
        for item in res_safe_flame["items"]:
            self.assertEqual(item["safety"]["severity"], "safe")
            self.assertTrue(any(s.startswith("flame_") for s in item["stances"]))

        # Query text search
        res_text = self.store.query_items(page=1, page_size=10, query="玩机器")
        self.assertGreater(res_text["total_matched"], 0)

    def test_05_single_item_retrieval(self):
        item = self.store.get_single_item(1)
        self.assertIsNotNone(item)
        self.assertEqual(item["index"], 1)
        self.assertIn("raw_text", item)
        self.assertIn("annotation", item)
        self.assertEqual(item["annotation"]["index"], 1)

        # Non-existent index
        self.assertIsNone(self.store.get_single_item(999999))

    def test_06_validation_positive_and_negative(self):
        # Valid entry
        valid_entry = {
            "index": 1,
            "targets": ["streamer"],
            "stances": ["tease_playful"],
            "topics": ["streamer_appearance_pig_weight"],
            "formats": ["plain_statement"],
            "culture": ["origin_6657"],
            "entities": [{"name": "玩机器", "type": "streamer"}],
            "context": "standalone",
            "safety": {"severity": "safe", "flags": ["none"]},
            "confidence": 1.0,
            "review": {"status": "pending"}
        }
        ok, errs = self.store.validate_entry(valid_entry)
        self.assertTrue(ok, f"Expected valid, got errors: {errs}")

        # Negative 1: Raw text leakage
        bad_entry_text = dict(valid_entry)
        bad_entry_text["text"] = "should not be here"
        ok, errs = self.store.validate_entry(bad_entry_text)
        self.assertFalse(ok)
        self.assertTrue(any("forbidden field 'text'" in e for e in errs))

        # Negative 2: Anti-spoofing (pending with reviewer)
        bad_entry_review = dict(valid_entry)
        bad_entry_review["review"] = {"status": "pending", "reviewer": "alice"}
        ok, errs = self.store.validate_entry(bad_entry_review)
        self.assertFalse(ok)
        self.assertTrue(any("Spoofed review" in e for e in errs))

        # Negative 3: Safety contradiction (profanity with safe severity)
        bad_entry_safety = dict(valid_entry)
        bad_entry_safety["safety"] = {"severity": "safe", "flags": ["profanity"]}
        ok, errs = self.store.validate_entry(bad_entry_safety)
        self.assertFalse(ok)
        self.assertTrue(any("Safety contradiction" in e for e in errs))

        # Negative 4: Flame stance without corresponding target
        bad_entry_flame = dict(valid_entry)
        bad_entry_flame["targets"] = ["chat_audience"]
        bad_entry_flame["stances"] = ["flame_streamer"]
        ok, errs = self.store.validate_entry(bad_entry_flame)
        self.assertFalse(ok)
        self.assertTrue(any("requires target 'streamer'" in e for e in errs))

    def test_07_save_item_atomic_and_backup_in_sandbox(self):
        # Modify item 1 in sandbox
        original_item = self.store.get_single_item(1)
        orig_annotation = json.loads(json.dumps(original_item["annotation"]))

        modified_annotation = json.loads(json.dumps(orig_annotation))
        modified_annotation["review"] = {
            "status": "reviewed",
            "reviewer": "test_suite_reviewer",
            "comments": "isolated automation test"
        }
        modified_annotation["confidence"] = 0.99

        ok, errs = self.store.save_item(1, modified_annotation)
        self.assertTrue(ok, f"Save failed with: {errs}")

        # Verify in-memory updated
        curr = self.store.get_single_item(1)
        self.assertEqual(curr["annotation"]["review"]["status"], "reviewed")
        self.assertEqual(curr["annotation"]["review"]["reviewer"], "test_suite_reviewer")
        self.assertEqual(curr["annotation"]["confidence"], 0.99)

        # Verify sandbox batch file updated
        batch_1_path = os.path.join(self.temp_repo, "Widget", "Danmaku", "Annotation", "batches", "batch_001.json")
        with open(batch_1_path, "r", encoding="utf-8") as f:
            bdata = json.load(f)
        self.assertEqual(bdata["annotations"][0]["review"]["status"], "reviewed")

        # Verify sandbox backup directory created
        backups = os.listdir(self.store.backup_dir)
        self.assertGreater(len(backups), 0)

        # Verify real repository has NOT been touched
        real_batch_1 = os.path.join(REAL_REPO_ROOT, "Widget", "Danmaku", "Annotation", "batches", "batch_001.json")
        with open(real_batch_1, "r", encoding="utf-8") as f:
            real_bdata = json.load(f)
        self.assertEqual(real_bdata["annotations"][0]["review"]["status"], "pending")

    def test_08_transactional_save_fault_injection(self):
        """
        Fault-injection test:
        Simulate an unexpected failure during merged file atomic replacement.
        Verify that:
        1. Batch file is rolled back to original state.
        2. Merged file remains in original state.
        3. Memory state remains in original state.
        """
        target_idx = 10
        item_before = self.store.get_single_item(target_idx)
        orig_annotation = json.loads(json.dumps(item_before["annotation"]))
        batch_path = os.path.join(self.temp_repo, "Widget", "Danmaku", "Annotation", "batches", "batch_001.json")
        merged_path = os.path.join(self.temp_repo, "Widget", "Danmaku", "Annotation", "6657_annotations_v1.json")

        with open(batch_path, "r", encoding="utf-8") as f:
            batch_data_before = json.load(f)
        with open(merged_path, "r", encoding="utf-8") as f:
            merged_data_before = json.load(f)

        fault_annotation = json.loads(json.dumps(orig_annotation))
        fault_annotation["review"] = {
            "status": "reviewed",
            "reviewer": "fault_injector",
            "comments": "this change must be rolled back"
        }

        real_os_replace = os.replace

        def mock_replace(src, dst):
            # If replacing merged file, inject failure
            if os.path.abspath(dst) == os.path.abspath(merged_path):
                raise OSError("Simulated Disk Full during merged write replacement")
            return real_os_replace(src, dst)

        with patch("os.replace", side_effect=mock_replace):
            ok, errs = self.store.save_item(target_idx, fault_annotation)
            self.assertFalse(ok, "Expected save_item to fail due to injected failure")
            self.assertTrue(any("Transactional write failed and rolled back" in err for err in errs))

        # 1. Verify in-memory state remained unchanged
        curr_item = self.store.get_single_item(target_idx)
        self.assertEqual(curr_item["annotation"]["review"]["status"], orig_annotation.get("review", {}).get("status"))

        # 2. Verify batch file content on disk was rolled back cleanly
        with open(batch_path, "r", encoding="utf-8") as f:
            batch_data_after = json.load(f)
        self.assertEqual(batch_data_after["annotations"][target_idx - 1]["review"]["status"], orig_annotation.get("review", {}).get("status"))

        # 3. Verify merged file content on disk remained intact
        with open(merged_path, "r", encoding="utf-8") as f:
            merged_data_after = json.load(f)
        self.assertEqual(merged_data_after["annotations"][target_idx - 1]["review"]["status"], orig_annotation.get("review", {}).get("status"))


class TestAnnotationHTTPServerIsolated(unittest.TestCase):
    temp_dir: tempfile.TemporaryDirectory = None
    temp_repo: str = ""
    server = None
    thread = None
    port: int = 0

    @classmethod
    def setUpClass(cls):
        cls.temp_dir = tempfile.TemporaryDirectory(prefix="test_danmaku_http_")
        cls.temp_repo = cls.temp_dir.name

        src_danmaku = os.path.join(REAL_REPO_ROOT, "Widget", "Danmaku")
        dst_danmaku = os.path.join(cls.temp_repo, "Widget", "Danmaku")
        os.makedirs(os.path.dirname(dst_danmaku), exist_ok=True)
        shutil.copytree(src_danmaku, dst_danmaku, ignore=shutil.ignore_patterns("__pycache__", ".backups"))

        cls.port = get_free_port()
        cls.server = create_server(cls.temp_repo, host="127.0.0.1", port=cls.port)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        # Allow server thread to initialize
        for _ in range(20):
            try:
                with socket.create_connection(("127.0.0.1", cls.port), timeout=0.1):
                    break
            except OSError:
                threading.Event().wait(0.05)

    @classmethod
    def tearDownClass(cls):
        if cls.server:
            cls.server.shutdown()
            cls.server.server_close()
        if cls.temp_dir:
            cls.temp_dir.cleanup()

    def _url(self, path: str) -> str:
        return f"http://127.0.0.1:{self.port}{path}"

    def test_http_01_index_html(self):
        req = urllib.request.Request(self._url("/"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            content = resp.read().decode("utf-8")
            self.assertIn("6657 弹幕语义标注审阅系统", content)

    def test_http_02_get_meta(self):
        req = urllib.request.Request(self._url("/api/meta"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertTrue(data["success"])
            self.assertEqual(data["data"]["total_items"], EXPECTED_TOTAL_ITEMS)

    def test_http_03_get_stats(self):
        req = urllib.request.Request(self._url("/api/stats"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertTrue(data["success"])
            self.assertEqual(data["data"]["total"], EXPECTED_TOTAL_ITEMS)

    def test_http_04_get_items_query(self):
        req = urllib.request.Request(self._url("/api/items?page=1&page_size=5"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertTrue(data["success"])
            self.assertEqual(len(data["data"]["items"]), 5)

    def test_http_05_get_and_post_item_in_sandbox(self):
        # GET item 2
        req = urllib.request.Request(self._url("/api/item/2"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertTrue(data["success"])
            self.assertEqual(data["data"]["index"], 2)

        # POST invalid item (anti-spoofing rejection)
        bad_payload = dict(data["data"]["annotation"])
        bad_payload["review"] = {"status": "pending", "reviewer": "fake_admin"}
        post_req = urllib.request.Request(
            self._url("/api/item/2"),
            data=json.dumps(bad_payload).encode("utf-8"),
            headers={"Content-Type": "application/json"},
            method="POST"
        )
        try:
            with urllib.request.urlopen(post_req) as resp:
                self.fail("Expected HTTP 400 for spoofed pending reviewer, but got success")
        except urllib.error.HTTPError as e:
            self.assertEqual(e.code, 400)
            err_body = json.loads(e.read().decode("utf-8"))
            self.assertFalse(err_body["success"])
            self.assertTrue(any("Spoofed review" in err for err in err_body["errors"]))

    def test_http_06_preset_safe_with_flame(self):
        req = urllib.request.Request(self._url("/api/items?preset=safe_with_flame&page=1&page_size=20"))
        with urllib.request.urlopen(req) as resp:
            self.assertEqual(resp.status, 200)
            data = json.loads(resp.read().decode("utf-8"))
            self.assertTrue(data["success"])
            self.assertGreater(data["data"]["total_matched"], 0)
            for item in data["data"]["items"]:
                self.assertEqual(item["safety"]["severity"], "safe")
                self.assertTrue(any(s.startswith("flame_") for s in item["stances"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
