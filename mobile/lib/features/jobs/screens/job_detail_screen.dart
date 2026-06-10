import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class JobDetailScreen extends StatelessWidget {
  final String jobId;
  const JobDetailScreen({super.key, required this.jobId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Job Details')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Job ID: $jobId'),
            const Spacer(),
            FilledButton.icon(
              onPressed: () => context.go('/jobs/$jobId/paths'),
              icon: const Icon(Icons.people),
              label: const Text('Find a referral path'),
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}
