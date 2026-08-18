#!/usr/bin/env python3
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import threading

DEFAULT_MODEL = "opencode/deepseek-v4-flash-free"
RE9_PAK_PATH = "/mnt/c/Users/Ted/.biorand/biorand-re9_v4.pak"

CONTEXT = (
    "You are working with Resident Evil 9 (RE9).\n"
    f"Game pak file: {RE9_PAK_PATH}"
)

def parse_prompt_file(path):
    with open(path) as f:
        content = f.read()

    prompt_text = ""
    expected_text = ""

    prompt_idx = content.find("Prompt:")
    expected_idx = content.find("Expected:")

    if prompt_idx >= 0:
        start = prompt_idx + len("Prompt:")
        end = expected_idx if expected_idx >= 0 else len(content)
        prompt_text = content[start:end].strip()

    if expected_idx >= 0:
        expected_text = content[expected_idx + len("Expected:"):].strip()

    return prompt_text, expected_text


def indent(text, prefix="    "):
    return "\n".join(f"{prefix}{line}" for line in text.split("\n"))


def main():
    parser = argparse.ArgumentParser(
        description="Run an opencode prompt test against RE 9 game files"
    )
    parser.add_argument(
        "prompt_file", nargs="?",
        help="path to prompt file"
    )
    parser.add_argument(
        "-m", "--model", default=DEFAULT_MODEL,
        help="model name"
    )
    args = parser.parse_args()

    script_dir = os.path.dirname(os.path.abspath(__file__))
    prompt_path = args.prompt_file
    if not os.path.isabs(prompt_path):
        prompt_path = os.path.join(script_dir, prompt_path)

    if not os.path.exists(prompt_path):
        print(f"error: prompt file not found: {prompt_path}", file=sys.stderr)
        sys.exit(1)

    prompt_text, expected_text = parse_prompt_file(prompt_path)

    full_prompt = f"{CONTEXT}\n\n{prompt_text}"

    tmpdir = tempfile.mkdtemp(prefix="opencode_test_")
    try:
        proc = subprocess.Popen(
            ["opencode", "run", "-m", args.model, "--thinking", "--pure", full_prompt],
            cwd=tmpdir,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )

        out_lines = []

        def read_stream(src, capture):
            for line in src:
                sys.stderr.write(line)
                sys.stderr.flush()
                if capture is not None:
                    capture.append(line)

        t1 = threading.Thread(target=read_stream, args=(proc.stdout, out_lines))
        t2 = threading.Thread(target=read_stream, args=(proc.stderr, None))
        t1.start()
        t2.start()
        t1.join()
        t2.join()
        proc.wait()
    finally:
        shutil.rmtree(tmpdir, ignore_errors=True)

    output_text = "".join(out_lines)

    sys.stdout.write("<test>\n")
    sys.stdout.write(f"  <prompt>\n{indent(prompt_text)}\n")
    sys.stdout.write(f"  </prompt>\n")
    sys.stdout.write(f"  <output>\n{indent(output_text.strip())}\n")
    sys.stdout.write(f"  </output>\n")
    sys.stdout.write(f"  <expected>\n{indent(expected_text)}\n")
    sys.stdout.write(f"  </expected>\n")
    sys.stdout.write("</test>\n")
    sys.stdout.flush()

    sys.exit(proc.returncode)


if __name__ == "__main__":
    main()
