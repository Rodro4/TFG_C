#!usr/bin/env python
# -*- coding: utf-8 -*-

import rospy
from std_srvs.srv import Empty, EmptyResponse
from std_msgs.msg import Empty as EmptyMsg
import os

def handle_reset_slamp(req):
    rospy.loginfo("Reiniciando SLAM y odometría...")
        
    # Reiniciar gmapping (SLAM)
    os.system("rosnode kill /slam_gmapping")
    rospy.sleep(1.0) # Esperar un poco antes de relanzar
    os.system("rosrun gmapping slam_gmapping scan:=/scan __name:=slam_gmapping")
        
    # Reiniciar odometría
    pub = rospy.Publisher('/mobile_base/commands/reset_odometry', EmptyMsg, queue_size=1)
    rospy.sleep(1.0) # Esperar a que el publisher esté listo
    pub.publish(EmptyMsg())
        
    return EmptyResponse()
        
def slam_reset_server():
    rospy.init_node('slam_reset_service')
    rospy.Service('reset_slam', Empty, handle_reset_slam)
    rospy.loginfo("Servicio '/reset_slam' listo para recibir llamadas.")
    rospy.spin()
    
if __name__= "__main__":
    slam_reset_server()