#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
6657 Danmaku Annotation Web GUI Launcher
Runs the local server on 127.0.0.1 and opens the browser.
"""

import os
import sys
import argparse
import webbrowser
import threading

sys.stdout.reconfigure(encoding='utf-8')

# Ensure annotation directory is in sys.path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# Annotation -> Danmaku -> Widget -> Repo Root (3 levels up)
REPO_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", ".."))

if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from server import create_server

def main():
    parser = argparse.ArgumentParser(description="Start 6657 Danmaku Annotation Web GUI")
    parser.add_argument("--repo-root", default=REPO_ROOT, help="Repository root directory")
    parser.add_argument("--host", default="127.0.0.1", help="Host address (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8765, help="Port to listen on (default: 8765)")
    parser.add_argument("--no-browser", action="store_true", help="Do not open browser automatically")

    args = parser.parse_args()

    print("=" * 66)
    print(" 🚀 6657 弹幕多维标注审阅与编辑系统 (Local Web GUI)")
    print("=" * 66)
    print(f" [仓库路径] {args.repo_root}")
    print(f" [监听地址] http://{args.host}:{args.port}")
    print(f" [源文件]   Widget/Danmaku/6657_memes.json (严格只读不可变)")
    print(f" [批次目录] Widget/Danmaku/Annotation/batches/ (48 批次自动同步)")
    print(f" [合并文件] Widget/Danmaku/Annotation/6657_annotations_v1.json")
    print(f" [备份目录] Widget/Danmaku/Annotation/.backups/")
    print("=" * 66)

    server = create_server(args.repo_root, host=args.host, port=args.port)
    url = f"http://{args.host}:{args.port}"

    if not args.no_browser:
        print(" -> 正在调起默认浏览器打开 GUI 页面...")
        threading.Timer(0.8, lambda: webbrowser.open(url)).start()

    print(" -> 服务运行中... (按 Ctrl+C 可停止服务)\n")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[INFO] 已接收中断信号，GUI 服务已安全退出。")
        server.server_close()

if __name__ == "__main__":
    main()
