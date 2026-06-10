import 'package:amazon_cognito_identity_dart_2/cognito.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Wraps Amazon Cognito SRP authentication and token lifecycle management.
///
/// Pool ID and client ID are injected via `--dart-define` at build time:
///   flutter run \
///     --dart-define=COGNITO_USER_POOL_ID=ap-south-1_XXXXXXXXX \
///     --dart-define=COGNITO_CLIENT_ID=XXXXXXXXXXXXXXXXXXX
class CognitoService {
  static const _poolId =
      String.fromEnvironment('COGNITO_USER_POOL_ID', defaultValue: '');
  static const _clientId =
      String.fromEnvironment('COGNITO_CLIENT_ID', defaultValue: '');

  static const _accessTokenKey  = 'cognito_access_token';
  static const _idTokenKey      = 'cognito_id_token';
  static const _refreshTokenKey = 'cognito_refresh_token';
  static const _userIdKey       = 'cognito_user_id';

  final _storage = const FlutterSecureStorage();
  late final CognitoUserPool _pool;

  CognitoService() {
    _pool = CognitoUserPool(_poolId, _clientId);
  }

  // ── Sign-in ─────────────────────────────────────────────────────────────────

  /// Authenticates via SRP. Returns the user sub (UUID) on success.
  /// Throws [CognitoClientException] with an [authenticationError] message on failure.
  Future<String> signIn(String email, String password) async {
    final cognitoUser      = CognitoUser(email, _pool);
    final authDetails      = AuthenticationDetails(
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
    ]);

    return userId;
  }

  // ── Sign-up ─────────────────────────────────────────────────────────────────

  /// Initiates sign-up. Returns the user sub once the confirmation code is sent.
  Future<String> signUp(String email, String password, String fullName) async {
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
  }

  /// Confirms sign-up with the emailed verification code.
  Future<void> confirmSignUp(String email, String code) async {
    final cognitoUser = CognitoUser(email, _pool);
    await cognitoUser.confirmRegistration(code);
  }

  // ── Sign-out ────────────────────────────────────────────────────────────────

  /// Clears local tokens and global-signs-out from Cognito.
  /// [email] is best-effort — if empty or invalid, globalSignOut is skipped
  /// gracefully and local storage is still cleared.
  Future<void> signOut([String email = '']) async {
    try {
      final cognitoUser = CognitoUser(email, _pool);
      await cognitoUser.globalSignOut();
    } catch (_) {
      // Best-effort — always clear local storage
    }
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _idTokenKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _userIdKey),
    ]);
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
}
