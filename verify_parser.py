import struct
import os
from pathlib import Path

# Test the same parsing logic in Python to verify before C# testing
aimap_folder = r"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\sectionroot\leveldesign\aimap"

def parse_aimap(filepath):
    """Parse AIMAP file with corrected algorithm."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    filename = Path(filepath).name
    
    if data[0:4] != b'AIMP':
        return {'error': 'Invalid magic'}
    
    name_length = struct.unpack('<I', data[4:8])[0]
    
    # Read name
    name_chars = []
    for i in range(8, 8 + name_length * 2, 2):
        if data[i] == 0 and data[i+1] == 0:
            break
        name_chars.append(chr(data[i] | (data[i+1] << 8)))
    map_name = ''.join(name_chars)
    
    # Name ends at 0x08 + name_length * 2
    name_byte_end = 8 + name_length * 2
    
    # GUID is at 16-byte aligned position after name
    guid_offset = (name_byte_end + 15) & ~15
    guid_bytes = data[guid_offset:guid_offset+16]
    
    # Parse GUID properly (little-endian format)
    # GUID structure: Data1 (4), Data2 (2), Data3 (2), Data4 (8)
    d1 = struct.unpack('<I', guid_bytes[0:4])[0]
    d2 = struct.unpack('<H', guid_bytes[4:6])[0]
    d3 = struct.unpack('<H', guid_bytes[6:8])[0]
    d4 = guid_bytes[8:16]
    guid_str = f"{d1:08x}-{d2:04x}-{d3:04x}-{d4[0]:02x}{d4[1]:02x}-{d4[2]:02x}{d4[3]:02x}{d4[4]:02x}{d4[5]:02x}{d4[6]:02x}{d4[7]:02x}"
    
    # Offsets base is after GUID
    offsets_base = guid_offset + 16
    
    # Find RSZ magic to verify
    rsz_magic = data.find(b'RSZ\x00')
    
    # Find via. pattern for type name
    via_pattern = b'v\x00i\x00a\x00.\x00'
    via_offset = data.find(via_pattern)
    type_name = ''
    if via_offset >= 0:
        for i in range(via_offset, min(via_offset + 200, len(data)), 2):
            if data[i] == 0 and data[i+1] == 0:
                break
            type_name += chr(data[i] | (data[i+1] << 8))
    
    # Read offset fields
    off = offsets_base
    fields = []
    for i in range(10):
        if off + 4 <= len(data):
            val = struct.unpack('<I', data[off:off+4])[0]
            fields.append((f'0x{off:02X}', val))
            off += 8  # 8-byte pairs
    
    # Find which field matches RSZ offset
    rsz_field_idx = -1
    for i, (pos, val) in enumerate(fields):
        if val == rsz_magic:
            rsz_field_idx = i
            break
    
    # Points estimation based on type name end
    if via_offset > 0:
        type_name_end = via_offset + (len(type_name) + 1) * 2
        type_name_end = (type_name_end + 7) & ~7  # Align
        points_data_size = rsz_magic - type_name_end
        point_count = points_data_size // 24
    else:
        type_name_end = 0
        point_count = 0
    
    return {
        'filename': filename,
        'name_length': name_length,
        'map_name': map_name,
        'guid': guid_str,
        'guid_offset': f'0x{guid_offset:X}',
        'offsets_base': f'0x{offsets_base:X}',
        'rsz_offset': f'0x{rsz_magic:X}',
        'type_name': type_name[:50] + '...' if len(type_name) > 50 else type_name,
        'via_offset': f'0x{via_offset:X}' if via_offset >= 0 else 'N/A',
        'points_offset': f'0x{type_name_end:X}',
        'point_count': point_count,
        'rsz_field_idx': rsz_field_idx,
    }

# Test with all files
print("AIMAP PARSER VERIFICATION")
print("=" * 120)

files = sorted(Path(aimap_folder).glob("*.aimap.*"))
results = []

for fpath in files[:10]:  # First 10 files
    r = parse_aimap(str(fpath))
    results.append(r)
    print(f"\n{r['filename']}:")
    print(f"  Name: '{r['map_name']}' (len={r['name_length']})")
    print(f"  GUID: {r['guid']} (at {r['guid_offset']})")
    print(f"  TypeName: {r['type_name']} (at {r['via_offset']})")
    print(f"  Points: {r['point_count']} at {r['points_offset']}")
    print(f"  RSZ: {r['rsz_offset']} (field idx {r['rsz_field_idx']})")

print("\n" + "=" * 120)
print("SUMMARY - RSZ Field Index (should be consistent)")
for r in results:
    print(f"  {r['filename']:<45} RSZ at field idx {r['rsz_field_idx']}")
