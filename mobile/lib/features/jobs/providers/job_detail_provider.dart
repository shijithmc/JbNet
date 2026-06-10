import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'job_feed_provider.dart'; // also re-exports JobDto via job_feed_provider's export directive

/// Looks up a single job from the already-loaded feed cache.
///
/// Design decision: the backend exposes only GET /jobs (list). Rather than
/// adding a separate GET /jobs/{id} endpoint for v1 MVP, we re-use the
/// feed cache — zero extra network call when the feed is already loaded.
/// If the job is not in the cache (e.g. navigated to directly via deep link
/// before the feed loaded), the feed is fetched first, then the job is found.
///
/// Returns null if the job is not found in the response. FA-H01.
final jobDetailProvider =
    FutureProvider.autoDispose.family<JobDto?, String>((ref, jobId) async {
  final jobs = await ref.watch(jobFeedProvider.future);
  try {
    return jobs.firstWhere((j) => j.id == jobId);
  } catch (_) {
    // Job not found in current feed page — could add a direct API call here
    // once GET /jobs/{id} is implemented on the backend.
    return null;
  }
});
