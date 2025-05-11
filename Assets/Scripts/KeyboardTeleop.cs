using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

namespace RosSharp.RosBridgeClient
{
    public class KeyboardTeleop : UnityPublisher<Twist>
    {
        private Twist message;

        public float linearSpeed = 0.3f;
        public float angularSpeed = 1.0f;

        protected override void Start()
        {
            base.Start();
            InitializeMessage();
        }

        private void FixedUpdate()
        {
            UpdateMessageFromInput();
            Publish(message);
        }

        private void InitializeMessage()
        {
            message = new Twist
            {
                linear = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
                angular = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3()
            };
        }

        private void UpdateMessageFromInput()
        {
            float move = 0.0f;
            float turn = 0.0f;

            // Movimiento lineal (adelante/atrás)
            if (Input.GetKey(KeyCode.UpArrow))
                move = 2.0f;
            else if (Input.GetKey(KeyCode.DownArrow))
                move = -2.0f;

            // Rotación (izquierda/derecha)
            if (Input.GetKey(KeyCode.LeftArrow))
                turn = 1.0f;
            else if (Input.GetKey(KeyCode.RightArrow))
                turn = -1.0f;

            message.linear.x = move * linearSpeed;
            message.angular.z = turn * angularSpeed;
        }
    }
}
