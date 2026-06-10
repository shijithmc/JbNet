import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

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
}

final profileProvider =
    AsyncNotifierProvider.autoDispose<ProfileNotifier, UserProfile>(
        ProfileNotifier.new);
