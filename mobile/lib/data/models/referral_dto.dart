// Data transfer objects for the referral domain.
//
// Moved from screen-level files to the shared data layer so providers and
// any future repository layer can reference them without importing screens.
// FA-007 + FA-016.

// ── Referral path discovery (GET /referrals/paths) ───────────────────────────

class ReferralPathHopDto {
  final String userId;
  final String fullName;
  final String headline;
  final String employerName;
  final bool isAtTargetCompany;

  const ReferralPathHopDto({
    required this.userId,
    required this.fullName,
    required this.headline,
    required this.employerName,
    required this.isAtTargetCompany,
  });

  factory ReferralPathHopDto.fromJson(Map<String, dynamic> j) =>
      ReferralPathHopDto(
        userId:             j['userId']           as String? ?? '',
        fullName:           j['fullName']         as String? ?? '',
        headline:           j['headline']         as String? ?? '',
        employerName:       j['employerName']     as String? ?? '',
        isAtTargetCompany:  j['isAtTargetCompany'] as bool?  ?? false,
      );
}

class ReferralPathDto {
  final int totalHops;
  final List<ReferralPathHopDto> hops;

  const ReferralPathDto({required this.totalHops, required this.hops});

  factory ReferralPathDto.fromJson(Map<String, dynamic> j) => ReferralPathDto(
        totalHops: j['totalHops'] as int? ?? 0,
        hops: ((j['hops'] as List<dynamic>?) ?? [])
            .map((h) => ReferralPathHopDto.fromJson(h as Map<String, dynamic>))
            .toList(),
      );
}

class DiscoverPathsResult {
  final String jobId;
  final String companyName;
  final String jobTitle;
  final List<ReferralPathDto> paths;

  const DiscoverPathsResult({
    required this.jobId,
    required this.companyName,
    required this.jobTitle,
    required this.paths,
  });

  factory DiscoverPathsResult.fromJson(Map<String, dynamic> j) =>
      DiscoverPathsResult(
        jobId:       j['jobId']       as String? ?? '',
        companyName: j['companyName'] as String? ?? '',
        jobTitle:    j['jobTitle']    as String? ?? '',
        paths: ((j['paths'] as List<dynamic>?) ?? [])
            .map((p) => ReferralPathDto.fromJson(p as Map<String, dynamic>))
            .toList(),
      );
}

// ── Referral status (GET /referrals/{id}) ────────────────────────────────────

/// Typed DTO replacing the previous untyped [Map<String, dynamic>] return from
/// [referralStatusProvider]. Runtime crashes on missing keys are eliminated
/// by null-coalescing defaults. FA-016.
class ReferralStatusDto {
  final String id;
  final String status;
  final String requesterId;
  final String referrerId;
  final String jobId;
  final String jobTitle;
  final String companyName;
  final String? resumeS3Key;
  final String? personalNote;

  const ReferralStatusDto({
    required this.id,
    required this.status,
    required this.requesterId,
    required this.referrerId,
    required this.jobId,
    required this.jobTitle,
    required this.companyName,
    this.resumeS3Key,
    this.personalNote,
  });

  factory ReferralStatusDto.fromJson(Map<String, dynamic> j) =>
      ReferralStatusDto(
        id:           j['id']           as String? ?? '',
        status:       j['status']       as String? ?? 'Pending',
        requesterId:  j['requesterId']  as String? ?? '',
        referrerId:   j['referrerId']   as String? ?? '',
        jobId:        j['jobId']        as String? ?? '',
        jobTitle:     j['jobTitle']     as String? ?? '',
        companyName:  j['companyName']  as String? ?? '',
        resumeS3Key:  j['resumeS3Key']  as String?,
        personalNote: j['personalNote'] as String?,
      );
}
