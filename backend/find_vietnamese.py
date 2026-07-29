import re
import sys

def find_vietnamese(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    with open('out.txt', 'a', encoding='utf-8') as out:
        out.write(f"--- {file_path} ---\n")
        for i, line in enumerate(lines):
            if re.search(r'"[^"]*[^\x00-\x7F][^"]*"', line):
                out.write(f"{i+1}: {line.strip()}\n")

if __name__ == "__main__":
    find_vietnamese(sys.argv[1])
