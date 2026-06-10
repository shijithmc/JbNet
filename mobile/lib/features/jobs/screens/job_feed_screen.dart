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
        error: (e, _) => Center(child: Text('Failed to load jobs: $e')),
        data: (jobs) => jobs.isEmpty
            ? const Center(child: Text('No job postings yet. Check back soon.'))
            : ListView.separated(
                padding: const EdgeInsets.all(16),
                itemCount: jobs.length,
                separatorBuilder: (ctx, _) => const SizedBox(height: 8),
                itemBuilder: (context, i) {
                  final job = jobs[i];
                  return Card(
                    child: ListTile(
                      onTap: () => context.go('/jobs/${job.id}'),
                      title: Text(job.title, maxLines: 1, overflow: TextOverflow.ellipsis),
                      subtitle: Text('${job.companyName} · ${job.location}'),
                      trailing: Text(
                        '${job.daysAgo}d ago',
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                      leading: CircleAvatar(child: Text(job.companyName[0].toUpperCase())),
                    ),
                  );
                },
              ),
      ),
    );
  }
}
