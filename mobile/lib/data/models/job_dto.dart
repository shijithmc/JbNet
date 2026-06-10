/// Data transfer object for a job listing returned by GET /jobs.
///
/// Moved from [job_feed_provider.dart] to the shared data layer so any
/// feature can import it without coupling to the provider file. FA-007.
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
        id:          json['id']          as String? ?? '',
        companyName: json['companyName'] as String? ?? '',
        title:       json['title']       as String? ?? '',
        location:    json['location']    as String? ?? '',
        daysAgo:     json['daysAgo']     as int?    ?? 0,
      );
}
