package com.example.inventoryscanner

import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.POST

interface ApiService {
    @POST("api/Auth/login")
    fun login(@Body request: LoginRequest): Call<LoginResponse>
}