import struct
import os

folder = r"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\sectionroot\leveldesign\aimap"

# Analyze component data structure
for fname in ['location_rpd_tyrant_around.aimap.27', 'location_laboratory_licker_around.aimap.27']:
    fpath = os.path.join(folder, fname)
    with open(fpath, 'rb') as f:
        data = f.read()
    
    print(f"\n{'='*70}\n{fname}\n{'='*70}")
    
    # Find via. pattern
    pattern = bytes([0x76, 0x00, 0x69, 0x00, 0x61, 0x00, 0x2E, 0x00])
    via_off = data.find(pattern)
    
    # Read type name
    type_end = via_off
    while type_end < len(data)-1 and not (data[type_end] == 0 and data[type_end+1] == 0):
        type_end += 2
    type_end += 2  # Include null
    
    type_name = data[via_off:type_end].decode('utf-16-le').rstrip('\x00')
    print(f"TypeName: {type_name}")
    print(f"TypeName ends at: 0x{type_end:X}")
    
    # Align to 4 bytes
    data_start = (type_end + 3) & ~3
    print(f"Data starts at: 0x{data_start:X}")
    
    # Read first few uint32 values
    print(f"\nFirst uint32 values at data start:")
    for i in range(10):
        off = data_start + i * 4
        if off + 4 <= len(data):
            val = struct.unpack('<I', data[off:off+4])[0]
            print(f"  [{i}] 0x{off:04X}: {val}")
    
    # Find RSZ
    rsz_off = data.find(b'RSZ\x00')
    print(f"\nRSZ at: 0x{rsz_off:X}")
    
    # Parse RSZ header
    inst_count = struct.unpack('<I', data[rsz_off+12:rsz_off+16])[0]
    print(f"RSZ instance count: {inst_count}")
    
    # First value after typename should be count
    first_val = struct.unpack('<I', data[data_start:data_start+4])[0]
    print(f"\nFirst value ({first_val}) vs RSZ instances ({inst_count})")
    
    # Calculate element size
    data_size = rsz_off - data_start
    if first_val > 0:
        elem_size = data_size / first_val
        print(f"Data size: {data_size} / {first_val} = {elem_size:.1f} bytes per element")
    
    # Show hex of first few elements
    print(f"\nFirst 3 elements (if 24 bytes each):")
    for i in range(min(3, first_val)):
        elem_start = data_start + 4 + i * 24  # 4 for count prefix
        hex_str = ' '.join(f'{data[elem_start+j]:02X}' for j in range(24))
        
        # Parse as 3 floats + 3 ints
        f1 = struct.unpack('<f', data[elem_start:elem_start+4])[0]
        f2 = struct.unpack('<f', data[elem_start+4:elem_start+8])[0]
        f3 = struct.unpack('<f', data[elem_start+8:elem_start+12])[0]
        i1 = struct.unpack('<I', data[elem_start+12:elem_start+16])[0]
        i2 = struct.unpack('<I', data[elem_start+16:elem_start+20])[0]
        i3 = struct.unpack('<I', data[elem_start+20:elem_start+24])[0]
        
        print(f"  [{i}] Vec3({f1:.1f}, {f2:.1f}, {f3:.1f}) | {i1}, {i2}, {i3}")
