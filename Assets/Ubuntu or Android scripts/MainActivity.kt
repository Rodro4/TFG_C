package com.tfg.telepresence

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.*
import android.media.*
import android.os.Bundle
import android.util.Log
import android.util.Size
import android.widget.ImageView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.camera.core.*
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.core.content.ContextCompat
import java.io.ByteArrayOutputStream
import java.net.*
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

class MainActivity : AppCompatActivity() {
    private lateinit var imageView: ImageView
    private lateinit var viewFinder: PreviewView
    private val TAG = "Telepresence"
    private var videoSocket: DatagramSocket? = null
    private var audioSocket: DatagramSocket? = null
    private var cameraSocket: DatagramSocket? = null
    private lateinit var cameraExecutor: ExecutorService
    private val serverIPStr = "192.168.1.144" // IP PC / Unity
    private val requestPermissionsLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val audioGranted = permissions[Manifest.permission.RECORD_AUDIO] ?: false
        val cameraGranted = permissions[Manifest.permission.CAMERA] ?: false
        
        if (audioGranted) startMicSender()
        if (cameraGranted) startCameraSender()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        imageView = findViewById(R.id.videoView)
        viewFinder = findViewById(R.id.viewFinder)

        cameraExecutor = Executors.newSingleThreadExecutor()

        Log.d(TAG, "IP MOBILE: ${getIPAddress()}")

