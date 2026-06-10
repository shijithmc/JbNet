import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:jbnet/core/auth_exception.dart';
import 'package:jbnet/core/auth_state.dart';
import 'package:jbnet/core/cognito_service.dart';
import 'package:jbnet/main.dart';

// ── Fake CognitoService ───────────────────────────────────────────────────────
//
// Overrides the three async operations that touch FlutterSecureStorage or the
// Cognito network. Used via [cognitoServiceProvider.overrideWith] so that
// widget tests run without platform channels or a real Cognito pool.

class _FakeCognitoService extends CognitoService {
  final bool _startAuthenticated;
  final bool _signInSucceeds;

  _FakeCognitoService({
    bool startAuthenticated = false,
    bool signInSucceeds = true,
  })  : _startAuthenticated = startAuthenticated,
        _signInSucceeds = signInSucceeds,
        super.forTesting();

  @override
  Future<RestoreResult?> tryRestoreSession() async {
    if (_startAuthenticated) {
      return const RestoreResult(
          userId: 'test-user-id', accessToken: 'fake-access-token');
    }
    return null;
  }

  @override
  Future<String> signIn(String email, String password) async {
    if (!_signInSucceeds) throw const InvalidCredentialsException();
    return 'test-user-id';
  }

  @override
  Future<String?> getAccessToken() async =>
      _signInSucceeds ? 'fake-access-token' : null;

  @override
  Future<bool> refreshSession() async => false;

  @override
  Future<void> signOut([String email = '']) async {}
}

// ── Helpers ───────────────────────────────────────────────────────────────────

ProviderScope _appWithFakeCognito({
  bool startAuthenticated = false,
  bool signInSucceeds = true,
}) {
  return ProviderScope(
    overrides: [
      cognitoServiceProvider.overrideWithValue(
        _FakeCognitoService(
          startAuthenticated: startAuthenticated,
          signInSucceeds: signInSucceeds,
        ),
      ),
    ],
    child: const JbNetApp(),
  );
}

// ── Tests ─────────────────────────────────────────────────────────────────────

void main() {
  group('App cold-start routing', () {
    testWidgets('unauthenticated user sees login screen', (tester) async {
      await tester.pumpWidget(_appWithFakeCognito(startAuthenticated: false));
      await tester.pumpAndSettle();

      // Splash resolves → /auth/login
      expect(find.text('JbNet'), findsOneWidget);
      expect(find.text('Sign in'), findsOneWidget);
    });

    testWidgets('authenticated user bypasses login', (tester) async {
      await tester.pumpWidget(_appWithFakeCognito(startAuthenticated: true));
      await tester.pumpAndSettle();

      // Splash resolves → /jobs (bottom nav + AppBar both render "Jobs")
      expect(find.text('Jobs'), findsWidgets);
      expect(find.text('Sign in'), findsNothing);
    });
  });

  group('Login screen', () {
    testWidgets('shows error on invalid credentials', (tester) async {
      await tester.pumpWidget(_appWithFakeCognito(signInSucceeds: false));
      await tester.pumpAndSettle();

      await tester.enterText(
          find.widgetWithText(TextFormField, 'Email'), 'wrong@test.com');
      await tester.enterText(
          find.widgetWithText(TextFormField, 'Password'), 'wrongpassword');
      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('Incorrect email or password.'), findsOneWidget);
    });
  });

  group('AuthState', () {
    test('isRestoring defaults to true', () {
      const state = AuthState();
      expect(state.isRestoring, isTrue);
      expect(state.isAuthenticated, isFalse);
    });

    test('copyWith preserves unspecified fields', () {
      const state = AuthState(
        isRestoring: false,
        isAuthenticated: true,
        userId: 'u1',
        accessToken: 'tok',
      );
      final updated = state.copyWith(accessToken: 'tok2');
      expect(updated.isRestoring, isFalse);
      expect(updated.isAuthenticated, isTrue);
      expect(updated.userId, 'u1');
      expect(updated.accessToken, 'tok2');
    });
  });

  group('AuthException', () {
    test('InvalidCredentialsException has correct message', () {
      const e = InvalidCredentialsException();
      expect(e.message, 'Incorrect email or password.');
      expect(e.code, 'NotAuthorizedException');
    });

    test('UnknownAuthException preserves code', () {
      const e = UnknownAuthException('SomeNewCode');
      expect(e.code, 'SomeNewCode');
    });
  });
}
