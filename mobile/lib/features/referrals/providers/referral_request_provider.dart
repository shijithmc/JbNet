import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

/// Typed error for referral-request submission failures. FA-H03.
class ReferralRequestException implements Exception {
  final String message;
  const ReferralRequestException(this.message);
  @override
  String toString() => message;
}

/// Manages the submission of a new referral request through a discovered path.
///
/// State is the [requestId] returned by the API on success, or null before
/// submission. Auto-disposed when the screen leaves the tree — each discovery
/// session starts fresh.
///
/// FA-H03: Dio logic moved out of [DiscoverPathsScreen._submit].
class ReferralRequestNotifier extends AutoDisposeAsyncNotifier<String?> {
  @override
  Future<String?> build() async => null;

  /// Submits a referral request and returns the new [requestId].
  ///
  /// Throws [ReferralRequestException] on API failure. Sets [state] to
  /// [AsyncLoading] while in-flight and [AsyncValue.data] / [AsyncValue.error]
  /// on completion.
  Future<String> submit({
    required String jobId,
    required List<String> hopParticipantIds,
    String? personalNote,
  }) async {
    state = const AsyncLoading();
    final dio = ref.read(apiClientProvider);
    try {
      final response = await dio.post<Map<String, dynamic>>(
        '/referrals',
        data: {
          'jobId':             jobId,
          'hopParticipantIds': hopParticipantIds,
          if (personalNote != null && personalNote.isNotEmpty)
            'personalNote': personalNote,
        },
      );
      final requestId = response.data!['requestId'] as String;
      state = AsyncValue.data(requestId);
      return requestId;
    } on DioException catch (e) {
      final err = ReferralRequestException(
        e.response?.statusCode == 400
            ? 'Invalid request. Check your selections.'
            : 'Failed to submit. Try again.',
      );
      state = AsyncValue.error(err, StackTrace.current);
      throw err;
    } catch (e, st) {
      final err = const ReferralRequestException('Failed to submit. Try again.');
      state = AsyncValue.error(err, st);
      throw err;
    }
  }
}

final referralRequestProvider =
    AsyncNotifierProvider.autoDispose<ReferralRequestNotifier, String?>(
        ReferralRequestNotifier.new);
