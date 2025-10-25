// TFSubscriber.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;

public class TFSubscriber : MonoBehaviour
{
    public string tfTopic = "/tf";
    public string tfStaticTopic = "/tf_static";

    private RosSocket rosSocket;

    private class TFNode
    {
        public string parent;
        public Vector3 position;
        public Quaternion rotation;
        public double time;
        public bool isStatic;
    }

    private readonly Dictionary<string, List<TFNode>> tfHistory = new();

    void Start()
    {
        var rosConnector = FindObjectOfType<RosConnector>();
        if (rosConnector == null)
        {
            Debug.LogError("TFSubscriber: RosConnector no encontrado.");
            enabled = false;
            return;
        }

        rosSocket = rosConnector.RosSocket;
        rosSocket.Subscribe<TFMessage>(tfTopic, msg => ReceiveTFMessage(msg, false), 0);
        rosSocket.Subscribe<TFMessage>(tfStaticTopic, msg => ReceiveTFMessage(msg, true), 0);
        Debug.Log("TFSubscriber: Subscribed to /tf and /tf_static");
    }

    private void ReceiveTFMessage(TFMessage msg, bool isStatic)
    {
        foreach (var tf in msg.transforms)
        {
            string parent = tf.header.frame_id.Trim('/');
            string child = tf.child_frame_id.Trim('/');

            var node = new TFNode
            {
                parent = parent,
                position = new Vector3((float)tf.transform.translation.x,
                                        (float)tf.transform.translation.y,
                                        (float)tf.transform.translation.z),
                rotation = new Quaternion((float)tf.transform.rotation.x,
                                           (float)tf.transform.rotation.y,
                                           (float)tf.transform.rotation.z,
                                           (float)tf.transform.rotation.w),
                time = tf.header.stamp.secs + tf.header.stamp.nsecs * 1e-9,
                isStatic = isStatic
            };

            if (!tfHistory.TryGetValue(child, out var list))
            {
                list = new List<TFNode>();
                tfHistory[child] = list;
            }

            if (isStatic)
            {
                list.Clear();
                list.Add(node);
            }
            else
            {
                list.Add(node);

                // PURGA: mantén solo los últimos N segundos
                double cutoff = node.time - 10.0; // 10 segundos de historial
                list.RemoveAll(n => n.time < cutoff);
            }
        }
    }

    /// <summary>
    /// Resuelve la transformación sourceFrame->targetFrame en el instante timeStamp.
    /// </summary>
    public bool TryGetTransformAtTime(string targetFrame, string sourceFrame, double timeStamp, out Vector3 position, out Quaternion rotation)
    {
        targetFrame = targetFrame.Trim('/');
        sourceFrame = sourceFrame.Trim('/');

        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (targetFrame == sourceFrame)
            return true;

        var chain = new List<TFNode>();
        string current = sourceFrame;
        int safety = 0;
        while (current != targetFrame && safety++ < 50)
        {
            if (!tfHistory.TryGetValue(current, out var list) || list.Count == 0)
                return false;

            TFNode best = list
                .Where(n => n.time <= timeStamp || n.isStatic)
                .OrderByDescending(n => n.time)
                .FirstOrDefault();
            if (best == null) return false;

            double tfAge = timeStamp - best.time;
            if (tfAge > 1.0)
            {
               Debug.LogWarning($"TF too old! Age: {tfAge:F2}s (TF time: {best.time:F2}, cloud time: {timeStamp:F2})");
            }

            chain.Add(best);
            current = best.parent;
        }

        if (current != targetFrame)
            return false;

        position = Vector3.zero;
        rotation = Quaternion.identity;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var n = chain[i];
            position = n.rotation * position + n.position;
            rotation = n.rotation * rotation;
        }

        return true;
    }
}
