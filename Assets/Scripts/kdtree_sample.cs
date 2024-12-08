using System;
using UnityEngine;
using System.Collections.Generic;

public class KdTreeNode
{
    public Vector3 Point;
    public KdTreeNode Left;
    public KdTreeNode Right;
}

public class KdTree
{
    private KdTreeNode root;

    public void Build(List<Vector3> points)
    {
        root = BuildTree(points, 0);
    }

    private KdTreeNode BuildTree(List<Vector3> points, int depth)
    {
        if (points.Count == 0) return null;

        int axis = depth % 3; // 0 = X, 1 = Y, 2 = Z
        points.Sort((a, b) => (axis == 0 ? a.x : (axis == 1 ? a.y : a.z)).CompareTo(axis == 0 ? b.x : (axis == 1 ? b.y : b.z)));

        int medianIndex = points.Count / 2;
        KdTreeNode node = new KdTreeNode
        {
            Point = points[medianIndex],
            Left = BuildTree(points.GetRange(0, medianIndex), depth + 1),
            Right = BuildTree(points.GetRange(medianIndex + 1, points.Count - medianIndex - 1), depth + 1)
        };

        return node;
    }

    public Vector3 Nearest(Vector3 query)
    {
        return Nearest(root, query, 0).Point;
    }

    private KdTreeNode Nearest(KdTreeNode node, Vector3 query, int depth)
    {
        if (node == null) return null;

        int axis = depth % 3;
        KdTreeNode nextNode = null;
        KdTreeNode otherNode = null;

        if ((axis == 0 && query.x < node.Point.x) || (axis == 1 && query.y < node.Point.y) || (axis == 2 && query.z < node.Point.z))
        {
            nextNode = node.Left;
            otherNode = node.Right;
        }
        else
        {
            nextNode = node.Right;
            otherNode = node.Left;
        }

        KdTreeNode best = Nearest(nextNode, query, depth + 1);

        if (best == null || (query - node.Point).sqrMagnitude < (query - best.Point).sqrMagnitude)
        {
            best = node;
        }

        float distanceToPlane = axis == 0 ? query.x - node.Point.x : (axis == 1 ? query.y - node.Point.y : query.z - node.Point.z);
        if (Mathf.Abs(distanceToPlane) < (query - best.Point).magnitude)
        {
            KdTreeNode otherBest = Nearest(otherNode, query, depth + 1);
            if (otherBest != null && (query - otherBest.Point).sqrMagnitude < (query - best.Point).sqrMagnitude)
            {
                best = otherBest;
            }
        }

        return best;
    }
}