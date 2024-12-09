import open3d as o3d
import numpy as np
import time
import copy

test_count = 10
process_time_all = 0
resolution = 64

# メッシュの読み込み(これはUnityで行ったボクセル化タスクに用いられたメッシュと同じ点群数)
mesh = o3d.io.read_triangle_mesh("bun_zipper.ply")

for _ in range(test_count):
    start_time = time.time()

    # バウンディングボックスを計算
    aabb = mesh.get_axis_aligned_bounding_box()

    # AABBの最大辺の長さを計算
    aabb_extent = aabb.get_extent()
    aabb_max_edge = np.max(aabb_extent)

    unit = aabb_max_edge / resolution
    voxel_grid = o3d.geometry.VoxelGrid.create_from_triangle_mesh(mesh, unit)

    # ボクセル化
    end_time = time.time()
    process_time_all += (end_time - start_time)

process_time = (process_time_all * 100)   # s => ms
print(f"voxelize on CPU(open3d): {process_time:.2f} ms")
