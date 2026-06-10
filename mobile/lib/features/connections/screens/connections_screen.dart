import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

class ConnectionsScreen extends ConsumerStatefulWidget {
  const ConnectionsScreen({super.key});

  @override
  ConsumerState<ConnectionsScreen> createState() => _ConnectionsScreenState();
}

class _ConnectionsScreenState extends ConsumerState<ConnectionsScreen> {
  bool _busy = false;

  // ── Add connection ────────────────────────────────────────────────────────

  Future<void> _showAddConnection() async {
    final targetIdCtrl = TextEditingController();
    final noteCtrl = TextEditingController();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
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
                hintText: 'usr-xxxxxxxxxxxx',
                border: OutlineInputBorder(),
              ),
              autocorrect: false,
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: noteCtrl,
              decoration: const InputDecoration(
                labelText: 'Note (optional)',
                hintText: 'We met at…',
                border: OutlineInputBorder(),
              ),
              maxLines: 2,
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
    final note = noteCtrl.text.trim();
    targetIdCtrl.dispose();
    noteCtrl.dispose();

    if (confirmed != true || targetId.isEmpty) return;

    setState(() => _busy = true);
    try {
      final dio = ref.read(apiClientProvider);
      await dio.post<dynamic>('/connections', data: {
        'targetUserId': targetId,
        'note': note.isEmpty ? null : note,
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Connection request sent.')),
        );
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

  // ── Accept connection ─────────────────────────────────────────────────────

  Future<void> _showAcceptConnection() async {
    final requesterIdCtrl = TextEditingController();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
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
                hintText: 'usr-xxxxxxxxxxxx',
                border: OutlineInputBorder(),
              ),
              autocorrect: false,
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
  Widget build(BuildContext context) => Scaffold(
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
          ],
        ),
        floatingActionButton: FloatingActionButton.extended(
          onPressed: _busy ? null : _showAddConnection,
          icon: const Icon(Icons.person_add),
          label: const Text('Add connection'),
        ),
        body: Center(
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
                OutlinedButton.icon(
                  onPressed: _busy ? null : _showAcceptConnection,
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Accept a pending request'),
                ),
              ],
            ),
          ),
        ),
      );
}
