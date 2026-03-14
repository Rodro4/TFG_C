package com.tfg.telepresence

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
import android.media.*
import android.os.Bundle
import android.util.Log
import android.widget.ImageView
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.SocketTimeoutException

class MainActivity : AppCompatActivity() {

    private lateinit var imageView: ImageView
    private val TAG = "TelepresenceApp"

    private var videoSocket: DatagramSocket? = null
    private var audioSocket: DatagramSocket? = null

    private val requestPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { isGranted: Boolean ->
        if (isGranted) startMicSender()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        imageView = findViewById(R.id.videoView)

        Log.d(TAG, "IP MOBILE: ${getIPAddress()}")

        startVideoReceiver()
        startAudioReceiver()
        checkPermissions()
    }

    override fun onDestroy() {
        super.onDestroy()
        videoSocket?.close()
        audioSocket?.close()
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
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO)
            == PackageManager.PERMISSION_GRANTED) {
            startMicSender()
        } else {
            requestPermissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }

    private fun startVideoReceiver() {
        Thread {
            try {
                videoSocket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(5000))
                }
                val buffer = ByteArray(65535)
                val packet = DatagramPacket(buffer, buffer.size)

                while(videoSocket?.isClosed == false) {
                    packet.length = buffer.size
                    videoSocket?.receive(packet)
                    val imageBytes = packet.data.copyOf(packet.length)
                    val bitmap = BitmapFactory.decodeByteArray(imageBytes, 0, imageBytes.size)
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

            Log.d(TAG, "AudioTrack iniciado")

            val socket = DatagramSocket(null).apply {
                reuseAddress = true
                soTimeout = 0
                receiveBufferSize = 65535
                bind(InetSocketAddress(5001))
            }

            val buffer = ByteArray(4096)
            val packet = DatagramPacket(buffer, buffer.size)

            try {

                Log.d(TAG, "Esperando audio")

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

                Log.d(TAG, "AudioTrack detenido")

            }

        }.start()
    }



    private fun startMicSender() {

        Thread {

            android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_URGENT_AUDIO)

            val sampleRate = 48000
            val chunkSamples = 480
            val chunkBytes = chunkSamples * 2

            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.RECORD_AUDIO
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                Log.e(TAG, "No se tiene permiso de micrófono")
                return@Thread
            }

            val recorder = AudioRecord(
                MediaRecorder.AudioSource.VOICE_COMMUNICATION,
                sampleRate,
                AudioFormat.CHANNEL_IN_MONO,
                AudioFormat.ENCODING_PCM_16BIT,
                chunkBytes * 4
            )

            val socket = DatagramSocket()
            val serverIP = InetAddress.getByName("192.168.1.144") // IP PC

            val buffer = ByteArray(chunkBytes)

            Log.d(TAG, "Iniciando AudioRecord...")

            recorder.startRecording()
            Log.d(TAG, "AudioRecord iniciado, enviando a $serverIP:5004")

            var counter = 0

            while (true) {
                val read = recorder.read(buffer, 0, buffer.size)

                if (read > 0) {
                    Log.d(TAG, "Leído chunk #$counter, bytes: $read")
                    counter++

                    try {
                        socket.send(DatagramPacket(buffer, read, serverIP, 5004)) // PUERTO
                        Log.d(TAG, "Paquete enviado #$counter")
                    } catch (e: Exception) {
                        Log.e(TAG, "Error enviando UDP: ${e.message}")
                    }
                } else {
                    Log.e(TAG, "No se leyó nada del micrófono")
                }
            }
        }.start()
    }
}
