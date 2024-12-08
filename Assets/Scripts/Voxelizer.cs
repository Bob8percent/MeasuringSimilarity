using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;
using Unity.VisualScripting;

namespace Voxelizer
{
    public class CPUVoxelizer
    {
        public class Triangle
        {
            public Vector3 a, b, c;     // 3 points for a triangle
            public bool frontFacing;    // a flag indicates front facing for direction to fill the voxels
            public Bounds bounds;       // AABB of a triangle

            public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 dir, bool surfaceOnly = true)
            {
                this.a = a;
                this.b = b;
                this.c = c;

                if (!surfaceOnly)
                {
                    // check if a triangle is front or back facing for direction to fill the voxels
                    var normal = Vector3.Cross(b - a, c - a);
                    this.frontFacing = (Vector3.Dot(normal, dir) <= 0f);
                }
                else
                {
                    this.frontFacing = true;
                }

                // calculate AABB of a triangle
                var min = Vector3.Min(Vector3.Min(a, b), c);
                var max = Vector3.Max(Vector3.Max(a, b), c);
                bounds.SetMinMax(min, max);
            }
        }

        public static void Voxelize(GameObject targetObject, int resolution, out Voxel_t[] voxels, out float unit, bool surfaceOnly = true)
        {
            Mesh mesh = targetObject.GetComponent<MeshFilter>().sharedMesh;
            
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;

            // From the specified resolution, calculate the unit length of one voxel
            float maxLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            unit = maxLength / resolution;

            // half of the unit length
            var hunit = unit * 0.5f;

            // The bounds extended by "half of the unit length constituting one voxel" is defined as the scope of voxelization
            var start = bounds.min - new Vector3(hunit, hunit, hunit);  // Minimum bounds to voxel
            var end = bounds.max + new Vector3(hunit, hunit, hunit);    // Maximum bounds to voxel
            var size = end - start;                                     // Size of bounds to voxel

            // The size of three-dimensional voxel data is determined based on the unit length of the voxel and the scope of voxelization
            var width = Mathf.CeilToInt(size.x / unit);
            var height = Mathf.CeilToInt(size.y / unit);
            var depth = Mathf.CeilToInt(size.z / unit);
            var volume = new Voxel_t[width, height, depth];

            // In the subsequent processing, 
            // in order to refer to the position and size of each voxel data, generate an AABB array.
            var boxes = new Bounds[width, height, depth];
            var voxelUnitSize = Vector3.one * unit;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        var p = new Vector3(x, y, z) * unit + start;
                        var aabb = new Bounds(p, voxelUnitSize);
                        boxes[x, y, z] = aabb;
                    }
                }
            }

            var vertices = mesh.vertices;
            var indices = mesh.triangles;

            // direction to fill the voxels
            var direction = Vector3.forward;

            for (int i = 0, n = indices.Length; i < n; i += 3)
            {
                // a target triangle
                var tri = new Triangle(
                    vertices[indices[i]],
                    vertices[indices[i + 1]],
                    vertices[indices[i + 2]],
                    direction
                );

                // calculate a AABB of a triangle 
                var min = tri.bounds.min - start;
                var max = tri.bounds.max - start;
                int iminX = Mathf.RoundToInt(min.x / unit), iminY = Mathf.RoundToInt(min.y / unit), iminZ = Mathf.RoundToInt(min.z / unit);
                int imaxX = Mathf.RoundToInt(max.x / unit), imaxY = Mathf.RoundToInt(max.y / unit), imaxZ = Mathf.RoundToInt(max.z / unit);
                iminX = Mathf.Clamp(iminX, 0, width - 1);
                iminY = Mathf.Clamp(iminY, 0, height - 1);
                iminZ = Mathf.Clamp(iminZ, 0, depth - 1);
                imaxX = Mathf.Clamp(imaxX, 0, width - 1);
                imaxY = Mathf.Clamp(imaxY, 0, height - 1);
                imaxZ = Mathf.Clamp(imaxZ, 0, depth - 1);

                uint front = (uint)(tri.frontFacing ? 1 : 0);

                // inside AABB of a triangle,
                // check intersections a triangle and voxels
                for (int x = iminX; x <= imaxX; x++)
                {
                    for (int y = iminY; y <= imaxY; y++)
                    {
                        for (int z = iminZ; z <= imaxZ; z++)
                        {
                            if (Intersects(tri, boxes[x, y, z]))
                            {
                                var voxel = volume[x, y, z];
                                voxel.position = boxes[x, y, z].center;
                                if ((voxel.front & 1) == 0)
                                {
                                    voxel.front = front;
                                }
                                else
                                {
                                    voxel.front = voxel.front & front;
                                }
                                voxel.fill = 1;
                                volume[x, y, z] = voxel;
                            }
                        }
                    }
                }
            }

            if (!surfaceOnly)
            {
                // fill inside of a mesh
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        // fill inside of a mesh from z-nearest side (x, y, 0)
                        for (int z = 0; z < depth; z++)
                        {
                            // continue if (x, y, z) is empty
                            if (volume[x, y, z].IsEmpty()) continue;

                            // step forward to front face
                            int ifront = z;
                            for (; ifront < depth && volume[x, y, ifront].IsFrontFace(); ifront++) { }

                            // break if position is out of bounds
                            if (ifront >= depth) break;

                            int iback = ifront;

                            // step forward to empty
                            for (; iback < depth && volume[x, y, iback].IsEmpty(); iback++) { }

                            if (iback >= depth) break;

                            // check if iback is back voxel
                            if (volume[x, y, iback].IsBackFace())
                            {
                                // step forward to back face
                                for (; iback < depth && volume[x, y, iback].IsBackFace(); iback++) { }
                            }

                            // fill from ifront to iback
                            for (int z2 = ifront; z2 < iback; z2++)
                            {
                                var p = boxes[x, y, z2].center;
                                var voxel = volume[x, y, z2];
                                voxel.position = p;
                                voxel.fill = 1;
                                volume[x, y, z2] = voxel;
                            }

                            // advance loop to (x, y, iback)
                            z = iback;
                        }
                    }
                }
            }

            // get non-empty voxels 
            voxels = new Voxel_t[width * height * depth];
            int index = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        if (!volume[x, y, z].IsEmpty())
                        {
                            voxels[index] = volume[x, y, z];
                        }
                        index += 1;
                    }
                }
            }
        }

        public static bool Intersects(Triangle tri, Bounds aabb)
        {
            // get the center of aabb and extents
            Vector3 center = aabb.center, extents = aabb.max - center;

            // translate the triangle as conceptually moving the AABB to origin
            Vector3 v0 = tri.a - center,
                v1 = tri.b - center,
                v2 = tri.c - center;

            // compute the edge vectors of the triangle
            Vector3 f0 = v1 - v0,
                f1 = v2 - v1,
                f2 = v0 - v2;

            // cross products of triangle edges & aabb edges
            // AABB normals are the x (1, 0, 0), y (0, 1, 0), z (0, 0, 1) axis.
            // so we can get the cross products between triangle edge vectors and AABB normals without calculation
            Vector3 a00 = new Vector3(0, -f0.z, f0.y), // cross product of X and f0
                a01 = new Vector3(0, -f1.z, f1.y), // X and f1
                a02 = new Vector3(0, -f2.z, f2.y), // X and f2
                a10 = new Vector3(f0.z, 0, -f0.x), // Y and f0
                a11 = new Vector3(f1.z, 0, -f1.x), // Y and f1
                a12 = new Vector3(f2.z, 0, -f2.x), // Y and f2
                a20 = new Vector3(-f0.y, f0.x, 0), // Z and f0
                a21 = new Vector3(-f1.y, f1.x, 0), // Z and f1
                a22 = new Vector3(-f2.y, f2.x, 0); // Z and f2

            // Test 9 axes
            if (
                !Intersects(v0, v1, v2, extents, a00) ||
                !Intersects(v0, v1, v2, extents, a01) ||
                !Intersects(v0, v1, v2, extents, a02) ||
                !Intersects(v0, v1, v2, extents, a10) ||
                !Intersects(v0, v1, v2, extents, a11) ||
                !Intersects(v0, v1, v2, extents, a12) ||
                !Intersects(v0, v1, v2, extents, a20) ||
                !Intersects(v0, v1, v2, extents, a21) ||
                !Intersects(v0, v1, v2, extents, a22)
            )
            {
                return false;
            }

            // Test x axis
            if (Mathf.Max(v0.x, v1.x, v2.x) < -extents.x || Mathf.Min(v0.x, v1.x, v2.x) > extents.x)
            {
                return false;
            }

            // Test y axis
            if (Mathf.Max(v0.y, v1.y, v2.y) < -extents.y || Mathf.Min(v0.y, v1.y, v2.y) > extents.y)
            {
                return false;
            }

            // Test z axis
            if (Mathf.Max(v0.z, v1.z, v2.z) < -extents.z || Mathf.Min(v0.z, v1.z, v2.z) > extents.z)
            {
                return false;
            }

            // Test triangle normal
            var normal = Vector3.Cross(f1, f0).normalized;
            var pl = new Plane(normal, Vector3.Dot(normal, tri.a));
            return Intersects(pl, aabb);
        }

        // check intersection between the triangle (v0, v1, v2) and AABB (extents) with projection onto the axis
        protected static bool Intersects(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 extents, Vector3 axis)
        {
            // project all 3 vertices of the triangle onto the axis
            float p0 = Vector3.Dot(v0, axis);
            float p1 = Vector3.Dot(v1, axis);
            float p2 = Vector3.Dot(v2, axis);

            // project the AABB onto the axis
            float r = extents.x * Mathf.Abs(axis.x) + extents.y * Mathf.Abs(axis.y) + extents.z * Mathf.Abs(axis.z);
            float minP = Mathf.Min(p0, p1, p2);
            float maxP = Mathf.Max(p0, p1, p2);
            return !((maxP < -r) || (r < minP));
        }

        // check intersection between the plane and AABB
        public static bool Intersects(Plane pl, Bounds aabb)
        {
            Vector3 center = aabb.center;
            var extents = aabb.max - center;

            // project the extents onto the plane normal
            var r = extents.x * Mathf.Abs(pl.normal.x) + extents.y * Mathf.Abs(pl.normal.y) + extents.z * Mathf.Abs(pl.normal.z);

            // compute the distance of box center from plane
            var s = Vector3.Dot(pl.normal, center) - pl.distance;

            // check if s is within [-r, r]
            return Mathf.Abs(s) <= r;
        }
    }

    public class GPUVoxelizer
    {

        public static void VoxelizeSurface(ref ComputeShader voxelizer,
            in GameObject target, out Voxel_t[] voxels, out float unit,
            in int resolution = 64)
        {
            Mesh mesh = target.GetComponent<MeshFilter>().sharedMesh;

            mesh.RecalculateBounds();
            var bounds = mesh.bounds;

            // From the specified resolution, calculate the unit length of one voxel
            float maxLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            unit = maxLength / resolution;

            // half of the unit length
            var hunit = unit * 0.5f;

            // The bounds extended by "half of the unit length constituting one voxel" is defined as the scope of voxelization
            var start = bounds.min - new Vector3(hunit, hunit, hunit);  // Minimum bounds to voxel
            var end = bounds.max + new Vector3(hunit, hunit, hunit);    // Maximum bounds to voxel
            var size = end - start;                                     // Size of bounds to voxel

            // The size of three-dimensional voxel data is determined based on the unit length of the voxel and the scope of voxelization
            int width = Mathf.CeilToInt(size.x / unit);
            int height = Mathf.CeilToInt(size.y / unit);
            int depth = Mathf.CeilToInt(size.z / unit);

            // send voxel data to GPU
            voxelizer.SetVector("_Start", start);
            voxelizer.SetVector("_End", end);
            voxelizer.SetVector("_Size", size);

            voxelizer.SetFloat("_Unit", unit);
            voxelizer.SetFloat("_InvUnit", 1f / unit);
            voxelizer.SetFloat("_HalfUnit", hunit);
            voxelizer.SetInt("_Width", width);
            voxelizer.SetInt("_Height", height);
            voxelizer.SetInt("_Depth", depth);

            // generate ComputeBuffer representing Voxel_t array
            var voxelBuffer = new ComputeBuffer(width * height * depth, Marshal.SizeOf(typeof(Voxel_t)));
            voxels = new Voxel_t[voxelBuffer.count];
            voxelBuffer.SetData(voxels); // initialize

            // generate ComputeBuffer representing vertex array
            var vertices = mesh.vertices;
            var vertBuffer = new ComputeBuffer(vertices.Length, Marshal.SizeOf(typeof(Vector3)));
            vertBuffer.SetData(vertices);
            // generate ComputeBuffer representing triangle array
            var triangles = mesh.triangles;
            var triBuffer = new ComputeBuffer(triangles.Length, Marshal.SizeOf(typeof(int)));
            triBuffer.SetData(triangles);

            // send mesh data to GPU kernel
            var surfaceAllKer = new Kernel(voxelizer, "SurfaceAll");
            voxelizer.SetBuffer(surfaceAllKer.Index, "_VoxelBuffer", voxelBuffer);
            voxelizer.SetBuffer(surfaceAllKer.Index, "_VertBuffer", vertBuffer);
            voxelizer.SetBuffer(surfaceAllKer.Index, "_TriBuffer", triBuffer);

            // set triangle count in a mesh
            var triangleCount = triBuffer.count / 3;
            voxelizer.SetInt("_TriangleCount", triangleCount);

            // execute surface construction
            voxelizer.Dispatch(surfaceAllKer.Index, triangleCount / (int)surfaceAllKer.ThreadX + 1, (int)surfaceAllKer.ThreadY, (int)surfaceAllKer.ThreadZ);

            voxelBuffer.GetData(voxels);

            // dispose buffer
            voxelBuffer.Release();
            triBuffer.Release();
            vertBuffer.Release();
        }

        public static float CalcSimilarity(ref ComputeShader voxelizer,
            in GameObject target, in GameObject destination, ref Mesh displayer,
            in int resolution = 64)
        {
            Mesh targetMesh = target.GetComponent<SkinnedMeshRenderer>().sharedMesh;
            Mesh destinationMesh = destination.GetComponent<SkinnedMeshRenderer>().sharedMesh;

            targetMesh.RecalculateBounds();
            destinationMesh.RecalculateBounds();
            var targetBounds = targetMesh.bounds;
            var destinationBounds = destinationMesh.bounds;
            // targetとdestinationのBoundsを包含する最小サイズのBoundsを計算
            Vector3 minPoint = new Vector3(
                Mathf.Min(targetBounds.min.x, destinationBounds.min.x),
                Mathf.Min(targetBounds.min.y, destinationBounds.min.y),
                Mathf.Min(targetBounds.min.z, destinationBounds.min.z)
                );
            Vector3 maxPoint = new Vector3(
                Mathf.Max(targetBounds.max.x, destinationBounds.max.x),
                Mathf.Max(targetBounds.max.y, destinationBounds.max.y),
                Mathf.Max(targetBounds.max.z, destinationBounds.max.z)
                );
            Bounds bounds = new Bounds((maxPoint + minPoint) / 2, (maxPoint - minPoint));
            return CalcSimilarityInner1(ref voxelizer, bounds, targetMesh, destinationMesh, ref displayer, resolution);
        }

        private static float CalcSimilarityInner1(ref ComputeShader voxelizer,
            in Bounds bounds,
            in Mesh targetMesh, in Mesh destinationMesh,
            ref Mesh displayer,
            in int resolution = 64)
        {
            // From the specified resolution, calculate the unit length of one voxel
            float maxLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var unit = maxLength / resolution;

            // half of the unit length
            var hunit = unit * 0.5f;

            // The bounds extended by "half of the unit length constituting one voxel" is defined as the scope of voxelization
            var start = bounds.min - new Vector3(hunit, hunit, hunit);  // Minimum bounds to voxel
            var end = bounds.max + new Vector3(hunit, hunit, hunit);    // Maximum bounds to voxel
            var size = end - start;                                     // Size of bounds to voxel

            // The size of three-dimensional voxel data is determined based on the unit length of the voxel and the scope of voxelization
            int width = Mathf.CeilToInt(size.x / unit);
            int height = Mathf.CeilToInt(size.y / unit);
            int depth = Mathf.CeilToInt(size.z / unit);

            // send voxel data to GPU
            voxelizer.SetVector("_Start", start);
            voxelizer.SetVector("_End", end);
            voxelizer.SetVector("_Size", size);

            voxelizer.SetFloat("_Unit", unit);
            voxelizer.SetFloat("_InvUnit", 1f / unit);
            voxelizer.SetFloat("_HalfUnit", hunit);
            voxelizer.SetInt("_Width", width);
            voxelizer.SetInt("_Height", height);
            voxelizer.SetInt("_Depth", depth);

            ComputeBuffer targetVoxelBuffer;
            ComputeBuffer targetVertBuffer;
            ComputeBuffer targetTriBuffer;
            ComputeBuffer destinationVoxelBuffer;
            ComputeBuffer destinationVertBuffer;
            ComputeBuffer destinationTriBuffer;
            Voxel_t[] targetVoxels;
            Voxel_t[] destinationVoxels;
            dispatchCompute(ref voxelizer, width, height, depth, targetMesh, ref displayer, out targetVoxelBuffer, out targetVertBuffer, out targetTriBuffer, out targetVoxels);
            dispatchCompute(ref voxelizer, width, height, depth, destinationMesh, ref displayer, out destinationVoxelBuffer, out destinationVertBuffer, out destinationTriBuffer, out destinationVoxels);

            // draw similarity
            ComputeBuffer voxelLevelBuffer;
            drawSimilarity(ref voxelizer, width, height, depth, targetMesh, ref displayer,
                ref targetVoxelBuffer, ref targetVertBuffer, ref targetTriBuffer,
                ref destinationVoxelBuffer, out voxelLevelBuffer);

            //calc similarity
            float similarity = CalcSimilarityInner2(targetVoxels, destinationVoxels, unit * unit * unit, width, height, depth);

            // dispose buffer
            targetVoxelBuffer.Release();
            targetVertBuffer.Release();
            targetTriBuffer.Release();
            destinationVoxelBuffer.Release();
            destinationVertBuffer.Release();
            destinationTriBuffer.Release();
            voxelLevelBuffer.Release();

            return similarity;
        }

        // IoUにより類似度測定
        static private float CalcSimilarityInner2(in Voxel_t[] targetVoxels, in Voxel_t[] destinationVoxels, in float unitVolume,
            in int width, in int height, in int depth)
        {
            float overlapVolume = 0f;   // 積集合
            float unionVolume = 0f;     // 和集合

            for (int i = 0; i < width; i++)
            {

                for (int j = 0; j < height; j++)
                {

                    for (int k = 0; k < depth; k++)
                    {
                        int idx = k * (width * height) + j * width + i;
                        if (!targetVoxels[idx].IsEmpty() && !destinationVoxels[idx].IsEmpty())
                        {
                            overlapVolume += unitVolume;
                        }
                        if (!targetVoxels[idx].IsEmpty() || !destinationVoxels[idx].IsEmpty())
                        {
                            unionVolume += unitVolume;
                        }
                    }
                }
            }

            return overlapVolume / unionVolume;
        }

        static private void dispatchCompute(ref ComputeShader voxelizer,
            in int width, in int height, in int depth,
            in Mesh mesh, ref Mesh displayer,
            out ComputeBuffer voxelBuffer, out ComputeBuffer vertBuffer, out ComputeBuffer triBuffer,
            out Voxel_t[] voxels)
        {
            // generate ComputeBuffer representing Voxel_t array
            voxelBuffer = new ComputeBuffer(width * height * depth, Marshal.SizeOf(typeof(Voxel_t)));
            voxels = new Voxel_t[voxelBuffer.count];
            voxelBuffer.SetData(voxels); // initialize

            // generate ComputeBuffer representing vertex array
            var vertices = mesh.vertices;
            vertBuffer = new ComputeBuffer(vertices.Length, Marshal.SizeOf(typeof(Vector3)));
            vertBuffer.SetData(vertices);
            // generate ComputeBuffer representing triangle array
            var triangles = mesh.triangles;
            triBuffer = new ComputeBuffer(triangles.Length, Marshal.SizeOf(typeof(int)));
            triBuffer.SetData(triangles);

            // send mesh data to GPU kernel "SurfaceFront" and "SurfaceBack"
            var surfaceFrontKer = new Kernel(voxelizer, "SurfaceFront");
            voxelizer.SetBuffer(surfaceFrontKer.Index, "_VoxelBuffer", voxelBuffer);
            voxelizer.SetBuffer(surfaceFrontKer.Index, "_VertBuffer", vertBuffer);
            voxelizer.SetBuffer(surfaceFrontKer.Index, "_TriBuffer", triBuffer);

            // set triangle count in a mesh
            var triangleCount = triBuffer.count / 3;
            voxelizer.SetInt("_TriangleCount", triangleCount);

            // execute surface construction in front triangles
            voxelizer.Dispatch(surfaceFrontKer.Index, triangleCount / (int)surfaceFrontKer.ThreadX + 1, (int)surfaceFrontKer.ThreadY, (int)surfaceFrontKer.ThreadZ);

            // execute surface construction in back triangles
            var surfaceBackKer = new Kernel(voxelizer, "SurfaceBack");
            voxelizer.SetBuffer(surfaceBackKer.Index, "_VoxelBuffer", voxelBuffer);
            voxelizer.SetBuffer(surfaceBackKer.Index, "_VertBuffer", vertBuffer);
            voxelizer.SetBuffer(surfaceBackKer.Index, "_TriBuffer", triBuffer);
            voxelizer.Dispatch(surfaceBackKer.Index, triangleCount / (int)surfaceBackKer.ThreadX + 1, (int)surfaceBackKer.ThreadY, (int)surfaceBackKer.ThreadZ);

            // send voxel data to GPU kernel "Volume"
            var volumeKer = new Kernel(voxelizer, "Volume");
            voxelizer.SetBuffer(volumeKer.Index, "_VoxelBuffer", voxelBuffer);

            // execute to fill voxels inside of a mesh
            voxelizer.Dispatch(volumeKer.Index, width / (int)volumeKer.ThreadX + 1, height / (int)volumeKer.ThreadY + 1, (int)surfaceFrontKer.ThreadZ);

            voxelBuffer.GetData(voxels);
        }

        // 類似度を可視化
        // NOTE: 類似している部分は水色、異なっている部分を赤で描画
        static private void drawSimilarity(ref ComputeShader voxelizer,
            in int width, in int height, in int depth,
            in Mesh mesh,
            ref Mesh displayer,
            ref ComputeBuffer targetVoxelBuffer,
            ref ComputeBuffer targetVertBuffer,
            ref ComputeBuffer targetTriBuffer,
            ref ComputeBuffer destinationVoxelBuffer,
            out ComputeBuffer voxelLevelBuffer
            )
        {
            // calc similarity
            var similarityKer = new Kernel(voxelizer, "Similarity");
            voxelizer.SetBuffer(similarityKer.Index, "_TargetVertBuffer", targetVertBuffer);
            voxelizer.SetBuffer(similarityKer.Index, "_TargetTriBuffer", targetTriBuffer);
            int triangleCount = mesh.triangles.Length / 3;
            voxelizer.SetInt("_TargetTriangleCount", triangleCount);
            voxelizer.SetBuffer(similarityKer.Index, "_TargetVoxelBuffer", targetVoxelBuffer);
            voxelizer.SetBuffer(similarityKer.Index, "_DestinationVoxelBuffer", destinationVoxelBuffer);
            voxelLevelBuffer = new ComputeBuffer(triangleCount, Marshal.SizeOf(typeof(float)));
            voxelizer.SetBuffer(similarityKer.Index, "_VoxelLevelBuffer", voxelLevelBuffer);
            float[] voxelLevels = new float[triangleCount];
            voxelLevelBuffer.SetData(voxelLevels);
            voxelizer.Dispatch(similarityKer.Index, triangleCount / (int)similarityKer.ThreadX + 1, (int)similarityKer.ThreadY, (int)similarityKer.ThreadZ);
            voxelLevelBuffer.GetData(voxelLevels);

            // draw similarity
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Color[] colors = new Color[vertices.Length];
            int[] drawCounts = new int[mesh.vertices.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float level = Mathf.Clamp(voxelLevels[i / 3], 0f, 1f);
                Color color = new Color();
                color.r = (1f - level);
                color.g = level;
                color.b = level;
                color.a = 1f;

                if (drawCounts[triangles[i]] > 0)
                    colors[triangles[i]] = (color + colors[triangles[i]]) / (++drawCounts[triangles[i]]);
                else
                    colors[triangles[i]] = color;

                if (drawCounts[triangles[i + 1]] > 0)
                    colors[triangles[i + 1]] = (color + colors[triangles[i + 1]]) / (++drawCounts[triangles[i + 1]]);
                else
                    colors[triangles[i + 1]] = color;

                if (drawCounts[triangles[i + 2]] > 0)
                    colors[triangles[i + 2]] = (color + colors[triangles[i + 2]]) / (++drawCounts[triangles[i + 2]]);
                else
                    colors[triangles[i + 2]] = color;
            }
            displayer.colors = colors;
        }

    }

}