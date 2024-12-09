import open3d as o3d
import numpy as np
import time

test_count = 10
process_time_all = 0

# 点群の読み込み(これはUnityで行ったNNタスクに用いられたメッシュと同じ点群数)
pcd_s = o3d.io.read_point_cloud("bun_zipper.ply")
pcd_t = o3d.io.read_point_cloud("bun_zipper_t.ply")

for _ in range(test_count):

    start_time = time.time()

    # KD-Treeの構築
    kdtree = o3d.geometry.KDTreeFlann(pcd_t)

    # 最近傍ペアを探索し、線分を作成
    lines = []
    for i, point in enumerate(pcd_s.points):  # ソース点群の各点で探索
        [_, idx, _] = kdtree.search_knn_vector_3d(point, 1)  # 最近傍1点を探索
        lines.append((i, idx))

    end_time = time.time()
    process_time_all += (end_time - start_time)

process_time = (process_time_all * 100)   # s => ms
print(f"knn on CPU(open3d): {process_time:.2f} ms")
