import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';
import '../providers/connections_provider.dart';

/// Lists the authenticated user's accepted connections and provides actions to
/// send new connection requests and accept pending ones.
class ConnectionsScreen extends ConsumerStatefulWidget {
  const ConnectionsScreen({super.key});

  @override
  ConsumerState<ConnectionsScreen> createState() => _ConnectionsScreenState();
}

class _ConnectionsScreenState extends ConsumerState<ConnectionsScreen> {
  bool _busy = false;

  // ── Send connection request ───────────────────────────────────────────────

  Future<void> _showAddConnection() async {
    final targetIdCtrl = TextEditingController();
    final noteCtrl     = TextEditingController();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(
            24, 24, 24, MediaQuery.viewInsetsOf(ctx).bottom + 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Add connection',
              style: Theme.of(ctx)
                  .textTheme
                  .titleMedium
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 4),
            Text(
              'Enter the user ID of the person you want to connect with.',
              style: Theme.of(ctx)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: Colors.grey),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: targetIdCtrl,
              decoration: const InputDecoration(
                labelText: 'User ID',
                hintText:  'usr-xxxxxxxxxxxx',
                border:    OutlineInputBorder(),
              ),
              autocorrect:     false,
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: noteCtrl,
              decoration: const InputDecoration(
                labelText: 'Note (optional)',
                hintText:  'We met at…',
                border:    OutlineInputBorder(),
              ),
              maxLines:        2,
              textInputAction: TextInputAction.done,
            ),
            const SizedBox(height: 20),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Send request'),
            ),
          ],
        ),
      ),
    );

    final targetId = targetIdCtrl.text.trim();
    final note     = noteCtrl.text.trim();
    targetIdCtrl.dispose();
    noteCtrl.dispose();

    if (confirmed != true || targetId.isEmpty) return;

    setState(() => _busy = true);
    try {
      final dio = ref.read(apiClientProvider);
      await dio.post<dynamic>('/connections', data: {
        'targetUserId': targetId,
        'note':         note.isEmpty ? null : note,
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Connection request sent.')),
        );
        // Refresh list — the new connection is still Pending so won't appear yet,
        // but it triggers a reload for consistency.
        ref.read(connectionsProvider.notifier).refresh();
      }
    } on DioException catch (e) {
      if (mounted) {
        final msg = e.response?.statusCode == 409
            ? 'Connection request already sent.'
            : e.response?.statusCode == 404
                ? 'User not found.'
                : 'Failed to send request. Try again.';
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(msg)));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  // ── Accept connection request ─────────────────────────────────────────────

  Future<void> _showAcceptConnection() async {
    final requesterIdCtrl = TextEditingController();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(
            24, 24, 24, MediaQuery.viewInsetsOf(ctx).bottom + 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Accept pending request',
              style: Theme.of(ctx)
                  .textTheme
                  .titleMedium
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 4),
            Text(
              'Enter the user ID of the person who sent you a request.',
              style: Theme.of(ctx)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: Colors.grey),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: requesterIdCtrl,
              decoration: const InputDecoration(
                labelText: 'Requester user ID',
                hintText:  'usr-xxxxxxxxxxxx',
                border:    OutlineInputBorder(),
              ),
              autocorrect:     false,
              textInputAction: TextInputAction.done,
            ),
            const SizedBox(height: 20),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Accept'),
            ),
          ],
        ),
      ),
    );

    final requesterId = requesterIdCtrl.text.trim();
    requesterIdCtrl.dispose();

    if (confirmed != true || requesterId.isEmpty) return;

    setState(() => _busy = true);
    try {
      final dio = ref.read(apiClientProvider);
      await dio.post<dynamic>('/connections/$requesterId/accept');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Connection accepted.')),
        );
        // New accepted connection — refresh list so it appears immediately.
        ref.read(connectionsProvider.notifier).refresh();
      }
    } on DioException catch (e) {
      if (mounted) {
        final msg = e.response?.statusCode == 404
            ? 'Request not found.'
            : 'Failed to accept. Try again.';
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(msg)));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  // ── Build ─────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final connectionsAsync = ref.watch(connectionsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Network'),
        actions: [
          if (_busy)
            const Padding(
              padding: EdgeInsets.only(right: 16),
              child: Center(
                child: SizedBox.square(
                  dimension: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            ),
          IconButton(
            tooltip: 'Accept pending request',
            icon: const Icon(Icons.check_circle_outline),
            onPressed: _busy ? null : _showAcceptConnection,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _busy ? null : _showAddConnection,
        icon: const Icon(Icons.person_add),
        label: const Text('Add connection'),
      ),
      body: connectionsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => _ErrorBody(
          message: err.toString(),
          onRetry: () => ref.invalidate(connectionsProvider),
        ),
        data: (connections) => connections.isEmpty
            ? _EmptyState(onAddTap: _showAddConnection)
            : _ConnectionsList(
                connections: connections,
                onRefresh: () async => ref.invalidate(connectionsProvider),
              ),
      ),
    );
  }
}

