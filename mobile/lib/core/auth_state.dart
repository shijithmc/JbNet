import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'cognito_service.dart';

/// Immutable snapshot of the current authentication state.
///
/// [isRestoring] is true from construction until [AuthStateNotifier._restore]
/// completes. Routers and UI should hold or show a splash while this is true
/// to prevent a flash-of-login-screen on cold start. FA-005.
class AuthState {
  final bool isRestoring;
  final bool isAuthenticated;
  final String? userId;
  final String? accessToken;

  const AuthState({
    this.isRestoring = true, // hold until _restore() completes
    this.isAuthenticated = false,
    this.userId,
    this.accessToken,
  });

  AuthState copyWith({
    bool? isRestoring,
    bool? isAuthenticated,
    String? userId,
    String? accessToken,
  }) =>
      AuthState(
        isRestoring:     isRestoring     ?? this.isRestoring,
        isAuthenticated: isAuthenticated ?? this.isAuthenticated,
        userId:          userId          ?? this.userId,
        accessToken:     accessToken     ?? this.accessToken,
      );
}

/// Riverpod notifier that manages authentication state.
/// Restores session from secure storage on construction.
class AuthStateNotifier extends StateNotifier<AuthState> {
  final CognitoService _cognito;

  /// Prefer injecting via [cognitoServiceProvider] rather than constructing
  /// directly — supports test overrides. FA-009.
  AuthStateNotifier(this._cognito) : super(const AuthState()) {
    _restore();
  }

  /// Restores any previously saved Cognito session.
  /// Handles JWT expiry check and silent refresh. FA-005 + FA-013.
  Future<void> _restore() async {
    final result = await _cognito.tryRestoreSession();
    if (result != null) {
      state = AuthState(
        isRestoring:     false,
        isAuthenticated: true,
        userId:          result.userId,
        accessToken:     result.accessToken,
      );
    } else {
      state = const AuthState(isRestoring: false);
    }
  }

  /// Signs in via Cognito SRP. Throws an [AuthException] subclass on failure.
  Future<void> signIn(String email, String password) async {
    final userId = await _cognito.signIn(email, password);
    final token  = await _cognito.getAccessToken();
    state = AuthState(
      isRestoring:     false,
      isAuthenticated: true,
      userId:          userId,
      accessToken:     token,
    );
  }

  /// Signs out, clears tokens, and resets state.
  Future<void> signOut([String email = '']) async {
    await _cognito.signOut(email);
    state = const AuthState(isRestoring: false);
  }

  /// Updates state after a transparent token refresh (called by the Dio
  /// interceptor — does not trigger a full sign-in flow). FA-004.
  void setAuthenticated({
    required String userId,
    required String accessToken,
  }) {
    state = state.copyWith(
      isAuthenticated: true,
      userId:          userId,
      accessToken:     accessToken,
    );
  }
}

final authStateProvider = StateNotifierProvider<AuthStateNotifier, AuthState>(
  (ref) => AuthStateNotifier(ref.read(cognitoServiceProvider)),
);
