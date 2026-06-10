import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../providers/job_feed_provider.dart';

class JobFeedScreen extends ConsumerWidget {
  const JobFeedScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feedAsync = ref.watch(jobFeedProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Jobs'),
        actions: [
          IconButton(icon: const Icon(Icons.search), onPressed: () {
            // TODO: navigate to search
          }),
        ],
      ),
      body: feedAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, size: 48),
                const SizedBox(height: 12),
                Text('Failed to load jobs: $e', textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => ref.invalidate(jobFeedProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (jobs) => jobs.isEmpty
            ? const Center(child: Text('No job postings yet. Check back soon.'))
            : RefreshIndicator(
                // FA-H06: pull-to-refresh wired via AsyncNotifierProvider.refresh().
                onRefresh: () => ref.read(jobFeedProvider.notifier).refresh(),
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: jobs.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (context, i) {
                    final job = jobs[i];
                    // FA-M02: guard empty companyName before index [0] access.
                    final initial = job.companyName.isNotEmpty
                        ? job.companyName[0].toUpperCase()
                        : '?';
                    return Card(
                      child: ListTile(
                        onTap: () => context.go('/jobs/${job.id}'),
                        title: Text(
                          job.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        subtitle: Text('${job.companyName} · ${job.location}'),
                        trailing: Text(
                          '${job.daysAgo}d ago',
                          style: Theme.of(context).textTheme.labelSmall,
                        ),
                        leading: CircleAvatar(child: Text(initial)),
                      ),
                    );
                  },
                ),
              ),
      ),
    );
  }
}
