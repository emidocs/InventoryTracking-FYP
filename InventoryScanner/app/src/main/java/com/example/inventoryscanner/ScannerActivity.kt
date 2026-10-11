package com.example.inventoryscanner

import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import android.content.Context
import android.content.Intent

class ScannerActivity : AppCompatActivity() {

    private var scannedAssetId: Int = 0

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // 1. Draw the screen before finding buttons.
        setContentView(R.layout.activity_scanner)
        // Retrieve the saved username, defaulting to "Technician" if it somehow fails
        val sharedPref = getSharedPreferences("InventoryPrefs", Context.MODE_PRIVATE)
        val savedUsername = sharedPref.getString("USERNAME", "Technician")

// Set the welcome text
        val tvWelcome = findViewById<TextView>(R.id.tvWelcomeMessage)
        tvWelcome.text = "Welcome, $savedUsername!"

        // 2. Now you can find the buttons
        val btnLogout = findViewById<Button>(R.id.btnLogout)
        btnLogout.setOnClickListener {
            val sharedPref = getSharedPreferences("InventoryPrefs", Context.MODE_PRIVATE)
            sharedPref.edit().remove("JWT_TOKEN").apply()

            val intent = Intent(this, MainActivity::class.java)
            startActivity(intent)
            finish()
        }

        val tvScanResult = findViewById<TextView>(R.id.tvScanResult)
        val btnScan = findViewById<Button>(R.id.btnScan)

        // Scanner 1: Strictly for the UTHM QR Code
        val qrOptions = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .build()
        val qrScanner = GmsBarcodeScanning.getClient(this, qrOptions)

        // Scanner 2: For the Hardware Manufacturer Barcode
        val barcodeOptions = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_ALL_FORMATS)
            .build()
        val barcodeScanner = GmsBarcodeScanning.getClient(this, barcodeOptions)

        btnScan.setOnClickListener {
            tvScanResult.text = "Step 1: Scanning QR tag..."

            // ---------------------------------------------
            // STEP 1: LAUNCH THE QR SCANNER
            // ---------------------------------------------
            qrScanner.startScan()
                .addOnSuccessListener { qrCode ->
                    val rawQrValue = qrCode.rawValue ?: ""

                    try {
                        // We expect the Python script format: "ASSET_ID:1"
                        // This strips the text and leaves just the integer
                        scannedAssetId = rawQrValue.replace("ASSET_ID:", "").toInt()

                        tvScanResult.text = "Asset #$scannedAssetId Linked.\nStep 2: Scanning Hardware..."
                        Toast.makeText(this, "QR Scanned! Now scan the hardware barcode.", Toast.LENGTH_LONG).show()

                        // ---------------------------------------------
                        // STEP 2: IMMEDIATELY LAUNCH THE BARCODE SCANNER
                        // ---------------------------------------------
                        barcodeScanner.startScan()
                            .addOnSuccessListener { hwBarcode ->
                                val serialNumber = hwBarcode.rawValue ?: ""
                                tvScanResult.text = "Verifying Asset #$scannedAssetId\nSN: $serialNumber"

                                // Both scans complete! Send the REAL data to the server.
                                sendVerificationToServer(scannedAssetId, serialNumber)
                            }
                            .addOnCanceledListener {
                                Toast.makeText(this, "Hardware Scan Cancelled", Toast.LENGTH_SHORT).show()
                                tvScanResult.text = "Scan Cancelled"
                            }
                    } catch (e: Exception) {
                        Toast.makeText(this, "Invalid QR Code!", Toast.LENGTH_SHORT).show()
                        tvScanResult.text = "Error: Invalid QR Tag"
                    }
                }
                .addOnCanceledListener {
                    Toast.makeText(this, "QR Scan Cancelled", Toast.LENGTH_SHORT).show()
                    tvScanResult.text = "Ready to Scan"
                }
        }
    }

    private fun sendVerificationToServer(assetId: Int, serialNumber: String) {
        // Change "AppPrefs" to "InventoryPrefs" so it can actually find the saved token!
        val sharedPreferences = getSharedPreferences("InventoryPrefs", Context.MODE_PRIVATE)
        val token = sharedPreferences.getString("JWT_TOKEN", "") ?: ""

        // Package the REAL scanned data from the camera
        // (Category and Specs are handled by the backend database lookup)
        val request = AssetVerificationRequest(
            assetId = assetId,
            serialNumber = serialNumber,
            category = "Auto-Detect",
            specifications = "Auto-Detect"
        )

        ApiClient.instance.verifyAsset("Bearer $token", request).enqueue(object : Callback<AssetVerificationResponse> {
            override fun onResponse(call: Call<AssetVerificationResponse>, response: Response<AssetVerificationResponse>) {
                if (response.isSuccessful && response.body() != null) {
                    val result = response.body()!!

                    // 1. Determine the severity based on API's matchStatus
                    val isTampered = result.matchStatus != "Verified"
                    val dialogTitle = if (isTampered) "⚠️ SECURITY ALERT" else "✅ Asset Verified"

                    // 2. Build and show the un-ignorable dialog
                    android.app.AlertDialog.Builder(this@ScannerActivity)
                        .setTitle(dialogTitle)
                        .setMessage(result.message)
                        .setCancelable(false) // This forces the user to click the button; they can't just tap away
                        .setPositiveButton("Acknowledge") { dialog, _ ->
                            dialog.dismiss()
                            // Optional: Reset the text view for the next scan
                            findViewById<TextView>(R.id.tvScanResult).text = "Ready to Scan"
                        }
                        .show()

                    Log.d("VERIFY", "Status: ${result.matchStatus}")
                } else {
                    // Keep the error toast for standard network/server failures
                    Toast.makeText(this@ScannerActivity, "Verification failed: ${response.code()}", Toast.LENGTH_SHORT).show()
                }
            }

            override fun onFailure(call: Call<AssetVerificationResponse>, t: Throwable) {
                Toast.makeText(this@ScannerActivity, "Network error: ${t.message}", Toast.LENGTH_SHORT).show()
            }
        })
    }
}