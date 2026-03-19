#!/usr/bin/env python3
import argparse
import json
import math
import re
import struct
from pathlib import Path


PARTICLE_PATTERN = re.compile(r"ParticleData_Fluid_(\d+)\.vtk$")
RIGID_PATTERN = re.compile(r"rb_data_1_(\d+)\.vtk$")


def parse_legacy_vtk_points(path: Path):
    data = path.read_bytes()
    marker = b"POINTS "
    marker_index = data.index(marker)
    header_end = data.index(b"\n", marker_index)
    header = data[marker_index:header_end].decode("ascii")
    _, count_str, type_name = header.split()
    point_count = int(count_str)
    if type_name == "float":
        scalar_format = "f"
        scalar_size = 4
    elif type_name == "double":
        scalar_format = "d"
        scalar_size = 8
    else:
        raise ValueError(f"Unsupported VTK scalar type '{type_name}' in {path}")

    values_offset = header_end + 1
    values_count = point_count * 3
    values_size = values_count * scalar_size
    raw_values = data[values_offset:values_offset + values_size]
    values = struct.unpack(">" + scalar_format * values_count, raw_values)
    return [values[index:index + 3] for index in range(0, len(values), 3)]


def sorted_frame_paths(directory: Path, pattern: re.Pattern):
    items = []
    for path in directory.glob("*.vtk"):
        match = pattern.match(path.name)
        if match:
            items.append((int(match.group(1)), path))
    items.sort(key=lambda item: item[0])
    return items


def write_float32_buffer(path: Path, values):
    with path.open("wb") as handle:
        for value in values:
            handle.write(struct.pack("<f", value))


def build_cache(input_dir: Path, output_dir: Path, center):
    vtk_dir = input_dir / "vtk"
    if not vtk_dir.exists():
        raise FileNotFoundError(f"Missing vtk directory: {vtk_dir}")

    particle_frames = sorted_frame_paths(vtk_dir, PARTICLE_PATTERN)
    rigid_frames = sorted_frame_paths(vtk_dir, RIGID_PATTERN)

    if not particle_frames:
        raise RuntimeError("No particle VTK files found.")
    if not rigid_frames:
        raise RuntimeError("No rigid-body VTK files found.")
    if len(particle_frames) != len(rigid_frames):
        raise RuntimeError("Particle and rigid-body frame counts do not match.")

    output_dir.mkdir(parents=True, exist_ok=True)

    first_rigid_points = parse_legacy_vtk_points(rigid_frames[0][1])
    if len(first_rigid_points) < 2:
        raise RuntimeError("Rigid-body frame does not contain enough points to derive rotation.")

    frame_indices = []
    fluid_values = []
    rigid_angles = []
    particle_count = None

    initial_edge = (
        first_rigid_points[1][0] - first_rigid_points[0][0],
        first_rigid_points[1][1] - first_rigid_points[0][1],
    )
    initial_angle = math.atan2(initial_edge[1], initial_edge[0])

    for (particle_frame_index, particle_path), (rigid_frame_index, rigid_path) in zip(particle_frames, rigid_frames):
        if particle_frame_index != rigid_frame_index:
            raise RuntimeError(
                f"Mismatched frame indices: particle {particle_frame_index}, rigid {rigid_frame_index}"
            )

        frame_indices.append(particle_frame_index)

        particle_points = parse_legacy_vtk_points(particle_path)
        if particle_count is None:
            particle_count = len(particle_points)
        elif particle_count != len(particle_points):
            raise RuntimeError("Particle count changed between frames.")

        for x, y, z in particle_points:
            fluid_values.extend((x - center[0], y - center[1], z - center[2]))

        rigid_points = parse_legacy_vtk_points(rigid_path)
        edge = (
            rigid_points[1][0] - rigid_points[0][0],
            rigid_points[1][1] - rigid_points[0][1],
        )
        angle = math.atan2(edge[1], edge[0]) - initial_angle
        while angle > math.pi:
            angle -= 2.0 * math.pi
        while angle < -math.pi:
            angle += 2.0 * math.pi
        rigid_angles.append(angle)

    write_float32_buffer(output_dir / "fluid_positions.bin", fluid_values)
    write_float32_buffer(output_dir / "container_angles.bin", rigid_angles)

    metadata = {
        "source": str(input_dir),
        "frameCount": len(frame_indices),
        "particleCount": particle_count,
        "frameRate": 20.0,
        "centerOffset": [0.0, 0.0, 0.0],
        "particleRadius": 0.03,
        "containerInnerSize": [2.0, 2.0, 2.0],
        "frameIndices": frame_indices,
        "fluidPositionsFile": "fluid_positions.bin",
        "containerAnglesFile": "container_angles.bin",
    }
    (output_dir / "cache.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description="Convert SPlisHSPlasH VTK exports to a compact Unity playback cache.")
    parser.add_argument("input_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--center", nargs=3, type=float, default=(0.0, 1.5, 0.0))
    args = parser.parse_args()

    build_cache(args.input_dir, args.output_dir, args.center)


if __name__ == "__main__":
    main()
