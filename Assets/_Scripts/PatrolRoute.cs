using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PatrolNode
{
    [Tooltip("The physical location of the waypoint.")]
    public Transform waypoint;
    [Tooltip("How long the guard stands here before moving to the next node.")]
    public float waitTime = 2.0f;
}

public class PatrolRoute : MonoBehaviour
{
    public List<PatrolNode> nodes = new List<PatrolNode>();
    [Tooltip("If true, draws yellow lines between the nodes in the Scene view.")]
    public bool showRouteGizmos = true;

    /// <summary>
    /// Measures the distance from the AI to all nodes and returns the index of the closest one.
    /// Crucial for cleanly returning to the patrol after abandoning a search.
    /// </summary>
    public int GetClosestNodeIndex(Vector3 currentAIPosition)
    {
        if (nodes.Count == 0) return 0;

        int closestIndex = 0;
        float shortestDistance = Mathf.Infinity;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].waypoint == null) continue;

            float dist = Vector3.Distance(currentAIPosition, nodes[i].waypoint.position);
            if (dist < shortestDistance)
            {
                shortestDistance = dist;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    private void OnDrawGizmos()
    {
        if (!showRouteGizmos || nodes.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].waypoint != null)
            {
                Gizmos.DrawWireSphere(nodes[i].waypoint.position, 0.3f);

                // Draw line to the next node (and loop back to the first)
                Transform nextWaypoint = nodes[(i + 1) % nodes.Count].waypoint;
                if (nextWaypoint != null)
                {
                    Gizmos.DrawLine(nodes[i].waypoint.position, nextWaypoint.position);
                }
            }
        }
    }
}