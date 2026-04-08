package com.tfg.telepresence

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.*
import android.media.*
import android.os.Bundle
import android.util.Log
import android.util.Size
import android.view.View
import android.view.WindowManager
import android.widget.Button
import android.widget.EditText
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.camera.core.*
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.core.content.ContextCompat
import java.io.ByteArrayOutputStream
import java.net.*
import java.util.Collections
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

class MainActivity : AppCompatActivity() {
    private lateinit var imageView: ImageView
    private lateinit var viewFinder: PreviewView
    private lateinit var tvLocalIP: TextView
    private lateinit var etIP: EditText
    private lateinit var etVideoPort: EditText
    private lateinit var etAudioPort: EditText
    private lateinit var etMicPort: EditText
    private lateinit var etCamPort: EditText
    private lateinit var btnConnect: Button
    private lateinit var configPanel: LinearLayout

    private val tag = "Telepresence"
    private var videoSocket: DatagramSocket? = null
    private var audioSocket: DatagramSocket? = null
    private var cameraSocket: DatagramSocket? = null
    private var micSocket: DatagramSocket? = null
    
    private lateinit var cameraExecutor: ExecutorService
    
    @Volatile
    private var isRunning = false
    private var frameCount = 0
    private var lastLogTime = 0L

    private val requestPermissionsLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val audioGranted = permissions[Manifest.permission.RECORD_AUDIO] ?: false
        val cameraGranted = permissions[Manifest.permission.CAMERA] ?: false
        
        if (audioGranted && cameraGranted) {
            // Permisos concedidos
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        
        // Mantener la pantalla encendida evita que el sistema pause la cámara por inactividad
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        
        imageView = findViewById(R.id.videoView)
        viewFinder = findViewById(R.id.viewFinder)
        tvLocalIP = findViewById(R.id.tvLocalIP)
        etIP = findViewById(R.id.etIP)
        etVideoPort = findViewById(R.id.etVideoPort)
        etAudioPort = findViewById(R.id.etAudioPort)
        etMicPort = findViewById(R.id.etMicPort)
        etCamPort = findViewById(R.id.etCamPort)
        btnConnect = findViewById(R.id.btnConnect)
        configPanel = findViewById(R.id.configPanel)

        cameraExecutor = Executors.newSingleThreadExecutor()

        val myIP = getLocalIpAddress() ?: "No detectada"
        tvLocalIP.text = "Mi IP: $myIP"

        btnConnect.setOnClickListener {
            if (!isRunning) {
                startStreaming()
                btnConnect.text = "Detener"
                configPanel.visibility = View.GONE
            } else {
                stopStreaming()
                btnConnect.text = "Conectar"
                configPanel.visibility = View.VISIBLE
            }
        }

        imageView.setOnClickListener {
            if (isRunning) {
                configPanel.visibility = if (configPanel.visibility == View.VISIBLE) View.GONE else View.VISIBLE
            }
        }

        checkPermissions()
    }

    private fun getLocalIpAddress(): String? {
        try {
            val interfaces = Collections.list(NetworkInterface.getNetworkInterfaces())
            for (intf in interfaces) {
                val addrs = Collections.list(intf.inetAddresses)
                for (addr in addrs) {
                    if (!addr.isLoopbackAddress) {
                        val sAddr = addr.hostAddress
                        val isIPv4 = sAddr?.indexOf(':') ?: -1 < 0
                        if (isIPv4) return sAddr
                    }
                }
            }
        } catch (ex: Exception) {
            Log.e(tag, "Error obteniendo IP local: ${ex.message}")
        }
        return null
    }

    private fun startStreaming() {
        isRunning = true
        frameCount = 0
        val ip = etIP.text.toString()
        val vPort = etVideoPort.text.toString().toIntOrNull() ?: 5000
        val aPort = etAudioPort.text.toString().toIntOrNull() ?: 5001
        val mPort = etMicPort.text.toString().toIntOrNull() ?: 5004
        val cPort = etCamPort.text.toString().toIntOrNull() ?: 5005

        startVideoReceiver(vPort)
        startAudioReceiver(aPort)
        startMicSender(ip, mPort)
        startCameraSender(ip, cPort)
    }

    private fun stopStreaming() {
        isRunning = false
        videoSocket?.close()
        audioSocket?.close()
        cameraSocket?.close()
        micSocket?.close()
    }

    override fun onDestroy() {
        super.onDestroy()
        stopStreaming()
        cameraExecutor.shutdown()
    }

