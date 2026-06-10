import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../data/models/job_dto.dart';
import '../providers/job_detail_provider.dart';

/// Displays details for a specific job posting.
/// FA-H01: loads real data from [jobDetailProvider] instead of showing the raw jobId.
class JobDetailScreen extends ConsumerWidget {
  final String jobId;
  const JobDetailScreen({super.key, required this.jobId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detailAsync = ref.watch(jobDetailProvider(jobId));

    return Scaffold(
      appBar: AppBar(title: const Text('Job Details')),
      body: detailAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 48),
                const SizedBox(height: 12),
                const Text('Failed to load job details.',
                    textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => ref.invalidate(jobDetailProvider(jobId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (job) {
          if (job == null) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.work_off_outlined, size: 48),
                    const SizedBox(height: 12),
                    const Text('Job not found.', textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => context.go('/jobs'),
                      child: const Text('Back to jobs'),
                    ),
                  ],
                ),
              ),
            );
          }
          return _JobDetailBody(job: job, jobId: jobId);
        },
      ),
    );
  }
}

class _JobDetailBody extends StatelessWidget {
  const _JobDetailBody({required this.job, required this.jobId});

  final JobDto job;
  final String jobId;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final initial = job.companyName.isNotEmpty
        ? job.companyName[0].toUpperCase()
        : '?';

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Company header
          Row(
            children: [
              CircleAvatar(
                radius: 28,
                child: Text(
                  initial,
                  style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      job.title,
                      style: theme.textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      job.companyName,
                      style: theme.textTheme.bodyLarge
                          ?.copyWith(color: theme.colorScheme.primary),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // Meta chips
          Wrap(
            spacing: 8,
            children: [
              Chip(
                avatar: const Icon(Icons.location_on, size: 16),
                label: Text(job.location),
              ),
              Chip(
                avatar: const Icon(Icons.access_time, size: 16),
                label: Text('${job.daysAgo} day${job.daysAgo == 1 ? '' : 's'} ago'),
              ),
            ],
          ),

          const Divider(height: 32),
          const Spacer(),

          FilledButton.icon(
            onPressed: () => context.go('/jobs/$jobId/paths'),
            icon: const Icon(Icons.people),
            label: const Text('Find a referral path'),
          ),
          const SizedBox(height: 16),
        ],
      ),
    );
  }
}
