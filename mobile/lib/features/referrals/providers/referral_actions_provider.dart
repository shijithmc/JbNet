import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

/// The result of a completed referral action. Used by the screen to determine
/// follow-up behaviour (e.g. navigate away after withdraw).
enum ReferralActionResult { forwarded, accepted, declined, withdrawn }

/// Typed error for referral-action API failures. FA-H04.
class ReferralActionException implements Exception {
  final String message;
  const ReferralActionException(this.message);
  @override
  String toString() => message;
}

/// Provides forward / accept / decline / withdraw actions for referral requests.
///
/// Each method receives the [requestId] from the screen and delegates to the
/// API — screens no longer hold Dio instances directly.  FA-H04.
///
/// State is the most recent [ReferralActionResult] on success; null at rest;
/// [AsyncLoading] while an action is in-flight. Only one action runs at a time
/// (the screen disables buttons while `_actioning` is true).
class ReferralActionsNotifier
    extends AutoDisposeAsyncNotifier<ReferralActionResult?> {
  @override
  Future<ReferralActionResult?> build() async => null;

  Future<void> forward({
    required String requestId,
    String? note,
  }) async {
    await _run(
      () => ref.read(apiClientProvider).post<dynamic>(
            '/referrals/$requestId/forward',
            data: {if (note != null && note.isNotEmpty) 'note': note},
          ),
      result: ReferralActionResult.forwarded,
    );
  }

  Future<void> accept({required String requestId}) async {
    await _run(
      () => ref
          .read(apiClientProvider)
          .post<dynamic>('/referrals/$requestId/accept'),
      result: ReferralActionResult.accepted,
    );
  }

  Future<void> decline({required String requestId}) async {
    await _run(
      () => ref.read(apiClientProvider).post<dynamic>(
            '/referrals/$requestId/decline',
            data: const {'reason': null},
          ),
      result: ReferralActionResult.declined,
    );
  }

  Future<void> withdraw({required String requestId}) async {
    await _run(
      () => ref
          .read(apiClientProvider)
          .delete<dynamic>('/referrals/$requestId'),
      result: ReferralActionResult.withdrawn,
    );
  }

  Future<void> _run(
    Future<void> Function() fn, {
    required ReferralActionResult result,
  }) async {
    state = const AsyncLoading();
    try {
      await fn();
      state = AsyncValue.data(result);
    } on DioException catch (e) {
      final err = ReferralActionException(
        e.response?.statusCode == 403
            ? 'Not authorised for this action.'
            : 'Action failed. Please try again.',
      );
      state = AsyncValue.error(err, StackTrace.current);
      throw err;
    } catch (e, st) {
      const err = ReferralActionException('Action failed. Please try again.');
      state = AsyncValue.error(err, st);
      throw err;
    }
  }
}

final referralActionsProvider = AsyncNotifierProvider.autoDispose<
    ReferralActionsNotifier, ReferralActionResult?>(ReferralActionsNotifier.new);
