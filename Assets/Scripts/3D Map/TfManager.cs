//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Tf2;
//using System.Collections.Generic;
//using UnityEngine;

//using UnityVector3 = UnityEngine.Vector3;
//using UnityQuaternion = UnityEngine.Quaternion;

//using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
//using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;
//using RosTransformStamped =
//    RosSharp.RosBridgeClient.MessageTypes.Geometry.TransformStamped;

//public class TfManager : MonoBehaviour
//{
//    private RosSocket rosSocket;

//    // child_frame -> TransformStamped
//    private Dictionary<string, RosTransformStamped> tfTree =
//        new Dictionary<string, RosTransformStamped>();

//    void Start()
//    {
//        RosConnector connector = FindObjectOfType<RosConnector>();
//        rosSocket = connector.RosSocket;

//        rosSocket.Subscribe<TFMessage>("/tf", ReceiveTf);
//        rosSocket.Subscribe<TFMessage>("/tf_static", ReceiveTf);
//    }

//    private void ReceiveTf(TFMessage msg)
//    {
//        foreach (var tf in msg.transforms)
//        {
//            tfTree[tf.child_frame_id] = tf;
//        }
//    }

//    // TRANSFORMACI�N DE PUNTOS
//    public bool TryTransformPoint(
//        UnityVector3 pointRos,
//        string fromFrame,
//        string toFrame,
//        out UnityVector3 pointUnity)
//    {
//        pointUnity = UnityVector3.zero;

//        if (!BuildChain(fromFrame, toFrame, out List<RosTransformStamped> chain))
//            return false;

//        UnityVector3 p = pointRos;
//        UnityQuaternion q = UnityQuaternion.identity;

//        foreach (var tf in chain)
//        {
//            UnityVector3 t = RosToUnity(tf.transform.translation);
//            UnityQuaternion r = RosToUnity(tf.transform.rotation);

//            p = r * p + t;
//            q = r * q;
//        }

//        pointUnity = p;
//        return true;
//    }

//    // CONSTRUCCI�N DE CADENA TF
//    private bool BuildChain(
//        string from,
//        string to,
//        out List<RosTransformStamped> chain)
//    {
//        chain = new List<RosTransformStamped>();
//        string current = from;

//        while (current != to)
//        {
//            if (!tfTree.ContainsKey(current))
//                return false;

//            var tf = tfTree[current];
//            chain.Add(tf);
//            current = tf.header.frame_id;
//        }

//        return true;
//    }

//    // CONVERSI�N ROS -> UNITY
//    private UnityVector3 RosToUnity(RosVector3 v)
//    {
//        return new UnityVector3(
//            (float)-v.y,
//            (float)v.z,
//            (float)v.x
//        );
//    }

//    private UnityQuaternion RosToUnity(RosQuaternion q)
//    {
//        return new UnityQuaternion(
//            (float)-q.y,
//            (float)q.z,
//            (float)q.x,
//            (float)-q.w
//        );
//    }
//}










































using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;
using System.Collections.Generic;
using UnityEngine;

using UnityVector3 = UnityEngine.Vector3;
using UnityQuaternion = UnityEngine.Quaternion;

using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;
using RosTransformStamped = RosSharp.RosBridgeClient.MessageTypes.Geometry.TransformStamped;

public class TfManager : MonoBehaviour
{
    private RosSocket rosSocket;

    // child_frame -> list of transforms over time
    private Dictionary<string, SortedList<double, RosTransformStamped>> tfBuffer
        = new Dictionary<string, SortedList<double, RosTransformStamped>>();

    void Start()
    {
        RosConnector connector = FindObjectOfType<RosConnector>();
        rosSocket = connector.RosSocket;

        rosSocket.Subscribe<TFMessage>("/tf", ReceiveTf);
        rosSocket.Subscribe<TFMessage>("/tf_static", ReceiveTf);
    }

    private void ReceiveTf(TFMessage msg)
    {
        foreach (var tf in msg.transforms)
        {
            double t = RosTimeToDouble(tf.header.stamp);

            if (!tfBuffer.ContainsKey(tf.child_frame_id))
                tfBuffer[tf.child_frame_id] = new SortedList<double, RosTransformStamped>();

            var buffer = tfBuffer[tf.child_frame_id];

            // evitar duplicados exactos
            if (!buffer.ContainsKey(t))
                buffer[t] = tf;

            // limitar memoria (IMPORTANTE)
            if (buffer.Count > 200)
                buffer.RemoveAt(0);
        }
    }

    // =========================
    // MAIN API
    // =========================
    public bool TryTransformPointAtTime(
        Vector3 pointRos,
        string fromFrame,
        string toFrame,
        double time,
        out Vector3 result)
    {
        result = Vector3.zero;

        if (!BuildChainAtTime(fromFrame, toFrame, time, out List<RosTransformStamped> chain))
            return false;

        Vector3 p = pointRos;

        foreach (var tf in chain)
        {
            Vector3 t = RosToUnity(tf.transform.translation);
            Quaternion r = RosToUnity(tf.transform.rotation);

            p = r * p + t;
        }

        result = p;
        return true;
    }

    // =========================
    // TF CHAIN (TIME-AWARE)
    // =========================
    private bool BuildChainAtTime(
        string from,
        string to,
        double time,
        out List<RosTransformStamped> chain)
    {
        chain = new List<RosTransformStamped>();

        string current = from;

        int safety = 0;

        while (current != to)
        {
            safety++;
            if (safety > 50) return false;

            if (!tfBuffer.ContainsKey(current))
                return false;

            var buffer = tfBuffer[current];

            if (buffer.Count == 0)
                return false;

            var tf = GetClosestTf(buffer, time);

            chain.Add(tf);
            current = tf.header.frame_id;
        }

        return true;
    }

    // =========================
    // GET TF CLOSEST TO TIME
    // =========================
    private RosTransformStamped GetClosestTf(
        SortedList<double, RosTransformStamped> buffer,
        double time)
    {
        double bestKey = buffer.Keys[0];
        double bestDiff = Mathf.Abs((float)(bestKey - time));

        foreach (var k in buffer.Keys)
        {
            double diff = Mathf.Abs((float)(k - time));

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestKey = k;
            }
        }

        return buffer[bestKey];
    }

    // =========================
    // TIME CONVERSION
    // =========================
    private double RosTimeToDouble(RosSharp.RosBridgeClient.MessageTypes.Std.Time t)
    {
        return t.secs + t.nsecs * 1e-9;
    }

    // =========================
    // ROS -> UNITY
    // =========================
    private Vector3 RosToUnity(RosVector3 v)
    {
        return new Vector3(
            (float)-v.y,
            (float)v.z,
            (float)v.x
        );
    }

    private Quaternion RosToUnity(RosQuaternion q)
    {
        return new Quaternion(
            (float)-q.y,
            (float)q.z,
            (float)q.x,
            (float)-q.w
        );
    }
}