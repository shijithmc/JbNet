import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

/// Thrown when the resume upload flow fails (picking → presigning → S3 PUT).
class ResumeUploadException implements Exception {
  final String message;
  const ResumeUploadException(this.message);
}

/// Lightweight client-side model for the authenticated user's profile.
class UserProfile {
  final String userId;
  final String fullName;
  final String email;
  final String headline;
  final String? employerName;
  final String? city;
  final String? profilePhotoUrl;
  final bool hasResume;
  final int connectionCount;
  final int activeReferralCount;

  const UserProfile({
    required this.userId,
    required this.fullName,
    required this.email,
    required this.headline,
    this.employerName,
    this.city,
    this.profilePhotoUrl,
    required this.hasResume,
    required this.connectionCount,
    required this.activeReferralCount,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
        userId:             json['userId']             as String,
        fullName:           json['fullName']           as String,
        email:              json['email']              as String,
        headline:           json['headline']           as String,
        employerName:       json['employerName']       as String?,
        city:               json['city']               as String?,
        profilePhotoUrl:    json['profilePhotoUrl']    as String?,
        hasResume:          json['hasResume']          as bool,
        connectionCount:    json['connectionCount']    as int,
        activeReferralCount: json['activeReferralCount'] as int,
      );

  UserProfile copyWith({
    String? fullName,
    String? headline,
    String? employerName,
    String? city,
    bool? hasResume,
  }) =>
      UserProfile(
        userId:             userId,
        email:              email,
        fullName:           fullName           ?? this.fullName,
        headline:           headline           ?? this.headline,
        employerName:       employerName       ?? this.employerName,
        city:               city               ?? this.city,
        profilePhotoUrl:    profilePhotoUrl,
        hasResume:          hasResume          ?? this.hasResume,
        connectionCount:    connectionCount,
        activeReferralCount: activeReferralCount,
      );
}

/// Fetches and caches the authenticated user's profile.
/// Auto-disposed when no longer listened to (screen leaves).
class ProfileNotifier extends AutoDisposeAsyncNotifier<UserProfile> {
  @override
  Future<UserProfile> build() async => _fetchProfile();

  Future<UserProfile> _fetchProfile() async {
    final dio      = ref.read(apiClientProvider);
    final response = await dio.get<Map<String, dynamic>>('/users/me');
    return UserProfile.fromJson(response.data!);
  }

  /// Updates the profile in-place and re-syncs from server.
  Future<void> updateProfile({
    required String fullName,
    required String headline,
    String? employerName,
    String? city,
  }) async {
    final dio = ref.read(apiClientProvider);
    await dio.put<void>('/users/me', data: {
      'fullName':     fullName,
      'headline':     headline,
      'employerName': employerName,
      'city':         city,
    });
    // Optimistically update state then re-fetch for consistency
    if (state.hasValue) {
      state = AsyncValue.data(state.value!.copyWith(
        fullName:     fullName,
        headline:     headline,
        employerName: employerName,
        city:         city,
      ));
    }
    // Refresh from server to catch server-side transformations
    ref.invalidateSelf();
  }

  /// Requests a presigned S3 upload URL for the resume PDF.
  /// Returns the upload URL; the caller is responsible for the PUT to S3.
  Future<String> requestResumeUpload({
    required String fileName,
    required int sizeBytes,
  }) async {
    final dio = ref.read(apiClientProvider);
    final response = await dio.post<Map<String, dynamic>>('/users/me/resume', data: {
      'fileName':  fileName,
      'sizeBytes': sizeBytes,
    });
    return response.data!['uploadUrl'] as String;
  }

  /// Picks a PDF from [filePath], requests a presigned S3 URL, and PUTs the
  /// file bytes directly to S3. On success, updates [hasResume] optimistically.
  ///
  /// Uses a separate no-auth [Dio] instance for the S3 PUT — presigned URLs
  /// include auth in the query string and reject the `Authorization` header.
  Future<void> uploadResume({
    required String filePath,
    required String fileName,
  }) async {
    final file = File(filePath);
    final int sizeBytes;
    try {
      sizeBytes = await file.length();
    } catch (_) {
      throw const ResumeUploadException('Could not read the selected file.');
    }

    // Step 1 — request presigned URL from our backend.
    final String uploadUrl;
    try {
      uploadUrl = await requestResumeUpload(
        fileName:  fileName,
        sizeBytes: sizeBytes,
      );
    } on DioException catch (e) {
      throw ResumeUploadException(switch (e.response?.statusCode) {
        413 => 'File is too large. Maximum size is 5 MB.',
        400 => 'Invalid file. Please upload a PDF.',
        _   => 'Upload failed. Please try again.',
      });
    }

    // Step 2 — PUT directly to S3 presigned URL.
    // Intentionally NO auth interceptor: S3 presigned URLs embed credentials
    // in the query string and reject any Authorization header.
    final s3Dio = Dio(BaseOptions(
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 60),
    ));
    try {
      await s3Dio.put<void>(
        uploadUrl,
        data:    file.openRead(),
        options: Options(
          headers: {
            'Content-Type':   'application/pdf',
            Headers.contentLengthHeader: sizeBytes,
          },
          validateStatus: (status) => status == 200,
        ),
      );
    } on DioException {
      throw const ResumeUploadException('Upload to S3 failed. Please try again.');
    }

    // Step 3 — optimistically mark resume present, then re-fetch.
    if (state.hasValue) {
      state = AsyncValue.data(state.value!.copyWith(hasResume: true));
    }
    ref.invalidateSelf();
  }
}

final profileProvider =
    AsyncNotifierProvider.autoDispose<ProfileNotifier, UserProfile>(
        ProfileNotifier.new);
