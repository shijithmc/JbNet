import 'package:amazon_cognito_identity_dart_2/cognito.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/auth_state.dart';
import '../../../core/cognito_service.dart';

enum _Step { form, confirm }

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey   = GlobalKey<FormState>();
  final _nameCtrl  = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _passCtrl  = TextEditingController();
  final _codeCtrl  = TextEditingController();
  final _cognito   = CognitoService();

  _Step   _step    = _Step.form;
  bool    _loading = false;
  String? _error;
  String  _registeredEmail = '';

  @override
  void dispose() {
    _nameCtrl.dispose();
    _emailCtrl.dispose();
    _passCtrl.dispose();
    _codeCtrl.dispose();
    super.dispose();
  }

  // ── Step 1: sign-up ─────────────────────────────────────────────────────────

  Future<void> _signUp() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() { _loading = true; _error = null; });
    try {
      _registeredEmail = _emailCtrl.text.trim();
      await _cognito.signUp(_registeredEmail, _passCtrl.text, _nameCtrl.text.trim());
      setState(() => _step = _Step.confirm);
    } on CognitoClientException catch (e) {
      setState(() => _error = _signUpError(e.code));
    } catch (_) {
      setState(() => _error = 'Sign-up failed. Please try again.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  // ── Step 2: confirm code → auto sign-in ─────────────────────────────────────

  Future<void> _confirm() async {
    final code = _codeCtrl.text.trim();
    if (code.length < 6) {
      setState(() => _error = 'Enter the 6-digit code from your email.');
      return;
    }
    setState(() { _loading = true; _error = null; });
    try {
      await _cognito.confirmSignUp(_registeredEmail, code);
      await ref.read(authStateProvider.notifier).signIn(_registeredEmail, _passCtrl.text);
      // GoRouter redirect handles navigation on auth state change
    } on CognitoClientException catch (e) {
      setState(() => _error = _confirmError(e.code));
    } catch (_) {
      setState(() => _error = 'Confirmation failed. Please try again.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _signUpError(String? c) => switch (c) {
    'UsernameExistsException'   => 'An account with this email already exists.',
    'InvalidPasswordException'  => 'Password must be ≥8 chars with numbers and symbols.',
    'InvalidParameterException' => 'Check your email and password format.',
    _                           => 'Sign-up failed. Please try again.',
  };

  String _confirmError(String? c) => switch (c) {
    'CodeMismatchException'          => 'Incorrect code. Check your email.',
    'ExpiredCodeException'           => 'Code expired. Request a new one.',
    'TooManyFailedAttemptsException' => 'Too many attempts. Please wait.',
    _                                => 'Confirmation failed. Please try again.',
  };

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Create Account')),
    body: SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: _step == _Step.form ? _buildForm(context) : _buildConfirm(context),
      ),
    ),
  );

  Widget _buildForm(BuildContext context) => Form(
    key: _formKey,
    child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      const SizedBox(height: 8),
      Text('Join JbNet',
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold)),
      const SizedBox(height: 4),
      Text('Get referred to your dream job', style: Theme.of(context).textTheme.bodyLarge),
      const SizedBox(height: 32),
      TextFormField(
        controller: _nameCtrl,
        decoration: const InputDecoration(labelText: 'Full name'),
        textCapitalization: TextCapitalization.words,
        textInputAction: TextInputAction.next,
        validator: (v) => (v == null || v.trim().isEmpty) ? 'Full name required.' : null,
      ),
      const SizedBox(height: 16),
      TextFormField(
        controller: _emailCtrl,
        decoration: const InputDecoration(labelText: 'Email'),
        keyboardType: TextInputType.emailAddress,
        autocorrect: false,
        textInputAction: TextInputAction.next,
        validator: (v) {
          if (v == null || v.isEmpty) return 'Email required.';
          if (!v.contains('@')) return 'Enter a valid email.';
          return null;
        },
      ),
      const SizedBox(height: 16),
      TextFormField(
        controller: _passCtrl,
        decoration: const InputDecoration(
          labelText: 'Password',
          helperText: 'At least 8 characters, with a number and symbol.',
        ),
        obscureText: true,
        textInputAction: TextInputAction.done,
        onFieldSubmitted: (_) => _signUp(),
        validator: (v) => (v == null || v.length < 8) ? 'Min 8 characters.' : null,
      ),
      if (_error != null) ...[
        const SizedBox(height: 12),
        Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
      ],
      const SizedBox(height: 24),
      FilledButton(
        onPressed: _loading ? null : _signUp,
        child: _loading
            ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : const Text('Create account'),
      ),
      const SizedBox(height: 16),
      TextButton(
        onPressed: () => context.go('/auth/login'),
        child: const Text('Already have an account? Sign in'),
      ),
    ]),
  );

  Widget _buildConfirm(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const SizedBox(height: 8),
      Text('Check your email',
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold)),
      const SizedBox(height: 8),
      Text('We sent a 6-digit code to $_registeredEmail.',
          style: Theme.of(context).textTheme.bodyLarge),
      const SizedBox(height: 32),
      TextField(
        controller: _codeCtrl,
        decoration: const InputDecoration(labelText: 'Verification code'),
        keyboardType: TextInputType.number,
        textInputAction: TextInputAction.done,
        onSubmitted: (_) => _confirm(),
        maxLength: 6,
      ),
      if (_error != null) ...[
        const SizedBox(height: 8),
        Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
      ],
      const SizedBox(height: 24),
      FilledButton(
        onPressed: _loading ? null : _confirm,
        child: _loading
            ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : const Text('Verify & sign in'),
      ),
    ],
  );
}
