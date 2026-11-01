"""Read the .NET single-file manifest without launching or extracting the app."""
import json
import struct
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = path.read_bytes()
marker = bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')
position = data.find(marker)
if position < 8:
    raise SystemExit('Not a .NET single-file bundle')
cursor = struct.unpack_from('<q', data, position - 8)[0]

def number(fmt):
    global cursor
    result = struct.unpack_from(fmt, data, cursor)[0]
    cursor += struct.calcsize(fmt)
    return result

def string():
    global cursor
    length, shift = 0, 0
    while True:
        value = number('<B')
        length |= (value & 127) << shift
        if value < 128:
            break
        shift += 7
    result = data[cursor:cursor + length].decode('utf-8')
    cursor += length
    return result

major, minor, count = number('<I'), number('<I'), number('<I')
bundle_id = string()
if major >= 2:
    cursor += 40  # deps/runtimeconfig locations and bundle flags
entries = []
for _ in range(count):
    offset, size = number('<q'), number('<q')
    compressed = number('<q') if major >= 6 else 0
    kind, name = number('<B'), string()
    entries.append(dict(path=name, bytes=size, stored=compressed or size, compressed=compressed > 0, type=kind))
unwanted = [e['path'] for e in entries if e['path'].lower().endswith(('.pdb', '.xml', '.trx', '.log', '.cs', '.ps1'))]
print(json.dumps(dict(executable=str(path), bytes=len(data), bundle_version=f'{major}.{minor}', count=count,
                     compressed_entries=sum(e['compressed'] for e in entries), unwanted=unwanted,
                     largest=sorted(entries, key=lambda e: e['stored'], reverse=True)[:12], entries=entries), indent=2))
if unwanted:
    raise SystemExit('Unexpected development files in bundle')
