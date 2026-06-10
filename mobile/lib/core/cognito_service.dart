import 'dart:convert';

import 'package:amazon_cognito_identity_dart_2/cognito.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'auth_exception.dart';

/// Result returned by [CognitoService.tryRestoreSession] when a valid (or
/// freshly refreshed) session is found in secure storage.
class RestoreResult {
  final String userId;
  final String accessToken;
  const RestoreResult({required this.userId, required this.accessToken});
}

/// Wraps Amazon Cognito SRP authentication and token lifecycle management.
///
/// Pool ID and client ID are injected via `--dart-define` at build time:
///   flutter run \
///     --dart-define=COGNITO_USER_POOL_ID=ap-south-1_XXXXXXXXX \
///     --dart-define=COGNITO_CLIENT_ID=XXXXXXXXXXXXXXXXXXX
///
/// All [CognitoClientException] values are mapped to typed [AuthException]
/// subclasses before they propagate — callers need not import the Cognito
/// package. FA-012.
class CognitoService {
  static const _poolId =
      String.fromEnvironment('COGNITO_USER_POOL_ID', defaultValue: '');
  static const _clientId =
      String.fromEnvironment('COGNITO_CLIENT_ID', defaultValue: '');

  static const _accessTokenKey  = 'cognito_access_token';
  static const _idTokenKey      = 'cognito_id_token';
  static const _refreshTokenKey = 'cognito_refresh_token';
  static const _userIdKey       = 'cognito_user_id';
  static const _emailKey        = 'cognito_email'; // FA-006: stored for globalSignOut

  final _storage = const FlutterSecureStorage();
  late final CognitoUserPool _pool;

  CognitoService() {
    _pool = CognitoUserPool(_poolId, _clientId);
  }

  /// Test-only constructor — bypasses pool-ID format validation.
  /// Not intended for production use.
  @visibleForTesting
  CognitoService.forTesting() {
    // 'us-east-1_TESTFAKE0' satisfies the Cognito pool-ID regex without
    // requiring valid dart-define values at test time.
    _pool = CognitoUserPool('us-east-1_TESTFAKE0', 'fake-client-id');
  }

  // ── Sign-in ─────────────────────────────────────────────────────────────────

  /// Authenticates via SRP. Returns the user sub (UUID) on success.
  /// Throws an [AuthException] subclass on failure — no Cognito types exposed.
  Future<String> signIn(String email, String password) async {
    try {
      final cognitoUser = CognitoUser(email, _pool);
      final authDetails = AuthenticationDetails(
        username: email,
        password: password,
      );
      // authenticateUser returns CognitoUserSession? but throws on auth failure,
      // so null here means an unexpected state — assert non-null.
      final session = (await cognitoUser.authenticateUser(authDetails))!;

      final accessToken  = session.accessToken.jwtToken  ?? '';
      final idToken      = session.idToken.jwtToken      ?? '';
      final refreshToken = session.refreshToken?.token   ?? '';
      final userId       = session.idToken.getSub()      ?? '';

      await Future.wait([
        _storage.write(key: _accessTokenKey,  value: accessToken),
        _storage.write(key: _idTokenKey,      value: idToken),
        _storage.write(key: _refreshTokenKey, value: refreshToken),
        _storage.write(key: _userIdKey,       value: userId),
        _storage.write(key: _emailKey,        value: email), // FA-006
      ]);

      return userId;
    } on CognitoClientException catch (e) {
      throw _mapCognito(e.code);
    }
  }

  // ── Sign-up ─────────────────────────────────────────────────────────────────

  /// Initiates sign-up. Returns the user sub once the confirmation code is sent.
  Future<String> signUp(String email, String password, String fullName) async {
    try {
      final userAttributes = [
        AttributeArg(name: 'name',  value: fullName),
        AttributeArg(name: 'email', value: email),
      ];
      final data = await _pool.signUp(
        email,
        password,
        userAttributes: userAttributes,
      );
      return data.userSub ?? '';
    } on CognitoClientException catch (e) {
      throw _mapCognito(e.code);
    }
  }

  /// Confirms sign-up with the emailed verification code.
  Future<void> confirmSignUp(String email, String code) async {
    try {
      final cognitoUser = CognitoUser(email, _pool);
      await cognitoUser.confirmRegistration(code);
    } on CognitoClientException catch (e) {
      throw _mapCognito(e.code);
    }
  }

  // ── Session restore with JWT expiry check ────────────────────────────────────

  /// Tries to restore a previously saved session from secure storage.
  ///
  /// - If the stored access token is still valid, returns [RestoreResult].
  /// - If the access token is expired, attempts a silent token refresh.
  ///   On success, returns [RestoreResult] with the new token.
  /// - If no session exists or refresh fails, returns null and clears storage.
  ///
  /// FA-005 + FA-013.
  Future<RestoreResult?> tryRestoreSession() async {
    final email       = await _storage.read(key: _emailKey);
    final accessToken = await _storage.read(key: _accessTokenKey);
    final userId      = await _storage.read(key: _userIdKey);

    if (email == null || accessToken == null || userId == null ||
        email.isEmpty || accessToken.isEmpty || userId.isEmpty) {
      return null;
    }

    // Token still valid — return immediately without a network call.
    if (!_isExpired(accessToken)) {
      return RestoreResult(userId: userId, accessToken: accessToken);
    }

    // Access token expired — attempt silent refresh using stored refresh token.
    final refreshed = await refreshSession();
    if (!refreshed) {
      await _clearStorage();
      return null;
    }

    final newToken = await _storage.read(key: _accessTokenKey);
    if (newToken == null || newToken.isEmpty) return null;
    return RestoreResult(userId: userId, accessToken: newToken);
  }

