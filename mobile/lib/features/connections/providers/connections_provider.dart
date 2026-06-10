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

/// Fetches the authenticated user's accepted connections from the API.
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

  /// Re-loads the list from the server.
  Future<void> refresh() async => ref.invalidateSelf();
}

final connectionsProvider =
    AsyncNotifierProvider.autoDispose<ConnectionsNotifier, List<ConnectionSummary>>(
        ConnectionsNotifier.new);
