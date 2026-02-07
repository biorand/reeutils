import struct
import os
from pathlib import Path

aimap_folder = r"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\sectionroot\leveldesign\aimap"

def analyze_point_data(filepath):
    """Deep analysis of point data structure."""
    with open(filepath, 'rb') as f:
        data = f.read()
    
    filename = Path(filepath).name
    print(f"\n{'='*80}\nFILE: {filename}\n{'='*80}")
    
    # Find RSZ magic
    rsz_offset = data.find(b'RSZ\x00')
    print(f"RSZ at: 0x{rsz_offset:X}")
    
    # Find via. pattern
    via_offset = data.find(b'v\x00i\x00a\x00.\x00')
    if via_offset < 0:
        print("No type name found!")
        return
    
    # Read type name
    type_name = ''
    for i in range(via_offset, min(via_offset + 200, len(data)), 2):
        if data[i] == 0 and data[i+1] == 0:
            break
        type_name += chr(data[i] | (data[i+1] << 8))
    
    print(f"TypeName: {type_name}")
    print(f"TypeName at: 0x{via_offset:X}")
    
    # Calculate where type name ends
    type_name_end = via_offset + (len(type_name) + 1) * 2
    type_name_end_aligned = (type_name_end + 7) & ~7
    
    print(f"TypeName ends at: 0x{type_name_end:X} (aligned: 0x{type_name_end_aligned:X})")
    print(f"Data between TypeName end and RSZ: {rsz_offset - type_name_end_aligned} bytes")
    
    # Show hex dump of data after type name
    print(f"\nData after TypeName (0x{type_name_end:X} - 0x{min(type_name_end+256, rsz_offset):X}):")
    for i in range(type_name_end, min(type_name_end + 256, rsz_offset), 16):
        hex_bytes = ' '.join(f'{data[i+j]:02X}' for j in range(min(16, len(data)-i)))
        
        # Try to interpret as floats
        floats = []
        for j in range(0, 16, 4):
            if i + j + 4 <= len(data):
                try:
                    f_val = struct.unpack('<f', data[i+j:i+j+4])[0]
                    if abs(f_val) < 10000 and f_val != 0:
                        floats.append(f"{f_val:.1f}")
                    else:
                        floats.append("--")
                except:
                    floats.append("--")
        
        print(f"  {i:04X}: {hex_bytes}  | floats: {' '.join(floats)}")
    
    # Show first few bytes of RSZ
    print(f"\nRSZ header (0x{rsz_offset:X}):")
    rsz_header = data[rsz_offset:rsz_offset+48]
    for i in range(0, len(rsz_header), 16):
        hex_bytes = ' '.join(f'{rsz_header[i+j]:02X}' for j in range(min(16, len(rsz_header)-i)))
        print(f"  {rsz_offset+i:04X}: {hex_bytes}")
    
    # Parse RSZ header
    if rsz_offset + 32 <= len(data):
        rsz_magic = data[rsz_offset:rsz_offset+4]
        rsz_version = struct.unpack('<I', data[rsz_offset+4:rsz_offset+8])[0]
        rsz_obj_count = struct.unpack('<I', data[rsz_offset+8:rsz_offset+12])[0]
        rsz_inst_count = struct.unpack('<I', data[rsz_offset+12:rsz_offset+16])[0]
        
        print(f"\nRSZ: version={rsz_version}, objects={rsz_obj_count}, instances={rsz_inst_count}")
        
        # Compare instance count with "point count"
        my_point_count = (rsz_offset - type_name_end_aligned) // 24
        print(f"My estimated point count (24-byte): {my_point_count}")
        print(f"RSZ instance count: {rsz_inst_count}")

# Analyze a few files
files_to_check = [
    'location_rpd.aimap.27',  # Large file
    'location_laboratory_licker_around.aimap.27',  # Small with known points
    'location_rpd_tyrant_around.aimap.27',  # Known working
]

for fname in files_to_check:
    fpath = os.path.join(aimap_folder, fname)
    if os.path.exists(fpath):
        analyze_point_data(fpath)
