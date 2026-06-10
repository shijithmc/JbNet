import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';
import '../../../data/models/job_dto.dart';

// FA-007: JobDto moved to lib/data/models/job_dto.dart.
// FA-M09: converted from FutureProvider to AsyncNotifierProvider so
// the screen can call refresh() for pull-to-refresh. FA-H06 fix.
export '../../../data/models/job_dto.dart';

/// Loads and caches the job feed from GET /jobs.
///
/// [refresh] invalidates the cached result so [RefreshIndicator] can trigger
/// a reload. Uses [autoDispose] so the cache is freed when no screen is
/// watching.
class JobFeedNotifier extends AutoDisposeAsyncNotifier<List<JobDto>> {
  @override
  Future<List<JobDto>> build() async {
    final dio = ref.watch(apiClientProvider);
    final response = await dio.get<Map<String, dynamic>>('/jobs');

    final rawItems = response.data?['items'];
    if (rawItems == null) return <JobDto>[];

    final items = rawItems as List<dynamic>? ?? <dynamic>[];
    return items.whereType<Map<String, dynamic>>().map(JobDto.fromJson).toList();
  }

  /// Triggers a fresh load from the server (called by pull-to-refresh).
  Future<void> refresh() async => ref.invalidateSelf();
}

final jobFeedProvider =
    AsyncNotifierProvider.autoDispose<JobFeedNotifier, List<JobDto>>(
        JobFeedNotifier.new);
