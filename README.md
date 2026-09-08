# Telepresencia y Reconstrucción 3D con Turtlebot2 y Realidad Virtual

Dos Trabajos de Fin de Grado (URJC, 2025-2026) que integran ROS, Unity y realidad virtual sobre un robot Turtlebot2:

- **TFG Ingeniería de Computadores** - Teleoperación del robot mediante VR, con comunicación audiovisual bidireccional y avatar con sincronización labial.
- **TFG Diseño y Desarrollo de Videojuegos** - Reconstrucción tridimensional del entorno en tiempo real (cuatro técnicas de visualización) y minijuego cooperativo jugador-robot.

Ambos comparten la misma base técnica y buena parte del código.

## Arquitectura

- **Robot**: Turtlebot2 (Ubuntu 16.04, ROS Kinetic), cámara RGB-D Orbbec Astra.
- **Unity**: núcleo del sistema, cliente de VR (Meta Quest 2), conectado al robot mediante ROS# / rosbridge (WebSocket).
- **Android** (solo TFG Computadores): app en Kotlin para audio y vídeo bidireccional con el interlocutor, vía UDP.

La comunicación de control y estado usa rosbridge; la de audio/vídeo en tiempo real usa UDP directo.

## Requisitos

- ROS Kinetic sobre Ubuntu 16.04 (o Gazebo 7 para el entorno simulado)
- Unity 2022.3+ con XR Interaction Toolkit
- Python 2.7 (nodos del robot)
- Meta Quest 2
- Android Studio (solo si se usa la app móvil)

## Estructura del repositorio

```
Assets/Scripts/
├── 3D Map/              Suscriptores ROS y renderizado de mapas
├── VR/                  Teleoperación y lógica del minijuego
├── Audio/                Envío/recepción de audio por UDP
└── UI Gimmicks/         Paneles de control e interfaz

Assets/Ubuntu or Android scripts/
├── udp_camera_stream_robust.py    Streaming RGB-D del robot
├── pointcloud_relay.py            Decimación de la nube de puntos
├── turtlebot2_robot_manager.py    Servicios y estado del robot
└── MainActivity.kt                App Android

TFG_C.tex   Memoria del TFG de Computadores
TFG_V.tex   Memoria del TFG de Videojuegos
```

## Puesta en marcha

En el robot (o la máquina virtual con el simulador):

```bash
roslaunch turtlebot_bringup minimal.launch
roslaunch turtlebot_navigation gmapping_demo.launch
roslaunch rosbridge_server rosbridge_websocket.launch
python pointcloud_relay.py
rosrun octomap_server octomap_color_server_node \
  cloud_in:=/camera/depth_registered/points _resolution:=0.05 _frame_id:=map
python turtlebot2_robot_manager.py
```

En Unity: asignar la IP del robot en `RosConnector`, conectar el visor y ejecutar.

## Documentación

Las memorias (`MEMORIA_TFG_VIDEOJUEGOS.pdf` y `MEMORIA_TFG_COMPUTADORES.pdf`) describen en detalle la arquitectura, las decisiones de diseño, los experimentos realizados y los problemas encontrados durante el desarrollo.

## Autor

Rodrigo Hervada Llahosa - Tutor: Jonathan Crespo Herrero - URJC
