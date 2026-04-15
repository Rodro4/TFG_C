# #!/usr/bin/env python

# import rospy
# import socket
# import cv2
# import numpy as np
# from cv_bridge import CvBridge
# from sensor_msgs.msg import Image

# # CONFIG
# UDP_IP = "192.168.1.144"
# UDP_PORT = 5006
# DEPTH_PORT = 5007

# MAX_UDP_SIZE = 65000

# class CameraUDPStreamer:
    # def __init__(self):
        # self.bridge = CvBridge()
        # self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

        # rospy.Subscriber("/camera/rgb/image_raw", Image, self.callback)
        # rospy.Subscriber("/camera/depth/image_raw", Image, self.callback_depth)

        # rospy.loginfo("udp_camera_stream iniciado")

    # def callback_depth(self, msg):
        # try:
            # depth = self.bridge.imgmsg_to_cv2(msg, desired_encoding="32FC1")

            # min_depth = 0.5
            # max_depth = 5.0

            # depth = np.clip(depth, min_depth, max_depth)

            # depth_normalized = ((depth - min_depth) / (max_depth - min_depth)) * 255.0
            # depth_normalized = depth_normalized.astype(np.uint8)

            # depth_normalized = cv2.resize(depth_normalized, (1080, 720))

  
            # depth_rgb = cv2.cvtColor(depth_normalized, cv2.COLOR_GRAY2BGR)

            # encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 70]
            # result, encoded = cv2.imencode('.jpg', depth_rgb, encode_param)

            # if not result:
                # return

            # data = encoded.tobytes()

            # if len(data) < 65000:
                # self.sock.sendto(data, (UDP_IP, DEPTH_PORT))

        # except Exception as e:
            # rospy.logerr("Depth error: %s" % str(e))

    # def callback(self, msg):
        # try:
            # frame = self.bridge.imgmsg_to_cv2(msg, "bgr8")

            # frame = cv2.resize(frame, (1080, 720))

            # encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 70]
            # result, encoded = cv2.imencode('.jpg', frame, encode_param)

            # if not result:
                # return

            # data = encoded.tobytes()

            # if len(data) < MAX_UDP_SIZE:
                # self.sock.sendto(data, (UDP_IP, UDP_PORT))
            # else:
                # rospy.logwarn("Frame demasiado grande: %d bytes" % len(data))

        # except Exception as e:
            # rospy.logerr("Error: %s" % str(e))


# if __name__ == "__main__":
    # rospy.init_node("udp_camera_streamer")
    # streamer = CameraUDPStreamer()
    # rospy.spin()
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
#!/usr/bin/env python
import rospy, socket, cv2, numpy as np
from cv_bridge import CvBridge
from sensor_msgs.msg import Image

# --- CONFIGURACION CENTRALIZADA ---
IP_DESTINO = "192.168.1.144"
PUERTO_RGB = 5006
PUERTO_DEPTH = 5007
ANCHO, ALTO = 1080, 720
CALIDAD = 70      # 0 a 100
MIN_DIST = 0.5    # Metros
MAX_DIST = 5.0    # Metros
# ----------------------------------

class CameraStreamer:
    def __init__(self):
        self.bridge = CvBridge()
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        
        rospy.Subscriber("/camera/rgb/image_raw", Image, self.send_rgb)
        rospy.Subscriber("/camera/depth/image_raw", Image, self.send_depth)

    def send_rgb(self, msg):
        img = self.bridge.imgmsg_to_cv2(msg, "bgr8")
        img = cv2.resize(img, (ANCHO, ALTO))
        _, data = cv2.imencode('.jpg', img, [cv2.IMWRITE_JPEG_QUALITY, CALIDAD])
        
        if len(data) < 65000:
            self.sock.sendto(data.tobytes(), (IP_DESTINO, PUERTO_RGB))

    def send_depth(self, msg):
        # Obtenemos la imagen tal cual viene (16UC1 o 32FC1)
        depth = self.bridge.imgmsg_to_cv2(msg, msg.encoding)
        
        # Si viene en milimetros (entero 16 bits), pasamos a metros
        if "16U" in msg.encoding:
            depth = depth.astype(np.float32) / 1000.0
        
        # Limpiar valores invalidos (NaN o Inf)
        depth = np.nan_to_num(depth)
        
        # Normalizar a escala de grises (0-255) segun el rango deseado
        depth = np.clip(depth, MIN_DIST, MAX_DIST)
        norm = (((depth - MIN_DIST) / (MAX_DIST - MIN_DIST)) * 255).astype(np.uint8)
        norm = cv2.resize(norm, (ANCHO, ALTO))

        # Enviamos como JPG (mas eficiente que enviar crudo)
        _, data = cv2.imencode('.jpg', norm, [cv2.IMWRITE_JPEG_QUALITY, CALIDAD])
        
        if len(data) < 65000:
            self.sock.sendto(data.tobytes(), (IP_DESTINO, PUERTO_DEPTH))

if __name__ == "__main__":
    rospy.init_node("cam_streamer")
    CameraStreamer()
    rospy.spin()