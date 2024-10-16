using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

public class ShapeMatching
{
    // targetをdestinationに極力誤差のないように整列させる
    // TODO: 今回はShapeMatching法を使用。もっと精度がいいICPなるものがあるらしい
    public static void AlignMesh(ref GameObject target, ref GameObject destination)
    {
        Mesh targetMesh = target.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Mesh destinationMesh = destination.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        int[] triangles1 = targetMesh.triangles;
        int[] triangles2 = destinationMesh.triangles;
        Vector3[] vertices1 = targetMesh.vertices;
        Vector3[] vertices2 = destinationMesh.vertices;
        var triangleCount = triangles1.Length;
        var n = triangleCount / 3;
        
        if (triangleCount != triangles2.Length)
        {
            // 三角形数が異なるメッシュ同士は整列させられない
            throw new Exception("invalid meshes!");
        }

        // target, destinationの重心を求める
        var p = new Vector3[n]; // 三角形の重心リスト
        var q = new Vector3[n];
        var centerP = Vector3.zero; // 全三角形の重心
        var centerQ = Vector3.zero;

        for (int i = 0; i < triangleCount; i += 3)
        {
            Vector3 v1 = vertices1[triangles1[i]];
            Vector3 v2 = vertices1[triangles1[i + 1]];
            Vector3 v3 = vertices1[triangles1[i + 2]];

            Vector3 center = (v1 + v2 + v3) / 3.0f;
            p[i / 3] = center;
            centerP += center;

            v1 = vertices2[triangles1[i]];
            v2 = vertices2[triangles1[i + 1]];
            v3 = vertices2[triangles1[i + 2]];

            center = (v1 + v2 + v3) / 3.0f;
            q[i / 3] = center;
            centerQ += center;
        }
        centerP /=  n;
        centerQ /= n;

        // p, qの分散共分散行列を求める
        Matrix<double> H = DenseMatrix.OfArray(new double[,]
        {
            { 0.0, 0.0, 0.0 },
            { 0.0, 0.0, 0.0 },
            { 0.0, 0.0, 0.0 }
        });
        for (int i = 0; i < n; i++)
        {
            p[i] = p[i] - centerP;
            q[i] = q[i] - centerQ;
            var vectorP = Vector<double>.Build.DenseOfArray(new double[] { (double)p[i].x, (double)p[i].y, (double)p[i].z });
            var vectorQ = Vector<double>.Build.DenseOfArray(new double[] { (double)q[i].x, (double)q[i].y, (double)q[i].z });

            H += vectorP.ToColumnMatrix() * vectorQ.ToRowMatrix();
        }
        // SVDを実行
        var svd = H.Svd();
        var rotationMatrix = svd.VT.Transpose() * svd.U.Transpose();
        var mathNetVector = Vector<double>.Build.DenseOfArray(new double[] { (double)centerP.x, (double)centerP.y, (double)centerP.z });
        mathNetVector = rotationMatrix * mathNetVector;

        var translation = centerQ - new Vector3((float)mathNetVector[0], (float)mathNetVector[1], (float)mathNetVector[2]);

        var column1 = new Vector3((float)rotationMatrix.Column(1)[0], (float)rotationMatrix.Column(1)[1], (float)rotationMatrix.Column(1)[2]);
        var column2 = new Vector3((float)rotationMatrix.Column(2)[0], (float)rotationMatrix.Column(2)[1], (float)rotationMatrix.Column(2)[2]);

        Quaternion targetRotation = Quaternion.LookRotation(column2, column1);

        target.transform.position = translation;
        target.transform.rotation = targetRotation;
    }

    // Meshの頂点が編集されたか
    public static bool IsEditMesh(ref GameObject oldTarget, ref GameObject nowTarget)
    {
        Mesh oldMesh = oldTarget.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Mesh nowMesh = nowTarget.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Vector3[] oldVertices = oldMesh.vertices;
        Vector3[] nowVertices = nowMesh.vertices;

        // 頂点の数が異なる場合は変更されたと見なす
        if (oldVertices.Length != nowVertices.Length)
        {
            return true;
        }

        // 各頂点座標が異なるかどうかを比較
        for (int i = 0; i < oldVertices.Length; i++)
        {
            if (oldVertices[i] != nowVertices[i])
            {
                return true;
            }
        }

        return false;
    }

}