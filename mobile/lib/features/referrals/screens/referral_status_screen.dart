import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api_client.dart';
import '../../../core/auth_state.dart';
import '../../../data/models/referral_dto.dart';
import '../providers/referral_actions_provider.dart';

// FA-016: provider returns typed [ReferralStatusDto].
// FA-H04: forward/accept/decline/withdraw delegated to ReferralActionsNotifier.
// FA-M04: actions gated on user role (requester sees Withdraw; others see Forward/Accept/Decline).

// ── Provider ──────────────────────────────────────────────────────────────────

// GET /referrals/{id} currently returns a placeholder { id } response.
// The full status shape (status, jobTitle, companyName, hops, role) will be
// populated once the backend query is implemented.
// ReferralStatusDto.fromJson uses null-coalescing defaults for all fields.
final referralStatusProvider =
    FutureProvider.family<ReferralStatusDto, String>((ref, requestId) async {
  final dio = ref.watch(apiClientProvider);
  final response =
      await dio.get<Map<String, dynamic>>('/referrals/$requestId');
  return ReferralStatusDto.fromJson(response.data!);
});

// ── Screen ────────────────────────────────────────────────────────────────────

class ReferralStatusScreen extends ConsumerStatefulWidget {
  final String requestId;
  const ReferralStatusScreen({super.key, required this.requestId});

  @override
  ConsumerState<ReferralStatusScreen> createState() =>
      _ReferralStatusScreenState();
}

