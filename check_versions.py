import struct
import os

base = r'e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer'
files = [
    'location_laboratory_licker_around.aimap.27',
    'location_laboratory_tyrant_around.aimap.27', 
    'location_orphanapproach_dog_around.aimap.27',
    'location_orphanasylum_dog_around.aimap.27',
    'location_rpd_tyrant_around.aimap.27',
]

print("HEADER FIELD CORRELATION ANALYSIS")
print("="*70)

for f in files:
    path = os.path.join(base, f)
    with open(path, 'rb') as fp:
        data = fp.read(0x100)
    
    magic = data[0:4].decode('ascii')
    field_0x04 = struct.unpack('<I', data[4:8])[0]
    
    # Read map name
    name_chars = []
    i = 8
    while i < len(data) - 1:
        if data[i] == 0 and data[i+1] == 0:
            break
        name_chars.append(chr(data[i] | (data[i+1] << 8)))
        i += 2
    map_name = ''.join(name_chars)
    name_len = len(map_name)
    
    print(f"{f}:")
    print(f"  Field at 0x04: {field_0x04}")
    print(f"  Map Name: '{map_name}'")
    print(f"  Name Length: {name_len} chars")
    print(f"  Name Length + 1: {name_len + 1}")
    print(f"  MATCH: {'YES!' if field_0x04 == name_len + 1 else 'NO'}")
    print()
