import 'package:flutter/material.dart';

class DiscoverPathsScreen extends StatelessWidget {
  final String jobId;
  const DiscoverPathsScreen({super.key, required this.jobId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Referral Paths')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Text('Finding connections for job $jobId…'),
            const SizedBox(height: 24),
            // TODO: show discovered paths and allow user to select one and request referral
            const Text('Path discovery — to be implemented'),
          ],
        ),
      ),
    );
  }
}
