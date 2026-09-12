"""Repair the known U+6307 outline using radicals from the same font.

Requires fonttools and Pillow. Writes a candidate and evidence, never the input.
Usage: python tools/font-repair/repair_text_c.py --source Assets/Font/text-c.ttf
"""
import argparse
import copy
import hashlib
import io
import json
import struct
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.ttLib.sfnt import SFNTWriter
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphCoordinates
from fontTools.ttLib.tables.ttProgram import Program
from PIL import Image, ImageDraw, ImageFont


def contours(font, name):
    glyph = font["glyf"][name]
    coordinates, ends, flags = glyph.getCoordinates(font["glyf"])
    start = 0
    for end in ends:
        yield list(coordinates[start:end + 1]), list(flags[start:end + 1])
        start = end + 1


def raster(font_path, character, size):
    mask = ImageFont.truetype(str(font_path), size).getmask(character)
    return mask.size, bytes(mask)


def render_comparison(before, after, output):
    image = Image.new("RGB", (1250, 480), (30, 35, 40))
    draw = ImageDraw.Draw(image)
    for column, path in enumerate((before, after)):
        x = 20 + column * 620
        draw.text((x, 12), "BEFORE" if column == 0 else "AFTER", fill="white")
        for y, size, text in ((45, 32, "指令下达成功"),
                              (100, 32, "指令下达失败：目标不可达"),
                              (170, 64, "指令下达成功"),
                              (280, 120, "指 址")):
            draw.text((x, y), text, font=ImageFont.truetype(str(path), size),
                      fill=(190, 255, 210))
    image.save(output)


def repair(source, output):
    output.mkdir(parents=True, exist_ok=True)
    candidate = output / "text-c.ttf"
    if source.resolve() == candidate.resolve():
        raise ValueError("Output must differ from the source font")
    font = TTFont(source, recalcTimestamp=False)
    cmap = font.getBestCmap()
    target, wrong = cmap[0x6307], cmap[0x5740]
    if any(raster(source, "指", size) != raster(source, "址", size)
           for size in (32, 64, 120)):
        raise ValueError("Source is not the known broken font; refusing to patch")
    original_glyphs = {name: font["glyf"][name].compile(font["glyf"])
                       for name in font.getGlyphOrder()}
    original_metrics = copy.deepcopy(font["hmtx"].metrics)
    original_cmaps = [copy.deepcopy(table.cmap) for table in font["cmap"].tables]

    # Keep complete contours (including holes), their winding and native positions.
    hand = [(points, flags) for points, flags in contours(font, cmap[0x62CD])
            if max(x for x, _ in points) < 400]  # 拍: 扌
    right = [(points, flags) for points, flags in contours(font, cmap[0x8102])
             if min(x for x, _ in points) > 400]  # 脂: 旨
    if len(hand) != 1 or len(right) != 4:
        raise ValueError("Unexpected donor outlines; review font version first")
    glyph = Glyph()
    glyph.numberOfContours = len(hand) + len(right)
    glyph.coordinates = GlyphCoordinates()
    glyph.endPtsOfContours = []
    glyph.flags = bytearray()
    for points, flags in hand + right:
        glyph.coordinates.extend(points)
        glyph.flags.extend(flags)
        glyph.endPtsOfContours.append(len(glyph.coordinates) - 1)
    # The old hint bytecode addresses the wrong glyph's points and cannot be reused.
    glyph.program = Program()
    glyph.program.fromBytecode([])
    glyph.recalcBounds(font["glyf"])
    font["glyf"][target] = glyph
    font["hmtx"][target] = (original_metrics[target][0], glyph.xMin)
    compiled_buffer = io.BytesIO()
    font.save(compiled_buffer)
    compiled = TTFont(io.BytesIO(compiled_buffer.getvalue()))
    baseline = TTFont(source)
    # Avoid unrelated cmap/post/hhea normalization performed by a full TTFont save.
    # SFNTWriter rebuilds offsets/checksums while copying all untouched table bytes.
    allowed_tables = {"glyf", "loca", "head", "hmtx", "maxp"}
    # Keep the original hhea.numberOfHMetrics representation instead of allowing
    # TTFont to compress repeated advances at the end of hmtx.
    metrics = bytearray(baseline.reader["hmtx"])
    target_id = baseline.getGlyphID(target)
    metric_count = baseline["hhea"].numberOfHMetrics
    bearing_offset = (target_id * 4 + 2 if target_id < metric_count else
                      metric_count * 4 + (target_id - metric_count) * 2)
    struct.pack_into(">h", metrics, bearing_offset, glyph.xMin)
    with candidate.open("wb") as stream:
        writer = SFNTWriter(stream, len(baseline.reader.tables), baseline.sfntVersion)
        for tag in baseline.reader.keys():
            writer[tag] = (bytes(metrics) if tag == "hmtx" else
                           compiled.reader[tag] if tag in allowed_tables else baseline.reader[tag])
        writer.close()

    fixed = TTFont(candidate, recalcTimestamp=False, checkChecksums=2)
    changed_glyphs = [name for name in fixed.getGlyphOrder()
                      if fixed["glyf"][name].compile(fixed["glyf"]) != original_glyphs[name]]
    changed_metrics = [name for name, value in fixed["hmtx"].metrics.items()
                       if value != original_metrics[name]]
    assert changed_glyphs == [target], changed_glyphs
    assert changed_metrics == [target], changed_metrics
    assert fixed.getGlyphOrder() == font.getGlyphOrder()
    assert [table.cmap for table in fixed["cmap"].tables] == original_cmaps
    changed_tables = [tag for tag in baseline.reader.keys()
                      if baseline.reader[tag] != fixed.reader[tag]]
    assert set(changed_tables) <= allowed_tables, changed_tables
    checks = {}
    for size in (32, 64, 120):
        assert raster(candidate, "指", size) != raster(candidate, "址", size)
        assert raster(source, "址", size) == raster(candidate, "址", size)
        for character in "令下达成功失败：目标不可拍脂":
            assert raster(source, character, size) == raster(candidate, character, size)
        checks[str(size)] = "distinct_target_unchanged_controls"
    render_comparison(source, candidate, output / "comparison.png")
    report = {
        "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "fixedSha256": hashlib.sha256(candidate.read_bytes()).hexdigest(),
        "glyphCount": len(fixed.getGlyphOrder()), "changedGlyphs": changed_glyphs,
        "changedMetrics": changed_metrics, "changedTables": changed_tables,
        "cmapUnchanged": True, "glyphOrderUnchanged": True,
        "targetAdvance": fixed["hmtx"][target][0],
        "sourceGlyphs": [cmap[0x62CD], cmap[0x8102]],
        "rasterChecks": checks, "status": "PASS"
    }
    (output / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("Logs/FontRepair/candidate"))
    args = parser.parse_args()
    repair(args.source, args.output)
