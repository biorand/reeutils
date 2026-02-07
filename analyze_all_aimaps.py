import struct
import os
from pathlib import Path

aimap_folder = r"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\sectionroot\leveldesign\aimap"
output_dir = r"e:\Projects\Library\reeutils\aimap_analysis"
os.makedirs(output_dir, exist_ok=True)

def read_utf16_string(data, start, max_len=256):
    """Read UTF-16LE string until null terminator, return (string, end_pos)."""
    result = []
    i = start
    while i < min(start + max_len, len(data) - 1):
        if data[i] == 0 and data[i+1] == 0:
            break
        result.append(chr(data[i] | (data[i+1] << 8)))
        i += 2
    return ''.join(result), i + 2

def analyze_aimap(filepath):
    """Analyze a single AIMAP file and return structured data."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    filename = Path(filepath).name
    result = {'filename': filename, 'filesize': len(data)}
    
    # Magic check
    if len(data) < 8 or data[0:4] != b'AIMP':
        result['error'] = 'Invalid magic'
        return result
    
    # Field at 0x04 - we now know this is NameLength
    name_length = struct.unpack('<I', data[4:8])[0]
    result['name_length'] = name_length
    
    # Map name at 0x08
    map_name, name_end = read_utf16_string(data, 8, name_length * 2 + 2)
    result['map_name'] = map_name
    result['name_actual_len'] = len(map_name)
    result['name_end_offset'] = name_end
    
    # Calculate where GUID should start (after name, aligned?)
    # Hypothesis 1: GUID immediately after null-terminated name
    # Hypothesis 2: GUID at fixed offset after name calculation
    
    # The name ends at 0x08 + (name_length * 2) bytes (UTF-16)
    calculated_name_end = 0x08 + (name_length * 2)
    result['calculated_name_end'] = f'0x{calculated_name_end:X}'
    
    # Look for potential GUID position - 16 bytes of non-zero data
    # Try different possible GUID start positions
    for guid_offset in [calculated_name_end, calculated_name_end + 4, (calculated_name_end + 7) & ~7]:
        if guid_offset + 16 <= len(data):
            guid_bytes = data[guid_offset:guid_offset+16]
            # Check if this looks like a valid GUID (not all zeros, not UTF-16 text)
            is_zero = all(b == 0 for b in guid_bytes)
            is_text = all((guid_bytes[i] == 0 and 32 <= guid_bytes[i-1] < 127) for i in range(1, 16, 2))
            if not is_zero and not is_text:
                result[f'guid_at_0x{guid_offset:X}'] = guid_bytes.hex()
    
    # Find RSZ magic
    rsz_magic = b'RSZ\x00'
    rsz_offset = data.find(rsz_magic)
    result['rsz_offset'] = f'0x{rsz_offset:X}' if rsz_offset >= 0 else 'NOT FOUND'
    result['rsz_offset_int'] = rsz_offset
    
    # Find type name "via." pattern
    via_pattern = b'v\x00i\x00a\x00.\x00'
    via_offset = data.find(via_pattern)
    if via_offset >= 0:
        type_name, _ = read_utf16_string(data, via_offset, 200)
        result['type_name'] = type_name
        result['type_name_offset'] = f'0x{via_offset:X}'
        
        # Check for length prefix BEFORE the type name
        if via_offset >= 4:
            possible_len = struct.unpack('<I', data[via_offset-4:via_offset])[0]
            if possible_len == len(type_name) or possible_len == len(type_name) + 1:
                result['type_name_len_prefix'] = f'{possible_len} at 0x{via_offset-4:X}'
    
    # Dump bytes from calculated_name_end to type_name_offset to understand middle structure
    if via_offset >= 0:
        middle_start = calculated_name_end
        middle_end = via_offset - 4  # Before type name length
        if middle_end > middle_start:
            result['middle_section_size'] = middle_end - middle_start
            
            # Try to interpret the middle section as uint32 values
            middle_values = []
            for off in range(middle_start, min(middle_end, middle_start + 64), 4):
                if off + 4 <= len(data):
                    val = struct.unpack('<I', data[off:off+4])[0]
                    middle_values.append(f'0x{off:02X}:{val}')
            result['middle_fields'] = ' | '.join(middle_values[:12])
    
    return result

# Analyze all files
print("=" * 100)
print("COMPREHENSIVE AIMAP FORMAT ANALYSIS")
print("=" * 100)

all_results = []
files = sorted(Path(aimap_folder).glob("*.aimap.*"))

for filepath in files:
    result = analyze_aimap(str(filepath))
    all_results.append(result)

# Print summary table
print("\n" + "=" * 100)
print("BASIC INFO")
print("=" * 100)
print(f"{'Filename':<45} {'Size':>10} {'NameLen':>8} {'MapName':<25} {'RSZ Offset':>12}")
print("-" * 100)
for r in all_results:
    print(f"{r['filename']:<45} {r['filesize']:>10} {r.get('name_length', '?'):>8} {r.get('map_name', '?')[:25]:<25} {r.get('rsz_offset', '?'):>12}")

# Print type names
print("\n" + "=" * 100)
print("TYPE NAMES FOUND")
print("=" * 100)
type_names = set()
for r in all_results:
    tn = r.get('type_name', '')
    if tn:
        type_names.add(tn)
for tn in sorted(type_names):
    print(f"  {tn}")

# Print middle section analysis to find patterns
print("\n" + "=" * 100)
print("MIDDLE SECTION FIELDS (between name and type_name)")
print("=" * 100)
for r in all_results[:10]:  # First 10 files
    print(f"\n{r['filename']}:")
    print(f"  NameEnd: {r.get('calculated_name_end', '?')}")
    print(f"  TypeNameOff: {r.get('type_name_offset', '?')}")
    print(f"  MiddleSize: {r.get('middle_section_size', '?')}")
    print(f"  Fields: {r.get('middle_fields', '?')}")

# Save complete dump to file
with open(os.path.join(output_dir, 'complete_analysis.txt'), 'w') as f:
    for r in all_results:
        f.write(f"\n{'='*60}\n{r['filename']}\n{'='*60}\n")
        for k, v in sorted(r.items()):
            f.write(f"  {k}: {v}\n")

print(f"\n\nComplete analysis saved to {output_dir}/complete_analysis.txt")
