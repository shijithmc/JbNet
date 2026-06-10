import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api_client.dart';
import '../../../data/models/referral_dto.dart';
import '../providers/referral_request_provider.dart';

// FA-007: DTOs moved to lib/data/models/referral_dto.dart.
// FA-H03: referral submission delegated to ReferralRequestNotifier.

// ── Provider ──────────────────────────────────────────────────────────────────

final discoverPathsProvider =
    FutureProvider.family<DiscoverPathsResult, String>((ref, jobId) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<Map<String, dynamic>>(
    '/referrals/paths',
    queryParameters: {'jobId': jobId},
  );
  return DiscoverPathsResult.fromJson(response.data!);
});

// ── Screen ────────────────────────────────────────────────────────────────────

class DiscoverPathsScreen extends ConsumerStatefulWidget {
  final String jobId;
  const DiscoverPathsScreen({super.key, required this.jobId});

  @override
  ConsumerState<DiscoverPathsScreen> createState() =>
      _DiscoverPathsScreenState();
}

class _DiscoverPathsScreenState extends ConsumerState<DiscoverPathsScreen> {
  int? _selectedIndex;
  final _noteCtrl = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit(DiscoverPathsResult result) async {
    if (_selectedIndex == null) {
      setState(() => _error = 'Select a referral path first.');
      return;
    }
    final path = result.paths[_selectedIndex!];
    setState(() { _submitting = true; _error = null; });
    try {
      // FA-H03: submit via provider — no Dio in the screen.
      final requestId = await ref
          .read(referralRequestProvider.notifier)
          .submit(
            jobId:             widget.jobId,
            hopParticipantIds: path.hops.map((h) => h.userId).toList(),
            personalNote:      _noteCtrl.text.trim().isEmpty
                ? null
                : _noteCtrl.text.trim(),
          );
      if (mounted) context.go('/referrals/$requestId');
    } on ReferralRequestException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Failed to submit. Try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pathsAsync = ref.watch(discoverPathsProvider(widget.jobId));

    return Scaffold(
      appBar: AppBar(title: const Text('Referral Paths')),
      body: pathsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 48),
                const SizedBox(height: 16),
                Text('Failed to discover paths: $e',
                    textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () =>
                      ref.invalidate(discoverPathsProvider(widget.jobId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (result) => _buildContent(context, result),
      ),
    );
  }

  Widget _buildContent(BuildContext context, DiscoverPathsResult result) {
    if (result.paths.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.people_outline, size: 56),
              const SizedBox(height: 16),
              Text('No referral paths found',
                  style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(
                'Expand your network to find connections at ${result.companyName}.',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            ],
          ),
        ),
      );
    }

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                result.jobTitle,
                style: Theme.of(context)
                    .textTheme
                    .titleMedium
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              Text(
                result.companyName,
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Colors.grey),
              ),
              const SizedBox(height: 4),
              Text(
                '${result.paths.length} path${result.paths.length == 1 ? '' : 's'} found',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: result.paths.length + 1, // +1 for note + submit
            separatorBuilder: (_, _) => const SizedBox(height: 12),
            itemBuilder: (context, index) {
              if (index == result.paths.length) {
                return _buildNoteAndSubmit(context, result);
              }
              return _buildPathCard(context, result.paths[index], index);
            },
          ),
        ),
      ],
    );
  }

  Widget _buildPathCard(
      BuildContext context, ReferralPathDto path, int index) {
    final isSelected = _selectedIndex == index;
    final primary    = Theme.of(context).colorScheme.primary;

    return GestureDetector(
      onTap: () => setState(() => _selectedIndex = index),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        decoration: BoxDecoration(
          border: Border.all(
            color: isSelected ? primary : Colors.grey.shade300,
            width: isSelected ? 2 : 1,
          ),
          borderRadius: BorderRadius.circular(12),
          color: isSelected ? primary.withValues(alpha: 0.06) : null,
        ),
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  isSelected
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: isSelected ? primary : Colors.grey,
                  size: 20,
                ),
                const SizedBox(width: 8),
                Text(
                  '${path.totalHops}-hop path',
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                        color: isSelected ? primary : null,
                      ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: path.hops.asMap().entries.expand((entry) {
                  final i   = entry.key;
                  final hop = entry.value;
                  return [
                    if (i > 0)
                      const Padding(
                        padding: EdgeInsets.symmetric(horizontal: 8),
                        child: Icon(
                            Icons.arrow_forward, size: 16, color: Colors.grey),
                      ),
                    _HopChip(hop: hop),
                  ];
                }).toList(),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildNoteAndSubmit(
      BuildContext context, DiscoverPathsResult result) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: 8),
        TextField(
          controller: _noteCtrl,
          decoration: const InputDecoration(
            labelText: 'Personal note (optional)',
            hintText: 'A brief message to your connection…',
            border: OutlineInputBorder(),
          ),
          maxLines: 3,
          maxLength: 500,
        ),
        if (_error != null) ...[
          const SizedBox(height: 8),
          Text(_error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ],
        const SizedBox(height: 16),
        FilledButton(
          onPressed: (_submitting || _selectedIndex == null)
              ? null
              : () => _submit(result),
          child: _submitting
              ? const SizedBox.square(
                  dimension: 20,
                  child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Request referral'),
        ),
        const SizedBox(height: 32),
      ],
    );
  }
}

// ── Hop chip ──────────────────────────────────────────────────────────────────

class _HopChip extends StatelessWidget {
  final ReferralPathHopDto hop;
  const _HopChip({required this.hop});

  @override
  Widget build(BuildContext context) {
    final primary   = Theme.of(context).colorScheme.primary;
    final secondary = Theme.of(context).colorScheme.secondary;
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          CircleAvatar(
            radius: 22,
            backgroundColor: hop.isAtTargetCompany ? primary : secondary,
            child: Text(
              hop.fullName.isNotEmpty ? hop.fullName[0].toUpperCase() : '?',
              style: const TextStyle(color: Colors.white, fontSize: 16),
            ),
          ),
          const SizedBox(height: 4),
          Text(
            hop.fullName.split(' ').first,
            style: const TextStyle(fontSize: 11),
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
          ),
          Text(
            hop.employerName,
            style: TextStyle(fontSize: 10, color: Colors.grey.shade600),
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
          ),
          if (hop.isAtTargetCompany)
            Icon(Icons.verified, size: 14, color: primary),
        ],
      ),
    );
  }
}