// ── Connection list ───────────────────────────────────────────────────────────

class _ConnectionsList extends StatelessWidget {
  const _ConnectionsList({required this.connections, required this.onRefresh});

  final List<ConnectionSummary> connections;
  final RefreshCallback onRefresh;

  @override
  Widget build(BuildContext context) => RefreshIndicator(
        onRefresh: onRefresh,
        child: ListView.separated(
          padding: const EdgeInsets.fromLTRB(0, 8, 0, 88), // 88 = FAB clearance
          itemCount: connections.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) {
            final c = connections[index];
            return _ConnectionTile(connection: c);
          },
        ),
      );
}

class _ConnectionTile extends StatelessWidget {
  const _ConnectionTile({required this.connection});
  final ConnectionSummary connection;

  @override
  Widget build(BuildContext context) {
    final initials = connection.connectedUserId.length >= 2
        ? connection.connectedUserId.substring(0, 2).toUpperCase()
        : '?';

    return ListTile(
      leading: CircleAvatar(child: Text(initials)),
      title:   Text(connection.connectedUserId),
      subtitle: Text(
        'Connected ${_relativeDate(connection.acceptedAt)}',
        style: const TextStyle(fontSize: 12),
      ),
    );
  }

  String _relativeDate(DateTime dt) {
    final diff = DateTime.now().difference(dt);
    if (diff.inDays > 365) return '${(diff.inDays / 365).floor()} year(s) ago';
    if (diff.inDays > 30)  return '${(diff.inDays / 30).floor()} month(s) ago';
    if (diff.inDays > 0)   return '${diff.inDays} day(s) ago';
    if (diff.inHours > 0)  return '${diff.inHours} hour(s) ago';
    return 'just now';
  }
}

// ── Empty state ───────────────────────────────────────────────────────────────

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.onAddTap});
  final VoidCallback onAddTap;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(32, 0, 32, 80),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.people_outline,
                size: 80,
                color: Theme.of(context)
                    .colorScheme
                    .primary
                    .withValues(alpha: 0.35),
              ),
              const SizedBox(height: 24),
              Text(
                'Build your network',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 8),
              Text(
                'Connect with colleagues and friends to unlock referral paths to their companies.',
                textAlign: TextAlign.center,
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Colors.grey),
              ),
              const SizedBox(height: 32),
              FilledButton.icon(
                onPressed: onAddTap,
                icon:  const Icon(Icons.person_add),
                label: const Text('Add your first connection'),
              ),
            ],
          ),
        ),
      );
}

// ── Error state ───────────────────────────────────────────────────────────────

class _ErrorBody extends StatelessWidget {
  const _ErrorBody({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48, color: Colors.red),
              const SizedBox(height: 12),
              Text('Could not load connections',
                  style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(message,
                  style: Theme.of(context).textTheme.bodySmall,
                  textAlign: TextAlign.center),
              const SizedBox(height: 16),
              ElevatedButton.icon(
                icon:  const Icon(Icons.refresh),
                label: const Text('Retry'),
                onPressed: onRetry,
              ),
            ],
          ),
        ),
      );
}
