#!/usr/bin/env python
"""
Relay de PointCloud2: decima y throttlea antes de publicar.
Sustituye /camera/depth_registered/points por /camera/depth_registered/points_decimated
"""
import rospy
import sensor_msgs.point_cloud2 as pc2
from sensor_msgs.msg import PointCloud2, PointField
import struct, time

# --- CONFIG ---
INPUT_TOPIC  = "/camera/depth_registered/points"
OUTPUT_TOPIC = "/camera/depth_registered/points_decimated"
SKIP         = 10      # publicar 1 de cada N puntos
MAX_HZ       = 2.0     # maximo 2 publicaciones por segundo
# --------------

pub = None
last_publish_time = 0.0

def callback(msg):
    global last_publish_time

    now = time.time()
    if (now - last_publish_time) < (1.0 / MAX_HZ):
        return  # throttle: descartar mensaje

    last_publish_time = now

    # Leer todos los puntos (generador, eficiente)
    all_points = list(pc2.read_points(msg, skip_nans=True, field_names=("x","y","z","rgb")))

    # Decimar
    decimated = all_points[::SKIP]

    if not decimated:
        return

    # Reconstruir PointCloud2
    fields = [
        PointField('x',   0,  PointField.FLOAT32, 1),
        PointField('y',   4,  PointField.FLOAT32, 1),
        PointField('z',   8,  PointField.FLOAT32, 1),
        PointField('rgb', 12, PointField.FLOAT32, 1),
    ]

    out = pc2.create_cloud(msg.header, fields, decimated)
    pub.publish(out)

if __name__ == "__main__":
    rospy.init_node("pointcloud_relay")
    pub = rospy.Publisher(OUTPUT_TOPIC, PointCloud2, queue_size=1)
    rospy.Subscriber(INPUT_TOPIC, PointCloud2, callback, queue_size=1)
    rospy.loginfo("pointcloud_relay listo: %s -> %s (skip=%d, max_hz=%.1f)" 
                  % (INPUT_TOPIC, OUTPUT_TOPIC, SKIP, MAX_HZ))
    rospy.spin()