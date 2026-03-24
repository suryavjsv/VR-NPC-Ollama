"""
fix_sherpa_cs9.py
-----------------
Fixes sherpa-onnx C# wrapper files to compile under C# 9.0 (Unity default).

The problem: C# 10.0 introduced parameterless struct constructors.
sherpa-onnx scripts use:
    public struct Foo {
        public Foo() { ... }   // <-- C# 10 only
    }

The fix: remove the parameterless constructor body and replace with
field initializers on the declarations, OR simply remove the empty
constructor if all it does is set defaults (sherpa-onnx structs
initialize via IntPtr/string fields that default to 0/null anyway).

Usage:
    1. Copy this script next to your Assets/ folder (project root)
    2. Run: python fix_sherpa_cs9.py
    3. Rebuild in Unity — all CS8773 errors should be gone
"""

import os
import re
import sys

# Path to the SherpaOnnx plugin folder — adjust if needed
PLUGIN_DIR = os.path.join("Assets", "Plugins", "SherpaOnnx")

def fix_file(path):
    with open(path, "r", encoding="utf-8") as f:
        original = f.read()

    # Remove parameterless constructors: `public StructName() { }` or with whitespace/newlines
    # Pattern: public <ClassName>() { } — possibly multiline with only whitespace inside
    fixed = re.sub(
        r'public\s+\w+\s*\(\s*\)\s*\{[^}]*\}\s*',
        '',
        original
    )

    if fixed != original:
        with open(path, "w", encoding="utf-8") as f:
            f.write(fixed)
        return True
    return False

def main():
    if not os.path.isdir(PLUGIN_DIR):
        print(f"ERROR: Could not find {PLUGIN_DIR}")
        print("Make sure you run this script from your Unity project root.")
        sys.exit(1)

    cs_files = [
        os.path.join(PLUGIN_DIR, f)
        for f in os.listdir(PLUGIN_DIR)
        if f.endswith(".cs")
    ]

    if not cs_files:
        print(f"No .cs files found in {PLUGIN_DIR}")
        sys.exit(1)

    fixed_count = 0
    for path in cs_files:
        if fix_file(path):
            print(f"  Fixed: {os.path.basename(path)}")
            fixed_count += 1

    print(f"\nDone. Fixed {fixed_count}/{len(cs_files)} files.")
    print("Now rebuild in Unity — CS8773 errors should be gone.")

if __name__ == "__main__":
    main()
