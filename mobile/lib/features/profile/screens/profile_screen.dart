import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/auth_state.dart';
import '../providers/profile_provider.dart';

/// Displays the authenticated user's profile with options to edit and sign out.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(profileProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          IconButton(
            tooltip: 'Sign out',
            icon: const Icon(Icons.logout),
            onPressed: () => _confirmSignOut(context, ref),
          ),
        ],
      ),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:   (err, _) => _ErrorBody(message: err.toString(), onRetry: () => ref.invalidate(profileProvider)),
        data:    (profile) => _ProfileBody(profile: profile),
      ),
    );
  }

  Future<void> _confirmSignOut(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Sign out?'),
        content: const Text('You will need to sign in again to use the app.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: TextButton.styleFrom(foregroundColor: Colors.red),
            child: const Text('Sign out'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await ref.read(authStateProvider.notifier).signOut();
    }
  }
}

// ── Profile body ─────────────────────────────────────────────────────────────

class _ProfileBody extends ConsumerWidget {
  const _ProfileBody({required this.profile});

  final UserProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(profileProvider),
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // ── Avatar + name ──────────────────────────────────────────────
            Center(
              child: Column(
                children: [
                  _Avatar(photoUrl: profile.profilePhotoUrl, fullName: profile.fullName),
                  const SizedBox(height: 12),
                  Text(
                    profile.fullName,
                    style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    profile.headline,
                    style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.primary),
                    textAlign: TextAlign.center,
                  ),
                  if (profile.employerName != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      profile.employerName!,
                      style: theme.textTheme.bodyMedium?.copyWith(color: Colors.grey[600]),
                    ),
                  ],
                  if (profile.city != null) ...[
                    const SizedBox(height: 2),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.location_on, size: 14, color: Colors.grey[500]),
                        const SizedBox(width: 2),
                        Text(
                          profile.city!,
                          style: theme.textTheme.bodySmall?.copyWith(color: Colors.grey[500]),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
            ),

            const SizedBox(height: 24),
            const Divider(),
            const SizedBox(height: 8),

            // ── Stats row ──────────────────────────────────────────────────
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                _StatChip(label: 'Connections', value: profile.connectionCount),
                _StatChip(label: 'Referrals', value: profile.activeReferralCount),
              ],
            ),

            const SizedBox(height: 24),

            // ── Actions ────────────────────────────────────────────────────
            _SectionHeader(title: 'Actions'),
            const SizedBox(height: 8),

            _ActionTile(
              icon: Icons.edit,
              label: 'Edit profile',
              onTap: () => _openEditSheet(context, ref),
            ),
            _ActionTile(
              icon: profile.hasResume ? Icons.description : Icons.upload_file,
              label: profile.hasResume ? 'Update resume' : 'Upload resume',
              onTap: () => _openResumeUpload(context, ref),
            ),

            const SizedBox(height: 24),

            // ── Account info ───────────────────────────────────────────────
            _SectionHeader(title: 'Account'),
            const SizedBox(height: 8),
            _InfoRow(label: 'Email', value: profile.email),
          ],
        ),
      ),
    );
  }

  void _openEditSheet(BuildContext context, WidgetRef ref) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => _EditProfileSheet(profile: profile),
    );
  }

  void _openResumeUpload(BuildContext context, WidgetRef ref) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Resume upload — pick a PDF from your device.')),
    );
    // TODO(resume): integrate file_picker + S3 presigned upload (FA-007 follow-up)
  }
}

// ── Edit profile bottom sheet ─────────────────────────────────────────────────

class _EditProfileSheet extends ConsumerStatefulWidget {
  const _EditProfileSheet({required this.profile});
  final UserProfile profile;

  @override
  ConsumerState<_EditProfileSheet> createState() => _EditProfileSheetState();
}

