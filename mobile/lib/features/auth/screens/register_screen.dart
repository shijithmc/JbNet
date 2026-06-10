import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/auth_exception.dart';
import '../../../core/auth_state.dart';
import '../../../core/cognito_service.dart';
import '../../../core/validators.dart';

// FA-012: No `amazon_cognito_identity_dart_2` import — screens depend only
// on the domain AuthException hierarchy.
// FA-009: CognitoService injected via cognitoServiceProvider instead of
// constructed inline with `final _cognito = CognitoService()`.

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
      // FA-009: read from provider — never construct CognitoService directly.
      await ref
          .read(cognitoServiceProvider)
          .signUp(_registeredEmail, _passCtrl.text, _nameCtrl.text.trim());
      setState(() => _step = _Step.confirm);
    } on AuthException catch (e) {
      setState(() => _error = e.message);
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
      await ref
          .read(cognitoServiceProvider)
          .confirmSignUp(_registeredEmail, code);
      await ref
          .read(authStateProvider.notifier)
          .signIn(_registeredEmail, _passCtrl.text);
      // GoRouter redirect handles navigation on auth state change
    } on AuthException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Confirmation failed. Please try again.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Create Account')),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: _step == _Step.form
                ? _buildForm(context)
                : _buildConfirm(context),
          ),
        ),
      );

  Widget _buildForm(BuildContext context) => Form(
        key: _formKey,
        child:
            Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const SizedBox(height: 8),
          Text('Join JbNet',
              style: Theme.of(context)
                  .textTheme
                  .headlineMedium
                  ?.copyWith(fontWeight: FontWeight.bold)),
          const SizedBox(height: 4),
          Text('Get referred to your dream job',
              style: Theme.of(context).textTheme.bodyLarge),
          const SizedBox(height: 32),
          TextFormField(
            controller: _nameCtrl,
            decoration: const InputDecoration(labelText: 'Full name'),
            textCapitalization: TextCapitalization.words,
            textInputAction: TextInputAction.next,
            validator: (v) => requiredField(v, 'Full name'),
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _emailCtrl,
            decoration: const InputDecoration(labelText: 'Email'),
            keyboardType: TextInputType.emailAddress,
            autocorrect: false,
            textInputAction: TextInputAction.next,
            validator: emailValidator,
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
            validator: passwordValidator,
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _loading ? null : _signUp,
            child: _loading
                ? const SizedBox.square(
                    dimension: 20,
                    child: CircularProgressIndicator(strokeWidth: 2))
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
              style: Theme.of(context)
                  .textTheme
                  .headlineMedium
                  ?.copyWith(fontWeight: FontWeight.bold)),
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
            Text(_error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _loading ? null : _confirm,
            child: _loading
                ? const SizedBox.square(
                    dimension: 20,
                    child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Verify & sign in'),
          ),
        ],
      );
}
