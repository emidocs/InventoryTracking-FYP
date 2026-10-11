package com.example.inventoryscanner

import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.Header
import retrofit2.http.POST

// The request payload matching your C# DTO
data class AssetVerificationRequest(
    val assetId: Int,
    val serialNumber: String,
    val category: String,
    val specifications: String
)

// The response matching your C# return object
data class AssetVerificationResponse(
    val matchStatus: String,
    val message: String,
    val realTimeHash: String,
    val baselineHash: String
)

interface ApiService {
    @POST("api/Auth/login")
    fun login(@Body request: LoginRequest): Call<LoginResponse>

    @POST("api/Assets/verify")
    fun verifyAsset(
        @Header("Authorization") token: String,
        @Body request: AssetVerificationRequest
    ): Call<AssetVerificationResponse>
}