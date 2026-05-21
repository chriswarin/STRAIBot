# scripts/fix_gbrain.py
#
# Reference artifact from the initial GBrain MCP integration session (2026-05-20).
#
# Problem it solved:
#   replace_string_in_file targeted only the first 3-line `using` block of
#   GBrainClient.cs and replaced it with a full rewrite. The tool appended the
#   new content before the old content rather than replacing the whole file,
#   producing two `namespace STRAIBot.Services.GBrain;` declarations and a
#   CS8954 compile error.
#
# Fix:
#   This script scanned the file for the second occurrence of the namespace
#   declaration and truncated everything from that line onward.
#
# Usage (from repo root — only needed if the dual-namespace issue recurs):
#   python scripts/fix_gbrain.py

import sys

path = r"STRAIBot\Services\GBrain\GBrainClient.cs"

with open(path, "r", encoding="utf-8") as f:
    lines = f.readlines()

cutoff = None
count = 0
for i, line in enumerate(lines):
    if line.strip() == "namespace STRAIBot.Services.GBrain;":
        count += 1
        if count == 2:
            cutoff = i
            break

if cutoff:
    lines = lines[:cutoff]
    with open(path, "w", encoding="utf-8") as f:
        f.writelines(lines)
    print(f"Truncated at line {cutoff}. File now {len(lines)} lines.")
else:
    print("No duplicate namespace found — file is already clean.")
