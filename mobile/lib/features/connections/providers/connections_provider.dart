import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

/// Client-side summary of one accepted connection.
class ConnectionSummary {
  final String connectionId;
  final String connectedUserId;
  final DateTime acceptedAt;

  const ConnectionSummary({
    required this.connectionId,
    required this.connectedUserId,
    required this.acceptedAt,
  });

  factory ConnectionSummary.fromJson(Map<String, dynamic> json) =>
      ConnectionSummary(
        connectionId:    json['connectionId']    as String,
        connectedUserId: json['connectedUserId'] as String,
        acceptedAt:      DateTime.parse(json['acceptedAt'] as String),
      );
}

/// Typed error for connection-action API failures. FA-H02.
class ConnectionException implements Exception {
  final String message;
  const ConnectionException(this.message);
  @override
  String toString() => message;
}

/// Fetches the authenticated user's accepted connections and provides
/// actions for sending and accepting connection requests.
/// FA-H02: direct Dio calls removed from the screen; all networking lives here.
class ConnectionsNotifier
    extends AutoDisposeAsyncNotifier<List<ConnectionSummary>> {
  @override
  Future<List<ConnectionSummary>> build() async => _fetchConnections();

  Future<List<ConnectionSummary>> _fetchConnections() async {
    final dio      = ref.read(apiClientProvider);
    final response = await dio.get<List<dynamic>>('/connections/me');
    return (response.data ?? [])
        .cast<Map<String, dynamic>>()
        .map(ConnectionSummary.fromJson)
        .toList();
  }

  /// Sends a connection request to [targetUserId].
  /// Throws [ConnectionException] on known API errors.
  Future<void> sendConnectionRequest({
    required String targetUserId,
    String? note,
  }) async {
    final dio = ref.read(apiClientProvider);
    try {
      await dio.post<dynamic>('/connections', data: {
        'targetUserId': targetUserId,
        if (note != null && note.isNotEmpty) 'note': note,
      });
      ref.invalidateSelf(); // refresh list
    } on DioException catch (e) {
      throw ConnectionException(switch (e.response?.statusCode) {
        409 => 'Connection request already sent.',
        404 => 'User not found.',
        _   => 'Failed to send request. Try again.',
      });
    }
  }

  /// Accepts a pending connection request from [requesterId].
  /// Throws [ConnectionException] on known API errors.
  Future<void> acceptConnectionRequest({required String requesterId}) async {
    final dio = ref.read(apiClientProvider);
    try {
      await dio.post<dynamic>('/connections/$requesterId/accept');
      ref.invalidateSelf(); // accepted connection now appears in list
    } on DioException catch (e) {
      throw ConnectionException(switch (e.response?.statusCode) {
        404 => 'Request not found.',
        _   => 'Failed to accept. Try again.',
      });
    }
  }

  /// Re-loads the list from the server.
  Future<void> refresh() async => ref.invalidateSelf();
}

final connectionsProvider =
    AsyncNotifierProvider.autoDispose<ConnectionsNotifier, List<ConnectionSummary>>(
        ConnectionsNotifier.new);