  // ── Token refresh ────────────────────────────────────────────────────────────

  /// Silently refreshes the Cognito access token using the stored refresh token.
  ///
  /// Returns true on success (new token written to storage), false otherwise.
  /// FA-004.
  Future<bool> refreshSession() async {
    try {
      final email           = await _storage.read(key: _emailKey);
      final refreshTokenStr = await _storage.read(key: _refreshTokenKey);

      if (email == null || email.isEmpty ||
          refreshTokenStr == null || refreshTokenStr.isEmpty) {
        return false;
      }

      final cognitoUser  = CognitoUser(email, _pool);
      final refreshToken = CognitoRefreshToken(refreshTokenStr);
      final newSession   = await cognitoUser.refreshSession(refreshToken);
      if (newSession == null) return false;

      final writes = [
        _storage.write(
            key: _accessTokenKey,
            value: newSession.accessToken.jwtToken ?? ''),
        _storage.write(
            key: _idTokenKey,
            value: newSession.idToken.jwtToken ?? ''),
      ];
      // Refresh token may or may not be rotated depending on the Cognito pool config.
      final newRefresh = newSession.refreshToken?.token;
      if (newRefresh != null && newRefresh.isNotEmpty) {
        writes.add(_storage.write(key: _refreshTokenKey, value: newRefresh));
      }
      await Future.wait(writes);
      return true;
    } catch (_) {
      return false;
    }
  }

  // ── Sign-out ────────────────────────────────────────────────────────────────

  /// Invalidates the Cognito session on the server (global sign-out) and
  /// clears all locally stored tokens.
  ///
  /// FA-006: always reads the stored email so globalSignOut succeeds even when
  /// called from the Dio 401 interceptor with no email parameter.
  Future<void> signOut([String email = '']) async {
    // Use the passed email first; fall back to what was stored on sign-in.
    final resolvedEmail =
        email.isNotEmpty ? email : (await _storage.read(key: _emailKey) ?? '');

    try {
      if (resolvedEmail.isNotEmpty) {
        final cognitoUser = CognitoUser(resolvedEmail, _pool);
        await cognitoUser.globalSignOut();
      }
    } catch (_) {
      // Best-effort server invalidation — always clear local storage below.
    }
    await _clearStorage();
  }

  // ── Token access ────────────────────────────────────────────────────────────

  /// Returns the stored access token or null if not signed in.
  Future<String?> getAccessToken() => _storage.read(key: _accessTokenKey);

  /// Returns the stored user sub or null if not signed in.
  Future<String?> getUserId() => _storage.read(key: _userIdKey);

  /// Returns true if a non-empty access token is stored.
  Future<bool> isSignedIn() async {
    final token = await _storage.read(key: _accessTokenKey);
    return token != null && token.isNotEmpty;
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  Future<void> _clearStorage() => Future.wait([
        _storage.delete(key: _accessTokenKey),
        _storage.delete(key: _idTokenKey),
        _storage.delete(key: _refreshTokenKey),
        _storage.delete(key: _userIdKey),
        _storage.delete(key: _emailKey),
      ]);

  /// Decodes the JWT [exp] claim and returns true if the token has expired.
  /// Returns true on any decoding error — safe-fail to unauthenticated. FA-013.
  static bool _isExpired(String token) {
    try {
      final parts = token.split('.');
      if (parts.length != 3) return true;

      // JWT uses base64url encoding without padding.
      var payload = parts[1].replaceAll('-', '+').replaceAll('_', '/');
      switch (payload.length % 4) {
        case 2:  payload += '=='; break;
        case 3:  payload += '=';  break;
        default: break;
      }

      final decoded  = utf8.decode(base64.decode(payload));
      final json     = jsonDecode(decoded) as Map<String, dynamic>;
      final exp      = json['exp'] as int?;
      if (exp == null) return true;

      // 30-second buffer to account for clock skew.
      return DateTime.now().millisecondsSinceEpoch > (exp * 1000) - 30000;
    } catch (_) {
      return true;
    }
  }

  /// Maps a Cognito exception code to a typed [AuthException]. FA-012.
  static AuthException _mapCognito(String? code) => switch (code) {
        'NotAuthorizedException'         => const InvalidCredentialsException(),
        'UserNotFoundException'          => const UserNotFoundException(),
        'UserNotConfirmedException'      => const UserNotConfirmedException(),
        'UsernameExistsException'        => const UsernameExistsException(),
        'InvalidPasswordException'       => const InvalidPasswordException(),
        'InvalidParameterException'      => const InvalidPasswordException(),
        'CodeMismatchException'          => const CodeMismatchException(),
        'ExpiredCodeException'           => const ExpiredCodeException(),
        'TooManyFailedAttemptsException' => const TooManyAttemptsException(),
        'PasswordResetRequiredException' => const PasswordResetRequiredException(),
        _                               => UnknownAuthException(code ?? 'unknown'),
      };
}

/// Global Riverpod provider for [CognitoService].
///
/// Inject via [ref.read(cognitoServiceProvider)] rather than constructing
/// [CognitoService] inline — allows overriding with a fake in tests. FA-009.
final cognitoServiceProvider = Provider<CognitoService>(
  (ref) => CognitoService(),
);
