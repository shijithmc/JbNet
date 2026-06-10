import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api_client.dart';

class JobDto {
  final String id;
  final String companyName;
  final String title;
  final String location;
  final int daysAgo;

  const JobDto({
    required this.id,
    required this.companyName,
    required this.title,
    required this.location,
    required this.daysAgo,
  });

  factory JobDto.fromJson(Map<String, dynamic> json) => JobDto(
    id:          json['id'] as String,
    companyName: json['companyName'] as String,
    title:       json['title'] as String,
    location:    json['location'] as String,
    daysAgo:     json['daysAgo'] as int,
  );
}

final jobFeedProvider = FutureProvider<List<JobDto>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<Map<String, dynamic>>('/jobs');
  final items = (response.data!['items'] as List<dynamic>);
  return items.map((e) => JobDto.fromJson(e as Map<String, dynamic>)).toList();
});
