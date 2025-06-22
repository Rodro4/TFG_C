using System.Collections.Generic;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;

public class TFSubscriber : MonoBehaviour
{
    public string tfTopic = "/tf";
    public string tfStaticTopic = "/tf_static";

    private RosSocket rosSocket;

    // Internal representation of a transform node in the TF tree
    private class TFNode
    {
        public string parent;
        public Vector3 position;
        public Quaternion rotation;
        public double time;
        public bool isStatic;
    }

    // Stores transformations indexed by child_frame_id
    private Dictionary<string, TFNode> tfTree = new();

    void Start()
    {
        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("TFSubscriber: RosConnector not found.");
            enabled = false;
            return;
        }

        rosSocket = rosConnector.RosSocket;
        rosSocket.Subscribe<TFMessage>(tfTopic, msg => ReceiveTFMessage(msg, false), 0);
        rosSocket.Subscribe<TFMessage>(tfStaticTopic, msg => ReceiveTFMessage(msg, true), 0);
        Debug.Log("TFSubscriber: Subscribed to /tf y /tf_static");
    }

    // Callback to process incoming TF messages
    private void ReceiveTFMessage(TFMessage msg, bool isStatic)
    {
        foreach (var tf in msg.transforms)
        {
            string parent = tf.header.frame_id.Trim('/');
            string child = tf.child_frame_id.Trim('/');

            Vector3 pos = new Vector3(
                (float)tf.transform.translation.x,
                (float)tf.transform.translation.y,
                (float)tf.transform.translation.z
            );

            Quaternion rot = new Quaternion(
                (float)tf.transform.rotation.x,
                (float)tf.transform.rotation.y,
                (float)tf.transform.rotation.z,
                (float)tf.transform.rotation.w
            );

            double time = tf.header.stamp.secs + tf.header.stamp.nsecs * 1e-9;

            // If a static transform already exists for this frame, skip dynamic updates
            if (tfTree.ContainsKey(child) && tfTree[child].isStatic && !isStatic)
                continue;

            tfTree[child] = new TFNode
            {
                parent = parent,
                position = pos,
                rotation = rot,
                time = time,
                isStatic = isStatic
            };

            if (isStatic)
                Debug.Log($"TFSubscriber (/tf_static): Registered transform {parent} -> {child}.");
        }
    }

    /// <summary>
    /// Recursively builds the transform chain from sourceFrame to targetFrame.
    /// Returns false if no valid path is found.
    /// </summary>
    public bool TryGetTransform(string targetFrame, string sourceFrame, out Vector3 position, out Quaternion rotation)
    {
        targetFrame = targetFrame.Trim('/');
        sourceFrame = sourceFrame.Trim('/');

        if (targetFrame == sourceFrame)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return true;
        }

        List<TFNode> chain = new List<TFNode>();
        string current = sourceFrame;
        while (current != targetFrame)
        {
            if (!tfTree.ContainsKey(current))
            {
                Debug.LogWarning($"TFSubscriber: Could not find path from {sourceFrame} to {targetFrame}.");
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            var node = tfTree[current];
            chain.Add(node);
            current = node.parent;

            // Prevent infinite loops due to cyclic TFs
            if (chain.Count > 20)
            {
                Debug.LogError("TFSubscriber: Possible loop detected in TF chain.");
                break;
            }
        }

        // Apply the transform chain from root to leaf
        position = Vector3.zero;
        rotation = Quaternion.identity;

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            position = chain[i].rotation * position + chain[i].position;
            rotation = chain[i].rotation * rotation;
        }

        Debug.Log($"TFSubscriber: Transform from {sourceFrame} to {targetFrame} resolved. Pos={position}, Rot={rotation.eulerAngles}");
        return true;
    }

    /// <summary>
    /// Optional debug method to print the current TF tree.
    /// Useful for understanding how frames are linked.
    /// </summary>
    public void PrintTFGraph()
    {
        Debug.Log("----- TF Tree -----");
        foreach (var kvp in tfTree)
        {
            string child = kvp.Key;
            TFNode node = kvp.Value;
            Debug.Log($"TF: {node.parent} -> {child} | Pos: {node.position}, Rot: {node.rotation.eulerAngles}, t={node.time:F2}, Static={node.isStatic}");
        }
        Debug.Log("------------------------");
    }

    void Update()
    {
        // You can uncomment this line for periodic TF tree debugging
        // PrintTFGraph();
    }
}