class _EditProfileSheetState extends ConsumerState<_EditProfileSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameCtrl;
  late final TextEditingController _headlineCtrl;
  late final TextEditingController _employerCtrl;
  late final TextEditingController _cityCtrl;

  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _nameCtrl     = TextEditingController(text: widget.profile.fullName);
    _headlineCtrl = TextEditingController(text: widget.profile.headline);
    _employerCtrl = TextEditingController(text: widget.profile.employerName ?? '');
    _cityCtrl     = TextEditingController(text: widget.profile.city ?? '');
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _headlineCtrl.dispose();
    _employerCtrl.dispose();
    _cityCtrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() => _saving = true);

    try {
      await ref.read(profileProvider.notifier).updateProfile(
        fullName:     _nameCtrl.text.trim(),
        headline:     _headlineCtrl.text.trim(),
        employerName: _employerCtrl.text.trim().isEmpty ? null : _employerCtrl.text.trim(),
        city:         _cityCtrl.text.trim().isEmpty     ? null : _cityCtrl.text.trim(),
      );

      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to save: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + bottom),
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Edit profile',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 16),

            // Full name
            TextFormField(
              controller: _nameCtrl,
              decoration: const InputDecoration(labelText: 'Full name *'),
              textCapitalization: TextCapitalization.words,
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Full name is required.';
                if (v.trim().length < 2)          return 'Name must be at least 2 characters.';
                return null;
              },
            ),
            const SizedBox(height: 12),

            // Headline
            TextFormField(
              controller: _headlineCtrl,
              decoration: const InputDecoration(labelText: 'Headline *'),
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Headline is required.';
                return null;
              },
            ),
            const SizedBox(height: 12),

            // Employer
            TextFormField(
              controller: _employerCtrl,
              decoration: const InputDecoration(labelText: 'Employer (optional)'),
              textCapitalization: TextCapitalization.words,
            ),
            const SizedBox(height: 12),

            // City
            TextFormField(
              controller: _cityCtrl,
              decoration: const InputDecoration(labelText: 'City (optional)'),
              textCapitalization: TextCapitalization.words,
            ),
            const SizedBox(height: 24),

            ElevatedButton(
              onPressed: _saving ? null : _save,
              child: _saving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Save changes'),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Small reusable widgets ────────────────────────────────────────────────────

class _Avatar extends StatelessWidget {
  const _Avatar({required this.photoUrl, required this.fullName});
  final String? photoUrl;
  final String  fullName;

  @override
  Widget build(BuildContext context) {
    final initials = fullName.trim().isEmpty
        ? '?'
        : fullName.trim().split(' ').take(2).map((w) => w[0].toUpperCase()).join();

    return CircleAvatar(
      radius: 44,
      backgroundImage: photoUrl != null ? NetworkImage(photoUrl!) : null,
      child: photoUrl == null
          ? Text(initials, style: const TextStyle(fontSize: 28, fontWeight: FontWeight.bold))
          : null,
    );
  }
}

class _StatChip extends StatelessWidget {
  const _StatChip({required this.label, required this.value});
  final String label;
  final int    value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        Text(
          value.toString(),
          style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold),
        ),
        Text(label, style: theme.textTheme.bodySmall),
      ],
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});
  final String title;

  @override
  Widget build(BuildContext context) => Text(
        title,
        style: Theme.of(context)
            .textTheme
            .titleMedium
            ?.copyWith(fontWeight: FontWeight.bold),
      );
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.icon, required this.label, required this.onTap});
  final IconData icon;
  final String   label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => ListTile(
        leading: Icon(icon),
        title:   Text(label),
        trailing: const Icon(Icons.chevron_right),
        onTap:   onTap,
        contentPadding: EdgeInsets.zero,
      );
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(
            width: 80,
            child: Text(label, style: theme.textTheme.bodySmall?.copyWith(color: Colors.grey[600])),
          ),
          Expanded(child: Text(value, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}

class _ErrorBody extends StatelessWidget {
  const _ErrorBody({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48, color: Colors.red),
              const SizedBox(height: 12),
              Text('Could not load profile', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(message, style: Theme.of(context).textTheme.bodySmall, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              ElevatedButton.icon(
                icon: const Icon(Icons.refresh),
                label: const Text('Retry'),
                onPressed: onRetry,
              ),
            ],
          ),
        ),
      );
}
