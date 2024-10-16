using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

public class ShapeMatching
{
    // target��destination�ɋɗ͌덷�̂Ȃ��悤�ɐ��񂳂���
    // TODO: �����ShapeMatching�@���g�p�B�����Ɛ��x������ICP�Ȃ���̂�����炵��
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
            // �O�p�`�����قȂ郁�b�V�����m�͐��񂳂����Ȃ�
            throw new Exception("invalid meshes!");
        }

        // target, destination�̏d�S�����߂�
        var p = new Vector3[n]; // �O�p�`�̏d�S���X�g
        var q = new Vector3[n];
        var centerP = Vector3.zero; // �S�O�p�`�̏d�S
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

        // p, q�̕��U�����U�s������߂�
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
        // SVD�����s
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

    // Mesh�̒��_���ҏW���ꂽ��
    public static bool IsEditMesh(ref GameObject oldTarget, ref GameObject nowTarget)
    {
        Mesh oldMesh = oldTarget.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Mesh nowMesh = nowTarget.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        Vector3[] oldVertices = oldMesh.vertices;
        Vector3[] nowVertices = nowMesh.vertices;

        // ���_�̐����قȂ�ꍇ�͕ύX���ꂽ�ƌ��Ȃ�
        if (oldVertices.Length != nowVertices.Length)
        {
            return true;
        }

        // �e���_���W���قȂ邩�ǂ������r
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