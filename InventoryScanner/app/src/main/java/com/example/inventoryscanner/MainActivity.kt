package com.example.inventoryscanner

import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import android.content.Context
import android.content.Intent

class MainActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val sharedPref = getSharedPreferences("InventoryPrefs", Context.MODE_PRIVATE)
        val savedToken = sharedPref.getString("JWT_TOKEN", null)

        if (savedToken != null) {
            // Token exists! Boot them straight to the scanner.
            val intent = Intent(this, ScannerActivity::class.java)
            startActivity(intent)
            finish() // This kills the MainActivity so they can't hit the back button
            return // Stops the rest of the login screen UI from loading
        }
        setContentView(R.layout.activity_main)

        val etUsername = findViewById<EditText>(R.id.etUsername)
        val etPassword = findViewById<EditText>(R.id.etPassword)
        val btnLogin = findViewById<Button>(R.id.btnLogin)

        btnLogin.setOnClickListener {
            val username = etUsername.text.toString().trim()
            val password = etPassword.text.toString().trim()

            if (username.isEmpty() || password.isEmpty()) {
                Toast.makeText(this, "Please fill in all fields", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            performLogin(username, password)
        }
    }

    private fun performLogin(username: String, password: String) {
        val request = LoginRequest(username, password)

        ApiClient.instance.login(request).enqueue(object : Callback<LoginResponse> {
            override fun onResponse(call: Call<LoginResponse>, response: Response<LoginResponse>) {
                if (response.isSuccessful && response.body() != null) {
                    val token = response.body()!!.token
                    val role = response.body()!!.role

                    // 1. Save the token to physical storage
                    val sharedPref = getSharedPreferences("InventoryPrefs", Context.MODE_PRIVATE)
                    sharedPref.edit()
                        .putString("JWT_TOKEN", token)
                        .putString("USERNAME", request.username) // Adjust this variable to match what they typed!
                        .apply()

                    Toast.makeText(this@MainActivity, "Welcome $role!", Toast.LENGTH_SHORT).show()

                    // 2. Navigate to the Scanner Activity
                    val intent = android.content.Intent(this@MainActivity, ScannerActivity::class.java)
                    startActivity(intent)
                    finish()
                } else {
                    // THIS IS THE NEW DEBUGGING LOGIC
                    val errorCode = response.code()
                    val errorString = response.errorBody()?.string() ?: "Unknown Server Error"

                    Log.e("API_DEBUG", "Failed with code: $errorCode")
                    Log.e("API_DEBUG", "Server message: $errorString")

                    Toast.makeText(this@MainActivity, "Error $errorCode: Check Logcat", Toast.LENGTH_LONG).show()
                }
            }

            override fun onFailure(call: Call<LoginResponse>, t: Throwable) {
                Toast.makeText(this@MainActivity, "Network error: ${t.message}", Toast.LENGTH_SHORT).show()
                Log.e("AUTH", "Error", t)
            }
        })
    }
}