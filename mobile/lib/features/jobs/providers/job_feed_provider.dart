import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';
import '../../../data/models/job_dto.dart';

// FA-007: JobDto moved to lib/data/models/job_dto.dart — no longer defined here.
// FA-017: force-unwrap and untyped cast replaced with null-safe coercion.

export '../../../data/models/job_dto.dart';

final jobFeedProvider = FutureProvider<List<JobDto>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<Map<String, dynamic>>('/jobs');

  final rawItems = response.data?['items'];
  if (rawItems == null) return <JobDto>[];

  final items = rawItems as List<dynamic>? ?? <dynamic>[];
  return items
      .whereType<Map<String, dynamic>>()
      .map(JobDto.fromJson)
      .toList();
});
