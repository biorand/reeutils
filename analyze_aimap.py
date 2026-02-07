import struct

with open(r'e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_rpd_tyrant_around.aimap.27', 'rb') as f:
    data = f.read()

print('=== AIMAP HEADER ANALYSIS ===')
print()

# 0x00-0x03: Magic
magic = data[0:4].decode('ascii')
print(f'0x00: Magic = "{magic}"')

# 0x04-0x07: Version (uint32)
version = struct.unpack('<I', data[4:8])[0]
print(f'0x04: Version = {version} (0x{version:X})')

# 0x08-0x2B: Map Name (UTF-16LE, null-terminated)
# Find the double-null terminator (UTF-16 end), search every 2 bytes
i = 8
while i < 0x2C:
    if data[i] == 0 and data[i+1] == 0:
        break
    i += 2
name_end = i
map_name = data[8:name_end].decode('utf-16le')
print(f'0x08: Map Name = "{map_name}" (ends at 0x{name_end:X})')

# 0x2C: Unknown uint32
unk1 = struct.unpack('<I', data[0x2C:0x30])[0]
print(f'0x2C: Unknown1 = {unk1}')

# 0x30-0x3F: GUID (16 bytes)
guid_bytes = data[0x30:0x40]
# Standard GUID format
import uuid
guid = uuid.UUID(bytes_le=guid_bytes)
print(f'0x30: GUID = {guid}')

# 0x40-0x47: Padding or reserved
pad1 = struct.unpack('<Q', data[0x40:0x48])[0]
print(f'0x40: Padding/Reserved = {pad1}')

# 0x48-0x4F: DataOffset (RSZ data offset as uint64)
data_offset = struct.unpack('<Q', data[0x48:0x50])[0]
print(f'0x48: Data Offset (RSZ?) = {data_offset} (0x{data_offset:X})')

# 0x50-0x57: Padding
pad2 = struct.unpack('<Q', data[0x50:0x58])[0]
print(f'0x50: Padding = {pad2}')

# 0x58-0x5F: Points Offset?
points_offset = struct.unpack('<Q', data[0x58:0x60])[0]
print(f'0x58: Points Offset? = {points_offset} (0x{points_offset:X})')

# 0x60-0x67
val60 = struct.unpack('<Q', data[0x60:0x68])[0]
print(f'0x60: Value = {val60} (0x{val60:X})')

# 0x68-0x6F
val68 = struct.unpack('<Q', data[0x68:0x70])[0]
print(f'0x68: Value = {val68} (0x{val68:X})')

# 0x70-0x77
val70 = struct.unpack('<Q', data[0x70:0x78])[0]
print(f'0x70: Value = {val70} (0x{val70:X})')

# 0x78-0x7B: Point Count?
point_count = struct.unpack('<I', data[0x78:0x7C])[0]
print(f'0x78: Point Count? = {point_count}')

# 0x7C-0x7F: Padding
pad3 = struct.unpack('<I', data[0x7C:0x80])[0]
print(f'0x7C: Value = {pad3}')

# 0x80+: Type Name String (length-prefixed?)
type_name_len = struct.unpack('<I', data[0x80:0x84])[0]
print(f'0x80: Type Name Length = {type_name_len}')

type_name_start = 0x84
type_name_end = type_name_start + type_name_len * 2  # UTF-16
type_name = data[type_name_start:type_name_end].decode('utf-16le').rstrip('\x00')
print(f'0x84: Type Name = "{type_name}"')

# After type name: Point data?
print()
print('=== CHECKING DATA AT OFFSETS ===')

# Check what's at data_offset
if 0 < data_offset < len(data):
    preview = data[data_offset:data_offset+32]
    hex_preview = ' '.join(f'{b:02X}' for b in preview)
    ascii_preview = ''.join(chr(b) if 32 <= b < 127 else '.' for b in preview)
    print(f'At DataOffset 0x{data_offset:X}: {hex_preview}')
    print(f'                         ASCII: {ascii_preview}')

# Check what's at points_offset
if 0 < points_offset < len(data):
    preview = data[points_offset:points_offset+32]
    hex_preview = ' '.join(f'{b:02X}' for b in preview)
    print(f'At PointsOffset 0x{points_offset:X}: {hex_preview}')

# The RSZ magic should be RSZ\0 = 52 53 5A 00
rsz_search = b'RSZ\x00'
rsz_pos = data.find(rsz_search)
print()
print(f'RSZ Magic found at: 0x{rsz_pos:X}' if rsz_pos >= 0 else 'RSZ Magic NOT found!')

# Print some context around expected RSZ location
print()
print('=== DATA AROUND 0x3FC0 (suspected RSZ location from earlier) ===')
for offset in [0x3DC0, 0x3E00, 0x3F00, 0x3FC0, 0x4000]:
    if offset < len(data):
        preview = data[offset:offset+16]
        hex_preview = ' '.join(f'{b:02X}' for b in preview)
        ascii_preview = ''.join(chr(b) if 32 <= b < 127 else '.' for b in preview)
        print(f'0x{offset:04X}: {hex_preview}  {ascii_preview}')