        startVideoReceiver()
        startAudioReceiver()
        checkPermissions()
    }

    override fun onDestroy() {
        super.onDestroy()
        videoSocket?.close()
        audioSocket?.close()
        cameraSocket?.close()
        cameraExecutor.shutdown()
    }

    private fun getIPAddress(): String {
        try {
            val interfaces = NetworkInterface.getNetworkInterfaces()
            while (interfaces.hasMoreElements()) {
                val networkInterface = interfaces.nextElement()
                val addresses = networkInterface.inetAddresses
                while (addresses.hasMoreElements()) {
                    val address = addresses.nextElement()
                    if (!address.isLoopbackAddress && address.hostAddress.contains(".")) {
                        return address.hostAddress
                    }
                }
            }
        } catch (e: Exception) { e.printStackTrace() }
        return "No encontrada"
    }

    private fun checkPermissions() {
        val permissionsToRequest = mutableListOf<String>()
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            permissionsToRequest.add(Manifest.permission.RECORD_AUDIO)
        } else {
            startMicSender()
        }
        
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            permissionsToRequest.add(Manifest.permission.CAMERA)
        } else {
            startCameraSender()
        }

        if (permissionsToRequest.isNotEmpty()) {
            requestPermissionsLauncher.launch(permissionsToRequest.toTypedArray())
        }
    }

    private fun startVideoReceiver() {
        Thread {
            try {
                videoSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    // MEJORA: Aumentamos el buffer de recepción a 1MB para evitar pérdida de frames
                    receiveBufferSize = 1024 * 1024
                    bind(InetSocketAddress(5000))
                }
                val buffer = ByteArray(65535)
                val packet = DatagramPacket(buffer, buffer.size)

                // Opción para decodificación más rápida
                val options = BitmapFactory.Options().apply {
                    inMutable = true // Permite reutilizar memoria si fuera necesario
                    inPreferredConfig = Bitmap.Config.RGB_565 // Menos memoria que ARGB_8888
                }

                while(videoSocket?.isClosed == false) {
                    packet.length = buffer.size
                    videoSocket?.receive(packet)

                    val bitmap = BitmapFactory.decodeByteArray(packet.data, 0, packet.length, options)
                    if (bitmap != null) {
                        runOnUiThread { imageView.setImageBitmap(bitmap) }
                    }
                }
            } catch (e: Exception) { Log.e(TAG, "Video error: ${e.message}") }
        }.start()
    }

    private fun startAudioReceiver() {
        Thread {
            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_URGENT_AUDIO)

            val sampleRate = 48000
            val channelConfig = AudioFormat.CHANNEL_OUT_MONO
            val audioFormat = AudioFormat.ENCODING_PCM_16BIT

            val minBufSize = AudioTrack.getMinBufferSize(sampleRate, channelConfig, audioFormat)

            val audioTrack = AudioTrack(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                    .build(),
                AudioFormat.Builder()
                    .setEncoding(audioFormat)
                    .setSampleRate(sampleRate)
                    .setChannelMask(channelConfig)
                    .build(),
                minBufSize / 4,
                AudioTrack.MODE_STREAM,
                AudioManager.AUDIO_SESSION_ID_GENERATE
            )

            audioTrack.play()

            val socket = DatagramSocket(null).apply {
                reuseAddress = true
                soTimeout = 0
                receiveBufferSize = 65535
                bind(InetSocketAddress(5001))
            }

            val buffer = ByteArray(4096)
            val packet = DatagramPacket(buffer, buffer.size)

            try {
                while (true) {
                    packet.length = buffer.size
                    socket.receive(packet)
                    val length = if (packet.length % 2 == 0) packet.length else packet.length - 1
                    if (length > 0) {
                        audioTrack.write(packet.data, 0, length, AudioTrack.WRITE_NON_BLOCKING)
                    }
                }
            } catch (e: Exception) {
                Log.e(TAG, "AudioReceiver error: ${e.message}")
            } finally {
                audioTrack.stop()
                audioTrack.release()
                socket.close()
            }
        }.start()
    }

    private fun startMicSender() {
        Thread {
            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_URGENT_AUDIO)

            val sampleRate = 48000
            val chunkSamples = 480
            val chunkBytes = chunkSamples * 2

            if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) return@Thread

            val recorder = AudioRecord(
                MediaRecorder.AudioSource.VOICE_COMMUNICATION,
                sampleRate,
                AudioFormat.CHANNEL_IN_MONO,
                AudioFormat.ENCODING_PCM_16BIT,
                chunkBytes * 4
            )

            val socket = DatagramSocket()
            val serverIP = InetAddress.getByName(serverIPStr)

            val buffer = ByteArray(chunkBytes)
            recorder.startRecording()

            while (true) {
                val read = recorder.read(buffer, 0, buffer.size)
                if (read > 0) {
                    try {
                        socket.send(DatagramPacket(buffer, read, serverIP, 5004))
                    } catch (e: Exception) { Log.e(TAG, "Error enviando UDP audio: ${e.message}") }
                }
            }
        }.start()
    }

    // Sustituye estos métodos en tu MainActivity.kt

    private fun startCameraSender() {val cameraProviderFuture = ProcessCameraProvider.getInstance(this)
        cameraProviderFuture.addListener({
            val cameraProvider = cameraProviderFuture.get()

            val imageAnalysis = ImageAnalysis.Builder()
                // Bajamos un poco la resolución para asegurar que el paquete quepa en UDP
                .setTargetResolution(Size(176, 144))
                .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                .build()

            cameraSocket = DatagramSocket()
            val serverIP = InetAddress.getByName(serverIPStr)

            imageAnalysis.setAnalyzer(cameraExecutor) { imageProxy ->
                val jpegBytes = imageProxyToJpeg(imageProxy)
                imageProxy.close()

                // IMPORTANTE: UDP es inestable con paquetes > 15-20KB.
                // Intentamos mantenernos bajos.
                if (jpegBytes != null && jpegBytes.size < 65000) {
                    try {
                        val packet = DatagramPacket(jpegBytes, jpegBytes.size, serverIP, 5005)
                        cameraSocket?.send(packet)
                        // Log.d(TAG, "Enviado: ${jpegBytes.size} bytes") // Para debug
                    } catch (e: Exception) {
                        Log.e(TAG, "Error UDP: ${e.message}")
                    }
                }
            }

            val cameraSelector = CameraSelector.DEFAULT_FRONT_CAMERA
            val preview = Preview.Builder().build().also {
                it.setSurfaceProvider(viewFinder.surfaceProvider)
            }

            try {
                cameraProvider.unbindAll()
                cameraProvider.bindToLifecycle(this, cameraSelector, preview, imageAnalysis)
            } catch (e: Exception) {
                Log.e(TAG, "Error cámara: ${e.message}")
            }
        }, ContextCompat.getMainExecutor(this))
    }

    private fun imageProxyToJpeg(image: ImageProxy): ByteArray? {
        // Conversión robusta de YUV_420_888 a NV21 respetando strides
        val width = image.width
        val height = image.height
        val yPlane = image.planes[0]
        val uPlane = image.planes[1]
        val vPlane = image.planes[2]

        val yBuffer = yPlane.buffer
        val uBuffer = uPlane.buffer
        val vBuffer = vPlane.buffer

        val yRowStride = yPlane.rowStride
        val uvRowStride = uPlane.rowStride
        val uvPixelStride = uPlane.pixelStride

        val nv21 = ByteArray(width * height * 3 / 2)
        var idY = 0
        var idUV = width * height

        // Copiar plano Y
        for (y in 0 until height) {
            yBuffer.position(y * yRowStride)
            yBuffer.get(nv21, idY, width)
            idY += width
        }

        // Copiar planos U y V entrelazados (NV21 es V-U-V-U)
        for (y in 0 until height / 2) {
            for (x in 0 until width / 2) {
                val uPos = y * uvRowStride + x * uvPixelStride
                val vPos = y * vPlane.rowStride + x * vPlane.pixelStride
                nv21[idUV++] = vBuffer.get(vPos)
                nv21[idUV++] = uBuffer.get(uPos)
            }
        }

        val yuvImage = YuvImage(nv21, ImageFormat.NV21, width, height, null)
        val out = ByteArrayOutputStream()
        // Calidad 30 es suficiente para pruebas y mantiene el paquete pequeño
        val success = yuvImage.compressToJpeg(Rect(0, 0, width, height), 30, out)
        return if (success) out.toByteArray() else null
    }
}
