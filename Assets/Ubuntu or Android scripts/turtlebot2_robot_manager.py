#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
turtlebot2_robot_manager.py
Servicios y publishers centralizados para el panel de control de Unity.

Servicios ROS expuestos:
  /reset_slam       â†’ Reinicia gmapping + odometrÃ­a
  /reset_odometry   â†’ Solo reinicia odometrÃ­a (sin tocar SLAM)
  /set_motors       â†’ Enciende/apaga motores (std_srvs/SetBool)

Publishers:
  /robot_status     â†’ std_msgs/String con JSON: baterÃ­a, velocidad, estado motores
"""

import rospy
import os
import json
import time
import subprocess

from std_srvs.srv import Empty, EmptyResponse, SetBool, SetBoolRequest, SetBoolResponse
from std_msgs.msg import String, Empty as EmptyMsg
from kobuki_msgs.msg import MotorPower, SensorState
from nav_msgs.msg import Odometry

# --- CONFIG ---
STATUS_RATE_HZ   = 1.0   # Frecuencia de publicaciÃ³n del estado
VMIN             = 13.5  # Voltaje mÃ­nimo de la baterÃ­a (0%)
VMAX             = 16.3  # Voltaje mÃ¡ximo de la baterÃ­a (100%)
# --------------

class RobotManager:
    def __init__(self):
        # Estado interno
        self.battery_voltage  = 0.0
        self.battery_percent  = 0.0
        self.motors_enabled   = True
        self.linear_speed     = 0.0
        self.angular_speed    = 0.0

        # Publisher de estado hacia Unity
        self.status_pub = rospy.Publisher('/robot_status', String, queue_size=1)

        # Publisher para apagar/encender motores
        self.motor_pub = rospy.Publisher(
            '/mobile_base/commands/motor_power',
            MotorPower, queue_size=1)

        # Publisher para resetear odometrÃ­a
        self.odom_reset_pub = rospy.Publisher(
            '/mobile_base/commands/reset_odometry',
            EmptyMsg, queue_size=1)

        # Suscriptores
        rospy.Subscriber('/mobile_base/sensors/core',
                         SensorState, self._cb_sensor)
        rospy.Subscriber('/odom', Odometry, self._cb_odom)

        # Servicios
        rospy.Service('/reset_slam',     Empty,   self._srv_reset_slam)
        rospy.Service('/reset_odometry', Empty,   self._srv_reset_odometry)
        rospy.Service('/set_motors',     SetBool, self._srv_set_motors)

        # Timer de publicaciÃ³n de estado
        rospy.Timer(rospy.Duration(1.0 / STATUS_RATE_HZ), self._publish_status)

        rospy.loginfo("[RobotManager] Listo. Servicios: /reset_slam, /reset_odometry, /set_motors")

    # â”€â”€â”€ CALLBACKS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    def _cb_sensor(self, msg):
        self.battery_voltage = msg.battery / 10.0
        p = (self.battery_voltage - VMIN) / (VMAX - VMIN) * 100.0
        self.battery_percent = max(0.0, min(100.0, p))

    def _cb_odom(self, msg):
        vx = msg.twist.twist.linear.x
        vy = msg.twist.twist.linear.y
        self.linear_speed  = (vx**2 + vy**2) ** 0.5
        self.angular_speed = abs(msg.twist.twist.angular.z)

    # â”€â”€â”€ SERVICIOS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    def _srv_reset_slam(self, req):
        rospy.loginfo("[RobotManager] Reiniciando SLAM + odometrÃ­a...")
        # 1. Resetear odometrÃ­a del hardware
        rospy.sleep(0.2)
        self.odom_reset_pub.publish(EmptyMsg())
        # 2. Matar y relanzar gmapping
        os.system("rosnode kill /slam_gmapping")
        rospy.sleep(1.5)
        os.system("rosrun gmapping slam_gmapping scan:=/scan __name:=slam_gmapping &")
        rospy.loginfo("[RobotManager] SLAM reiniciado.")
        return EmptyResponse()

    def _srv_reset_odometry(self, req):
        rospy.loginfo("[RobotManager] Reiniciando solo odometrÃ­a...")
        rospy.sleep(0.2)
        self.odom_reset_pub.publish(EmptyMsg())
        rospy.loginfo("[RobotManager] OdometrÃ­a reiniciada.")
        return EmptyResponse()

    def _srv_set_motors(self, req):
        """req.data = True â†’ motores ON, False â†’ motores OFF"""
        state = MotorPower()
        state.state = MotorPower.ON if req.data else MotorPower.OFF
        # Publicar varias veces para asegurar recepciÃ³n
        for _ in range(3):
            self.motor_pub.publish(state)
            rospy.sleep(0.05)
        self.motors_enabled = req.data
        label = "ON" if req.data else "OFF"
        rospy.loginfo("[RobotManager] Motores -> %s" % label)
        return SetBoolResponse(success=True, message="Motors " + label)

    # â”€â”€â”€ STATUS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    def _publish_status(self, event):
        status = {
            "battery_v":       round(self.battery_voltage, 2),
            "battery_pct":     round(self.battery_percent, 1),
            "linear_speed":    round(self.linear_speed, 3),
            "angular_speed":   round(self.angular_speed, 3),
        }
        self.status_pub.publish(String(data=json.dumps(status)))


if __name__ == "__main__":
    rospy.init_node('turtlebot2_robot_manager')
    manager = RobotManager()
    rospy.spin()