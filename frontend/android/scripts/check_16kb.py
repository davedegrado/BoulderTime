#!/usr/bin/env python3
"""
Checks that the app runs on Android devices with 16 KB memory pages, which Google Play requires of apps targeting
Android 15 and later. Two things must hold for every native library (.so) the package carries:

  * each ELF LOAD segment is aligned to at least 16 KB (that is how the library was linked);
  * in an APK, an uncompressed library starts at a 16 KB boundary inside the zip (that is how it was packaged).

Usage: check_16kb.py PACKAGE.apk|PACKAGE.aab [...]
Exits 1 if anything is not aligned. A package with no native libraries passes, and says so.
"""
import struct
import sys
import zipfile

PAGE = 16 * 1024
PT_LOAD = 1


def load_alignments(data: bytes):
    """The p_align of every LOAD segment of an ELF file."""
    if data[:4] != b"\x7fELF":
        raise ValueError("not an ELF file")
    is64 = data[4] == 2
    endian = "<" if data[5] == 1 else ">"
    if is64:
        phoff, = struct.unpack_from(endian + "Q", data, 0x20)
        phentsize, phnum = struct.unpack_from(endian + "HH", data, 0x36)
    else:
        phoff, = struct.unpack_from(endian + "I", data, 0x1C)
        phentsize, phnum = struct.unpack_from(endian + "HH", data, 0x2A)
    aligns = []
    for i in range(phnum):
        off = phoff + i * phentsize
        p_type, = struct.unpack_from(endian + "I", data, off)
        if p_type != PT_LOAD:
            continue
        align, = struct.unpack_from(endian + ("Q" if is64 else "I"), data, off + (0x30 if is64 else 0x1C))
        aligns.append(align)
    return aligns


def data_offset(zf: zipfile.ZipFile, info: zipfile.ZipInfo) -> int:
    """Where the entry's bytes start in the archive (after its local header)."""
    zf.fp.seek(info.header_offset)
    header = zf.fp.read(30)
    name_len, extra_len = struct.unpack("<HH", header[26:30])
    return info.header_offset + 30 + name_len + extra_len


def check(path: str) -> list:
    problems = []
    is_apk = path.endswith(".apk")
    with zipfile.ZipFile(path) as zf:
        libs = [i for i in zf.infolist() if i.filename.endswith(".so")]
        if not libs:
            print(f"{path}: no native libraries, nothing to align.")
            return problems
        for info in libs:
            # 32-bit ABIs are exempt: 16 KB pages exist only on 64-bit devices.
            if "/armeabi-v7a/" in info.filename or "/x86/" in info.filename:
                continue
            try:
                aligns = load_alignments(zf.read(info))
            except ValueError as e:
                problems.append(f"{info.filename}: {e}")
                continue
            bad = [a for a in aligns if a < PAGE]
            if bad:
                problems.append(f"{info.filename}: LOAD segments aligned to {min(bad)} bytes (needs {PAGE})")
            if is_apk and info.compress_type == zipfile.ZIP_STORED and data_offset(zf, info) % PAGE:
                problems.append(f"{info.filename}: stored uncompressed but not at a 16 KB boundary in the APK")
            print(f"{path}: {info.filename} ok" if not bad else f"{path}: {info.filename} NOT ALIGNED")
    return problems


def main(paths):
    if not paths:
        print(__doc__)
        return 2
    problems = [p for path in paths for p in check(path)]
    for p in problems:
        print(f"::error::{p}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
