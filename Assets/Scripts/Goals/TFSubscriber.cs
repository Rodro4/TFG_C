//using UnityEngine;
//using System.Collections.Generic;
//using RosSharp.RosBridgeClient;
//using RosSharp.RosBridgeClient.MessageTypes.Tf2;
//using RosSharp.RosBridgeClient.MessageTypes.Geometry;

//namespace RosSharp.RosBridgeClient
//{
//    public class TFSubscriber : UnitySubscriber<TFMessage>
//    {
//        private Dictionary<string, TransformStamped> tfTransforms = new Dictionary<string, TransformStamped>();

//        protected override void Start()
//        {
//            base.Start();
//        }

//        protected override void ReceiveMessage(TFMessage message)
//        {
//            foreach (TransformStamped transform in message.transforms)
//            {
//                tfTransforms[transform.child_frame_id] = transform;
//            }
//        }

//        public bool TryGetTransform(string childFrame, string parentFrame, out Matrix4x4 result)
//        {
//            result = Matrix4x4.identity;

//            if (!tfTransforms.ContainsKey(childFrame))
//                return false;

//            TransformStamped transform = tfTransforms[childFrame];

//            if (transform.header.frame_id != parentFrame)
//                return false;

//            // Usa explícitamente UnityEngine.Vector3 y UnityEngine.Quaternion
//            UnityEngine.Vector3 position = new UnityEngine.Vector3(
//                (float)transform.transform.translation.x,
//                (float)transform.transform.translation.y,
//                (float)transform.transform.translation.z
//            );

//            UnityEngine.Quaternion rotation = new UnityEngine.Quaternion(
//                (float)transform.transform.rotation.x,
//                (float)transform.transform.rotation.y,
//                (float)transform.transform.rotation.z,
//                (float)transform.transform.rotation.w
//            );

//            result = Matrix4x4.TRS(position, rotation, UnityEngine.Vector3.one);
//            return true;
//        }

//        public UnityEngine.Vector3 TransformPoint(string fromFrame, string toFrame, UnityEngine.Vector3 point)
//        {
//            if (!TryGetTransform(fromFrame, toFrame, out Matrix4x4 tfMatrix))
//            {
//                Debug.LogWarning($"Transformación de {fromFrame} a {toFrame} no encontrada.");
//                return point;
//            }

//            return tfMatrix.MultiplyPoint3x4(point);
//        }
//    }
//}