class _ReferralStatusScreenState
    extends ConsumerState<ReferralStatusScreen> {
  bool _actioning = false;

  // ── Actions — FA-H04: delegated to ReferralActionsNotifier ──────────────────

  Future<void> _forward() async {
    final note = await _promptNote(
      context,
      title: 'Forward request',
      hint: 'Optional note to the next person…',
    );
    if (note == null) return; // user cancelled
    await _doAction(
      () => ref.read(referralActionsProvider.notifier).forward(
            requestId: widget.requestId,
            note: note.isEmpty ? null : note,
          ),
      successMessage: 'Request forwarded.',
    );
  }

  Future<void> _accept() async {
    final confirmed = await _confirm(
      context,
      title: 'Accept referral request?',
      body: 'You are committing to internally refer this candidate.',
    );
    if (!confirmed) return;
    await _doAction(
      () => ref
          .read(referralActionsProvider.notifier)
          .accept(requestId: widget.requestId),
      successMessage: 'Request accepted.',
    );
  }

  Future<void> _decline() async {
    final confirmed = await _confirm(
      context,
      title: 'Decline referral request?',
      body: 'The job seeker will be notified you cannot help with this request.',
    );
    if (!confirmed) return;
    await _doAction(
      () => ref
          .read(referralActionsProvider.notifier)
          .decline(requestId: widget.requestId),
      successMessage: 'Request declined.',
    );
  }

  Future<void> _withdraw() async {
    final confirmed = await _confirm(
      context,
      title: 'Withdraw request?',
      body: 'This cancels your referral request. This cannot be undone.',
    );
    if (!confirmed) return;
    await _doAction(
      () => ref
          .read(referralActionsProvider.notifier)
          .withdraw(requestId: widget.requestId),
      successMessage: 'Request withdrawn.',
      afterAction: () {
        if (mounted) context.go('/jobs');
      },
    );
  }

  Future<void> _doAction(
    Future<void> Function() fn, {
    required String successMessage,
    VoidCallback? afterAction,
  }) async {
    setState(() => _actioning = true);
    try {
      await fn();
      if (mounted) {
        ref.invalidate(referralStatusProvider(widget.requestId));
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(successMessage)));
        afterAction?.call();
      }
    } on ReferralActionException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) setState(() => _actioning = false);
    }
  }

  Future<bool> _confirm(
    BuildContext context, {
    required String title,
    required String body,
  }) async =>
      await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(title),
          content: Text(body),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('Confirm')),
          ],
        ),
      ) ??
      false;

  Future<String?> _promptNote(
    BuildContext context, {
    required String title,
    required String hint,
  }) async {
    final ctrl = TextEditingController();
    final result = await showDialog<String?>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: TextField(
          controller: ctrl,
          decoration: InputDecoration(hintText: hint),
          maxLines: 3,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, null),
              child: const Text('Cancel')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, ctrl.text.trim()),
              child: const Text('Send')),
        ],
      ),
    );
    ctrl.dispose();
    return result;
  }

  // ── Build ────────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final statusAsync =
        ref.watch(referralStatusProvider(widget.requestId));

    return Scaffold(
      appBar: AppBar(
        title: const Text('Referral Status'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: () =>
                ref.invalidate(referralStatusProvider(widget.requestId)),
          ),
        ],
      ),
      body: statusAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 48),
                const SizedBox(height: 16),
                const Text(
                  'Unable to load request status.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => ref.invalidate(
                      referralStatusProvider(widget.requestId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (dto) => _buildContent(context, dto),
      ),
    );
  }

  Widget _buildContent(BuildContext context, ReferralStatusDto dto) {
    final status = dto.status;
    final color  = _statusColor(context, status);

    // FA-M04: gate actions on the authenticated user's role.
    // requesterId = the job seeker who sent the referral.
    // If currentUserId matches requesterId → show only Withdraw.
    // Otherwise → show intermediary actions (Forward / Accept / Decline).
    final currentUserId = ref.read(authStateProvider).userId;
    final isRequester   = currentUserId != null && currentUserId == dto.requesterId;

    // Determine whether the current status still allows actions.
    final isTerminal = {'accepted', 'declined', 'withdrawn', 'expired', 'completed'}
        .contains(status.toLowerCase());

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Status badge
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: color.withValues(alpha: 0.3)),
            ),
            child: Row(
              children: [
                Icon(_statusIcon(status), color: color),
                const SizedBox(width: 12),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Status',
                        style: Theme.of(context).textTheme.bodySmall),
                    Text(
                      status,
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(
                              color: color, fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          _InfoRow(label: 'For', value: '${dto.jobTitle} · ${dto.companyName}'),
          _InfoRow(label: 'Request ID', value: widget.requestId),
          const SizedBox(height: 32),

          if (!isTerminal) ...[
            Text('Actions', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 12),

            if (isRequester) ...[
              // Job seeker: can only withdraw their own request.
              _ActionCard(
                icon: Icons.undo,
                title: 'Withdraw',
                subtitle: 'Cancel your referral request.',
                color: Colors.red,
                onTap: _actioning ? null : _withdraw,
              ),
            ] else ...[
              // Intermediary / referrer: forward, accept, or decline.
              _ActionCard(
                icon: Icons.forward,
                title: 'Forward',
                subtitle: 'Pass to the next person in the chain.',
                onTap: _actioning ? null : _forward,
              ),
              const SizedBox(height: 8),
              _ActionCard(
                icon: Icons.check_circle_outline,
                title: 'Accept',
                subtitle: 'Commit to internally referring this candidate.',
                color: Colors.green,
                onTap: _actioning ? null : _accept,
              ),
              const SizedBox(height: 8),
              _ActionCard(
                icon: Icons.cancel_outlined,
                title: 'Decline',
                subtitle: 'Decline at your hop.',
                color: Colors.orange,
                onTap: _actioning ? null : _decline,
              ),
            ],

            if (_actioning)
              const Padding(
                padding: EdgeInsets.only(top: 16),
                child: Center(child: CircularProgressIndicator()),
              ),
          ],
        ],
      ),
    );
  }

  Color _statusColor(BuildContext context, String status) =>
      switch (status.toLowerCase()) {
        'accepted' || 'completed' => Colors.green,
        'declined' || 'withdrawn' || 'expired' => Colors.red,
        'forwarded' || 'active' => Colors.blue,
        _ => Theme.of(context).colorScheme.primary,
      };

  IconData _statusIcon(String status) =>
      switch (status.toLowerCase()) {
        'accepted' || 'completed' => Icons.check_circle,
        'declined' || 'withdrawn' || 'expired' => Icons.cancel,
        'forwarded' || 'active' => Icons.forward,
        _ => Icons.hourglass_empty,
      };
}

// ── Helper widgets ────────────────────────────────────────────────────────────

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;
  const _InfoRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Text(
            '$label: ',
            style: Theme.of(context)
                .textTheme
                .bodyMedium
                ?.copyWith(color: Colors.grey),
          ),
          Expanded(
            child: Text(
              value,
              style: Theme.of(context).textTheme.bodyMedium,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      );
}

class _ActionCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final Color? color;
  final VoidCallback? onTap;

  const _ActionCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    this.color,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final c = color ?? Theme.of(context).colorScheme.primary;
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: c.withValues(alpha: 0.1),
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, color: c, size: 20),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: onTap == null ? Colors.grey : null),
                    ),
                    Text(
                      subtitle,
                      style: Theme.of(context)
                          .textTheme
                          .bodySmall
                          ?.copyWith(color: Colors.grey),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right,
                  color: onTap == null ? Colors.grey.shade400 : Colors.grey),
            ],
          ),
        ),
      ),
    );
  }
}
