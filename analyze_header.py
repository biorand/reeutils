import struct
import os
from pathlib import Path

aimap_folder = r"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\sectionroot\leveldesign\aimap"

def hex_dump_section(data, start, end, label):
    """Create formatted hex dump of a section."""
    print(f"\n{label} (0x{start:04X} - 0x{end:04X}):")
    for i in range(start, min(end, len(data)), 16):
        hex_bytes = ' '.join(f'{data[i+j]:02X}' if i+j < end else '  ' for j in range(16))
        ascii_chars = ''.join(chr(data[i+j]) if 32 <= data[i+j] < 127 else '.' for j in range(16) if i+j < end)
        print(f"  {i:04X}: {hex_bytes}  {ascii_chars}")

def analyze_header(filepath):
    """Deep analysis of header structure."""
    with open(filepath, 'rb') as f:
        data = f.read(min(0x200, os.path.getsize(filepath)))  # First 512 bytes

    filename = Path(filepath).name
    print("\n" + "=" * 80)
    print(f"FILE: {filename}")
    print(f"Total size: {os.path.getsize(filepath)} bytes")
    print("=" * 80)
    
    # Basic header
    magic = data[0:4].decode('ascii')
    name_len = struct.unpack('<I', data[4:8])[0]
    print(f"\n0x00: Magic = '{magic}'")
    print(f"0x04: NameLength = {name_len} (chars including null)")
    
    # Read name
    name_chars = []
    for i in range(8, 8 + name_len * 2, 2):
        if data[i] == 0 and data[i+1] == 0:
            break
        name_chars.append(chr(data[i] | (data[i+1] << 8)))
    map_name = ''.join(name_chars)
    name_byte_end = 8 + name_len * 2
    
    print(f"0x08: MapName = '{map_name}' ({len(map_name)} chars)")
    print(f"      Name ends at byte offset 0x{name_byte_end:X}")
    
    # What comes after the name?
    print(f"\nBytes after name (0x{name_byte_end:X} onwards):")
    
    # Check alignment - data likely aligned to 8 or 16 bytes
    aligned_8 = (name_byte_end + 7) & ~7
    aligned_16 = (name_byte_end + 15) & ~15
    
    print(f"  8-byte aligned: 0x{aligned_8:X}")
    print(f"  16-byte aligned: 0x{aligned_16:X}")
    
    # Read uint32 values after the name
    print(f"\nUint32 fields starting at 0x{name_byte_end:X}:")
    for off in range(name_byte_end, min(name_byte_end + 80, len(data)), 4):
        val = struct.unpack('<I', data[off:off+4])[0]
        # Also show as signed and float
        val_signed = struct.unpack('<i', data[off:off+4])[0]
        try:
            val_float = struct.unpack('<f', data[off:off+4])[0]
            float_str = f"{val_float:.2f}" if abs(val_float) < 1e10 else "huge"
        except:
            float_str = "?"
        
        # Check if this could be an offset (reasonable value for file)
        is_offset = 0 < val < os.path.getsize(filepath)
        
        print(f"  0x{off:02X}: {val:10} (0x{val:08X}) {'<-- possible offset' if is_offset else ''}")
    
    # Find RSZ magic
    full_data = open(filepath, 'rb').read()
    rsz_off = full_data.find(b'RSZ\x00')
    print(f"\nRSZ magic found at: 0x{rsz_off:X} ({rsz_off})")
    
    # Find "via." pattern
    via_off = full_data.find(b'v\x00i\x00a\x00.\x00')
    if via_off >= 0:
        print(f"via. pattern found at: 0x{via_off:X}")
        # Read type name
        type_chars = []
        for i in range(via_off, min(via_off + 200, len(full_data)), 2):
            if full_data[i] == 0 and full_data[i+1] == 0:
                break
            type_chars.append(chr(full_data[i] | (full_data[i+1] << 8)))
        type_name = ''.join(type_chars)
        print(f"TypeName: '{type_name}' ({len(type_name)} chars)")
        
        # Check length prefix
        if via_off >= 4:
            len_prefix = struct.unpack('<I', full_data[via_off-4:via_off])[0]
            print(f"Length prefix at 0x{via_off-4:X}: {len_prefix} (TypeName len = {len(type_name)})")
    
    # Hex dump of header
    hex_dump_section(data, 0, min(0x100, len(data)), "Full Header Hex Dump")
    
    return {
        'filename': filename,
        'name_len': name_len,
        'map_name': map_name,
        'name_byte_end': name_byte_end,
        'rsz_off': rsz_off,
        'via_off': via_off if via_off >= 0 else None
    }

# Analyze a few representative files
files_to_analyze = [
    'location_rpd_b1.aimap.27',  # 712KB - short name (6 chars)
    'location_rpd_tyrant_around.aimap.27',  # 18KB - longer name  
    'location_laboratory_tyrant_around.aimap.27',  # 1.4KB - smallest
    'location_orphanapproach_dog_around.aimap.27',  # Small
]

results = []
for fname in files_to_analyze:
    fpath = os.path.join(aimap_folder, fname)
    if os.path.exists(fpath):
        results.append(analyze_header(fpath))

print("\n\n" + "=" * 80)
print("COMPARISON SUMMARY")
print("=" * 80)
print(f"{'Filename':<45} {'NameLen':>8} {'NameEnd':>10} {'RSZ':>10} {'via.':>10}")
for r in results:
    via_str = f"0x{r['via_off']:X}" if r['via_off'] else "N/A"
    print(f"{r['filename']:<45} {r['name_len']:>8} 0x{r['name_byte_end']:02X}      0x{r['rsz_off']:X}      {via_str}")
