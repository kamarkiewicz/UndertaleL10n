import struct
import sys

def read_chunks(path):
    with open(path, "rb") as f:
        data = f.read()
    assert data[0:4] == b"FORM", data[0:4]
    total_len = struct.unpack_from("<I", data, 4)[0]
    pos = 8
    end = 8 + total_len
    chunks = {}
    while pos < end:
        tag = data[pos:pos+4].decode("ascii")
        length = struct.unpack_from("<I", data, pos+4)[0]
        chunk_data_start = pos + 8
        chunks[tag] = (chunk_data_start, length)
        pos = chunk_data_start + length
    return data, chunks

def parse_strg(data, offset, length):
    count = struct.unpack_from("<I", data, offset)[0]
    print("STRG count field:", count, "chunk length:", length, flush=True)
    if count < 0 or count > 500000:
        raise ValueError(f"Suspicious STRG count: {count}")
    ptrs = struct.unpack_from(f"<{count}I", data, offset + 4)
    strings = []
    mismatches = 0
    for i, p in enumerate(ptrs):
        str_len = struct.unpack_from("<I", data, p)[0]
        if str_len < 0 or str_len > 10_000_000 or p + 4 + str_len > len(data):
            print(f"BAD ENTRY at index {i}: ptr={p} str_len={str_len} filelen={len(data)}", flush=True)
            raise ValueError("bad string entry")
        # Authoritative terminator is the actual NUL byte, not the declared
        # length field: tools that re-encode ASCII text as UTF-8 in place
        # (e.g. injecting accented characters) can leave a stale length
        # that undercounts the real byte size, while the NUL terminator
        # still marks the true end.
        real_end = data.index(b"\x00", p + 4)
        if real_end != p + 4 + str_len:
            mismatches += 1
        s = data[p+4:real_end]
        strings.append(s.decode("utf-8", errors="replace"))
    if mismatches:
        print(f"Note: {mismatches} entries had a declared length that didn't match their actual NUL terminator (recovered via NUL scan)", flush=True)
    return strings

if __name__ == "__main__":
    path = sys.argv[1]
    out_path = sys.argv[2]
    data, chunks = read_chunks(path)
    print("Top-level chunks found:", list(chunks.keys()), flush=True)
    if "STRG" not in chunks:
        print("NO STRG CHUNK FOUND")
        sys.exit(1)
    offset, length = chunks["STRG"]
    strings = parse_strg(data, offset, length)
    print("String count:", len(strings))
    with open(out_path, "w", encoding="utf-8") as f:
        for s in strings:
            f.write(s.replace("\n", "\\n").replace("\r", "\\r") + "\n")
