import 'package:amazon_cognito_identity_dart_2/cognito.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'cognito_service.dart';

/// Immutable snapshot of the current authentication state.
class AuthState {
  final bool isAuthenticated;
  final String? userId;
  final String? accessToken;

  const AuthState({
    this.isAuthenticated = false,
    this.userId,
    this.accessToken,
  });

  AuthState copyWith({bool? isAuthenticated, String? userId, String? accessToken}) =>
      AuthState(
        isAuthenticated: isAuthenticated ?? this.isAuthenticated,
        userId: userId ?? this.userId,
        accessToken: accessToken ?? this.accessToken,
      );
}

/// Riverpod notifier that manages authentication state.
/// Restores session from secure storage on construction.
class AuthStateNotifier extends StateNotifier<AuthState> {
  final CognitoService _cognito;

  AuthStateNotifier([CognitoService? cognito])
      : _cognito = cognito ?? CognitoService(),
        super(const AuthState()) {
    _restore();
  }

  /// Restores any previously stored Cognito session.
  Future<void> _restore() async {
    final token  = await _cognito.getAccessToken();
    final userId = await _cognito.getUserId();
    if (token != null && token.isNotEmpty && userId != null) {
      state = AuthState(isAuthenticated: true, userId: userId, accessToken: token);
    }
  }

  /// Signs in via Cognito SRP. Throws [CognitoClientException] on failure.
  Future<void> signIn(String email, String password) async {
    final userId = await _cognito.signIn(email, password);
    final token  = await _cognito.getAccessToken();
    state = AuthState(isAuthenticated: true, userId: userId, accessToken: token);
  }

  /// Signs out, clears tokens, and resets state.
  /// [email] is used for Cognito global sign-out; if omitted (e.g. on token
  /// expiry from the API interceptor), local storage is still cleared.
  Future<void> signOut([String email = '']) async {
    await _cognito.signOut(email);
    state = const AuthState();
  }

  /// For test overrides and post-confirm auto-sign-in flows.
  void setAuthenticated({required String userId, required String accessToken}) {
    state = state.copyWith(isAuthenticated: true, userId: userId, accessToken: accessToken);
  }
}

final authStateProvider = StateNotifierProvider<AuthStateNotifier, AuthState>(
  (ref) => AuthStateNotifier(),
);

/// Typed Cognito exception — exposed so UI can format error messages.
typedef CognitoError = CognitoClientException;
