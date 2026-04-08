#!/usr/bin/env python

import rospy
import socket
import cv2
import numpy as np
from cv_bridge import CvBridge
from sensor_msgs.msg import Image

# CONFIG
UDP_IP = "192.168.1.144"
UDP_PORT = 5006
DEPTH_PORT = 5007

MAX_UDP_SIZE = 65000

class CameraUDPStreamer:
    def __init__(self):
        self.bridge = CvBridge()
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

        rospy.Subscriber("/camera/rgb/image_raw", Image, self.callback)
        rospy.Subscriber("/camera/depth/image_raw", Image, self.callback_depth)

        rospy.loginfo("udp_camera_stream iniciado")

    def callback_depth(self, msg):
        try:
            depth = self.bridge.imgmsg_to_cv2(msg, desired_encoding="32FC1")

            min_depth = 0.5
            max_depth = 5.0

            depth = np.clip(depth, min_depth, max_depth)

            depth_normalized = ((depth - min_depth) / (max_depth - min_depth)) * 255.0
            depth_normalized = depth_normalized.astype(np.uint8)

            depth_normalized = cv2.resize(depth_normalized, (1920, 1080))

  
            depth_rgb = cv2.cvtColor(depth_normalized, cv2.COLOR_GRAY2BGR)

            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 70]
            result, encoded = cv2.imencode('.jpg', depth_rgb, encode_param)

            if not result:
                return

            data = encoded.tobytes()

            if len(data) < 65000:
                self.sock.sendto(data, (UDP_IP, DEPTH_PORT))

        except Exception as e:
            rospy.logerr("Depth error: %s" % str(e))

    def callback(self, msg):
        try:
            frame = self.bridge.imgmsg_to_cv2(msg, "bgr8")

            frame = cv2.resize(frame, (1920, 1080))

            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 70]
            result, encoded = cv2.imencode('.jpg', frame, encode_param)

            if not result:
                return

            data = encoded.tobytes()

            if len(data) < MAX_UDP_SIZE:
                self.sock.sendto(data, (UDP_IP, UDP_PORT))
            else:
                rospy.logwarn("Frame demasiado grande: %d bytes" % len(data))

        except Exception as e:
            rospy.logerr("Error: %s" % str(e))


if __name__ == "__main__":
    rospy.init_node("udp_camera_streamer")
    streamer = CameraUDPStreamer()
    rospy.spin()