    private fun checkPermissions() {
        val permissionsToRequest = mutableListOf<String>()
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            permissionsToRequest.add(Manifest.permission.RECORD_AUDIO)
        }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            permissionsToRequest.add(Manifest.permission.CAMERA)
        }

        if (permissionsToRequest.isNotEmpty()) {
            requestPermissionsLauncher.launch(permissionsToRequest.toTypedArray())
        }
    }

    private fun startVideoReceiver(port: Int) {
        Thread {
            try {
                videoSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    receiveBufferSize = 1024 * 1024
                    bind(InetSocketAddress(port))
                }
                val buffer = ByteArray(65535)
                val packet = DatagramPacket(buffer, buffer.size)

                val options = BitmapFactory.Options().apply {
                    inMutable = true
                    inPreferredConfig = Bitmap.Config.RGB_565
                }

                while(isRunning && videoSocket?.isClosed == false) {
                    packet.length = buffer.size
                    videoSocket?.receive(packet)

                    val bitmap = BitmapFactory.decodeByteArray(packet.data, 0, packet.length, options)
                    if (bitmap != null) {
                        runOnUiThread { imageView.setImageBitmap(bitmap) }
                    }
                }
            } catch (e: Exception) { Log.e(tag, "Video error: ${e.message}") }
        }.start()
    }

    private fun startAudioReceiver(port: Int) {
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

            try {
                audioSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    soTimeout = 0
                    receiveBufferSize = 65535
                    bind(InetSocketAddress(port))
                }

                val buffer = ByteArray(4096)
                val packet = DatagramPacket(buffer, buffer.size)

                while (isRunning && audioSocket?.isClosed == false) {
                    packet.length = buffer.size
                    audioSocket?.receive(packet)
                    val length = if (packet.length % 2 == 0) packet.length else packet.length - 1
                    if (length > 0) {
                        audioTrack.write(packet.data, 0, length, AudioTrack.WRITE_NON_BLOCKING)
                    }
                }
            } catch (e: Exception) {
                Log.e(tag, "AudioReceiver error: ${e.message}")
            } finally {
                audioTrack.stop()
                audioTrack.release()
                audioSocket?.close()
            }
        }.start()
    }

    private fun startMicSender(ip: String, port: Int) {
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

            try {
                micSocket = DatagramSocket()
                val serverIP = InetAddress.getByName(ip)
                val buffer = ByteArray(chunkBytes)
                recorder.startRecording()

                while (isRunning && micSocket?.isClosed == false) {
                    val read = recorder.read(buffer, 0, buffer.size)
                    if (read > 0) {
                        micSocket?.send(DatagramPacket(buffer, read, serverIP, port))
                    }
                }
            } catch (e: Exception) { 
                Log.e(tag, "Error enviando UDP audio: ${e.message}") 
            } finally {
                recorder.stop()
                recorder.release()
                micSocket?.close()
            }
        }.start()
    }

    private fun startCameraSender(ip: String, port: Int) {
        val cameraProviderFuture = ProcessCameraProvider.getInstance(this)
        cameraProviderFuture.addListener({
            val cameraProvider = cameraProviderFuture.get()

            val targetSize = Size(320, 240)

            val imageAnalysis = ImageAnalysis.Builder()
                .setTargetResolution(targetSize)
                // STRATEGY_BLOCK_PRODUCER obliga a CameraX a esperar al procesamiento, evitando que se "salte" frames quieto
                .setBackpressureStrategy(ImageAnalysis.STRATEGY_BLOCK_PRODUCER)
                .build()

            val preview = Preview.Builder()
                .setTargetResolution(targetSize)
                .build().also {
                    it.setSurfaceProvider(viewFinder.surfaceProvider)
                }

            try {
                cameraSocket = DatagramSocket()
                val serverIP = InetAddress.getByName(ip)

                imageAnalysis.setAnalyzer(cameraExecutor) { imageProxy ->
                    try {
                        if (!isRunning) return@setAnalyzer
                        
                        val jpegBytes = imageProxyToJpeg(imageProxy)
                        if (jpegBytes != null && jpegBytes.size < 65000) {
                            val packet = DatagramPacket(jpegBytes, jpegBytes.size, serverIP, port)
                            cameraSocket?.send(packet)

                            frameCount++
                            val now = System.currentTimeMillis()
                            if (now - lastLogTime > 2000) {
                                Log.d(tag, "Streaming: frame $frameCount, size: ${jpegBytes.size} bytes")
                                lastLogTime = now
                            }
                        }
                    } catch (e: Exception) {
                        Log.e(tag, "Error en analyzer: ${e.message}")
                    } finally {
                        imageProxy.close()
                    }
                }

                val cameraSelector = CameraSelector.DEFAULT_FRONT_CAMERA
                cameraProvider.unbindAll()
                cameraProvider.bindToLifecycle(this, cameraSelector, preview, imageAnalysis)
            } catch (e: Exception) {
                Log.e(tag, "Error cámara: ${e.message}")
            }
        }, ContextCompat.getMainExecutor(this))
    }

    private fun imageProxyToJpeg(image: ImageProxy): ByteArray? {
        val width = image.width
        val height = image.height
        val planes = image.planes
        
        val yBuffer = planes[0].buffer
        val uBuffer = planes[1].buffer
        val vBuffer = planes[2].buffer

        val nv21 = ByteArray(width * height * 3 / 2)
        var idY = 0
        var idUV = width * height

        for (y in 0 until height) {
            yBuffer.position(y * planes[0].rowStride)
            yBuffer.get(nv21, idY, width)
            idY += width
        }
        
        val uvRowStride = planes[1].rowStride
        val uvPixelStride = planes[1].pixelStride
        for (y in 0 until height / 2) {
            for (x in 0 until width / 2) {
                val uPos = y * uvRowStride + x * uvPixelStride
                val vPos = y * planes[2].rowStride + x * planes[2].pixelStride
                nv21[idUV++] = vBuffer.get(vPos)
                nv21[idUV++] = uBuffer.get(uPos)
            }
        }

        val yuvImage = YuvImage(nv21, ImageFormat.NV21, width, height, null)
        val out = ByteArrayOutputStream()
        // Comprimir el Rect completo del sensor
        val success = yuvImage.compressToJpeg(Rect(0, 0, width, height), 70, out)
        return if (success) out.toByteArray() else null
    }
}
