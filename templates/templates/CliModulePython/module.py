#!/usr/bin/env python3
import json
import pathlib
import sys

if sys.argv[1:] == ["--corevar-describe"]:
    print(pathlib.Path(__file__).with_name("corevar.module.json").read_text(encoding="utf-8"))
    raise SystemExit(0)

name = sys.argv[2] if len(sys.argv) > 2 and sys.argv[1] == "hello" else "world"
print(f"Hello, {name}!")
