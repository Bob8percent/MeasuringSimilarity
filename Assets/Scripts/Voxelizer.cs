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

    public class GPUVoxelizer
    {

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
                        if(!targetVoxels[idx].IsEmpty() || !destinationVoxels[idx].IsEmpty())
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