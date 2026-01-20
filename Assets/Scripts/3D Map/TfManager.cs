using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;
using System.Collections.Generic;
using UnityEngine;

using UnityVector3 = UnityEngine.Vector3;
using UnityQuaternion = UnityEngine.Quaternion;

using RosVector3 = RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3;
using RosQuaternion = RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion;
using RosTransformStamped =
    RosSharp.RosBridgeClient.MessageTypes.Geometry.TransformStamped;

public class TfManager : MonoBehaviour
{
    private RosSocket rosSocket;

    // child_frame -> TransformStamped
    private Dictionary<string, RosTransformStamped> tfTree =
        new Dictionary<string, RosTransformStamped>();

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
            tfTree[tf.child_frame_id] = tf;
            //Debug.Log($"TF recibido: {tf.header.frame_id} -> {tf.child_frame_id}");
        }
    }

    // =========================
    // TRANSFORMACIÓN DE PUNTOS
    // =========================

    public bool TryTransformPoint(
        UnityVector3 pointRos,
        string fromFrame,
        string toFrame,
        out UnityVector3 pointUnity)
    {
        pointUnity = UnityVector3.zero;

        if (!BuildChain(fromFrame, toFrame, out List<RosTransformStamped> chain))
            return false;

        UnityVector3 p = pointRos;
        UnityQuaternion q = UnityQuaternion.identity;

        foreach (var tf in chain)
        {
            UnityVector3 t = RosToUnity(tf.transform.translation);
            UnityQuaternion r = RosToUnity(tf.transform.rotation);

            p = r * p + t;
            q = r * q;
        }

        pointUnity = p;
        return true;
    }

    // =========================
    // CONSTRUCCIÓN DE CADENA TF
    // =========================

    private bool BuildChain(
        string from,
        string to,
        out List<RosTransformStamped> chain)
    {
        chain = new List<RosTransformStamped>();
        string current = from;

        while (current != to)
        {
            if (!tfTree.ContainsKey(current))
                return false;

            var tf = tfTree[current];
            chain.Add(tf);
            current = tf.header.frame_id;
        }

        return true;
    }

    // =========================
    // CONVERSIÓN ROS -> UNITY
    // =========================

    private UnityVector3 RosToUnity(RosVector3 v)
    {
        return new UnityVector3(
            (float)-v.y,
            (float)v.z,
            (float)v.x
        );
    }

    private UnityQuaternion RosToUnity(RosQuaternion q)
    {
        return new UnityQuaternion(
            (float)-q.y,
            (float)q.z,
            (float)q.x,
            (float)-q.w
        );
    }
}
