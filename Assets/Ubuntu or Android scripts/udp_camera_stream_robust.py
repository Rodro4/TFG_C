#!/usr/bin/env python
"""
Robust UDP Camera Streamer for Turtlebot2 RGBD camera
- Reconexion automatica ante fallos de red
- Manejo de errores de red sin crashes
- Lazy publishing fix (fuerza suscriptores activos)
- Logging detallado
"""

import rospy
import socket
import cv2
import numpy as np
import sys
import time
from cv_bridge import CvBridge
from sensor_msgs.msg import Image

# --- CONFIGURACION CENTRALIZADA ---
IP_DESTINO = "10.169.26.139"
PUERTO_RGB = 5006
PUERTO_DEPTH = 5007
ANCHO, ALTO = 1080, 720
CALIDAD = 50      # 0 a 100
MIN_DIST = 0.5    # Metros
MAX_DIST = 5.0    # Metros
TIMEOUT_SOCKET = 2.0  # segundos
RECONECT_DELAY = 2.0  # segundos entre intentos
# ----------------------------------

class CameraStreamer:
    def __init__(self):
        self.bridge = CvBridge()
        self.sock = None
        self.last_error_time = 0
        self.error_count = 0
        self.success_count = 0
        self.enable_rgb = True
	self.enable_depth = False

	rospy.Subscriber("/camera_stream_mode", rospy.AnyMsg, self.on_stream_mode)
        rospy.loginfo("[CameraStreamer] Inicializando...")
        self.reconnect()
        
        # Suscriptores (estos activan lazy publishing del driver)
        rospy.loginfo("[CameraStreamer] Suscribiendo a topicos de camara...")
        rospy.Subscriber("/camera/rgb/image_raw", Image, self.send_rgb, queue_size=1)
        rospy.Subscriber("/camera/depth/image_raw", Image, self.send_depth, queue_size=1)
        
        rospy.loginfo("[CameraStreamer] Ready. Streaming a %s:%d y %s:%d" % 
                     (IP_DESTINO, PUERTO_RGB, IP_DESTINO, PUERTO_DEPTH))

    def on_stream_mode(self, msg):
        # msg.data contiene "rgb" o "depth"
        if hasattr(msg, 'data'):
            mode = msg.data
            self.enable_rgb = (mode == "rgb")
            self.enable_depth = (mode == "depth")
            rospy.loginfo("Stream mode: RGB=%s, Depth=%s" % (self.send_rgb, self.send_depth))

    def reconnect(self):
        """Crea/recrea el socket UDP con manejo de errores"""
        try:
            if self.sock:
                self.sock.close()
            
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.sock.settimeout(TIMEOUT_SOCKET)
            
            rospy.loginfo("[CameraStreamer] Socket UDP creado correctamente")
            self.error_count = 0  # Reset contador de errores al reconectar
            
        except socket.error as e:
            rospy.logerr("[CameraStreamer] Error creando socket: %s" % str(e))
            self.sock = None
            time.sleep(RECONECT_DELAY)

    def send_rgb(self, msg):
        """Envia frame RGB comprimido por UDP"""
	if not self.enable_rgb:
	    return
        if self.sock is None:
            self.reconnect()
            return
        
        try:
            # Convertir y redimensionar
            img = self.bridge.imgmsg_to_cv2(msg, "bgr8")
            img = cv2.resize(img, (ANCHO, ALTO))
            
            # Comprimir JPEG
            _, data = cv2.imencode('.jpg', img, [cv2.IMWRITE_JPEG_QUALITY, CALIDAD])
            
            # Enviar solo si el tamano es reasonable (evita fragmentacion UDP)
            if len(data) < 65000:
                self.sock.sendto(data.tobytes(), (IP_DESTINO, PUERTO_RGB))
                self.success_count += 1
                
                # Log de exito cada 100 frames
                if self.success_count % 100 == 0:
                    rospy.logdebug("[CameraStreamer] RGB: %d frames enviados correctamente" % self.success_count)
            else:
                rospy.logwarn("[CameraStreamer] RGB frame demasiado grande (%d bytes), descartado" % len(data))
        
        except socket.timeout:
            self._handle_network_error("RGB", "Socket timeout (host inaccesible)")
        
        except socket.error as e:
            if e.errno == 101:  # Network is unreachable
                self._handle_network_error("RGB", "Network unreachable")
            elif e.errno == 111:  # Connection refused
                self._handle_network_error("RGB", "Connection refused")
            else:
                rospy.logwarn("[CameraStreamer] RGB socket error (%d): %s" % (e.errno, str(e)))
        
        except Exception as e:
            rospy.logerr("[CameraStreamer] Unexpected error in send_rgb: %s" % str(e))

    def send_depth(self, msg):
	if not self.enable_depth:
	    return
        """Envia mapa de profundidad comprimido por UDP"""
        if self.sock is None:
            self.reconnect()
            return
        
        try:
            # Obtener imagen de profundidad en su formato nativo
            depth = self.bridge.imgmsg_to_cv2(msg, msg.encoding)
            
            # Si viene en milimetros (entero 16 bits), convertir a metros
            if "16U" in msg.encoding:
                depth = depth.astype(np.float32) / 1000.0
            elif "32F" in msg.encoding:
                depth = depth.astype(np.float32)
            
            # Limpiar valores invalidos (NaN o Inf) - compatible con NumPy antiguo
            depth = np.nan_to_num(depth)
            depth[np.isinf(depth) & (depth > 0)] = MAX_DIST
            depth[np.isinf(depth) & (depth < 0)] = MIN_DIST
            
            # Normalizar a escala de grises (0-255) segun el rango deseado
            depth = np.clip(depth, MIN_DIST, MAX_DIST)
            norm = (((depth - MIN_DIST) / (MAX_DIST - MIN_DIST)) * 255).astype(np.uint8)
            norm = cv2.resize(norm, (ANCHO, ALTO))
            
            # Comprimir JPEG
            _, data = cv2.imencode('.jpg', norm, [cv2.IMWRITE_JPEG_QUALITY, CALIDAD])
            
            # Enviar
            if len(data) < 65000:
                self.sock.sendto(data.tobytes(), (IP_DESTINO, PUERTO_DEPTH))
            else:
                rospy.logwarn("[CameraStreamer] Depth frame demasiado grande (%d bytes), descartado" % len(data))
        
        except socket.timeout:
            self._handle_network_error("Depth", "Socket timeout (host inaccesible)")
        
        except socket.error as e:
            if e.errno == 101:  # Network is unreachable
                self._handle_network_error("Depth", "Network unreachable")
            elif e.errno == 111:  # Connection refused
                self._handle_network_error("Depth", "Connection refused")
            else:
                rospy.logwarn("[CameraStreamer] Depth socket error (%d): %s" % (e.errno, str(e)))
        
        except Exception as e:
            rospy.logerr("[CameraStreamer] Unexpected error in send_depth: %s" % str(e))

    def _handle_network_error(self, stream_type, error_msg):
        """Maneja errores de red con reconexion inteligente"""
        self.error_count += 1
        current_time = time.time()
        
        # Log solo una vez cada 10 segundos para no saturar
        if current_time - self.last_error_time > 10.0:
            rospy.logwarn("[CameraStreamer] %s: %s (error #%d)" % 
                         (stream_type, error_msg, self.error_count))
            self.last_error_time = current_time
            
            # Intentar reconectar
            rospy.loginfo("[CameraStreamer] Intentando reconectar en %f segundos..." % RECONECT_DELAY)
            self.reconnect()

if __name__ == "__main__":
    try:
        rospy.init_node("udp_camera_streamer", log_level=rospy.INFO)
        streamer = CameraStreamer()
        rospy.spin()
    
    except KeyboardInterrupt:
        rospy.loginfo("[CameraStreamer] Shutdown por usuario")
        sys.exit(0)
    
    except Exception as e:
        rospy.logerr("[CameraStreamer] Error fatal: %s" % str(e))
        sys.exit(1)