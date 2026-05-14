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
import android.widget.*
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

    // --- CONFIGURACIÓN CENTRALIZADA ---
    private var IP_DESTINO = "192.168.1.144"
    private var PUERTO_VIDEO_REC = 5000
    private var PUERTO_AUDIO_REC = 5001
    private var PUERTO_MIC_SEND = 5004
    private var PUERTO_CAM_SEND = 5005

    private var CALIDAD_JPG = 40
    private var ANCHO_VIDEO = 1080
    private var ALTO_VIDEO = 720
    private var FRECUENCIA_AUDIO = 48000
    // ----------------------------------

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

    private var videoSocket: DatagramSocket? = null
    private var audioSocket: DatagramSocket? = null
    private var cameraSocket: DatagramSocket? = null
    private var micSocket: DatagramSocket? = null
    
    private lateinit var cameraExecutor: ExecutorService
    private var isRunning = false

    private val requestPermissionsLauncher = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        
        initUI()
        cameraExecutor = Executors.newSingleThreadExecutor()
        checkPermissions()
    }

    private fun initUI() {
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

        // Cargar valores por defecto en la interfaz
        etIP.setText(IP_DESTINO)
        etVideoPort.setText(PUERTO_VIDEO_REC.toString())
        etAudioPort.setText(PUERTO_AUDIO_REC.toString())
        etMicPort.setText(PUERTO_MIC_SEND.toString())
        etCamPort.setText(PUERTO_CAM_SEND.toString())

        tvLocalIP.text = "Mi IP: ${getLocalIpAddress() ?: "Desconocida"}"

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
    }

    private fun getLocalIpAddress(): String? {
        try {
            val interfaces = Collections.list(NetworkInterface.getNetworkInterfaces())
            for (intf in interfaces) {
                val addrs = Collections.list(intf.inetAddresses)
                for (addr in addrs) {
                    if (!addr.isLoopbackAddress && addr is Inet4Address) {
                        return addr.hostAddress
                    }
                }
            }
        } catch (e: Exception) {}
        return null
    }

    private fun startStreaming() {
        isRunning = true
        val ip = etIP.text.toString()
        val vPort = etVideoPort.text.toString().toInt()
        val aPort = etAudioPort.text.toString().toInt()
        val mPort = etMicPort.text.toString().toInt()
        val cPort = etCamPort.text.toString().toInt()

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

    private fun startVideoReceiver(port: Int) {
        Thread {
            try {
                videoSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(port))
                }
                val buffer = ByteArray(65535)
                val packet = DatagramPacket(buffer, buffer.size)

                while(isRunning) {
                    videoSocket?.receive(packet)
                    val bitmap = BitmapFactory.decodeByteArray(packet.data, 0, packet.length)
                    if (bitmap != null) {
                        runOnUiThread { imageView.setImageBitmap(bitmap) }
                    }
                }
            } catch (e: Exception) {}
        }.start()
    }

    private fun startAudioReceiver(port: Int) {
        Thread {
            val minBufSize = AudioTrack.getMinBufferSize(FRECUENCIA_AUDIO, AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT)
            val audioTrack = AudioTrack(
                AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_MEDIA).setContentType(AudioAttributes.CONTENT_TYPE_SPEECH).build(),
                AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT).setSampleRate(FRECUENCIA_AUDIO).setChannelMask(AudioFormat.CHANNEL_OUT_MONO).build(),
                minBufSize, AudioTrack.MODE_STREAM, AudioManager.AUDIO_SESSION_ID_GENERATE
            )
            audioTrack.play()

            try {
                audioSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(port))
                }
                val buffer = ByteArray(4096)
                val packet = DatagramPacket(buffer, buffer.size)

                while (isRunning) {
                    audioSocket?.receive(packet)
                    audioTrack.write(packet.data, 0, packet.length)
                }
            } catch (e: Exception) {} finally {
                audioTrack.stop()
                audioTrack.release()
            }
        }.start()
    }

    private fun startMicSender(ip: String, port: Int) {
        Thread {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) return@Thread
            val recorder = AudioRecord(MediaRecorder.AudioSource.MIC, FRECUENCIA_AUDIO, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, 8192)
            try {
                micSocket = DatagramSocket()
                val serverIP = InetAddress.getByName(ip)
                val buffer = ByteArray(960)
                recorder.startRecording()
                while (isRunning) {
                    val read = recorder.read(buffer, 0, buffer.size)
                    if (read > 0) micSocket?.send(DatagramPacket(buffer, read, serverIP, port))
                }
            } catch (e: Exception) {} finally {
                recorder.stop()
                recorder.release()
            }
        }.start()
    }

    private fun startCameraSender(ip: String, port: Int) {
        val cameraProviderFuture = ProcessCameraProvider.getInstance(this)
        cameraProviderFuture.addListener({
            val cameraProvider = cameraProviderFuture.get()
            val targetSize = Size(ANCHO_VIDEO, ALTO_VIDEO)

            val imageAnalysis = ImageAnalysis.Builder()
                .setTargetResolution(targetSize)
                .setBackpressureStrategy(ImageAnalysis.STRATEGY_BLOCK_PRODUCER)
                .build()

            val preview = Preview.Builder().setTargetResolution(targetSize).build().also {
                it.setSurfaceProvider(viewFinder.surfaceProvider)
            }

            try {
                cameraSocket = DatagramSocket()
                val serverIP = InetAddress.getByName(ip)
                imageAnalysis.setAnalyzer(cameraExecutor) { imageProxy ->
                    try {
                        if (isRunning) {
                            val jpegBytes = imageProxyToJpeg(imageProxy)
                            if (jpegBytes != null) {
                                cameraSocket?.send(DatagramPacket(jpegBytes, jpegBytes.size, serverIP, port))
                            }
                        }
                    } catch (e: Exception) {} finally { imageProxy.close() }
                }
                cameraProvider.unbindAll()
                cameraProvider.bindToLifecycle(this, CameraSelector.DEFAULT_FRONT_CAMERA, preview, imageAnalysis)
            } catch (e: Exception) {}
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
        
        for (y in 0 until height / 2) {
            for (x in 0 until width / 2) {
                val uPos = y * planes[1].rowStride + x * planes[1].pixelStride
                val vPos = y * planes[2].rowStride + x * planes[2].pixelStride
                nv21[idUV++] = vBuffer.get(vPos)
                nv21[idUV++] = uBuffer.get(uPos)
            }
        }

        val out = ByteArrayOutputStream()
        val yuvImage = YuvImage(nv21, ImageFormat.NV21, width, height, null)
        return if (yuvImage.compressToJpeg(Rect(0, 0, width, height), CALIDAD_JPG, out)) out.toByteArray() else null
    }

    private fun checkPermissions() {
        val permissions = arrayOf(Manifest.permission.RECORD_AUDIO, Manifest.permission.CAMERA)
        val missing = permissions.filter { ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED }
        if (missing.isNotEmpty()) requestPermissionsLauncher.launch(missing.toTypedArray())
    }

    override fun onDestroy() {
        super.onDestroy()
        stopStreaming()
        cameraExecutor.shutdown()
    }
}
