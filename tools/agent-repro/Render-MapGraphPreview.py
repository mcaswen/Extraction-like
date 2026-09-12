"""Render Unity's measured layout evidence for visual review; this is not a HUD screenshot."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def render(evidence_path: Path, output_path: Path, width: int, height: int) -> None:
    data = json.loads(evidence_path.read_text(encoding="utf-8-sig"))
    if not data.get("nodes") or not data.get("zones"):
        raise ValueError("Evidence has no validated layout to render.")
    root = Path(__file__).resolve().parents[2]
    font_path = root / "Assets/Font/text-c.ttf"
    factor = 2
    image = Image.new("RGB", (width * factor, height * factor), "#11171b")
    draw = ImageDraw.Draw(image)
    zones = {zone["_zoneId"]: zone for zone in data["zones"]}
    bounds = [zone["_bounds"] for zone in zones.values()]
    minimum_x = min(b["x"] for b in bounds)
    maximum_x = max(b["x"] + b["width"] for b in bounds)
    minimum_y = min(b["y"] for b in bounds)
    maximum_y = max(b["y"] + b["height"] for b in bounds)
    margin, top, bottom = 52, 102, 44
    scale = min((width - margin * 2) / (maximum_x - minimum_x),
                (height - top - bottom) / (maximum_y - minimum_y))
    offset_x = (width - (maximum_x - minimum_x) * scale) / 2
    offset_y = top + (height - top - bottom - (maximum_y - minimum_y) * scale) / 2

    def point(x: float, y: float) -> tuple[float, float]:
        return ((offset_x + (x - minimum_x) * scale) * factor,
                (offset_y + (maximum_y - y) * scale) * factor)

    def text(position, value, size, color, anchor="mm"):
        font = ImageFont.truetype(str(font_path), max(9, round(size * factor)))
        draw.text(position, value, font=font, fill=color, anchor=anchor)

    def rectangle(bounds_value):
        return (*point(bounds_value["x"], bounds_value["y"] + bounds_value["height"]),
                *point(bounds_value["x"] + bounds_value["width"], bounds_value["y"]))

    text((margin * factor, 38 * factor), "区域指挥", 27, "#dae2de", "lm")
    text(((width - margin) * factor, 38 * factor), f'{len(zones)} 个区域  ·  {len(data["nodes"])} 个群', 16, "#899895", "rm")
    draw.line((margin * factor, 74 * factor, (width - margin) * factor, 74 * factor), fill="#2a353b", width=factor)
    for zone in zones.values():
        empty = not any(n["_zoneId"] == zone["_zoneId"] for n in data["nodes"])
        draw.rectangle(rectangle(zone["_bounds"]), fill="#151c20" if empty else "#1b252a", outline="#35454b", width=factor)

    alignments = {line["_id"]: line["_coordinate"] for line in data["constraints"]["_alignments"]}
    nodes = {}
    for node in data["nodes"]:
        zone = zones[node["_zoneId"]]["_bounds"]
        x = node["_position"]["x"] + zone["x"] + zone["width"] / 2
        y = node["_position"]["y"] + zone["y"] + zone["height"] / 2
        nodes[node["_nodeId"]] = (node, alignments.get(node["_columnId"], x), alignments.get(node["_rowId"], y))
    connected = set()
    for edge in data["edges"]:
        first, x1, y1 = nodes[edge["_fromNodeId"]]
        second, x2, y2 = nodes[edge["_toNodeId"]]
        connected.update((first["_nodeId"], second["_nodeId"]))
        horizontal = edge["_axis"] == 1
        size_axis = "x" if horizontal else "y"
        sign = 1 if (x2 - x1 if horizontal else y2 - y1) > 0 else -1
        a = (first["_footprint"][size_axis] / 2 + edge["_fromInset"]) * sign
        b = (second["_footprint"][size_axis] / 2 + edge["_toInset"]) * sign
        start = point(x1 + a if horizontal else x1, y1 if horizontal else y1 + a)
        end = point(x2 - b if horizontal else x1, y1 if horizontal else y2 - b)
        draw.line((*start, *end), fill="#647571", width=max(factor, round(max(2, edge["_widthOverride"]) * scale * factor)))

    colors = {2: "#c7c7a0", 3: "#cf8983", 4: "#cf8983", 5: "#80bda4"}
    for node, x, y in nodes.values():
        px, py = point(x, y)
        radius = min(node["_footprint"].values()) * scale * factor * 0.42
        thickness = max(2, round(1.6 * scale * factor))
        color = colors.get(node["_nodeKind"], "#b7c2c3")
        draw.ellipse((px - radius * 1.3, py - radius * 1.3, px + radius * 1.3, py + radius * 1.3), fill="#1b252a")
        if node["_nodeKind"] == 2:
            draw.rectangle((px-radius, py-radius*.8, px+radius, py+radius*.8), outline=color, width=thickness)
            draw.line((px-radius, py-radius*.25, px+radius, py-radius*.25), fill=color, width=thickness)
            draw.line((px, py-radius*.8, px, py+radius*.8), fill=color, width=thickness)
        elif node["_nodeKind"] in (3, 4):
            draw.line([(px, py-radius), (px+radius, py), (px, py+radius), (px-radius, py), (px, py-radius)], fill=color, width=thickness, joint="curve")
            draw.line((px, py-radius*.4, px, py+radius*.2), fill=color, width=thickness)
        elif node["_nodeKind"] == 5:
            draw.line([(px, py-radius), (px-radius*.75, py-radius), (px-radius*.75, py+radius), (px, py+radius)], fill=color, width=thickness)
            draw.line((px-radius*.15, py, px+radius, py), fill=color, width=thickness)
            draw.line([(px+radius*.5, py-radius*.5), (px+radius, py), (px+radius*.5, py+radius*.5)], fill=color, width=thickness)
        if node["_nodeId"] not in connected:
            text((px + radius * 2, py - radius * 1.2), "!", max(12, scale * 18), "#d6ad76")
    for zone in zones.values():
        bounds_value = zone["_bounds"]
        center = point(bounds_value["x"] + bounds_value["width"] / 2, bounds_value["y"] + bounds_value["height"] / 2)
        text(center, zone["_displayName"], min(24, max(12, 24 * scale)), "#afbfbb")
    text((width * factor / 2, (height - 18) * factor), "□ 资源   ◇ 敌人   → 撤离       ! 当前导航断连", 13, "#83918d")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.resize((width, height), Image.Resampling.LANCZOS).save(output_path)
    print(output_path)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--width", type=int, default=1600)
    parser.add_argument("--height", type=int, default=1000)
    args = parser.parse_args()
    render(args.evidence, args.output, args.width, args.height)
